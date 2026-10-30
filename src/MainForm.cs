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

        private readonly Label _title = new Label();
        private readonly Label _subtitle = new Label();
        private readonly Label _langSwitch = new Label();
        private readonly Label _logCaption = new Label();
        private readonly Label _footer = new Label();
        private readonly Label _edgeLabel = new Label();
        private readonly Label _edgeHint = new Label();

        private readonly FlatButton _primary = new FlatButton();
        private readonly FlatButton _check = new FlatButton();
        private readonly CheckBoxDark _ipv6 = new CheckBoxDark();
        private readonly TextBox _edge = new TextBox();
        private readonly TextBox _log = new TextBox();

        private bool _busy;
        private bool _bypassOn;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string subAppName, string subIdList);

        public MainForm()
        {
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
            RefreshState(false);
            ApplyLanguage();

            Log(Strings.LogHosts(HostsFix.HostsPath));
            if (!HostsFix.IsAdmin() && !HostsFix.AdminCheckBypassed)
                Log(Strings.WarnNoAdmin);
            Log(Strings.RestartHint);
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
                // на старых системах останется светлый заголовок
            }
        }

        // ------------------------------------------------------------------- ui

        private void BuildUi()
        {
            _title.Font = new Font("Segoe UI", 15.5F, FontStyle.Bold);
            _title.ForeColor = Theme.Text;
            _title.AutoSize = false;
            _title.Bounds = new Rectangle(24, 20, 500, 32);
            Controls.Add(_title);

            _langSwitch.AutoSize = false;
            _langSwitch.Bounds = new Rectangle(548, 26, 48, 22);
            _langSwitch.TextAlign = ContentAlignment.MiddleRight;
            _langSwitch.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            _langSwitch.ForeColor = Theme.Faint;
            _langSwitch.Cursor = Cursors.Hand;
            _langSwitch.MouseEnter += delegate { _langSwitch.ForeColor = Theme.Text; };
            _langSwitch.MouseLeave += delegate { _langSwitch.ForeColor = Theme.Faint; };
            _langSwitch.Click += delegate { OnSwitchLanguage(); };
            Controls.Add(_langSwitch);

            _subtitle.ForeColor = Theme.Muted;
            _subtitle.Bounds = new Rectangle(24, 56, 572, 36);
            Controls.Add(_subtitle);

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

            _primary.Bounds = new Rectangle(24, 172, 280, 42);
            _primary.Click += delegate { OnPrimary(); };
            Controls.Add(_primary);

            _check.Bounds = new Rectangle(312, 172, 284, 42);
            _check.NormalBack = Theme.Surface;
            _check.HoverBack = Theme.SurfaceHover;
            _check.PressedBack = Theme.SurfacePressed;
            _check.BorderColor = Theme.Border;
            _check.ForeColor = Theme.Text;
            _check.Click += delegate { OnCheck(); };
            Controls.Add(_check);

            Card settings = new Card();
            settings.Bounds = new Rectangle(24, 230, 572, 96);
            settings.Fill = Theme.Surface;
            settings.BorderColor = Theme.Border;
            Controls.Add(settings);

            _ipv6.BackColor = Theme.Surface;
            _ipv6.ForeColor = Theme.Text;
            _ipv6.Bounds = new Rectangle(16, 16, 470, 22);
            settings.Controls.Add(_ipv6);

            _edgeLabel.ForeColor = Theme.Muted;
            _edgeLabel.BackColor = Theme.Surface;
            _edgeLabel.Bounds = new Rectangle(16, 50, 78, 30);
            _edgeLabel.TextAlign = ContentAlignment.MiddleLeft;
            settings.Controls.Add(_edgeLabel);

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

            _edgeHint.ForeColor = Theme.Faint;
            _edgeHint.BackColor = Theme.Surface;
            _edgeHint.Bounds = new Rectangle(246, 50, 310, 30);
            _edgeHint.TextAlign = ContentAlignment.MiddleLeft;
            settings.Controls.Add(_edgeHint);

            _logCaption.Font = new Font("Segoe UI", 8.25F, FontStyle.Bold);
            _logCaption.ForeColor = Theme.Faint;
            _logCaption.Bounds = new Rectangle(24, 338, 200, 18);
            Controls.Add(_logCaption);

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

            _footer.Font = new Font("Segoe UI", 8F);
            _footer.ForeColor = Theme.Faint;
            _footer.Bounds = new Rectangle(24, 540, 572, 18);
            Controls.Add(_footer);

            AcceptButton = _primary;
        }

        /// <summary>Применяет текущий язык ко всем подписям окна.</summary>
        private void ApplyLanguage()
        {
            Text = Strings.WindowTitle;
            _title.Text = Strings.WindowTitle;
            _subtitle.Text = Strings.Subtitle;
            _check.Text = Strings.ButtonCheck;
            _ipv6.Text = Strings.Ipv6Caption;
            _edgeLabel.Text = Strings.EdgeCaption;
            _edgeHint.Text = Strings.EdgeHint(HostsFix.BackupEdgeIp);
            _logCaption.Text = Strings.LogCaption;
            _footer.Text = Strings.Footer;
            _langSwitch.Text = Strings.OtherLanguageCode.ToUpperInvariant();

            _ipv6.Invalidate();
            UpdatePrimary();
            RefreshState(false);
        }

        private void OnSwitchLanguage()
        {
            if (_busy) return;

            Strings.SetLanguage(Strings.OtherLanguageCode);
            Strings.Save();
            ApplyLanguage();
        }

        /// <summary>Внешний вид главной кнопки: включить (акцент) или выключить (осторожно).</summary>
        private void UpdatePrimary()
        {
            _primary.Text = _bypassOn ? Strings.ButtonDisable : Strings.ButtonEnable;

            if (_bypassOn)
            {
                _primary.NormalBack = Theme.Surface;
                _primary.HoverBack = Color.FromArgb(46, 30, 34);
                _primary.PressedBack = Color.FromArgb(38, 25, 29);
                _primary.BorderColor = Color.FromArgb(82, 44, 50);
                _primary.ForeColor = Color.FromArgb(248, 113, 113);
                _primary.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            }
            else
            {
                _primary.NormalBack = Theme.Accent;
                _primary.HoverBack = Theme.AccentHover;
                _primary.PressedBack = Theme.AccentPressed;
                _primary.BorderColor = Color.Transparent;
                _primary.ForeColor = Color.White;
                _primary.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            }

            _primary.Invalidate();
        }

        // -------------------------------------------------------------- actions

        private void OnPrimary()
        {
            if (_bypassOn) OnDisable();
            else OnInstall();
        }

        private void OnInstall()
        {
            string ip = _edge.Text.Trim();
            bool blockIpv6 = _ipv6.Checked;

            RunAsync(Strings.BusyInstall, delegate(Action<string> log)
            {
                HostsFix.Install(ip, blockIpv6, log);
                log(string.Empty);
                log(Strings.DoneMessage);
            });
        }

        private void OnDisable()
        {
            RunAsync(Strings.BusyDisable, delegate(Action<string> log)
            {
                HostsFix.Revert(log);
            });
        }

        private void OnCheck()
        {
            string fallback = _edge.Text.Trim();
            RunAsync(Strings.BusyCheck, delegate(Action<string> log)
            {
                HostsState state = HostsFix.GetState();
                string expected = state.Applied && state.EdgeIp != null ? state.EdgeIp : fallback;

                if (!state.Applied) log(Strings.CheckNotApplied);

                log(Strings.CheckExpected(expected));
                log(string.Empty);
                NetworkCheck.RunAll(expected, log);
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
                    Log(Strings.ErrorPrefix(ex.Message));
                }
                finally
                {
                    try
                    {
                        BeginInvoke(new Action(delegate()
                        {
                            _busy = false;
                            SetButtonsEnabled(true);
                            RefreshState(true);
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
            _primary.Enabled = enabled;
            _check.Enabled = enabled;
        }

        private void RefreshState(bool logWarnings)
        {
            HostsState state = HostsFix.GetState();
            _bypassOn = state.Applied;

            if (state.Applied)
            {
                string text = Strings.StatusOn(state.EdgeIp ?? "?");
                if (state.Ipv6Blocked) text += Strings.Ipv6Suffix;
                SetStatus(text, true);
            }
            else
            {
                SetStatus(Strings.StatusOff, false);
            }

            UpdatePrimary();

            if (logWarnings && state.OtherEntries)
                Log(Strings.WarnOtherEntries);
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
