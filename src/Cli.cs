using System;
using System.Globalization;

namespace OpencodeDpiFix
{
    /// <summary>
    /// Консольный режим — для автоматизации и проверки в CI.
    /// Вывод идёт в консоль родителя (программа собрана как GUI-приложение).
    /// </summary>
    internal static class Cli
    {
        public static int Run(string[] args)
        {
            string command = (args[0] ?? string.Empty).ToLowerInvariant();
            string edgeIp = HostsFix.DefaultEdgeIp;
            bool blockIpv6 = false;

            for (int i = 1; i < args.Length; i++)
            {
                string arg = (args[i] ?? string.Empty).ToLowerInvariant();
                if (arg == "--ipv6" || arg == "-ipv6") blockIpv6 = true;
                else if ((arg == "--ip" || arg == "-ip") && i + 1 < args.Length) edgeIp = args[++i];
                else if (arg == "--help" || arg == "-h" || arg == "/?")
                {
                    Usage();
                    return 0;
                }
                else
                {
                    Console.Error.WriteLine("Неизвестный параметр: " + args[i]);
                    Usage();
                    return 2;
                }
            }

            Action<string> log = delegate(string message) { Console.WriteLine(message); };

            try
            {
                switch (command)
                {
                    case "install":
                        HostsFix.Install(edgeIp, blockIpv6, log);
                        return 0;

                    case "revert":
                        HostsFix.Revert(log);
                        return 0;

                    case "check":
                    {
                        HostsState state = HostsFix.GetState();
                        string expected = state.Applied && state.EdgeIp != null ? state.EdgeIp : edgeIp;
                        Console.WriteLine("Ожидаемый edge-адрес: " + expected);
                        Console.WriteLine();
                        return NetworkCheck.RunAll(expected, log) ? 0 : 1;
                    }

                    case "status":
                    {
                        HostsState state = HostsFix.GetState();
                        if (state.Applied)
                        {
                            Console.WriteLine("включён: " + (state.EdgeIp ?? "?") +
                                (state.Ipv6Blocked ? " (IPv6 отклоняется)" : string.Empty));
                            return 0;
                        }
                        Console.WriteLine("выключен");
                        return 1;
                    }

                    case "help":
                    case "--help":
                    case "-h":
                    case "/?":
                        Usage();
                        return 0;

                    default:
                        Console.Error.WriteLine("Неизвестная команда: " + args[0]);
                        Usage();
                        return 2;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ОШИБКА: " + ex.Message);
                return 1;
            }
        }

        private static void Usage()
        {
            Console.WriteLine("OpencodeDpiFix " + Program.Version + " — обход блокировки opencode.ai через hosts");
            Console.WriteLine();
            Console.WriteLine("  OpencodeDpiFix.exe                       открыть окно программы");
            Console.WriteLine("  OpencodeDpiFix.exe install [--ipv6]      включить обход (нужен администратор)");
            Console.WriteLine("  OpencodeDpiFix.exe revert                удалить записи (нужен администратор)");
            Console.WriteLine("  OpencodeDpiFix.exe check                 проверить доступность доменов");
            Console.WriteLine("  OpencodeDpiFix.exe status                показать состояние записей");
            Console.WriteLine();
            Console.WriteLine("Параметры:");
            Console.WriteLine("  --ip <адрес>   edge-адрес, по умолчанию " + HostsFix.DefaultEdgeIp +
                              " (резервный " + HostsFix.BackupEdgeIp + ")");
            Console.WriteLine("  --ipv6         дополнительно отклонять IPv6 для этих доменов");
        }
    }
}
