# GestureClip 覆盖更新方法

适用：从旧版 GestureClip 覆盖更新到新版。

## 更新步骤

1. 右键托盘图标，选择“退出 GestureClip”。
2. 打开任务管理器，确认没有 `GestureClip` 进程。
3. 解压新版 update 压缩包。
4. 将新版文件覆盖到旧版 GestureClip 程序目录。
5. 双击 `GestureClip.exe` 启动新版。

## 会保留的数据

你的以下数据会保留：

- 剪贴板历史
- 图片历史
- 常用片段
- 手势配置
- 自定义手势
- 黑名单
- HUD 设置
- 工位小熊设置
- 猝死提醒设置
- 打工人等级 / XP
- 用户设置
- 日志目录

默认数据位置：

```text
%LOCALAPPDATA%\GestureClip\
```

数据库：

```text
%LOCALAPPDATA%\GestureClip\gestureclip.db
```

日志：

```text
%LOCALAPPDATA%\GestureClip\logs\
```

## 建议备份

如果担心更新失败，可以先备份：

1. 旧版程序目录
2. `%LOCALAPPDATA%\GestureClip\`

## 注意

- 不要在程序运行中直接覆盖文件。
- 必须先退出 GestureClip。
- 如果 exe 被占用，说明旧进程还没退出，请先在任务管理器确认。
- update 包只覆盖程序文件，不会包含你的数据库、日志或用户配置。

