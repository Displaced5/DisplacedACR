# Displaced VPR

适用于 PromeRotation 国服 API 15 的蝰蛇剑士 PVE ACR。

发布包名称为 `Displaced.zip`，ACR 列表与设置界面中的名称为 **Displaced VPR**。

## 安装

在 PromeRotation 的 ACR 订阅/仓库管理中添加以下清单地址，然后下载安装：

```text
https://github.com/Displaced5/DisplacedACR/releases/latest/download/repo.json
```

发布页面：[GitHub Releases](https://github.com/Displaced5/DisplacedACR/releases/latest)。

若仓库为私有，访问源码和发布资产需要相应 GitHub 权限；匿名订阅下载可能不可用。

手动安装时，将压缩包内容解压到 PromeRotation 配置目录下的 `ACR/Displaced`，然后刷新外部 ACR，选择 **Displaced VPR**。压缩包内的 DLL 位于根目录，请勿再套一层目录。

ACR 作者与加载目录为 `Displaced`，显示名称为 `Displaced VPR`。DLL 文件名继续为 `ViperACR.dll`。

从旧版升级时，缺少的新作者配置会从旧 `ViperACR` 目录迁移，已有新配置优先。使用构建脚本安装会将旧包移到 ACR 扫描目录外备份。手动升级请先将旧 `ACR/ViperACR` 中的 `settings.json` 复制到新目录，并将 `Settings/ACRConfig/ViperACR` 的 JSON 复制到 `Settings/ACRConfig/Displaced`（不覆盖已有配置），然后将旧包移出 ACR 目录，避免同时加载两个循环。

## 功能

- 日随模式使用动态循环；高难起手方案依次为100级标准起手、100级绝妖星起手、90级绝龙诗起手、80级绝亚起手。旧动态起手配置回退为默认标准起手。
- 日随模式在不打断现有连招的前提下，优先使用祖灵降临；非 8 人小队在目标血量低于 20% 时不启动祖灵降临或蛇灵气，8 人小队解除此限制。
- 两种模式与内置标准起手优先消耗祖灵降临预备；保留正在进行的蛇剑／蟒剑及祖灵连段，基础连击按现有保连规则处理。
- 内置标准起手按实时身位安排盘蛇顺序，倒计时蛇行为 500ms。
- 高难起手方案新增 80 级绝亚起手：倒数 4 秒真北、500ms 蛇行，固定 11 个 GCD，第二 GCD 后按爆发药开关使用药品；该方案保持指定盘蛇顺序。
- 高难起手方案提供 90 级绝龙诗起手：500ms 蛇行，第一 GCD 后蛇灵气、第二 GCD 后爆发药、第三 GCD 后真北，固定 15 个 GCD；使用 90 级可用的四式祖灵之牙，不包含祖灵之蛇与祖灵大蛇牙。药品和真北分别遵循现有 QT，盘蛇顺序固定。
- 100级绝妖星起手：500ms蛇行，穿裂尖齿后蛇灵气，固定盘蛇与双牙顺序，接祖灵降临、四式祖灵之牙及祖灵之蛇、祖灵大蛇牙，最后背裂獠齿与蛇尾击；共12个GCD，不安排起手药品或真北，结束后恢复正常循环。
- 提供 QT、技能悬浮面板、循环设置、拓展功能与开发诊断。
- Hotkey 面板提供手动极限技按钮，按当前极限槽选择 LB1／LB2／LB3；无槽或目标无效时不提交。
- Hotkey 面板提供“爆发药”按钮，手动使用宿主按职业推荐的背包药品（优先HQ），无需开启自动用药QT或选择敌方目标；无药、冷却中或角色不可行动时不提交。
- 蛇灵气可在祖灵期间的安全窗口使用，仍保留续剑优先、资源防覆盖及游戏可用性检查。
- 拓展功能提供默认开启的三插开关；关闭时恢复原有插入次数上限，开启仍受动画锁、提交间隔和安全窗口约束。
- 配置与诊断记录保存在本机，不包含在发布包中。

## 源码与构建

Displaced VPR 源码保存在本仓库，采用 [MIT 许可证](LICENSE)，访问范围遵循仓库权限。源码包含循环、UI 适配、独立起手脚本与通用回归测试。私人配置、诊断日志、游戏数据及宿主 DLL 不纳入仓库。

使用 .NET 10 SDK 和 PowerShell 7 执行 `./build.ps1 -UseSdk`，可通过公开 SDK 构建 `dist/Displaced.zip` 与 `dist/repo.json`。提供 `-DalamudDir` 和 `-PromeDir` 可使用实际宿主校验，加 `-Install` 会备份并更新本机安装。详见 [构建说明](docs/BUILD.md) 与 [发布约定](docs/RELEASE.md)。

离线检查与包加载验证不等于游戏内实战验证。

UI 使用经过适配的 ErosUI；其 MIT 许可证见 `vendor/ErosUI/LICENSE`，发布包附带 `ErosUI-LICENSE.txt`。

发布格式参考 [MirukuACR](https://github.com/MilkVio/MirukuACR)。
