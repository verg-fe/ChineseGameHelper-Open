# 第三方边界与致谢

本项目只为独立编写的助手管理代码授予 MIT 许可。用户选择的插件不因此变成 MIT 软件。

| 项目 | 归属 / 许可边界 | 本仓库状态 |
|---|---|---|
| DLSS-NR-on-AMD | Daniel Blanco，自定义限制性许可 | 无安装器、代理 DLL、运行库、修改后二进制或补丁；只支持用户选定的本地安装器和组件路径 |
| dlss5-neural-amd | cLohan；上游桥接源码为 MIT | 无桥接原生源码及二进制，本仓库只有独立的安装/配置管理代码 |
| OptiScaler | OptiScaler contributors，GPL-3.0 | 不随包提供，导入用户本地目录 |
| AMD-NR | AMDNR by 3zwr1 — https://github.com/3zwr1/AMD-NR---OptiScaler；GPL-3.0及其署名声明 | 不随包提供；许可与源码可得性需按实际采用版本核对 |
| Magpie | 相应上游/fork 作者，GPL-3.0 | 不随包提供；仅启动所选程序，普通上游版不等于带神经网络的扩展版 |
| NVIDIA 模型 / DLL | 相应权利人；非助手自产 | 无 DLL、BIN、PAK、模型或提取代码 |
| Intel XeSS、AMD FidelityFX、DLSS Enabler 等 | 各自作者与许可证 | 不随包提供，不能以助手许可替代其条款 |
| Microsoft.Web.WebView2 SDK | Microsoft，采用包内 LICENSE.txt / NOTICE.txt | 源码包不含 SDK；构建从官方 NuGet 取得固定包，便携包保留 SDK 许可及通知 |
| WebView2 Runtime | Microsoft 的独立运行时条款 | 不随包提供，使用系统已经安装的运行时 |

上游来源：

- https://github.com/danielblnc/DLSS-NR-on-AMD （尤其 LICENSE）
- https://github.com/zmodelerlover/dlss5-neural-amd
- https://github.com/optiscaler/OptiScaler
- https://github.com/3zwr1/AMD-NR---OptiScaler
- https://github.com/Blinue/Magpie
- https://github.com/artur-graniszewski/DLSS-Enabler
- https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.4191.47

“其他作者获得了特别授权”不意味着本项目获得相同授权。本版不提供受限制组件的镜像或打包下载，亦未公开原个人试验版缓存。
