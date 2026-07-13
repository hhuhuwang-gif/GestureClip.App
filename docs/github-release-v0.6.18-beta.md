# GestureClip v0.6.18 Beta

## 下载

- [GestureClip-v0.6.18-beta-win-x64.zip](https://github.com/hhuhuwang-gif/GestureClip.App/releases/download/v0.6.18-beta/GestureClip-v0.6.18-beta-win-x64.zip)
- SHA256SUMS.txt

解压后双击 `GestureClip.exe`。

## 本次修复（建议升级）

**下滑手势粘贴跨机失败 — 完整加固**

- 右键按下瞬间记录前台窗口，粘贴前强制恢复焦点
- 粘贴前释放鼠标键 + 全部修饰键；`Ctrl+V` 拆分发送 + 扫描码
- 多层回退：`SendInput` → `keybd_event` → `WM_PASTE`
- 粘贴类手势：HUD 先隐藏再注入；注入失败不再当成成功
- 修正 x64 `INPUT` 结构布局

## 0.6.17 起仍包含

- `Ctrl+Shift+V` 纯文本粘贴（先清修饰键）
- 剪贴板历史 Enter 粘贴前先隐藏面板
- 智能粘贴 / 密码不记历史 / UI 升级 / 多路径更新

## 数据

`%LOCALAPPDATA%\GestureClip\` 覆盖程序不会删除。
