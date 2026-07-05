# GestureClip v0.5.1 Beta - 稳定性修复版

## 这次重点

- 手势绑定页稳定性修复。
- 自定义手势新增、显示、识别、删除体验优化。
- 自定义手势动作选择表单改成更宽松的纵向布局。
- R+L 组合手势默认补齐：右键按住 + 左键点击。
- 新增导出诊断包，方便反馈问题。
- 补齐公测文档、帮助文档、已知问题和 Issue 模板。

## 下载文件

- `GestureClip-v0.5.1-beta-win-x64.zip`：完整包，解压后运行 `GestureClip.exe`。
- `GestureClip-v0.5.1-beta-update-win-x64.zip`：老用户覆盖更新包。
- `SHA256SUMS.txt`：压缩包 SHA256 校验值。

## 覆盖更新方法

1. 右键托盘图标，选择“退出 GestureClip”。
2. 打开任务管理器，确认没有 `GestureClip.exe`。
3. 解压 update 压缩包。
4. 将新版文件覆盖到旧版程序目录。
5. 双击 `GestureClip.exe` 启动新版。

## 数据保留说明

覆盖更新不会删除用户数据。默认数据目录：

```text
%LOCALAPPDATA%\GestureClip\
```

会保留：剪贴板历史、图片历史、手势配置、自定义手势、黑名单、HUD 设置、工位小熊设置、过劳提醒设置、等级 / XP、用户设置。

## 已知问题

- 当前 Beta 包未签名，Windows SmartScreen 可能提示未知发布者。
- 管理员权限窗口中，普通权限运行的 GestureClip 可能无法稳定执行输入模拟。
- 图片历史数量很大时，首次缩略图加载仍可能需要一点时间。
- 覆盖更新前必须先退出程序，否则 `GestureClip.exe` 可能被占用。
