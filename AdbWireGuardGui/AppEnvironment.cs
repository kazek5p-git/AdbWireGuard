namespace AdbWireGuardGui;

internal static class AppEnvironment
{
    public static string AppTitle => "ADB przez WireGuard";
    public static string PackageRoot => ResolvePackageRoot();
    public static string SettingsPath => Path.Combine(AppContext.BaseDirectory, "gui-settings.json");

    private static string ResolvePackageRoot()
    {
        var overridePath = ReadOverridePath();
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return overridePath;
        }

        var localPackage = Path.Combine(AppContext.BaseDirectory, "ADB-WireGuard");
        if (Directory.Exists(localPackage))
        {
            return localPackage;
        }

        var sharedPackage = GetSharedPackageRoot();
        if (!string.IsNullOrWhiteSpace(sharedPackage) && Directory.Exists(sharedPackage))
        {
            return sharedPackage;
        }

        var appDataPackage = GetAppDataPackageRoot();
        if (!string.IsNullOrWhiteSpace(appDataPackage) && Directory.Exists(appDataPackage))
        {
            return appDataPackage;
        }

        return localPackage;
    }

    private static string? GetSharedPackageRoot()
    {
        try
        {
            var baseDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var directoryName = Path.GetFileName(baseDirectory);
            if (!string.Equals(directoryName, "ADB-WireGuard-GUI", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var parentDirectory = Directory.GetParent(baseDirectory);
            if (parentDirectory is null)
            {
                return null;
            }

            return Path.Combine(parentDirectory.FullName, "ADB-WireGuard");
        }
        catch
        {
            return null;
        }
    }

    private static string? GetAppDataPackageRoot()
    {
        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localAppData))
            {
                return null;
            }

            return Path.Combine(localAppData, "ADB-WireGuard", "package");
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadOverridePath()
    {
        try
        {
            var overrideFile = Path.Combine(AppContext.BaseDirectory, "package-root.txt");
            if (!File.Exists(overrideFile))
            {
                return null;
            }

            var text = File.ReadAllText(overrideFile).Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch
        {
            return null;
        }
    }
}
