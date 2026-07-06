# GestureClip v0.6.2 Beta

## 下载

- `GestureClip-v0.6.2-beta-win-x64.zip`
- `SHA256SUMS.txt`

## 这次更新

- 新增真正的一键覆盖更新安装器。
- 设置页「一键覆盖更新」会自动下载 GitHub Latest Release 的 Windows x64 zip。
- 托盘菜单也提供「一键覆盖更新」。
- 下载后会解压到临时目录，退出当前 GestureClip 后覆盖旧版程序文件并自动重启。
- 覆盖安装会保留本地数据库、剪贴板历史、设置和日志目录。
- 如果自动更新失败，会打开 GitHub 最新 Release 页面供手动下载。

## 安装 / 覆盖更新

首次安装：下载并解压 `GestureClip-v0.6.2-beta-win-x64.zip`，双击 `GestureClip.exe`。

老用户以后可以在应用内使用：

```text
设置页 → 关于 → 一键覆盖更新
```

或右下角托盘菜单：

```text
一键覆盖更新
```

## 本地数据说明

覆盖程序文件不会删除本地历史记录和设置；默认数据保存在：

```text
%LOCALAPPDATA%\GestureClip
```

## 校验

下载后可使用 `SHA256SUMS.txt` 校验 zip 文件完整性。

## 已知说明

- 当前 Beta 包未签名，Windows SmartScreen 可能提示未知发布者。
- 管理员权限窗口中，普通权限运行的 GestureClip 可能无法稳定执行输入模拟。
- 覆盖更新前必须先退出程序，否则 `GestureClip.exe` 可能被占用。
