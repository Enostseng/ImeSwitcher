// =============================================================
//  IME 繁簡切換器  v1.0.0 (C#)
//  1. 切換「微軟注音」的「輸出簡體中文」設定
//  2. 選取文字後按快速鍵，直接把文字轉成繁體或簡體
//  原理：改寫 HKCU\SOFTWARE\Microsoft\IME\15.0\IMETC
//        的 "Enable Simplified Chinese Output"
//  以 Windows 內建 .NET Framework 4.x 的 csc.exe 編譯（C# 5 語法）
//  繁簡轉換詞庫：OpenCC 1.4.2（Apache License 2.0），見 dict\OpenCC-LICENSE.txt
//
//  開發者：Enos Tseng
//  聯絡信箱：enostseng@gmail.com
//  授權：MIT License（見 LICENSE）
//  原始碼：https://github.com/Enostseng/ImeSwitcher
// =============================================================
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("IME 繁簡切換器")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyProduct("IME 繁簡切換器")]
[assembly: System.Reflection.AssemblyCompany("Enos Tseng")]
[assembly: System.Reflection.AssemblyCopyright("Copyright © 2026 Enos Tseng (enostseng@gmail.com)")]
[assembly: System.Reflection.AssemblyDescription("IME 繁簡切換器 - 開發者 Enos Tseng")]

namespace ImeSwitcher
{
    static class Program
    {
        public const string AppName = "IME 繁簡切換器";
        public const string AppVersion = "1.0.0";
        public const string Developer = "Enos Tseng";
        public const string DeveloperEmail = "enostseng@gmail.com";

        [STAThread]
        static void Main(string[] args)
        {
            // 命令列：--status 把目前狀態寫到檔案（測試用，唯讀）
            if (args.Length > 0 && args[0] == "--status")
            {
                string outPath = args.Length > 1 ? args[1] : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "status.txt");
                File.WriteAllText(outPath, ImeRegistry.Describe(), new UTF8Encoding(true));
                return;
            }

            // 命令列：--convert t|s 輸入檔 輸出檔（測試轉換引擎用，UTF-8）
            //   t／s：依設定檔；s2tw、s2twp、tw2s、tw2sp：指定模式
            if (args.Length >= 4 && args[0] == "--convert")
            {
                string mode = args[1];
                if (mode == "t" || mode == "s")
                {
                    ChineseConverter.UseTaiwanPhrases = Settings.Load().UseTaiwanPhrases;
                    mode = ChineseConverter.ModeName(mode == "t");
                }
                string input = File.ReadAllText(args[2], Encoding.UTF8);
                System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                OpenCcEngine engine = OpenCcEngine.Get(mode);
                long loadMs = sw.ElapsedMilliseconds;
                sw.Restart();
                string output = engine.Convert(input);
                File.WriteAllText(args[3], output, new UTF8Encoding(true));
                if (args.Length >= 5)
                    File.WriteAllText(args[4], "mode=" + mode + "\r\nload_ms=" + loadMs + "\r\nconvert_ms=" + sw.ElapsedMilliseconds + "\r\nchars=" + input.Length + "\r\n");
                return;
            }

            bool createdNew;
            using (Mutex mutex = new Mutex(true, "Local\\ImeSwitcher_SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("已經在執行中（請看系統匣）。", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                try { NativeMethods.SetProcessDPIAware(); } catch { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayApp());
            }
        }
    }

    // ---------------------------------------------------------
    //  登錄檔讀寫
    // ---------------------------------------------------------
    static class ImeRegistry
    {
        public const string KeyPath = @"SOFTWARE\Microsoft\IME\15.0\IMETC";
        public const string ValueName = "Enable Simplified Chinese Output";

        /// <summary>0 = 繁體, 1 = 簡體, -1 = 無法辨識。值不存在時以 REG_SZ "0x00000000" 建立。</summary>
        public static int ReadState(out bool created)
        {
            created = false;
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(KeyPath))
            {
                object raw = key.GetValue(ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (raw == null)
                {
                    key.SetValue(ValueName, "0x00000000", RegistryValueKind.String);
                    created = true;
                    return 0;
                }
                return Parse(raw);
            }
        }

        static int Parse(object raw)
        {
            if (raw is int)
            {
                int i = (int)raw;
                if (i == 1) return 1;
                if (i == 0) return 0;
                return -1;
            }
            string s = Convert.ToString(raw).Trim();
            if (s == "0x00000001" || s == "1") return 1;
            if (s == "0x00000000" || s == "0") return 0;
            return -1;
        }

        /// <summary>依原本的型別寫回，不更換型別。</summary>
        public static void WriteState(int state)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(KeyPath))
            {
                RegistryValueKind kind = RegistryValueKind.String;
                if (key.GetValue(ValueName) != null)
                    kind = key.GetValueKind(ValueName);

                if (kind == RegistryValueKind.DWord)
                    key.SetValue(ValueName, state, RegistryValueKind.DWord);
                else
                    key.SetValue(ValueName, state == 1 ? "0x00000001" : "0x00000000", RegistryValueKind.String);
            }
        }

        public static string RawDescription()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(KeyPath))
            {
                if (key == null) return "（機碼不存在）";
                object raw = key.GetValue(ValueName);
                if (raw == null) return "（值不存在）";
                return key.GetValueKind(ValueName) + " \"" + Convert.ToString(raw) + "\"";
            }
        }

        /// <summary>唯讀描述（不會建立任何東西），給 --status 使用。</summary>
        public static string Describe()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("time=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("key=HKCU\\" + KeyPath);
            sb.AppendLine("value=" + ValueName);
            sb.AppendLine("raw=" + RawDescription());
            sb.AppendLine("startup=" + (Startup.IsEnabled() ? "on" : "off"));
            return sb.ToString();
        }
    }

    // ---------------------------------------------------------
    //  開機自動啟動：HKCU\...\Run
    // ---------------------------------------------------------
    static class Startup
    {
        const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "ImeSwitcher";

        public static bool IsEnabled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey))
            {
                return key != null && key.GetValue(RunName) != null;
            }
        }

        public static void SetEnabled(bool on)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (on)
                    key.SetValue(RunName, "\"" + Application.ExecutablePath + "\"", RegistryValueKind.String);
                else if (key.GetValue(RunName) != null)
                    key.DeleteValue(RunName);
            }
        }
    }

    // ---------------------------------------------------------
    //  設定檔：%APPDATA%\ImeSwitcher\settings.ini（key=value）
    // ---------------------------------------------------------
    class Settings
    {
        public bool Notify = true;
        public bool FirstRun = true;
        public string ToggleHotkey = "Alt+Shift+F";
        public string NotifyHotkey = "Ctrl+Shift+Alt+X";
        public string ToTraditionalHotkey = "Alt+Shift+T";
        public string ToSimplifiedHotkey = "Alt+Shift+S";
        public bool UseTaiwanPhrases = false;

        public static string Dir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ImeSwitcher"); }
        }
        public static string FilePath { get { return Path.Combine(Dir, "settings.ini"); } }

        public static Settings Load()
        {
            Settings s = new Settings();
            try
            {
                if (!File.Exists(FilePath)) { s.Save(); return s; }
                foreach (string line in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    string t = line.Trim();
                    if (t.Length == 0 || t.StartsWith(";") || t.StartsWith("#") || t.StartsWith("[")) continue;
                    int eq = t.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = t.Substring(0, eq).Trim().ToLowerInvariant();
                    string v = t.Substring(eq + 1).Trim();
                    if (k == "notify") s.Notify = v != "0";
                    else if (k == "firstrun") s.FirstRun = v != "0";
                    else if (k == "togglehotkey") s.ToggleHotkey = v;
                    else if (k == "notifyhotkey") s.NotifyHotkey = v;
                    else if (k == "totraditionalhotkey") s.ToTraditionalHotkey = v;
                    else if (k == "tosimplifiedhotkey") s.ToSimplifiedHotkey = v;
                    else if (k == "usetaiwanphrases") s.UseTaiwanPhrases = v == "1";
                }
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                string[] lines = new string[]
                {
                    "; IME 繁簡切換器 設定檔（改完請在系統匣選單按「重新載入設定」）",
                    "; 快速鍵格式範例：Alt+Shift+F、Ctrl+Alt+F、Win+Shift+Z",
                    "[General]",
                    "Notify=" + (Notify ? "1" : "0"),
                    "FirstRun=" + (FirstRun ? "1" : "0"),
                    "[Hotkeys]",
                    "ToggleHotkey=" + ToggleHotkey,
                    "NotifyHotkey=" + NotifyHotkey,
                    "; 選取文字後按下，把選取的文字轉成繁體／簡體",
                    "ToTraditionalHotkey=" + ToTraditionalHotkey,
                    "ToSimplifiedHotkey=" + ToSimplifiedHotkey,
                    "[Convert]",
                    "; 1 = 轉換時一併改用台灣用語（软件→軟體）；0 = 只轉字形（软件→軟件）",
                    "UseTaiwanPhrases=" + (UseTaiwanPhrases ? "1" : "0")
                };
                File.WriteAllLines(FilePath, lines, new UTF8Encoding(true));
            }
            catch { }
        }
    }

    // ---------------------------------------------------------
    //  快速鍵字串解析："Ctrl+Shift+Alt+X" -> modifiers + vk
    // ---------------------------------------------------------
    static class HotkeyParser
    {
        public const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_WIN = 0x8, MOD_NOREPEAT = 0x4000;

        public static bool TryParse(string text, out uint mods, out uint vk)
        {
            mods = 0; vk = 0;
            if (string.IsNullOrEmpty(text)) return false;
            string[] parts = text.Split('+');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (p.Length == 0) return false;
                string u = p.ToUpperInvariant();
                bool last = i == parts.Length - 1;
                if (!last)
                {
                    if (u == "CTRL" || u == "CONTROL") mods |= MOD_CONTROL;
                    else if (u == "ALT") mods |= MOD_ALT;
                    else if (u == "SHIFT") mods |= MOD_SHIFT;
                    else if (u == "WIN") mods |= MOD_WIN;
                    else return false;
                }
                else
                {
                    Keys k;
                    if (p.Length == 1 && char.IsDigit(p[0])) p = "D" + p;
                    if (!Enum.TryParse<Keys>(p, true, out k)) return false;
                    if ((k & Keys.Modifiers) != 0 || k == Keys.None) return false;
                    vk = (uint)k;
                }
            }
            return mods != 0 && vk != 0;
        }
    }

    // ---------------------------------------------------------
    //  主程式：系統匣 + 快速鍵
    // ---------------------------------------------------------
    class TrayApp : ApplicationContext
    {
        const int HK_TOGGLE = 1, HK_NOTIFY = 2, HK_TO_TRAD = 3, HK_TO_SIMP = 4;

        Settings settings;
        NotifyIcon tray;
        HotkeyWindow hkWindow;
        System.Windows.Forms.Timer syncTimer;
        ToolStripMenuItem miState, miToggle, miNotify, miStartup, miPhrases;
        OsdForm osd;
        int state = -2;
        Icon currentIcon;
        List<string> hotkeyErrors = new List<string>();

        public TrayApp()
        {
            settings = Settings.Load();
            ChineseConverter.UseTaiwanPhrases = settings.UseTaiwanPhrases;
            osd = new OsdForm();
            IntPtr forceHandle = osd.Handle; // 讓背景執行緒可以 BeginInvoke
            Log.Write("啟動 v" + Program.AppVersion);
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                    OpenCcEngine.Get(ChineseConverter.ModeName(true));
                    OpenCcEngine.Get(ChineseConverter.ModeName(false));
                    Log.Write("詞庫預先載入完成，" + sw.ElapsedMilliseconds + " ms");
                }
                catch (Exception ex) { Log.Write("詞庫載入失敗：" + ex.Message); }
            });

            hkWindow = new HotkeyWindow();
            hkWindow.HotkeyPressed += OnHotkey;

            BuildTray();
            RegisterHotkeys();
            SyncState(null, EventArgs.Empty);

            syncTimer = new System.Windows.Forms.Timer();
            syncTimer.Interval = 3000;
            syncTimer.Tick += SyncState;
            syncTimer.Start();

            if (settings.FirstRun)
            {
                tray.ShowBalloonTip(4000, Program.AppName,
                    "已在系統匣執行。\n" + settings.ToggleHotkey + "：切換繁／簡", ToolTipIcon.Info);
                settings.FirstRun = false;
                settings.Save();
            }
            if (hotkeyErrors.Count > 0)
            {
                MessageBox.Show(string.Join("\n", hotkeyErrors.ToArray()), Program.AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void BuildTray()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            miState = new ToolStripMenuItem("目前：讀取中…");
            miState.Enabled = false;
            miToggle = new ToolStripMenuItem("切換繁／簡", null, delegate { Toggle(); });
            miToggle.Font = new Font(miToggle.Font, FontStyle.Bold);
            miNotify = new ToolStripMenuItem("顯示切換通知", null, delegate { ToggleNotify(); });
            miStartup = new ToolStripMenuItem("開機自動啟動", null, delegate { ToggleStartup(); });
            miPhrases = new ToolStripMenuItem("選取轉換時改用台灣用語", null, delegate { TogglePhrases(); });

            menu.Items.Add(miState);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miToggle);
            menu.Items.Add(miNotify);
            menu.Items.Add(miStartup);
            menu.Items.Add(miPhrases);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("開啟設定檔", null, delegate { OpenSettings(); }));
            menu.Items.Add(new ToolStripMenuItem("重新載入設定", null, delegate { ReloadSettings(); }));
            menu.Items.Add(new ToolStripMenuItem("關於", null, delegate { ShowAbout(); }));
            menu.Items.Add(new ToolStripMenuItem("結束", null, delegate { ExitApp(); }));

            tray = new NotifyIcon();
            tray.ContextMenuStrip = menu;
            tray.MouseClick += delegate(object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) Toggle();
            };
            UpdateTray();
            tray.Visible = true;
        }

        void RegisterHotkeys()
        {
            hotkeyErrors.Clear();
            NativeMethods.UnregisterHotKey(hkWindow.Handle, HK_TOGGLE);
            NativeMethods.UnregisterHotKey(hkWindow.Handle, HK_NOTIFY);
            NativeMethods.UnregisterHotKey(hkWindow.Handle, HK_TO_TRAD);
            NativeMethods.UnregisterHotKey(hkWindow.Handle, HK_TO_SIMP);

            if (!TryRegister(HK_TOGGLE, settings.ToggleHotkey, "切換"))
            {
                settings.ToggleHotkey = "Alt+Shift+F";
                TryRegister(HK_TOGGLE, settings.ToggleHotkey, "切換");
            }
            if (!TryRegister(HK_NOTIFY, settings.NotifyHotkey, "通知"))
            {
                settings.NotifyHotkey = "Ctrl+Shift+Alt+X";
                TryRegister(HK_NOTIFY, settings.NotifyHotkey, "通知");
            }
            if (!TryRegister(HK_TO_TRAD, settings.ToTraditionalHotkey, "選取轉繁體"))
            {
                settings.ToTraditionalHotkey = "Alt+Shift+T";
                TryRegister(HK_TO_TRAD, settings.ToTraditionalHotkey, "選取轉繁體");
            }
            if (!TryRegister(HK_TO_SIMP, settings.ToSimplifiedHotkey, "選取轉簡體"))
            {
                settings.ToSimplifiedHotkey = "Alt+Shift+S";
                TryRegister(HK_TO_SIMP, settings.ToSimplifiedHotkey, "選取轉簡體");
            }
            miToggle.ShortcutKeyDisplayString = settings.ToggleHotkey;
        }

        bool TryRegister(int id, string text, string label)
        {
            uint mods, vk;
            if (!HotkeyParser.TryParse(text, out mods, out vk))
            {
                hotkeyErrors.Add(label + "快速鍵格式無效：「" + text + "」，改用預設值。");
                return false;
            }
            if (!NativeMethods.RegisterHotKey(hkWindow.Handle, id, mods | HotkeyParser.MOD_NOREPEAT, vk))
            {
                hotkeyErrors.Add(label + "快速鍵「" + text + "」無法註冊（可能被其他程式占用）。");
                return false;
            }
            return true;
        }

        void OnHotkey(int id)
        {
            if (id == HK_TOGGLE) Toggle();
            else if (id == HK_NOTIFY) ToggleNotify();
            else if (id == HK_TO_TRAD) ConvertSelection(true);
            else if (id == HK_TO_SIMP) ConvertSelection(false);
        }

        void Toggle()
        {
            try
            {
                bool created;
                int cur = ImeRegistry.ReadState(out created);
                if (cur == -1)
                {
                    MessageBox.Show("登錄值內容無法辨識，為避免改壞設定，本次不切換。\n\n位置：HKCU\\" + ImeRegistry.KeyPath
                        + "\n名稱：" + ImeRegistry.ValueName + "\n內容：" + ImeRegistry.RawDescription(),
                        Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                int next = cur == 1 ? 0 : 1;
                ImeRegistry.WriteState(next);
                RefreshIme();
                state = next;
                UpdateTray();
                if (settings.Notify) osd.ShowText(next == 1 ? "簡體" : "繁體");
            }
            catch (Exception ex)
            {
                MessageBox.Show("切換失敗：\n" + ex.Message, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // 讓輸入法重新讀取設定：焦點切到工作列再切回原視窗（沿用原版做法）
        static void RefreshIme()
        {
            IntPtr fg = NativeMethods.GetForegroundWindow();
            IntPtr taskbar = NativeMethods.FindWindow("Shell_TrayWnd", null);
            if (taskbar != IntPtr.Zero)
            {
                NativeMethods.SetForegroundWindow(taskbar);
                Thread.Sleep(50);
            }
            if (fg != IntPtr.Zero) NativeMethods.SetForegroundWindow(fg);
        }

        volatile bool converting;
        int convertJob;

        // 選取文字轉換：在獨立的 STA 執行緒執行，剪貼簿卡住也不會讓主程式當掉
        void ConvertSelection(bool toTraditional)
        {
            if (converting) { Log.Write("略過：上一次轉換尚未結束"); return; }
            converting = true;
            int job = ++convertJob;
            Log.Write("#" + job + " 開始：" + (toTraditional ? "轉繁" : "轉簡"));

            Thread worker = new Thread(delegate()
            {
                string result;
                try { result = SelectionConverter.Run(toTraditional); }
                catch (Exception ex) { result = "轉換失敗：" + ex.Message; Log.Write("#" + job + " 例外：" + ex); }
                Log.Write("#" + job + " 結束：" + result);
                if (job == convertJob)
                {
                    converting = false;
                    try { osd.BeginInvoke((MethodInvoker)delegate { osd.ShowText(result); }); } catch { }
                }
            });
            worker.IsBackground = true;
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();

            // 看門狗：5 秒還沒結束就放棄這次，允許下一次轉換
            System.Windows.Forms.Timer watchdog = new System.Windows.Forms.Timer();
            watchdog.Interval = 5000;
            watchdog.Tick += delegate
            {
                watchdog.Stop();
                watchdog.Dispose();
                if (converting && job == convertJob)
                {
                    Log.Write("#" + job + " 逾時（5 秒），放棄");
                    convertJob++;
                    converting = false;
                    osd.ShowText("轉換逾時，請再試一次");
                }
            };
            watchdog.Start();
        }

        void TogglePhrases()
        {
            settings.UseTaiwanPhrases = !settings.UseTaiwanPhrases;
            ChineseConverter.UseTaiwanPhrases = settings.UseTaiwanPhrases;
            settings.Save();
            UpdateTray();
            osd.ShowText(settings.UseTaiwanPhrases ? "轉換時改用台灣用語" : "轉換時只轉字形");
            Log.Write("用語轉換：" + (settings.UseTaiwanPhrases ? "開" : "關"));
        }

        void ToggleNotify()
        {
            settings.Notify = !settings.Notify;
            settings.Save();
            UpdateTray();
            osd.ShowText(settings.Notify ? "通知已開啟" : "通知已關閉");
        }

        void ToggleStartup()
        {
            try
            {
                bool on = !Startup.IsEnabled();
                Startup.SetEnabled(on);
                osd.ShowText(on ? "已設定開機啟動" : "已取消開機啟動");
            }
            catch (Exception ex)
            {
                MessageBox.Show("設定開機啟動失敗：\n" + ex.Message, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            UpdateTray();
        }

        void SyncState(object sender, EventArgs e)
        {
            try
            {
                bool created;
                int s = ImeRegistry.ReadState(out created);
                if (s != state)
                {
                    state = s;
                    UpdateTray();
                }
            }
            catch { }
        }

        void UpdateTray()
        {
            string label = state == 1 ? "簡體" : state == 0 ? "繁體" : "無法辨識";
            miState.Text = "目前：" + label;
            miNotify.Checked = settings.Notify;
            miStartup.Checked = Startup.IsEnabled();
            miPhrases.Checked = settings.UseTaiwanPhrases;

            string tip = Program.AppName + "｜目前輸出：" + label;
            tray.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;

            Icon old = currentIcon;
            currentIcon = TrayIconFactory.Create(state == 1 ? "簡" : state == 0 ? "繁" : "?", state == 1);
            tray.Icon = currentIcon;
            if (old != null) TrayIconFactory.Destroy(old);
        }

        void OpenSettings()
        {
            settings.Save();
            try { System.Diagnostics.Process.Start("notepad.exe", "\"" + Settings.FilePath + "\""); } catch { }
        }

        void ReloadSettings()
        {
            settings = Settings.Load();
            ChineseConverter.UseTaiwanPhrases = settings.UseTaiwanPhrases;
            RegisterHotkeys();
            UpdateTray();
            if (hotkeyErrors.Count > 0)
                MessageBox.Show(string.Join("\n", hotkeyErrors.ToArray()), Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else
                osd.ShowText("設定已重新載入");
        }

        void ShowAbout()
        {
            MessageBox.Show(Program.AppName + " v" + Program.AppVersion
                + "\n\n切換繁／簡：" + settings.ToggleHotkey
                + "\n開關通知：" + settings.NotifyHotkey
                + "\n選取文字轉繁體：" + settings.ToTraditionalHotkey
                + "\n選取文字轉簡體：" + settings.ToSimplifiedHotkey
                + "\n單擊系統匣圖示：切換"
                + "\n選取轉換模式：" + (settings.UseTaiwanPhrases ? "字形＋台灣用語" : "只轉字形")
                + "\n\n設定檔：" + Settings.FilePath
                + "\n\n若切換後沒反應：請先切到其他輸入法再切回注音。"
                + "\n\n開發者：" + Program.Developer
                + "\n聯絡信箱：" + Program.DeveloperEmail
                + "\n授權：MIT License"
                + "\n原始碼：github.com/Enostseng/ImeSwitcher"
                + "\n\n繁簡轉換詞庫：OpenCC 1.4.2（Apache License 2.0）",
                Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        void ExitApp()
        {
            syncTimer.Stop();
            NativeMethods.UnregisterHotKey(hkWindow.Handle, HK_TOGGLE);
            NativeMethods.UnregisterHotKey(hkWindow.Handle, HK_NOTIFY);
            NativeMethods.UnregisterHotKey(hkWindow.Handle, HK_TO_TRAD);
            NativeMethods.UnregisterHotKey(hkWindow.Handle, HK_TO_SIMP);
            tray.Visible = false;
            tray.Dispose();
            hkWindow.DestroyHandle();
            ExitThread();
        }
    }

    // ---------------------------------------------------------
    //  隱藏視窗：接收 WM_HOTKEY
    // ---------------------------------------------------------
    class HotkeyWindow : NativeWindow
    {
        const int WM_HOTKEY = 0x0312;
        public event Action<int> HotkeyPressed;

        public HotkeyWindow()
        {
            CreateHandle(new CreateParams());
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && HotkeyPressed != null)
                HotkeyPressed(m.WParam.ToInt32());
            base.WndProc(ref m);
        }
    }

    // ---------------------------------------------------------
    //  系統匣圖示：直接畫出「繁」或「簡」
    // ---------------------------------------------------------
    static class TrayIconFactory
    {
        public static Icon Create(string text, bool simplified)
        {
            int size = SystemInformation.SmallIconSize.Width;
            if (size < 16) size = 16;
            using (Bitmap bmp = new Bitmap(size, size))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.Transparent);
                Color bg = simplified ? Color.FromArgb(200, 60, 50) : Color.FromArgb(40, 110, 200);
                using (SolidBrush b = new SolidBrush(bg))
                using (GraphicsPath path = RoundRect(new Rectangle(0, 0, size - 1, size - 1), size / 4))
                {
                    g.FillPath(b, path);
                }
                using (Font f = new Font("Microsoft JhengHei", size * 0.62f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString(text, f, Brushes.White, new RectangleF(0, 0, size, size + 1), sf);
                }
                IntPtr h = bmp.GetHicon();
                return Icon.FromHandle(h);
            }
        }

        public static void Destroy(Icon icon)
        {
            try { NativeMethods.DestroyIcon(icon.Handle); } catch { }
            try { icon.Dispose(); } catch { }
        }

        static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            GraphicsPath p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    // ---------------------------------------------------------
    //  畫面提示（OSD）：顯示在滑鼠所在螢幕上方，滑鼠可穿透，不搶焦點
    // ---------------------------------------------------------
    class OsdForm : Form
    {
        System.Windows.Forms.Timer holdTimer = new System.Windows.Forms.Timer();
        System.Windows.Forms.Timer fadeTimer = new System.Windows.Forms.Timer();
        Label label = new Label();

        public OsdForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(34, 34, 34);
            Padding = new Padding(24, 10, 24, 10);

            label.AutoSize = true;
            label.ForeColor = Color.White;
            label.Font = new Font("Microsoft JhengHei", 20f, FontStyle.Bold);
            label.Location = new Point(24, 10);
            Controls.Add(label);

            holdTimer.Interval = 600;
            holdTimer.Tick += delegate { holdTimer.Stop(); fadeTimer.Start(); };
            fadeTimer.Interval = 20;
            fadeTimer.Tick += delegate
            {
                double o = Opacity - 0.06;
                if (o <= 0) { fadeTimer.Stop(); Hide(); Opacity = 1; }
                else Opacity = o;
            };
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_EX_TOOLWINDOW = 0x80, WS_EX_TRANSPARENT = 0x20, WS_EX_NOACTIVATE = 0x08000000, WS_EX_TOPMOST = 0x8;
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOPMOST;
                return cp;
            }
        }

        public void ShowText(string text)
        {
            holdTimer.Stop();
            fadeTimer.Stop();
            label.Text = text;
            Size sz = label.PreferredSize;
            ClientSize = new Size(sz.Width + 48, sz.Height + 20);

            Rectangle wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            Location = new Point(wa.Left + (wa.Width - Width) / 2, wa.Top + 100);

            IntPtr rgn = NativeMethods.CreateRoundRectRgn(0, 0, Width + 1, Height + 1, 20, 20);
            NativeMethods.SetWindowRgn(Handle, rgn, true);

            Opacity = 1;
            if (!Visible) Show();
            NativeMethods.SetWindowPos(Handle, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
            holdTimer.Start();
        }
    }

    // ---------------------------------------------------------
    //  剪貼簿備份：只處理常見格式，避免讀取特殊格式時卡住
    // ---------------------------------------------------------
    class ClipboardBackup
    {
        string text;
        Image image;
        System.Collections.Specialized.StringCollection files;
        bool empty = true;

        public static ClipboardBackup Take()
        {
            ClipboardBackup b = new ClipboardBackup();
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    if (Clipboard.ContainsText(TextDataFormat.UnicodeText)) { b.text = Clipboard.GetText(TextDataFormat.UnicodeText); b.empty = false; }
                    else if (Clipboard.ContainsImage()) { b.image = Clipboard.GetImage(); b.empty = false; }
                    else if (Clipboard.ContainsFileDropList()) { b.files = Clipboard.GetFileDropList(); b.empty = false; }
                    return b;
                }
                catch { Thread.Sleep(50); }
            }
            return b;
        }

        public string Describe()
        {
            if (text != null) return "文字 " + text.Length + " 字";
            if (image != null) return "圖片";
            if (files != null) return "檔案 " + files.Count + " 個";
            return "空的或其他格式（不還原）";
        }

        public void Restore()
        {
            Exception last = null;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    if (text != null) Clipboard.SetText(text, TextDataFormat.UnicodeText);
                    else if (image != null) Clipboard.SetImage(image);
                    else if (files != null) Clipboard.SetFileDropList(files);
                    else if (empty) Clipboard.Clear();
                    return;
                }
                catch (Exception ex) { last = ex; Thread.Sleep(60); }
            }
            if (last != null) throw last;
        }
    }

    // ---------------------------------------------------------
    //  紀錄檔：%APPDATA%\ImeSwitcher\log.txt（超過 200KB 自動清空）
    // ---------------------------------------------------------
    static class Log
    {
        static readonly object gate = new object();

        public static void Write(string msg)
        {
            try
            {
                lock (gate)
                {
                    string path = Path.Combine(Settings.Dir, "log.txt");
                    Directory.CreateDirectory(Settings.Dir);
                    if (File.Exists(path) && new FileInfo(path).Length > 200 * 1024) File.Delete(path);
                    File.AppendAllText(path, DateTime.Now.ToString("HH:mm:ss.fff") + " [T" + Thread.CurrentThread.ManagedThreadId + "] " + msg + Environment.NewLine, new UTF8Encoding(false));
                }
            }
            catch { }
        }
    }

    // ---------------------------------------------------------
    //  繁簡轉換：依設定選擇 OpenCC 模式
    //  預設 s2tw／tw2s（只轉字形），開啟用語轉換時 s2twp／tw2sp
    // ---------------------------------------------------------
    static class ChineseConverter
    {
        public static volatile bool UseTaiwanPhrases;

        public static string ModeName(bool toTraditional)
        {
            if (toTraditional) return UseTaiwanPhrases ? "s2twp" : "s2tw";
            return UseTaiwanPhrases ? "tw2sp" : "tw2s";
        }

        public static string Convert(string text, bool toTraditional)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return OpenCcEngine.Get(ModeName(toTraditional)).Convert(text);
        }
    }

    // ---------------------------------------------------------
    //  OpenCC 轉換引擎（C# 實作，對應 OpenCC 1.4.2 的設定檔）
    //  1. 以分詞詞庫做最長匹配分詞（未匹配的連續字元合併成一段）
    //  2. 每一段依序經過轉換鏈的每一步，每一步都用該步詞庫做最長匹配
    //  詞庫以內嵌資源方式打包在 exe 內（dict\*.txt）
    // ---------------------------------------------------------
    class OpenCcEngine
    {
        static readonly Dictionary<string, OpenCcEngine> engines = new Dictionary<string, OpenCcEngine>();
        static readonly Dictionary<string, DictGroup> dictCache = new Dictionary<string, DictGroup>();
        static readonly object gate = new object();

        DictGroup segmentation;
        DictGroup[] chain;

        public static OpenCcEngine Get(string mode)
        {
            lock (gate)
            {
                OpenCcEngine e;
                if (engines.TryGetValue(mode, out e)) return e;
                e = Build(mode);
                engines[mode] = e;
                return e;
            }
        }

        static OpenCcEngine Build(string mode)
        {
            OpenCcEngine e = new OpenCcEngine();
            if (mode == "s2tw" || mode == "s2twp")
            {
                e.segmentation = Group("STPhrases", "STPhrases_GeneratedFromRegionalPhrases");
                DictGroup step1 = Group("STPhrases", "STPhrases_GeneratedFromRegionalPhrases", "STCharacters");
                DictGroup step2 = mode == "s2twp"
                    ? Group("TWPhrases", "TWVariantsPhrases", "TWVariants")
                    : Group("TWVariantsPhrases", "TWVariants");
                e.chain = new DictGroup[] { step1, step2 };
            }
            else if (mode == "tw2s" || mode == "tw2sp")
            {
                e.segmentation = Group("TSPhrases");
                DictGroup step1 = mode == "tw2sp"
                    ? Group("TWPhrasesRev", "TWVariantsRevPhrases", "TWVariantsRev")
                    : Group("TWVariantsRevPhrases", "TWVariantsRev");
                // 官方預設不含 TSCharactersExt（易顯示成方塊的罕用字），這裡也不含
                DictGroup step2 = Group("TSPhrases", "TSCharacters");
                e.chain = new DictGroup[] { step1, step2 };
            }
            else throw new ArgumentException("不支援的轉換模式：" + mode);
            return e;
        }

        static DictGroup Group(params string[] names)
        {
            string key = string.Join("+", names);
            DictGroup g;
            if (dictCache.TryGetValue(key, out g)) return g;
            g = new DictGroup();
            foreach (string n in names) g.AddResource(n + ".txt"); // 前面的詞庫優先
            dictCache[key] = g;
            return g;
        }

        public string Convert(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            StringBuilder sb = new StringBuilder(text.Length + 16);
            foreach (string seg in Segment(text))
            {
                string cur = seg;
                foreach (DictGroup step in chain) cur = step.ConvertMaxMatch(cur);
                sb.Append(cur);
            }
            return sb.ToString();
        }

        List<string> Segment(string text)
        {
            List<string> segs = new List<string>();
            int segStart = 0, pos = 0;
            while (pos < text.Length)
            {
                int len = segmentation.MatchPrefixLength(text, pos);
                if (len > 0)
                {
                    if (segStart < pos) segs.Add(text.Substring(segStart, pos - segStart));
                    segs.Add(text.Substring(pos, len));
                    pos += len;
                    segStart = pos;
                }
                else pos += DictGroup.CharLen(text, pos);
            }
            if (segStart < text.Length) segs.Add(text.Substring(segStart));
            return segs;
        }
    }

    class DictGroup
    {
        readonly Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.Ordinal);
        int maxLen;

        public void AddResource(string name)
        {
            Stream s = typeof(DictGroup).Assembly.GetManifestResourceStream(name);
            if (s == null) throw new FileNotFoundException("找不到內嵌詞庫：" + name);
            using (StreamReader r = new StreamReader(s, Encoding.UTF8))
            {
                string line;
                while ((line = r.ReadLine()) != null)
                {
                    if (line.Length == 0 || line[0] == '#') continue;
                    int tab = line.IndexOf('\t');
                    if (tab <= 0) continue;
                    string key = line.Substring(0, tab);
                    string rest = line.Substring(tab + 1).Trim();
                    int sp = rest.IndexOf(' ');
                    string value = sp < 0 ? rest : rest.Substring(0, sp); // 多個候選取第一個
                    if (value.Length == 0 || map.ContainsKey(key)) continue;
                    map[key] = value;
                    if (key.Length > maxLen) maxLen = key.Length;
                }
            }
        }

        public static int CharLen(string text, int pos)
        {
            if (char.IsHighSurrogate(text[pos]) && pos + 1 < text.Length && char.IsLowSurrogate(text[pos + 1])) return 2;
            return 1;
        }

        // 回傳從 pos 開始、詞庫中最長的匹配長度；沒有匹配回傳 0
        public int MatchPrefixLength(string text, int pos)
        {
            int limit = Math.Min(maxLen, text.Length - pos);
            for (int len = limit; len > 0; len--)
            {
                if (len < text.Length - pos && char.IsLowSurrogate(text[pos + len]) && char.IsHighSurrogate(text[pos + len - 1])) continue; // 不切開代理對
                if (map.ContainsKey(text.Substring(pos, len))) return len;
            }
            return 0;
        }

        public string ConvertMaxMatch(string text)
        {
            StringBuilder sb = new StringBuilder(text.Length);
            int pos = 0;
            while (pos < text.Length)
            {
                int len = MatchPrefixLength(text, pos);
                if (len > 0)
                {
                    sb.Append(map[text.Substring(pos, len)]);
                    pos += len;
                }
                else
                {
                    int cl = CharLen(text, pos);
                    sb.Append(text, pos, cl);
                    pos += cl;
                }
            }
            return sb.ToString();
        }
    }

    // ---------------------------------------------------------
    //  選取文字轉換流程（含剪貼簿備份與還原）
    // ---------------------------------------------------------
    static class SelectionConverter
    {
        const ushort VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_LWIN = 0x5B, VK_RWIN = 0x5C;
        const ushort VK_C = 0x43, VK_V = 0x56;

        public static string Run(bool toTraditional)
        {
            string label = toTraditional ? "繁體" : "簡體";

            // 1. 等使用者放開快速鍵的修飾鍵，避免 Ctrl+C 變成 Ctrl+Alt+Shift+C
            if (!WaitModifiersReleased(1500))
                return "請放開快速鍵後再試";
            Log.Write("  修飾鍵已放開");

            // 2. 備份剪貼簿（只備份文字、圖片、檔案清單）
            ClipboardBackup backup = ClipboardBackup.Take();
            Log.Write("  剪貼簿已備份：" + backup.Describe());

            // 3. 複製選取的文字
            uint seq = NativeMethods.GetClipboardSequenceNumber();
            SendCtrlKey(VK_C);
            if (!WaitClipboardChanged(seq, 800))
            {
                Log.Write("  Ctrl+C 後剪貼簿沒有變化");
                return "沒有選取文字";
            }
            Log.Write("  Ctrl+C 完成");

            string text = null;
            for (int i = 0; i < 5 && text == null; i++)
            {
                try { if (Clipboard.ContainsText(TextDataFormat.UnicodeText)) text = Clipboard.GetText(TextDataFormat.UnicodeText); else break; }
                catch { Thread.Sleep(50); }
            }
            if (string.IsNullOrEmpty(text))
            {
                RestoreClipboard(backup);
                return "沒有選取文字";
            }

            // 4. 轉換
            string converted = ChineseConverter.Convert(text, toTraditional);
            if (converted == text)
            {
                RestoreClipboard(backup);
                return "已經是" + label;
            }
            int changed = 0;
            for (int i = 0; i < text.Length && i < converted.Length; i++)
                if (text[i] != converted[i]) changed++;

            Log.Write("  轉換完成，" + text.Length + " 字，變更 " + changed + " 字");

            // 5. 貼回
            if (!TrySetText(converted))
            {
                RestoreClipboard(backup);
                return "無法寫入剪貼簿";
            }
            SendCtrlKey(VK_V);
            Log.Write("  Ctrl+V 已送出");

            // 6. 等目標程式讀完剪貼簿再還原
            Thread.Sleep(400);
            RestoreClipboard(backup);
            Log.Write("  剪貼簿已還原");
            return "已轉成" + label + (ChineseConverter.UseTaiwanPhrases ? "＋用語" : "") + "（" + changed + " 字）";
        }

        static bool WaitModifiersReleased(int timeoutMs)
        {
            ushort[] keys = { VK_SHIFT, VK_CONTROL, VK_MENU, VK_LWIN, VK_RWIN };
            int waited = 0;
            while (waited < timeoutMs)
            {
                bool any = false;
                foreach (ushort k in keys)
                    if ((NativeMethods.GetAsyncKeyState(k) & 0x8000) != 0) { any = true; break; }
                if (!any) return true;
                Thread.Sleep(15);
                waited += 15;
            }
            return false;
        }

        static bool WaitClipboardChanged(uint seq, int timeoutMs)
        {
            int waited = 0;
            while (waited < timeoutMs)
            {
                if (NativeMethods.GetClipboardSequenceNumber() != seq) { Thread.Sleep(30); return true; }
                Thread.Sleep(20);
                waited += 20;
            }
            return false;
        }

        static void SendCtrlKey(ushort vk)
        {
            NativeMethods.INPUT[] inputs = new NativeMethods.INPUT[4];
            inputs[0] = KeyInput(VK_CONTROL, false);
            inputs[1] = KeyInput(vk, false);
            inputs[2] = KeyInput(vk, true);
            inputs[3] = KeyInput(VK_CONTROL, true);
            NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
        }

        static NativeMethods.INPUT KeyInput(ushort vk, bool up)
        {
            NativeMethods.INPUT input = new NativeMethods.INPUT();
            input.type = 1; // INPUT_KEYBOARD
            input.u.ki.wVk = vk;
            input.u.ki.dwFlags = up ? 0x0002u : 0u; // KEYEVENTF_KEYUP
            return input;
        }

        static void RestoreClipboard(ClipboardBackup backup)
        {
            try { backup.Restore(); }
            catch (Exception ex) { Log.Write("  還原剪貼簿失敗：" + ex.Message); }
        }

        static bool TrySetText(string text)
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try { Clipboard.SetText(text, TextDataFormat.UnicodeText); return true; }
                catch { Thread.Sleep(60); }
            }
            return false;
        }
    }

    static class NativeMethods
    {
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")]
        public static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);
        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr handle);
        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")]
        public static extern uint GetClipboardSequenceNumber();
        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT { public uint type; public InputUnion u; }
        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);
    }
}
