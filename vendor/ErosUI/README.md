# ErosUI

基于 PromeRotation 的 ACR 通用 UI 框架，从 ErosACR 的默认主题中分离而来。接入后你的 ACR 直接获得一套完整的界面：

- QT / Hotkey 悬浮面板：按钮显隐、右键拖拽排序、布局可调（QT 面板按 Category 分类分组）
- 战斗控制条：运行 / 停手 / 主动攻击 / 设置入口
- 日夜双主题，主色 RGB 自定义

框架本身不包含任何职业逻辑。QT 开关表、热键按钮等内容全部由使用方注入，
框架不预置任何按钮或技能，接入什么就显示什么。

## 快速开始

全部 API 集中在 `ErosUIFramework` 一个类，只需 `using ErosUI;`。

1. 引用本工程（项目引用，或编译产物 `ErosUI.dll`）
2. 在 Rotation 构造函数最前面注入职业环境
3. 在生命周期回调里成对调用 Install/Uninstall（各一个调用即可，内部已包含
   热键面板构建、QT 注册与设置落盘）

```csharp
using ErosUI;

// 2. 注入职业环境
// 注意：MyQT、MyJobNames、CommonNames、ExecuteLogic 都是占位名，
// 换成你自己 ACR 里的类型，这段示例不能直接复制编译
ErosUIFramework.Configure(
    jobTag: "SAM",
    jobName: "武士",
    qtAll: MyQT.All,                       // QT 开关表：键 → 默认值
    qtIsMetaKey: MyQT.IsMetaKey,           // 元键（不进面板）
    qtIsVisibleInMode: MyQT.IsVisibleInMode, // 高难/日随各自的可见性
    qtDefault: MyQT.Default,
    qtCascadeRules: MyQT.CascadeRules,     // QT 联动表
    hotkeyNames: [.. MyJobNames, .. CommonNames],
    buildHotkeys: b =>
    {
        b.Fixed("疾跑", 7571u, ActionType.OffGcd, ActionTargetType.Self);
        b.Execute("爆发药", new ExecuteLogic(() => { /* ... */ }));
    },
    author: "你的作者名");   // 见下文「作者身份」
```

```csharp
// 3. 生命周期：严格成对调用
public void OnEnterAcr() => ErosUIFramework.Install();
public void OnExitAcr() => ErosUIFramework.Uninstall();
```

需要时还可直接使用门面上的其余入口：`设置QT`（带联动写入）、`重建QT可见性`
（切换模式后重注册）、`SaveSettings`（立即落盘）、`QtPanelVisible` /
`SetQtPanelVisible` 与 `HotkeyPanelVisible` / `SetHotkeyPanelVisible`（两个悬浮
面板各自独立控制）、`OpenSettings`、`DrawSettingsEntry`（宿主 DrawSettings 入口按钮）。

## 模块组成

| 组件 | 说明 |
|------|------|
| `ErosUIFramework` | 唯一公共入口：Configure 注入、Install/Uninstall 装卸（内含热键/QT/落盘）、面板开关 |
| `Common/UI/SettingsWindowBase*` | 设置窗口（侧边栏页签布局 + 主题页） |
| `Common/UI/ErosUISettingsUI.cs` | 设置页内容（基础设置 / Hotkey / QT面板 三页） |
| `Common/UI/ErosUIQtPanelWindow.cs` | QT 悬浮面板（按 Category 分组网格、组内拖拽排序、按模式记忆显隐） |
| `Common/UI/ErosUIHotkeyPanelWindow.cs` | 热键悬浮面板（图标、冷却、队列待发提示） |
| `Common/UI/CombatControlWindow*.cs` | 战斗控制条（运行/停手/主动攻击/设置入口） |
| `Common/UI/SimplePalette.cs` | 日间/夜间双主题色板，主题页可自定义主色 |
| `Common/Data/` | 配置持久化（通用 + 按职业分档的 JSON） |
| `ErosUIHotkeyBuilder` | 热键面板条目构建器（Configure 注入用） |

## 依赖

- .NET 10.0 Windows
- PromeRotation

## 目录结构

```
ErosUI/
├── ErosUI.csproj
├── Common/
│   ├── APIHelper.cs     （QT 联动写入与可见性重建，内部使用）
│   ├── Data/            （职业环境、设置持久化、主题模式）
│   ├── Helper/          （热键构建器）
│   └── UI/              （框架门面与全部窗口控件）
```

## 作者身份（author）

框架内所有对外可见的「自称」统一为一个作者名字段，经 `Configure` 的 `author`
参数注入，**默认占位值为 `author`，使用方必须改成自己的作者名**。它决定：

- **配置目录**：`pluginConfigs\PromeRotation\Settings\ACRConfig\<作者>\`——因此必须与
  你 `RotationMetadata` 的 Author 参数一致，否则配置目录对不上
- **窗口标题前缀**：各窗口标题以 `{作者}` 开头（窗口名同时是宿主 WindowSystem 的
  键，改名会丢对应窗口的位置记忆）
- **日志前缀**：框架日志行统一为 `[<作者>] ...`

## 配置落盘位置

- 通用（主题等）：`pluginConfigs\PromeRotation\Settings\ACRConfig\<作者>\Common.json`
- 按职业：同目录 `{JobTag}.json`（如 `SAM.json`）
- 首次使用按代码默认值自动落盘；想给某个职业预置出厂配置，把 `Default{JobTag}.json`
  放进本工程的 `Resources` 目录（会打进 DLL），或在 `Configure` 里传
  `defaultSettingsJson` 直接注入 JSON 文本，二者取其一即可（缺省同样走代码默认值）

注意：必须先调用 `Configure` 再读写设置。未注入职业环境就访问 `ErosUISettings.Instance`
会直接抛异常——这是为了防止按空 QT 表误清用户已存的配置。

## 许可证

MIT，见 [LICENSE](LICENSE)。
