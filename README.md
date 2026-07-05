# GestureClip

Windows 本地优先的剪贴板历史 + 鼠标手势快捷工具。

大白话：复制过的文字和图片能找回来；按住鼠标右键一划，可以复制、粘贴、回车、后退；所有数据默认只放在你自己的电脑里。

## 最新版本

```text
v0.5.0 Beta
```

推荐下载：

```text
GestureClip-v0.5.0-beta-win-x64.zip
```

老用户覆盖更新：

```text
GestureClip-v0.5.0-beta-update-win-x64.zip
```

解压后双击：

```text
GestureClip.exe
```

当前发布包是 Windows x64 self-contained 版本，通常不需要额外安装 .NET Runtime。

## 主要功能

- 文本 / 图片剪贴板历史
- `Ctrl + \`` 打开或关闭剪贴板面板
- 搜索、删除、固定、收藏剪贴板记录
- 多选文本后合并复制
- 图片缩略图预览，点击可重新复制图片
- 右键鼠标手势
- 自定义手势绑定
- 黑名单应用，不记录敏感软件剪贴板
- 开机自启
- 数据清理
- 工位小熊 HUD
- 今日工资、下班倒计时、发薪日倒计时
- 打工人等级 / XP
- 过劳提醒 / 猝死提醒
- HUD 根据工作时间阶段变色

## 默认快捷键

打开 / 关闭剪贴板面板：

```text
Ctrl + `
```

## 默认右键手势

按住鼠标右键滑动：

| 手势 | 动作 |
| --- | --- |
| 上划 | 复制 `Ctrl + C` |
| 下划 | 粘贴 `Ctrl + V` |
| 上下划 | 回车 `Enter` |
| 下上划 | 取消 `Esc` |
| 左划 | 后退 `Alt + Left` |
| 右划 | 前进 `Alt + Right` |
| 左右划 | 全选 `Ctrl + A` |
| 右左划 | 撤销 `Ctrl + Z` |
| 下左划 | 粘贴并回车 |

## 数据与隐私

GestureClip 默认本地运行：

- 不上传剪贴板内容
- 不需要账号
- 不做云同步
- 不做 AI 分析
- 不做 OCR
- 日志不记录剪贴板正文
- 可暂停剪贴板记录
- 可把密码管理器加入黑名单

默认数据位置：

```text
数据库：%LOCALAPPDATA%\GestureClip\gestureclip.db
日志：  %LOCALAPPDATA%\GestureClip\logs\
```

覆盖更新程序目录不会删除这些本地数据。

## 覆盖更新

老用户请看：

```text
UPDATE.md
```

简短流程：

1. 右键托盘图标，退出 GestureClip。
2. 确认任务管理器没有 GestureClip 进程。
3. 解压 update 包。
4. 覆盖旧版程序目录。
5. 双击 `GestureClip.exe` 启动新版。

## 常见问题

### Windows 提示未知发布者？

当前 Beta 包未做代码签名，Windows SmartScreen 可能提示未知发布者。

### 快捷键注册失败？

一般是快捷键被其他软件占用。到设置里换一个热键，或关闭占用热键的软件。

### 管理员窗口里手势不稳定？

Windows 权限隔离导致。普通权限程序不能稳定控制管理员权限窗口。需要时可以用管理员身份运行 GestureClip。

### 退出后还有后台进程？

新版退出时会释放托盘、热键、剪贴板监听、鼠标 Hook、边缘触发和过劳提醒 Timer。如果仍有残留，请带日志反馈。

## 开发运行

需要 Windows + .NET 8 SDK。

```powershell
dotnet restore .\GestureClip.sln
dotnet build .\GestureClip.sln --no-restore
dotnet test .\GestureClip.sln --no-restore
dotnet run --project .\src\GestureClip.App\GestureClip.App.csproj
```

发布 Windows x64 单文件版：

```powershell
.\scripts\publish-win-x64.ps1
```

