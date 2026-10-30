using System;
using System.Globalization;
using Microsoft.Win32;

namespace OpencodeDpiFix
{
    /// <summary>
    /// Все тексты интерфейса и сообщений. Язык определяется так:
    /// переменная окружения (для тестов) → сохранённый выбор → язык системы.
    /// </summary>
    internal static class Strings
    {
        private const string RegPath = @"Software\OpencodeDpiFix";
        private const string RegValue = "Language";

        private static bool _en;

        public static bool English { get { return _en; } }
        public static string LanguageCode { get { return _en ? "en" : "ru"; } }
        public static string OtherLanguageCode { get { return _en ? "ru" : "en"; } }

        private static string T(string ru, string en)
        {
            return _en ? en : ru;
        }

        // ------------------------------------------------------------- language

        public static void Initialize()
        {
            string code = Environment.GetEnvironmentVariable("OPENCODE_DPI_FIX_LANG");
            if (string.IsNullOrEmpty(code)) code = ReadSaved();

            if (string.IsNullOrEmpty(code))
            {
                code = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru" ? "ru" : "en";
            }

            SetLanguage(code);
        }

        public static void SetLanguage(string code)
        {
            _en = !string.Equals(code, "ru", StringComparison.OrdinalIgnoreCase);
        }

        public static void Save()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegPath))
                {
                    if (key != null) key.SetValue(RegValue, LanguageCode);
                }
            }
            catch
            {
                // язык просто не запомнится — не критично
            }
        }

        private static string ReadSaved()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegPath))
                {
                    if (key != null) return key.GetValue(RegValue) as string;
                }
            }
            catch
            {
            }
            return null;
        }

        // ------------------------------------------------------------------ ui

        public static string WindowTitle { get { return T("Обход блокировки opencode.ai", "opencode.ai bypass"); } }

        public static string Subtitle
        {
            get
            {
                return T(
                    "Направляет opencode.ai, models.dev и registry.npmjs.org на рабочий edge-адрес " +
                    "Cloudflare через файл hosts. Прокси и VPN не нужны.",
                    "Points opencode.ai, models.dev and registry.npmjs.org at a working Cloudflare " +
                    "edge address through the hosts file. No proxy, no VPN.");
            }
        }

        public static string ButtonEnable { get { return T("Включить обход", "Enable bypass"); } }
        public static string ButtonDisable { get { return T("Выключить обход", "Disable bypass"); } }
        public static string ButtonCheck { get { return T("Проверить доступ", "Check access"); } }

        public static string Ipv6Caption
        {
            get { return T("Отклонять IPv6 (если сайт открывается медленно)", "Reject IPv6 (if the site loads slowly)"); }
        }

        public static string EdgeCaption { get { return T("Edge-адрес", "Edge address"); } }

        public static string EdgeHint(string backup)
        {
            return _en ? "if it does not work, try " + backup : "если не работает — попробуйте " + backup;
        }

        public static string LogCaption { get { return T("Журнал", "Log"); } }

        public static string Footer
        {
            get
            {
                return T(
                    "Обратимо: «Выключить обход» убирает только свой блок, копии hosts.bak-* лежат рядом с hosts",
                    "Reversible: “Disable bypass” removes only this block; hosts.bak-* backups stay next to hosts");
            }
        }

        // -------------------------------------------------------------- status

        public static string StatusOn(string ip)
        {
            return _en ? "Bypass is on — domains go to " + ip : "Обход включён — домены идут на " + ip;
        }

        public static string Ipv6Suffix { get { return T(", IPv6 отклоняется", ", IPv6 rejected"); } }
        public static string StatusOff { get { return T("Обход выключен — записей в hosts нет", "Bypass is off — no hosts records"); } }

        public static string BusyInstall { get { return T("Включаю обход…", "Enabling the bypass…"); } }
        public static string BusyDisable { get { return T("Выключаю обход…", "Disabling the bypass…"); } }
        public static string BusyCheck { get { return T("Проверяю доступ…", "Checking access…"); } }

        // ----------------------------------------------------------------- log

        public static string LogHosts(string path) { return T("Файл hosts: " + path, "Hosts file: " + path); }
        public static string WarnNoAdmin
        {
            get { return T("Внимание: нет прав администратора — правка hosts не сработает.", "Warning: not running as administrator — hosts cannot be edited."); }
        }
        public static string RestartHint
        {
            get { return T("После включения закройте и заново откройте браузер и opencode.", "After enabling, restart your browser and opencode."); }
        }
        public static string WarnOtherEntries
        {
            get
            {
                return T(
                    "Внимание: в hosts есть посторонние записи для этих доменов — они могут мешать.",
                    "Warning: hosts already contains other records for these domains — they may interfere.");
            }
        }
        public static string ErrorPrefix(string message) { return T("ОШИБКА: " + message, "ERROR: " + message); }
        public static string DoneMessage
        {
            get { return T("Готово. Закройте и заново откройте браузер и приложение opencode.", "Done. Restart your browser and the opencode app."); }
        }
        public static string CheckNotApplied
        {
            get
            {
                return T(
                    "Внимание: записи этого инструмента в hosts не найдены — проверяю как есть.",
                    "Note: this tool's records were not found in hosts — checking as is.");
            }
        }
        public static string CheckExpected(string ip) { return T("Ожидаемый edge-адрес: " + ip, "Expected edge address: " + ip); }

        public static string HostsBlockComment
        {
            get
            {
                return T(
                    "# Правило DPI режет TLS ClientHello с PQ key share; этот edge не затронут.",
                    "# DPI resets TLS ClientHello with a PQ key share; this edge is not affected.");
            }
        }

        public static string HostsIpv6Comment
        {
            get
            {
                return T(
                    "# IPv6 принудительно отключён для этих доменов (соединение отклоняется, браузер сразу берёт IPv4).",
                    "# IPv6 is force-disabled for these domains (the attempt fails fast, the browser uses IPv4).");
            }
        }

        // ------------------------------------------------------------- hostsfix

        public static string Backup(string path) { return T("Резервная копия: " + path, "Backup: " + path); }

        public static string PreviousRemoved(int lines)
        {
            return _en
                ? "Removed the previous block (" + lines + " lines)."
                : "Удалён предыдущий блок (" + lines + " строк).";
        }

        public static string HostsUpdated(string ip, bool ipv6)
        {
            if (_en) return "hosts updated: " + ip + (ipv6 ? " (+ IPv6 rejected)" : string.Empty) + ".";
            return "hosts обновлён: " + ip + (ipv6 ? " (+ IPv6 отклоняется)" : string.Empty) + ".";
        }

        public static string DnsFlushed { get { return T("Кэш DNS сброшен.", "DNS cache flushed."); } }
        public static string DnsFlushFailed(string message)
        {
            return T("Не удалось сбросить кэш DNS: " + message, "Could not flush the DNS cache: " + message);
        }

        public static string VerifyHeader { get { return T("Проверка резолва:", "DNS check:"); } }
        public static string Ok { get { return "OK"; } }
        public static string NotApplied { get { return T("НЕ ПРИМЕНИЛОСЬ", "NOT APPLIED"); } }
        public static string NoAnswer { get { return T("нет ответа", "no answer"); } }

        public static string NothingToRevert
        {
            get { return T("Записей этого инструмента в hosts нет — менять нечего.", "No records from this tool in hosts — nothing to revert."); }
        }

        public static string BlockRemoved(int lines)
        {
            return _en ? "Block removed (" + lines + " lines)." : "Блок удалён (" + lines + " строк).";
        }

        public static string Restored
        {
            get { return T("hosts вернулся к состоянию до установки.", "hosts is back to its previous state."); }
        }

        public static string InvalidIp(string value)
        {
            return T("Некорректный IPv4-адрес: «" + value + "»", "Invalid IPv4 address: \"" + value + "\"");
        }

        public static string HostsNotFound(string path)
        {
            return T("Не найден файл hosts: " + path, "Hosts file not found: " + path);
        }

        public static string NoAdmin
        {
            get
            {
                return T(
                    "Нужны права администратора: запустите программу и подтвердите запрос UAC.",
                    "Administrator rights are required: run the app and confirm the UAC prompt.");
            }
        }

        // ---------------------------------------------------------- netcheck

        public static string ColumnDomain { get { return T("домен", "domain"); } }
        public static string ColumnResolve { get { return T("резолв", "resolved"); } }
        public static string ColumnTcp { get { return "tcp443"; } }
        public static string ColumnHttps { get { return "https"; } }

        public static string TcpYes { get { return "OK"; } }
        public static string TcpNo { get { return T("нет", "no"); } }

        public static string HttpOk(int code) { return "HTTP " + code; }
        public static string HttpError(string message) { return T("ошибка: " + message, "error: " + message); }
        public static string NoDescription { get { return T("без описания", "no description"); } }

        public static string HintResolve(string ip)
        {
            return _en ? "   -> hosts does not point the domain at " + ip : "   -> hosts не направляет домен на " + ip;
        }
        public static string HintTcp
        {
            get { return T("   -> TCP 443 не открывается", "   -> TCP 443 does not open"); }
        }
        public static string HintHttps
        {
            get { return T("   -> HTTPS не отвечает", "   -> HTTPS does not respond"); }
        }
        public static string SummaryOk
        {
            get
            {
                return T("Итог: всё работает — opencode.ai, models.dev и npm доступны.",
                         "Result: everything works — opencode.ai, models.dev and npm are reachable.");
            }
        }
        public static string SummaryFail
        {
            get { return T("Итог: есть проблемы. Смотрите пометки выше.", "Result: there are problems. See the notes above."); }
        }

        // ----------------------------------------------------------------- cli

        public static string UsageHeader(string version)
        {
            return T("OpencodeDpiFix " + version + " — обход блокировки opencode.ai через hosts",
                     "OpencodeDpiFix " + version + " — opencode.ai bypass via hosts");
        }

        public static string UsageBody
        {
            get
            {
                if (_en)
                {
                    return
                        "\r\n" +
                        "  OpencodeDpiFix.exe                       open the window\r\n" +
                        "  OpencodeDpiFix.exe install [--ipv6]      enable the bypass (admin required)\r\n" +
                        "  OpencodeDpiFix.exe revert                remove the records (admin required)\r\n" +
                        "  OpencodeDpiFix.exe check                 test the three domains\r\n" +
                        "  OpencodeDpiFix.exe status                print the current state\r\n" +
                        "\r\n" +
                        "Options:\r\n" +
                        "  --ip <address>   edge address, default " + HostsFix.DefaultEdgeIp +
                        " (backup " + HostsFix.BackupEdgeIp + ")\r\n" +
                        "  --ipv6           also reject IPv6 for these domains\r\n" +
                        "  --lang <ru|en>   interface language\r\n";
                }

                return
                    "\r\n" +
                    "  OpencodeDpiFix.exe                       открыть окно\r\n" +
                    "  OpencodeDpiFix.exe install [--ipv6]      включить обход (нужен администратор)\r\n" +
                    "  OpencodeDpiFix.exe revert                удалить записи (нужен администратор)\r\n" +
                    "  OpencodeDpiFix.exe check                 проверить доступность доменов\r\n" +
                    "  OpencodeDpiFix.exe status                показать состояние записей\r\n" +
                    "\r\n" +
                    "Параметры:\r\n" +
                    "  --ip <адрес>   edge-адрес, по умолчанию " + HostsFix.DefaultEdgeIp +
                    " (резервный " + HostsFix.BackupEdgeIp + ")\r\n" +
                    "  --ipv6         дополнительно отклонять IPv6 для этих доменов\r\n" +
                    "  --lang <ru|en> язык интерфейса\r\n";
            }
        }

        public static string UnknownOption(string value)
        {
            return T("Неизвестный параметр: " + value, "Unknown option: " + value);
        }

        public static string UnknownCommand(string value)
        {
            return T("Неизвестная команда: " + value, "Unknown command: " + value);
        }

        public static string StatusOnCli(string ip, bool ipv6)
        {
            if (_en) return "on: " + ip + (ipv6 ? " (IPv6 rejected)" : string.Empty);
            return "включён: " + ip + (ipv6 ? " (IPv6 отклоняется)" : string.Empty);
        }

        public static string StatusOffCli { get { return T("выключен", "off"); } }
    }
}
