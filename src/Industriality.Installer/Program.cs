using System.Runtime.InteropServices;

namespace Industriality.Installer;

internal static class Program
{
    private static int Main()
    {
        try
        {
            Bootstrapper.Run();
            return 0;
        }
        catch (Exception ex)
        {
            ReportError(ex);
            return 1;
        }
    }

    private static void ReportError(Exception ex)
    {
        var message = $"Failed to launch Industriality:\n\n{ex.Message}";
        Console.Error.WriteLine(message);
        Console.Error.WriteLine(ex.StackTrace);

        if (OperatingSystem.IsWindows())
        {
            try { MessageBoxW(IntPtr.Zero, message, "Industriality Launcher", 0x10); }
            catch { /* P/Invoke failures shouldn't mask original error */ }
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
