// SnapScroll - lightweight screenshot tool for Windows
// PrtSc -> drag a region -> copy / save / mark up / scrolling capture.
// Builds with the C# compiler that ships with .NET Framework 4.x (see build.bat).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("SnapScroll")]
[assembly: AssemblyProduct("SnapScroll")]
[assembly: AssemblyVersion("0.1.1.0")]
[assembly: AssemblyFileVersion("0.1.1.0")]
[assembly: AssemblyInformationalVersion("0.1.1-beta")]

namespace SnapScroll
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            bool created;
            using (var mutex = new Mutex(true, "SnapScroll_SingleInstance_7F3A9C", out created))
            {
                if (!created)
                {
                    MessageBox.Show("SnapScroll is already running. Look for its icon in the system tray.", "SnapScroll");
                    return;
                }
                Native.EnableDpiAwareness();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += (s, e) =>
                    MessageBox.Show("Something went wrong:\n\n" + e.Exception, "SnapScroll", MessageBoxButtons.OK, MessageBoxIcon.Error);
                bool silent = args.Length > 0 && args[0].Equals("/silent", StringComparison.OrdinalIgnoreCase);
                try { Application.Run(new TrayApp(silent)); }
                catch (Exception ex)
                {
                    MessageBox.Show("SnapScroll couldn't start:\n\n" + ex.Message, "SnapScroll", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                GC.KeepAlive(mutex);
            }
        }
    }

    // ------------------------------------------------------------------ Win32
    static class Native
    {
        public const int WH_KEYBOARD_LL = 13, WH_MOUSE_LL = 14;
        public const int WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202, WM_RBUTTONDOWN = 0x0204, WM_RBUTTONUP = 0x0205;
        public const int VK_RETURN = 0x0D, VK_SHIFT = 0x10, VK_S = 0x53;

        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData; public uint flags; public uint time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT r);
        public const int WM_KEYDOWN = 0x0100, WM_SYSKEYDOWN = 0x0104;
        public const int VK_SNAPSHOT = 0x2C, VK_ESCAPE = 0x1B, VK_LWIN = 0x5B, VK_RWIN = 0x5C;
        public const uint LLKHF_ALTDOWN = 0x20;
        public const uint WDA_EXCLUDEFROMCAPTURE = 0x11;
        public const uint WM_QUIT = 0x0012, WM_APP = 0x8000;

        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct KBDLLHOOKSTRUCT { public uint vkCode; public uint scanCode; public uint flags; public uint time; public IntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string name);
        [StructLayout(LayoutKind.Sequential)]
        public struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam; public IntPtr lParam; public uint time; public POINT pt; }
        [DllImport("user32.dll")] public static extern int GetMessage(out MSG msg, IntPtr hwnd, uint min, uint max);
        [DllImport("user32.dll")] public static extern bool PeekMessage(out MSG msg, IntPtr hwnd, uint min, uint max, uint remove);
        [DllImport("user32.dll")] public static extern bool TranslateMessage(ref MSG msg);
        [DllImport("user32.dll")] public static extern IntPtr DispatchMessage(ref MSG msg);
        [DllImport("user32.dll")] public static extern bool PostThreadMessage(uint threadId, uint msg, IntPtr w, IntPtr l);
        [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vk);
        [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);
        [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);
        [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr value);
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

        public static void EnableDpiAwareness()
        {
            try { if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return; } catch { }   // per-monitor v2
            try { SetProcessDPIAware(); } catch { }
        }

        public static bool KeyDown(int vk) { return (GetAsyncKeyState(vk) & 0x8000) != 0; }
    }

    // --------------------------------------------------------------- Settings
    static class Settings
    {
        const string Key = @"Software\SnapScroll";
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static string SaveFolder
        {
            get
            {
                try
                {
                    using (var k = Registry.CurrentUser.OpenSubKey(Key))
                    {
                        if (k != null)
                        {
                            var v = k.GetValue("SaveFolder") as string;
                            if (!string.IsNullOrEmpty(v)) return v;
                        }
                    }
                }
                catch { }
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "SnapScroll");
            }
            set { using (var k = Registry.CurrentUser.CreateSubKey(Key)) k.SetValue("SaveFolder", value); }
        }

        public static bool StartWithWindows
        {
            get
            {
                try { using (var k = Registry.CurrentUser.OpenSubKey(RunKey)) return k != null && k.GetValue("SnapScroll") != null; }
                catch { return false; }
            }
            set
            {
                using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (value) k.SetValue("SnapScroll", "\"" + Application.ExecutablePath + "\" /silent");
                    else k.DeleteValue("SnapScroll", false);
                }
            }
        }

        public static string NewFilePath(string ext)
        {
            string dir = SaveFolder;
            Directory.CreateDirectory(dir);
            string baseName = "Screenshot_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string p = Path.Combine(dir, baseName + ext);
            int i = 2;
            while (File.Exists(p)) { p = Path.Combine(dir, baseName + "_" + i + ext); i++; }
            return p;
        }
    }

    // ------------------------------------------------------------ Image utils
    static class ImageUtil
    {
        public static Bitmap Capture(Rectangle r)
        {
            var b = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(b))
                g.CopyFromScreen(r.Location, Point.Empty, r.Size, CopyPixelOperation.SourceCopy);
            return b;
        }

        public static Bitmap Crop(Bitmap src, Rectangle r)
        {
            var b = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(b))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.DrawImage(src, new Rectangle(0, 0, r.Width, r.Height), r, GraphicsUnit.Pixel);
            }
            return b;
        }

        public static void Save(Image img, string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            ImageFormat fmt = ImageFormat.Png;
            if (ext == ".jpg" || ext == ".jpeg") fmt = ImageFormat.Jpeg;
            else if (ext == ".bmp") fmt = ImageFormat.Bmp;
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            if (fmt == ImageFormat.Png) { img.Save(path, fmt); return; }
            using (var flat = new Bitmap(img.Width, img.Height, PixelFormat.Format24bppRgb))
            {
                using (var g = Graphics.FromImage(flat))
                {
                    g.Clear(Color.White);
                    g.DrawImage(img, new Rectangle(0, 0, img.Width, img.Height), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel);
                }
                flat.Save(path, fmt);
            }
        }

        public static bool CopyToClipboard(Image img)
        {
            for (int i = 0; i < 6; i++)
            {
                try { Clipboard.SetImage(img); return true; }
                catch (ExternalException) { Thread.Sleep(80); }
            }
            return false;
        }

        public static string SaveAsDialog(Image img, IWin32Window owner)
        {
            using (var dlg = new SaveFileDialog())
            {
                dlg.Filter = "PNG image|*.png|JPEG image|*.jpg|Bitmap|*.bmp";
                string folder = Settings.SaveFolder;
                dlg.InitialDirectory = Directory.Exists(folder) ? folder : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                dlg.FileName = "Screenshot_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".png";
                dlg.AddExtension = true;
                if (dlg.ShowDialog(owner) != DialogResult.OK) return null;
                Save(img, dlg.FileName);
                return dlg.FileName;
            }
        }

        public static void ShowInFolder(string path)
        {
            try
            {
                if (path != null && File.Exists(path)) Process.Start("explorer.exe", "/select,\"" + path + "\"");
                else Process.Start("explorer.exe", "\"" + Settings.SaveFolder + "\"");
            }
            catch { }
        }
    }

    static class AppInfo
    {
        // Single source of truth: the AssemblyInformationalVersion attribute at the top of this file.
        public static readonly string Version = ReadVersion();

        static string ReadVersion()
        {
            var a = (AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(
                Assembly.GetExecutingAssembly(), typeof(AssemblyInformationalVersionAttribute));
            return a != null ? a.InformationalVersion.Replace('-', ' ') : "dev";
        }
    }

    static class AppIcon
    {
        static Icon small, large;
        static Icon Load(Size size)
        {
            try
            {
                using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("SnapScroll.app.ico"))
                    if (s != null) return new Icon(s, size);
            }
            catch { }
            return SystemIcons.Application;
        }
        public static Icon Small { get { if (small == null) small = Load(SystemInformation.SmallIconSize); return small; } }
        public static Icon Large { get { if (large == null) large = Load(SystemInformation.IconSize); return large; } }
    }

    // ------------------------------------------------------------ Hook thread
    // Low-level hooks must answer within a few hundred milliseconds or Windows silently removes
    // them. So they live on their own thread that does nothing but pump messages; capture,
    // clipboard and saving happen elsewhere and can never make PrtSc stop working.
    class HookThread
    {
        const uint MSG_MOUSE_ON = Native.WM_APP + 1, MSG_MOUSE_OFF = Native.WM_APP + 2;
        readonly Native.LowLevelKeyboardProc keyProc, mouseProc;
        readonly ManualResetEvent ready = new ManualResetEvent(false);
        volatile uint threadId;
        public volatile bool KeyboardHooked;

        public HookThread(Native.LowLevelKeyboardProc keyProc, Native.LowLevelKeyboardProc mouseProc)
        {
            this.keyProc = keyProc;
            this.mouseProc = mouseProc;
        }

        public void Start()
        {
            var t = new Thread(Loop);
            t.IsBackground = true;
            t.Name = "SnapScroll hooks";
            t.Start();
            ready.WaitOne(5000);
        }

        void Loop()
        {
            IntPtr mod;
            using (var p = Process.GetCurrentProcess())
            using (var m = p.MainModule)
                mod = Native.GetModuleHandle(m.ModuleName);

            Native.MSG msg;
            Native.PeekMessage(out msg, IntPtr.Zero, 0, 0, 0);   // creates this thread's message queue
            threadId = Native.GetCurrentThreadId();
            IntPtr keyHook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, keyProc, mod, 0);
            IntPtr mouseHook = IntPtr.Zero;
            KeyboardHooked = keyHook != IntPtr.Zero;
            ready.Set();

            while (Native.GetMessage(out msg, IntPtr.Zero, 0, 0) > 0)
            {
                if (msg.hwnd == IntPtr.Zero && msg.message == MSG_MOUSE_ON)
                {
                    if (mouseHook == IntPtr.Zero) mouseHook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, mouseProc, mod, 0);
                    continue;
                }
                if (msg.hwnd == IntPtr.Zero && msg.message == MSG_MOUSE_OFF)
                {
                    if (mouseHook != IntPtr.Zero) { Native.UnhookWindowsHookEx(mouseHook); mouseHook = IntPtr.Zero; }
                    continue;
                }
                Native.TranslateMessage(ref msg);
                Native.DispatchMessage(ref msg);
            }
            if (mouseHook != IntPtr.Zero) Native.UnhookWindowsHookEx(mouseHook);
            if (keyHook != IntPtr.Zero) Native.UnhookWindowsHookEx(keyHook);
        }

        void Post(uint m) { if (threadId != 0) Native.PostThreadMessage(threadId, m, IntPtr.Zero, IntPtr.Zero); }
        public void MouseOn() { Post(MSG_MOUSE_ON); }
        public void MouseOff() { Post(MSG_MOUSE_OFF); }
        public void Stop() { Post(Native.WM_QUIT); }
    }

    // --------------------------------------------------------------- Tray app
    enum CaptureAction { None, CopySave, Copy, SaveAs, Edit, Scroll }

    class TrayApp : ApplicationContext
    {
        readonly NotifyIcon tray;
        readonly Control ui;
        readonly HookThread hooks;
        bool busy;
        string lastSaved;
        // Read by the hook thread, so volatile.
        volatile OverlayForm overlay;
        volatile ScrollSession scroll;
        volatile bool swallowUp;
        ScrollIndicator scrollIndicator;

        public TrayApp(bool silent)
        {
            ui = new Control();
            ui.CreateControl();
            IntPtr force = ui.Handle;

            tray = new NotifyIcon();
            tray.Icon = AppIcon.Small;
            tray.Text = "SnapScroll " + AppInfo.Version + " - press PrtSc";
            var menu = new ContextMenuStrip();
            var title = new ToolStripMenuItem("SnapScroll " + AppInfo.Version);
            title.Enabled = false;
            menu.Items.Add(title);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Capture region  (PrtSc)", null, delegate { ui.BeginInvoke((MethodInvoker)BeginCapture); });
            menu.Items.Add("Scrolling capture  (Shift+PrtSc)", null, delegate { ui.BeginInvoke((MethodInvoker)StartScrollMode); });
            menu.Items.Add("Open screenshots folder", null, delegate { ImageUtil.ShowInFolder(null); });
            menu.Items.Add("Change save folder...", null, delegate { ChangeFolder(); });
            var startup = new ToolStripMenuItem("Start with Windows");
            startup.Checked = Settings.StartWithWindows;
            startup.Click += delegate
            {
                try { Settings.StartWithWindows = !startup.Checked; } catch (Exception ex) { MessageBox.Show(ex.Message, "SnapScroll"); }
                startup.Checked = Settings.StartWithWindows;
            };
            menu.Items.Add(startup);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, delegate { ExitThread(); });
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { BeginCapture(); };
            tray.BalloonTipClicked += delegate { if (lastSaved != null) ImageUtil.ShowInFolder(lastSaved); };
            tray.Visible = true;

            hooks = new HookThread(HookCallback, MouseCallback);
            hooks.Start();

            if (!hooks.KeyboardHooked)
                tray.ShowBalloonTip(5000, "SnapScroll", "Couldn't hook the PrtSc key. Double-click the tray icon to capture instead.", ToolTipIcon.Warning);
            else if (!silent)
                tray.ShowBalloonTip(3000, "SnapScroll is running", "Press PrtSc to capture. Right-click the tray icon for options.", ToolTipIcon.Info);
        }

        protected override void ExitThreadCore()
        {
            hooks.Stop();
            tray.Visible = false;
            tray.Dispose();
            base.ExitThreadCore();
        }

        IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var k = (Native.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.KBDLLHOOKSTRUCT));
                int msg = wParam.ToInt32();
                bool down = msg == Native.WM_KEYDOWN || msg == Native.WM_SYSKEYDOWN;

                if (k.vkCode == Native.VK_SNAPSHOT)
                {
                    // Leave Alt+PrtSc and Win+PrtSc to Windows.
                    bool alt = (k.flags & Native.LLKHF_ALTDOWN) != 0;
                    bool win = Native.KeyDown(Native.VK_LWIN) || Native.KeyDown(Native.VK_RWIN);
                    if (!alt && !win)
                    {
                        if (down)
                        {
                            if (Native.KeyDown(Native.VK_SHIFT)) ui.BeginInvoke((MethodInvoker)StartScrollMode);
                            else ui.BeginInvoke((MethodInvoker)BeginCapture);
                        }
                        return (IntPtr)1;
                    }
                }
                else if (scroll != null && (k.vkCode == Native.VK_ESCAPE || k.vkCode == Native.VK_RETURN))
                {
                    if (down)
                    {
                        if (k.vkCode == Native.VK_ESCAPE) CancelScroll();
                        else if (scroll.Phase == 2) scroll.Stop = true;
                    }
                    return (IntPtr)1;
                }
                else if (k.vkCode == Native.VK_S && overlay != null)
                {
                    if (down) { var o = overlay; ui.BeginInvoke((MethodInvoker)delegate { o.StartScrolling(); }); }
                    return (IntPtr)1;
                }
                else if (k.vkCode == Native.VK_ESCAPE && overlay != null)
                {
                    if (down) { var o = overlay; ui.BeginInvoke((MethodInvoker)delegate { o.HandleEscape(); }); }
                    return (IntPtr)1;
                }
            }
            return Native.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        void BeginCapture()
        {
            if (busy) return;
            busy = true;
            Rectangle vs = SystemInformation.VirtualScreen;
            Bitmap shot;
            try { shot = ImageUtil.Capture(vs); }
            catch (Exception ex) { busy = false; Warn("Capture failed: " + ex.Message); return; }

            OverlayForm ov;
            try { ov = new OverlayForm(shot, vs); }
            catch (Exception ex) { shot.Dispose(); busy = false; Warn("Capture failed: " + ex.Message); return; }
            overlay = ov;
            ov.FormClosed += delegate
            {
                overlay = null;
                CaptureAction a = ov.Action;
                Bitmap crop = ov.Crop;
                string err = ov.Error;
                shot.Dispose();
                if (err != null) Warn("Capture failed: " + err);
                ui.BeginInvoke((MethodInvoker)delegate { HandleResult(a, crop); });
            };
            try { ov.Show(); ov.Activate(); }
            catch (Exception ex) { overlay = null; busy = false; Warn("Capture failed: " + ex.Message); }
        }

        void HandleResult(CaptureAction a, Bitmap crop)
        {
            try
            {
                switch (a)
                {
                    case CaptureAction.CopySave:
                        {
                            bool copied = ImageUtil.CopyToClipboard(crop);
                            string p = Settings.NewFilePath(".png");
                            ImageUtil.Save(crop, p);
                            lastSaved = p;
                            tray.ShowBalloonTip(2500, copied ? "Copied & saved" : "Saved (clipboard was busy)", Path.GetFileName(p) + "\nClick to show in folder.", ToolTipIcon.None);
                            break;
                        }
                    case CaptureAction.Copy:
                        if (ImageUtil.CopyToClipboard(crop)) tray.ShowBalloonTip(1500, "Copied to clipboard", crop.Width + " \u00D7 " + crop.Height, ToolTipIcon.None);
                        else Warn("The clipboard is busy; try again.");
                        break;
                    case CaptureAction.SaveAs:
                        {
                            string p = ImageUtil.SaveAsDialog(crop, null);
                            if (p != null) lastSaved = p;
                            break;
                        }
                    case CaptureAction.Edit:
                        {
                            var ed = new EditorForm(crop);
                            crop = null;  // editor owns it now
                            ShowFront(ed);
                            break;
                        }
                }
            }
            catch (Exception ex) { MessageBox.Show("Something went wrong:\n\n" + ex, "SnapScroll", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            finally
            {
                if (crop != null) crop.Dispose();
                busy = false;
            }
            if (a == CaptureAction.Scroll) StartScrollMode();
        }

        // ---- Scrolling capture: click top-left, scroll, click bottom-right.
        void StartScrollMode()
        {
            if (busy || scroll != null) return;
            busy = true;
            var ss = new ScrollSession();
            ss.Phase = 1;
            scroll = ss;
            swallowUp = false;
            scrollIndicator = new ScrollIndicator(ss);
            scrollIndicator.Show();
            hooks.MouseOn();
        }

        void CancelScroll()
        {
            var ss = scroll;
            if (ss == null) return;
            if (ss.Phase == 1) ui.BeginInvoke((MethodInvoker)EndScrollMode);
            else ss.Cancel = true;
        }

        void EndScrollMode()
        {
            scroll = null;
            if (!swallowUp) UnhookMouse();
            if (scrollIndicator != null) { scrollIndicator.Close(); scrollIndicator = null; }
            busy = false;
        }

        void BeginWatching()
        {
            var ss = scroll;
            if (ss == null) return;
            Point p1 = ss.P1;
            Rectangle mon = Screen.FromPoint(p1).Bounds;
            Rectangle watch = mon;
            Native.POINT np; np.X = p1.X; np.Y = p1.Y;
            IntPtr root = Native.GetAncestor(Native.WindowFromPoint(np), 2);
            Native.RECT wr;
            if (root != IntPtr.Zero && Native.GetWindowRect(root, out wr))
            {
                Rectangle w = Rectangle.Intersect(Rectangle.FromLTRB(wr.Left, wr.Top, wr.Right, wr.Bottom), mon);
                if (w.Width > 100 && w.Height > 100 && w.Contains(p1)) watch = w;
            }
            scrollIndicator.ShowCorner(p1);

            var t = new Thread(delegate()
            {
                Bitmap result = null;
                string msg = null;
                try { result = ScrollCapture.Run(watch, ss, out msg); }
                catch (Exception ex) { msg = "Scrolling capture failed: " + ex.Message; }

                ui.BeginInvoke((MethodInvoker)delegate
                {
                    EndScrollMode();
                    if (msg != null) tray.ShowBalloonTip(4000, "SnapScroll", msg, ToolTipIcon.Info);
                    if (result != null) ShowFront(new EditorForm(result));
                });
            });
            t.IsBackground = true;
            t.Start();
        }

        // Clicks during scrolling capture set the corners and never reach the page.
        IntPtr MouseCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                var ss = scroll;
                if (ss != null && (msg == Native.WM_LBUTTONDOWN || msg == Native.WM_RBUTTONDOWN))
                {
                    var info = (Native.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.MSLLHOOKSTRUCT));
                    Point pt = new Point(info.pt.X, info.pt.Y);
                    swallowUp = true;
                    if (msg == Native.WM_RBUTTONDOWN) CancelScroll();
                    else if (ss.Phase == 1)
                    {
                        ss.P1 = pt;
                        ss.Phase = 2;
                        ui.BeginInvoke((MethodInvoker)BeginWatching);
                    }
                    else if (ss.Phase == 2)
                    {
                        ss.P2 = pt;
                        ss.HasP2 = true;
                        ss.Phase = 3;
                        ss.Stop = true;
                    }
                    return (IntPtr)1;
                }
                if (swallowUp && (msg == Native.WM_LBUTTONUP || msg == Native.WM_RBUTTONUP))
                {
                    swallowUp = false;
                    if (scroll == null) hooks.MouseOff();
                    return (IntPtr)1;
                }
            }
            return Native.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        void UnhookMouse() { hooks.MouseOff(); }

        static void ShowFront(Form f)
        {
            f.TopMost = true;
            f.Show();
            Native.SetForegroundWindow(f.Handle);
            f.Activate();
            f.BringToFront();
            f.TopMost = false;
        }

        void ChangeFolder()
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Where should SnapScroll save screenshots?";
                dlg.SelectedPath = Settings.SaveFolder;
                if (dlg.ShowDialog() == DialogResult.OK) Settings.SaveFolder = dlg.SelectedPath;
            }
        }

        void Warn(string m) { tray.ShowBalloonTip(4000, "SnapScroll", m, ToolTipIcon.Warning); }
    }

    // -------------------------------------------------------- Region overlay
    class OverlayForm : Form
    {
        readonly Bitmap shot, dim;
        readonly Rectangle vs;
        readonly ContextMenuStrip menu;
        Point start;
        Rectangle sel = Rectangle.Empty;
        bool dragging, menuChose, finished;

        public CaptureAction Action = CaptureAction.None;
        public Bitmap Crop;
        public string Error;

        public OverlayForm(Bitmap shot, Rectangle vs)
        {
            this.shot = shot;
            this.vs = vs;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            Bounds = vs;
            TopMost = true;
            ShowInTaskbar = false;
            KeyPreview = true;
            Cursor = Cursors.Cross;
            BackColor = Color.Black;
            Text = "SnapScroll";
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

            dim = new Bitmap(shot.Width, shot.Height, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(dim))
            {
                g.DrawImage(shot, new Rectangle(0, 0, shot.Width, shot.Height), 0, 0, shot.Width, shot.Height, GraphicsUnit.Pixel);
                using (var b = new SolidBrush(Color.FromArgb(120, 0, 0, 0))) g.FillRectangle(b, 0, 0, shot.Width, shot.Height);
            }

            menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;
            AddItem("&Copy + Save", "Enter", CaptureAction.CopySave);
            AddItem("Copy &only", "", CaptureAction.Copy);
            AddItem("&Save as...", "", CaptureAction.SaveAs);
            AddItem("&Edit / markup...", "", CaptureAction.Edit);
            menu.Items.Add(new ToolStripSeparator());
            AddItem("Sc&rolling capture...", "S", CaptureAction.Scroll);
            menu.Items.Add(new ToolStripSeparator());
            var cancel = new ToolStripMenuItem("Cancel");
            cancel.ShortcutKeyDisplayString = "Esc";
            cancel.Click += delegate { menuChose = true; Finish(CaptureAction.None); };
            menu.Items.Add(cancel);
            menu.Closed += (s, e) =>
            {
                // Windows closes the menu *before* raising the item's Click, so don't
                // clear the selection when the menu closed because an item was picked.
                if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked) return;
                BeginInvoke((MethodInvoker)delegate
                {
                    if (!menuChose && !finished) { sel = Rectangle.Empty; Invalidate(); }
                });
            };
        }

        void AddItem(string text, string keys, CaptureAction a)
        {
            var it = new ToolStripMenuItem(text);
            it.ShortcutKeyDisplayString = keys;
            it.Click += delegate { menuChose = true; Finish(a); };
            menu.Items.Add(it);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Bounds = vs;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            dim.Dispose();
            menu.Dispose();
            base.OnFormClosed(e);
        }

        public void HandleEscape()
        {
            if (menu.Visible) { menu.Close(); return; }
            if (dragging || !sel.IsEmpty) { dragging = false; sel = Rectangle.Empty; Invalidate(); return; }
            Finish(CaptureAction.None);
        }

        void Finish(CaptureAction a)
        {
            if (finished) return;
            finished = true;
            try
            {
                Action = a;
                if (a == CaptureAction.Scroll) { }
                else if (a != CaptureAction.None)
                {
                    Rectangle r = Rectangle.Intersect(sel, new Rectangle(0, 0, shot.Width, shot.Height));
                    if (r.Width > 0 && r.Height > 0) Crop = ImageUtil.Crop(shot, r);
                    else Action = CaptureAction.None;
                }
            }
            catch (Exception ex)
            {
                Action = CaptureAction.None;
                if (Crop != null) { Crop.Dispose(); Crop = null; }
                Error = ex.Message;
            }
            finally
            {
                // Always close, so the overlay can never be left swallowing keys.
                try { BeginInvoke((MethodInvoker)Close); } catch { Close(); }
            }
        }

        public void StartScrolling()
        {
            menuChose = true;
            if (menu.Visible) menu.Close();
            Finish(CaptureAction.Scroll);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) HandleEscape();
            else if (e.KeyCode == Keys.S) StartScrolling();
            else if (e.KeyCode == Keys.Enter && !sel.IsEmpty) { menuChose = true; menu.Close(); Finish(CaptureAction.CopySave); }
            base.OnKeyDown(e);
        }

        static Rectangle Norm(Point a, Point b)
        {
            return Rectangle.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X) + 1, Math.Max(a.Y, b.Y) + 1);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                dragging = true;
                start = e.Location;
                sel = Rectangle.Empty;
                Invalidate();
            }
            else if (e.Button == MouseButtons.Right) HandleEscape();
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging)
            {
                Rectangle old = sel;
                sel = Rectangle.Intersect(Norm(start, e.Location), new Rectangle(0, 0, vs.Width, vs.Height));
                Rectangle u = old.IsEmpty ? sel : Rectangle.Union(old, sel);
                u.Inflate(6, 6);
                u = Rectangle.Union(u, new Rectangle(u.X, u.Y - 40, 260, 40));
                Invalidate(u);
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && dragging)
            {
                dragging = false;
                if (sel.Width < 5 || sel.Height < 5)
                {
                    // A simple click grabs the whole monitor under the cursor.
                    Rectangle mon = Screen.FromPoint(PointToScreen(e.Location)).Bounds;
                    sel = new Rectangle(mon.X - vs.X, mon.Y - vs.Y, mon.Width, mon.Height);
                }
                Invalidate();
                menuChose = false;
                menu.Show(this, e.Location);
                if (menu.Items.Count > 0) menu.Items[0].Select();
            }
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Rectangle clip = e.ClipRectangle;
            g.DrawImage(dim, clip, clip, GraphicsUnit.Pixel);

            if (sel.Width > 0 && sel.Height > 0)
            {
                Rectangle i = Rectangle.Intersect(sel, clip);
                if (!i.IsEmpty) g.DrawImage(shot, i, i, GraphicsUnit.Pixel);
                using (var pen = new Pen(Color.FromArgb(0, 153, 255), 2))
                    g.DrawRectangle(pen, sel.X, sel.Y, sel.Width - 1, sel.Height - 1);

                string label = sel.Width + " \u00D7 " + sel.Height;
                using (var f = new Font("Segoe UI", 10f, FontStyle.Bold))
                {
                    Size sz = TextRenderer.MeasureText(label, f);
                    int ly = sel.Y - sz.Height - 6;
                    if (ly < 0) ly = sel.Y + 4;
                    var lr = new Rectangle(sel.X, ly, sz.Width + 8, sz.Height + 2);
                    using (var b = new SolidBrush(Color.FromArgb(220, 20, 20, 20))) g.FillRectangle(b, lr);
                    TextRenderer.DrawText(g, label, f, lr, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }
            else
            {
                string hint = "Drag to select   \u2022   Click = whole screen   \u2022   S = scrolling capture   \u2022   Esc = cancel";
                Rectangle mon = Screen.FromPoint(Cursor.Position).Bounds;
                mon.Offset(-vs.X, -vs.Y);
                using (var f = new Font("Segoe UI", 11f))
                {
                    Size sz = TextRenderer.MeasureText(hint, f);
                    var hr = new Rectangle(mon.X + (mon.Width - sz.Width - 24) / 2, mon.Y + 40, sz.Width + 24, sz.Height + 14);
                    using (var b = new SolidBrush(Color.FromArgb(230, 25, 25, 25))) g.FillRectangle(b, hr);
                    TextRenderer.DrawText(g, hint, f, hr, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }
        }
    }

    // ------------------------------------------------- Scrolling capture core
    class ScrollSession
    {
        public volatile int Phase;          // 1 = waiting for top-left click, 2 = scrolling, 3 = finishing
        public volatile bool Stop, Cancel, Lost, Scrolled, HasP2;
        public volatile int Height;
        public volatile int Frames;         // frames examined (diagnostics)
        public Point P1, P2;
    }

    static class ScrollCapture
    {
        const int MaxHeight = 30000;
        const int K = 72;              // sample columns per row
        const int PixTol = 40;         // per-sample brightness tolerance (0-255)

        class Frame
        {
            public Bitmap Bmp;
            public byte[] G;           // H * K grayscale samples
            public bool[] Tex;         // row has detail worth matching
            public int H;
        }

        static Frame Grab(Rectangle r, int[] cols)
        {
            var b = ImageUtil.Capture(r);
            int W = b.Width, H = b.Height;
            var f = new Frame { Bmp = b, H = H, G = new byte[H * K], Tex = new bool[H] };
            var d = b.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int[] row = new int[W];
                for (int y = 0; y < H; y++)
                {
                    Marshal.Copy(IntPtr.Add(d.Scan0, y * d.Stride), row, 0, W);
                    int mn = 255, mx = 0, o = y * K;
                    for (int k = 0; k < K; k++)
                    {
                        int x = cols[k], sum = 0;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int v = row[Math.Max(0, Math.Min(W - 1, x + dx))];
                            sum += (((v >> 16) & 255) * 3 + ((v >> 8) & 255) * 6 + (v & 255)) / 10;
                        }
                        int gv = sum / 3;
                        f.G[o + k] = (byte)gv;
                        if (gv < mn) mn = gv;
                        if (gv > mx) mx = gv;
                    }
                    f.Tex[y] = mx - mn > 24;
                }
            }
            finally { b.UnlockBits(d); }
            return f;
        }

        static bool RowBad(Frame a, int ra, Frame b, int rb)
        {
            int oa = ra * K, ob = rb * K, bad = 0;
            for (int k = 0; k < K; k++)
            {
                int dv = a.G[oa + k] - b.G[ob + k];
                if (dv > PixTol || dv < -PixTol) { if (++bad > K / 5) return true; }
            }
            return false;
        }

        static bool Same(Frame a, Frame b)
        {
            int bad = 0, lim = a.H / 100 + 1;
            for (int y = 0; y < a.H; y++) if (RowBad(a, y, b, y) && ++bad > lim) return false;
            return true;
        }

        // Rows that look the same at the same place in both frames (browser toolbar, sticky header/footer).
        static void FixedBands(Frame a, Frame b, out int top, out int bottom)
        {
            int H = a.H;
            top = 0;
            while (top < H * 45 / 100 && !RowBad(a, top, b, top)) top++;
            bottom = 0;
            while (bottom < H * 35 / 100 && !RowBad(a, H - 1 - bottom, b, H - 1 - bottom)) bottom++;
        }

        static int RowSad(Frame a, int ra, Frame b, int rb, out bool bad)
        {
            int oa = ra * K, ob = rb * K, sad = 0, nbad = 0;
            for (int k = 0; k < K; k++)
            {
                int dv = a.G[oa + k] - b.G[ob + k];
                if (dv < 0) dv = -dv;
                sad += dv;
                if (dv > PixTol) nbad++;
            }
            bad = nbad > K / 5;
            return sad;
        }

        // Find s such that cur row r matches prev row r + s. Only the middle of the window is
        // compared (sticky headers appear at the top, chat bubbles sit at the bottom), and the
        // winner is the shift with the lowest average difference, so it lands on the exact pixel.
        static int FindShift(Frame prev, Frame cur, int c0, int c1)
        {
            int n = c1 - c0;
            int m0 = c0 + n / 5, m1 = c1 - n / 10;
            int best = -1;
            double bestErr = double.MaxValue;
            int minRows = Math.Max(60, (m1 - m0) * 55 / 100);   // demand a solid overlap: thin slivers can fool us
            int cap = K * 25;   // one row can only count so much: a local animation can't veto a clear match
            for (int s = 1; ; s++)
            {
                int rEnd = Math.Min(m1, c1 - s);
                int rows = rEnd - m0;
                if (rows < minRows) break;
                int maxBad = rows / 4 + 2, used = 0, nbad = 0;   // a local animation may spoil up to a quarter of rows
                long sum = 0;
                for (int r = m0; r < rEnd; r++)
                {
                    if (!prev.Tex[r + s] && !cur.Tex[r]) continue;
                    bool rb;
                    int d = RowSad(prev, r + s, cur, r, out rb);
                    sum += d < cap ? d : cap;
                    used++;
                    if (rb && ++nbad > maxBad) break;
                }
                if (nbad > maxBad || used < 20) continue;
                double err = sum / (double)used;
                if (err < bestErr - 0.01) { bestErr = err; best = s; }
            }
            // Reject a "best" that still isn't a real match (a few levels of noise per sample at most).
            return bestErr <= K * 10 ? best : -1;
        }

        public static Bitmap Run(Rectangle watch, ScrollSession ss, out string message)
        {
            message = null;
            int W = watch.Width, H = watch.Height;

            // Sample columns to the right of the first click (avoids fixed left sidebars and the scrollbar).
            int xs = ss.P1.X - watch.X, xe = W - 26;
            if (xe - xs < 150) { xs = 0; xe = W; }
            int[] cols = new int[K];
            for (int k = 0; k < K; k++) cols[k] = xs + (int)((xe - xs - 1) * (k + 0.5) / K);

            // New content is taken from above this bottom margin, so pinned footers / chat bubbles never repeat.
            int margin = Math.Max(40, H / 4);

            Frame prev = Grab(watch, cols);
            for (int i = 0; i < 10; i++)   // make sure the first view is settled
            {
                Thread.Sleep(40);
                Frame again = Grab(watch, cols);
                bool same = Same(prev, again);
                prev.Bmp.Dispose();
                prev = again;
                if (same) break;
            }
            var pieces = new List<Bitmap>();
            pieces.Add(ImageUtil.Crop(prev.Bmp, new Rectangle(0, 0, W, H - margin)));
            int off = 0;
            int lostSince = 0;
            int y1 = Math.Max(0, Math.Min(H - 1, ss.P1.Y - watch.Y));
            ss.Height = H - y1;
            bool finalPass = false;

            try
            {
                while (true)
                {
                    if (ss.Cancel) return null;
                    if (ss.Stop)
                    {
                        if (finalPass) break;
                        finalPass = true;          // one last look after the page settles
                        Thread.Sleep(120);
                    }
                    else Thread.Sleep(15);

                    Frame cur = Grab(watch, cols);
                    ss.Frames++;
                    if (Same(prev, cur)) { cur.Bmp.Dispose(); continue; }

                    int t, b;
                    FixedBands(prev, cur, out t, out b);
                    int c0 = t, c1 = H - b;
                    if (c1 - c0 < 120) { c0 = 0; c1 = H; }

                    int s = FindShift(prev, cur, c0, c1);
                    if (s <= 0 || s > H - margin - 10)
                    {
                        bool up = FindShift(cur, prev, c0, c1) > 0;
                        cur.Bmp.Dispose();
                        if (up) { lostSince = 0; ss.Lost = false; }
                        else if (lostSince == 0) lostSince = Environment.TickCount;
                        else if (Environment.TickCount - lostSince > 500) ss.Lost = true;
                        continue;
                    }
                    lostSince = 0;
                    ss.Lost = false;
                    ss.Scrolled = true;

                    pieces.Add(ImageUtil.Crop(cur.Bmp, new Rectangle(0, H - margin - s, W, s)));
                    off += s;
                    ss.Height = off + H - y1;
                    prev.Bmp.Dispose();
                    prev = cur;

                    if (off + H >= MaxHeight) { message = "Stopped at the size limit (about " + (MaxHeight / H) + " screens)."; break; }
                }

                pieces.Add(ImageUtil.Crop(prev.Bmp, new Rectangle(0, H - margin, W, margin)));

                // Work out the crop in "whole page" coordinates: screen row r of the last view = off + r.
                int x1 = ss.P1.X - watch.X, x2 = W;
                int yEnd = off + H;
                if (ss.HasP2)
                {
                    x2 = ss.P2.X - watch.X;
                    yEnd = off + Math.Max(0, Math.Min(H, ss.P2.Y - watch.Y));
                }
                int xa = Math.Max(0, Math.Min(x1, x2)), xb = Math.Min(W, Math.Max(x1, x2) + 1);
                if (xb - xa < 20) { xa = Math.Max(0, Math.Min(x1, W - 20)); xb = W; }
                int ya = Math.Min(y1, yEnd), yb = Math.Max(y1, yEnd) + 1;
                if (yb - ya < 10) { ya = 0; yb = off + H; }
                yb = Math.Min(yb, off + H);

                var result = new Bitmap(xb - xa, yb - ya, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(result))
                {
                    g.CompositingMode = CompositingMode.SourceCopy;
                    int y = 0;
                    foreach (var p in pieces)
                    {
                        // piece occupies page rows [y, y + p.Height)
                        int top = Math.Max(y, ya), bot = Math.Min(y + p.Height, yb);
                        if (bot > top)
                            g.DrawImage(p, new Rectangle(0, top - ya, xb - xa, bot - top),
                                        new Rectangle(xa, top - y, xb - xa, bot - top), GraphicsUnit.Pixel);
                        y += p.Height;
                    }
                }
                return result;
            }
            finally
            {
                foreach (var p in pieces) p.Dispose();
                if (prev != null) prev.Bmp.Dispose();
            }
        }
    }

    // Click-through, never-activating, excluded-from-capture topmost window.
    class GhostForm : Form
    {
        public GhostForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            ShowInTaskbar = false;
            TopMost = true;
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x08000000 /*NOACTIVATE*/ | 0x80 /*TOOLWINDOW*/ | 0x20 /*TRANSPARENT*/ | 0x80000 /*LAYERED*/;
                return cp;
            }
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { Native.SetWindowDisplayAffinity(Handle, Native.WDA_EXCLUDEFROMCAPTURE); } catch { }
        }
    }

    class FrameStrip : GhostForm
    {
        public FrameStrip(Rectangle bounds)
        {
            BackColor = Color.FromArgb(0, 153, 255);
            Opacity = 0.95;
            Bounds = bounds;
        }
    }

    // Label that follows the mouse with instructions and the captured length.
    class ScrollIndicator : GhostForm
    {
        readonly ScrollSession ss;
        readonly System.Windows.Forms.Timer timer;
        readonly Font big = new Font("Segoe UI", 13f, FontStyle.Bold);
        readonly Font small = new Font("Segoe UI", 9f);
        readonly List<FrameStrip> corner = new List<FrameStrip>();
        string line1 = "", line2 = "";
        bool lost;
        int screenH;

        public ScrollIndicator(ScrollSession ss)
        {
            this.ss = ss;
            Opacity = 0.95;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            timer = new System.Windows.Forms.Timer();
            timer.Interval = 30;
            timer.Tick += delegate { Tick(); };
        }

        public void ShowCorner(Point p)
        {
            screenH = Screen.FromPoint(p).Bounds.Height;
            corner.Add(new FrameStrip(new Rectangle(p.X - 3, p.Y - 3, 28, 3)));
            corner.Add(new FrameStrip(new Rectangle(p.X - 3, p.Y, 3, 25)));
            foreach (var c in corner) c.Show();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Tick();
            timer.Start();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timer.Stop();
            timer.Dispose();
            foreach (var c in corner) c.Close();
            big.Dispose();
            small.Dispose();
            base.OnFormClosed(e);
        }

        void Tick()
        {
            if (ss.Scrolled && corner.Count > 0) { foreach (var cs in corner) cs.Close(); corner.Clear(); }

            string l1, l2;
            bool lo = false;
            int phase = ss.Phase;
            if (phase == 1)
            {
                l1 = "\u2460  Click the TOP-LEFT corner";
                l2 = "of the area you want to capture   \u2022   Esc = cancel";
            }
            else if (phase == 2)
            {
                double screens = ss.Height / (double)Math.Max(1, screenH);
                l1 = "\u2461  Scroll, then click the BOTTOM-RIGHT corner";
                lo = ss.Lost;
                l2 = lo ? "Lost track \u2014 scroll back up a little"
                        : "\u2195 " + screens.ToString("0.0") + " screens  (" + ss.Height.ToString("N0") + " px)   \u2022   Esc = cancel";
            }
            else { l1 = "Finishing..."; l2 = ""; }

            if (l1 != line1 || l2 != line2 || lo != lost)
            {
                line1 = l1; line2 = l2; lost = lo;
                Size s1 = TextRenderer.MeasureText(line1, big), s2 = TextRenderer.MeasureText(line2, small);
                Size = new Size(Math.Max(s1.Width, s2.Width) + 26, s1.Height + s2.Height + 14);
                Invalidate();
            }
            Point c = Cursor.Position;
            Rectangle wa = Screen.FromPoint(c).WorkingArea;
            int x = c.X + 22, y = c.Y + 22;
            if (x + Width > wa.Right) x = c.X - Width - 10;
            if (y + Height > wa.Bottom) y = c.Y - Height - 10;
            if (Left != x || Top != y) Location = new Point(x, y);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(lost ? Color.FromArgb(200, 90, 0) : Color.FromArgb(28, 28, 28));
            using (var p = new Pen(lost ? Color.FromArgb(255, 170, 60) : Color.FromArgb(0, 153, 255), 2))
                g.DrawRectangle(p, 1, 1, Width - 3, Height - 3);
            Size s1 = TextRenderer.MeasureText(line1, big);
            TextRenderer.DrawText(g, line1, big, new Point(12, 6), Color.White);
            TextRenderer.DrawText(g, line2, small, new Point(13, 6 + s1.Height), Color.FromArgb(225, 225, 225));
        }
    }

    // ------------------------------------------------------------ Markup editor
    enum Tool { Box, Arrow, Highlight, Text, Redact }

    class Shape
    {
        public Tool Kind;
        public Point A, B;
        public Color Color;
        public float Width;
        public string Text;
        public float FontSize;
    }

    class Canvas : Panel
    {
        public Canvas()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        }
    }

    class EditorForm : Form
    {
        readonly Bitmap img;
        readonly List<Shape> shapes = new List<Shape>();
        readonly List<Shape> redo = new List<Shape>();
        readonly Dictionary<Tool, ToolStripButton> toolButtons = new Dictionary<Tool, ToolStripButton>();
        Tool tool = Tool.Box;
        Color color = Color.FromArgb(232, 30, 37);
        float penWidth = 3f;
        float fontSize = 22f;
        float zoom = 1f;
        bool fitWidth;
        bool dirty = true;
        bool suppress;
        string savedPath;
        Shape cur;
        TextBox editBox;
        Point editPos;

        readonly ToolStrip ts;
        readonly Panel host;
        readonly Canvas canvas;
        ToolStripDropDownButton colorBtn;
        ToolStripComboBox zoomBox;
        ToolStripLabel statusLbl;

        static readonly string[] ZoomItems = { "Fit width", "25%", "50%", "75%", "100%", "150%", "200%" };

        public EditorForm(Bitmap image)
        {
            img = image;
            Text = "SnapScroll " + AppInfo.Version + "  \u2014  " + img.Width + " \u00D7 " + img.Height;
            Icon = AppIcon.Large;
            KeyPreview = true;
            AutoScaleMode = AutoScaleMode.None;
            Font = new Font("Segoe UI", 9.5f);
            StartPosition = FormStartPosition.Manual;

            ts = new ToolStrip();
            ts.GripStyle = ToolStripGripStyle.Hidden;
            ts.Padding = new Padding(6, 3, 6, 3);
            ts.RenderMode = ToolStripRenderMode.System;

            AddToolButton(Tool.Box, "\u25A1 Box", "Rectangle outline (B)", Color.Empty);
            AddToolButton(Tool.Arrow, "\u2197 Arrow", "Arrow (A)", Color.Empty);
            AddToolButton(Tool.Highlight, "\u2588 Highlight", "Yellow highlighter (H)", Color.Goldenrod);
            AddToolButton(Tool.Text, "T  Text", "Text (T) - click to place, Enter to finish, Shift+Enter for new line", Color.Empty);
            AddToolButton(Tool.Redact, "\u25A0 Redact", "Black-out box to hide info (R)", Color.Empty);
            ts.Items.Add(new ToolStripSeparator());

            colorBtn = new ToolStripDropDownButton("Color");
            colorBtn.ToolTipText = "Color for boxes, arrows and text";
            AddColor("Red", Color.FromArgb(232, 30, 37));
            AddColor("Orange", Color.FromArgb(255, 140, 0));
            AddColor("Yellow", Color.FromArgb(255, 214, 0));
            AddColor("Green", Color.FromArgb(22, 163, 74));
            AddColor("Blue", Color.FromArgb(24, 119, 242));
            AddColor("Purple", Color.FromArgb(147, 51, 234));
            AddColor("Black", Color.Black);
            AddColor("White", Color.White);
            colorBtn.DropDownItems.Add(new ToolStripSeparator());
            colorBtn.DropDownItems.Add("More colors...", null, delegate
            {
                using (var dlg = new ColorDialog())
                {
                    dlg.Color = color;
                    dlg.FullOpen = true;
                    if (dlg.ShowDialog(this) == DialogResult.OK) SetColor(dlg.Color);
                }
            });
            ts.Items.Add(colorBtn);

            ts.Items.Add(new ToolStripLabel("Line"));
            var widthBox = MakeCombo(new[] { "2", "3", "5", "8" }, "3", 45);
            widthBox.SelectedIndexChanged += delegate { penWidth = float.Parse(widthBox.Text); host.Focus(); };
            ts.Items.Add(widthBox);

            ts.Items.Add(new ToolStripLabel("Text"));
            var fontBox = MakeCombo(new[] { "16", "22", "30", "44", "60" }, "22", 45);
            fontBox.SelectedIndexChanged += delegate { fontSize = float.Parse(fontBox.Text); host.Focus(); };
            ts.Items.Add(fontBox);

            ts.Items.Add(new ToolStripSeparator());
            AddButton("\u21B6 Undo", "Undo (Ctrl+Z)", delegate { Undo(); });
            AddButton("\u21B7 Redo", "Redo (Ctrl+Y)", delegate { Redo(); });
            ts.Items.Add(new ToolStripSeparator());

            zoomBox = MakeCombo(ZoomItems, "100%", 80);
            zoomBox.ToolTipText = "Zoom (Ctrl + / Ctrl - / Ctrl 0)";
            zoomBox.SelectedIndexChanged += delegate
            {
                if (suppress) return;
                if (zoomBox.SelectedIndex == 0) SetZoom(0, true);
                else SetZoom(float.Parse(zoomBox.Text.TrimEnd('%')) / 100f, false);
                host.Focus();
            };
            ts.Items.Add(zoomBox);

            var saveAs = AddButton("Save as...", "Save as (Ctrl+Shift+S)", delegate { SaveAs(); });
            var save = AddButton("\u1F4BE Save", "Save to screenshots folder (Ctrl+S)", delegate { Save(); });
            var copy = AddButton("\u29C9 Copy", "Copy to clipboard (Ctrl+C)", delegate { Copy(); });
            copy.Font = new Font(Font, FontStyle.Bold);
            statusLbl = new ToolStripLabel("");
            statusLbl.ForeColor = Color.FromArgb(22, 130, 60);
            statusLbl.IsLink = false;
            statusLbl.Click += delegate { if (savedPath != null) ImageUtil.ShowInFolder(savedPath); };
            foreach (ToolStripItem it in new ToolStripItem[] { copy, save, saveAs, statusLbl }) it.Alignment = ToolStripItemAlignment.Right;
            ts.Items.Remove(saveAs); ts.Items.Remove(save); ts.Items.Remove(copy);
            ts.Items.Add(copy); ts.Items.Add(save); ts.Items.Add(saveAs); ts.Items.Add(statusLbl);

            host = new Panel();
            host.Dock = DockStyle.Fill;
            host.AutoScroll = true;
            host.BackColor = Color.FromArgb(64, 64, 64);
            canvas = new Canvas();
            canvas.Location = Point.Empty;
            canvas.Cursor = Cursors.Cross;
            canvas.Paint += Canvas_Paint;
            canvas.MouseDown += Canvas_MouseDown;
            canvas.MouseMove += Canvas_MouseMove;
            canvas.MouseUp += Canvas_MouseUp;
            host.Controls.Add(canvas);
            host.Resize += delegate { if (fitWidth) SetZoom(0, true); };

            Controls.Add(host);
            Controls.Add(ts);

            SetTool(Tool.Box);
            SetColor(color);

            // Window size: fit the image, up to 90% of the screen.
            Rectangle wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            int chromeW = Width - ClientSize.Width, chromeH = Height - ClientSize.Height;
            int tbH = ts.PreferredSize.Height;
            int sb = SystemInformation.VerticalScrollBarWidth + 4;
            int maxW = (int)(wa.Width * 0.9), maxH = (int)(wa.Height * 0.9);
            int w = Math.Max(Math.Max(760, ts.PreferredSize.Width + 20), Math.Min(maxW, img.Width + sb + chromeW));
            int h = Math.Max(420, Math.Min(maxH, img.Height + tbH + sb + chromeH));
            w = Math.Min(w, wa.Width);
            Bounds = new Rectangle(wa.X + (wa.Width - w) / 2, wa.Y + (wa.Height - h) / 2, w, h);

            if (img.Width + sb > w - chromeW) SetZoom(0, true); else SetZoom(1f, false);
        }

        // ---- toolbar helpers
        void AddToolButton(Tool t, string text, string tip, Color fore)
        {
            var b = new ToolStripButton(text);
            b.ToolTipText = tip;
            b.DisplayStyle = ToolStripItemDisplayStyle.Text;
            if (!fore.IsEmpty) b.ForeColor = fore;
            b.Click += delegate { SetTool(t); };
            ts.Items.Add(b);
            toolButtons[t] = b;
        }

        ToolStripButton AddButton(string text, string tip, EventHandler click)
        {
            var b = new ToolStripButton(text);
            b.ToolTipText = tip;
            b.DisplayStyle = ToolStripItemDisplayStyle.Text;
            b.Click += click;
            ts.Items.Add(b);
            return b;
        }

        ToolStripComboBox MakeCombo(string[] items, string sel, int width)
        {
            var c = new ToolStripComboBox();
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            c.Items.AddRange(items);
            c.SelectedItem = sel;
            c.AutoSize = false;
            c.Width = width;
            c.FlatStyle = FlatStyle.System;
            return c;
        }

        void AddColor(string name, Color c)
        {
            colorBtn.DropDownItems.Add(name, Swatch(c), delegate { SetColor(c); });
        }

        static Bitmap Swatch(Color c)
        {
            var b = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(b))
            {
                using (var br = new SolidBrush(c)) g.FillRectangle(br, 1, 1, 14, 14);
                g.DrawRectangle(Pens.Gray, 1, 1, 13, 13);
            }
            return b;
        }

        void SetColor(Color c)
        {
            color = c;
            var old = colorBtn.Image;
            colorBtn.Image = Swatch(c);
            colorBtn.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            if (old != null) old.Dispose();
            if (editBox != null) editBox.ForeColor = c;
        }

        void SetTool(Tool t)
        {
            CommitText();
            tool = t;
            foreach (var kv in toolButtons) kv.Value.Checked = kv.Key == t;
            canvas.Cursor = t == Tool.Text ? Cursors.IBeam : Cursors.Cross;
        }

        void SetStatus(string s) { statusLbl.Text = s; }

        // ---- zoom
        void SetZoom(float z, bool fit)
        {
            CommitText();
            fitWidth = fit;
            if (fit)
            {
                int full = Math.Max(50, host.Width - 2);
                z = Math.Min(1f, full / (float)img.Width);
                if (img.Height * z > host.Height)   // a vertical scrollbar will appear: leave room for it
                    z = Math.Min(1f, Math.Max(50, full - SystemInformation.VerticalScrollBarWidth) / (float)img.Width);
            }
            z = Math.Max(0.05f, Math.Min(4f, z));
            zoom = z;
            canvas.Size = new Size((int)Math.Ceiling(img.Width * zoom), (int)Math.Ceiling(img.Height * zoom));
            canvas.Invalidate();
            UpdateZoomBox();
        }

        void UpdateZoomBox()
        {
            suppress = true;
            if (fitWidth) zoomBox.SelectedIndex = 0;
            else
            {
                string s = Math.Round(zoom * 100) + "%";
                int i = zoomBox.Items.IndexOf(s);
                if (i < 0) { zoomBox.Items.Add(s); i = zoomBox.Items.Count - 1; }
                zoomBox.SelectedIndex = i;
            }
            suppress = false;
        }

        void StepZoom(int dir)
        {
            float[] steps = { 0.1f, 0.25f, 0.33f, 0.5f, 0.75f, 1f, 1.25f, 1.5f, 2f, 3f, 4f };
            float z = zoom;
            if (dir > 0) { foreach (var s in steps) if (s > z + 0.001f) { SetZoom(s, false); return; } }
            else { for (int i = steps.Length - 1; i >= 0; i--) if (steps[i] < z - 0.001f) { SetZoom(steps[i], false); return; } }
        }

        // ---- coordinates
        Point ToImg(Point p)
        {
            int x = (int)Math.Round(p.X / zoom), y = (int)Math.Round(p.Y / zoom);
            return new Point(Math.Max(0, Math.Min(img.Width - 1, x)), Math.Max(0, Math.Min(img.Height - 1, y)));
        }

        static Rectangle Rect(Point a, Point b)
        {
            return Rectangle.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
        }

        Rectangle CanvasBounds(Shape s)
        {
            Rectangle r = Rect(s.A, s.B);
            int pad = (int)(s.Width * 6 + 8);
            r.Inflate(pad, pad);
            return new Rectangle((int)Math.Floor(r.X * zoom), (int)Math.Floor(r.Y * zoom),
                                 (int)Math.Ceiling(r.Width * zoom) + 2, (int)Math.Ceiling(r.Height * zoom) + 2);
        }

        Rectangle VisibleCanvasRect()
        {
            return new Rectangle(-canvas.Left, -canvas.Top, host.ClientSize.Width, host.ClientSize.Height);
        }

        void InvalidateVisible() { canvas.Invalidate(VisibleCanvasRect()); }

        // ---- painting
        void Canvas_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            Rectangle clip = Rectangle.Intersect(e.ClipRectangle, VisibleCanvasRect());
            if (clip.IsEmpty) return;

            Rectangle src = Rectangle.FromLTRB(
                (int)Math.Floor(clip.Left / zoom) - 1, (int)Math.Floor(clip.Top / zoom) - 1,
                (int)Math.Ceiling(clip.Right / zoom) + 1, (int)Math.Ceiling(clip.Bottom / zoom) + 1);
            src.Intersect(new Rectangle(0, 0, img.Width, img.Height));
            var dst = new RectangleF(src.X * zoom, src.Y * zoom, src.Width * zoom, src.Height * zoom);
            g.InterpolationMode = zoom < 1f ? InterpolationMode.HighQualityBilinear : InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            using (var ia = new ImageAttributes())
            {
                ia.SetWrapMode(WrapMode.TileFlipXY);   // avoids blurry seams at tile edges
                g.DrawImage(img, Rectangle.Round(dst), src.X, src.Y, src.Width, src.Height, GraphicsUnit.Pixel, ia);
            }

            g.PixelOffsetMode = PixelOffsetMode.Default;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.ScaleTransform(zoom, zoom);
            foreach (var s in shapes) DrawShape(g, s);
            if (cur != null) DrawShape(g, cur);
        }

        static void DrawShape(Graphics g, Shape s)
        {
            Rectangle r = Rect(s.A, s.B);
            switch (s.Kind)
            {
                case Tool.Box:
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var p = new Pen(s.Color, s.Width)) { p.LineJoin = LineJoin.Round; g.DrawRectangle(p, r); }
                    break;
                case Tool.Highlight:
                    g.SmoothingMode = SmoothingMode.None;
                    using (var b = new SolidBrush(Color.FromArgb(110, 255, 230, 0))) g.FillRectangle(b, r);
                    break;
                case Tool.Redact:
                    g.SmoothingMode = SmoothingMode.None;
                    g.FillRectangle(Brushes.Black, r);
                    break;
                case Tool.Arrow:
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var p = new Pen(s.Color, s.Width))
                    {
                        float cap = Math.Max(3.2f, 14f / s.Width);
                        using (var arrow = new AdjustableArrowCap(cap * 0.8f, cap, true))
                        {
                            p.StartCap = LineCap.Round;
                            p.CustomEndCap = arrow;
                            g.DrawLine(p, s.A, s.B);
                        }
                    }
                    break;
                case Tool.Text:
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var path = new GraphicsPath())
                    {
                        string[] lines = s.Text.Split('\n');
                        float lineH = s.FontSize * 1.3f;
                        for (int i = 0; i < lines.Length; i++)
                            if (lines[i].Length > 0)
                                path.AddString(lines[i], TextFamily, (int)FontStyle.Bold, s.FontSize,
                                               new PointF(s.A.X, s.A.Y + i * lineH), StringFormat.GenericDefault);
                        Color outline = IsLight(s.Color) ? Color.FromArgb(40, 40, 40) : Color.White;
                        using (var op = new Pen(outline, Math.Max(2.5f, s.FontSize / 6f)) { LineJoin = LineJoin.Round })
                            g.DrawPath(op, path);
                        using (var br = new SolidBrush(s.Color)) g.FillPath(br, path);
                    }
                    break;
            }
        }

        static FontFamily textFamily;
        static FontFamily TextFamily
        {
            get
            {
                if (textFamily == null)
                {
                    try { textFamily = new FontFamily("Segoe UI"); }
                    catch { textFamily = FontFamily.GenericSansSerif; }
                }
                return textFamily;
            }
        }

        static bool IsLight(Color c) { return (c.R * 0.299 + c.G * 0.587 + c.B * 0.114) > 160; }

        // ---- mouse
        void Canvas_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (editBox != null) { CommitText(); return; }
            Point p = ToImg(e.Location);
            if (tool == Tool.Text) { StartText(p); return; }
            cur = new Shape { Kind = tool, A = p, B = p, Color = color, Width = penWidth };
        }

        void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (cur == null) return;
            Rectangle before = CanvasBounds(cur);
            cur.B = ToImg(e.Location);
            canvas.Invalidate(Rectangle.Union(before, CanvasBounds(cur)));
        }

        void Canvas_MouseUp(object sender, MouseEventArgs e)
        {
            if (cur == null) return;
            Shape s = cur;
            cur = null;
            Rectangle r = Rect(s.A, s.B);
            bool tiny = s.Kind == Tool.Arrow
                ? Math.Abs(s.A.X - s.B.X) + Math.Abs(s.A.Y - s.B.Y) < 6
                : r.Width < 3 || r.Height < 3;
            if (!tiny) AddShape(s);
            canvas.Invalidate(CanvasBounds(s));
        }

        void AddShape(Shape s)
        {
            shapes.Add(s);
            redo.Clear();
            dirty = true;
            SetStatus("");
            canvas.Invalidate(CanvasBounds(s));
        }

        void Undo()
        {
            CommitText();
            if (shapes.Count == 0) return;
            var s = shapes[shapes.Count - 1];
            shapes.RemoveAt(shapes.Count - 1);
            redo.Add(s);
            dirty = true;
            InvalidateVisible();
        }

        void Redo()
        {
            if (redo.Count == 0) return;
            var s = redo[redo.Count - 1];
            redo.RemoveAt(redo.Count - 1);
            shapes.Add(s);
            dirty = true;
            InvalidateVisible();
        }

        // ---- text entry
        void StartText(Point imgPt)
        {
            editPos = imgPt;
            editBox = new TextBox();
            editBox.Multiline = true;
            editBox.BorderStyle = BorderStyle.FixedSingle;
            editBox.Font = new Font("Segoe UI", Math.Max(6f, fontSize * zoom), FontStyle.Bold, GraphicsUnit.Pixel);
            editBox.ForeColor = color;
            editBox.BackColor = IsLight(color) ? Color.FromArgb(70, 70, 70) : Color.White;
            editBox.Location = new Point((int)(imgPt.X * zoom), (int)(imgPt.Y * zoom));
            SizeEditBox();
            editBox.TextChanged += delegate { SizeEditBox(); };
            editBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && !e.Shift) { e.SuppressKeyPress = true; CommitText(); }
                else if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; editBox.Text = ""; CommitText(); }
            };
            canvas.Controls.Add(editBox);
            editBox.Focus();
        }

        void SizeEditBox()
        {
            if (editBox == null) return;
            string t = editBox.Text.Length == 0 ? "Type here" : editBox.Text;
            Size sz = TextRenderer.MeasureText(t + "WW", editBox.Font);
            editBox.Size = new Size(Math.Max(80, sz.Width + 8), Math.Max(editBox.Font.Height + 8, sz.Height + 10));
        }

        void CommitText()
        {
            if (editBox == null) return;
            var box = editBox;
            editBox = null;
            string t = box.Text.Replace("\r\n", "\n").TrimEnd();
            canvas.Controls.Remove(box);
            box.Dispose();
            host.Focus();
            if (t.Length > 0)
            {
                var s = new Shape { Kind = Tool.Text, A = editPos, Text = t, Color = color, FontSize = fontSize, Width = 1 };
                // Approximate bounds for invalidation
                using (var f = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel))
                {
                    Size sz = TextRenderer.MeasureText(t, f);
                    s.B = new Point(editPos.X + sz.Width + 10, editPos.Y + sz.Height + 10);
                }
                AddShape(s);
            }
        }

        // ---- output
        Bitmap RenderFinal()
        {
            CommitText();
            var b = new Bitmap(img.Width, img.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(b))
            {
                g.DrawImage(img, new Rectangle(0, 0, img.Width, img.Height), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel);
                foreach (var s in shapes) DrawShape(g, s);
            }
            return b;
        }

        void Copy()
        {
            using (var b = RenderFinal())
            {
                if (ImageUtil.CopyToClipboard(b)) { SetStatus("\u2714 Copied to clipboard"); dirty = false; }
                else SetStatus("Clipboard busy - try again");
            }
        }

        void Save()
        {
            try
            {
                if (savedPath == null) savedPath = Settings.NewFilePath(".png");
                using (var b = RenderFinal()) ImageUtil.Save(b, savedPath);
                dirty = false;
                SetStatus("\u2714 Saved " + Path.GetFileName(savedPath));
                statusLbl.ToolTipText = savedPath + "\nClick to show in folder";
            }
            catch (Exception ex) { MessageBox.Show(this, "Couldn't save: " + ex.Message, "SnapScroll"); }
        }

        void SaveAs()
        {
            try
            {
                using (var b = RenderFinal())
                {
                    string p = ImageUtil.SaveAsDialog(b, this);
                    if (p == null) return;
                    savedPath = p;
                    dirty = false;
                    SetStatus("\u2714 Saved " + Path.GetFileName(p));
                    statusLbl.ToolTipText = p + "\nClick to show in folder";
                }
            }
            catch (Exception ex) { MessageBox.Show(this, "Couldn't save: " + ex.Message, "SnapScroll"); }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (editBox != null && editBox.Focused) return base.ProcessCmdKey(ref msg, keyData);
            switch (keyData)
            {
                case Keys.Control | Keys.Z: Undo(); return true;
                case Keys.Control | Keys.Y:
                case Keys.Control | Keys.Shift | Keys.Z: Redo(); return true;
                case Keys.Control | Keys.C: Copy(); return true;
                case Keys.Control | Keys.S: Save(); return true;
                case Keys.Control | Keys.Shift | Keys.S: SaveAs(); return true;
                case Keys.Control | Keys.Oemplus:
                case Keys.Control | Keys.Shift | Keys.Oemplus:
                case Keys.Control | Keys.Add: StepZoom(1); return true;
                case Keys.Control | Keys.OemMinus:
                case Keys.Control | Keys.Subtract: StepZoom(-1); return true;
                case Keys.Control | Keys.D0:
                case Keys.Control | Keys.NumPad0: SetZoom(1f, false); return true;
                case Keys.B: SetTool(Tool.Box); return true;
                case Keys.A: SetTool(Tool.Arrow); return true;
                case Keys.H: SetTool(Tool.Highlight); return true;
                case Keys.T: SetTool(Tool.Text); return true;
                case Keys.R: SetTool(Tool.Redact); return true;
                case Keys.Escape: Close(); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            CommitText();
            if (dirty && e.CloseReason == CloseReason.UserClosing)
            {
                var r = MessageBox.Show(this, "Save this screenshot before closing?", "SnapScroll",
                    MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Cancel) { e.Cancel = true; return; }
                if (r == DialogResult.Yes) { Save(); if (dirty) { e.Cancel = true; return; } }
            }
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            img.Dispose();
            base.OnFormClosed(e);
        }
    }
}
