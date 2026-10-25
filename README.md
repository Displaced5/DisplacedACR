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

## 分发

GitHub 公开仓库仅提供编译后的安装包、订阅清单、使用说明与第三方许可证。Displaced VPR 的 ACR 源码不公开，发布包不附带独立起手脚本源码。

UI 使用经过适配的 ErosUI；其 MIT 许可证见 `vendor/ErosUI/LICENSE`，发布包附带 `ErosUI-LICENSE.txt`。

发布格式参考 [MirukuACR](https://github.com/MilkVio/MirukuACR)。
