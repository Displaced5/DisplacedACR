# 构建 Displaced VPR

需要 Windows、PowerShell 7 和 .NET 10 SDK。

## 公开 SDK 构建

```powershell
./build.ps1 -UseSdk
```

脚本使用固定版本的 `PromeRotation.SDK.CNGL`，构建 ACR 与独立起手脚本，运行纯逻辑回归检查，并生成 `dist/Displaced.zip` 与 `dist/repo.json`。不需要提交宿主 DLL 或游戏数据。没有实际宿主时会明确跳过加载器验证。

发布包内 DLL 位于 ZIP 根目录；只打包明确列出的运行文件与说明、许可证，不打包配置、诊断日志、PDB、宿主依赖或游戏素材。

## 使用实际宿主

```powershell
./build.ps1 -DalamudDir $env:DALAMUD_HOME -PromeDir $env:PROME_HOME
```

提供的目录应分别包含 `Dalamud.dll` 与 `PromeRotation.dll`。脚本会调用实际 PR 的包加载器与元数据校验器，检查 `ViperACR` 包身份、接口、起手、设置和事件契约，不连接游戏或发送技能。

公开 SDK 构建也可提供上述两个目录，在打包前验证 SDK 产物与实际宿主的兼容性。加 `-Install` 会备份并更新当前 Windows 用户的 ACR/ViperACR，保留配置文件。

若只编译主工程：

```powershell
dotnet build ViperACR.csproj -c Release -p:UseInstalledHost=false
```

等级同步使用逐技能解锁判断；100级标准起手仅在满足技能、资源、开关和目标条件时启用。离线检查不等于游戏内实战验证。

## 可选数据工具

`tools/HostTools` 的游戏数据导出模式通过 `FFXIV_SQPACK` 环境变量读取游戏的 `sqpack` 路径；普通数据导出也可将目录作为首参数。数据工具生成的游戏数据和图标不属于发布内容。
