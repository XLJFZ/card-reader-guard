# Windows 安装程序

用 Inno Setup 6 把 `dist` 里的便携版打包成一次点击即可完成的安装程序。

## 构建

先构建便携版，产物在 `dist`：

```powershell
./build.ps1
```

再编译安装程序脚本：

```powershell
& "$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe" installer\CardReaderGuard.iss
```

输出 `installer/output/CardReaderGuard-Setup-0.2.4-x64.exe`。CI 在每次推送时执行同样的两步，把安装包上传为构建产物。

## 安装行为

- 安装到 `%LOCALAPPDATA%\Programs\CardReaderGuard`，按当前用户安装，不弹 UAC，不改系统目录。
- 开始菜单建三项：打开管理页、`--stop` 停止后台监测、卸载。
- 桌面快捷方式和“登录时启动”都是可选项，默认不勾选；后者以 `--background` 启动，不弹管理页。
- 卸载会清除 `status.log`、`manage.flag`、`stop.flag`、`appearance.txt` 这些运行时文件，但保留 `device.xml`，重装后读卡器绑定仍在；因此有绑定记录时程序目录不会自动消失，需要彻底清理时手动删除该文件。

## 维护

`CardReaderGuard.iss` 里的 `AppVersion` 与 `AssemblyInfo.cs` 的 `AssemblyFileVersion` 必须一致，CI 在编译前会比较两者，不一致直接失败。发版时两处一起改。
