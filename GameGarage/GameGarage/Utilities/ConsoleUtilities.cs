using System.ComponentModel;
using System.Runtime.InteropServices;

namespace GameGarage.Utilities;

internal static class ConsoleUtilities
{
    public static Thread STAThreadWrapper(Action action)
    {
        Thread thread = new(() => action());
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return thread;
    }

    public sealed class AttachToConsole : IDisposable
    {
        private readonly bool allocated;
        public AttachToConsole(bool AttachToParent)
        {
            // A private console prevents cancellation from reaching unrelated terminal children.
            if (!AttachToParent) FreeConsole();
            bool attached = AttachToParent && (GetConsoleWindow() != IntPtr.Zero || AttachConsole(uint.MaxValue));
            if (!attached)
            {
                allocated = AllocConsole();
                if (!allocated) throw new Win32Exception(Marshal.GetLastWin32Error());
                VisibilityControl.Hide();
            }
            BreakControl.RegisterCTRLHandler();
        }
        public void Dispose()
        {
            BreakControl.UnregisterCTRLHandler();
            if (allocated) FreeConsole();
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(uint processId);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();
    }

    public static class VisibilityControl
    {
        public static void Hide() => ShowWindow(GetConsoleWindow(), 0);
        public static void Show() => ShowWindow(GetConsoleWindow(), 5);
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr window, int command);
    }

    public static class BreakControl
    {
        private delegate bool ConsoleControlHandler(uint eventType);
        // Native code retains this pointer. Root the delegate for the entire process lifetime.
        private static readonly ConsoleControlHandler Handler = eventType => eventType is 0 or 1;
        public static void RegisterCTRLHandler()
        {
            if (!SetConsoleCtrlHandler(Handler, true))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        public static void UnregisterCTRLHandler() => SetConsoleCtrlHandler(Handler, false);
        public static void SendBreakSignal()
        {
            // The GUI creates a private hidden console inherited by its children.
            // Never terminate Windows servicing tools if cooperative cancellation is delayed.
            if (!GenerateConsoleCtrlEvent(1, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(ConsoleControlHandler handler, bool add);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GenerateConsoleCtrlEvent(uint eventType, uint processGroupId);
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();
}
