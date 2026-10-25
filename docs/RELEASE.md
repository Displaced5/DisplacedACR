# 发布约定

显示名：Displaced VPR。仓库名与发布包名：Displaced。
加载身份 `Author`、程序集和安装目录均为 `ViperACR`，用于宿主包校验与配置兼容。

1. 同步修改 `Directory.Build.props` 和 `Core/ReleaseInfo.cs` 的版本号。
2. 使用 `./build.ps1 -UseSdk` 构建；发布前指定实际宿主目录完成加载器验证。
3. 审核 Git 暂存内容与 ZIP 文件。排除个人路径、私人记录、截图、凭据、配置、日志、反编译参考文件与 PDB。保留 ErosUI 许可证及修改说明。
4. 提交源码并创建 `v<版本>` 标签。发布同一提交构建的 `Displaced.zip` 和 `repo.json`。
5. 发布后通过公开下载链接重新下载资产，校验清单 SHA-256、版本、DLL 和 ZIP 根目录结构，再将同一 DLL 部署到本机。

`repo.json` 的 `apiVersion` 为 15，职业为 VPR，`contentScope` 为 All。`referencePromeVersion` 取自实际编译使用的引用程序集，不冒充运行宿主版本。`sha256` 对应完整 ZIP。

安装订阅地址：

```text
https://github.com/Displaced5/Displaced/releases/latest/download/repo.json
```

压缩包下载地址：

```text
https://github.com/Displaced5/Displaced/releases/latest/download/Displaced.zip
```

发布格式参考 MirukuACR 的 ZIP/清单约定；Displaced 的构建和发布脚本独立维护。
