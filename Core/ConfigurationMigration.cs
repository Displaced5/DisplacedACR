namespace ViperACR.Core;

public static class ConfigurationMigration
{
    // Copy each missing JSON independently so interrupted migrations can resume.
    // Existing destination settings always win.
    public static void CopyMissingJson(string source, string destination)
    {
        if (!Directory.Exists(source)) return;
        foreach (var file in Directory.EnumerateFiles(source, "*.json"))
        {
            var target = Path.Combine(destination, Path.GetFileName(file));
            if (File.Exists(target)) continue;
            Directory.CreateDirectory(destination);
            File.Copy(file, target, overwrite: false);
        }
    }
}
