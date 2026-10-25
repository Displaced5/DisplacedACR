using System.Text.Json;
using ECommons.DalamudServices;
using ECommons.Logging;
using PromeRotation.Config;
using PromeRotation.Data;

namespace ErosUI;

// 职业运行期设置（单例，JSON 持久化，按职业分文件: {JobTag}.json）。
// 面板布局 / QT 显隐与默认值 / 排序等框架配置在此; 多职业通用内容（主题）在 ErosUICommonSettings（Common.json）。
public class ErosUISettings
{
    private static ErosUISettings? instance;
    private static string? loadedJob;

    // 按当前职业取设置实例: 职业环境(JobTag)变化时自动重载对应文件（多职业共存,
    // 各职业的布局/QT 快照互不污染）。
    public static ErosUISettings Instance
    {
        get
        {
            // 没注入职业环境就读设置，会按空 QT 表把用户已存的排序/显隐配置清掉，直接拒绝
            if (!ErosUIJobEnv.Configured)
                throw new InvalidOperationException(
                    "请先调用 ErosUIFramework.Configure 注入职业环境，再读写设置。");
            var job = 职业文件名;
            if (instance == null || loadedJob != job)
            {
                instance = Load();
                loadedJob = job;
            }
            return instance;
        }
    }

    // ============================================================
    // === 职业框架设置 ===
    // ============================================================

    // 高难模式 / 日随模式（初次访问默认日随，面板顶部按钮可切换）
    public bool IsHighEnd = false;
    public bool LongArms = true;
    public bool UnlimitedDashDistance = false;
    public bool TripleWeave = true;
    public bool DeathAutoResetQt = true;
    public bool OpenerAutoPull = true;
    public bool OpenerAutoReleaseHold = true;

    // 战斗控制悬浮条位置（窗口拖动后落盘）
    public System.Numerics.Vector2? 控制条位置;

    // ============================================================
    // === 热键面板（每职业一套布局; 外观默认值各职业一致） ===
    // ============================================================
    // 热键面板每行按钮数
    public int HotkeyColumns = 5;

    // 热键按钮间距(px)
    public int HotkeySpacing = 5;

    // 热键缩放(%)（基准按钮 45px）
    public int HotkeyScalePercent = 100;

    // 隐藏的热键按钮名（只控制显隐，不分日随/高难）
    public HashSet<string> HiddenHotkeys = new();

    // 热键面板按钮自定义顺序（完整名字序; 未调整过为空 = 按热键表定义顺序显示）
    public List<string> HotkeyOrder = new();

    // 锁定热键排序: 开启后热键悬浮面板按钮不可右键拖拽换位（防战斗误拖）
    public bool HotkeyPanelOrderLocked = false;

    // ============================================================
    // === QT 面板（每职业一套布局; 外观默认值各职业一致; QT面板页滑块可调） ===
    // ============================================================
    // QT 面板每行按钮数
    public int QtPanelColumns = 3;

    // QT 面板按钮间距(px)
    public int QtPanelSpacing = 10;

    // QT 面板缩放(%)（基准按钮 112×34px）
    public int QtPanelScalePercent = 100;

    // ============================================================
    // === QT 面板排列顺序（每职业一套; 悬浮面板右键拖拽可调） ===
    // ============================================================
    // QT 面板按钮自定义顺序（完整键序; 未调整过为空 = 按 QT 表定义顺序显示）
    public List<string> QtOrder = new();

    // 锁定 QT 排序: 开启后悬浮面板按钮不可右键拖拽换位（防战斗误拖）
    public bool QtPanelOrderLocked = false;

    // ============================================================
    // === QT 面板按钮显隐（按模式各存一套；键=QT名; 缺省=显示。只控制显隐, 不影响开关状态） ===
    // ============================================================
    // 高难模式的 QT 显隐记录
    public Dictionary<string, bool> QtVisibleHighEnd = new();

    // 日随模式的 QT 显隐记录
    public Dictionary<string, bool> QtVisibleDaily = new();

    // 当前模式的显隐记录字典
    private Dictionary<string, bool> CurrentQtVisibleDict => IsHighEnd ? QtVisibleHighEnd : QtVisibleDaily;

    // 该 QT 按钮是否显示在 QT 面板（按当前模式取对应记录；未配置过 = 显示）
    public bool IsQtVisible(string key)
        => !CurrentQtVisibleDict.TryGetValue(key, out var v) || v;

    // 设置【当前模式】的 QT 按钮显隐并持久化（同步到 PR 本体面板的 HiddenQts）
    public void SetQtVisible(string key, bool visible)
    {
        CurrentQtVisibleDict[key] = visible;
        Save();
        SyncQtHiddenToPr();
    }

    // 把【当前模式】的显隐配置同步到 PR 本体面板的 HiddenQts 集合（数据源 = 当前职业 QT 表）。
    // 切换模式后由 重建QT可见性 重新调用，即实现「切模式自动套用该模式的显隐记录」。
    public void SyncQtHiddenToPr()
    {
        var hidden = PromeSettings.Instance.HiddenQts;
        foreach (var key in ErosUIJobEnv.QtAll.Keys)
        {
            if (ErosUIJobEnv.QtIsMetaKey(key)) continue;
            var wantHide = !IsQtVisible(key);
            var has = hidden.Contains(key);
            if (wantHide && !has) hidden.Add(key);
            else if (!wantHide && has) hidden.Remove(key);
        }
    }

    // ============================================================
    // === QT 排列顺序（悬浮面板 / 各设置页统一取此顺序） ===
    // ============================================================

    // 当前职业 QT 键的显示顺序：QtOrder 中仍有效的键按其顺序在前,
    // QT 表新增的键按定义顺序补尾; QtOrder 为空时即 QT 表定义顺序（与旧版行为一致）。
    public List<string> GetOrderedQtKeys()
    {
        var all = ErosUIJobEnv.QtAll;
        var result = new List<string>(all.Count);
        var seen = new HashSet<string>();
        foreach (var k in QtOrder)
            if (all.ContainsKey(k) && seen.Add(k)) result.Add(k);
        foreach (var k in all.Keys)
            if (seen.Add(k)) result.Add(k);
        return result;
    }

    // 把指定 QT 移动到面板上的目标位置（插入语义）。
    // 目标索引按面板当前显示的格子计数，被隐藏的键不占格子；隐藏键留在原槽位，
    // 不随可见键的移动而重排。移动后落盘并重建 QT 注册顺序。
    public void MoveQt(string key, int 可见目标索引)
    {
        var qt = PromeSettings.Instance.QuickToggles;
        var hidden = PromeSettings.Instance.HiddenQts;
        var full = GetOrderedQtKeys();
        // 面板可见序列 = 已注册且未被显隐隐藏（模式不可见的键本就不会被重建注册）
        var visibleSet = new HashSet<string>(full.Where(k => qt.ContainsKey(k) && !hidden.Contains(k)));
        if (!visibleSet.Contains(key)) return;
        var visible = full.Where(visibleSet.Contains).ToList();
        var from = visible.IndexOf(key);
        可见目标索引 = Math.Clamp(可见目标索引, 0, visible.Count - 1);
        if (from < 0 || from == 可见目标索引) return;
        var moved = visible[from];
        visible.RemoveAt(from);
        visible.Insert(可见目标索引, moved);
        // 重构全序: 可见槽位按新序填入, 隐藏键保持原槽位
        var result = new List<string>(full.Count);
        var vi = 0;
        foreach (var k in full)
            result.Add(visibleSet.Contains(k) ? visible[vi++] : k);
        QtOrder = result;
        Save();
        APIHelper.重建QT可见性();
    }

    // ============================================================
    // === 热键排列顺序（悬浮面板 / 热键设置页统一取此顺序） ===
    // ============================================================

    // 当前职业热键名的显示顺序：HotkeyOrder 中仍有效的名字按其顺序在前,
    // 热键表新增的名字按定义顺序补尾; HotkeyOrder 为空时即热键表定义顺序（与旧版行为一致）。
    public List<string> GetOrderedHotkeyNames()
    {
        var all = ErosUIJobEnv.HotkeyNames;
        var result = new List<string>(all.Length);
        var seen = new HashSet<string>();
        foreach (var k in HotkeyOrder)
            if (all.Contains(k) && seen.Add(k)) result.Add(k);
        foreach (var k in all)
            if (seen.Add(k)) result.Add(k);
        return result;
    }

    // 把指定热键移动到面板上的目标位置（插入语义），索引口径与 MoveQt 相同：
    // 按面板当前显示的格子计数，被隐藏的名字不占格子。面板每帧按此顺序重排，落盘即可。
    public void MoveHotkey(string name, int 可见目标索引)
    {
        var hidden = HiddenHotkeys;
        var full = GetOrderedHotkeyNames();
        // 面板可见序列 = 未被显隐隐藏
        var visibleSet = new HashSet<string>(full.Where(k => !hidden.Contains(k)));
        if (!visibleSet.Contains(name)) return;
        var visible = full.Where(visibleSet.Contains).ToList();
        var from = visible.IndexOf(name);
        可见目标索引 = Math.Clamp(可见目标索引, 0, visible.Count - 1);
        if (from < 0 || from == 可见目标索引) return;
        var moved = visible[from];
        visible.RemoveAt(from);
        visible.Insert(可见目标索引, moved);
        // 重构全序: 可见槽位按新序填入, 隐藏键保持原槽位
        var result = new List<string>(full.Count);
        var vi = 0;
        foreach (var k in full)
            result.Add(visibleSet.Contains(k) ? visible[vi++] : k);
        HotkeyOrder = result;
        Save();
    }

    // ============================================================
    // === QT 默认值持久化（按模式） ===
    // ============================================================

    // 高难模式专用 QT 默认值
    public Dictionary<string, bool> QtHighEndDefaults = new();

    // 日随模式专用 QT 默认值
    public Dictionary<string, bool> QtDailyDefaults = new();

    // ============================================================
    // === 方法 ===
    // ============================================================

    public Dictionary<string, bool> GetCurrentModeDefaults()
        => IsHighEnd ? QtHighEndDefaults : QtDailyDefaults;

    // 保存当前 QT 状态到指定模式的默认值快照。
    // 注意: 只采集当前模式下可见的 QT——隐藏的 QT 读不到实时值，跳过以免把已有默认值覆盖成 false。
    public void SaveQtSnapshot(bool toHighEnd)
    {
        var dict = toHighEnd ? QtHighEndDefaults : QtDailyDefaults;
        foreach (var key in ErosUIJobEnv.QtAll.Keys)
        {
            if (ErosUIJobEnv.QtIsMetaKey(key)) continue;
            if (!ErosUIJobEnv.QtIsVisibleInMode(key, IsHighEnd)) continue;
            dict[key] = PromeSettings.Instance.GetQt(key);
        }
    }

    // 从指定模式快照恢复 QT 状态
    public void RestoreQtSnapshot(bool fromHighEnd)
    {
        var dict = fromHighEnd ? QtHighEndDefaults : QtDailyDefaults;
        if (dict.Count == 0)
        {
            foreach (var key in ErosUIJobEnv.QtAll.Keys)
            {
                if (ErosUIJobEnv.QtIsMetaKey(key)) continue;
                APIHelper.设置QT(key, ErosUIJobEnv.QtDefault(key));
            }
        }
        else
        {
            foreach (var key in ErosUIJobEnv.QtAll.Keys)
            {
                if (ErosUIJobEnv.QtIsMetaKey(key)) continue;
                APIHelper.设置QT(key, dict.TryGetValue(key, out var v) ? v : ErosUIJobEnv.QtDefault(key));
            }
        }
    }

    // 模式切换（QT 自动重置为目标模式的默认值）
    public void SwitchMode(bool toHighEnd)
    {
        if (IsHighEnd == toHighEnd) return;
        IsHighEnd = toHighEnd;
        // 切模式即重置为目标模式的默认 QT（QT面板页默认值子视图配置，缺省走出厂默认）；
        // 不再用切换瞬间的实时状态覆盖默认值——想存当前状态请用 QT面板页的「从当前QT导入」
        RestoreQtSnapshot(toHighEnd);
        // 重建 QT 可见性（按模式归属过滤）——否则面板不同步
        APIHelper.重建QT可见性();
        Save();
    }


    // ============================================================
    // === JSON 持久化 ===
    // ============================================================
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        IncludeFields = true,
    };

    // 配置目录（稳定路径，与进程无关）：
    // 插件配置目录\PromeRotation\Settings\ACRConfig\<作者>——宿主 ACRAuthorSetting 按作者约定的
    // 配置目录; 作者身份经 ErosUIJobEnv.Configure 注入（默认占位「author」, 使用方改成自己的作者名,
    // 与 RotationMetadata 的 Author 一致, 详见 README）; 目录不存在时由 FilePath 首次访问自动建。
    public static string SettingsDirectory
    {
        get
        {
            // 同样要求先注入职业环境，否则配置会写到占位作者名 "author" 的目录里
            if (!ErosUIJobEnv.Configured)
                throw new InvalidOperationException(
                    "请先调用 ErosUIFramework.Configure 注入职业环境，再读写设置。");
            return ACRAuthorSetting.GetSettingsDirectory(ErosUIJobEnv.作者);
        }
    }

    // 配置文件名按当前职业: JobTag 由使用方经 ErosUIJobEnv.Configure 注入, 以它为准。
    // 不读 Core.Me——宿主切职业/登录加载存在 ClassJob.RowId 未落地(仍报旧职业)的窗口期,
    // 登录或切职业的瞬间读取玩家职业可能拿到旧值，据此加载会把配置绑到错误职业的文件上。
    public static string 职业文件名 => ErosUIJobEnv.JobTag;

    public static string FilePath
    {
        get
        {
            try
            {
                var dir = SettingsDirectory;
                if (!System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);   // 已存在时为 no-op
                return System.IO.Path.Combine(dir, 职业文件名 + ".json");
            }
            catch (Exception e) when (e is not InvalidOperationException)
            {
                // 宿主目录拿不到时的兜底路径；文件名保持按职业分，和正常路径口径一致
                return System.IO.Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                    "XIVLauncherCN", "pluginConfigs", "PromeRotation", "Settings", "ACRConfig",
                    ErosUIJobEnv.作者, 职业文件名 + ".json");
            }
        }
    }

    public static ErosUISettings Load()
    {
        try
        {
            if (System.IO.File.Exists(FilePath))
            {
                var json = System.IO.File.ReadAllText(FilePath);
                var s = JsonSerializer.Deserialize<ErosUISettings>(json, JsonOptions);
                if (s != null)
                {
                    return Normalize(s);
                }
            }
            else
            {
                // 首次使用（用户配置不存在）：加载内嵌默认配置并立即落盘生成用户文件
                var s = LoadEmbeddedDefaults();
                if (s != null)
                {
                    var loaded = Normalize(s);
                    loaded.Save();
                    PluginLog.Log($"[{ErosUIJobEnv.作者}] 首次使用，已写入默认配置: {FilePath}");
                    return loaded;
                }
            }
        }
        catch (Exception e) { PluginLog.Error($"[{ErosUIJobEnv.作者}] 设置加载失败: {e.Message}"); }
        return new ErosUISettings();
    }

    // 读取首次使用的出厂默认配置：优先用使用方注入的 JSON（ErosUIFramework.Configure 的
    // defaultSettingsJson 参数），没注入再找框架 DLL 内嵌的 Resources/Default{职业}.json，都没有返回 null
    private static ErosUISettings? LoadEmbeddedDefaults()
    {
        try
        {
            var json = ErosUIJobEnv.DefaultSettingsJson
                       ?? ReadEmbeddedDefaultJson($"Default{职业文件名}.json");
            return json == null ? null : JsonSerializer.Deserialize<ErosUISettings>(json, JsonOptions);
        }
        catch (Exception e)
        {
            PluginLog.Error($"[{ErosUIJobEnv.作者}] 内嵌默认配置解析失败: {e.Message}");
            return null;
        }
    }

    // 读取内嵌出厂配置资源文本（Resources/Default*.json，随 DLL 发布; 资源缺失返回 null）。
    // 目前只有按职业设置（本类）的首次使用出厂配置走此入口。
    internal static string? ReadEmbeddedDefaultJson(string fileName)
    {
        try
        {
            using var stream = typeof(ErosUISettings).Assembly
                .GetManifestResourceStream($"ErosUI.Resources.{fileName}");
            if (stream == null)
            {
                PluginLog.Warning($"[{ErosUIJobEnv.作者}] 未找到内嵌默认配置资源: {fileName}");
                return null;
            }
            using var reader = new System.IO.StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch (Exception e)
        {
            PluginLog.Error($"[{ErosUIJobEnv.作者}] 内嵌默认配置读取失败: {fileName}, {e.Message}");
            return null;
        }
    }

    // 载入后的统一规整：按当前职业 QT 表修剪显隐记录与排序表
    private static ErosUISettings Normalize(ErosUISettings s)
    {
        var qt = ErosUIJobEnv.QtAll;
        // QtVisible 双模式显隐记录修剪: 剔除当前职业 QT 表已不存在的死键
        // （出厂/旧版配置残留; 缺省未配置 = 显示, 无需补齐）
        foreach (var k in s.QtVisibleHighEnd.Keys.Where(k => !qt.ContainsKey(k)).ToList())
            s.QtVisibleHighEnd.Remove(k);
        foreach (var k in s.QtVisibleDaily.Keys.Where(k => !qt.ContainsKey(k)).ToList())
            s.QtVisibleDaily.Remove(k);
        // QtOrder 修剪: 当前职业 QT 表已不存在的键剔除、重复项去重
        s.QtOrder = s.QtOrder.Where(qt.ContainsKey).Distinct().ToList();
        // HotkeyOrder 修剪: 当前职业热键表已不存在的名字剔除、重复项去重
        s.HotkeyOrder = s.HotkeyOrder.Where(ErosUIJobEnv.HotkeyNames.Contains).Distinct().ToList();
        return s;
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, JsonOptions);
            var dir = System.IO.Path.GetDirectoryName(FilePath);
            if (dir != null && !System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(FilePath, json);
        }
        catch { /* 写失败静默 */ }
    }
}
