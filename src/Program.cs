using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace DshNotify
{
    static class Program
    {
        public static bool TestClosing;

        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);

        public static bool DshForeground()
        {
            try
            {
                IntPtr fg = GetForegroundWindow();
                if (fg == IntPtr.Zero) return false;
                uint pid;
                GetWindowThreadProcessId(fg, out pid);
                var p = System.Diagnostics.Process.GetProcessById((int)pid);
                return p.ProcessName == "DeepSeek Harness";
            }
            catch { return false; }
        }

        public const string DSH_EXE = @"D:\dsh\DeepSeek Harness.exe";

        [STAThread]
        static void Main()
        {
            Cfg.EnsureFile();
            try { using (var gg = Graphics.FromHwnd(IntPtr.Zero)) U.S = gg.DpiX / 96f; } catch { U.S = 1f; }
            if (U.S < 1f) U.S = 1f;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool testMode = false, showOnce = false, noWatch = false, askMode = false;
            foreach (string a in Environment.GetCommandLineArgs())
            {
                if (a == "--test") testMode = true;
                else if (a == "--show") showOnce = true;
                else if (a == "--no-watch") noWatch = true;
                else if (a == "--ask") askMode = true;
            }
            if (testMode || showOnce)
            {
                if (testMode) Cfg.MarkTest();
                var tctx = new ApplicationContext();
                var t = new Toast();
                t.ForceTest = testMode;
                t.AskMode = askMode;
                TestClosing = true;
                var once = new System.Windows.Forms.Timer();
                once.Interval = 80;
                once.Tick += delegate
                {
                    once.Stop();
                    once.Dispose();
                    t.ShowToast();
                };
                once.Start();
                Application.Run(tctx);
                return;
            }

            using (var ctx = new Ctx())
            {
                ctx.Start(noWatch);
                Application.Run(ctx);
            }
        }

        public static void ActivateDsh()
        {
            foreach (var p in System.Diagnostics.Process.GetProcessesByName("DeepSeek Harness"))
            {
                if (p.MainWindowHandle != IntPtr.Zero)
                {
                    if (IsIconic(p.MainWindowHandle)) ShowWindow(p.MainWindowHandle, 9);
                    SetForegroundWindow(p.MainWindowHandle);
                    return;
                }
            }
            try { System.Diagnostics.Process.Start(DSH_EXE); } catch { }
        }
    }

    static class U
    {
        public static float S = 1f;
        public static int D(int v) { return (int)Math.Round(v * S); }

        public static Font F(string name, float size, FontStyle st)
        {
            try { return new Font(name, size * S, st); }
            catch { return new Font("Microsoft YaHei UI", size * S, st); }
        }
        public static float D(float v) { return v * S; }
    }

    class Ctx : ApplicationContext
    {
        Toast toast;
        Form marshal;
        NotifyIcon tray;
        FileSystemWatcher watch;
        System.Threading.Timer idle;
        readonly object gate = new object();

        public Ctx()
        {
            toast = new Toast();
            marshal = new Form();
            marshal.FormBorderStyle = FormBorderStyle.None;
            marshal.ShowInTaskbar = false;
            marshal.StartPosition = FormStartPosition.Manual;
            marshal.Location = new Point(-4000, -4000);
            marshal.Size = new Size(1, 1);
            marshal.Show();
            marshal.Hide();
            tray = new NotifyIcon();
            try { tray.Icon = new Icon(@"D:\dsh\resources\tray.ico"); } catch { tray.Icon = SystemIcons.Application; }
            tray.Text = "DSH 完成提醒";
            tray.Visible = true;
            var menu = new ContextMenu();
            menu.MenuItems.Add("显示提醒", delegate { toast.ShowToast(); });
            menu.MenuItems.Add("退出", delegate { ExitThread(); });
            tray.ContextMenu = menu;
            tray.DoubleClick += delegate { Program.ActivateDsh(); };
        }

        public void Start(bool noWatch)
        {
            if (noWatch) return;
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dsh", "sessions");
            if (Directory.Exists(root))
            {
                watch = new FileSystemWatcher(root, "*.zstd");
                watch.IncludeSubdirectories = true;
                watch.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName;
                watch.Changed += OnChange;
                watch.Created += OnChange;
                watch.EnableRaisingEvents = true;
            }
        }

        public static void Log(string m)
        {
            try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "dshnotify.log"), DateTime.Now.ToString("HH:mm:ss") + " " + m + "\r\n"); } catch { }
        }

        public static string LastSession = "";

        void OnChange(object s, FileSystemEventArgs e)
        {
            try
            {
                string dir = Path.GetFileName(Path.GetDirectoryName(e.FullPath));
                if (dir != null && dir.StartsWith("session-"))
                {
                    string id = dir.Substring(8);
                    if (id.Length > 8) id = id.Substring(0, 8);
                    LastSession = id;
                }
            }
            catch { }
            lock (gate)
            {
                if (idle != null) idle.Dispose();
                idle = new System.Threading.Timer(delegate
                {
                    try { marshal.BeginInvoke(new Action(delegate { try { toast.ShowToast(); Ctx.Log("shown h=" + toast.Handle + " vis=" + toast.Visible); } catch (Exception ex) { Ctx.Log("showerr " + ex); } })); } catch (Exception ex2) { Ctx.Log("beginerr " + ex2); }
                }, null, 3000, Timeout.Infinite);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (tray != null) { tray.Visible = false; tray.Dispose(); }
            if (marshal != null) marshal.Dispose();
            if (watch != null) watch.Dispose();
            base.Dispose(disposing);
        }
    }

    static class Cfg
    {
        public static string Title = "\u2714  \u8bf7\u6c42\u5df2\u5b8c\u6210";
        public static string Sub = "\u70b9\u8fd9\u91cc\u56de\u5230 DeepSeek Harness";
        public static string TitleAsk = "\u2753  \u9700\u8981\u4f60\u9009\u62e9";
        public static string SubAsk = "\u70b9\u8fd9\u91cc\u56de\u53bb\u56de\u7b54";
        public static int W = 336, H = 124, AnimIn = 800, AnimOut = 420, Hold = 30000;
        public static int MX = 0, MY = 10, ImgSize = 56;
        public static string Pos = "br", Img = "";
        public static string FontName = "Microsoft YaHei UI";
        public static float TitleSize = 11.5f;
        public static float SubSize = 9f;
        public static string Align = "left";
        public static string ImgPos = "left";
        public static string ExitFx = "auto";
        public static int FxMs = 800;
        public static int TextX = -1;
        public static int TextY = -1;

        public static string DirPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dsh", "dsh-notify");
        public static string FilePath = System.IO.Path.Combine(DirPath, "config.ini");

        public static string StampPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dsh-notify.teststamp");

        public static void MarkTest()
        {
            try { System.IO.File.WriteAllText(StampPath, DateTime.Now.Ticks.ToString()); } catch { }
        }

        public static bool TestFresh()
        {
            try
            {
                if (!System.IO.File.Exists(StampPath)) return false;
                return (DateTime.Now - System.IO.File.GetLastWriteTime(StampPath)).TotalSeconds < 10;
            }
            catch { return false; }
        }

        public static void EnsureFile()
        {
            try
            {
                if (File.Exists(FilePath)) return;
                System.IO.Directory.CreateDirectory(DirPath);
                string def = "# dsh-notify \u914d\u7f6e\uff1a\u6539\u5b8c\u4fdd\u5b58\uff0c\u4e0b\u6b21\u5f39\u51fa\u81ea\u52a8\u751f\u6548\r\n"
                    + "title=\u2714  \u8bf7\u6c42\u5df2\u5b8c\u6210\r\n"
                    + "sub=\u70b9\u8fd9\u91cc\u56de\u5230 DeepSeek Harness\r\n"
                    + "width=336\r\nheight=124\r\nanim_in=800\r\nanim_out=420\r\nhold_ms=30000\r\n"
                    + "pos=br\r\nmargin_x=0\r\nmargin_y=10\r\nimage=\r\nimage_size=56\r\n";
                File.WriteAllText(FilePath, def);
            }
            catch { }
        }

        public static void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (string raw in File.ReadAllLines(FilePath, System.Text.Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    int i = line.IndexOf('=');
                    if (i <= 0) continue;
                    string k = line.Substring(0, i).Trim().ToLower();
                    string v = line.Substring(i + 1).Trim();
                    int t; float f2;
                    if (k == "title") Title = v;
                    else if (k == "sub") Sub = v;
                    else if (k == "title_ask") TitleAsk = v;
                    else if (k == "sub_ask") SubAsk = v;
                    else if (k == "width" && int.TryParse(v, out t) && t > 120) W = t;
                    else if (k == "height" && int.TryParse(v, out t) && t > 60) H = t;
                    else if (k == "anim_in" && int.TryParse(v, out t)) AnimIn = t;
                    else if (k == "anim_out" && int.TryParse(v, out t)) AnimOut = t;
                    else if (k == "hold_ms" && int.TryParse(v, out t)) Hold = t;
                    else if (k == "pos") Pos = v.ToLower();
                    else if (k == "margin_x" && int.TryParse(v, out t)) MX = t;
                    else if (k == "margin_y" && int.TryParse(v, out t)) MY = t;
                    else if (k == "image") Img = v;
                    else if (k == "image_size" && int.TryParse(v, out t)) ImgSize = t;
                    else if (k == "font" && v.Length > 0) FontName = v;
                    else if (k == "title_size" && float.TryParse(v, out f2) && f2 > 4f) TitleSize = f2;
                    else if (k == "sub_size" && float.TryParse(v, out f2) && f2 > 4f) SubSize = f2;
                    else if (k == "align") Align = v.ToLower();
                    else if (k == "img_pos") ImgPos = v.ToLower();
                    else if (k == "exit_fx") ExitFx = v.ToLower();
                    else if (k == "fx_ms" && int.TryParse(v, out t)) FxMs = t;
                    else if (k == "text_x" && int.TryParse(v, out t)) TextX = t;
                    else if (k == "text_y" && int.TryParse(v, out t)) TextY = t;
                }
            }
            catch { }
        }
    }


    // 灰飞湮灭 / 飞升：基于 UpdateLayeredWindow 的逐像素粒子特效
    class Fx : Form
    {
        [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] struct SIZE { public int CX, CY; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct BLENDFUNC { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
            IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNC pblend, int dwFlags);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr hdc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr h);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);

        const int ULW_ALPHA = 2, AC_SRC_ALPHA = 1;

        class Tile
        {
            public Bitmap Bmp;
            public int X, Y;
            public float Delay, VX, VY;
        }

        Bitmap src;
        public Action Done;
        Tile[] tiles;
        int pad = 140;
        int total;
        float tx, ty;      // 目标原点（相对本窗口）
        System.Windows.Forms.Timer tm = new System.Windows.Forms.Timer();
        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();

        public Fx(Bitmap shot, int durationMs, bool rise)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            src = shot;
            total = Math.Max(250, durationMs);
            pad = (int)(140 * U.S);
            int cols = 30, rows = 13;
            int cw = Math.Max(1, shot.Width / cols);
            int ch = Math.Max(1, shot.Height / rows);
            var rnd = new Random(7);
            tiles = new Tile[cols * rows];
            int idx = 0;
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    int sx = x * cw, sy = y * ch;
                    int w = (x == cols - 1) ? shot.Width - sx : cw;
                    int h = (y == rows - 1) ? shot.Height - sy : ch;
                    if (w <= 0 || h <= 0) continue;
                    var t = new Tile();
                    t.Bmp = new Bitmap(w, h);
                    using (var g = Graphics.FromImage(t.Bmp))
                        g.DrawImage(shot, new Rectangle(0, 0, w, h), new Rectangle(sx, sy, w, h), GraphicsUnit.Pixel);
                    t.X = sx; t.Y = sy;

                    if (rise)
                    {
                        t.Delay = (float)y / rows * 0.45f;
                        t.VX = (float)(rnd.NextDouble() * 44 - 22) * U.S;
                        t.VY = -(float)(160 + rnd.NextDouble() * 220) * U.S;
                    }
                    else
                    {
                        float cx = x - (cols - 1) / 2f;
                        float cy = y - (rows - 1) / 2f;
                        float maxr = (float)Math.Sqrt(((cols - 1) / 2f) * ((cols - 1) / 2f) + ((rows - 1) / 2f) * ((rows - 1) / 2f));
                        t.Delay = (1f - (float)Math.Sqrt(cx * cx + cy * cy) / maxr) * 0.45f;
                        float ang = (float)(Math.Atan2(cy, cx) + rnd.NextDouble() * 0.8 - 0.4);
                        float spd = (float)(80 + rnd.NextDouble() * 150) * U.S;
                        t.VX = (float)Math.Cos(ang) * spd;
                        t.VY = (float)Math.Sin(ang) * spd - 20 * U.S;
                    }
                    tiles[idx++] = t;
                }
            }
            tm.Interval = 16;
            tm.Tick += delegate { Frame(); };
        }

        public void Play(Point loc, int w, int h)
        {
            Bounds = new Rectangle(loc.X - pad, loc.Y - pad, w + pad * 2, h + pad * 2);
            tx = pad; ty = pad;
            Show();
            sw.Start();
            tm.Start();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80000; // WS_EX_LAYERED
                return cp;
            }
        }

        void Frame()
        {
            int el = (int)sw.ElapsedMilliseconds;
            var bmp = new Bitmap(Bounds.Width, Bounds.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Low;
                foreach (var t in tiles)
                {
                    if (t == null) continue;
                    float local = (el - t.Delay * total) / (total * (1f - t.Delay));
                    if (local < 0f) local = 0f;
                    if (local >= 1f) continue;
                    float ease = local * local;
                    float sc = 1f - local * 0.75f;
                    float a = 1f - local;
                    int dx = (int)(t.X + tx + t.VX * ease);
                    int dy = (int)(t.Y + ty + t.VY * ease);
                    int w = (int)(t.Bmp.Width * sc);
                    int h = (int)(t.Bmp.Height * sc);
                    if (w <= 0 || h <= 0) continue;
                    var cm = new System.Drawing.Imaging.ColorMatrix();
                    cm.Matrix33 = a;
                    using (var ia = new System.Drawing.Imaging.ImageAttributes())
                    {
                        ia.SetColorMatrix(cm);
                        g.DrawImage(t.Bmp, new Rectangle(dx, dy, w, h), 0, 0, t.Bmp.Width, t.Bmp.Height, GraphicsUnit.Pixel, ia);
                    }
                }
            }
            Push(bmp);
            bmp.Dispose();
            if (el >= total)
            {
                tm.Stop();
                Close();
                if (Done != null) { try { Done(); } catch { } }
                if (Program.TestClosing) Application.Exit();
            }
        }

        void Push(Bitmap bmp)
        {
            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr hBmp = bmp.GetHbitmap(Color.FromArgb(0));
            IntPtr old = SelectObject(memDc, hBmp);
            var size = new SIZE { CX = bmp.Width, CY = bmp.Height };
            var ptDst = new POINT { X = Bounds.X, Y = Bounds.Y };
            var ptSrc = new POINT { X = 0, Y = 0 };
            var blend = new BLENDFUNC { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = AC_SRC_ALPHA };
            UpdateLayeredWindow(Handle, screenDc, ref ptDst, ref size, memDc, ref ptSrc, 0, ref blend, ULW_ALPHA);
            SelectObject(memDc, old);
            DeleteObject(hBmp);
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    class Toast : Form
    {
        Label title, sub;
        Bitmap pic;
        int imgX = -1, imgY = -1;
        public bool ForceTest;
        public bool AskMode;
        bool activateAfter;
        Panel close;
        System.Windows.Forms.Timer fade, anim;
        int animFrom, animTo, animT, animDur; bool animOut, animVert; int fgTick;
        int life = 0;

        public Toast()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.White;
            Size = new Size(U.D(Cfg.W), U.D(Cfg.H));
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            ClientSize = new Size(U.D(Cfg.W), U.D(Cfg.H));

            title = new Label();
            title.Text = "\u2714  请求已完成";
            title.Font = U.F(Cfg.FontName, Cfg.TitleSize, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(20, 20, 20);
            title.AutoSize = false;
            title.AutoEllipsis = true;
            title.Location = new Point(U.D(18), U.D(16));
            title.Size = new Size(U.D(336) - U.D(56), U.D(34));
            title.BackColor = Color.Transparent;

            sub = new Label();
            sub.Text = "点这里回到 DeepSeek Harness";
            sub.Font = U.F(Cfg.FontName, Cfg.SubSize, FontStyle.Regular);
            sub.ForeColor = Color.FromArgb(120, 120, 120);
            sub.AutoSize = true;
            sub.Location = new Point(U.D(20), U.D(86));
            sub.BackColor = Color.Transparent;

            close = new Panel();
            close.Size = new Size(U.D(26), U.D(26));
            close.Location = new Point(Width - U.D(44), U.D(12));
            close.BackColor = Color.Transparent;
            close.Cursor = Cursors.Hand;
            close.Paint += delegate(object s2, PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var p = new Pen(Color.FromArgb(90, 90, 90), U.D(1.8f)))
                {
                    g.DrawLine(p, U.D(8), U.D(8), U.D(18), U.D(18));
                    g.DrawLine(p, U.D(18), U.D(8), U.D(8), U.D(18));
                }
            };
            close.Click += delegate { HideToast(); };

            Click += delegate { ClickDismiss(); };
            title.Click += delegate { ClickDismiss(); };
            sub.Click += delegate { ClickDismiss(); };

            Controls.Add(title); Controls.Add(sub); Controls.Add(close);

            anim = new System.Windows.Forms.Timer();
            anim.Interval = 15;
            anim.Tick += delegate
            {
                animT += 15;
                float p2 = Math.Min(1f, (float)animT / animDur);
                float e = 1f - (float)Math.Pow(1f - p2, 3);
                int x = (int)(animFrom + (animTo - animFrom) * e);
                if (animVert) Location = new Point(Location.X, x); else Location = new Point(x, Location.Y);
                if (p2 >= 1f)
                {
                    anim.Stop();
                    if (animOut)
                    {
                        Hide();
                        if (activateAfter) { activateAfter = false; Program.ActivateDsh(); }
                        if (ForceTest) Application.Exit();
                    }
                }
            };

            fade = new System.Windows.Forms.Timer();
            fade.Interval = 30;
            fade.Tick += delegate
            {
                life += 30;
                if (life > Cfg.Hold) { HideToast(); return; }
                fgTick += 30;
                if (fgTick >= 500)
                {
                    fgTick = 0;
                    if (!animOut && !ForceTest && (Program.DshForeground() || Cfg.TestFresh())) HideToast();
                }
            };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var r = new Rectangle(0, 0, Width, Height);
            using (var b = new SolidBrush(Color.White)) g.FillRectangle(b, r);
            using (var p = new Pen(Color.FromArgb(222, 222, 222), 1f))
                g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            using (var b2 = new SolidBrush(Color.FromArgb(40, 40, 40)))
                g.FillRectangle(b2, Width - U.D(3), 0, U.D(3), Height);
            if (pic != null)
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                int dx = imgX >= 0 ? U.D(imgX) : U.D(18);
                int dy = imgY >= 0 ? U.D(imgY) : (Height - pic.Height) / 2;
                g.DrawImage(pic, dx, dy, pic.Width, pic.Height);
            }
        }

        static GraphicsPath Round(Rectangle r, int rad)
        {
            var p = new GraphicsPath();
            int d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public void ClickDismiss()
        {
            activateAfter = true;
            HideToast();
        }

        public void ShowToast()
        {
            if (!ForceTest && (Program.DshForeground() || Cfg.TestFresh())) return;
            var wa = Screen.PrimaryScreen.WorkingArea;
            ClientSize = new Size(U.D(Cfg.W), U.D(Cfg.H));
            Cfg.Load();
            ClientSize = new Size(U.D(Cfg.W), U.D(Cfg.H));
            title.Text = AskMode ? Cfg.TitleAsk : Cfg.Title;
            sub.Text = AskMode ? Cfg.SubAsk : Cfg.Sub;
            bool leftSide = Cfg.Pos.Contains("l");
            bool topSide = Cfg.Pos.Contains("t");
            int tx = leftSide ? wa.Left + U.D(Cfg.MX) : wa.Right - Width - U.D(Cfg.MX);
            int ty = topSide ? wa.Top + U.D(Cfg.MY) : wa.Bottom - Height - U.D(Cfg.MY);
            int hiddenX = leftSide ? wa.Left - Width - 8 : wa.Right + 4;

            if (pic != null) { pic.Dispose(); pic = null; }
            try
            {
                if (!string.IsNullOrEmpty(Cfg.Img) && System.IO.File.Exists(Cfg.Img))
                {
                    using (var raw = new Bitmap(Cfg.Img))
                    {
                        pic = new Bitmap(raw, new Size(U.D(Cfg.ImgSize), U.D(Cfg.ImgSize)));
                    }
                }
            }
            catch { pic = null; }

            title.Font = U.F(Cfg.FontName, Cfg.TitleSize, FontStyle.Bold);
            sub.Font = U.F(Cfg.FontName, Cfg.SubSize, FontStyle.Regular);

            int pad = U.D(14);
            int imgS = U.D(Cfg.ImgSize);
            int gap = U.D(10);
            string ip = Cfg.ImgPos;
            int textX = pad;
            int textW = Cfg.W - pad * 2;
            int titleY = U.D(12);
            int subY = Cfg.H - U.D(34);
            imgX = -1; imgY = -1;

            if (pic != null && ip == "left")
            {
                imgX = 14; imgY = (Cfg.H - Cfg.ImgSize) / 2;
                textX = pad + imgS + gap;
                textW = Cfg.W - textX - pad;
            }
            else if (pic != null && ip == "right")
            {
                imgX = Cfg.W - 14 - Cfg.ImgSize; imgY = (Cfg.H - Cfg.ImgSize) / 2;
                textW = Cfg.W - pad * 2 - imgS - gap;
            }
            else if (pic != null && ip == "top")
            {
                imgX = (Cfg.W - Cfg.ImgSize) / 2; imgY = 14;
                titleY = pad + Cfg.ImgSize + U.D(6);
                subY = titleY + U.D(28);
                textW = Cfg.W - pad * 2;
            }
            if (Cfg.TextX >= 0)
            {
                textX = Cfg.TextX;
                textW = Cfg.W - Cfg.TextX - 14;
            }
            if (Cfg.TextY >= 0)
            {
                titleY = Cfg.TextY;
                subY = titleY + U.D(30) + (int)(2 * U.S);
            }
            if (textW < 50) textW = 50;

            ContentAlignment ca = Cfg.Align == "center" ? ContentAlignment.MiddleCenter
                                 : (Cfg.Align == "right" ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft);
            title.TextAlign = ca;
            sub.TextAlign = ca;
            title.Location = new Point(textX, titleY);
            title.Size = new Size(textW, U.D(30));
            sub.Location = new Point(textX, subY);
            sub.Size = new Size(textW, U.D(24));

            animOut = false;
            if (!Visible)
            {
                Location = new Point(hiddenX, ty);
                Show();
                animFrom = hiddenX; animTo = tx; animT = 0; animDur = Cfg.AnimIn; anim.Start();
            }
            else
            {
                Location = new Point(tx, ty);
            }
            life = 0;
            fgTick = 0;
            TopMost = true;
            try
            {
                string lg = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dsh-notify.log");
                System.IO.File.AppendAllText(lg, string.Format(
                    "{0:HH:mm:ss} test={1} U={2} wa={3} wh={4}x{5} loc={6} cfg pos={7} mx={8} my={9}\r\n",
                    DateTime.Now, ForceTest, U.S, wa, Width, Height, tx + "," + ty, Cfg.Pos, Cfg.MX, Cfg.MY));
            }
            catch { }
            fade.Start();
        }

        void HideToast()
        {
            fade.Stop();
            if (!Visible) { Hide(); return; }
            var wa = Screen.PrimaryScreen.WorkingArea;
            int dLeft = Location.X - wa.Left;
            int dRight = wa.Right - (Location.X + Width);
            int dTop = Location.Y - wa.Top;
            int dBottom = wa.Bottom - (Location.Y + Height);
            // 角落时逐边比较，取最近的一边；距离相同则优先按 pos 指定的方向
            int near = dLeft; string side = "l";
            if (dRight < near) { near = dRight; side = "r"; }
            if (dTop < near) { near = dTop; side = "t"; }
            if (dBottom < near) { near = dBottom; side = "b"; }
            string ps = Cfg.Pos;
            if (dLeft == near && ps.Contains("l")) side = "l";
            if (dRight == near && ps.Contains("r")) side = "r";
            if (dTop == near && ps.Contains("t")) side = "t";
            if (dBottom == near && ps.Contains("b")) side = "b";

            try
            {
                string lg2 = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dsh-notify.log");
                System.IO.File.AppendAllText(lg2, string.Format(
                    "{0:HH:mm:ss} exit side={1} near={2} d=({3},{4},{5},{6}) pos={7}\r\n",
                    DateTime.Now, side, near, dLeft, dRight, dTop, dBottom, ps));
            }
            catch { }

            string fx = Cfg.ExitFx;
            bool slide;
            if (fx == "slide") slide = true;
            else if (fx == "dissolve" || fx == "rise") slide = false;
            else slide = near <= (int)(44 * U.S);

            if (slide)
            {
                animOut = true;
                animT = 0;
                animDur = Cfg.AnimOut;
                animVert = (side == "t" || side == "b");
                if (animVert)
                {
                    animFrom = Location.Y;
                    animTo = (side == "t") ? (wa.Top - Height - 8) : (wa.Bottom + 8);
                }
                else
                {
                    animFrom = Location.X;
                    animTo = (side == "l") ? (wa.Left - Width - 8) : (wa.Right + 8);
                }
                anim.Start();
                return;
            }

            animOut = true;
            try
            {
                var shot = new Bitmap(Width, Height);
                try { DrawToBitmap(shot, new Rectangle(0, 0, Width, Height)); }
                catch { shot.Dispose(); Hide(); return; }
                var loc = Location;
                Hide();
                var f = new Fx(shot, Cfg.FxMs, fx == "rise");
                f.Done = delegate
                {
                    if (activateAfter) { activateAfter = false; Program.ActivateDsh(); }
                };
                f.Play(loc, Width, Height);
            }
            catch { Hide(); if (ForceTest) Application.Exit(); }
        }
    }
}
