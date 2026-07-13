# GestureClip v0.6.19 Beta

## 下载

- [GestureClip-v0.6.19-beta-win-x64.zip](https://github.com/hhuhuwang-gif/GestureClip.App/releases/download/v0.6.19-beta/GestureClip-v0.6.19-beta-win-x64.zip)
- SHA256SUMS.txt

解压后双击 `GestureClip.exe`。

## 本次修复（建议升级）

**智能粘贴开启后无法粘贴**

- 智能粘贴只做尽力改写剪贴板，注入**统一**走与关闭时相同的 `Ctrl+V`。
- 纯文本无变化时跳过 OpenClipboard，减少跨机竞态。
- 目标已是前台时不抢焦点；SendInput → keybd_event → WM_PASTE。

**下滑手势弹出右键菜单**

- 粘贴时不再注入合成鼠标抬起（RIGHTUP 会触发右键菜单）。
- 手势后短时拦截残留右键抬起。

## 0.6.18 起仍包含

- 下滑手势粘贴焦点恢复、x64 INPUT 布局、HUD 先藏再注入

## 数据

`%LOCALAPPDATA%\GestureClip\` 覆盖程序不会删除。
