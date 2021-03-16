using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LabControlClient
{
    public class Taskbar
    {
        [DllImport("user32.dll")]
        private static extern int FindWindow(string className, string windowText);

        [DllImport("user32.dll")]
        private static extern int ShowWindow(int hwnd, int command);

        [DllImport("user32.dll")]
        public static extern int FindWindowEx(int parentHandle, int childAfter, string className, int windowTitle);

        [DllImport("user32.dll")]
        private static extern int GetDesktopWindow();

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 1;

        protected static int Handle
        {
            get
            {
                return FindWindow("Shell_TrayWnd", "");
            }
        }

        protected static int HandleOfStartButton
        {
            get
            {
                int handleOfDesktop = GetDesktopWindow();
                int handleOfStartButton = FindWindowEx(handleOfDesktop, 0, "button", 0);
                return handleOfStartButton;
            }
        }

        public static void Show()
        {
            ShowWindow(Handle, SW_SHOW);
            ShowWindow(HandleOfStartButton, SW_SHOW);
        }

        public static void Hide()
        {
            ShowWindow(Handle, SW_HIDE);
            ShowWindow(HandleOfStartButton, SW_HIDE);
        }

        public static void SetTaskManager(bool enable)
        {
            RegistryKey objRegistryKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\System");
            if (enable && objRegistryKey.GetValue("DisableTaskMgr") != null)
                objRegistryKey.DeleteValue("DisableTaskMgr");
            else
                objRegistryKey.SetValue("DisableTaskMgr", "1");
            objRegistryKey.Close();
        }
    }
}
