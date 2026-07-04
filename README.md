# GestureClip

<p align="center">
  <img src="docs/images/gestureclip-icon.png" width="132" alt="GestureClip 工位小熊图标" />
</p>

<p align="center">
  <b>Windows 本地剪贴板历史 + 鼠标手势快捷操作 + 工位小熊 HUD</b>
</p>

<p align="center">
  大白话：复制粘贴更快，右键一划就执行动作；所有数据默认只放在你自己的电脑里。
</p>

<p align="center">
  <a href="https://github.com/hhuhuwang-gif/GestureClip.App/releases">下载最新版</a>
</p>

---

## 最新版本

```text
v0.2.5
```

推荐下载：

```text
GestureClip-v0.2.5-win-x64.zip
```

解压后双击：

```text
GestureClip.exe
```

---

## 它能做什么？

GestureClip 是一个 Windows 桌面效率小工具，主要做三件事：

1. **剪贴板历史**：复制过的文字、图片，可以重新找回来。
2. **右键鼠标手势**：按住右键一划，直接复制、粘贴、回车、Esc、后退、前进。
3. **工位小熊 HUD**：手势时显示动作、快捷键、今日工资、下班倒计时、XP 等状态反馈。

适合：写代码、写文档、填表、处理网页、每天大量复制粘贴的人。

---

## 默认快捷键

打开 / 关闭剪贴板面板：

```text
Ctrl + `
```

剪贴板面板里可以：

- 搜索历史
- 回车粘贴
- 数字键快速粘贴
- 固定常用内容
- 删除记录
- 多选文本后合并复制
- 查看图片缩略图并重新复制图片

---

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

手势绑定可以在设置里改。

---

## 工位小熊 HUD

右键滑动时会短暂出现状态栏：

- 当前识别到的手势
- 即将执行的动作
- 快捷键说明
- 今日复制 / 粘贴 / 手势次数
- 今日工资估算
- 距离下班多久
- 距离发薪日多久
- XP / 等级 / 趣味文案

它不是常驻桌面小窗，不会一直挡屏幕。右键一滑出现，松开后自动消失。

---

## 设置里有什么？

- 暂停 / 恢复剪贴板记录
- 暂停 / 恢复鼠标手势
- 修改剪贴板热键
- 修改手势绑定
- 黑名单：指定软件不记录剪贴板、不响应手势
- 开机自启
- 数据清理：按数量或天数清理历史
- 日志目录、数据目录、诊断信息

---

## 隐私说明

GestureClip 默认本地运行：

- 不上传云端
- 不需要账号
- 不做 AI 分析
- 不做 OCR
- 日志不记录剪贴板正文
- 可以暂停剪贴板记录
- 可以把密码管理器加入黑名单

默认数据位置：

```text
数据库：%LOCALAPPDATA%\GestureClip\gestureclip.db
日志：  %LOCALAPPDATA%\GestureClip\logs\
```

建议加入黑名单的软件：

```text
1Password.exe
Bitwarden.exe
KeePass.exe
```

---

## 常见问题

### 快捷键注册失败怎么办？

一般是快捷键被别的软件占用了。到设置里换一个热键，或关闭占用热键的软件。

### 管理员窗口里手势不稳定？

Windows 权限隔离导致。普通权限程序不能稳定控制管理员权限窗口。需要时可以用管理员身份运行 GestureClip。

### 退出后还有后台进程怎么办？

新版会在退出时释放托盘、热键、剪贴板监听和鼠标 Hook。如果仍有残留，请带日志反馈。

---

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

---

## English Short Intro

GestureClip is a local-first Windows clipboard history and mouse gesture utility. It supports text/image clipboard history, a quick overlay hotkey, right-button gestures, customizable gesture bindings, and a playful worker-status HUD.

