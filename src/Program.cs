using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace DshNotify
{
    static class Program
    {
        public static bool TestClosing;
        public static bool DemoMode;
        public static string DemoFx = "dissolve";

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

            bool testMode = false, showOnce = false, noWatch = false, askMode = false, demoMode = false;
            string demoFx = "dissolve";
            string[] argv = Environment.GetCommandLineArgs();
            for (int ai = 0; ai < argv.Length; ai++)
            {
                string a = argv[ai];
                if (a == "--demo")
                {
                    demoMode = true;
                    if (ai + 1 < argv.Length && !argv[ai + 1].StartsWith("--")) demoFx = argv[ai + 1];
                }
            }
            foreach (string a in argv)
            {
                if (a == "--test") testMode = true;
                else if (a == "--show") showOnce = true;
                else if (a == "--no-watch") noWatch = true;
                else if (a == "--ask") askMode = true;
            }
            if (demoMode)
            {
                Cfg.MarkTest();
                DemoMode = true;
                DemoFx = demoFx;
                testMode = true;
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
            try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "dshnotify.log"), DateTime.Now.ToString("HH:mm:ss.fff") + " " + m + "\r\n"); } catch { }
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
        // 所有消失特效的唯一清单。以后新增特效只在这里加一项即可：
        // exit_fx=random 会自动把它算进随机池，每种特效概率相同。
        public static readonly string[] FxAll = { "slide", "dissolve", "rise", "collapse", "hand", "wave" };
        public static int Shatter = 1;      // 被动：拍地插件在跑就自动生效
        public static int ShardPct = 50;    // 停留到自动关闭时间的百分之多少开始掷骰子
        public static int ShardP0 = 10;     // 到点时的出现概率 %
        public static int ShardStep = 5;    // 每多等 1 秒，概率增加多少 %
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
                    + "pos=br\r\nmargin_x=0\r\nmargin_y=10\r\nimage=\r\nimage_size=56\r\n"
                    + "# \u6d88\u5931\u7279\u6548\uff1aslide / dissolve / rise / collapse / hand / wave\uff0c"
                    + "random=\u6bcf\u79cd\u7b49\u6982\u7387\u968f\u673a\u4e00\u79cd\uff08\u65b0\u7279\u6548\u81ea\u52a8\u8fdb\u6c60\uff09\u3002"
                    + "auto=\u8ddf\u636e\u79bb\u5c4f\u5e55\u8fb9\u7684\u8ddd\u79bb\u81ea\u52a8\u9009\r\n"
                    + "exit_fx=random\r\n"
                    + "# \u6d88\u5931\u52a8\u753b\u65f6\u957f\uff08\u6beb\u79d2\uff09\uff0c\u6240\u6709\u6d88\u5931\u7279\u6548\u90fd\u8ddf\u5b83\u8d70\r\n"
                    + "fx_ms=800\r\n"
                    + "# \u62cd\u5730\u63d2\u4ef6\u5728\u8dd1\u65f6\uff0c\u5f39\u7a97\u505c\u7559\u5230\u4e00\u5b9a\u65f6\u95f4\u540e\u4f1a\u88ab\u62cd\u788e\uff08\u4e0d\u8df3\u8f6c\uff09\r\n"
                    + "shatter=1\r\nshatter_pct=50\r\nshatter_p0=10\r\nshatter_step=5\r\n";
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
            else if (k == "shatter") Shatter = (v == "1" || v == "true") ? 1 : 0;
            else if (k == "shatter_pct") { int sv; if (int.TryParse(v, out sv)) ShardPct = Math.Max(0, Math.Min(95, sv)); }
            else if (k == "shatter_p0") { int sv; if (int.TryParse(v, out sv)) ShardP0 = Math.Max(0, Math.Min(100, sv)); }
            else if (k == "shatter_step") { int sv; if (int.TryParse(v, out sv)) ShardStep = Math.Max(0, Math.Min(100, sv)); }
                    else if (k == "fx_ms" && int.TryParse(v, out t)) FxMs = t;
                    else if (k == "text_x" && int.TryParse(v, out t)) TextX = t;
                    else if (k == "text_y" && int.TryParse(v, out t)) TextY = t;
                }
            }
            catch { }
        }
    }


    // 粒子特效：灰飞湮灭（冲击波+化灰）/ 飞升 / 倒塌
    class Fx : Form
    {
        [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] struct SIZE { public int CX, CY; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct BLENDFUNC { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
        [StructLayout(LayoutKind.Sequential)]
        struct BITMAPINFOHEADER
        {
            public int biSize, biWidth, biHeight;
            public short biPlanes, biBitCount;
            public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
            IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNC pblend, int dwFlags);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr hdc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr h);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")]
        static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER pbmi, uint usage, out IntPtr bits, IntPtr section, uint offset);

        const int ULW_ALPHA = 2, AC_SRC_ALPHA = 1;
        const int MODE_DISSOLVE = 0, MODE_RISE = 1, MODE_COLLAPSE = 2, MODE_HAND = 3;

        class Frag
        {
            public float Cx, Cy, Start, Vx, Vy, G;
            public int Col;
        }

        int mode;
        int[] srcPx;
        int srcW, srcH;
        int cols, rows, cellW, cellH;
        int[] tileSX, tileSY, tileW, tileH;
        int[] cellOf;
        float[] distN, turnT, rndv, dirx, diry, lenpx;
        bool[] gap;
        Frag[] frags;

        int pad, total;
        float tx, ty;

        // 天堂之门
        int gx, gy, gateR, gateReach, gdim;
        float gtx, gty;
        const int GSTEP = 4;
        const float GATE_EY = 0.34f;   // 椭圆压扁比（远处天空的透视）
        // 巨角恶魔手 精灵条
        int beamW = 96, beamH = 240;
        int[] beamPx;
        int handW, handH, handN = 26;
        int[] handPx;
        // 齑粉粒子
        int pcount;
        short[] pwX, pwY, pwVX, pwVY, pwDelay;
        int[] pwCol;
        int[] glDist;
        float[] glRay, glKernel;
        float[] pX, pY, pT, pD;
        System.Windows.Forms.Timer tm = new System.Windows.Forms.Timer();
        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        public Action Done;

        int cw, ch;
        int[] buf;
        IntPtr dib, memDc, screenDc, dibBits, oldObj;
        long frameSum; int frameCnt;

        public Fx(Bitmap shot, int durationMs, string kind, Point origin)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            mode = kind == "rise" ? MODE_RISE : (kind == "collapse" ? MODE_COLLAPSE : (kind == "hand" ? MODE_HAND : MODE_DISSOLVE));

            srcW = shot.Width; srcH = shot.Height;
            var bd = shot.LockBits(new Rectangle(0, 0, srcW, srcH), System.Drawing.Imaging.ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            srcPx = new int[srcW * srcH];
            System.Runtime.InteropServices.Marshal.Copy(bd.Scan0, srcPx, 0, srcPx.Length);
            shot.UnlockBits(bd);
            shot.Dispose();

            total = Math.Max(300, durationMs);
            pad = (int)(170 * U.S);

            int cell = Math.Max(2, (int)(3 * U.S));
            cols = Math.Max(4, srcW / cell);
            rows = Math.Max(4, srcH / cell);
            cellW = Math.Max(1, srcW / cols);
            cellH = Math.Max(1, srcH / rows);
            int n = cols * rows;
            tileSX = new int[n]; tileSY = new int[n]; tileW = new int[n]; tileH = new int[n];
            cellOf = new int[n]; distN = new float[n]; turnT = new float[n]; rndv = new float[n]; gap = new bool[n];
            dirx = new float[n]; diry = new float[n]; lenpx = new float[n];
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    int i = y * cols + x;
                    tileSX[i] = x * cellW; tileSY[i] = y * cellH;
                    tileW[i] = (x == cols - 1) ? srcW - tileSX[i] : cellW;
                    tileH[i] = (y == rows - 1) ? srcH - tileSY[i] : cellH;
                    if (tileW[i] <= 0) tileW[i] = 1;
                    if (tileH[i] <= 0) tileH[i] = 1;
                }
            }

            Ctx.Log("fx origin=" + origin.X + "," + origin.Y + " src=" + srcW + "x" + srcH);
            var rnd = new Random(Environment.TickCount);
            float ox = Math.Max(0, Math.Min(srcW - 1, origin.X));
            float oy = Math.Max(0, Math.Min(srcH - 1, origin.Y));
            float maxD = (float)Math.Sqrt(Math.Max(ox, srcW - ox) * Math.Max(ox, srcW - ox)
                                        + Math.Max(oy, srcH - oy) * Math.Max(oy, srcH - oy));
            for (int i = 0; i < n; i++)
            {
                float px = tileSX[i] + tileW[i] * 0.5f;
                float py = tileSY[i] + tileH[i] * 0.5f;
                float ddx0 = px - ox, ddy0 = py - oy;
                float dl = (float)Math.Sqrt(ddx0 * ddx0 + ddy0 * ddy0);
                distN[i] = dl / maxD;
                lenpx[i] = dl;
                dirx[i] = dl > 0.001f ? ddx0 / dl : 0f;
                diry[i] = dl > 0.001f ? ddy0 / dl : 0f;
                rndv[i] = (float)rnd.NextDouble();
            }

            if (mode == MODE_HAND) { BuildPowder(rnd); LoadHandStrip(); }
            if (mode == MODE_RISE) LoadBeam();
            if (mode == MODE_COLLAPSE) BuildCollapse(rnd);
            else if (mode == MODE_RISE) BuildRise();
            else BuildDissolve();

            tm.Interval = 15;
            tm.Tick += delegate { Frame(); };
        }

        void BuildDissolve()
        {
            // 黑手：从左右两条边同时向中间碎，速度一致
            bool twoSided = (mode == MODE_HAND);
            for (int i = 0; i < cellOf.Length; i++)
            {
                cellOf[i] = i;
                if (twoSided)
                {
                    // 从手心（被握住的位置）开始碎，向四周扩散
                    float cx0 = tileSX[i] + tileW[i] * 0.5f;
                    float cy0 = tileSY[i] + tileH[i] * 0.5f;
                    float dx0 = cx0 - srcW * 0.5f;
                    float dy0 = cy0 - srcH * 0.62f;
                    float d0 = (float)Math.Sqrt(dx0 * dx0 + dy0 * dy0);
                    float palmR = srcH * 0.55f;
                    float maxD = (float)Math.Sqrt(srcW * srcW * 0.25f + srcH * srcH * 0.25f) - palmR;
                    if (maxD < 1f) maxD = 1f;
                    distN[i] = Math.Max(0f, d0 - palmR) / maxD;
                    if (distN[i] > 1f) distN[i] = 1f;
                    float inv = d0 > 1f ? 1f / d0 : 0f;
                    dirx[i] = dx0 * inv * (0.75f + rndv[i] * 0.35f);
                    diry[i] = dy0 * inv * (0.75f + rndv[i] * 0.35f);
                }
                turnT[i] = Math.Min(0.88f, distN[i] * 0.80f + rndv[i] * 0.06f);
            }
        }

        void BuildRise()
        {
            for (int i = 0; i < cellOf.Length; i++)
            {
                float h = (float)tileSY[i] / srcH;
                cellOf[i] = i;
                turnT[i] = h * 0.45f + rndv[i] * 0.1f;
            }
        }

        float[] cutJit;      // 每列的横向锯齿
        int piecesCount;
        int[] cutYv;
        int[] cutBase;

        void BuildCollapse(Random rnd)
        {
            int n = cellOf.Length;
            piecesCount = 3 + rnd.Next(1, 3);              // 4~5 竖列
            cutBase = new int[piecesCount + 1];
            cutBase[0] = 0; cutBase[piecesCount] = cols;
            for (int k = 1; k < piecesCount; k++)
                cutBase[k] = Math.Max(2, Math.Min(cols - 2, (int)((float)cols * k / piecesCount + (rnd.NextDouble() - 0.5) * cols * 0.14)));
            cutYv = new int[piecesCount];
            for (int k = 0; k < piecesCount; k++)
                cutYv[k] = Math.Max(3, Math.Min(rows - 3, (int)(rows * (0.32 + rnd.NextDouble() * 0.36))));
            cutJit = new float[piecesCount + 1];
            for (int k = 0; k <= piecesCount; k++) cutJit[k] = (float)(rnd.NextDouble() * 6.28);

            frags = new Frag[piecesCount * 2];
            for (int k = 0; k < frags.Length; k++) { frags[k] = new Frag(); }

            for (int y = 0; y < rows; y++)
            {
                // 本行的锯齿裂纹位置
                var cutAt = new int[piecesCount + 1];
                cutAt[0] = -1; cutAt[piecesCount] = cols + 1;
                for (int k = 1; k < piecesCount; k++)
                    cutAt[k] = cutBase[k] + (int)(Math.Sin(y * 0.42 + cutJit[k]) * 2.2);

                for (int x = 0; x < cols; x++)
                {
                    int i = y * cols + x;
                    int col = 0;
                    for (int k = 1; k < piecesCount; k++) if (x >= cutAt[k]) col = k;
                    bool upper = y < cutYv[col] + (int)(Math.Sin(x * 0.35 + cutJit[col]) * 1.5);
                    int fi = col * 2 + (upper ? 0 : 1);
                    cellOf[i] = fi;
                    frags[fi].Cx += tileSX[i] + tileW[i] * 0.5f;
                    frags[fi].Cy += tileSY[i] + tileH[i] * 0.5f;

                    bool near = false;
                    for (int k = 1; k < piecesCount; k++)
                        if (Math.Abs(x - cutAt[k]) <= 1) near = true;
                    int cy = cutYv[col] + (int)(Math.Sin(x * 0.35 + cutJit[col]) * 1.5);
                    if (Math.Abs(y - cy) <= 1) near = true;
                    gap[i] = near;
                    if (near) cellOf[i] = fi;
                }
            }
            int[] cnt = new int[frags.Length];
            for (int i = 0; i < n; i++) cnt[cellOf[i]]++;
            for (int k = 0; k < frags.Length; k++)
            {
                var f = frags[k];
                if (cnt[k] == 0) { f.Start = 2f; continue; }
                f.Cx /= cnt[k]; f.Cy /= cnt[k];
                // 两边 + 上边先塌；靠中间、靠下的最后
                float side = Math.Abs(f.Cx - srcW * 0.5f) / (srcW * 0.5f);
                float top = 1f - f.Cy / srcH;
                float first = Math.Max(side, top);
                f.Start = 0.18f + (1f - first) * 0.32f + (float)rnd.NextDouble() * 0.04f;
                // 横向只轻微向内收（主体是往下压）
                float inward = (srcW * 0.5f - f.Cx) * 0.55f;
                f.Vx = inward * (0.7f + (float)rnd.NextDouble() * 0.5f) * U.S;
                f.Vy = (float)(40 + rnd.NextDouble() * 50) * U.S;
                f.G = (float)(700 + rnd.NextDouble() * 240) * U.S;
            }
            // 裂纹从点击点向外张开
            for (int i = 0; i < n; i++) turnT[i] = Math.Min(0.24f, distN[i] * 0.20f + rndv[i] * 0.05f);
        }

        public void Play(Point loc, int w, int h)
        {
            if (mode == MODE_RISE)
            {
                var vs = SystemInformation.VirtualScreen;
                int top = vs.Top;
                Bounds = new Rectangle(loc.X - pad, top, w + pad * 2, (loc.Y + h + pad) - top);
                tx = pad;
                ty = loc.Y - top;
                gateR = (int)(96 * U.S);
                gateReach = (int)(gateR * 2.7f);
                gx = (int)(w * 0.5f + pad);
                gy = (int)(gateR * GATE_EY) + 1;   // 椭圆顶弧正好贴住屏幕上沿
                gtx = gx - tx;
                gty = gy - ty;
                BuildGate();
                BuildParticles();
            }
            else
            {
                int pd = mode == MODE_HAND ? (int)(300 * U.S) : pad;
                Bounds = new Rectangle(loc.X - pd, loc.Y - pd, w + pd * 2, h + pd * 2);
                tx = pd; ty = pd;
            }
            cw = Bounds.Width; ch = Bounds.Height;
            buf = new int[cw * ch];
            var bi = new BITMAPINFOHEADER();
            bi.biSize = 40; bi.biWidth = cw; bi.biHeight = -ch;
            bi.biPlanes = 1; bi.biBitCount = 32; bi.biCompression = 0;
            screenDc = GetDC(IntPtr.Zero);
            memDc = CreateCompatibleDC(screenDc);
            dib = CreateDIBSection(memDc, ref bi, 0, out dibBits, IntPtr.Zero, 0);
            oldObj = SelectObject(memDc, dib);
            CreateHandle();
            Frame();
            Show();
            sw.Start();
            tm.Start();
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80000;
                cp.ExStyle |= 0x08000000;
                cp.ExStyle |= 0x20;
                return cp;
            }
        }

        void Frame() { try { FrameCore(); } catch { } }

        void BlitTile(int i, int dxb, int dyb, int dw, int dh, int alpha)
        {
            int sx0 = tileSX[i], sy0 = tileSY[i], tw = tileW[i], th = tileH[i];
            for (int yy = 0; yy < dh; yy++)
            {
                int dy = dyb + yy;
                if (dy < 0 || dy >= ch) continue;
                int sy = sy0 + (int)((long)yy * th / dh);
                if (sy < 0 || sy >= srcH) continue;
                int srow = sy * srcW + sx0;
                int drow = dy * cw;
                for (int xx = 0; xx < dw; xx++)
                {
                    int dx = dxb + xx;
                    if (dx < 0 || dx >= cw) continue;
                    int sp = srcPx[srow + (int)((long)xx * tw / dw)];
                    int sa = (sp >> 24) & 0xFF;
                    if (sa == 0) continue;
                    int a2 = alpha >= 255 ? sa : sa * alpha / 255;
                    if (a2 <= 2) continue;
                    int r = (((sp >> 16) & 0xFF) * a2) >> 8;
                    int g = (((sp >> 8) & 0xFF) * a2) >> 8;
                    int b = ((sp & 0xFF) * a2) >> 8;
                    buf[drow + dx] = (a2 << 24) | (r << 16) | (g << 8) | b;
                }
            }
        }

        // 在已绘制区域上叠一层白光（冲击波）
        void TintTile(int i, int dxb, int dyb, int dw, int dh, int alpha)
        {
            for (int yy = 0; yy < dh; yy++)
            {
                int dy = dyb + yy;
                if (dy < 0 || dy >= ch) continue;
                int drow = dy * cw;
                for (int xx = 0; xx < dw; xx++)
                {
                    int dx = dxb + xx;
                    if (dx < 0 || dx >= cw) continue;
                    int d = buf[drow + dx];
                    int da = (d >> 24) & 0xFF;
                    if (da == 0) continue;
                    int r = ((d >> 16) & 0xFF);
                    int g = ((d >> 8) & 0xFF);
                    int b = (d & 0xFF);
                    r += (132 - r) * alpha / 255;
                    g += (168 - g) * alpha / 255;
                    b += (208 - b) * alpha / 255;
                    buf[drow + dx] = (da << 24) | (r << 16) | (g << 8) | b;
                }
            }
        }

        void FrameCore()
        {
            long t0 = sw.ElapsedMilliseconds;
            int el = (int)t0;
            float t = (float)el / total;
            Array.Clear(buf, 0, buf.Length);

            if (el == 0)
            {
                int ox = (int)tx, oy = (int)ty;
                for (int y = 0; y < srcH; y++)
                {
                    int dy = y + oy;
                    if (dy < 0 || dy >= ch) continue;
                    Buffer.BlockCopy(srcPx, y * srcW * 4, buf, (dy * cw + ox) * 4, srcW * 4);
                }
            }
            else if (mode == MODE_HAND) DrawCrush(t, el);
            else if (mode == MODE_COLLAPSE) DrawCollapse(t, el);
            else if (mode == MODE_RISE) DrawRise(t, el);
            else DrawDissolve(t, el, true);

            System.Runtime.InteropServices.Marshal.Copy(buf, 0, dibBits, buf.Length);
            var size = new SIZE { CX = cw, CY = ch };
            var ptDst = new POINT { X = Bounds.X, Y = Bounds.Y };
            var ptSrc = new POINT { X = 0, Y = 0 };
            var blend = new BLENDFUNC { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = AC_SRC_ALPHA };
            UpdateLayeredWindow(Handle, screenDc, ref ptDst, ref size, memDc, ref ptSrc, 0, ref blend, ULW_ALPHA);

            frameCnt++;
            frameSum += sw.ElapsedMilliseconds - t0;
            if (el >= total)
            {
                tm.Stop();
                if (frameCnt > 0 && frameCnt < 3000) Ctx.Log("fx frames=" + frameCnt + " avg=" + (frameSum / frameCnt) + "ms tiles=" + cellOf.Length);
                SelectObject(memDc, oldObj);
                if (dib != IntPtr.Zero) { DeleteObject(dib); dib = IntPtr.Zero; }
                if (memDc != IntPtr.Zero) { DeleteDC(memDc); memDc = IntPtr.Zero; }
                if (screenDc != IntPtr.Zero) { ReleaseDC(IntPtr.Zero, screenDc); screenDc = IntPtr.Zero; }
                Close();
                if (Done != null) { try { Done(); } catch { } }
                if (Program.TestClosing) Application.Exit();
            }
        }

        // 灰飞湮灭：冲击波把像素往外推（折射），随后从点击点向外化灰
        void DrawDissolve(float t, int el, bool push)
        {
            float wave = push ? (float)Math.Pow(t, 0.32) * 1.80f : (float)Math.Pow(t, 0.30) * 1.90f;  // 黑手：波前更快更夸张
            float wd = 0.070f + 0.060f * t;                    // 波前厚度
            float strength = push ? 64f * U.S * (1f - t * 0.3f) : 0f;   // 外推强度（黑手版不要冲击波）

            // 第一遍：未受波及 / 已化灰
            for (int i = 0; i < cellOf.Length; i++)
            {
                int dx = tileSX[i] + (int)tx;
                int dy = tileSY[i] + (int)ty;
                float delta = distN[i] - wave;
                bool ring = t < turnT[i] && Math.Abs(delta) < wd;
                if (ring) continue;

                if (t < turnT[i]) { BlitTile(i, dx, dy, tileW[i], tileH[i], 255); continue; }

                float local = (t - turnT[i]) / (1f - turnT[i]);
                if (local >= 1f) continue;
                float jitter = (rndv[i] - 0.5f) * 1.3f;
                float cj = (float)Math.Cos(jitter), sj = (float)Math.Sin(jitter);
                float nx = dirx[i] * cj - diry[i] * sj;
                float ny = dirx[i] * sj + diry[i] * cj;
                float spd = (95f + rndv[i] * 170f) * (1.15f - distN[i] * 0.35f) * U.S * (push ? 1.70f : 1.55f);
                float fly = 1f - (1f - local) * (1f - local);
                float gr = local * local * (push ? 1f : 1.5f);
                int ddx = (int)(dx + nx * spd * fly);
                int ddy = (int)(dy + ny * spd * fly + 210f * U.S * gr);
                float sc = 1f - local * 0.85f;
                int dw = (int)(tileW[i] * sc), dh = (int)(tileH[i] * sc);
                if (dw <= 0 || dh <= 0) continue;
                int aa = (int)(255 * Math.Pow(1f - local, 1.25f));
                if (aa <= 2) continue;
                BlitTile(i, ddx, ddy, dw, dh, aa);
            }

            // 第二遍：波前——向外推开 + 放大 + 冷灰描边
            for (int i = 0; i < cellOf.Length; i++)
            {
                float delta = distN[i] - wave;
                if (Math.Abs(delta) >= wd) continue;
                if (t >= turnT[i]) continue;
                float k = 1f - Math.Abs(delta) / wd;           // 0..1
                float kk = k * k * k;
                float pushAmt = strength * kk;
                int dx = tileSX[i] + (int)tx + (int)(dirx[i] * pushAmt);
                int dy = tileSY[i] + (int)ty + (int)(diry[i] * pushAmt);
                float grow = 1f + 1.1f * kk;
                int dw = Math.Max(1, (int)(tileW[i] * grow));
                int dh = Math.Max(1, (int)(tileH[i] * grow));
                BlitTile(i, dx, dy, dw, dh, 255);
                TintTile(i, dx, dy, dw, dh, (int)(215 * k * (1f - t * 0.45f)));
            }
        }

        void BuildGate()
        {
            gdim = 2 * gateReach / GSTEP + 1;
            glDist = new int[gdim * gdim];
            glRay = new float[gdim * gdim];
            for (int ly = 0; ly < gdim; ly++)
            {
                for (int lx = 0; lx < gdim; lx++)
                {
                    float dx = lx * GSTEP - gateReach;
                    float dy = (ly * GSTEP - gateReach) / GATE_EY;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy);
                    int idx = ly * gdim + lx;
                    glDist[idx] = (int)d;
                    double ang = Math.Atan2(dy, dx);
                    double ray = Math.Pow(0.5 + 0.5 * Math.Cos(ang * 13.0), 9.0);
                    glRay[idx] = (float)ray;
                }
            }
            glKernel = new float[gateReach + 2];
            float sig = gateR * 0.16f;
            for (int k = 0; k < glKernel.Length; k++)
            {
                float v = k / sig;
                glKernel[k] = (float)Math.Exp(-v * v);
            }
        }

        void AddGlow(int x, int y, int a8, int cr, int cg, int cb)
        {
            if (x < 0 || x >= cw || y < 0 || y >= ch || a8 <= 1) return;
            int idx = y * cw + x;
            int d = buf[idx];
            int da = (d >> 24) & 0xFF;
            int na = da + a8;
            if (na > 255) na = 255;
            int r = ((d >> 16) & 0xFF) + (cr * a8 >> 8);
            int g = ((d >> 8) & 0xFF) + (cg * a8 >> 8);
            int b = (d & 0xFF) + (cb * a8 >> 8);
            if (r > 255) r = 255;
            if (g > 255) g = 255;
            if (b > 255) b = 255;
            buf[idx] = (na << 24) | (r << 16) | (g << 8) | b;
        }

        void DrawGate(float alpha, float radius)
        {
            if (alpha <= 0.01f || glDist == null) return;
            int R = (int)radius;
            for (int ly = 0; ly < gdim; ly++)
            {
                int py = gy - gateReach + ly * GSTEP;
                for (int lx = 0; lx < gdim; lx++)
                {
                    int idx = ly * gdim + lx;
                    int d = glDist[idx];
                    int k = d - R; if (k < 0) k = -k;
                    if (k >= glKernel.Length) continue;
                    float ring = glKernel[k];
                    float edge = 1f - (float)d / gateReach;
                    if (edge <= 0f) continue;
                    float rays = glRay[idx] * edge * edge * 0.85f;
                    float I = (ring + rays) * alpha;
                    if (I <= 0.03f) continue;
                    int a8 = (int)(I * 255f);
                    if (a8 > 255) a8 = 255;
                    int bx = gx - gateReach + lx * GSTEP;
                    for (int yy = 0; yy < GSTEP; yy++)
                        for (int xx = 0; xx < GSTEP; xx++)
                            AddGlow(bx + xx, py + yy, a8, 255, 236, 196);
                }
            }
        }

        void BuildParticles()
        {
            var rnd = new Random(Environment.TickCount * 3 + 17);
            pX = new float[150]; pY = new float[150]; pT = new float[150]; pD = new float[150];
            for (int i = 0; i < pX.Length; i++)
            {
                // 从弹窗四周（含框外一圈）出发
                double ang = rnd.NextDouble() * Math.PI * 2;
                float rad = 0.55f + (float)rnd.NextDouble() * 0.75f;
                pX[i] = srcW * 0.5f + (float)Math.Cos(ang) * srcW * 0.5f * rad;
                pY[i] = srcH * 0.5f + (float)Math.Sin(ang) * srcH * 0.5f * rad;
                pT[i] = 0.16f + (float)rnd.NextDouble() * 0.42f;
                pD[i] = 0.34f + (float)rnd.NextDouble() * 0.22f;
            }
        }

        void DrawParticles(float t)
        {
            if (pX == null) return;
            for (int i = 0; i < pX.Length; i++)
            {
                float local = (t - pT[i]) / pD[i];
                if (local <= 0f || local >= 1f) continue;
                float e = local * local * (1.5f - 0.5f * local);
                float dx = pX[i] + (gtx - pX[i]) * e;
                float dy = pY[i] + (gty - pY[i]) * e;
                // 侧向弧线，看起来像被卷进去
                float sway = (float)Math.Sin(local * 3.4f + i) * 9f * U.S * (1f - local);
                dx += sway;
                int a = (int)(255 * Math.Min(1f, local * 6f) * (1f - local * local * local));
                if (a <= 3) continue;
                int px = (int)(dx + tx), py = (int)(dy + ty);
                for (int oy = -1; oy <= 1; oy++)
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        int w2 = (ox == 0 && oy == 0) ? a : a / 2;
                        AddGlow(px + ox, py + oy, w2, 255, 214, 130);
                    }
            }
        }

        void DrawRise(float t, int el)
        {
            float open = Math.Min(1f, t / 0.09f);                  // 光柱展开
            float close = 1f;
            if (t > 0.80f) close = 1f - (t - 0.80f) / 0.20f;
            if (close < 0f) close = 0f;
            float gAlpha = Math.Min(open, close);
            float beamWid = (0.30f + 0.70f * open) * (0.25f + 0.75f * close);

            float cxW = tx + srcW * 0.5f;
            float beamHalf = 62f * U.S * beamWid;
            float beamBot = ty + srcH * 0.62f;

            // 1) 光柱先画（在弹窗后面）
            DrawBeamColumn(cxW, 0f, beamBot, beamHalf, gAlpha * 0.52f, 1.05f);          // 外圈柔光
            DrawBeamColumn(cxW, 0f, beamBot, beamHalf * 0.36f, gAlpha * 0.95f, 2.30f);   // 亮芯，拉开对比

            // 2) 弹窗：压扁 → 纵向拉长成细条 → 收进光柱
            float sx, sy, lift, alpha;
            if (t < 0.16f)
            {
                float k = t / 0.16f;
                sx = 1f + 0.16f * k;
                sy = 1f - 0.44f * k;
                lift = 8f * U.S * k;
                alpha = 1f;
            }
            else if (t < 0.50f)
            {
                float k = (t - 0.16f) / 0.34f;
                float e = 1f - (float)Math.Pow(1f - k, 3);
                sx = 1.16f - 1.06f * e;
                sy = 0.56f + 6.9f * e;
                lift = 8f * U.S + (srcH * 0.92f) * e;
                alpha = 1f - 0.45f * e;
            }
            else if (t < 0.72f)
            {
                float k = (t - 0.50f) / 0.22f;
                sx = 0.10f - 0.07f * k;
                sy = 7.46f + 1.4f * k;
                lift = 8f * U.S + srcH * 0.92f + srcH * 0.55f * k;
                alpha = 0.55f - 0.55f * k;
            }
            else { return; }

            float baseY = ty + srcH - lift;
            for (int i = 0; i < cellOf.Length; i++)
            {
                float rx = (tileSX[i] + tileW[i] * 0.5f - srcW * 0.5f) * sx;
                float ry = (srcH - (tileSY[i] + tileH[i] * 0.5f)) * sy;
                int dw = (int)(tileW[i] * sx), dh = (int)(tileH[i] * sy);
                if (dw < 1) dw = 1;
                if (dh < 1) dh = 1;
                if (dw > 900) dw = 900;
                int dx = (int)(cxW + rx - dw * 0.5f);
                int dy = (int)(baseY - ry - dh * 0.5f);
                int aa = (int)(255 * alpha);
                if (aa <= 2) continue;
                BlitTile(i, dx, dy, dw, dh, aa);
            }

            // 3) 被吸上去时溅起的光尘
            DrawParticles(t);
        }

        void BuildPowder(Random rnd)
        {
            int step = 2;
            int cap = 26000;
            pwX = new short[cap]; pwY = new short[cap]; pwCol = new int[cap];
            pwVX = new short[cap]; pwVY = new short[cap]; pwDelay = new short[cap];
            pcount = 0;
            for (int y = 0; y < srcH; y += step)
            {
                for (int x = 0; x < srcW; x += step)
                {
                    int c = srcPx[y * srcW + x];
                    if (((c >> 24) & 0xFF) < 40) continue;
                    if (pcount >= cap) break;
                    float dx = x - srcW * 0.5f, dy = y - srcH * 0.5f;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy) / (srcW * 0.5f);
                    float ang = (float)(rnd.NextDouble() * Math.PI * 2);
                    float spd = (float)(38 + rnd.NextDouble() * 140) * U.S;
                    pwX[pcount] = (short)x;
                    pwY[pcount] = (short)y;
                    pwCol[pcount] = c;
                    pwVX[pcount] = (short)(Math.Cos(ang) * spd);
                    pwVY[pcount] = (short)(Math.Sin(ang) * spd - 26 * U.S);
                    pwDelay[pcount] = (short)(d * 70 + rnd.NextDouble() * 40);
                    pcount++;
                }
            }
            Ctx.Log("powder particles=" + pcount);
        }

        void DrawPowder(int el, float t0, float span, float cxW, float cyW, float scx, float scy)
        {
            if (pwX == null) return;
            for (int i = 0; i < pcount; i++)
            {
                float local = (el - (t0 + pwDelay[i])) / span;
                if (local <= 0f || local >= 1f) continue;
                float sec = local * span * 0.001f;
                float px = cxW + (pwX[i] - srcW * 0.5f) * scx * (1f - local * 0.15f) + pwVX[i] * sec;
                float py = cyW + (pwY[i] - srcH * 0.5f) * scy * (1f - local * 0.15f) + pwVY[i] * sec + 90f * U.S * sec * sec;
                int a8 = (int)(245 * (1f - local) * (1f - local));
                if (a8 <= 5) continue;
                int col = pwCol[i];
                AddPix((int)px, (int)py, a8, (col >> 16) & 0xFF, (col >> 8) & 0xFF, col & 0xFF);
            }
        }

        void AddPix(int x, int y, int a8, int cr, int cg, int cb)
        {
            if (x < 0 || x >= cw || y < 0 || y >= ch || a8 <= 2) return;
            int idx = y * cw + x;
            int d = buf[idx];
            if (a8 > ((d >> 24) & 0xFF))
                buf[idx] = (a8 << 24) | (((cr * a8) >> 8) << 16) | (((cg * a8) >> 8) << 8) | ((cb * a8) >> 8);
        }

        void AddGlowSolid(int x, int y, int a8, int cr, int cg, int cb)
        {
            if (x < 0 || x >= cw || y < 0 || y >= ch) return;
            int idx = y * cw + x;
            int da = (buf[idx] >> 24) & 0xFF;
            if (a8 > da)
                buf[idx] = (a8 << 24) | (((cr * a8) >> 8) << 16) | (((cg * a8) >> 8) << 8) | ((cb * a8) >> 8);
        }

        // 巨角恶魔手：手掌 + 五指（两节，可蜷曲）
        void HandShape(float px, float py, float curl, bool fingersOnly)
        {
            float prx = srcW * 0.30f, pry = srcH * 0.46f;
            float seg1 = srcH * 0.50f, seg2 = srcH * 0.44f;
            float th0 = srcH * 0.115f, th1 = srcH * 0.095f;

            int nf = fingersOnly ? 5 : 0;
            float[] j0x = new float[5], j0y = new float[5];
            float[] j1x = new float[5], j1y = new float[5];
            float[] j2x = new float[5], j2y = new float[5];
            float[] thA = new float[5], thB = new float[5];
            if (nf > 0)
            {
                for (int f = 0; f < 4; f++)
                {
                    float spread = (f - 1.5f) * 0.22f * (1f - curl * 0.85f);
                    float a1 = spread + curl * 0.55f;
                    float a2 = a1 + curl * 1.15f;
                    j0x[f] = px + (f - 1.5f) * prx * 0.52f;
                    j0y[f] = py - pry * 0.72f;
                    j1x[f] = j0x[f] + (float)Math.Sin(a1) * seg1;
                    j1y[f] = j0y[f] - (float)Math.Cos(a1) * seg1;
                    j2x[f] = j1x[f] + (float)Math.Sin(a2) * seg2;
                    j2y[f] = j1y[f] - (float)Math.Cos(a2) * seg2;
                    thA[f] = th0 * (1f - f * 0.05f); thB[f] = th1 * (1f - f * 0.05f);
                }
                // 拇指
                float ta1 = -1.15f + curl * 0.85f, ta2 = ta1 - curl * 1.05f;
                j0x[4] = px - prx * 0.92f; j0y[4] = py + pry * 0.05f;
                j1x[4] = j0x[4] + (float)Math.Sin(ta1) * seg1 * 0.78f;
                j1y[4] = j0y[4] - (float)Math.Cos(ta1) * seg1 * 0.78f;
                j2x[4] = j1x[4] + (float)Math.Sin(ta2) * seg2 * 0.72f;
                j2y[4] = j1y[4] - (float)Math.Cos(ta2) * seg2 * 0.72f;
                thA[4] = th0 * 1.15f; thB[4] = th1 * 1.1f;
            }

            float ex = px + prx * 1.7f, ey0 = py - pry * 1.05f, ey1 = py + pry * 1.05f;
            if (fingersOnly) { ey0 = py - pry * 2.2f; }
            int x0 = (int)(px - prx * 1.75f), x1 = (int)ex;
            int y0 = (int)ey0, y1 = (int)ey1;
            if (x0 < 0) x0 = 0; if (y0 < 0) y0 = 0;
            if (x1 > cw) x1 = cw; if (y1 > ch) y1 = ch;

            int step = 4;
            for (int yy = y0; yy < y1; yy += step)
            {
                for (int xx = x0; xx < x1; xx += step)
                {
                    float bxx = xx + step * 0.5f - px;
                    float byy = yy + step * 0.5f - py;
                    float best = 0f, rim = 1f;
                    if (!fingersOnly)
                    {
                        float inP = 1f - (bxx * bxx) / (prx * prx) - (byy * byy) / (pry * pry);
                        if (inP > 0f) { best = 1f; rim = 1f - inP * 3.2f; if (rim < 0f) rim = 0f; }
                    }
                    for (int f = 0; f < nf; f++)
                    {
                        for (int segn = 0; segn < 2; segn++)
                        {
                            float ax = segn == 0 ? j0x[f] - px : j1x[f] - px;
                            float ay = segn == 0 ? j0y[f] - py : j1y[f] - py;
                            float bx2 = segn == 0 ? j1x[f] - px : j2x[f] - px;
                            float by2 = segn == 0 ? j1y[f] - py : j2y[f] - py;
                            float dx = bx2 - ax, dy = by2 - ay;
                            float len2 = dx * dx + dy * dy;
                            if (len2 < 1f) continue;
                            float tt = ((bxx - ax) * dx + (byy - ay) * dy) / len2;
                            if (tt < 0f) tt = 0f; else if (tt > 1f) tt = 1f;
                            float qx = bxx - (ax + dx * tt), qy = byy - (ay + dy * tt);
                            float d2 = qx * qx + qy * qy;
                            float thk = segn == 0 ? thA[f] : thB[f];
                            float thk2 = thk * thk;
                            if (d2 < thk2)
                            {
                                float d = (float)Math.Sqrt(d2) / thk;
                                float v = 1f - d * 0.75f;
                                if (v > best) { best = v; rim = d; }
                            }
                        }
                    }
                    if (best <= 0f) continue;
                    float sh = 1f - rim * 0.75f;
                    int r = (int)(30 + 120 * (1f - rim) * (1f - rim) + 40 * sh);
                    int g = (int)(8 + 28 * (1f - rim) * (1f - rim));
                    int b = (int)(10 + 26 * (1f - rim) * (1f - rim));
                    if (r > 255) r = 255; if (g > 255) g = 255; if (b > 255) b = 255;
                    for (int oy = 0; oy < step; oy++)
                        for (int ox = 0; ox < step; ox++)
                            AddGlowSolid(xx + ox, yy + oy, 255, r, g, b);
                }
            }
        }

        void LoadBeam()
        {
            try
            {
                string how;
                Bitmap bmp = LoadAssetBitmap("beam.png", "dsh-notify.beam.png", out how);
                if (bmp == null) { Ctx.Log("beam none"); return; }
                using (bmp)
                {
                    beamW = bmp.Width; beamH = bmp.Height;
                    var bd = bmp.LockBits(new Rectangle(0, 0, beamW, beamH), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    beamPx = new int[beamW * beamH];
                    Marshal.Copy(bd.Scan0, beamPx, 0, beamPx.Length);
                    bmp.UnlockBits(bd);
                }
                Ctx.Log("beam " + how);
            }
            catch (Exception ex) { Ctx.Log("beam load fail " + ex.Message); }
        }

        void AddLight(int x, int y, int a8, int r, int g, int b)
        {
            if (x < 0 || x >= cw || y < 0 || y >= ch || a8 <= 0) return;
            int i2 = y * cw + x;
            int d = buf[i2];
            int da = (d >> 24) & 0xFF, dr = (d >> 16) & 0xFF, dg = (d >> 8) & 0xFF, db = d & 0xFF;
            int ia = 255 - a8;
            int oa = a8 + da * ia / 255;
            int orr = r * a8 / 255 + dr * ia / 255;
            int og = g * a8 / 255 + dg * ia / 255;
            int ob = b * a8 / 255 + db * ia / 255;
            buf[i2] = (oa << 24) | (orr << 16) | (og << 8) | ob;
        }

        // 天堂光柱：从窗口顶端垂下来的一束光
        void DrawBeamColumn(float cx, float y0, float y1, float halfW, float alpha, float gain)
        {
            if (beamPx == null || halfW < 1f || alpha <= 0.01f) return;
            int ya = (int)y0, yb = (int)y1;
            float span = Math.Max(1f, y1 - y0);
            for (int y = ya; y < yb; y++)
            {
                float vv = (y - y0) / span;
                float tail = vv > 0.78f ? (1f - (vv - 0.78f) / 0.22f) : 1f;   // 底部收光，避免硬切
                int sy = (int)(vv * (beamH - 1));
                if (sy < 0) sy = 0; if (sy >= beamH) sy = beamH - 1;
                int row = sy * beamW;
                for (int x = (int)(cx - halfW); x <= (int)(cx + halfW); x += 2)
                {
                    float u = (x - (cx - halfW)) / (halfW * 2f);
                    if (u < 0f || u >= 1f) continue;
                    int c = beamPx[row + (int)(u * (beamW - 1))];
                    int a = (c >> 24) & 0xFF;
                    if (a < 2) continue;
                    float k = a / 255f * alpha * gain * tail;
                    int a8 = (int)(k * 255f);
                    if (a8 > 255) a8 = 255;
                    AddLight(x, y, a8, (c >> 16) & 0xFF, (c >> 8) & 0xFF, c & 0xFF);
                    AddLight(x + 1, y, a8, (c >> 16) & 0xFF, (c >> 8) & 0xFF, c & 0xFF);
                }
            }
        }

        // 先找本机同名素材文件，找不到再用内嵌资源
        static string FindLocalAsset(string name)
        {
            string[] dirs = new string[]
            {
                AppDomain.CurrentDomain.BaseDirectory,
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "dsh-notify", "assets"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "dsh-notify", "assets"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dsh", "dsh-notify", "assets")
            };
            for (int i = 0; i < dirs.Length; i++)
            {
                try
                {
                    string cand = Path.Combine(Path.GetFullPath(dirs[i]), name);
                    if (File.Exists(cand)) return cand;
                }
                catch { }
            }
            return null;
        }

        static Bitmap LoadAssetBitmap(string name, string resName, out string how)
        {
            how = "none";
            string local = FindLocalAsset(name);
            if (local != null)
            {
                try { Bitmap b = new Bitmap(local); how = "local"; return b; }
                catch { }
            }
            try
            {
                Stream st = Assembly.GetExecutingAssembly().GetManifestResourceStream(resName);
                if (st != null) { how = "embedded"; return new Bitmap(st); }
            }
            catch { }
            return null;
        }

        void LoadHandStrip()
        {
            try
            {
                string how;
                Bitmap bmp = LoadAssetBitmap("hand_strip.png", "dsh-notify.hand_strip.png", out how);
                if (bmp == null) { Ctx.Log("hand strip none"); return; }
                using (bmp)
                {
                    handW = bmp.Width / handN;
                    handH = bmp.Height;
                    var bd = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    handPx = new int[bmp.Width * bmp.Height];
                    Marshal.Copy(bd.Scan0, handPx, 0, handPx.Length);
                    bmp.UnlockBits(bd);
                }
                Ctx.Log("hand strip " + how);
            }
            catch (Exception ex) { Ctx.Log("hand load fail " + ex.Message); }
        }

        void BlendPremul(int x, int y, int a, int r, int g, int b)
        {
            if (x < 0 || x >= cw || y < 0 || y >= ch || a <= 0) return;
            int idx = y * cw + x;
            int d = buf[idx];
            int da = (d >> 24) & 0xFF, dr = (d >> 16) & 0xFF, dg = (d >> 8) & 0xFF, db = d & 0xFF;
            int ia = 255 - a;
            int oa = a + da * ia / 255;
            int orr = r * a / 255 + dr * ia / 255;
            int og = g * a / 255 + dg * ia / 255;
            int ob = b * a / 255 + db * ia / 255;
            buf[idx] = (oa << 24) | (orr << 16) | (og << 8) | ob;
        }

        void DrawHandFrame(int idx, float cx, float topY, float scale)
        {
            if (handPx == null) return;
            if (idx < 0) idx = 0; if (idx >= handN) idx = handN - 1;
            int srcX = idx * handW;
            int dw = (int)(handW * scale), dh = (int)(handH * scale);
            int ox = (int)(cx - dw * 0.5f), oy = (int)topY;
            for (int y = 0; y < dh; y++)
            {
                int sy = (int)(y / scale); if (sy >= handH) break;
                int row = sy * (handW * handN) + srcX;
                for (int x = 0; x < dw; x++)
                {
                    int sx2 = (int)(x / scale); if (sx2 >= handW) break;
                    int c = handPx[row + sx2];
                    int a = (c >> 24) & 0xFF;
                    if (a < 8) continue;
                    BlendPremul(ox + x, oy + y, a, (c >> 16) & 0xFF, (c >> 8) & 0xFF, c & 0xFF);
                }
            }
        }

        void DrawCrush(float t, int el)
        {
            const float FM = 38f;      // 手放慢：38ms/帧（约 26fps）
            int idx = (int)(el / FM);
            if (idx < 0) idx = 0;
            if (idx > handN - 1) idx = handN - 1;

            // 攥拳的那一帧开始碎：直接用灰飞烟灭的碎法，但不带冲击波
            const float BREAK_MS = 418f;     // 第 11 帧（随手的帧率走）
            const float BREAK_LEN = 570f;      // 破碎与收手同一时刻完成
            float dm = el - BREAK_MS;
            if (dm <= 0f)
            {
                float grab = Math.Max(0f, Math.Min(1f, (idx - 6) / 4f));
                float jolt = -8f * U.S * grab;
                for (int i = 0; i < cellOf.Length; i++)
                {
                    if (cellOf[i] < 0) continue;
                    BlitTile(i, tileSX[i] + (int)tx, tileSY[i] + (int)ty + (int)jolt, tileW[i], tileH[i], 255);
                }
            }
            else
            {
                float dt = Math.Min(1f, dm / BREAK_LEN);
                DrawDissolve(dt, el, false);
            }

            // 真素材：巨角恶魔手
            float scale = 4.0f * U.S;
            float cx = tx + srcW * 0.5f;
            float topY = ty + srcH * 0.62f - 0.72f * handH * scale;
            DrawHandFrame(idx, cx, topY, scale);
        }

        // 倒塌：先裂开颤动，再整体向下垮塌，坠落过程掉灰
        void DrawCollapse(float t, int el)
        {
            float shake = 0f;
            float trem = t < 0.32f ? t / 0.32f : 0f;
            if (trem > 0f) shake = (float)(Math.Sin(el * 0.13) * 4.5 * trem + Math.Sin(el * 0.31) * 1.5 * trem);
            for (int i = 0; i < cellOf.Length; i++)
            {
                var f = frags[cellOf[i]];
                float fdx = 0f, fdy = 0f;
                if (trem > 0f && f.Start < 1f)
                {
                    float rx = f.Cx - srcW * 0.5f, ry = f.Cy - srcH * 0.5f;
                    float rl = (float)Math.Sqrt(rx * rx + ry * ry) + 1f;
                    fdx = rx / rl * 2.2f * trem;
                    fdy = ry / rl * 2.2f * trem;
                }
                int dx = tileSX[i] + (int)tx + (int)(shake + fdx);
                int dy = tileSY[i] + (int)ty + (int)(shake * 0.4f + fdy);
                if (t < turnT[i]) { BlitTile(i, dx, dy, tileW[i], tileH[i], 255); continue; }
                if (gap[i]) continue;
                float local = (t - f.Start) / (1f - f.Start);
                if (local <= 0f)
                {
                    BlitTile(i, dx, dy, tileW[i], tileH[i], 255);
                    continue;
                }
                if (local >= 1f) continue;
                float gr = local * local;
                bool dust = rndv[i] < 0.10f;
                int ddx, ddy, dw, dh, aa;
                if (dust)
                {
                    float vx2 = (rndv[i] - 0.5f) * 120f * U.S;
                    int off = (int)(local * 160f * U.S);
                    ddx = dx + (int)(vx2 * local) + (i % 7) - 3;
                    ddy = dy + off;
                    dw = Math.Max(1, (int)(tileW[i] * (1f - local * 0.6f)));
                    dh = Math.Max(1, (int)(tileH[i] * (1f - local * 0.6f)));
                    aa = (int)(255 * Math.Pow(1f - local, 0.8f));
                }
                else
                {
                    float sq = 1f - 0.55f * Math.Min(1f, local / 0.62f);   // 先被压扁
                    int dySq = (int)(f.Cy + (tileSY[i] - f.Cy) * sq);
                    ddx = (int)(dx + f.Vx * local);
                    ddy = (int)(dySq + ty + (int)shake * 0 + f.Vy * local + 0.5f * f.G * gr);
                    dw = tileW[i];
                    dh = Math.Max(1, (int)(tileH[i] * sq));
                    aa = local < 0.78f ? 255 : (int)(255 * (1f - (local - 0.78f) / 0.22f));
                }
                if (dw <= 0 || dh <= 0 || aa <= 2) continue;
                BlitTile(i, ddx, ddy, dw, dh, aa);
            }
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (memDc != IntPtr.Zero) { SelectObject(memDc, oldObj); DeleteDC(memDc); memDc = IntPtr.Zero; }
                if (dib != IntPtr.Zero) { DeleteObject(dib); dib = IntPtr.Zero; }
                if (screenDc != IntPtr.Zero) { ReleaseDC(IntPtr.Zero, screenDc); screenDc = IntPtr.Zero; }
            }
            catch { }
            base.Dispose(disposing);
        }
    }

    class Toast : Form
    {
        Label title, sub;
        Bitmap pic;
        Bitmap cachedShot;
        int imgX = -1, imgY = -1;
        public bool ForceTest;
        public bool AskMode;
        bool activateAfter;
        Point clickPt; bool clickPtValid;
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
            close.MouseDown += MarkClick;
            close.Click += delegate { ClickDismiss(); };   // 点 X 也从 X 处开始碎

            MouseDown += MarkClick;
            title.MouseDown += MarkClick;
            sub.MouseDown += MarkClick;
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
                // 被动拍碎：越过阈值后每秒掷一次骰，概率逐秒累加
                if (Cfg.Shatter == 1 && !autoShard && Cfg.Hold > 0 && Cfg.ShardPct > 0)
                {
                    int due = Cfg.Hold * Cfg.ShardPct / 100;
                    if (life >= due)
                    {
                        int sec = (life - due) / 1000;
                        if (sec != shardSec)
                        {
                            shardSec = sec;
                            int pb = Cfg.ShardP0 + sec * Cfg.ShardStep;
                            if (pb >= 100 || rndShard.Next(100) < pb)
                            {
                                autoShard = true;
                                HideToast();
                                return;
                            }
                        }
                    }
                }
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

        void MarkClick(object s2, MouseEventArgs e)
        {
            var c = s2 as Control;
            Point pt = e.Location;
            if (c != null && c != this) pt = new Point(pt.X + c.Left, pt.Y + c.Top);
            clickPt = pt;
            clickPtValid = true;
        }

        public void ClickDismiss()
        {
            Ctx.Log("click");
            activateAfter = false;
            HideToast();
            var one = new System.Windows.Forms.Timer();
            one.Interval = 40;
            one.Tick += delegate
            {
                one.Stop();
                one.Dispose();
                Program.ActivateDsh();
            };
            one.Start();
        }

        void CacheShot()
        {
            try
            {
                if (cachedShot != null) { cachedShot.Dispose(); cachedShot = null; }
                var b = new Bitmap(Width, Height);
                DrawToBitmap(b, new Rectangle(0, 0, Width, Height));
                cachedShot = b;
            }
            catch { }
        }

        public void ShowToast()
        {
            if (!ForceTest && (Program.DshForeground() || Cfg.TestFresh())) return;
            var wa = Screen.PrimaryScreen.WorkingArea;
            ClientSize = new Size(U.D(Cfg.W), U.D(Cfg.H));
            Cfg.Load();
            if (Program.DemoMode)
            {
                Cfg.ExitFx = Program.DemoFx;
                Cfg.MX = 700; Cfg.MY = 420;
                Cfg.Hold = 14000;
            }
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
            var snap = new System.Windows.Forms.Timer();
            snap.Interval = Math.Max(200, Cfg.AnimIn + 120);
            snap.Tick += delegate { snap.Stop(); snap.Dispose(); CacheShot(); };
            snap.Start();
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

        bool autoShard;
        int shardSec = -1;
        Random rndShard = new Random();
        Random rndFx = new Random();

        // 把消失交给拍地插件：把窗口矩形发过去，让冲击波把它打碎
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string cls, string win);
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr wp, ref COPYDATASTRUCT lp);
        [StructLayout(LayoutKind.Sequential)] struct COPYDATASTRUCT { public IntPtr dwData; public int cbData; public IntPtr lpData; }

        bool HandOffToSlap()
        {
            try
            {
                IntPtr sink = FindWindow(null, "slap_fx_sink");
                Ctx.Log("sink=" + sink.ToInt64());
                if (sink == IntPtr.Zero) return false;
                int[] d = new int[] { Handle.ToInt32(), Left, Top, Left + Width, Top + Height, Cfg.FxMs };
                GCHandle g = GCHandle.Alloc(d, GCHandleType.Pinned);
                try
                {
                    COPYDATASTRUCT cds = new COPYDATASTRUCT();
                    cds.dwData = (IntPtr)0x5AAB;
                    cds.cbData = d.Length * 4;
                    cds.lpData = g.AddrOfPinnedObject();
                    SendMessage(sink, 0x004A, Handle, ref cds);
                }
                finally { g.Free(); }
                return true;
            }
            catch { return false; }
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

            Ctx.Log("hide begin fx=" + Cfg.ExitFx + " vis=" + Visible);
            string fx = Cfg.ExitFx;
            // 随机消失：从统一清单里等概率抽一个（新特效只要加进 FxAll 就自动参与）
            if (fx == "random")
            {
                fx = Cfg.FxAll[rndFx.Next(Cfg.FxAll.Length)];
                Ctx.Log("random -> " + fx);
            }

            // 新特效：交给拍地插件，用冲击波把它击碎（点 X 仍然会正常跳转）
            if ((fx == "wave" || autoShard) && HandOffToSlap())
            {
                animOut = true;
                Ctx.Log("handoff wave");
                System.Windows.Forms.Timer wt = new System.Windows.Forms.Timer();
                wt.Interval = 140;
                wt.Tick += delegate
                {
                    wt.Stop(); wt.Dispose();
                    Hide();
                    Ctx.Log("toast hidden");
                    if (activateAfter) { activateAfter = false; Program.ActivateDsh(); }
                };
                wt.Start();
                return;
            }
            bool slide;
            if (fx == "slide") slide = true;
            else if (fx == "auto") slide = near <= (int)(44 * U.S);
            else slide = false;      // 点名指定了特效就按它自己的动画走，不再被边缘距离影响

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
                Bitmap shot = cachedShot;
                cachedShot = null;
                if (shot == null)
                {
                    shot = new Bitmap(Width, Height);
                    try { DrawToBitmap(shot, new Rectangle(0, 0, Width, Height)); }
                    catch { shot.Dispose(); Hide(); return; }
                }
                var loc = Location;
                string kind = (fx == "rise" || fx == "collapse" || fx == "hand") ? fx : "dissolve";
                int dur = kind == "collapse" ? (int)(Cfg.FxMs * 2.6f) : (kind == "rise" ? (int)(Cfg.FxMs * 1.10f) : (kind == "hand" ? (int)(Cfg.FxMs * 1.30f) : (int)(Cfg.FxMs * 1.08f)));
                var f = new Fx(shot, dur, kind, clickPtValid ? clickPt : new Point(Width / 2, Height / 2));
                f.Done = delegate
                {
                    if (activateAfter) { activateAfter = false; Program.ActivateDsh(); }
                };
                f.Play(loc, Width, Height);
                Hide();           // 特效层已就位且内容一致，此处切换不可见
                Ctx.Log("toast hidden");
            }
            catch { Hide(); if (ForceTest) Application.Exit(); }
        }
    }
}
