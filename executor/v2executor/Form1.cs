// © BNE Softworks (github.com/newilf7871)
// chrome webview tabs from https://github.com/adamschwartz/chrome-tabs credit to him
// licensed under GNU GPLv3 (see LICENSE)
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.IO;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Diagnostics;
using Newtonsoft.Json;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;

namespace v2executor
{
    public partial class Form1 : Form
    {
        private Dictionary<string, string> SavedItems = new Dictionary<string, string>();
        private Panel _contentPanel;
        private string _currentView = "editor";
        private bool _isWebViewReady = false;
        private WebView2 _tabWebView;
        private List<RichTextBox> _editors = new List<RichTextBox>();
        private List<string> _tabNames = new List<string>();
        private int _activeTab = 0;
        private readonly string SaveFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "v2executor", "saved_scripts.json");
        private readonly string TabsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "v2executor", "tabs.json");

        private bool _dragging = false;
        private Point _dragStart;

        private bool _isFading = false;

        public Form1()
        {
            InitializeComponent();
            Opacity = 0;
            Load += Form1_Load;
            Resize += Form1_Resize;
            LoadSavedItemsFromDisk();
            SetupTabs();
            LoadTabsFromDisk();
            SetupContentPanel();
            SetupHomeButton();
            ApplyTheme();
            button4.Enabled = SavedItems.Count > 0;

            string flagPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "v2executor", "installed.flag");
            if (!File.Exists(flagPath))
            {
                ShowSplash();
                Directory.CreateDirectory(Path.GetDirectoryName(flagPath));
                File.WriteAllText(flagPath, "1");
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    ("https://discord.gg/GBAsdy9Atc")
                { UseShellExecute = true });

                Task.Run(async () =>
                {
                    try
                    {
                        int userCount = IncrementUserCount();
                        await SendToDiscordWebhook(userCount);
                    }
                    catch { }
                });
            }

            string countPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "v2executor", "user_count.txt");
            if (!File.Exists(countPath))
            {
                Task.Run(async () =>
                {
                    try
                    {
                        int userCount = IncrementUserCount();
                        await SendToDiscordWebhook(userCount);
                    }
                    catch { }
                });
            }

            string beginnerFlag = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "v2executor", "beginner.flag");
            if (!File.Exists(beginnerFlag))
            {
                File.WriteAllText(beginnerFlag, "1");
                ShowBeginnerCheck();
            }

            var logoPic = new PictureBox
            {
                Location = new Point(8, 4),
                Size = new Size(120, 28),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };
            Controls.Add(logoPic);
            logoPic.BringToFront();
            Task.Run(async () =>
            {
                try
                {
                    using var http = new System.Net.Http.HttpClient();
                    http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
                    var bytes = await http.GetByteArrayAsync("https://files.catbox.moe/v85gjt.png");
                    var ms = new System.IO.MemoryStream(bytes);
                    var bmp = new Bitmap(ms);
                    logoPic.Invoke((Action)(() => logoPic.Image = bmp));
                }
                catch { }
            });
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            FadeIn();
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized && !_isFading)
            {
                FadeOut();
            }
            else if (WindowState == FormWindowState.Normal && Opacity < 1 && !_isFading)
            {
                FadeIn();
            }
        }

        private async void FadeIn()
        {
            if (_isFading) return;
            _isFading = true;
            Show();
            for (double i = 0; i <= 1; i += 0.05)
            {
                Opacity = i;
                await Task.Delay(15);
            }
            Opacity = 1;
            _isFading = false;
        }

        private async void FadeOut()
        {
            if (_isFading) return;
            _isFading = true;
            for (double i = 1; i >= 0; i -= 0.05)
            {
                Opacity = i;
                await Task.Delay(15);
            }
            Opacity = 0;
            _isFading = false;
        }


        private async void SetupTabs()
        {
            if (_tabWebView != null) { Controls.Remove(_tabWebView); _tabWebView.Dispose(); _tabWebView = null; }
            _editors.Clear(); _tabNames.Clear(); _activeTab = 0;

            _tabWebView = new WebView2
            {
                Location = new Point(52, 36),
                Size = new Size(748, 36),
                BackColor = Theme.Surface,
                DefaultBackgroundColor = Color.Transparent
            };
            Controls.Add(_tabWebView);

            richTextBox1.Location = new Point(52, 72);
            richTextBox1.Size = new Size(748, 368);
            richTextBox1.Visible = true;
            richTextBox1.TextChanged -= RichTextBox1_TextChanged;
            richTextBox1.TextChanged += RichTextBox1_TextChanged;
            richTextBox1.TextChanged += (s, e) => ApplyLuauHighlighting(richTextBox1);
            _editors.Add(richTextBox1);
            _tabNames.Add("tab 1");

            await InitializeWebView2();

            AddTabToUI("tab 1");

            richTextBox1.BringToFront();
            _tabWebView.BringToFront();
            button1.BringToFront();
            button2.BringToFront();
        }

        private async Task InitializeWebView2()
        {
            try
            {
                var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(Path.GetTempPath(), "v2executor_wv2"));
                await _tabWebView.EnsureCoreWebView2Async(env);
                
                string tabsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tabs");
                if (!Directory.Exists(tabsDir))
                {
                    tabsDir = Path.Combine(Directory.GetCurrentDirectory(), "tabs");
                }

                if (!Directory.Exists(tabsDir))
                {
                    Notify("tabs folder not found: " + tabsDir);
                    return;
                }

                _tabWebView.CoreWebView2.SetVirtualHostNameToFolderMapping("v2executor.tabs", tabsDir, CoreWebView2HostResourceAccessKind.Allow);

                var navTcs = new TaskCompletionSource<bool>();
                _tabWebView.CoreWebView2.NavigationCompleted += (s2, e2) =>
                {
                    if (e2.IsSuccess) navTcs.TrySetResult(true);
                    else navTcs.TrySetException(new Exception($"Navigation failed: {e2.WebErrorStatus}"));
                };

                _tabWebView.Source = new Uri("http://v2executor.tabs/index.html");
                await navTcs.Task;
                _isWebViewReady = true;

                _tabWebView.WebMessageReceived += (s, e) =>
                {
                    var msg = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(e.WebMessageAsJson);
                    string type = msg.GetProperty("type").GetString() ?? "";
                    
                    if (type == "activeTabChange")
                    {
                        int index = msg.GetProperty("index").GetInt32();
                        SwitchTab(index, fromJS: true);
                    }
                    else if (type == "tabRemove")
                    {
                        int index = msg.GetProperty("index").GetInt32();
                        CloseTab(index, fromJS: true);
                    }
                    else if (type == "tabReorder")
                    {
                        int oldIdx = msg.GetProperty("originIndex").GetInt32();
                        int newIdx = msg.GetProperty("destinationIndex").GetInt32();
                        ReorderTabs(oldIdx, newIdx);
                    }
                };
            }
            catch (Exception ex)
            {
                Notify("Failed to initialize WebView2: " + ex.Message);
            }
        }

        private void AddTab(string name, int index, bool isFirst = false)
        {
            if (!isFirst)
            {
                var ed = new RichTextBox
                {
                    Location = new Point(52, 72),
                    Size = new Size(748, 368),
                    BackColor = Theme.EditorBg,
                    ForeColor = Theme.TextPrimary,
                    BorderStyle = BorderStyle.None,
                    Font = new Font("Consolas", 10.5f),
                    ScrollBars = RichTextBoxScrollBars.Vertical,
                    Visible = false
                };
                ed.TextChanged += (s, e) => { if (!_loadingTabs) WriteTabsToDisk(); };
                ed.TextChanged += (s2, e2) => ApplyLuauHighlighting((RichTextBox)s2);
                Controls.Add(ed);
                _editors.Add(ed);
                _tabNames.Add(name);
                button1.BringToFront();
                button2.BringToFront();
            }

            AddTabToUI(name);
        }

        private void AddTabToUI(string name)
        {
            if (_tabWebView.CoreWebView2 != null)
            {
                _tabWebView.ExecuteScriptAsync($"window.addTab('{name.Replace("'", "\\'")}')");
            }
        }

        private void SwitchTab(int index, bool fromJS = false)
        {
            if (index < 0 || index >= _editors.Count) return;
            
            _editors[_activeTab].Visible = false;
            _activeTab = index;
            _editors[_activeTab].Visible = true;
            _editors[_activeTab].BringToFront();
            button1.BringToFront();
            button2.BringToFront();

            if (!fromJS && _tabWebView.CoreWebView2 != null)
            {
                _tabWebView.ExecuteScriptAsync($"window.setCurrentTab({index})");
            }
        }

        private void CloseTab(int index, bool fromJS = false)
        {
            if (_editors.Count <= 1)
            {
                if (fromJS) AddTabToUI(_tabNames[0]);
                return; 
            }

            _editors[index].Visible = false;
            if (index > 0) Controls.Remove(_editors[index]);
            _editors.RemoveAt(index);
            if (index < _tabNames.Count) _tabNames.RemoveAt(index);

            if (!fromJS && _tabWebView.CoreWebView2 != null)
            {
                _tabWebView.ExecuteScriptAsync($"window.removeTab({index})");
            }

            if (_activeTab >= _editors.Count) _activeTab = _editors.Count - 1;
            SwitchTab(_activeTab);
            WriteTabsToDisk();
        }

        private void ReorderTabs(int oldIdx, int newIdx)
        {
            if (oldIdx == newIdx) return;
            
            var ed = _editors[oldIdx];
            _editors.RemoveAt(oldIdx);
            _editors.Insert(newIdx, ed);
            
            var name = _tabNames[oldIdx];
            _tabNames.RemoveAt(oldIdx);
            _tabNames.Insert(newIdx, name);
            
            if (_activeTab == oldIdx) _activeTab = newIdx;
            else if (oldIdx < _activeTab && newIdx >= _activeTab) _activeTab--;
            else if (oldIdx > _activeTab && newIdx <= _activeTab) _activeTab++;
            
            WriteTabsToDisk();
        }

        private void NewTab()
        {
            int idx = _editors.Count;
            string name = InputDialog("tab name:", "tab " + (idx + 1));
            if (string.IsNullOrWhiteSpace(name)) name = "tab " + (idx + 1);
            AddTab(name, idx);
            SwitchTab(idx);
        }


        private void ShowBeginnerCheck()
        {
            var d = new DoubleBufferedForm
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.CenterScreen,
                Size = new Size(340, 180),
                BackColor = Theme.Background,
                ShowInTaskbar = false
            };
            d.Region = MakeRoundedRegion(new Rectangle(0, 0, 340, 180), 10);

            d.Paint += (s, pe) =>
            {
                using var ab = new System.Drawing.Drawing2D.LinearGradientBrush(
                    new Point(0, 0), new Point(340, 0),
                    Theme.Accent, Color.FromArgb(80, 99, 102, 241));
                pe.Graphics.FillRectangle(ab, 0, 0, 340, 3);
                using var border = MakeRoundedPath(new Rectangle(0, 0, 339, 179), 10);
                pe.Graphics.DrawPath(new Pen(Theme.Border, 1), border);
            };

            var title = new Label
            {
                Text = "are you a beginner?",
                Location = new Point(0, 30),
                Size = new Size(340, 32),
                ForeColor = Theme.TextPrimary,
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };

            var sub = new Label
            {
                Text = "helps us personalise your experience",
                Location = new Point(0, 64),
                Size = new Size(340, 20),
                ForeColor = Theme.TextMuted,
                Font = new Font("Segoe UI", 8.5f),
                TextAlign = ContentAlignment.MiddleCenter
            };

            var yes = RBtn("yes", new Point(60, 106), new Size(100, 38), Theme.Accent, Theme.AccentHover, Color.White);
            var no = RBtn("no", new Point(178, 106), new Size(100, 38), Theme.SurfaceAlt, Theme.Border, Theme.TextPrimary);

            yes.Click += (s, e) =>
            {
                d.Close();
                ShowTryScript();
            };
            no.Click += (s, e) =>
            {
                d.Close();
                SetWelcomeText();
            };

            d.Controls.AddRange(new Control[] { title, sub, yes, no });
            d.ShowDialog(this);
        }

        private void ShowTryScript()
        {
            var d = new DoubleBufferedForm
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.CenterScreen,
                Size = new Size(380, 220),
                BackColor = Theme.Background,
                ShowInTaskbar = false
            };
            d.Region = MakeRoundedRegion(new Rectangle(0, 0, 380, 220), 10);

            d.Paint += (s, pe) =>
            {
                using var ab = new System.Drawing.Drawing2D.LinearGradientBrush(
                    new Point(0, 0), new Point(380, 0),
                    Theme.Accent, Color.FromArgb(80, 99, 102, 241));
                pe.Graphics.FillRectangle(ab, 0, 0, 380, 3);
                using var border = MakeRoundedPath(new Rectangle(0, 0, 379, 219), 10);
                pe.Graphics.DrawPath(new Pen(Theme.Border, 1), border);
            };

            var title = new Label
            {
                Text = "try this script!",
                Location = new Point(0, 28),
                Size = new Size(380, 32),
                ForeColor = Theme.TextPrimary,
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };

            var sub = new Label
            {
                Text = "infinite yield — a popular admin script" + Environment.NewLine + "great for beginners to explore the game!",
                Location = new Point(20, 68),
                Size = new Size(340, 40),
                ForeColor = Theme.TextMuted,
                Font = new Font("Segoe UI", 8.5f),
                TextAlign = ContentAlignment.MiddleCenter
            };

            var alright = RBtn("alright!", new Point(40, 130), new Size(130, 40), Theme.Accent, Theme.AccentHover, Color.White);
            var nothanks = RBtn("no thanks", new Point(206, 130), new Size(130, 40), Theme.SurfaceAlt, Theme.Border, Theme.TextPrimary);

            alright.Click += (s, e) =>
            {
                ActiveEditor.Text = @"loadstring(game:HttpGet(""https://raw.githubusercontent.com/EdgeIY/infiniteyield/master/source""))()";
                d.Close();
            };
            nothanks.Click += (s, e) => { d.Close(); SetWelcomeText(); };

            d.Controls.AddRange(new Control[] { title, sub, alright, nothanks });
            d.ShowDialog(this);
        }


        private void ShowSplash()
        {
            var splash = new DoubleBufferedForm
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.CenterScreen,
                Size = new Size(480, 280),
                BackColor = Color.FromArgb(15, 15, 20),
                ShowInTaskbar = false
            };
            splash.Region = MakeRoundedRegion(new Rectangle(0, 0, 480, 280), 14);

            float progress = 0f;
            int step = 0;
            string[] steps = { "initializing...", "loading components...", "connecting to services...", "preparing executor...", "almost ready..." };
            Image splashLogo = null;

            Task.Run(async () =>
            {
                try
                {
                    using var http = new System.Net.Http.HttpClient();
                    http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
                    var bytes = await http.GetByteArrayAsync("https://files.catbox.moe/v85gjt.png");
                    var ms = new System.IO.MemoryStream(bytes);
                    splashLogo = new Bitmap(ms);
                    if (!splash.IsDisposed) splash.Invoke((Action)splash.Invalidate);
                }
                catch { }
            });

            splash.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                g.FillRectangle(new SolidBrush(Color.FromArgb(15, 15, 20)), splash.ClientRectangle);

                using var borderPath = MakeRoundedPath(new Rectangle(0, 0, 479, 279), 14);
                g.DrawPath(new Pen(Color.FromArgb(45, 45, 65), 1), borderPath);

                using var ab = new System.Drawing.Drawing2D.LinearGradientBrush(
                    new Point(0, 0), new Point(480, 0),
                    Color.FromArgb(99, 102, 241), Color.FromArgb(60, 99, 102, 241));
                g.FillRectangle(ab, 0, 0, 480, 3);

                if (splashLogo != null)
                {
                    float aspect = (float)splashLogo.Width / splashLogo.Height;
                    int logoH = 60;
                    int logoW = (int)(logoH * aspect);
                    int logoX = (480 - logoW) / 2;
                    g.DrawImage(splashLogo, new Rectangle(logoX, 38, logoW, logoH));
                }
                else
                {
                    using var fb = new Font("Segoe UI", 18f, FontStyle.Bold);
                    using var fbrush = new SolidBrush(Color.FromArgb(99, 102, 241));
                    var t = "bacon & eggs"; var sz = g.MeasureString(t, fb);
                    g.DrawString(t, fb, fbrush, (480 - sz.Width) / 2, 46);
                }

                using var sf2 = new Font("Segoe UI", 9f);
                using var sb2 = new SolidBrush(Color.FromArgb(100, 100, 130));
                var sub = "executor"; var subSz = g.MeasureString(sub, sf2);
                g.DrawString(sub, sf2, sb2, (480 - subSz.Width) / 2, 108);

                g.DrawLine(new Pen(Color.FromArgb(35, 35, 50), 1), 40, 136, 440, 136);

                var track = new Rectangle(40, 158, 400, 8);
                using var tp = MakeRoundedPath(track, 4);
                g.FillPath(new SolidBrush(Color.FromArgb(30, 30, 42)), tp);

                int fw = (int)(400 * progress);
                if (fw > 0)
                {
                    var fill = new Rectangle(40, 158, Math.Min(fw, 400), 8);
                    using var fp2 = MakeRoundedPath(fill, 4);
                    using var fb2 = new System.Drawing.Drawing2D.LinearGradientBrush(
                        new Point(40, 158), new Point(440, 158),
                        Color.FromArgb(99, 102, 241), Color.FromArgb(139, 92, 246));
                    g.FillPath(fb2, fp2);
                }

                using var pf = new Font("Segoe UI", 8f);
                using var pb = new SolidBrush(Color.FromArgb(99, 102, 241));
                var pct = $"{(int)(progress * 100)}%"; var pctSz = g.MeasureString(pct, pf);
                g.DrawString(pct, pf, pb, (480 - pctSz.Width) / 2, 173);

                using var stf = new Font("Segoe UI", 8.5f);
                using var stb = new SolidBrush(Color.FromArgb(100, 100, 130));
                var st = steps[step]; var stSz = g.MeasureString(st, stf);
                g.DrawString(st, stf, stb, (480 - stSz.Width) / 2, 198);

                using var wf = new Font("Segoe UI", 7f);
                using var wb = new SolidBrush(Color.FromArgb(55, 55, 70));
                var wm = "made by ilovefood7871"; var wmSz = g.MeasureString(wm, wf);
                g.DrawString(wm, wf, wb, (480 - wmSz.Width) / 2, 256);
            };

            var t2 = new System.Windows.Forms.Timer { Interval = 50 };
            t2.Tick += (s, e) =>
            {
                progress += 1f / 20f;
                step = Math.Min((int)(progress * steps.Length), steps.Length - 1);
                splash.Invalidate();
                if (progress >= 1f)
                {
                    progress = 1f; t2.Stop();
                    var ct = new System.Windows.Forms.Timer { Interval = 300 };
                    ct.Tick += (s2, e2) => { ct.Stop(); splash.Close(); };
                    ct.Start();
                }
            };
            t2.Start();
            splash.ShowDialog();
        }

        private System.Drawing.Drawing2D.GraphicsPath MakeRoundedPath(Rectangle r, int rad)
        {
            var p = new System.Drawing.Drawing2D.GraphicsPath();
            p.AddArc(r.X, r.Y, rad * 2, rad * 2, 180, 90);
            p.AddArc(r.Right - rad * 2, r.Y, rad * 2, rad * 2, 270, 90);
            p.AddArc(r.Right - rad * 2, r.Bottom - rad * 2, rad * 2, rad * 2, 0, 90);
            p.AddArc(r.X, r.Bottom - rad * 2, rad * 2, rad * 2, 90, 90);
            p.CloseFigure();
            return p;
        }

        private Region MakeRoundedRegion(Rectangle r, int rad)
        {
            using var p = MakeRoundedPath(r, rad);
            return new Region(p);
        }


        private const int TitleBarHeight = 36;
        private Rectangle _closeBtn => new Rectangle(ClientSize.Width - 46, 0, 46, TitleBarHeight);
        private Rectangle _minimizeBtn => new Rectangle(ClientSize.Width - 92, 0, 46, TitleBarHeight);
        private bool _hoverClose, _hoverMinimize;
        private bool _fadingOut = false;

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_fadingOut && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                _fadingOut = true;
                var timer = new System.Windows.Forms.Timer { Interval = 16 };
                timer.Tick += (s, ev) =>
                {
                    if (Opacity > 0)
                    {
                        Opacity -= 0.08;
                    }
                    else
                    {
                        timer.Stop();
                        Close();
                    }
                };
                timer.Start();
            }
            else
            {
                base.OnFormClosing(e);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            using var sidebarBrush = new SolidBrush(Theme.Surface);
            e.Graphics.FillRectangle(sidebarBrush, 0, 0, 52, ClientSize.Height);

            using var sideBorderPen = new Pen(Theme.Border, 1);
            e.Graphics.DrawLine(sideBorderPen, 51, 0, 51, ClientSize.Height);

            using var titleBrush = new SolidBrush(Theme.Surface);
            e.Graphics.FillRectangle(titleBrush, 0, 0, ClientSize.Width, TitleBarHeight);

            if (_hoverClose)
                e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(196, 43, 28)), _closeBtn);
            using var symFont = new Font("Segoe UI", 10f);
            var closeFg = _hoverClose ? Color.White : Theme.TextMuted;
            TextRenderer.DrawText(e.Graphics, "✕", symFont, _closeBtn, closeFg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            if (_hoverMinimize)
                e.Graphics.FillRectangle(new SolidBrush(Theme.SurfaceAlt), _minimizeBtn);
            var minFg = _hoverMinimize ? Theme.TextPrimary : Theme.TextMuted;
            TextRenderer.DrawText(e.Graphics, "─", symFont, _minimizeBtn, minFg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            if (ClientSize.Width > 0)
            {
                using var accentBrush = new LinearGradientBrush(
                    new Point(0, TitleBarHeight), new Point(ClientSize.Width, TitleBarHeight),
                    Theme.Accent, Color.FromArgb(80, 99, 102, 241));
                e.Graphics.FillRectangle(accentBrush, 0, TitleBarHeight, ClientSize.Width, 2);
            }

            using var wmFont = new Font("Segoe UI", 7.5f);
            using var wmBrush = new SolidBrush(Theme.TextMuted);
            var wmSize = e.Graphics.MeasureString("made by ilovefood7871", wmFont);
            e.Graphics.DrawString("made by ilovefood7871", wmFont, wmBrush,
                (ClientSize.Width - wmSize.Width) / 2, (TitleBarHeight - wmSize.Height) / 2);

            using var sepPen = new Pen(Theme.Border, 1);
            e.Graphics.DrawLine(sepPen, 52, 450, ClientSize.Width - 6, 450);
            e.Graphics.DrawLine(sepPen, 8, 360, 44, 360);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (_closeBtn.Contains(e.Location)) { Close(); return; }
                if (_minimizeBtn.Contains(e.Location)) { WindowState = FormWindowState.Minimized; return; }
                if (e.Y <= TitleBarHeight) { _dragging = true; _dragStart = e.Location; }
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging)
            {
                Location = new Point(Location.X + e.X - _dragStart.X, Location.Y + e.Y - _dragStart.Y);
            }
            else
            {
                bool hc = _closeBtn.Contains(e.Location);
                bool hm = _minimizeBtn.Contains(e.Location);
                if (hc != _hoverClose || hm != _hoverMinimize)
                {
                    _hoverClose = hc; _hoverMinimize = hm;
                    Invalidate(new Rectangle(ClientSize.Width - 92, 0, 92, TitleBarHeight));
                }
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _dragging = false;
            base.OnMouseUp(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hoverClose = false; _hoverMinimize = false;
            Invalidate(new Rectangle(ClientSize.Width - 92, 0, 92, TitleBarHeight));
            base.OnMouseLeave(e);
        }


        private void SetupHomeButton()
        {
            var home = new RoundBtn
            {
                Location = new Point(6, 64),
                Size = new Size(40, 40),
                Text = "🏠",
                Font = new Font("Segoe UI", 16f),
                NormalColor = Theme.SurfaceAlt,
                HoverColor = Theme.Border,
                ForeColor = Theme.TextPrimary,
                Cursor = Cursors.Hand
            };
            home.Click += (s, e) => ShowView("editor");

            var newTabBtn = new RoundBtn
            {
                Location = new Point(6, 380),
                Size = new Size(40, 24),
                Text = "+",
                Tag = "newtab",
                Font = new Font("Segoe UI", 12f),
                NormalColor = Theme.SurfaceAlt,
                HoverColor = Theme.Border,
                ForeColor = Theme.TextPrimary,
                Cursor = Cursors.Hand
            };
            newTabBtn.Click += (s, e) => NewTab();
            Controls.Add(newTabBtn);
            newTabBtn.BringToFront();
            Controls.Add(home);
            home.BringToFront();
        }

        private void SetupContentPanel()
        {
            _contentPanel = new Panel
            {
                Location = new Point(52, 72),
                Size = new Size(748, 368),
                BackColor = Theme.Background,
                Visible = false
            };
            Controls.Add(_contentPanel);
        }

        private void ShowView(string view)
        {
            _currentView = view;
            _contentPanel.Controls.Clear();
            bool isEditor = view == "editor";
            _contentPanel.Visible = !isEditor;
            foreach (var ed in _editors) ed.Visible = isEditor && ed == ActiveEditor;
            if (_tabWebView != null) _tabWebView.Visible = isEditor;
            if (!isEditor) { _contentPanel.BringToFront(); }
            else { foreach (var ed in _editors) if (ed.Visible) ed.BringToFront(); _tabWebView?.BringToFront(); button1.BringToFront(); button2.BringToFront(); }

            if (view == "saved") BuildSavedView();
            if (view == "search") BuildSearchView();
            if (view == "save") BuildSaveView();
        }

        private void BuildSaveView()
        {
            _contentPanel.BackColor = Theme.Background;

            var title = new Label
            {
                Text = "save script",
                Location = new Point(16, 16),
                Size = new Size(400, 28),
                ForeColor = Theme.TextPrimary,
                Font = new Font("Segoe UI", 13f, FontStyle.Bold)
            };

            var lbl = new Label
            {
                Text = "give this a name:",
                Location = new Point(16, 58),
                Size = new Size(300, 20),
                ForeColor = Theme.TextMuted,
                Font = new Font("Segoe UI", 9f)
            };

            var wrap = new RoundPanel
            {
                Location = new Point(16, 82),
                Size = new Size(400, 38),
                BackColor = Theme.Surface,
                Radius = 8,
                ShowBorder = true
            };
            var txt = new TextBox
            {
                Text = "script " + (SavedItems.Count + 1),
                Location = new Point(10, 9),
                Size = new Size(378, 20),
                BorderStyle = BorderStyle.None,
                BackColor = Theme.Surface,
                ForeColor = Theme.TextPrimary,
                Font = new Font("Segoe UI", 10f)
            };
            wrap.Controls.Add(txt);

            var save = RBtn("💾  save", new Point(16, 136), new Size(120, 38), Theme.Accent, Theme.AccentHover, Color.White);
            save.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(ActiveEditor.Text)) { ShowView("editor"); return; }
                string name = txt.Text.Trim();
                if (string.IsNullOrWhiteSpace(name)) return;
                SavedItems[name] = ActiveEditor.Text;
                WriteSavedItemsToDisk();
                button4.Enabled = true;
                if (_welcomeShown) { ActiveEditor.Clear(); _welcomeShown = false; }
                ShowView("editor");
            };

            var cancel = RBtn("cancel", new Point(144, 136), new Size(90, 38), Theme.SurfaceAlt, Theme.Border, Theme.TextPrimary);
            cancel.Click += (s, e) => ShowView("editor");

            _contentPanel.Controls.AddRange(new Control[] { title, lbl, wrap, save, cancel });
        }

        private void BuildSavedView()
        {
            _contentPanel.BackColor = Theme.Background;

            var title = new Label
            {
                Text = "saved scripts",
                Location = new Point(16, 16),
                Size = new Size(400, 28),
                ForeColor = Theme.TextPrimary,
                Font = new Font("Segoe UI", 13f, FontStyle.Bold)
            };

            var scroll = new Panel
            {
                Location = new Point(16, 54),
                Size = new Size(720, 350),
                AutoScroll = true,
                BackColor = Color.Transparent
            };

            void Rebuild()
            {
                scroll.Controls.Clear();
                int y = 0;
                foreach (var key in SavedItems.Keys.ToList())
                {
                    string k = key;
                    var card = new Panel
                    {
                        Location = new Point(0, y),
                        Size = new Size(700, 48),
                        BackColor = Theme.Surface
                    };
                    var lbl = new Label
                    {
                        Text = k,
                        Location = new Point(12, 14),
                        Size = new Size(560, 20),
                        ForeColor = Theme.TextPrimary,
                        Font = new Font("Segoe UI", 9f)
                    };
                    var load = RBtn("load", new Point(580, 8), new Size(60, 32), Theme.Accent, Theme.AccentHover, Color.White);
                    load.Click += (s, ev) => { ActiveEditor.Text = SavedItems[k]; ShowView("editor"); };
                    var del = RBtn("✕", new Point(648, 8), new Size(36, 32), Theme.Danger, Theme.DangerHover, Color.White);
                    del.Click += (s, ev) =>
                    {
                        SavedItems.Remove(k); WriteSavedItemsToDisk();
                        button4.Enabled = SavedItems.Count > 0;
                        if (SavedItems.Count == 0) { ShowView("editor"); return; }
                        Rebuild();
                    };
                    card.Controls.AddRange(new Control[] { lbl, load, del });
                    scroll.Controls.Add(card);
                    y += 56;
                }
            }
            Rebuild();
            _contentPanel.Controls.AddRange(new Control[] { title, scroll });
        }

        private void BuildSearchView()
        {
            _contentPanel.BackColor = Theme.Background;

            var title = new Label
            {
                Text = "search scripts",
                Location = new Point(16, 16),
                Size = new Size(400, 28),
                ForeColor = Theme.TextPrimary,
                Font = new Font("Segoe UI", 13f, FontStyle.Bold)
            };

            var poweredBy = new LinkLabel
            {
                Text = "powered by scriptblox.com and rscripts.net",
                Location = new Point(16, 50),
                Size = new Size(400, 18),
                Font = new Font("Segoe UI", 8f),
                ForeColor = Theme.TextMuted,
                LinkColor = Theme.Accent,
                ActiveLinkColor = Theme.AccentHover
            };
            poweredBy.LinkClicked += (s, ev) => System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo("https://scriptblox.com") { UseShellExecute = true });

            var searchWrap = new RoundPanel
            {
                Location = new Point(16, 76),
                Size = new Size(712, 40),
                BackColor = Theme.Surface,
                Radius = 10,
                ShowBorder = false
            };
            var searchBox = new TextBox
            {
                Location = new Point(10, 9),
                Size = new Size(576, 22),
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10f),
                BackColor = Theme.Surface,
                ForeColor = Theme.TextPrimary,
                PlaceholderText = "search scripts..."
            };
            var btnSearch = RBtn("search", new Point(594, 5), new Size(108, 30), Theme.Accent, Theme.AccentHover, Color.White);
            searchWrap.Controls.Add(searchBox);
            searchWrap.Controls.Add(btnSearch);

            var status = new Label
            {
                Location = new Point(16, 124),
                Size = new Size(712, 18),
                Font = new Font("Segoe UI", 8f),
                ForeColor = Theme.TextMuted,
                BackColor = Color.Transparent
            };

            var results = new Panel
            {
                Location = new Point(16, 146),
                Size = new Size(712, 258),
                AutoScroll = true,
                BackColor = Theme.Background
            };

            var scriptMap = new Dictionary<string, string>();
            var descMap = new Dictionary<string, string>();
            var creatorMap = new Dictionary<string, string>();

            void DoSearch(string q)
            {
                results.Controls.Clear();
                scriptMap.Clear(); descMap.Clear(); creatorMap.Clear();
                status.Text = "searching..."; btnSearch.Enabled = false;
                Task.Run(async () =>
                {
                    try
                    {
                        using var http = new System.Net.Http.HttpClient();
                        http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
                        var list = new List<(string title, string raw, string desc, string creator, string source)>();

                        try
                        {
                            var json = await http.GetStringAsync(
                                $"https://rscripts.net/api/v2/scripts?page=1&orderBy=date&sort=desc&q={Uri.EscapeDataString(q)}");
                            var doc = System.Text.Json.JsonDocument.Parse(json);
                            foreach (var s in doc.RootElement.GetProperty("scripts").EnumerateArray())
                            {
                                string t2 = s.TryGetProperty("title", out var t) ? t.GetString() ?? "untitled" : "untitled";
                                string raw = s.TryGetProperty("rawScript", out var r) ? r.GetString() ?? "" : "";
                                string desc = s.TryGetProperty("description", out var d2) ? d2.GetString() ?? "" : "";
                                string creator = "";
                                if (s.TryGetProperty("user", out var u))
                                    creator = u.TryGetProperty("username", out var un) ? un.GetString() ?? "" : "";
                                if (!string.IsNullOrEmpty(raw)) list.Add((t2, raw, desc, creator, "rscripts"));
                            }
                        }
                        catch { }

                        try
                        {
                            var json2 = await http.GetStringAsync(
                                $"https://scriptblox.com/api/script/search?q={Uri.EscapeDataString(q)}&max=20&mode=free");
                            var doc2 = System.Text.Json.JsonDocument.Parse(json2);
                            var scripts = doc2.RootElement.GetProperty("result").GetProperty("scripts");
                            foreach (var s in scripts.EnumerateArray())
                            {
                                string t2 = s.TryGetProperty("title", out var t) ? t.GetString() ?? "untitled" : "untitled";
                                string raw = s.TryGetProperty("script", out var r) ? r.GetString() ?? "" : "";
                                string desc = s.TryGetProperty("game", out var g) ? (g.TryGetProperty("name", out var gn) ? gn.GetString() ?? "" : "") : "";
                                string creator = s.TryGetProperty("owner", out var o) ? (o.TryGetProperty("username", out var un) ? un.GetString() ?? "" : "") : "";
                                if (!string.IsNullOrEmpty(raw)) list.Add((t2, raw, desc, creator, "scriptblox"));
                            }
                        }
                        catch { }

                        _contentPanel.Invoke((Action)(() =>
                        {
                            results.Controls.Clear();
                            int y = 0;
                            foreach (var (t2, raw, desc, creator, source) in list)
                            {
                                string display = (t2.Length > 55 ? t2[..55] + "…" : t2) + " [" + source + "]";
                                scriptMap[display] = raw;
                                descMap[display] = desc;
                                creatorMap[display] = creator;

                                var card = new Panel
                                {
                                    Location = new Point(0, y),
                                    Size = new Size(696, 52),
                                    BackColor = Theme.Surface
                                };

                                var lbl = new Label
                                {
                                    Text = t2.Length > 55 ? t2[..55] + "…" : t2,
                                    Location = new Point(10, 6),
                                    Size = new Size(300, 20),
                                    ForeColor = Theme.TextPrimary,
                                    Font = new Font("Segoe UI", 9f)
                                };

                                var srcLbl = new Label
                                {
                                    Text = source,
                                    Location = new Point(10, 28),
                                    Size = new Size(120, 16),
                                    ForeColor = source == "scriptblox" ? Color.FromArgb(99, 200, 130) : Theme.Accent,
                                    Font = new Font("Segoe UI", 7.5f)
                                };

                                if (!string.IsNullOrEmpty(creator))
                                {
                                    var crLbl = new Label
                                    {
                                        Text = "by " + creator,
                                        Location = new Point(120, 28),
                                        Size = new Size(180, 16),
                                        ForeColor = Theme.TextMuted,
                                        Font = new Font("Segoe UI", 7.5f)
                                    };
                                    card.Controls.Add(crLbl);
                                }

                                var btnInfo = RBtn("ℹ", new Point(398, 10), new Size(32, 32), Theme.SurfaceAlt, Theme.Border, Theme.TextMuted);
                                btnInfo.Font = new Font("Segoe UI", 10f);
                                btnInfo.Tag = display;
                                btnInfo.Click += (s2, ev2) =>
                                {
                                    string key = (string)((RoundBtn)s2).Tag;
                                    string info = string.IsNullOrWhiteSpace(descMap[key]) ? "no description available." : descMap[key];
                                    string cr = creatorMap[key];
                                    if (!string.IsNullOrEmpty(cr)) info = "by " + cr + "\n\n" + info;
                                    Notify(info);
                                };

                                var btnLoad = RBtn("load", new Point(438, 10), new Size(68, 32), Theme.SurfaceAlt, Theme.Border, Theme.TextPrimary);
                                btnLoad.Tag = display;
                                btnLoad.Click += async (s2, ev2) =>
                                {
                                    string key = (string)((RoundBtn)s2).Tag;
                                    status.Text = "loading...";
                                    try
                                    {
                                        string sc = scriptMap[key];
                                        if (sc.StartsWith("http"))
                                        {
                                            using var h = new System.Net.Http.HttpClient();
                                            sc = await h.GetStringAsync(sc);
                                        }
                                        string tabName = t2.Length > 20 ? t2[..20] : t2;
                                        int idx = _editors.Count;
                                        AddTab(tabName, idx);
                                        SwitchTab(idx);
                                        ActiveEditor.Text = sc;
                                        WriteTabsToDisk();
                                        ShowView("editor");
                                    }
                                    catch { status.Text = "failed to load."; }
                                };

                                var btnSave = RBtn("+ save", new Point(514, 10), new Size(84, 32), Theme.Accent, Theme.AccentHover, Color.White);
                                btnSave.Tag = display;
                                btnSave.Click += async (s2, ev2) =>
                                {
                                    string key = (string)((RoundBtn)s2).Tag;
                                    status.Text = "fetching...";
                                    try
                                    {
                                        string sc = scriptMap[key];
                                        if (sc.StartsWith("http")) { using var h = new System.Net.Http.HttpClient(); sc = await h.GetStringAsync(sc); }
                                        string name = (t2.Length > 40 ? t2[..40] : t2);
                                        SavedItems[name] = sc;
                                        WriteSavedItemsToDisk();
                                        button4.Enabled = true;
                                        status.Text = "saved!";
                                    }
                                    catch { status.Text = "failed to save."; }
                                };

                                card.Controls.AddRange(new Control[] { lbl, srcLbl, btnInfo, btnLoad, btnSave });
                                results.Controls.Add(card);
                                y += 60;
                            }
                            status.Text = list.Count == 0 ? "no results." : $"{list.Count} result(s)";
                            btnSearch.Enabled = true;
                        }));
                    }
                    catch (Exception ex)
                    {
                        _contentPanel.Invoke((Action)(() => { status.Text = "error: " + ex.Message; btnSearch.Enabled = true; }));
                    }
                });
            }

            btnSearch.Click += (s, ev) => DoSearch(searchBox.Text);
            searchBox.KeyDown += (s, ev) => { if (ev.KeyCode == Keys.Enter) btnSearch.PerformClick(); };

            _contentPanel.Controls.AddRange(new Control[] { title, poweredBy, searchWrap, status, results });
        }

        private RichTextBox ActiveEditor => _editors.Count > _activeTab ? _editors[_activeTab] : richTextBox1;


        private void ApplyTheme()
        {
            BackColor = Theme.Background;
            foreach (var ed in _editors) { ed.BackColor = Theme.EditorBg; ed.ForeColor = Theme.TextPrimary; ed.BorderStyle = BorderStyle.None; ed.Font = new Font("Consolas", 10.5f); }
            if (_tabWebView != null) 
            { 
                _tabWebView.BackColor = Theme.Surface;
                if (_tabWebView.CoreWebView2 != null)
                {
                    _tabWebView.ExecuteScriptAsync($"window.setTheme({Theme.IsDark.ToString().ToLower()})");
                }
            }

            StyleRoundBtn(button1, Theme.Accent, Theme.AccentHover, Color.White);
            StyleRoundBtn(button2, Theme.SurfaceAlt, Theme.Border, Theme.TextPrimary);
            StyleRoundBtn(button3, Theme.SurfaceAlt, Theme.Border, Theme.TextPrimary, true);
            StyleRoundBtn(button4, Theme.SurfaceAlt, Theme.Border, Theme.TextPrimary, true);
            StyleRoundBtn(button5, Theme.SurfaceAlt, Theme.Border, Theme.TextPrimary, true);
            StyleRoundBtn(button6, Theme.Danger, Theme.DangerHover, Color.White, true);
            StyleRoundBtn(button7, Theme.SurfaceAlt, Theme.Border, Theme.TextPrimary, true);

            button7.Text = Theme.IsDark ? "☀" : "☾";
            Invalidate(true);
        }

        private void StyleRoundBtn(RoundBtn b, Color bg, Color hover, Color fg, bool iconBtn = false)
        {
            b.NormalColor = bg;
            b.HoverColor = hover;
            b.ForeColor = fg;
            b.Font = new Font("Segoe UI", iconBtn ? 16f : 9f);
            b.Cursor = Cursors.Hand;
        }


        private bool _welcomeShown = false;

        private void SetWelcomeText()
        {
            string welcome =
                "-- hi! this message is from the creator of bacon & eggs!" + Environment.NewLine +
                "-- click the trash bin to clear! (click it after you're done reading to clear this message.)" + Environment.NewLine +
                "-- click the magnifying glass to search for thousands of various scripts!" + Environment.NewLine +
                "-- enjoy!";
            ActiveEditor.Text = welcome;
            _welcomeShown = true;
        }


        private bool _highlighting = false;

        private void ApplyLuauHighlighting(RichTextBox rtb)
        {
            if (_highlighting) return;
            _highlighting = true;

            int sel = rtb.SelectionStart;
            int len = rtb.SelectionLength;
            rtb.SuspendLayout();

            string text = rtb.Text;
            int tlen = text.Length;

            rtb.SelectAll();
            rtb.SelectionColor = Theme.TextPrimary;

            var colKeyword = Color.FromArgb(86, 156, 214);
            var colBuiltin = Color.FromArgb(78, 201, 176);
            var colString = Color.FromArgb(206, 145, 120);
            var colComment = Color.FromArgb(106, 153, 85);
            var colNumber = Color.FromArgb(181, 206, 168);
            var colOperator = Color.FromArgb(180, 180, 180);

            string[] keywords = { "and","break","do","else","elseif","end","false","for",
                "function","if","in","local","nil","not","or","repeat","return","then",
                "true","until","while","continue" };
            string[] builtins = { "print","warn","error","require","pcall","xpcall","pairs",
                "ipairs","next","select","type","tostring","tonumber","rawget","rawset",
                "rawequal","rawlen","setmetatable","getmetatable","assert","collectgarbage",
                "dofile","load","loadfile","loadstring","unpack","table","string","math",
                "coroutine","os","io","bit32","utf8","game","workspace","script","wait",
                "spawn","delay","tick","time","Vector3","Vector2","CFrame","Color3","UDim2",
                "UDim","Enum","Instance","BrickColor","Ray","Region3","TweenInfo" };

            void Colorize(int start, int length, Color color)
            {
                if (start < 0 || start + length > tlen) return;
                rtb.Select(start, length);
                rtb.SelectionColor = color;
            }

            int i = 0;
            while (i < tlen)
            {
                if (i + 1 < tlen && text[i] == '-' && text[i + 1] == '-')
                {
                    if (i + 3 < tlen && text[i + 2] == '[' && text[i + 3] == '[')
                    {
                        int end2 = text.IndexOf("]]", i + 4);
                        int clen = end2 < 0 ? tlen - i : end2 + 2 - i;
                        Colorize(i, clen, colComment);
                        i += clen;
                    }
                    else
                    {
                        int nl = text.IndexOf('\n', i);
                        int clen = nl < 0 ? tlen - i : nl - i;
                        Colorize(i, clen, colComment);
                        i += clen;
                    }
                    continue;
                }

                if (text[i] == '"' || text[i] == '\'')
                {
                    char q = text[i];
                    int j = i + 1;
                    while (j < tlen && text[j] != q && text[j] != '\n')
                    {
                        if (text[j] == '\\' && j + 1 < tlen) j++;
                        j++;
                    }
                    if (j < tlen && text[j] == q) j++;
                    Colorize(i, j - i, colString);
                    i = j;
                    continue;
                }

                if (i + 1 < tlen && text[i] == '[' && text[i + 1] == '[')
                {
                    int end2 = text.IndexOf("]]", i + 2);
                    int slen = end2 < 0 ? tlen - i : end2 + 2 - i;
                    Colorize(i, slen, colString);
                    i += slen;
                    continue;
                }

                if (char.IsDigit(text[i]) || (text[i] == '.' && i + 1 < tlen && char.IsDigit(text[i + 1])))
                {
                    int j = i;
                    while (j < tlen && (char.IsDigit(text[j]) || text[j] == '.' || text[j] == 'x' ||
                           text[j] == 'e' || text[j] == 'E' || (j > i && (text[j] == '+' || text[j] == '-'))))
                        j++;
                    Colorize(i, j - i, colNumber);
                    i = j;
                    continue;
                }

                if (char.IsLetter(text[i]) || text[i] == '_')
                {
                    int j = i;
                    while (j < tlen && (char.IsLetterOrDigit(text[j]) || text[j] == '_')) j++;
                    string word = text.Substring(i, j - i);
                    if (Array.Exists(keywords, k => k == word))
                        Colorize(i, j - i, colKeyword);
                    else if (Array.Exists(builtins, k => k == word))
                        Colorize(i, j - i, colBuiltin);
                    i = j;
                    continue;
                }

                i++;
            }

            rtb.Select(sel, len);
            rtb.SelectionColor = Theme.TextPrimary;
            rtb.ResumeLayout();
            _highlighting = false;
        }

        private void RichTextBox1_TextChanged(object sender, EventArgs e)
        {
            if (!_loadingTabs) WriteTabsToDisk();
        }

        private void WriteTabsToDisk()
        {
            try
            {
                var data = new List<object>();
                for (int i = 0; i < _editors.Count; i++)
                    data.Add(new { name = i < _tabNames.Count ? _tabNames[i] : "tab " + (i + 1), text = _editors[i].Text });
                Directory.CreateDirectory(Path.GetDirectoryName(TabsFilePath));
                File.WriteAllText(TabsFilePath, JsonConvert.SerializeObject(data, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Tabs] Save failed: {ex.Message}");
            }
        }

        private bool _loadingTabs = false;

        private async void LoadTabsFromDisk()
        {
            try
            {
                if (!File.Exists(TabsFilePath)) return;
                var raw = File.ReadAllText(TabsFilePath);
                var data = JsonConvert.DeserializeObject<List<Newtonsoft.Json.Linq.JObject>>(raw);
                if (data == null || data.Count == 0) return;

                int attempts = 0;
                while (!_isWebViewReady && attempts < 50)
                {
                    await Task.Delay(100);
                    attempts++;
                }

                if (!_isWebViewReady || _tabWebView?.CoreWebView2 == null) return;

                _loadingTabs = true;

                await _tabWebView.ExecuteScriptAsync("window.clearTabs()");

                richTextBox1.Text = data[0]["text"]?.ToString() ?? "";
                _tabNames[0] = data[0]["name"]?.ToString() ?? "tab 1";
                AddTabToUI(_tabNames[0]);

                for (int i = 1; i < data.Count; i++)
                {
                    string name = data[i]["name"]?.ToString() ?? "tab " + (i + 1);
                    string text = data[i]["text"]?.ToString() ?? "";
                    int idx = _editors.Count;
                    AddTab(name, idx);
                    _editors[idx].Text = text;
                }
                SwitchTab(0);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Tabs] Load failed: {ex.Message}");
            }
            finally
            {
                _loadingTabs = false;
            }
        }

        private void RenameTab(int index)
        {
            if (index < 0 || index >= _tabNames.Count) return;
            string newName = InputDialog("rename tab:", _tabNames[index]);
            if (string.IsNullOrWhiteSpace(newName)) return;
            _tabNames[index] = newName;
            if (_tabWebView.CoreWebView2 != null)
            {
                _tabWebView.ExecuteScriptAsync($"window.renameTab({index}, '{newName.Replace("'", "\\'")}')");
            }
            WriteTabsToDisk();
        }

        private void LoadSavedItemsFromDisk()
        {
            try
            {
                if (File.Exists(SaveFilePath))
                    SavedItems = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(SaveFilePath))
                                 ?? new Dictionary<string, string>();
            }
            catch { SavedItems = new Dictionary<string, string>(); }
        }

        private void WriteSavedItemsToDisk()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SaveFilePath));
                File.WriteAllText(SaveFilePath, JsonConvert.SerializeObject(SavedItems, Formatting.Indented));
            }
            catch { }
        }


        private bool _isAttached = false;
        private readonly string AutoExecPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "autoexec");

        [DllImport("baconandeggs.dll", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        public static extern bool initialize();

        [DllImport("baconandeggs.dll", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        public static extern void execute(byte[] script);

        private void button1_Click(object sender, EventArgs e)
        {
            if (!_isAttached)
            {
                UpdateStatus("not attached");
                return;
            }
            string scriptText = ActiveEditor.Text;
            if (string.IsNullOrWhiteSpace(scriptText))
            {
                UpdateStatus("script is empty");
                return;
            }
            try
            {
                execute(Encoding.UTF8.GetBytes(scriptText + "\0"));
                UpdateStatus("executed");
            }
            catch (Exception ex)
            {
                UpdateStatus("execute failed: " + ex.Message);
                Notify("execute failed: " + ex.Message);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (_isAttached)
            {
                UpdateStatus("already attached");
                return;
            }
            if (!File.Exists("baconandeggs.dll"))
            {
                UpdateStatus("dll not found");
                Notify("baconandeggs.dll not found!");
                return;
            }
            try
            {
                bool result = initialize();
                if (result)
                {
                    _isAttached = true;
                    UpdateStatus("attached");
                    RunAutoExec();
                }
                else
                {
                    int error = Marshal.GetLastWin32Error();
                    UpdateStatus("attach failed: " + error);
                    Notify("attach failed! error code: " + error);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus("attach failed");
                Notify("attach exception: " + ex.Message);
            }
        }

        private void UpdateStatus(string msg)
        {
            if (_statusLabel == null) return;
            _statusLabel.Invoke((Action)(() => _statusLabel.Text = msg));
        }

        private void RunAutoExec()
        {
            try
            {
                if (!Directory.Exists(AutoExecPath)) return;
                var files = Directory.GetFiles(AutoExecPath, "*.lua")
                    .Concat(Directory.GetFiles(AutoExecPath, "*.txt"))
                    .ToArray();
                if (files.Length == 0) return;
                foreach (var file in files)
                {
                    try
                    {
                        var script = File.ReadAllText(file);
                        if (!string.IsNullOrWhiteSpace(script))
                            execute(Encoding.UTF8.GetBytes(script + "\0"));
                    }
                    catch { }
                }
            }
            catch { }
        }

        private void button6_Click(object sender, EventArgs e) { ActiveEditor.Clear(); }
        private void button7_Click(object sender, EventArgs e) { Theme.IsDark = !Theme.IsDark; ApplyTheme(); }


        private void Notify(string msg)
        {
            var d = Popup("notice", 340, 220);
            var lbl = new Label
            {
                Text = msg,
                Location = new Point(16, 16),
                Size = new Size(300, 80),
                ForeColor = Theme.TextPrimary,
                Font = new Font("Segoe UI", 9f)
            };
            var ok = RBtn("ok", new Point(115, 120), new Size(100, 34), Theme.Accent, Theme.AccentHover, Color.White);
            ok.Click += (s, e) => d.Close();
            d.Controls.AddRange(new Control[] { lbl, ok });
            d.ShowDialog(this);
        }

        private string InputDialog(string prompt, string def)
        {
            string result = "";
            var d = Popup("save script", 360, 175);
            var lbl = new Label
            {
                Text = prompt,
                Location = new Point(16, 16),
                Size = new Size(320, 20),
                ForeColor = Theme.TextPrimary,
                Font = new Font("Segoe UI", 9f)
            };
            var wrap = new RoundPanel
            {
                Location = new Point(16, 44),
                Size = new Size(320, 36),
                BackColor = Theme.Surface,
                Radius = 8
            };
            var txt = new TextBox
            {
                Text = def,
                Location = new Point(10, 8),
                Size = new Size(298, 20),
                BorderStyle = BorderStyle.None,
                BackColor = Theme.Surface,
                ForeColor = Theme.TextPrimary,
                Font = new Font("Segoe UI", 10f)
            };
            wrap.Controls.Add(txt);
            var ok = RBtn("ok", new Point(100, 106), new Size(100, 34), Theme.Accent, Theme.AccentHover, Color.White);
            var cancel = RBtn("cancel", new Point(212, 106), new Size(100, 34), Theme.SurfaceAlt, Theme.Border, Theme.TextPrimary);
            ok.Click += (s, e) => { result = txt.Text.Trim(); d.Close(); };
            cancel.Click += (s, e) => d.Close();
            d.Controls.AddRange(new Control[] { lbl, wrap, ok, cancel });
            d.ShowDialog(this);
            return result;
        }

        private Form Popup(string title, int w, int h) => new Form
        {
            Text = title,
            Size = new Size(w, h),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = Theme.Background,
            ForeColor = Theme.TextPrimary
        };

        private RoundBtn RBtn(string text, Point loc, Size size, Color bg, Color hover, Color fg) => new RoundBtn
        {
            Text = text,
            Location = loc,
            Size = size,
            NormalColor = bg,
            HoverColor = hover,
            ForeColor = fg
        };


        private void button3_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ActiveEditor.Text)) { Notify("nothing to save! type something first."); return; }
            ShowView("save");
        }


        private void button4_Click(object sender, EventArgs e)
        {
            ShowView("saved");
        }


        private void button5_Click(object sender, EventArgs e)
        {
            ShowView("search");
        }


        private static int IncrementUserCount()
        {
            try
            {
                string countPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "v2executor", "user_count.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(countPath));
                
                int count = 0;
                if (File.Exists(countPath))
                {
                    int.TryParse(File.ReadAllText(countPath), out count);
                }
                count++;
                File.WriteAllText(countPath, count.ToString());
                return count;
            }
            catch
            {
                return 1;
            }
        }

        private static async Task SendToDiscordWebhook(int userCount)
        {
            try
            {
                using var http = new System.Net.Http.HttpClient();
                var payload = new
                {
                    content = $"New user installed v2executor! Total users: {userCount}"
                };
                var json = JsonConvert.SerializeObject(payload);
                var content = new System.Net.Http.StringContent(json, Encoding.UTF8, "application/json");
                
                await http.PostAsync("webhook", content);
            }
            catch { }
        }
    }

    static class Theme
    {
        public static bool IsDark = true;
        public static Color Background => IsDark ? Color.FromArgb(15, 15, 20) : Color.FromArgb(242, 242, 248);
        public static Color Surface => IsDark ? Color.FromArgb(24, 24, 32) : Color.FromArgb(255, 255, 255);
        public static Color SurfaceAlt => IsDark ? Color.FromArgb(34, 34, 46) : Color.FromArgb(230, 230, 240);
        public static Color Accent => Color.FromArgb(99, 102, 241);
        public static Color AccentHover => Color.FromArgb(79, 82, 221);
        public static Color Success => Color.FromArgb(34, 197, 94);
        public static Color SuccessHover => Color.FromArgb(22, 163, 74);
        public static Color Danger => Color.FromArgb(220, 60, 80);
        public static Color DangerHover => Color.FromArgb(180, 40, 60);
        public static Color TextPrimary => IsDark ? Color.FromArgb(235, 235, 255) : Color.FromArgb(20, 20, 35);
        public static Color TextMuted => IsDark ? Color.FromArgb(100, 100, 130) : Color.FromArgb(140, 140, 170);
        public static Color Border => IsDark ? Color.FromArgb(45, 45, 65) : Color.FromArgb(205, 205, 222);
        public static Color EditorBg => IsDark ? Color.FromArgb(20, 20, 28) : Color.FromArgb(252, 252, 255);
    }

    class RoundBtn : Button
    {
        public int Radius = 22;
        public Color NormalColor = Theme.Accent;
        public Color HoverColor = Theme.AccentHover;
        private bool _hover;

        public RoundBtn()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            FlatAppearance.MouseOverBackColor = Color.Transparent;
            FlatAppearance.MouseDownBackColor = Color.Transparent;
            BackColor = Color.Transparent;
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9f);
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            int rad = Math.Min(Radius, Height);
            using var path = Rounded(ClientRectangle, rad);
            Region = new Region(path);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color bg = !Enabled ? Theme.SurfaceAlt : _hover ? HoverColor : NormalColor;
            Color fg = !Enabled ? Theme.TextMuted : ForeColor;
            int rad = Math.Min(Radius, Height);
            using var path = Rounded(ClientRectangle, rad);
            e.Graphics.FillPath(new SolidBrush(bg), path);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }

        static GraphicsPath Rounded(Rectangle r, int rad)
        {
            rad = Math.Min(rad, r.Height);
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, rad, rad, 180, 90);
            p.AddArc(r.Right - rad, r.Y, rad, rad, 270, 90);
            p.AddArc(r.Right - rad, r.Bottom - rad, rad, rad, 0, 90);
            p.AddArc(r.X, r.Bottom - rad, rad, rad, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    class DoubleBufferedForm : Form
    {
        public DoubleBufferedForm() { DoubleBuffered = true; }
    }

    class RoundPanel : Panel
    {
        public int Radius = 12;
        public bool ShowBorder = true;
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = Rounded(new Rectangle(0, 0, Width - 1, Height - 1), Radius);
            e.Graphics.FillPath(new SolidBrush(BackColor), path);
            if (ShowBorder) e.Graphics.DrawPath(new Pen(Theme.Border, 1), path);
        }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        static GraphicsPath Rounded(Rectangle r, int rad)
        {
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, rad, rad, 180, 90);
            p.AddArc(r.Right - rad, r.Y, rad, rad, 270, 90);
            p.AddArc(r.Right - rad, r.Bottom - rad, rad, rad, 0, 90);
            p.AddArc(r.X, r.Bottom - rad, rad, rad, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
