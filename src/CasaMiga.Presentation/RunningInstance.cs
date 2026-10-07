using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CasaMiga.Presentation;

/// <summary>Trae al frente la ventana de Casa Miga que ya está abierta cuando se intenta abrir otra.</summary>
internal static class RunningInstance
{
    private const int Restore = 9;
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShowWindowAsync(IntPtr window, int command);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsIconic(IntPtr window);

    /// <summary>true si había una sola otra ventana de Casa Miga y quedó al frente.</summary>
    public static bool TryActivateOther()
    {
        using var current = Process.GetCurrentProcess();
        var processes = Process.GetProcessesByName(current.ProcessName);
        try
        {
            var windows = processes.Where(p => p.Id != current.Id && p.MainWindowHandle != IntPtr.Zero).Select(p => p.MainWindowHandle).ToArray();
            if (windows.Length != 1) return false;
            if (IsIconic(windows[0])) ShowWindowAsync(windows[0], Restore);
            return SetForegroundWindow(windows[0]);
        }
        catch (Exception error) when (error is InvalidOperationException or Win32Exception) { return false; }
        finally { foreach (var process in processes) process.Dispose(); }
    }
}
