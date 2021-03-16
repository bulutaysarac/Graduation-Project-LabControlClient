using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Threading;

namespace LabControlClient
{
    public class DataClass
    {
        public static MainWindow mainWindow;
        public static bool IsScreenLocked = true;
        public static bool IsKeyboardLocked = false;
        public static string screenShareIP = null;

        private static string receivedData;

        [DllImport("user32.dll")]
        public static extern void mouse_event(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);

        public const int MOUSEEVENTF_LEFTDOWN = 0x02;
        public const int MOUSEEVENTF_LEFTUP = 0x04;
        public const int MOUSEEVENTF_RIGHTDOWN = 0x08;
        public const int MOUSEEVENTF_RIGHTUP = 0x10;
        private const int MOUSEEVENTF_WHEEL = 0x0800;

        [DllImport("user32.dll", SetLastError = true)]
        static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

        public const int KEYEVENTF_EXTENDEDKEY = 0x0001; //Key down flag
        public const int KEYEVENTF_KEYUP = 0x0002; //Key up flag

        public static string ReceivedData
        {
            get { return receivedData; }

            set
            {
                receivedData = value;

                if (ReceivedData.Equals("lock"))
                {
                    if (IsScreenLocked)
                    {
                        DataClass.mainWindow.Dispatcher.Invoke(() =>
                        {
                            DataClass.mainWindow.LockScreen(false);
                        });
                    }
                    else
                    {
                        DataClass.mainWindow.Dispatcher.Invoke(() =>
                        {
                            DataClass.mainWindow.LockScreen(true);
                        });
                    }
                    IsScreenLocked = !IsScreenLocked;
                }
                else if (ReceivedData.Equals("disable_keyboard"))
                {
                    if (IsKeyboardLocked)
                    {
                        DataClass.mainWindow.Dispatcher.Invoke(() =>
                        {
                            DataClass.mainWindow.LockKeyboard(false);
                        });
                    }
                    else
                    {
                        DataClass.mainWindow.Dispatcher.Invoke(() =>
                        {
                            DataClass.mainWindow.LockKeyboard(true);
                        });
                    }
                    IsKeyboardLocked = !IsKeyboardLocked;
                }
                else if (ReceivedData.Equals("shut_down"))
                {
                    Process.Start("shutdown", "/s /t 0");
                }
                else if (ReceivedData.Equals("restart"))
                {
                    Process.Start("shutdown.exe", "-r -t 0");
                }
                else if (ReceivedData.StartsWith("share_screen:"))
                {
                    DataClass.screenShareIP = ReceivedData.Split(':')[1];

                    DataClass.mainWindow.Dispatcher.Invoke(() =>
                    {
                        DataClass.mainWindow.ConnectScreenShareServer();
                        Thread.Sleep(100);
                        DataClass.mainWindow.StartSharingScreen();
                    });
                }
                else if (ReceivedData.Equals("stop_screen_share"))
                {
                    DataClass.mainWindow.Dispatcher.Invoke(() =>
                    {
                        DataClass.mainWindow.StopSharingScreen();
                        Thread.Sleep(100);
                        DataClass.mainWindow.DisconnectScreenShareServer();
                    });
                }
                else if (ReceivedData.StartsWith("mouse_point:"))
                {
                    string[] splittedData = ReceivedData.Split(':');
                    int _x = int.Parse(splittedData[1]);
                    int _y = int.Parse(splittedData[2]);
                    Cursor.Position = new System.Drawing.Point() { X = _x, Y = _y };
                }
                else if (ReceivedData.StartsWith("mouse_left") || ReceivedData.StartsWith("mouse_right"))
                {
                    switch (ReceivedData)
                    {
                        case "mouse_left_up":
                            mouse_event(MOUSEEVENTF_LEFTUP, Cursor.Position.X, Cursor.Position.Y, 0, 0);
                            break;
                        case "mouse_left_down":
                            mouse_event(MOUSEEVENTF_LEFTDOWN, Cursor.Position.X, Cursor.Position.Y, 0, 0);
                            break;
                        case "mouse_right_down":
                            mouse_event(MOUSEEVENTF_RIGHTDOWN, Cursor.Position.X, Cursor.Position.Y, 0, 0);
                            break;
                        case "mouse_right_up":
                            mouse_event(MOUSEEVENTF_RIGHTUP, Cursor.Position.X, Cursor.Position.Y, 0, 0);
                            break;
                    }
                }
                else if (ReceivedData.StartsWith("key:"))
                {
                    byte keyCode = Convert.ToByte(ReceivedData.Split(':')[1]);
                    keybd_event(keyCode, 0, KEYEVENTF_EXTENDEDKEY, 0);
                    keybd_event(keyCode, 0, KEYEVENTF_KEYUP, 0);
                }
                else if(ReceivedData.StartsWith("mouse_wheel:"))
                {
                    string[] splittedData = ReceivedData.Split(':');
                    int delta = int.Parse(splittedData[1]);
                    mouse_event(MOUSEEVENTF_WHEEL, 0, 0, delta, 0);
                }
                else if(ReceivedData.StartsWith("combin:"))
                {
                    string[] splittedData = ReceivedData.Split(':');
                    string[] keyCodes = splittedData[1].Split('+');
                    byte keyCode1 = Convert.ToByte(keyCodes[0]);
                    byte keyCode2 = Convert.ToByte(keyCodes[1]);
                    keybd_event(keyCode1, 0, 0, 0);
                    keybd_event(keyCode2, 0, 0, 0);
                    keybd_event(keyCode2, 0, KEYEVENTF_KEYUP, 0);
                    keybd_event(keyCode1, 0, KEYEVENTF_KEYUP, 0);
                }
                else if(ReceivedData.Equals("kill_client"))
                {
                    DataClass.mainWindow.Dispatcher.Invoke(() =>
                    {
                        Taskbar.Show();
                    });

                    try
                    {
                        Process backgroundService = Process.GetProcessesByName("BackgroundService")[0];
                        Process labControlClient = Process.GetProcessesByName("LabControlClient")[0];
                        backgroundService.Kill();
                        labControlClient.Kill();
                    }
                    catch { }
                }
                else if(ReceivedData.Equals("reset_client"))
                {
                    DataClass.mainWindow.Dispatcher.Invoke(() =>
                    {
                        Taskbar.Show();
                    });

                    try
                    {
                        Process labControlClient = Process.GetProcessesByName("LabControlClient")[0];
                        labControlClient.Kill();
                    }
                    catch { }
                }
            }
        }
    }
}
