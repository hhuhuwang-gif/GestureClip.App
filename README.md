# GestureClip

<p align="center">
  <img src="docs/images/gestureclip-icon.png" width="128" alt="GestureClip 小熊图标" />
</p>

<p align="center">
  <b>Windows 本地剪贴板历史 + 鼠标手势快捷操作 + 工位小熊 HUD</b>
</p>

<p align="center">
  复制粘贴少点几下，右键一划就干活。数据只保存在你自己的电脑里。
</p>

<p align="center">
  <a href="https://github.com/hhuhuwang-gif/GestureClip.App/releases">下载最新版</a>
</p>

---

## 一句话介绍

GestureClip 是一个 Windows 桌面效率小工具：

- 复制过的文字、图片，可以从剪贴板历史里找回来。
- 按 `Ctrl + \`` 打开剪贴板面板，搜索后直接粘贴。
- 按住鼠标右键一划，就能复制、粘贴、回车、Esc、后退、前进。
- 手势时会出现“工位小熊 HUD”，显示今日工资、下班倒计时、XP、等级和趣味战报。

适合每天大量复制粘贴、写文档、写代码、填表、处理网页的人。

---

## 最新版本

```text
v0.2.3
```

下载页面：

[GitHub Releases](https://github.com/hhuhuwang-gif/GestureClip.App/releases)

推荐下载：

```text
GestureClip-v0.2.3-win-x64.zip
```

解压后双击运行：

```text
GestureClip.exe
```

---

## 核心功能

### 1. 剪贴板历史

- 保存文本剪贴板历史
- 保存图片剪贴板历史，并显示缩略图
- 支持搜索
- 支持固定常用内容
- 支持删除单条、删除选中、清空全部
- 支持 `Shift` / `Ctrl` 多选
- 多选文本可以合并复制
- 防止重复内容无限堆积

默认快捷键：

```text
Ctrl + `
```

再次按快捷键可以关闭剪贴板面板。

### 2. 鼠标手势

默认按住鼠标右键滑动：

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

也可以在设置里改手势绑定。

### 3. 工位小熊 HUD

右键滑动时会出现一个轻量状态栏：

- 当前动作
- 快捷键说明
- 手势轨迹
- 今日工资估算
- 距离下班多久
- 距离发薪日多久
- 今日复制 / 粘贴 / 手势次数
- XP、等级、趣味文案

它不是常驻悬浮窗。右键一滑出现，松开后自动消失。

### 4. 日常设置

- 托盘常驻
- 开机自启
- 暂停 / 恢复剪贴板记录
- 暂停 / 恢复鼠标手势
- 黑名单：指定软件不记录、不响应手势
- 数据清理：按数量或天数清理历史
- 日志目录 / 数据目录一键打开
- 诊断信息复制

---

## 隐私说明

GestureClip 默认本地运行：

- 不上传云端
- 不需要账号
- 不做 AI 分析
- 不做 OCR
- 日志不记录剪贴板正文
- 工资、等级、XP 等数据只存在本机
- 可以随时暂停剪贴板记录
- 可以把密码管理器加入黑名单

默认数据位置：

```text
数据库：%LOCALAPPDATA%\GestureClip\gestureclip.db
日志：  %LOCALAPPDATA%\GestureClip\logs\
```

---

## 常见问题

### 快捷键注册失败怎么办？

一般是 `Ctrl + \`` 被别的软件占用了。关闭占用软件，或在设置里换一个快捷键。

### 为什么管理员窗口里手势不稳定？

Windows 权限隔离导致。普通权限程序不能稳定控制管理员权限窗口。需要时可以用管理员身份运行 GestureClip。

### 会不会记录密码？

如果密码被复制到了系统剪贴板，理论上任何剪贴板工具都能读取。建议把这些程序加入黑名单：

```text
1Password.exe
Bitwarden.exe
KeePass.exe
```

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


GestureClip is a local-first Windows clipboard history and mouse gesture utility. It supports text/image clipboard history, `Ctrl + \`` quick overlay, right-button gestures, customizable gesture bindings, and a playful worker-status HUD with XP and levels.

GestureClip is a local-first Windows clipboard history and mouse gesture utility. It supports text/image clipboard history, `Ctrl + \`` quick overlay, right-button gestures, customizable gesture bindings, and a playful worker-status HUD with XP and levels.

