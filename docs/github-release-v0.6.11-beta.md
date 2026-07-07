# GestureClip v0.6.11 Beta

## 下载

- `GestureClip-v0.6.11-beta-win-x64.zip`
- `SHA256SUMS.txt`

## 本次重点

- 修复点击「动作绑定」页后程序直接退出的问题。
- 动作绑定页现在会正确声明 `BooleanToVisibilityConverter`，避免 WPF 懒加载列表模板时找不到资源而崩溃。
- 动作绑定列表去掉高风险局部样式 BasedOn 引用，降低页面切换时的资源解析风险。

## 小白友好优化

- 设置首页补充：下载 zip 后先解压、双击 `GestureClip.exe` 打开、数据只保存在本机。
- 诊断页补充说明：诊断包保存在本机，不会自动上传，不包含剪贴板正文、数据库或图片原始内容。
- 检查更新失败时提示可以打开 GitHub Release 页面手动下载最新 zip，不再只告诉你“网络失败”。

## 稳定性优化

- 启动读取手势识别阈值、边缘热区、停留时间、冷却时间、滑动距离时，会自动夹到安全范围。
- 避免异常配置导致手势太灵敏、太迟钝或设置页表现异常。

## 怎么用

1. 下载 `GestureClip-v0.6.11-beta-win-x64.zip`。
2. 解压后双击 `GestureClip.exe`。
3. 按 `Ctrl + \`` 打开剪贴板历史。
4. 想改手势动作：进入「设置」→「动作绑定」。

## 本地数据影响

覆盖程序文件不会删除本地历史记录和设置；默认数据保存在：

```text
%LOCALAPPDATA%\GestureClip
```

GestureClip 仍然是本地优先：不登录、不上传剪贴板内容、不云同步。

## 校验

下载后可使用 `SHA256SUMS.txt` 校验 zip 文件完整性，确认下载的 `GestureClip-v0.6.11-beta-win-x64.zip` 没有损坏。

## 已知说明

- 当前 Beta 包未签名，Windows SmartScreen 可能提示未知发布者。
- 管理员权限窗口中，普通权限运行的 GestureClip 可能无法稳定执行输入模拟；如确实需要，请以管理员身份运行 GestureClip。
