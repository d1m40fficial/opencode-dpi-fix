using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text;

namespace OpencodeDpiFix
{
    internal sealed class HostsState
    {
        public bool Applied;
        public string EdgeIp;
        public bool Ipv6Blocked;
        public bool OtherEntries;
    }

    /// <summary>
    /// Логика правки файла hosts. Правило DPI у провайдера срабатывает на содержимое
    /// TLS ClientHello (post-quantum key share), а не на имя в SNI и не на IP.
    /// Классический X25519 на том же адресе проходит, поэтому достаточно направить
    /// домены на edge-адрес, где правило не применено.
    /// </summary>
    internal static class HostsFix
    {
        public const string Marker = "# --- DPI bypass: opencode / models.dev / npm ---";
        public const string EndTag = "# --- end ---";
        public const string DefaultEdgeIp = "8.47.69.5";
        public const string BackupEdgeIp = "8.6.112.5";

        public const string BlockComment =
            "# Правило DPI режет TLS ClientHello с PQ key share; этот edge не затронут.";
        public const string Ipv6Comment =
            "# IPv6 принудительно отключён для этих доменов (соединение отклоняется, браузер сразу берёт IPv4).";

        public static readonly string[] Domains = { "opencode.ai", "models.dev", "registry.npmjs.org" };

        public static string HostsPath
        {
            get
            {
                // Переопределение пути — только для тестов и CI.
                string overridePath = Environment.GetEnvironmentVariable("OPENCODE_DPI_FIX_HOSTS");
                if (!string.IsNullOrEmpty(overridePath)) return overridePath;

                string root = Environment.GetEnvironmentVariable("SystemRoot");
                if (string.IsNullOrEmpty(root))
                    root = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                return Path.Combine(root, @"System32\drivers\etc\hosts");
            }
        }

        /// <summary>Обход проверки прав — только для тестов и CI.</summary>
        public static bool AdminCheckBypassed
        {
            get { return Environment.GetEnvironmentVariable("OPENCODE_DPI_FIX_ALLOW_NONADMIN") == "1"; }
        }

        public static bool IsAdmin()
        {
            try
            {
                WindowsIdentity id = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(id);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        // ---------------------------------------------------------------- state

        public static HostsState GetState()
        {
            HostsState state = new HostsState();
            if (!File.Exists(HostsPath)) return state;

            Encoding encoding;
            string newLine;
            string[] lines = ReadAllLines(HostsPath, out encoding, out newLine);

            bool inside = false;
            string ipv4 = null;
            foreach (string raw in lines)
            {
                string line = raw.Trim();

                if (!inside)
                {
                    if (line == Marker)
                    {
                        inside = true;
                        state.Applied = true;
                        continue;
                    }
                    if (line.Length > 0 && line[0] != '#' && !state.OtherEntries)
                    {
                        string lower = " " + line.ToLowerInvariant().Replace('\t', ' ') + " ";
                        for (int i = 0; i < Domains.Length; i++)
                        {
                            if (lower.IndexOf(" " + Domains[i] + " ", StringComparison.Ordinal) >= 0)
                            {
                                state.OtherEntries = true;
                                break;
                            }
                        }
                    }
                    continue;
                }

                if (line == EndTag)
                {
                    inside = false;
                    continue;
                }

                string[] parts = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                string ip = parts[0];
                string host = parts[parts.Length - 1].ToLowerInvariant();
                if (host != "opencode.ai" && host != "models.dev" && host != "registry.npmjs.org") continue;

                if (ip == "::1") state.Ipv6Blocked = true;
                else if (ipv4 == null) ipv4 = ip;
            }

            state.EdgeIp = ipv4;
            return state;
        }

        // -------------------------------------------------------------- install

        public static void Install(string edgeIp, bool blockIpv6, Action<string> log)
        {
            IPAddress address;
            if (!IPAddress.TryParse((edgeIp ?? string.Empty).Trim(), out address) ||
                address.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new ArgumentException("Некорректный IPv4-адрес: «" + edgeIp + "»");
            }
            string ip = address.ToString();

            if (!File.Exists(HostsPath))
                throw new FileNotFoundException("Не найден файл hosts: " + HostsPath);

            Encoding encoding;
            string newLine;
            string[] lines = ReadAllLines(HostsPath, out encoding, out newLine);

            int removed;
            List<string> kept = StripBlock(lines, out removed);
            if (removed > 0) log("Удалён предыдущий блок (" + removed + " строк).");

            kept.Add(Marker);
            kept.Add(BlockComment);
            for (int i = 0; i < Domains.Length; i++)
                kept.Add(ip + "\t" + Domains[i]);

            if (blockIpv6)
            {
                kept.Add(Ipv6Comment);
                for (int i = 0; i < Domains.Length; i++)
                    kept.Add("::1\t" + Domains[i]);
            }
            kept.Add(EndTag);

            WriteWithBackup(kept, encoding, newLine, log);

            log("hosts обновлён: " + ip +
                (blockIpv6 ? " (+ IPv6 отклоняется)" : string.Empty) + ".");
            FlushDns(log);
            Verify(ip, log);
        }

        // --------------------------------------------------------------- revert

        public static void Revert(Action<string> log)
        {
            if (!File.Exists(HostsPath))
                throw new FileNotFoundException("Не найден файл hosts: " + HostsPath);

            Encoding encoding;
            string newLine;
            string[] lines = ReadAllLines(HostsPath, out encoding, out newLine);

            int removed;
            List<string> kept = StripBlock(lines, out removed);
            if (removed == 0)
            {
                log("Записей этого инструмента в hosts нет — менять нечего.");
                return;
            }

            WriteWithBackup(kept, encoding, newLine, log);
            log("Блок удалён (" + removed + " строк).");
            FlushDns(log);
            log("hosts вернулся к состоянию до установки.");
        }

        // -------------------------------------------------------------- helpers

        public static void Verify(string edgeIp, Action<string> log)
        {
            log("Проверка резолва:");
            for (int i = 0; i < Domains.Length; i++)
            {
                string[] ips = NetworkCheck.ResolveIpv4(Domains[i]);
                bool ok = Array.IndexOf(ips, edgeIp) >= 0;
                log(string.Format(CultureInfo.InvariantCulture, "  {0,-20} {1,-16} {2}",
                    Domains[i],
                    ips.Length > 0 ? string.Join(",", ips) : "нет ответа",
                    ok ? "OK" : "НЕ ПРИМЕНИЛОСЬ"));
            }
        }

        public static void FlushDns(Action<string> log)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("ipconfig", "/flushdns");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;

                using (Process p = Process.Start(psi))
                {
                    p.StandardOutput.ReadToEnd();
                    p.StandardError.ReadToEnd();
                    p.WaitForExit(10000);
                }
                log("Кэш DNS сброшен.");
            }
            catch (Exception ex)
            {
                log("Не удалось сбросить кэш DNS: " + ex.Message);
            }
        }

        private static List<string> StripBlock(string[] lines, out int removed)
        {
            List<string> kept = new List<string>();
            bool inside = false;
            removed = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();

                if (!inside && trimmed == Marker)
                {
                    inside = true;
                    removed++;
                    continue;
                }
                if (inside)
                {
                    removed++;
                    if (trimmed == EndTag) inside = false;
                    continue;
                }
                kept.Add(lines[i]);
            }

            // нормализуем конец файла: ровно один перевод строки
            while (kept.Count > 0 && kept[kept.Count - 1].Trim().Length == 0)
                kept.RemoveAt(kept.Count - 1);
            kept.Add(string.Empty);

            return kept;
        }

        private static void WriteWithBackup(List<string> lines, Encoding encoding, string newLine, Action<string> log)
        {
            if (!IsAdmin() && !AdminCheckBypassed)
            {
                throw new InvalidOperationException(
                    "Нужны права администратора: запустите программу и подтвердите запрос UAC.");
            }

            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string backup = HostsPath + ".bak-" + stamp;
            int suffix = 2;
            while (File.Exists(backup))
            {
                backup = HostsPath + ".bak-" + stamp + "-" + suffix.ToString(CultureInfo.InvariantCulture);
                suffix++;
            }

            File.Copy(HostsPath, backup, false);
            log("Резервная копия: " + backup);

            FileAttributes attributes = File.GetAttributes(HostsPath);
            if ((attributes & FileAttributes.ReadOnly) != 0)
                File.SetAttributes(HostsPath, attributes & ~FileAttributes.ReadOnly);

            string text = string.Join(newLine, lines.ToArray());
            byte[] preamble = encoding.GetPreamble();
            byte[] body = encoding.GetBytes(text);

            using (FileStream stream = new FileStream(HostsPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                if (preamble.Length > 0) stream.Write(preamble, 0, preamble.Length);
                stream.Write(body, 0, body.Length);
            }
        }

        private static string[] ReadAllLines(string path, out Encoding encoding, out string newLine)
        {
            byte[] bytes = File.ReadAllBytes(path);

            encoding = new UTF8Encoding(false);
            int offset = 0;

            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                encoding = new UTF8Encoding(true);
                offset = 3;
            }
            else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                encoding = Encoding.Unicode;
                offset = 2;
            }
            else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                encoding = Encoding.BigEndianUnicode;
                offset = 2;
            }

            string text = encoding.GetString(bytes, offset, bytes.Length - offset);
            newLine = text.IndexOf("\r\n", StringComparison.Ordinal) >= 0 ? "\r\n" : "\n";
            return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        }
    }
}
