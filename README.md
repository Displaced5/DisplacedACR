# Displaced VPR

适用于 PromeRotation 国服 API 15 的蝰蛇剑士 PVE ACR。

发布包名称为 `Displaced.zip`，ACR 列表与设置界面中的名称为 **Displaced VPR**。

## 安装

在 PromeRotation 的 ACR 订阅/仓库管理中添加以下清单地址，然后下载安装：

```text
https://github.com/Displaced5/Displaced/releases/latest/download/repo.json
```

发布页面：[GitHub Releases](https://github.com/Displaced5/Displaced/releases/latest)。

手动安装时，将压缩包内容解压到 PromeRotation 配置目录下的 `ACR/ViperACR`，然后刷新外部 ACR，选择 **Displaced VPR**。压缩包内的 DLL 位于根目录，请勿再套一层目录。

`ViperACR` 是宿主校验所用的包身份，也是现有配置目录名称。保留此名称以延续已有设置；它与界面显示名称不同。

## 功能

- 日随模式使用动态循环；高难模式根据起手选择使用标准或动态起手。
- 内置标准起手按实时身位安排盘蛇顺序，倒计时蛇行为 500ms。
- 提供 QT、技能悬浮面板、循环设置、拓展功能与开发诊断。
- 配置与诊断记录保存在本机，不包含在发布包中。

## 源码与构建

Displaced VPR 在本仓库公开源码，采用 [MIT 许可证](LICENSE)。源码包含循环、UI 适配、独立起手脚本与通用回归测试。私人配置、诊断日志、游戏数据及宿主 DLL 不纳入仓库。

使用 .NET 10 SDK 和 PowerShell 7 执行 `./build.ps1 -UseSdk`，可通过公开 SDK 构建 `dist/Displaced.zip` 与 `dist/repo.json`。提供 `-DalamudDir` 和 `-PromeDir` 可使用实际宿主校验，加 `-Install` 会备份并更新本机安装。详见 [构建说明](docs/BUILD.md) 与 [发布约定](docs/RELEASE.md)。

离线检查与包加载验证不等于游戏内实战验证。

UI 使用经过适配的 ErosUI；其 MIT 许可证见 `vendor/ErosUI/LICENSE`，发布包附带 `ErosUI-LICENSE.txt`。

发布格式参考 [MirukuACR](https://github.com/MilkVio/MirukuACR)。
