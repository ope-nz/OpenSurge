using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenSurge
{
    static class ReportGenerator
    {
        public static void Generate(string csvFile)
        {
            string htmlFile = csvFile.Replace(".csv", ".html");
            string[] allLines = File.ReadAllLines(csvFile);
            if (allLines.Length < 2) return;

            // --- Accumulators ---
            var ttfbList      = new List<long>();
            var downloadList  = new List<long>();
            var totalMsList   = new List<long>();
            var scatterPoints = new List<long[]>();   // [unixMs, totalMs]
            var pathTotals    = new Dictionary<string, List<long>>();
            var pathTtfbs     = new Dictionary<string, List<long>>();
            var pathDownloads = new Dictionary<string, List<long>>();
            var pathAllCount  = new Dictionary<string, int>();
            var pathErrCount  = new Dictionary<string, int>();
            var statusCounts  = new Dictionary<string, int>();
            long totalBytes   = 0;
            int  errorCount   = 0;
            long firstUnixMs  = long.MaxValue;
            var concSteps     = new List<long[]>();   // [unixMs, threadCount]
            int concRowCount  = 0;
            var errorPoints   = new List<long>();
            var successPoints = new List<long>();

            // CSV: Name,StatusCode,TimeToFirstByteMs,DownloadMs,TotalMs,ResponseSizeBytes,Timestamp
            foreach (string line in allLines.Skip(1))
            {
                string[] parts = line.Split(',');
                if (parts.Length < 7) continue;

                string rawName = parts[0];

                if (rawName == "__CONCURRENCY__")
                {
                    concRowCount++;
                    int threads; DateTime cts;
                    if (int.TryParse(parts[1], out threads) && DateTime.TryParse(parts[6], out cts))
                    {
                        cts = cts.ToUniversalTime();
                        long cMs = (long)(cts - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
                        concSteps.Add(new long[] { cMs, threads });
                    }
                    continue;
                }

                // Strip optional spatial suffix encoded by Qonda ("|id|cx|cy|scale")
                // For generic OpenSurge use, strip if present so the name is clean
                string path = rawName;
                if (rawName.IndexOf('|') >= 0)
                    path = rawName.Substring(0, rawName.IndexOf('|'));

                string status = parts[1];

                if (!statusCounts.ContainsKey(status)) statusCounts[status] = 0;
                statusCounts[status]++;

                if (!pathAllCount.ContainsKey(path)) pathAllCount[path] = 0;
                pathAllCount[path]++;

                bool isNetError  = status == "ERROR";
                bool isHttpError = status.Length == 3 && (status[0] == '4' || status[0] == '5');
                if (isNetError || isHttpError)
                {
                    errorCount++;
                    if (!pathErrCount.ContainsKey(path)) pathErrCount[path] = 0;
                    pathErrCount[path]++;
                    if (isNetError)
                    {
                        DateTime ets;
                        if (DateTime.TryParse(parts[6], out ets))
                        {
                            ets = ets.ToUniversalTime();
                            errorPoints.Add((long)(ets - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds);
                        }
                        continue;
                    }
                }

                long ttfb, download, total, size;
                DateTime ts;
                if (!long.TryParse(parts[2], out ttfb))      continue;
                if (!long.TryParse(parts[3], out download))  continue;
                if (!long.TryParse(parts[4], out total))     continue;
                if (!long.TryParse(parts[5], out size))      size = 0;
                if (!DateTime.TryParse(parts[6], out ts))    continue;

                ts = ts.ToUniversalTime();
                long unixMs = (long)(ts - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
                if (unixMs < firstUnixMs) firstUnixMs = unixMs;

                if (isHttpError) errorPoints.Add(unixMs);
                else             successPoints.Add(unixMs);

                ttfbList.Add(ttfb);
                downloadList.Add(download);
                totalMsList.Add(total);
                totalBytes += size;
                scatterPoints.Add(new long[] { unixMs, total });

                if (!pathTotals.ContainsKey(path))    pathTotals[path]    = new List<long>();
                if (!pathTtfbs.ContainsKey(path))     pathTtfbs[path]     = new List<long>();
                if (!pathDownloads.ContainsKey(path)) pathDownloads[path] = new List<long>();
                pathTotals[path].Add(total);
                pathTtfbs[path].Add(ttfb);
                pathDownloads[path].Add(download);
            }

            if (totalMsList.Count == 0) return;

            scatterPoints.Sort((a, b) => a[0].CompareTo(b[0]));

            double durationSec = firstUnixMs < long.MaxValue && scatterPoints.Count > 0
                ? (scatterPoints[scatterPoints.Count - 1][0] - firstUnixMs) / 1000.0
                : 1;

            int    totalRequests  = allLines.Length - 1 - concRowCount;
            double rps            = durationSec > 0 ? Math.Round(totalRequests / durationSec, 1) : 0;
            double tps            = durationSec > 0 ? Math.Round((totalRequests - errorCount) / durationSec, 1) : 0;
            double errorRatePct   = totalRequests > 0 ? Math.Round(errorCount * 100.0 / totalRequests, 1) : 0;
            double totalMb        = totalBytes / 1024.0 / 1024.0;
            double throughputKbps = durationSec > 0 ? Math.Round(totalBytes / 1024.0 / durationSec, 1) : 0;

            totalMsList.Sort();
            ttfbList.Sort();
            downloadList.Sort();

            long avgTotal    = (long)Math.Round(totalMsList.Average());
            long avgTtfb     = (long)Math.Round(ttfbList.Average());
            long avgDownload = (long)Math.Round(downloadList.Average());
            long minTotal    = totalMsList[0];
            long maxTotal    = totalMsList[totalMsList.Count - 1];
            long p50 = totalMsList[totalMsList.Count / 2];
            long p95 = totalMsList[Math.Min((int)(totalMsList.Count * 0.95), totalMsList.Count - 1)];
            long p99 = totalMsList[Math.Min((int)(totalMsList.Count * 0.99), totalMsList.Count - 1)];
            long outlierThreshold = p95;

            // Scatter: always keep outliers; sample normal points down to 2000
            var scatterNorm = scatterPoints.Where(p => p[1] <= outlierThreshold).ToList();
            var scatterOut  = scatterPoints.Where(p => p[1] >  outlierThreshold).ToList();
            int step = Math.Max(1, scatterNorm.Count / 2000);
            var sampledNorm = scatterNorm.Where((p, i) => i % step == 0).ToList();
            Func<long[], string> toScatterPt = p =>
                "{\"x\":" + ((p[0] - firstUnixMs) / 1000.0).ToString("F2", CultureInfo.InvariantCulture) +
                ",\"y\":" + p[1] + "}";
            string scatterNormJson = "[" + string.Join(",", sampledNorm.Select(p => toScatterPt(p))) + "]";
            string scatterOutJson  = "[" + string.Join(",", scatterOut.Select(p => toScatterPt(p)))  + "]";

            // Concurrency step chart
            var concPts = new List<string>();
            if (concSteps.Count > 0 && firstUnixMs < long.MaxValue)
            {
                foreach (var cs in concSteps)
                {
                    double elapsedSec = Math.Max(0, (cs[0] - firstUnixMs) / 1000.0);
                    concPts.Add("{\"x\":" + elapsedSec.ToString("F1", CultureInfo.InvariantCulture)
                              + ",\"y\":" + cs[1] + "}");
                }
                concPts.Add("{\"x\":" + durationSec.ToString("F1", CultureInfo.InvariantCulture)
                          + ",\"y\":" + concSteps[concSteps.Count - 1][1] + "}");
            }
            string concurrencyJson = "[" + string.Join(",", concPts) + "]";

            // Success/error counts per second
            string errorLineJson   = "[]";
            string successLineJson = "[]";
            if (firstUnixMs < long.MaxValue)
            {
                Func<List<long>, string> bucketBySec = delegate(List<long> pts) {
                    if (pts.Count == 0) return "[]";
                    var bySec = new Dictionary<int, int>();
                    foreach (long ep in pts)
                    {
                        int sec = (int)((ep - firstUnixMs) / 1000);
                        if (!bySec.ContainsKey(sec)) bySec[sec] = 0;
                        bySec[sec]++;
                    }
                    return "[" + string.Join(",", bySec.Keys.OrderBy(k => k).Select(sec =>
                        "{\"x\":" + sec.ToString(CultureInfo.InvariantCulture)
                        + ",\"y\":" + bySec[sec] + "}")) + "]";
                };
                errorLineJson   = bucketBySec(errorPoints);
                successLineJson = bucketBySec(successPoints);
            }

            // Histogram (20 buckets)
            const int BucketCount = 20;
            long bucketSize = Math.Max(1, maxTotal / BucketCount);
            int[] histogram = new int[BucketCount + 1];
            foreach (long v in totalMsList)
                histogram[Math.Min((int)(v / bucketSize), BucketCount)]++;
            string histLabels = "[" + string.Join(",",
                Enumerable.Range(0, BucketCount + 1).Select(i => "\"" + (i * bucketSize) + "ms\"")) + "]";
            string histCounts = "[" + string.Join(",", histogram) + "]";

            // Per-path stats ordered by avg total descending
            var pathStats = pathTotals.Keys.Select(key =>
            {
                var sorted = pathTotals[key].OrderBy(v => v).ToList();
                string label = key.Length > 55 ? "..." + key.Substring(key.Length - 52) : key;
                long pavg     = (long)Math.Round(pathTotals[key].Average());
                long pp95     = sorted[Math.Min((int)(sorted.Count * 0.95), sorted.Count - 1)];
                long pAvgTtfb = pathTtfbs[key].Count > 0
                                ? (long)Math.Round(pathTtfbs[key].Average()) : 0;
                long pAvgDl   = pathDownloads[key].Count > 0
                                ? (long)Math.Round(pathDownloads[key].Average()) : 0;
                int  pcnt     = pathTotals[key].Count;
                int  perrs    = pathErrCount.ContainsKey(key) ? pathErrCount[key] : 0;
                int  pall     = pathAllCount.ContainsKey(key) ? pathAllCount[key] : pcnt;
                return new { Key = key, Label = label, Avg = pavg, P95 = pp95,
                             AvgTtfb = pAvgTtfb, AvgDl = pAvgDl, Count = pcnt,
                             All = pall, Errs = perrs };
            }).OrderByDescending(p => p.Avg).ToList();

            string pathLabels   = "[" + string.Join(",", pathStats.Select(p => "\"" + EscapeJs(p.Label) + "\"")) + "]";
            string pathAvgData  = "[" + string.Join(",", pathStats.Select(p => p.Avg))     + "]";
            string pathP95Data  = "[" + string.Join(",", pathStats.Select(p => p.P95))     + "]";
            string pathTtfbData = "[" + string.Join(",", pathStats.Select(p => p.AvgTtfb)) + "]";
            string pathDlData   = "[" + string.Join(",", pathStats.Select(p => p.AvgDl))   + "]";
            string pathCntData  = "[" + string.Join(",", pathStats.Select(p => p.Count))   + "]";

            // Status distribution
            string statusLabels = "[" + string.Join(",", statusCounts.Keys.Select(k => "\"" + k + "\"")) + "]";
            string statusData   = "[" + string.Join(",", statusCounts.Values) + "]";
            string statusColors = "[" + string.Join(",", statusCounts.Keys.Select(k =>
                k == "200"        ? "\"rgba(46,204,113,0.8)\""  :
                k == "ERROR"      ? "\"rgba(231,76,60,0.8)\""   :
                k.StartsWith("4") ? "\"rgba(230,126,34,0.8)\""  :
                k.StartsWith("5") ? "\"rgba(231,76,60,0.8)\""   :
                                     "\"rgba(52,152,219,0.8)\"")) + "]";

            // Endpoint table rows
            var endpointRows = new StringBuilder();
            foreach (var ps in pathStats)
            {
                double epct = ps.All > 0 ? Math.Round(ps.Errs * 100.0 / ps.All, 1) : 0;
                string errStyle = ps.Errs > 0 ? " style='color:#e74c3c;font-weight:600'" : "";
                endpointRows.AppendFormat(
                    "<tr><td>{0}</td><td>{1}</td><td{2}>{3}</td><td{2}>{4}%</td>" +
                    "<td>{5}ms</td><td>{6}ms</td><td>{7}ms</td><td>{8}ms</td></tr>",
                    HtmlEncode(ps.Key),
                    ps.All, errStyle, ps.Errs,
                    epct.ToString("F1", CultureInfo.InvariantCulture),
                    ps.AvgTtfb, ps.AvgDl, ps.Avg, ps.P95);
            }

            int pathChartH = Math.Max(220, pathStats.Count * 34 + 60);

            string errClass    = errorCount   > 0 ? " err"  : "";
            string errPctClass = errorRatePct > 5 ? " err"  :
                                 errorRatePct > 0 ? " warn" : "";

            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html lang='en'><head><meta charset='UTF-8'>");
            sb.Append("<meta name='viewport' content='width=device-width,initial-scale=1'>");
            sb.Append("<title>OpenSurge Load Test Report</title>");
            sb.Append("<script src='https://cdn.jsdelivr.net/npm/chart.js@4.4.0/dist/chart.umd.min.js'></script>");
            sb.Append(@"<style>
*{box-sizing:border-box;margin:0;padding:0}
body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;background:#f0f2f5;color:#333}
.wrap{max-width:960px;margin:0 auto;padding-bottom:36px}
.hdr{background:#1e2a35;color:#fff;padding:20px 24px}
.hdr h1{font-size:20px;font-weight:600}
.hdr p{font-size:12px;opacity:.6;margin-top:3px}
.stats{display:flex;flex-wrap:wrap;gap:10px;padding:20px 0}
.stat{background:#fff;border-radius:8px;padding:14px 16px;flex:1;min-width:110px;box-shadow:0 1px 3px rgba(0,0,0,.08)}
.lbl{font-size:10px;text-transform:uppercase;letter-spacing:.5px;color:#888;margin-bottom:5px}
.val{font-size:22px;font-weight:700;color:#1e2a35}
.unit{font-size:11px;color:#aaa;margin-left:2px;font-weight:400}
.err .val{color:#e74c3c}
.warn .val{color:#e67e22}
.charts{display:flex;flex-direction:column;gap:16px}
.box{background:#fff;border-radius:8px;padding:18px;box-shadow:0 1px 3px rgba(0,0,0,.08)}
.box h3{font-size:11px;text-transform:uppercase;letter-spacing:.5px;color:#888;margin-bottom:14px;font-weight:600}
.ch{position:relative}
table.ep{width:100%;border-collapse:collapse;font-size:12px}
table.ep th{background:#1e2a35;color:#fff;padding:7px 10px;text-align:left;font-weight:500;font-size:11px;letter-spacing:.3px}
table.ep td{padding:6px 10px;border-bottom:1px solid #eee;color:#333}
table.ep tr:last-child td{border-bottom:none}
table.ep tr:hover td{background:#f7f9fb}
table.ep td:nth-child(n+2){text-align:right}
table.ep th:nth-child(n+2){text-align:right}
</style></head><body><div class='wrap'>
");

            sb.Append("<div class='hdr'><h1>OpenSurge Load Test Report</h1><p>Generated ");
            sb.Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.Append(" UTC &mdash; " + totalRequests + " requests over "
                      + Math.Round(durationSec, 0) + "s</p></div>");

            // KPI stats bar
            sb.Append("<div class='stats'>");
            Action<string, string, string, string> Stat = (cls, lbl, val, unit) => {
                sb.Append("<div class='stat" + cls + "'>");
                sb.Append("<div class='lbl'>" + lbl + "</div><div class='val'>" + val);
                if (unit != "") sb.Append("<span class='unit'>" + unit + "</span>");
                sb.Append("</div></div>");
            };
            Stat("",         "Req / sec",    rps.ToString("F1", CultureInfo.InvariantCulture), "");
            Stat("",         "Avg TPS",      tps.ToString("F1", CultureInfo.InvariantCulture), "");
            Stat("",         "Avg TTFB",     avgTtfb.ToString(),     "ms");
            Stat("",         "Avg Download", avgDownload.ToString(), "ms");
            Stat("",         "Avg Total",    avgTotal.ToString(),    "ms");
            Stat("",         "p50",          p50.ToString(),         "ms");
            Stat("",         "p95",          p95.ToString(),         "ms");
            Stat("",         "p99",          p99.ToString(),         "ms");
            Stat("",         "Min",          minTotal.ToString(),    "ms");
            Stat("",         "Max",          maxTotal.ToString(),    "ms");
            Stat("",         "Data",         totalMb.ToString("F1", CultureInfo.InvariantCulture), "MB");
            Stat("",         "Throughput",   throughputKbps.ToString("F1", CultureInfo.InvariantCulture), "KB/s");
            Stat(errClass,   "Errors",       errorCount.ToString(), "");
            Stat(errPctClass,"Error Rate",   errorRatePct.ToString("F1", CultureInfo.InvariantCulture), "%");
            sb.Append("</div>");

            // Endpoints table
            sb.Append("<div class='box' style='margin-top:16px'>");
            sb.Append("<h3>Endpoints</h3>");
            sb.Append("<table class='ep'>");
            sb.Append("<thead><tr><th>Endpoint</th><th>Requests</th><th>Errors</th>" +
                      "<th>Error %</th><th>Avg TTFB</th><th>Avg DL</th>" +
                      "<th>Avg Total</th><th>p95</th></tr></thead>");
            sb.Append("<tbody>");
            sb.Append(endpointRows.ToString());
            sb.Append("</tbody></table></div>");

            // Charts
            sb.Append("<div class='charts' style='margin-top:16px'>");
            sb.Append("<div class='box'><h3>Concurrent Requests Over Duration</h3><canvas id='cConc'></canvas></div>");
            sb.Append("<div class='box'><h3>Response Time Over Duration</h3><canvas id='cS'></canvas></div>");
            sb.Append("<div class='box'><h3>Requests Over Duration</h3><canvas id='cEB'></canvas></div>");
            sb.Append("<div class='box'><h3>Response Time Distribution</h3><canvas id='cH'></canvas></div>");
            sb.Append("<div class='box'><h3>Status Codes</h3>" +
                      "<div class='ch' style='height:260px'><canvas id='cD'></canvas></div></div>");
            sb.Append("<div class='box'><h3>Avg TTFB vs Download by Endpoint</h3>" +
                      "<div class='ch' style='height:" + pathChartH + "px'><canvas id='cT'></canvas></div></div>");
            sb.Append("<div class='box'><h3>Avg &amp; p95 by Endpoint</h3>" +
                      "<div class='ch' style='height:" + pathChartH + "px'><canvas id='cP'></canvas></div></div>");
            sb.Append("<div class='box'><h3>Request Count by Endpoint</h3>" +
                      "<div class='ch' style='height:" + pathChartH + "px'><canvas id='cC'></canvas></div></div>");
            sb.Append("</div></div>"); // .charts .wrap

            // Chart.js data + init
            sb.Append("<script>");
            sb.Append("const cd="    + concurrencyJson  + ";");
            sb.Append("const sdNorm=" + scatterNormJson  + ";");
            sb.Append("const sdOut="  + scatterOutJson   + ";");
            sb.Append("const el="    + errorLineJson     + ";");
            sb.Append("const esl="   + successLineJson   + ";");
            sb.Append("const dur="   + durationSec.ToString("F1", CultureInfo.InvariantCulture) + ";");
            sb.Append("const hl="    + histLabels        + ";");
            sb.Append("const hd="    + histCounts        + ";");
            sb.Append("const pl="    + pathLabels        + ";");
            sb.Append("const pa="    + pathAvgData       + ";");
            sb.Append("const pp="    + pathP95Data       + ";");
            sb.Append("const pt="    + pathTtfbData      + ";");
            sb.Append("const pdl="   + pathDlData        + ";");
            sb.Append("const pcnt="  + pathCntData       + ";");
            sb.Append("const sl="    + statusLabels      + ";");
            sb.Append("const scd="   + statusData        + ";");
            sb.Append("const scc="   + statusColors      + ";");
            sb.Append(@"
new Chart(document.getElementById('cConc'),{type:'line',data:{datasets:[{label:'Threads',data:cd,
  borderColor:'rgba(52,152,219,0.9)',backgroundColor:'rgba(52,152,219,0.15)',
  borderWidth:2,pointRadius:4,pointBackgroundColor:'rgba(52,152,219,0.9)',fill:true,stepped:'before'}]},
  options:{responsive:true,plugins:{legend:{display:false}},
  scales:{x:{type:'linear',title:{display:true,text:'Elapsed (s)'},beginAtZero:true},
          y:{beginAtZero:true,title:{display:true,text:'Threads'},ticks:{stepSize:1}}}}});

new Chart(document.getElementById('cS'),{type:'scatter',data:{datasets:[
  {label:'Normal',data:sdNorm,pointRadius:2,pointBackgroundColor:'rgba(52,152,219,0.35)',pointBorderColor:'transparent'},
  {label:'Outlier',data:sdOut,pointRadius:3,pointBackgroundColor:'rgba(220,53,69,0.75)',pointBorderColor:'transparent'}
]},
  options:{responsive:true,plugins:{legend:{display:true,position:'top'}},
  layout:{padding:{right:10}},
  scales:{x:{title:{display:true,text:'Elapsed (s)'},beginAtZero:true,max:dur},
          y:{title:{display:true,text:'Response Time (ms)'},beginAtZero:true}}}});

new Chart(document.getElementById('cEB'),{type:'scatter',data:{datasets:[
  {label:'Success',data:esl,pointRadius:3,pointBackgroundColor:'rgba(46,204,113,0.7)',pointBorderColor:'transparent'},
  {label:'Errors', data:el, pointRadius:3,pointBackgroundColor:'rgba(231,76,60,0.8)',pointBorderColor:'transparent'}
]},options:{responsive:true,plugins:{legend:{display:true,position:'top'}},
  layout:{padding:{right:10}},
  scales:{x:{min:0,max:dur,title:{display:true,text:'Elapsed (s)'},beginAtZero:true},
          y:{beginAtZero:true,ticks:{stepSize:1},title:{display:true,text:'Req/s'}}}}});

new Chart(document.getElementById('cH'),{type:'bar',data:{labels:hl,
  datasets:[{label:'Requests',data:hd,backgroundColor:'rgba(52,152,219,0.7)',borderWidth:0}]},
  options:{responsive:true,plugins:{legend:{display:false}},
  scales:{x:{title:{display:true,text:'Total ms'}},
          y:{beginAtZero:true,title:{display:true,text:'Count'}}}}});

new Chart(document.getElementById('cD'),{type:'doughnut',data:{labels:sl,
  datasets:[{data:scd,backgroundColor:scc,borderWidth:1}]},
  options:{responsive:true,maintainAspectRatio:false,
           plugins:{legend:{position:'right'}}}});

new Chart(document.getElementById('cT'),{type:'bar',data:{labels:pl,
  datasets:[
    {label:'Avg TTFB',    data:pt,  backgroundColor:'rgba(52,152,219,0.85)', stack:'s'},
    {label:'Avg Download',data:pdl, backgroundColor:'rgba(46,204,113,0.85)', stack:'s'}
  ]},
  options:{responsive:true,maintainAspectRatio:false,indexAxis:'y',
  plugins:{tooltip:{callbacks:{footer:function(items){
    var i=items[0].dataIndex;return 'Total: '+(pt[i]+pdl[i])+'ms';
  }}}},
  scales:{x:{stacked:true,beginAtZero:true,title:{display:true,text:'ms'}},
          y:{stacked:true}}}});

new Chart(document.getElementById('cP'),{type:'bar',data:{labels:pl,
  datasets:[{label:'Avg',data:pa,backgroundColor:'rgba(52,152,219,0.7)'},
            {label:'p95',data:pp,backgroundColor:'rgba(231,76,60,0.7)'}]},
  options:{responsive:true,maintainAspectRatio:false,indexAxis:'y',
  scales:{x:{beginAtZero:true,title:{display:true,text:'ms'}}}}});

new Chart(document.getElementById('cC'),{type:'bar',data:{labels:pl,
  datasets:[{label:'Requests',data:pcnt,
             backgroundColor:'rgba(155,89,182,0.7)',borderWidth:0}]},
  options:{responsive:true,maintainAspectRatio:false,indexAxis:'y',
  plugins:{legend:{display:false}},
  scales:{x:{beginAtZero:true,title:{display:true,text:'count'}}}}});
");
            sb.Append("</script></body></html>");

            File.WriteAllText(htmlFile, sb.ToString());
            Logger.Info("HTML report written to " + htmlFile);
        }

        static string EscapeJs(string s)
        {
            return s.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"");
        }

        static string HtmlEncode(string s)
        {
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                    .Replace("\"", "&quot;");
        }
    }
}
