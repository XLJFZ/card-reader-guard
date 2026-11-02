# macOS 原生测试版

这是独立实现的 macOS 12+ 应用，使用 AppKit、IOKit 和 Disk Arbitration。目标是保留 Windows 版的使用方式：用户手动确认读卡器，每只设备独立计时，平时没有 Dock 或菜单栏常驻图标，安全弹出成功时通知。

当前是开发测试版。即使自动构建和逻辑检查通过，也不代表真实 Mac 读卡器已经完成验收。

## 使用

完整解压 ZIP，将 `Card Reader Guard.app` 放到长期保留的位置，再打开管理页。插入 USB 读卡器和卡后，点击“添加读卡器”，核对型号、容量、BSD 设备名和挂载位置，勾选确认。候选列表可能包含移动硬盘，必须由用户确认目标是读卡器。

每只设备可命名、设置 1–120 分钟、暂停、删除或更换。点击“保存并静默运行”进入后台，再次打开应用即可管理。登录启动可在管理页选择；成功提示需要用户授予通知权限。关闭通知不会强制中断监测，但成功提示可能不可见。

本应用没有菜单栏常驻图标。需要停止时，打开管理页点击“退出监测”；需要取消登录启动时，先取消勾选登录启动并保存。应用打开期间暂停新的自动弹出请求，关闭后重新计时。已提交给系统的安全卸载请求不能通过打开管理页撤回。

## 绑定与安全范围

- 绑定 USB 厂商 ID、产品 ID、USB 序列号、接口位置及 USB 产品名称；不按盘符或 `diskN` 自动匹配。换接口可能需要重新绑定。报告完全相同标识的硬件仍无法被可靠区分。
- 只支持能取得设备自身读写统计的外接 USB 存储节点。缺少序列号、统计缺失、同一 USB 节点下有多块整盘介质、内部设备和不明确的设备都会被跳过。
- 初版保守排除 APFS 介质，以及根卷、应用所在卷和配置所在卷，避免影响系统盘及运行环境。内置 SD 读卡器、多槽设备、Thunderbolt 拓扑等未覆盖。
- 使用 IOKit 的累计读写字节数和操作次数判断空闲，不扫描卡内文件。计数变化、介质身份变化、统计缺失、超过 30 秒的采样间隔、系统唤醒和管理页关闭后重新计时。
- 达到设定时间后先再次核对身份和统计，再请求整盘安全卸载。系统拒绝时不继续弹出、不强制断开、不关闭占用文件的程序。成功卸载后再次核对物理介质身份，再请求系统弹出。
- macOS 版弹出的是目标整盘介质，不保证停止 USB 读卡器控制器或 USB 断电，也不保证降温。再次使用可能需要拔插。

## 配置与登录启动

本地绑定位于 `~/Library/Application Support/CardReaderGuard/devices.json`，状态日志在同目录。Windows 配置不会自动导入。日志不记录卡内文件名或内容。

登录启动使用当前用户的 `~/Library/LaunchAgents/io.github.xljfz.CardReaderGuard.plist`，在下次登录时按保存的应用路径启动。应用移动后应重新保存登录启动设置。应用不安装系统守护进程，不要求管理员权限，也不联网。

## 构建和验证

在已安装 Xcode Command Line Tools 的 Mac 上运行：

```sh
bash macos/build.sh
```

生成 Intel 与 Apple Silicon 通用应用及便携 ZIP，并执行不调用卸载或弹出接口的安全检查。`--self-test` 用于逻辑检查；`--diagnostics` 仅输出当前支持的外接 USB 候选数量，不提交卸载或弹出请求。

CI 构建使用临时 ad-hoc 签名，尚无 Developer ID 签名及 Apple 公证。它不是经过公证的正式分发版；能否直接打开受 macOS 安全策略限制。这里不提供关闭系统安全防护的方法。可在自己的 Mac 上从源码构建；正式分发需要后续完成签名、公证和实机验收。

实机验收仍需检查：正确识别读卡器、移动硬盘不被影响、持续读写时不提前弹出、文件占用时不强制断开、重新插拔、睡眠唤醒、通知、登录启动，以及 Retina / 深浅色管理界面。

## 参考

- [Apple：Disk Arbitration 卸载和弹出](https://developer.apple.com/documentation/diskarbitration/dadiskunmount(_:_:_:_:))
- [Apple：IORegistryEntryGetParentEntry](https://developer.apple.com/documentation/iokit/1514454-ioregistryentrygetparententry)
- [Apple：UNUserNotificationCenter](https://developer.apple.com/documentation/usernotifications/unusernotificationcenter)
