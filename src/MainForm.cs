using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace OpencodeDpiFix
{
    internal sealed class MainForm : Form
    {
        private readonly Card _statusCard = new Card();
        private readonly Label _statusDot = new Label();
        private readonly Label _statusText = new Label();
        private readonly FlatButton _install = new FlatButton();
        private readonly FlatButton _check = new FlatButton();
        private readonly FlatButton _revert = new FlatButton();
        private readonly CheckBoxDark _ipv6 = new CheckBoxDark();
        private readonly TextBox _edge = new TextBox();
        private readonly TextBox _log = new TextBox();

        private bool _busy;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string subAppName, string subIdList);

        public MainForm()
        {
            Text = "Обход блокировки opencode.ai";
            Font = new Font("Segoe UI", 9F);
            ClientSize = new Size(620, 578);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            AutoScaleMode = AutoScaleMode.Font;
            AutoScaleDimensions = new SizeF(7F, 15F);
            BackColor = Theme.Background;
            ForeColor = Theme.Text;

            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }

            BuildUi();
            RefreshState();

            Log("Файл hosts: " + HostsFix.HostsPath);
            if (!HostsFix.IsAdmin() && !HostsFix.AdminCheckBypassed)
                Log("Внимание: нет прав администратора — правка hosts не сработает.");
            Log("После включения закройте и заново откройте браузер и opencode.");
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            // тёмный заголовок окна: Windows 10 1809+ (20), ранние сборки (19)
            try
            {
                int dark = 1;
                if (DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int)) != 0)
                    DwmSetWindowAttribute(Handle, 19, ref dark, sizeof(int));
            }
            catch
            {
                // на старых системах просто останется светлый заголовок
            }
        }

        // ------------------------------------------------------------------- ui

        private void BuildUi()
        {
            Label title = new Label();
            title.Text = "Обход блокировки opencode.ai";
            title.Font = new Font("Segoe UI", 15.5F, FontStyle.Bold);
            title.ForeColor = Theme.Text;
            title.AutoSize = false;
            title.Bounds = new Rectangle(24, 20, 572, 32);
            Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text =
                "Направляет opencode.ai, models.dev и registry.npmjs.org на рабочий edge-адрес " +
                "Cloudflare через файл hosts. Прокси и VPN не нужны.";
            subtitle.ForeColor = Theme.Muted;
            subtitle.Bounds = new Rectangle(24, 56, 572, 36);
            Controls.Add(subtitle);

            _statusCard.Bounds = new Rectangle(24, 104, 572, 52);
            _statusCard.Radius = 10;
            _statusCard.Fill = Theme.WarnBack;
            _statusCard.BorderColor = Color.FromArgb(58, 47, 27);
            Controls.Add(_statusCard);

            _statusDot.Text = "●";
            _statusDot.Font = new Font("Segoe UI", 9F);
            _statusDot.ForeColor = Theme.WarnDot;
            _statusDot.BackColor = Theme.WarnBack;
            _statusDot.Bounds = new Rectangle(16, 0, 16, 52);
            _statusDot.TextAlign = ContentAlignment.MiddleCenter;
            _statusCard.Controls.Add(_statusDot);

            _statusText.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold);
            _statusText.ForeColor = Theme.WarnText;
            _statusText.BackColor = Theme.WarnBack;
            _statusText.Bounds = new Rectangle(38, 0, 518, 52);
            _statusText.TextAlign = ContentAlignment.MiddleLeft;
            _statusCard.Controls.Add(_statusText);

            StyleButton(_install, "Включить обход", 24, 172, 180, true);
            _install.Click += delegate { OnInstall(); };
            Controls.Add(_install);

            StyleButton(_check, "Проверить доступ", 220, 172, 180, false);
            _check.Click += delegate { OnCheck(); };
            Controls.Add(_check);

            StyleButton(_revert, "Удалить записи", 416, 172, 180, false);
            _revert.ForeColor = Color.FromArgb(214, 206, 210);
            _revert.Click += delegate { OnRevert(); };
            Controls.Add(_revert);

            Card settings = new Card();
            settings.Bounds = new Rectangle(24, 230, 572, 96);
            settings.Fill = Theme.Surface;
            settings.BorderColor = Theme.Border;
            Controls.Add(settings);

            _ipv6.Text = "Отклонять IPv6 (если сайт открывается медленно)";
            _ipv6.ForeColor = Theme.Text;
            _ipv6.BackColor = Theme.Surface;
            _ipv6.Bounds = new Rectangle(16, 16, 460, 22);
            settings.Controls.Add(_ipv6);

            Label edgeLabel = new Label();
            edgeLabel.Text = "Edge-адрес";
            edgeLabel.ForeColor = Theme.Muted;
            edgeLabel.BackColor = Theme.Surface;
            edgeLabel.Bounds = new Rectangle(16, 50, 78, 30);
            edgeLabel.TextAlign = ContentAlignment.MiddleLeft;
            settings.Controls.Add(edgeLabel);

            Card edgeBox = new Card();
            edgeBox.Bounds = new Rectangle(100, 51, 130, 28);
            edgeBox.Fill = Theme.LogBack;
            edgeBox.BorderColor = Theme.Border;
            edgeBox.Radius = 6;
            settings.Controls.Add(edgeBox);

            _edge.Text = HostsFix.DefaultEdgeIp;
            _edge.BorderStyle = BorderStyle.None;
            _edge.BackColor = Theme.LogBack;
            _edge.ForeColor = Theme.Text;
            _edge.Font = new Font("Consolas", 9.75F);
            _edge.Bounds = new Rectangle(10, 6, 110, 18);
            edgeBox.Controls.Add(_edge);

            Label edgeHint = new Label();
            edgeHint.Text = "если не работает — попробуйте " + HostsFix.BackupEdgeIp;
            edgeHint.ForeColor = Theme.Faint;
            edgeHint.BackColor = Theme.Surface;
            edgeHint.Bounds = new Rectangle(246, 50, 310, 30);
            edgeHint.TextAlign = ContentAlignment.MiddleLeft;
            settings.Controls.Add(edgeHint);

            Label logCaption = new Label();
            logCaption.Text = "Журнал";
            logCaption.Font = new Font("Segoe UI", 8.25F, FontStyle.Bold);
            logCaption.ForeColor = Theme.Faint;
            logCaption.Bounds = new Rectangle(24, 338, 200, 18);
            Controls.Add(logCaption);

            Card logCard = new Card();
            logCard.Bounds = new Rectangle(24, 358, 572, 168);
            logCard.Fill = Theme.LogBack;
            logCard.BorderColor = Theme.Border;
            logCard.Padding = new Padding(12, 10, 8, 10);
            Controls.Add(logCard);

            _log.Multiline = true;
            _log.ReadOnly = true;
            _log.ScrollBars = ScrollBars.Vertical;
            _log.WordWrap = true;
            _log.BorderStyle = BorderStyle.None;
            _log.BackColor = Theme.LogBack;
            _log.ForeColor = Theme.LogText;
            _log.Font = new Font("Consolas", 8.25F);
            _log.Dock = DockStyle.Fill;
            _log.TabStop = false;
            _log.HandleCreated += delegate
            {
                // тёмный ползунок прокрутки у многострочного поля
                try { SetWindowTheme(_log.Handle, "DarkMode_Explorer", null); }
                catch { }
            };
            logCard.Controls.Add(_log);

            Label footer = new Label();
            footer.Text = "Обратимо: «Удалить записи» убирает только свой блок, резервные копии hosts.bak-* лежат рядом с hosts";
            footer.Font = new Font("Segoe UI", 8F);
            footer.ForeColor = Theme.Faint;
            footer.Bounds = new Rectangle(24, 540, 572, 18);
            Controls.Add(footer);

            AcceptButton = _install;
        }

        private static void StyleButton(FlatButton button, string text, int x, int y, int width, bool primary)
        {
            button.Text = text;
            button.Bounds = new Rectangle(x, y, width, 42);
            button.Font = new Font("Segoe UI", 9.5F, primary ? FontStyle.Bold : FontStyle.Regular);

            if (primary)
            {
                button.NormalBack = Theme.Accent;
                button.HoverBack = Theme.AccentHover;
                button.PressedBack = Theme.AccentPressed;
                button.BorderColor = Color.Transparent;
                button.ForeColor = Color.White;
            }
            else
            {
                button.NormalBack = Theme.Surface;
                button.HoverBack = Theme.SurfaceHover;
                button.PressedBack = Theme.SurfacePressed;
                button.BorderColor = Theme.Border;
                button.ForeColor = Theme.Text;
            }
        }

        // -------------------------------------------------------------- actions

        private void OnInstall()
        {
            string ip = _edge.Text.Trim();
            bool blockIpv6 = _ipv6.Checked;

            RunAsync("Включаю обход…", delegate(Action<string> log)
            {
                HostsFix.Install(ip, blockIpv6, log);
                log(string.Empty);
                log("Готово. Закройте и заново откройте браузер и приложение opencode.");
            });
        }

        private void OnCheck()
        {
            string fallback = _edge.Text.Trim();
            RunAsync("Проверяю доступ…", delegate(Action<string> log)
            {
                HostsState state = HostsFix.GetState();
                string expected = state.Applied && state.EdgeIp != null ? state.EdgeIp : fallback;

                if (!state.Applied)
                    log("Внимание: записи этого инструмента в hosts не найдены — проверяю как есть.");

                log("Ожидаемый edge-адрес: " + expected);
                log(string.Empty);
                NetworkCheck.RunAll(expected, log);
            });
        }

        private void OnRevert()
        {
            RunAsync("Удаляю записи…", delegate(Action<string> log)
            {
                HostsFix.Revert(log);
            });
        }

        // ------------------------------------------------------------ machinery

        private void RunAsync(string statusText, Action<Action<string>> work)
        {
            if (_busy) return;

            _busy = true;
            SetButtonsEnabled(false);
            SetStatus(statusText, null);

            Thread thread = new Thread(delegate()
            {
                try
                {
                    work(Log);
                }
                catch (Exception ex)
                {
                    Log("ОШИБКА: " + ex.Message);
                }
                finally
                {
                    try
                    {
                        BeginInvoke(new Action(delegate()
                        {
                            _busy = false;
                            SetButtonsEnabled(true);
                            RefreshState();
                        }));
                    }
                    catch
                    {
                        // форма уже закрыта
                    }
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        private void SetButtonsEnabled(bool enabled)
        {
            _install.Enabled = enabled;
            _check.Enabled = enabled;
            _revert.Enabled = enabled;
        }

        private void RefreshState()
        {
            HostsState state = HostsFix.GetState();

            if (state.Applied)
            {
                string text = "Обход включён — домены идут на " + (state.EdgeIp ?? "неизвестный адрес");
                if (state.Ipv6Blocked) text += ", IPv6 отклоняется";
                SetStatus(text, true);
            }
            else
            {
                SetStatus("Обход выключен — записей в hosts нет", false);
            }

            if (state.OtherEntries)
                Log("Внимание: в hosts есть посторонние записи для этих доменов — они могут мешать.");
        }

        private void SetStatus(string text, bool? applied)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string, bool?>(SetStatus), text, applied);
                return;
            }

            Color fill, border, fore, dot;
            if (applied == true)
            {
                fill = Theme.OkBack;
                border = Color.FromArgb(30, 60, 42);
                fore = Theme.OkText;
                dot = Theme.OkDot;
            }
            else
            {
                fill = Theme.WarnBack;
                border = Color.FromArgb(58, 47, 27);
                fore = applied == false ? Theme.WarnText : Theme.Muted;
                dot = Theme.WarnDot;
            }

            _statusCard.Fill = fill;
            _statusCard.BorderColor = border;
            _statusCard.Invalidate();

            _statusDot.BackColor = fill;
            _statusDot.ForeColor = dot;

            _statusText.BackColor = fill;
            _statusText.ForeColor = fore;
            _statusText.Text = text;
        }

        private void Log(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(Log), message);
                return;
            }

            _log.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + Environment.NewLine);
        }
    }
}
