using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenSurge
{
    class TestStats
    {
        public int RequestsCompleted;
        public int ErrorCount;
    }

    class HttpExecutor
    {
        public readonly TestStats Stats = new TestStats();

        // Raised on a thread-pool thread when the run ends (normally or cancelled).
        // Argument is the CSV file path.
        public event Action<string> TestCompleted;

        readonly object _csvLock = new object();

        public void Run(
            List<RequestBlock> requests,
            string baseUrl,
            string bearerToken,
            int concurrency,
            int durationSeconds,
            int warmupSeconds,
            bool ramp,
            bool shuffle,
            string csvFile,
            CancellationToken cancel)
        {
            Stats.RequestsCompleted = 0;
            Stats.ErrorCount        = 0;

            var semaphore = ramp
                ? new SemaphoreSlim(1, concurrency)
                : new SemaphoreSlim(concurrency);

            var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(120);

            try
            {
                File.WriteAllText(csvFile,
                    "Name,StatusCode,TimeToFirstByteMs,DownloadMs,TotalMs,ResponseSizeBytes,Timestamp\n");

                var tasks = new List<Task>();
                var rng   = new Random();
                int lastConcurrency = ramp ? 1 : concurrency;

                // Warmup: send requests without logging
                if (warmupSeconds > 0)
                {
                    Logger.Info("Warmup started (" + warmupSeconds + "s)");
                    var warmupWatch = Stopwatch.StartNew();
                    var warmupTasks = new List<Task>();
                    int wi = 0;
                    while (warmupWatch.Elapsed.TotalSeconds < warmupSeconds
                           && !cancel.IsCancellationRequested)
                    {
                        if (wi >= requests.Count) wi = 0;
                        semaphore.Wait(cancel);
                        var block = requests[wi];
                        var wt = SendRequestAsync(client, block, baseUrl, bearerToken, csvFile, log: false)
                                     .ContinueWith(_ => semaphore.Release());
                        warmupTasks.Add(wt);
                        warmupTasks.RemoveAll(t => t.IsCompleted);
                        wi++;
                    }
                    try { Task.WaitAll(warmupTasks.ToArray()); } catch { }
                    Logger.Info("Warmup complete");
                }

                LogConcurrency(lastConcurrency, csvFile);

                var stopwatch   = Stopwatch.StartNew();
                int requestIndex = 0;

                try
                {
                    while (stopwatch.Elapsed.TotalSeconds < durationSeconds
                           && !cancel.IsCancellationRequested)
                    {
                        // Ramp: linearly increase concurrency over first 33% of duration
                        if (ramp && lastConcurrency < concurrency)
                        {
                            double fraction = Math.Min(1.0,
                                stopwatch.Elapsed.TotalSeconds / (durationSeconds * 0.33));
                            int target = Math.Min(concurrency, 1 + (int)(fraction * concurrency));
                            if (target > lastConcurrency)
                            {
                                semaphore.Release(target - lastConcurrency);
                                lastConcurrency = target;
                                LogConcurrency(lastConcurrency, csvFile);
                            }
                        }

                        if (requestIndex >= requests.Count)
                        {
                            requestIndex = 0;
                            if (shuffle)
                                requests = requests.OrderBy(_ => rng.Next()).ToList();
                        }

                        semaphore.Wait(cancel);

                        var req  = requests[requestIndex];
                        var task = SendRequestAsync(client, req, baseUrl, bearerToken, csvFile, log: true)
                                       .ContinueWith(_ => semaphore.Release());
                        tasks.Add(task);
                        tasks.RemoveAll(t => t.IsCompleted);

                        requestIndex++;
                    }
                }
                catch (OperationCanceledException) { }

                try { Task.WaitAll(tasks.ToArray()); } catch { }
            }
            finally
            {
                client.Dispose();
                semaphore.Dispose();
                Logger.Info("Test run ended. Requests=" + Stats.RequestsCompleted
                    + " Errors=" + Stats.ErrorCount);
            }

            if (TestCompleted != null)
                TestCompleted(csvFile);
        }

        async Task SendRequestAsync(
            HttpClient client,
            RequestBlock req,
            string baseUrl,
            string bearerToken,
            string csvFile,
            bool log)
        {
            // Resolve URL: absolute path used as-is; relative path prepended with baseUrl
            string url = req.Path.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? req.Path
                : baseUrl.TrimEnd('/') + "/" + req.Path.TrimStart('/');

            string requestName = req.Name;
            string timestamp   = DateTime.UtcNow.ToString("o");

            try
            {
                var message = new HttpRequestMessage(new HttpMethod(req.Method), url);

                // Copy headers (skip Content-Type; it goes on the content object)
                foreach (var h in req.Headers)
                {
                    if (string.Equals(h.Key, "Content-Type", StringComparison.OrdinalIgnoreCase)) continue;
                    if (string.Equals(h.Key, "Websurge-Request-Name", StringComparison.OrdinalIgnoreCase)) continue;
                    try { message.Headers.TryAddWithoutValidation(h.Key, h.Value); } catch { }
                }

                // Auth
                if (!string.IsNullOrEmpty(bearerToken))
                    message.Headers.TryAddWithoutValidation("Authorization", "Bearer " + bearerToken);

                // Body
                if (!string.IsNullOrEmpty(req.Body))
                {
                    string contentType = "application/x-www-form-urlencoded";
                    if (req.Headers.ContainsKey("Content-Type"))
                        contentType = req.Headers["Content-Type"];
                    message.Content = new StringContent(req.Body, Encoding.UTF8, contentType);
                }

                var swTotal   = Stopwatch.StartNew();
                var swConnect = Stopwatch.StartNew();

                HttpResponseMessage response = await client.SendAsync(
                    message, HttpCompletionOption.ResponseHeadersRead);

                swConnect.Stop();
                long ttfbMs = swConnect.ElapsedMilliseconds;

                var swDownload = Stopwatch.StartNew();
                byte[] body = await response.Content.ReadAsByteArrayAsync();
                swDownload.Stop();
                swTotal.Stop();

                long downloadMs  = swDownload.ElapsedMilliseconds;
                long totalMs     = swTotal.ElapsedMilliseconds;
                long size        = body.Length;
                string statusCode = ((int)response.StatusCode).ToString();

                Interlocked.Increment(ref Stats.RequestsCompleted);
                string sc0 = statusCode.Length > 0 ? statusCode[0].ToString() : "";
                if (statusCode == "ERROR" || sc0 == "4" || sc0 == "5")
                    Interlocked.Increment(ref Stats.ErrorCount);

                if (log)
                {
                    string row = requestName + "," + statusCode + "," + ttfbMs + ","
                               + downloadMs + "," + totalMs + "," + size + "," + timestamp + "\n";
                    lock (_csvLock) { File.AppendAllText(csvFile, row); }
                }
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref Stats.ErrorCount);
                if (log)
                {
                    string row = requestName + ",ERROR,,,,," + timestamp + "\n";
                    lock (_csvLock) { File.AppendAllText(csvFile, row); }
                }
                Logger.Debug("Request error: " + ex.Message);
            }
        }

        void LogConcurrency(int threads, string csvFile)
        {
            string row = "__CONCURRENCY__," + threads + ",,,,,"
                       + DateTime.UtcNow.ToString("o") + "\n";
            lock (_csvLock) { File.AppendAllText(csvFile, row); }
        }
    }
}
