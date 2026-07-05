# UPDATE

GestureClip 覆盖更新说明。

## 覆盖更新步骤

1. 右键托盘图标，选择退出 GestureClip。
2. 打开任务管理器，确认没有 `GestureClip.exe` 进程。
3. 下载 `GestureClip-v0.6.0-beta-update-win-x64.zip`。
4. 解压新版文件。
5. 将新版文件复制到旧程序目录并覆盖。
6. 双击 `GestureClip.exe` 启动。

## 用户数据在哪里

用户数据默认保存在：

```text
%LOCALAPPDATA%\GestureClip\
```

包括：

- 剪贴板文本历史
- 剪贴板图片历史
- 固定 / 收藏记录
- 手势配置
- 自定义手势
- R+L 组合手势配置
- 黑名单
- HUD 设置
- 工位小熊设置
- 过劳提醒设置
- 等级 / XP / 今日统计

## 覆盖更新不会删除什么

覆盖程序目录不会删除 `%LOCALAPPDATA%\GestureClip\` 里的数据。

## 更新失败怎么办

如果提示 `GestureClip.exe` 被占用：

1. 说明旧程序还在运行。
2. 先退出托盘程序。
3. 任务管理器结束残留的 `GestureClip.exe`。
4. 再覆盖文件。

## 不要做

- 不要把旧数据库复制到程序目录。
- 不要删除 `%LOCALAPPDATA%\GestureClip\`，除非你明确想清空所有数据。
- 不要在程序还运行时覆盖 exe。

