using PromeRotation.Data;
using PromeRotation.UI.HotKey;

namespace ErosUI;

// 热键面板构建器：按注册顺序产出条目列表，交给悬浮面板自绘（不使用宿主默认皮肤）。
// 已在设置里隐藏的按钮名会自动过滤。
public sealed class ErosUIHotkeyBuilder
{
    private readonly HashSet<string> hidden;

    /// <summary>构建产物，条目顺序即面板排布顺序。GameIcon 为游戏内原始图标 id（物品等无动作表的条目用），GameIconHQ 表示取 HQ 品质图标。</summary>
    public List<(string Name, IHotkey Hotkey, uint GameIcon, bool GameIconHQ)> Entries { get; } = new();

    public ErosUIHotkeyBuilder(ErosUISettings settings)
    {
        hidden = new HashSet<string>(settings.HiddenHotkeys);
    }

    /// <summary>注册一个固定技能按钮，点击时把该技能原样放进热键队列，图标自动取游戏内技能图标。</summary>
    public void Fixed(string name, uint spell, ActionType type, ActionTargetType target, Func<bool>? executionGuard = null)
    {
        if (!hidden.Contains(name))
            Entries.Add((name, new ActionHotkey(new PAction(spell, type, target) { ExecutionGuard = executionGuard }), 0, false));
    }

    /// <summary>注册一个自定义逻辑按钮，点击时执行传入的 IHotkeyLogic。</summary>
    /// <param name="iconActionID">游戏内动作 id，按钮的图标、冷却、充能显示都来自它。</param>
    /// <param name="customIconPath">自定义图标路径（宿主 Resources 资源名或绝对路径 tex/png）。</param>
    /// <param name="gameIconID">游戏内原始图标 id，用于物品等不在动作表里的条目，优先于 iconActionID。</param>
    /// <param name="gameIconHQ">图标是否取 HQ 品质（游戏内 hq/ 子目录）。</param>
    public void Execute(string name, IHotkeyLogic logic, uint iconActionID = 0, string? customIconPath = null, uint gameIconID = 0, bool gameIconHQ = false)
    {
        if (!hidden.Contains(name))
            Entries.Add((name, new DelegateHotkey(logic, iconActionID, customIconPath), gameIconID, gameIconHQ));
    }
}
