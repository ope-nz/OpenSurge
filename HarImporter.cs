using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace OpenSurge
{
    static class HarImporter
    {
        // Browser-injected headers that don't belong in a replay
        static readonly HashSet<string> SkipHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // HTTP/2 pseudo-headers
            ":authority", ":method", ":path", ":scheme",
            // Compression -- HttpClient negotiates this itself
            "accept-encoding",
            // Browser cache/prefetch directives
            "cache-control", "pragma",
            // Security/fetch metadata headers
            "sec-fetch-dest", "sec-fetch-mode", "sec-fetch-site", "sec-fetch-user",
            "sec-ch-ua", "sec-ch-ua-mobile", "sec-ch-ua-platform",
            "upgrade-insecure-requests",
        };

        public static List<RequestBlock> Import(string filePath)
        {
            var result = new List<RequestBlock>();
            string json;
            try { json = File.ReadAllText(filePath); }
            catch (Exception ex) { Logger.Error(ex); return result; }

            try
            {
                var root    = JObject.Parse(json);
                var log     = root["log"];
                if (log == null) { Logger.Warn("HAR file has no 'log' key: " + filePath); return result; }
                var entries = log["entries"] as JArray;
                if (entries == null) return result;

                foreach (JToken entry in entries)
                {
                    var req = entry["request"];
                    if (req == null) continue;

                    string method = ((string)req["method"] ?? "GET").ToUpperInvariant();
                    string url    = (string)req["url"] ?? "";
                    if (string.IsNullOrEmpty(url)) continue;

                    // Collect headers, skipping browser-internal ones
                    var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    var hArr = req["headers"] as JArray;
                    if (hArr != null)
                    {
                        foreach (JToken h in hArr)
                        {
                            string hName  = (string)h["name"]  ?? "";
                            string hValue = (string)h["value"] ?? "";
                            if (!string.IsNullOrEmpty(hName) && !SkipHeaders.Contains(hName))
                                headers[hName] = hValue;
                        }
                    }

                    // Body
                    string body = "";
                    var pd = req["postData"];
                    if (pd != null)
                    {
                        body = (string)pd["text"] ?? "";
                        string mime = (string)pd["mimeType"];
                        if (!string.IsNullOrEmpty(mime) && !headers.ContainsKey("Content-Type"))
                            headers["Content-Type"] = mime;
                    }

                    // Derive a short display name from the URL path
                    string name = url;
                    try
                    {
                        var uri = new Uri(url);
                        name = uri.AbsolutePath.TrimStart('/');
                        if (string.IsNullOrEmpty(name)) name = uri.Host;
                    }
                    catch { }

                    result.Add(new RequestBlock
                    {
                        Name    = name,
                        Method  = method,
                        Path    = url,       // full URL -- HttpExecutor detects "http" prefix
                        Headers = headers,
                        Body    = body
                    });
                }
            }
            catch (Exception ex) { Logger.Error(ex); }

            Logger.Info("Imported " + result.Count + " requests from HAR: " + filePath);
            return result;
        }
    }
}
