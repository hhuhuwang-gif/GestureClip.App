# GestureClip

<p align="center">
  <img src="docs/images/gestureclip-icon.png" width="112" alt="GestureClip 图标" />
</p>

<p align="center">
  <b>本地剪贴板历史 + 鼠标手势快捷操作 + 打工人 HUD</b>
</p>

<p align="center">
  少点几下鼠标，少切几次窗口，复制粘贴更顺手。
</p>

---

## 这是干什么的？

GestureClip 是一个 Windows 桌面小工具。

它主要做三件事：

1. 帮你保存剪贴板历史，刚才复制过的文字和图片可以找回来。
2. 按 `Ctrl + \`` 快速打开剪贴板面板，搜索、复制、粘贴都方便。
3. 按住右键一划，直接执行复制、粘贴、回车、Esc、返回、前进等操作。

新版还加了一个更好玩的东西：

> 右键手势 HUD 会显示“工位小熊 / 打工人状态栏”。  
> 你每次用手势都会获得 XP，等级会升级，还会显示今日工资、下班倒计时、发薪倒计时和趣味战报。

---

## 当前版本

```text
v0.2.2
```

下载：[GitHub Releases](https://github.com/hhuhuwang-gif/GestureClip.App/releases)

推荐下载：

```text
GestureClip-v0.2.2-win-x64.zip
```

解压后双击：

```text
GestureClip.exe
```

---

## 主要功能

### 剪贴板历史

- 保存文本剪贴板历史
- 保存图片剪贴板历史
- 图片显示缩略图
- 搜索历史内容
- 回车粘贴选中内容
- 数字键 `1 - 9` 快速粘贴
- 支持 `Shift` / `Ctrl` 多选
- 多选文本可合并复制
- 删除单条 / 删除选中 / 清空全部
- 固定常用内容
- 防止重复内容无限增加

### 鼠标手势

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

支持自定义手势绑定。  
也支持中键、侧键、屏幕边缘快捷触发。

### 工位小熊 HUD

右键滑动时会出现一个轻量 HUD：

- 当前手势方向
- 当前动作
- 快捷键说明
- Pattern
- 当前模式
- 趣味战报
- 经验 XP
- 打工人等级
- 今日工资
- 下班倒计时
- 发薪倒计时
- 今日复制 / 粘贴 / 手势次数

示例：

```text
↓ 下划 · 粘贴
Ctrl + V · Pattern: D

粘贴成功，牛马效率 +1
经验 +3

Lv.5 右键小法师    XP 128 / 900
今日 ￥186.40 · 下班 02:15 · 发薪 11 天
手势 37 · 复制 25 · 粘贴 18 · 少点 111 次
```

升级时会弹出一个 3 秒自动消失的小提示，不抢焦点，不影响鼠标操作。

### 设置和日常体验

- 托盘常驻
- 单实例运行
- 开机自启
- 剪贴板暂停 / 恢复
- 鼠标手势暂停 / 恢复
- 黑名单：指定软件不记录剪贴板、不响应手势
- 数据清理：按数量和天数清理历史
- 日志目录 / 数据目录一键打开
- 诊断信息复制

---

## 默认快捷键

打开 / 关闭剪贴板窗口：

```text
Ctrl + `
```

剪贴板窗口里：

| 操作 | 快捷键 |
| --- | --- |
| 粘贴选中内容 | `Enter` |
| 按编号粘贴 | `1` - `9` |
| 搜索 | 直接输入 |
| 清空搜索 / 关闭 | `Esc` |
| 复制选中内容 | `Ctrl + C` |
| 全选 | `Ctrl + A` |
| 删除选中 | `Delete` |
| 置顶 / 取消置顶 | `Ctrl + P` |

---

## 数据和隐私

GestureClip 默认只在本地工作：

- 不上传云端
- 不需要账号
- 不做 AI 分析
- 不做 OCR
- 日志不记录剪贴板正文
- 月薪和工位小熊数据只存在本机
- 可以暂停剪贴板记录
- 可以设置黑名单

默认数据位置：

```text
数据库：%LOCALAPPDATA%\GestureClip\gestureclip.db
日志：  %LOCALAPPDATA%\GestureClip\logs\
```

---

## 常见问题

### 快捷键注册失败怎么办？

一般是 `Ctrl + \`` 被别的软件占用了。  
可以关掉占用的软件，或在设置里重新设置快捷键。

### 为什么管理员窗口里手势不稳定？

Windows 权限隔离导致。  
普通权限程序不能稳定控制管理员权限窗口。需要时可以用管理员身份运行 GestureClip。

### 会保存密码吗？

如果密码管理器里的内容进入了系统剪贴板，理论上会被记录。  
建议把这些程序加到黑名单：

```text
1Password.exe
Bitwarden.exe
KeePass.exe
```

---

## 开发运行

需要：

- Windows
- .NET 8 SDK

```powershell
dotnet restore .\GestureClip.sln
dotnet build .\GestureClip.sln --no-restore
dotnet test .\GestureClip.sln --no-restore
```

运行开发版：

```powershell
dotnet run --project .\src\GestureClip.App\GestureClip.App.csproj
```

发布 Windows x64 单文件版：

```powershell
.\scripts\publish-win-x64.ps1
```

发布输出：

```text
artifacts/release/GestureClip/
```

---

## English Short Intro

GestureClip is a local-first Windows clipboard history and mouse gesture utility. It supports text/image clipboard history, `Ctrl + \`` quick overlay, right-button gestures, customizable gesture bindings, and a fun worker-status HUD with XP and level progression.
