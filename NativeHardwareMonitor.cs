using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace TangerineWin
{
    public static class NativeHardwareMonitor
    {
        private const int VK_LBUTTON = 0x01;
        private const int VK_SHIFT = 0x10;
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12; // Alt
        private const int VK_SPACE = 0x20;
        private const int VK_X = 0x58;

        private static Thread? _workerThread;
        private static volatile bool _isRunning = false;

        private static volatile int _targetVk = 0x00; // 0x00 = Both Shift AND X key work!
        private static string _triggerMode = "Shift_Or_X";

        public static event Action<int, int>? OnDragTriggered;
        public static event Action<int, int>? OnCursorMoved;
        public static event Action? OnLeftMouseReleased;

        public static void SetTriggerKey(int vkCode, string modeName)
        {
            _targetVk = vkCode;
            _triggerMode = modeName;
        }

        public static void Start()
        {
            if (_isRunning) return;
            _isRunning = true;

            _workerThread = new Thread(PollingLoop)
            {
                Name = "Tangerine_Hardware_Monitor",
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal
            };
            _workerThread.Start();
        }

        public static void Stop()
        {
            _isRunning = false;
            _workerThread?.Join(300);
        }

        public static void ForceOleDragEnterNudge()
        {
            if (GetCursorPos(out POINT pt))
            {
                SetCursorPos(pt.x + 1, pt.y);
                SetCursorPos(pt.x, pt.y);
            }
        }

        private static bool IsKeyDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

        private static bool IsTargetPressed()
        {
            // Default mode 0x00: Accept EITHER Shift OR X key!
            if (_targetVk == 0x00)
            {
                bool shift = IsKeyDown(VK_SHIFT) || IsKeyDown(0xA0) || IsKeyDown(0xA1);
                bool xKey  = IsKeyDown(VK_X);
                return shift || xKey;
            }

            if (IsKeyDown(_targetVk)) return true;

            if (_targetVk == 0x10) return IsKeyDown(0xA0) || IsKeyDown(0xA1);
            if (_targetVk == 0x12) return IsKeyDown(0xA4) || IsKeyDown(0xA5);
            if (_targetVk == 0x11) return IsKeyDown(0xA2) || IsKeyDown(0xA3);

            return false;
        }

        private static void PollingLoop()
        {
            bool wasLeftDown = false;
            POINT downPt = new POINT();
            bool triggeredThisDrag = false;
            bool wasHotkeyDown = false;
            int lastX = -1, lastY = -1;

            while (_isRunning)
            {
                try
                {
                    bool isLeftDown = IsKeyDown(VK_LBUTTON);
                    GetCursorPos(out POINT curPt);

                    if (curPt.x != lastX || curPt.y != lastY)
                    {
                        lastX = curPt.x;
                        lastY = curPt.y;
                        OnCursorMoved?.Invoke(curPt.x, curPt.y);
                    }

                    // Global Hotkey: Alt + Space (Opens wheel anytime at cursor, even without dragging)
                    bool altSpace = (IsKeyDown(VK_MENU) || IsKeyDown(0xA4) || IsKeyDown(0xA5)) && IsKeyDown(VK_SPACE);
                    if (altSpace && !wasHotkeyDown)
                    {
                        wasHotkeyDown = true;
                        int hx = curPt.x, hy = curPt.y;
                        ThreadPool.QueueUserWorkItem(_ => OnDragTriggered?.Invoke(hx, hy));
                    }
                    else if (!altSpace)
                    {
                        wasHotkeyDown = false;
                    }

                    // Mouse Left Button Down
                    if (isLeftDown && !wasLeftDown)
                    {
                        wasLeftDown = true;
                        downPt = curPt;
                        triggeredThisDrag = false;
                    }
                    // Mouse Left Button Up
                    else if (!isLeftDown && wasLeftDown)
                    {
                        wasLeftDown = false;
                        triggeredThisDrag = false;
                        OnLeftMouseReleased?.Invoke();
                    }

                    // Active Dragging State: Check if user moved > 4px AND pressed Trigger Key (Shift OR X)
                    if (isLeftDown && !triggeredThisDrag)
                    {
                        int dx = Math.Abs(curPt.x - downPt.x);
                        int dy = Math.Abs(curPt.y - downPt.y);

                        if ((dx > 4 || dy > 4) && IsTargetPressed())
                        {
                            triggeredThisDrag = true;
                            int px = curPt.x;
                            int py = curPt.y;
                            ThreadPool.QueueUserWorkItem(_ => OnDragTriggered?.Invoke(px, py));
                        }
                    }
                }
                catch { }

                Thread.Sleep(10);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int x; public int y; }

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
    }
}
