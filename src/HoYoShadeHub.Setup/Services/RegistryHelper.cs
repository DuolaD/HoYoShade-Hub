using Microsoft.Win32;

namespace HoYoShadeHub.Setup.Services;

public static class RegistryHelper
{

    public static void WriteUninstallInfo(string folder, string version, long size)
    {
        string exe = Path.Combine(folder, "HoYoShadeHub.exe");
        string setupExe = Path.Combine(folder, "HoYoShadeHub.Setup.exe");
        using var subkey = Registry.LocalMachine.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\HoYoShade Hub");
        subkey.SetValue("Publisher", "HoYoShade Hub Team", RegistryValueKind.String);
        subkey.SetValue("DisplayName", "HoYoShade Hub", RegistryValueKind.String);
        subkey.SetValue("DisplayIcon", exe, RegistryValueKind.String);
        subkey.SetValue("DisplayVersion", version, RegistryValueKind.String);
        subkey.SetValue("InstallLocation", folder, RegistryValueKind.String);
        subkey.SetValue("EstimatedSize", (int)(size / 1024), RegistryValueKind.DWord);
        subkey.SetValue("InstallDate", $"{DateTime.Now:yyyyMMdd}", RegistryValueKind.String);
        subkey.SetValue("UninstallString", $"\"{setupExe}\" uninstall", RegistryValueKind.String);
        subkey.SetValue("QuietUninstallString", $"\"{setupExe}\" uninstall /S", RegistryValueKind.String);
    }


    public static void DeleteUninstallInfo()
    {
        Registry.LocalMachine.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\HoYoShade Hub", false);
    }


    public static void WriteUrlProtocol(string folder)
    {
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\HoYoShade Hub", false);
        Registry.SetValue(@"HKEY_LOCAL_MACHINE\Software\Classes\HoYoShade Hub", "", "URL:HoYoShadeHub Protocol");
        Registry.SetValue(@"HKEY_LOCAL_MACHINE\Software\Classes\HoYoShade Hub", "URL Protocol", "");
        Registry.SetValue(@"HKEY_LOCAL_MACHINE\Software\Classes\HoYoShade Hub\DefaultIcon", "", "HoYoShadeHub.exe,1");
        Registry.SetValue(@"HKEY_LOCAL_MACHINE\Software\Classes\HoYoShade Hub\Shell\Open\Command", "", $"""
            "{Path.Combine(folder, "HoYoShadeHub.exe")}" "%1"
            """);
    }


    public static void DeleteUrlProtocol()
    {
        Registry.LocalMachine.DeleteSubKeyTree(@"Software\Classes\HoYoShade Hub", false);
    }


    public static string? GetInstallLocation()
    {
        using var subkey = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\HoYoShade Hub");
        return subkey?.GetValue("InstallLocation") as string;
    }


    public static void DeleteRegistrySetting()
    {
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\HoYoShade Hub", false);
    }

}





