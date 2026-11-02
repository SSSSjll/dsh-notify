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

            bool testMode = false;
            foreach (string a in Environment.GetCommandLineArgs())
            {
                if (a == "--test") testMode = true;
            }
            if (testMode)
            {
                var t = new Toast();
                t.ForceTest = true;
                t.ShowToast();
                Application.Run(t);
                return;
            }

            using (var ctx = new Ctx())
            {
                ctx.Start();
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

        public void Start()
        {
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
        public static int W = 336, H = 124, AnimIn = 800, AnimOut = 420, Hold = 30000;
        public static int MX = 0, MY = 10, ImgSize = 56;
        public static string Pos = "br", Img = "";
        public static string DirPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dsh", "dsh-notify");
        public static string FilePath = System.IO.Path.Combine(DirPath, "config.ini");

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
                    int t;
                    if (k == "title") Title = v;
                    else if (k == "sub") Sub = v;
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
                }
            }
            catch { }
        }
    }

    class Toast : Form
    {
        Label title, sub;
        Bitmap pic;
        public bool ForceTest;
        Panel close;
        System.Windows.Forms.Timer fade, anim;
        int animFrom, animTo, animT, animDur; bool animOut; int fgTick;
        int life = 0;

        public Toast()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.White;
            Size = new Size(U.D(Cfg.W), U.D(Cfg.H));
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            ClientSize = new Size(U.D(Cfg.W), U.D(Cfg.H));

            title = new Label();
            title.Text = "\u2714  请求已完成";
            title.Font = new Font("Microsoft YaHei UI", U.D(11.5f), FontStyle.Bold);
            title.ForeColor = Color.FromArgb(20, 20, 20);
            title.AutoSize = false;
            title.AutoEllipsis = true;
            title.Location = new Point(U.D(18), U.D(16));
            title.Size = new Size(U.D(336) - U.D(56), U.D(34));
            title.BackColor = Color.Transparent;

            sub = new Label();
            sub.Text = "点这里回到 DeepSeek Harness";
            sub.Font = new Font("Microsoft YaHei UI", U.D(9f));
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

            Click += delegate { HideToast(); Program.ActivateDsh(); };
            title.Click += delegate { HideToast(); Program.ActivateDsh(); };
            sub.Click += delegate { HideToast(); Program.ActivateDsh(); };

            Controls.Add(title); Controls.Add(sub); Controls.Add(close);

            anim = new System.Windows.Forms.Timer();
            anim.Interval = 15;
            anim.Tick += delegate
            {
                animT += 15;
                float p2 = Math.Min(1f, (float)animT / animDur);
                float e = 1f - (float)Math.Pow(1f - p2, 3);
                int x = (int)(animFrom + (animTo - animFrom) * e);
                Location = new Point(x, Location.Y);
                if (p2 >= 1f)
                {
                    anim.Stop();
                    if (animOut) { Hide(); if (ForceTest) Application.Exit(); }
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
                    if (!animOut && !ForceTest && Program.DshForeground()) HideToast();
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
                g.DrawImage(pic, U.D(18), (Height - pic.Height) / 2, pic.Width, pic.Height);
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

        public void ShowToast()
        {
            if (!ForceTest && Program.DshForeground()) return;
            var wa = Screen.PrimaryScreen.WorkingArea;
            ClientSize = new Size(U.D(Cfg.W), U.D(Cfg.H));
            Cfg.Load();
            ClientSize = new Size(U.D(Cfg.W), U.D(Cfg.H));
            title.Text = Cfg.Title;
            sub.Text = Cfg.Sub;
            bool leftSide = Cfg.Pos.StartsWith("l");
            bool topSide = Cfg.Pos.EndsWith("t");
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

            int pad = U.D(18);
            int textX = pad;
            int textW = Cfg.W - 56;
            if (pic != null)
            {
                textX = pad + U.D(Cfg.ImgSize) + U.D(12);
                textW = Cfg.W - Cfg.ImgSize - 12 - 56;
                if (textW < 60) textW = 60;
            }
            title.Location = new Point(textX, U.D(16));
            title.Size = new Size(U.D(textW), U.D(34));
            sub.Location = new Point(textX, U.D(86));

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
            fade.Start();
        }

        void HideToast()
        {
            fade.Stop();
            if (!Visible) { Hide(); return; }
            animOut = true;
            animFrom = Location.X; animTo = Location.X + Width + 8; animT = 0; animDur = Cfg.AnimOut; anim.Start();
        }
    }
}
