using System.Text.Json;
using ViperACR.Core;

namespace ViperACR.Host;

public enum OpenerMode { Standard = 0, Dynamic = 1, Tea80 = 2, Dsr90 = 3, Fru100 = 4 }

public sealed class ViperSettings
{
    public OpenerMode Opener { get; set; } = OpenerMode.Standard;
    public int AoeThreshold { get; set; } = 3;
    public int ReserveCoils { get; set; } = 1;
    public float BurstReserveSeconds { get; set; } = 45;
    public float WeaveSafetySeconds { get; set; } = 0.75f;
    public int TrueNorthGcdPercent { get; set; } = 50;
    public bool CombatLogRecordEnabled { get; set; }
    public bool ShowTargetLine { get; set; }
    public float TargetLineRed { get; set; } = 0.3f;
    public float TargetLineGreen { get; set; } = 0.9f;
    public float TargetLineBlue { get; set; } = 0.7f;
    public float TargetLineThickness { get; set; } = 2;
    public static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "XIVLauncherCN", "pluginConfigs", "PromeRotation", "ACR", ReleaseInfo.Author, "settings.json");
    public static ViperSettings Load(out string error)
    {
        error = "";
        try
        {
            var result = File.Exists(FilePath) ? JsonSerializer.Deserialize<ViperSettings>(File.ReadAllText(FilePath)) ?? new() : new();
            result.AoeThreshold = Math.Clamp(result.AoeThreshold, 2, 8);
            result.Opener = OpenerSelection.FromIndex(OpenerSelection.ToIndex(result.Opener));
            result.ReserveCoils = Math.Clamp(result.ReserveCoils, 0, 2);
            result.BurstReserveSeconds = Math.Clamp(result.BurstReserveSeconds, 15, 60);
            result.WeaveSafetySeconds = Math.Clamp(result.WeaveSafetySeconds, 0.65f, 1.2f);
            result.TrueNorthGcdPercent = Math.Clamp(result.TrueNorthGcdPercent, 0, 100);
            result.TargetLineRed = Normalize(result.TargetLineRed, 0.3f, 0, 1);
            result.TargetLineGreen = Normalize(result.TargetLineGreen, 0.9f, 0, 1);
            result.TargetLineBlue = Normalize(result.TargetLineBlue, 0.7f, 0, 1);
            result.TargetLineThickness = Normalize(result.TargetLineThickness, 2, 1, 10);
            return result;
        }
        catch (Exception ex) { error = "配置读取失败，已使用默认值：" + ex.Message; return new(); }
    }
    private static float Normalize(float value, float fallback, float min, float max) =>
        float.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, FilePath, true);
    }
}
