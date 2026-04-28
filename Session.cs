using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenSurge
{
    class RequestBlock
    {
        public string Name;
        public string Method;
        public string Path;
        public Dictionary<string, string> Headers;
        public string Body;
    }

    static class Session
    {
        public static List<RequestBlock> Parse(string filePath)
        {
            var result = new List<RequestBlock>();
            string content;
            try { content = File.ReadAllText(filePath); }
            catch (Exception ex) { Logger.Error(ex); return result; }

            string[] blocks = Regex.Split(content, "-{10,}");

            foreach (string rawBlock in blocks)
            {
                string block = rawBlock.Trim();
                if (string.IsNullOrWhiteSpace(block)) continue;

                // Skip WebSurge options blocks
                if (block.IndexOf("WebSurge Options", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                string[] lines = block.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

                // Find the first non-empty line -- it should be "METHOD PATH HTTP/x.x"
                int startLine = -1;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(lines[i])) { startLine = i; break; }
                }
                if (startLine < 0) continue;

                string[] requestParts = lines[startLine].Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (requestParts.Length < 2) continue;

                string method = requestParts[0].ToUpperInvariant();
                string path   = requestParts[1];

                // Parse headers until blank line
                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                int bodyStart = -1;
                for (int i = startLine + 1; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i]))
                    {
                        bodyStart = i + 1;
                        break;
                    }
                    int colon = lines[i].IndexOf(':');
                    if (colon > 0)
                    {
                        string hName  = lines[i].Substring(0, colon).Trim();
                        string hValue = lines[i].Substring(colon + 1).Trim();
                        headers[hName] = hValue;
                    }
                }

                // Build body from lines after the blank line
                string body = "";
                if (bodyStart >= 0 && bodyStart < lines.Length)
                {
                    var bodyLines = new List<string>();
                    for (int i = bodyStart; i < lines.Length; i++)
                        bodyLines.Add(lines[i]);
                    body = string.Join("\n", bodyLines).Trim();
                }

                // Name: prefer Websurge-Request-Name header, fallback to path
                string name = path;
                if (headers.ContainsKey("Websurge-Request-Name"))
                    name = headers["Websurge-Request-Name"];

                result.Add(new RequestBlock
                {
                    Name    = name,
                    Method  = method,
                    Path    = path,
                    Headers = headers,
                    Body    = body
                });
            }

            Logger.Info("Parsed " + result.Count + " request blocks from " + filePath);
            return result;
        }

        public static void Save(string filePath, List<RequestBlock> requests)
        {
            const string Sep = "------------------------------------------------------------------";
            var sb = new StringBuilder();
            foreach (var r in requests)
            {
                sb.AppendLine(r.Method + " " + r.Path + " HTTP/1.1");
                if (!string.IsNullOrEmpty(r.Name) && r.Name != r.Path)
                    sb.AppendLine("Websurge-Request-Name: " + r.Name);
                foreach (var h in r.Headers)
                {
                    if (h.Key.Equals("Websurge-Request-Name", StringComparison.OrdinalIgnoreCase)) continue;
                    sb.AppendLine(h.Key + ": " + h.Value);
                }
                sb.AppendLine();
                if (!string.IsNullOrEmpty(r.Body))
                {
                    sb.AppendLine(r.Body);
                    sb.AppendLine();
                }
                sb.AppendLine(Sep);
            }
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            Logger.Info("Session saved to " + filePath);
        }
    }
}
