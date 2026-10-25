using System.Numerics;

namespace ErosUI;

// ErosUI 各窗口共用配色：通用主色调（可自定义覆盖）+ Dark/Light 双色彩方案（布局共用，配色由方案区分）。
// 只用于窗口全局样式 Push/Pop，不做卡片/高亮/发光。
public static class SimplePalette
{
    // 色彩方案：Dark = 夜间模式（深底浅字），Light = 日间模式（浅底深字）。
    public enum UIColorScheme { Dark, Light }

    private static UIColorScheme currentScheme = UIColorScheme.Dark;

    // 切换色彩方案（由各窗口在 PreDraw 按设置的主题每帧写入）。
    public static void ApplyScheme(UIColorScheme scheme)
    {
        if (currentScheme == scheme) return;
        currentScheme = scheme;
        Recompute();
    }

    // === 主色自定义（设置窗「主题」页 RGB 调色）：全局唯一一份，日/夜方案都生效 ===
    private static Vector3? primaryOverride;

    // 是否设置了自定义主色（无则用内置配色）。
    public static bool HasPrimaryOverride => primaryOverride != null;

    // 读取自定义主色（RGB 0~1；未设置返回 null）。
    public static Vector3? GetPrimaryOverride() => primaryOverride;

    // 设置/清除（null = 恢复内置配色）自定义主色，立即重算生效。
    public static void SetPrimaryOverride(Vector3? rgb)
    {
        primaryOverride = rgb is { } c
            ? new Vector3(Math.Clamp(c.X, 0f, 1f), Math.Clamp(c.Y, 0f, 1f), Math.Clamp(c.Z, 0f, 1f))
            : null;
        Recompute();
    }

    // 主色调
    public static Vector4 Primary { get; private set; }
    public static Vector4 PrimaryHover { get; private set; }
    public static Vector4 PrimaryActive { get; private set; }

    // 强调色：勾选、滑条 grab、导航激活。浅色方案用暗一档的 PrimaryActive 保证白底对比度。
    public static Vector4 Accent => currentScheme == UIColorScheme.Light ? PrimaryActive : Primary;

    // === 语义文字色（随方案：日间浅底自动换深色变体，与背景区分；夜间保持亮色） ===

    // 高难模式文字（夜间亮橙 / 日间深琥珀）。
    public static Vector4 ModeHighEndText => currentScheme == UIColorScheme.Light
        ? new Vector4(0.75f, 0.55f, 0.10f, 1f)
        : new Vector4(1f, 0.80f, 0.20f, 1f);

    // 日随模式文字（夜间亮绿 / 日间深绿）。
    public static Vector4 ModeDailyText => currentScheme == UIColorScheme.Light
        ? new Vector4(0.05f, 0.72f, 0.22f, 1f)
        : new Vector4(0.20f, 1f, 0.40f, 1f);

    // 文字（随方案）
    public static Vector4 TextPrimary { get; private set; }
    public static Vector4 TextSecondary { get; private set; }
    public static Vector4 TextDisabled { get; private set; }

    public static Vector4 FrameBg { get; private set; }
    public static Vector4 FrameBgHovered { get; private set; }
    public static Vector4 FrameBgActive { get; private set; }

    // 弹窗底（Combo 下拉/右键菜单; 近实底保可读性, 随方案换深/浅）
    public static Vector4 PopupBg { get; private set; }

    // 边框与分隔（随方案）
    public static Vector4 Border { get; private set; }
    public static Vector4 BorderStrong { get; private set; }

    // 侧边栏导航激活项（随方案：Dark = 主色淡染胶囊，Light = 白色胶囊）
    public static Vector4 NavActiveBg { get; private set; }
    public static Vector4 NavActiveText { get; private set; }

    // 运行状态色（战斗控制窗口，两种方案共用）
    public static readonly Vector4 StateRunning = new(0.30f, 0.85f, 0.45f, 1f);   // 绿
    public static readonly Vector4 StateHold = new(0.96f, 0.62f, 0.20f, 1f);       // 橙
    public static readonly Vector4 StateOff = new(0.92f, 0.30f, 0.32f, 1f);        // 红

    static SimplePalette() => Recompute();

    private static void Recompute()
    {
        // 内置默认主色（hover/active 与自定义覆盖同一套明暗关系: 提亮 20% / 加深 12%）
        Primary = new(0.686f, 0.318f, 0.914f, 1f); // #AF51E9
        PrimaryHover = new(0.749f, 0.454f, 0.931f, 1f); // #BF74ED（提亮一档）
        PrimaryActive = new(0.604f, 0.280f, 0.804f, 1f); // #9A47CD（加深一档）

        // 自定义主色盖过内置配色：hover 提亮 / active 加深一档，保持相同的明暗关系
        if (primaryOverride is { } custom)
        {
            var c = new Vector4(custom.X, custom.Y, custom.Z, 1f);
            Primary = c;
            PrimaryHover = Vector4.Lerp(c, Vector4.One, 0.2f);
            PrimaryActive = Vector4.Lerp(c, Vector4.Zero, 0.12f);
        }

        if (currentScheme == UIColorScheme.Light)
        {
            TextPrimary = new(0.15f, 0.15f, 0.17f, 1f);
            TextSecondary = new(0.38f, 0.38f, 0.42f, 1f);
            TextDisabled = new(0.62f, 0.62f, 0.66f, 1f);
            FrameBg = new(1f, 1f, 1f, 0.85f);
            FrameBgHovered = new(1f, 1f, 1f, 0.95f);
            FrameBgActive = new(0.94f, 0.94f, 0.96f, 1f);
            PopupBg = new(1f, 1f, 1f, 0.98f);
            Border = new(0f, 0f, 0f, 0.10f);
            BorderStrong = new(0f, 0f, 0f, 0.18f);
            NavActiveBg = new(1f, 1f, 1f, 0.92f);
            NavActiveText = TextPrimary;
        }
        else
        {
            TextPrimary = new(0.95f, 0.95f, 0.95f, 1f);
            TextSecondary = new(0.70f, 0.70f, 0.70f, 1f);
            TextDisabled = new(0.35f, 0.35f, 0.35f, 1f);
            FrameBg = new(0.10f, 0.10f, 0.10f, 0.50f);
            FrameBgHovered = new(0.15f, 0.15f, 0.15f, 0.60f);
            FrameBgActive = new(0.20f, 0.20f, 0.20f, 0.70f);
            PopupBg = new(0.06f, 0.06f, 0.06f, 0.98f);
            Border = new(1f, 1f, 1f, 0.10f);
            BorderStrong = new(1f, 1f, 1f, 0.20f);
            NavActiveBg = WithAlpha(Primary, 0.18f);
            NavActiveText = TextPrimary;
        }
    }

    public static uint ToU32(Vector4 c)
    {
        var r = (byte)(Math.Clamp(c.X, 0f, 1f) * 255);
        var g = (byte)(Math.Clamp(c.Y, 0f, 1f) * 255);
        var b = (byte)(Math.Clamp(c.Z, 0f, 1f) * 255);
        var a = (byte)(Math.Clamp(c.W, 0f, 1f) * 255);
        return (uint)((a << 24) | (b << 16) | (g << 8) | r);
    }

    public static Vector4 WithAlpha(Vector4 c, float a) => new(c.X, c.Y, c.Z, a);
}
