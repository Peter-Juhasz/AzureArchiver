using System;
using System.Runtime.InteropServices;

namespace PhotoArchiver.Progress
{
    internal class WindowsTaskbarProgressIndicator : IProgressIndicator
    {
        public WindowsTaskbarProgressIndicator()
        {
            // the output of the app is streamed to a console, so we can't use the process' main window handle
            // HACK: we make the window identifyable, find it, then revert its title
            var originalTitle = System.Console.Title;
            var reference = Guid.NewGuid().ToString();
            System.Console.Title = reference;
            WindowHandle = FindWindowByCaption(IntPtr.Zero, reference);
            System.Console.Title = originalTitle;
        }

        protected IntPtr WindowHandle { get; }

        public void Initialize()
        {
            TaskbarProgress.SetState(WindowHandle, TaskbarProgress.TaskbarStates.Normal);
        }

        public void Indeterminate()
        {
            TaskbarProgress.SetState(WindowHandle, TaskbarProgress.TaskbarStates.Indeterminate);
        }

        public void Set(int processed, int all)
        {
            if (processed < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(processed));
            }

            if (all < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(all));
            }

            if (processed > all)
            {
                processed = all;
            }

            TaskbarProgress.SetValue(WindowHandle, processed, all);
        }

        public void Finished()
        {
            TaskbarProgress.SetState(WindowHandle, TaskbarProgress.TaskbarStates.NoProgress);
            FlashWindow(WindowHandle);
        }

        public void Error()
        {
            TaskbarProgress.SetState(WindowHandle, TaskbarProgress.TaskbarStates.Error);
        }


        [DllImport("user32.dll", EntryPoint = "FindWindow", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindowByCaption(IntPtr zeroOnly, string lpWindowName);

        private const UInt32 FLASHW_ALL = 3;
        private const UInt32 FLASHW_TIMERNOFG = 12;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        public static void FlashWindow(IntPtr windowHandle)
        {
            FLASHWINFO info = new FLASHWINFO
            {
                hwnd = windowHandle,
                dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG,
                uCount = 0,
                dwTimeout = 0
            };

            info.cbSize = Convert.ToUInt32(Marshal.SizeOf(info));
            FlashWindowEx(ref info);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FLASHWINFO
        {
            public UInt32 cbSize;
            public IntPtr hwnd;
            public UInt32 dwFlags;
            public UInt32 uCount;
            public UInt32 dwTimeout;
        }

        private static class TaskbarProgress
        {
            public enum TaskbarStates
            {
                NoProgress = 0,
                Indeterminate = 0x1,
                Normal = 0x2,
                Error = 0x4,
                Paused = 0x8
            }

            [ComImport()]
            [Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
            [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            private interface ITaskbarList3
            {
                // ITaskbarList
                [PreserveSig]
                void HrInit();
                [PreserveSig]
                void AddTab(IntPtr hwnd);
                [PreserveSig]
                void DeleteTab(IntPtr hwnd);
                [PreserveSig]
                void ActivateTab(IntPtr hwnd);
                [PreserveSig]
                void SetActiveAlt(IntPtr hwnd);

                // ITaskbarList2
                [PreserveSig]
                void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);

                // ITaskbarList3
                [PreserveSig]
                void SetProgressValue(IntPtr hwnd, UInt64 ullCompleted, UInt64 ullTotal);
                [PreserveSig]
                void SetProgressState(IntPtr hwnd, TaskbarStates state);
            }

            [ComImport()]
            [Guid("56fdf344-fd6d-11d0-958a-006097c9a090")]
            [ClassInterface(ClassInterfaceType.None)]
            private class TaskbarInstance
            {
            }

            private static ITaskbarList3 taskbarInstance = (ITaskbarList3)new TaskbarInstance();

            public static void SetState(IntPtr windowHandle, TaskbarStates taskbarState)
            {
                taskbarInstance.SetProgressState(windowHandle, taskbarState);
            }

            public static void SetValue(IntPtr windowHandle, double progressValue, double progressMax)
            {
                taskbarInstance.SetProgressValue(windowHandle, (ulong)progressValue, (ulong)progressMax);
            }
        }
    }
}
