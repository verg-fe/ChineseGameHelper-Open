# 中文游戏助手 · 开源版

Windows 中文游戏组件管理器，独立的 MIT 源码项目。提供游戏定位、配置组合、备份恢复与 WebView2 网页视频浏览器。第三方图形算法、模型和游戏不属于本项目。

**本版不内嵌、不下载、不修补神经运行库或模型。** 不能将“助手开源”理解为 DLSS、XeSS 或其他厂商技术全部开源。参见 [NOTICE.md](NOTICE.md)。

## 使用

解压完整便携包，运行 `中文游戏助手-开源版.exe`。不要只复制 EXE：网页浏览器需要旁边的 `browser/sdk` 文件。

1. 扫描游戏或手动添加 EXE。扫描结果只是路径线索，不保证兼容。
2. 进入 **外部组件 / 许可**，选择自己合法取得的原版安装器、桥接目录或 Magpie。
3. OptiScaler 等组合包在 **组合 / 帧生成 → 选择目录** 导入。“官方来源”只打开上游页面。
4. 安装前退出游戏。已有未知模组或文件校验不匹配时停止，避免覆盖。
5. 网页视频浏览器可普通播放网页与 MP4。“外部增强”只启动用户选择的 Magpie，保留它自己的配置与快捷键，不代表增强已经生效。

本地路径验证不等于安全认证。安装器使用已适配版本的固定 SHA256；外部 Magpie 只检查文件名及 x64 PE 类型，用户需自行确认来源可信。选择路径不立即运行组件。

## 与个人试验版隔离

- 数据目录：`%LOCALAPPDATA%\ChineseGameHelper.Open`，不读取原个人版缓存、账号或浏览器资料。
- 使用独立单实例锁，不替换桌面旧版 EXE。
- 不自动继承旧安装记录。**原个人版安装的模组，先用原个人版恢复**，再由开源版接管；否则按已有文件冲突处理。
- 桥接 0.3/0.4 入口仅检查本地匹配目录，保留既有适配识别；不提供受限制的运行库、权重或二进制补丁。存在适配代码不授予用户修改或分发第三方软件的权利。
- 不承诺所有老游戏、联网模式或反作弊兼容，不自动关闭反作弊。

## 从干净源码构建

环境：Windows 10/11、.NET Framework 4.8、Windows PowerShell 5.1 或 PowerShell 7。浏览器运行需已安装 Microsoft Edge WebView2 Runtime。

```powershell
./build.ps1
./test.ps1
./package.ps1
```

首次构建只从 NuGet 官方源取得固定版本 `Microsoft.Web.WebView2 1.0.4191.47`，校验 SHA256 后使用 SDK。不会下载神经运行库、游戏插件或模型。浏览器运行时本身不在构建包内。

SDK 缓存齐备后可 `./build.ps1 -Offline`。输出位于 `dist`。`package.ps1` 通过白名单生成源码包与便携包；源码包不包含 SDK DLL、EXE、私有缓存或用户路径。

## 测试范围

四组自检涵盖配置、文件冲突保护、安装恢复、版本切换、缺失组件处理、错误哈希拒绝、无内嵌资源检查。老游戏测试使用不可执行的合成文件，不需要任何第三方神经运行库。这些测试不证明 GPU 渲染、帧率或游戏兼容性。

## 目录

- `src/Program.cs`：主界面、扫描、原版工具调用与恢复。
- `src/StackManager.cs`：组合配置与事务记录。
- `src/LegacyBridge.cs`：本地桥接包校验及部署管理；不包含运行库实现。
- `src/Browser.cs`：WebView2 宿主与用户选择的外部增强程序生命周期。
- `src/ExternalComponents.cs`：组件路径与许可入口。
- `docs/`：发布边界、依赖与安全说明。

## 许可证与归属

本仓库的独立 C# 管理代码和构建脚本按 MIT 提供；第三方软件始终保留自己的许可。没有将 OptiScaler、Magpie 或神经网络的源码复制进本仓库，也不声称这些算法由助手开发。

涉及项目：Daniel Blanco 的 DLSS-NR-on-AMD、cLohan / zmodelerlover 的 dlss5-neural-amd、OptiScaler contributors、**AMDNR by 3zwr1 — https://github.com/3zwr1/AMD-NR---OptiScaler**、Magpie contributors、Microsoft WebView2。完整来源见 NOTICE。

DLSS、FSR、XeSS 及游戏名称属于相应权利人；本项目不代表 NVIDIA、AMD、Intel、Microsoft 或游戏厂商，也没有官方背书。
