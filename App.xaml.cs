using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows;
using Forms = System.Windows.Forms;

namespace TangerineWin
{
    public partial class App : Application
    {
        private Forms.NotifyIcon? _trayIcon;
        private MainWindow? _mainWindow;
        public static ShelfWindow? ShelfWin { get; private set; }
        public static AppSettings Settings { get; private set; } = new();

        private static Icon CreateTangerineTrayIcon()
        {
            using var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using var orangeBrush = new SolidBrush(Color.FromArgb(255, 255, 98, 0));
                using var whitePen = new Pen(Color.White, 2.5f);
                g.FillEllipse(orangeBrush, 3, 3, 26, 26);
                g.DrawEllipse(whitePen, 3, 3, 26, 26);
                using var innerBrush = new SolidBrush(Color.FromArgb(255, 24, 24, 30));
                g.FillEllipse(innerBrush, 11, 11, 10, 10);
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                try { File.AppendAllText(logPath, $"[{DateTime.Now}] Crash: {args.ExceptionObject}\r\n"); } catch { }
            };
            DispatcherUnhandledException += (s, args) =>
            {
                try { File.AppendAllText(logPath, $"[{DateTime.Now}] Dispatcher Crash: {args.Exception}\r\n"); } catch { }
                args.Handled = true;
            };

            try
            {
                int currentPid = Environment.ProcessId;
                foreach (var p in Process.GetProcessesByName("TangerineGlass"))
                {
                    if (p.Id != currentPid)
                    {
                        try { p.Kill(); p.WaitForExit(800); } catch { }
                    }
                }
            }
            catch { }

            base.OnStartup(e);

            try
            {
                Settings = AppSettings.Load();
                NativeHardwareMonitor.SetTriggerKey(Settings.TriggerVkCode, Settings.TriggerName);

                ShelfWin = new ShelfWindow();
                _mainWindow = new MainWindow();
                _mainWindow.ApplyTheme(Settings.ThemeName);
                _mainWindow.Show();
                _mainWindow.Hide();

                _trayIcon = new Forms.NotifyIcon
                {
                    Icon = CreateTangerineTrayIcon(),
                    Visible = true,
                    Text = "Tangerine Glass (Drag File + Hold Shift or X)"
                };

                BuildTrayMenu();

                _trayIcon.MouseClick += (_, args) =>
                {
                    if (args.Button == Forms.MouseButtons.Left)
                    {
                        var pt = Forms.Cursor.Position;
                        _mainWindow.ShowAtCursor(pt.X, pt.Y - 120);
                    }
                };

                NativeHardwareMonitor.OnDragTriggered += (x, y) => _mainWindow.ShowAtCursor(x, y);
                NativeHardwareMonitor.OnCursorMoved += (x, y) => _mainWindow.OnGlobalCursorMoved(x, y);
                NativeHardwareMonitor.OnLeftMouseReleased += () => _mainWindow.OnGlobalLeftMouseUp();

                NativeHardwareMonitor.Start();

                var curPt = Forms.Cursor.Position;
                _mainWindow.ShowAtCursor(curPt.X, curPt.Y);
            }
            catch (Exception ex)
            {
                try { File.AppendAllText(logPath, $"[{DateTime.Now}] Startup Exception: {ex}\r\n"); } catch { }
                MessageBox.Show($"Startup Error: {ex.Message}", "Tangerine Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private void BuildTrayMenu()
        {
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("Show Radial Wheel (Alt + Space)", null, (_, _) =>
            {
                var pt = Forms.Cursor.Position;
                _mainWindow?.ShowAtCursor(pt.X, pt.Y - 100);
            });

            menu.Items.Add("Open Hold Shelf (Dropover)", null, (_, _) =>
            {
                if (ShelfWin != null)
                {
                    ShelfWin.Show();
                    ShelfWin.Activate();
                }
            });

            menu.Items.Add("-");

            var themeMenu = new Forms.ToolStripMenuItem("Wheel Theme Preset");
            void AddThemeOption(string label, string themeKey)
            {
                var item = new Forms.ToolStripMenuItem(label)
                {
                    Checked = (Settings.ThemeName == themeKey)
                };
                item.Click += (_, _) =>
                {
                    Settings.ThemeName = themeKey;
                    Settings.Save();
                    _mainWindow?.ApplyTheme(themeKey);
                    BuildTrayMenu();
                };
                themeMenu.DropDownItems.Add(item);
            }

            AddThemeOption("Apple White Glass (Default)", "AppleWhite");
            AddThemeOption("Dark Obsidian", "DarkObsidian");
            AddThemeOption("Cyberpunk Orange", "CyberpunkOrange");
            menu.Items.Add(themeMenu);

            var triggerMenu = new Forms.ToolStripMenuItem("Trigger Key (While Dragging File)");
            void AddTriggerOption(string label, string keyName, int vkCode)
            {
                var item = new Forms.ToolStripMenuItem(label)
                {
                    Checked = (Settings.TriggerName == keyName)
                };
                item.Click += (_, _) =>
                {
                    Settings.TriggerName = keyName;
                    Settings.TriggerVkCode = vkCode;
                    Settings.Save();
                    NativeHardwareMonitor.SetTriggerKey(vkCode, keyName);
                    BuildTrayMenu();
                };
                triggerMenu.DropDownItems.Add(item);
            }

            AddTriggerOption("Drag + [Shift] OR [X] Key (Default - Both Work!)", "Shift_Or_X", 0x00);
            AddTriggerOption("Drag + Press [X] Key Only", "Key_X", 0x58);
            AddTriggerOption("Drag + Press [Shift] Key Only", "Key_Shift", 0x10);
            AddTriggerOption("Drag + Press [Alt] Key", "Key_Alt", 0x12);
            AddTriggerOption("Drag + Press [Ctrl] Key", "Key_Ctrl", 0x11);
            AddTriggerOption("Drag + Middle Mouse Click", "Mouse_Middle", 0x04);
            AddTriggerOption("Drag + Mouse Side Button (XButton1)", "Mouse_XButton1", 0x05);

            menu.Items.Add(triggerMenu);
            menu.Items.Add("-");
            menu.Items.Add("Exit Tangerine", null, (_, _) => Shutdown());

            if (_trayIcon != null)
                _trayIcon.ContextMenuStrip = menu;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            NativeHardwareMonitor.Stop();
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
            base.OnExit(e);
        }
    }
}
