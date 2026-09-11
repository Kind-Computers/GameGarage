using System.IO;
using Microsoft.Win32;

namespace GameGarage.Utilities;

internal static class SystemUtilities
{
    public static int WindowsVersion => Environment.OSVersion.Version.Build >= 22000 ? 11 : 10;
    public static string WindowsProductName
    {
        get
        {
            var fallback = $"Windows {WindowsVersion}";
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                var name = key?.GetValue("ProductName") as string;
                if (string.IsNullOrWhiteSpace(name)) return fallback;
                // Windows 11 retains "Windows 10" in this registry value on some installations.
                return WindowsVersion == 11 ? name.Replace("Windows 10", "Windows 11", StringComparison.Ordinal) : name;
            }
            catch (System.Security.SecurityException) { return fallback; }
            catch (UnauthorizedAccessException) { return fallback; }
            catch (IOException) { return fallback; }
        }
    }
}
