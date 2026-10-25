using ViperACR.Core;

namespace ViperACR.Host;

internal static class LegacyConfiguration
{
    internal static void Migrate()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XIVLauncherCN", "pluginConfigs", "PromeRotation");
        var oldSettings = Path.Combine(root, "ACR", "ViperACR", "settings.json");
        if (File.Exists(oldSettings) && !File.Exists(ViperSettings.FilePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ViperSettings.FilePath)!);
            File.Copy(oldSettings, ViperSettings.FilePath, overwrite: false);
        }
        var uiRoot = Path.Combine(root, "Settings", "ACRConfig");
        ConfigurationMigration.CopyMissingJson(Path.Combine(uiRoot, "ViperACR"),
            Path.Combine(uiRoot, ReleaseInfo.Author));
    }
}
