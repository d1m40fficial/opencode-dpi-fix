using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace OpencodeDpiFix
{
    internal sealed class CheckEntry
    {
        public string Domain;
        public string[] Ipv4;
        public bool TcpOk;
        public string HttpsText;
        public double Seconds;
        public bool HttpsOk;
        public bool ResolvedToEdge;
        public bool Ok;
    }

    internal static class NetworkCheck
    {
        static NetworkCheck()
        {
            // .NET Framework по умолчанию умеет только TLS 1.0 — включаем современные версии.
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { } // TLS 1.2
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)12288; } catch { } // TLS 1.3
        }

        public static string[] ResolveIpv4(string host)
        {
            List<string> result = new List<string>();
            try
            {
                IPAddress[] all = Dns.GetHostAddresses(host);
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i].AddressFamily == AddressFamily.InterNetwork)
                        result.Add(all[i].ToString());
                }
            }
            catch
            {
                // хост не резолвится — вернём пустой список
            }
            return result.ToArray();
        }

        public static bool TcpTest(string host, int port, int timeoutMs)
        {
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    IAsyncResult result = client.BeginConnect(host, port, null, null);
                    if (!result.AsyncWaitHandle.WaitOne(timeoutMs)) return false;
                    client.EndConnect(result);
                    return client.Connected;
                }
            }
            catch
            {
                return false;
            }
        }

        public static string UrlFor(string domain)
        {
            if (domain == "opencode.ai") return "https://opencode.ai/inference/v1/models";
            if (domain == "models.dev") return "https://models.dev/api.json";
            return "https://registry.npmjs.org/-/ping";
        }

        public static CheckEntry Check(string domain, string expectedEdgeIp)
        {
            CheckEntry entry = new CheckEntry();
            entry.Domain = domain;
            entry.Ipv4 = ResolveIpv4(domain);
            entry.TcpOk = TcpTest(domain, 443, 5000);
            entry.ResolvedToEdge = expectedEdgeIp != null && Array.IndexOf(entry.Ipv4, expectedEdgeIp) >= 0;

            entry.HttpsText = TestUrl(UrlFor(domain), out entry.Seconds, out entry.HttpsOk);
            entry.Ok = entry.ResolvedToEdge && entry.TcpOk && entry.HttpsOk;
            return entry;
        }

        public static bool RunAll(string expectedEdgeIp, Action<string> log)
        {
            bool allOk = true;

            log(string.Format(CultureInfo.InvariantCulture, "{0,-22} {1,-24} {2,-8} {3}",
                Strings.ColumnDomain, Strings.ColumnResolve, Strings.ColumnTcp, Strings.ColumnHttps));
            log(new string('-', 74));

            for (int i = 0; i < HostsFix.Domains.Length; i++)
            {
                CheckEntry entry = Check(HostsFix.Domains[i], expectedEdgeIp);
                if (!entry.Ok) allOk = false;

                log(string.Format(CultureInfo.InvariantCulture, "{0,-22} {1,-24} {2,-8} {3} ({4}s)",
                    entry.Domain,
                    entry.Ipv4.Length > 0 ? string.Join(",", entry.Ipv4) : Strings.NoAnswer,
                    entry.TcpOk ? Strings.TcpYes : Strings.TcpNo,
                    entry.HttpsText,
                    entry.Seconds.ToString("0.0", CultureInfo.InvariantCulture)));

                if (!entry.Ok)
                {
                    if (!entry.ResolvedToEdge)
                        log(Strings.HintResolve(expectedEdgeIp ?? "?"));
                    if (!entry.TcpOk)
                        log(Strings.HintTcp);
                    if (!entry.HttpsOk)
                        log(Strings.HintHttps);
                }
            }

            log(string.Empty);
            log(allOk ? Strings.SummaryOk : Strings.SummaryFail);

            return allOk;
        }

        private static string TestUrl(string url, out double seconds, out bool ok)
        {
            Stopwatch watch = Stopwatch.StartNew();
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "HEAD";
                request.Timeout = 15000;
                request.ReadWriteTimeout = 15000;
                request.AllowAutoRedirect = true;
                request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    seconds = watch.Elapsed.TotalSeconds;
                    ok = true;
                    return Strings.HttpOk((int)response.StatusCode);
                }
            }
            catch (WebException ex)
            {
                seconds = watch.Elapsed.TotalSeconds;
                HttpWebResponse response = ex.Response as HttpWebResponse;
                if (response != null)
                {
                    int code = (int)response.StatusCode;
                    response.Close();
                    ok = true;
                    return Strings.HttpOk(code);
                }
                ok = false;
                return Strings.HttpError(Shorten(ex.Message));
            }
            catch (Exception ex)
            {
                seconds = watch.Elapsed.TotalSeconds;
                ok = false;
                return Strings.HttpError(Shorten(ex.Message));
            }
        }

        private static string Shorten(string text)
        {
            if (string.IsNullOrEmpty(text)) return Strings.NoDescription;
            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length > 70 ? text.Substring(0, 70) + "…" : text;
        }
    }
}
