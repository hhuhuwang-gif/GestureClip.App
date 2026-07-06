# GestureClip v0.6.1 Beta

## 下载

- `GestureClip-v0.6.1-beta-win-x64.zip`
- `SHA256SUMS.txt`

## 这次更新

- 剪贴板历史重复复制体验优化：再次复制已有内容时刷新到列表顶部。
- 剪贴板历史面板更高、更紧凑，一次能看到更多记录。
- 数字快捷选择扩展为 `1-0`，支持快速选择前 10 条记录。
- 新增首次启动引导，三步说明剪贴板历史、鼠标手势和工位小熊。
- Release 资产精简为一个 Windows x64 zip 和 `SHA256SUMS.txt`。

## 修复

- 修复重复复制旧内容后仍停留在原位置的问题。
- 保持置顶记录优先，同时普通记录按最近活跃时间排序。

## 安装 / 覆盖更新

1. 退出正在运行的 GestureClip。
2. 下载并解压 `GestureClip-v0.6.1-beta-win-x64.zip`。
3. 覆盖旧程序目录。
4. 双击 `GestureClip.exe` 启动。

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
