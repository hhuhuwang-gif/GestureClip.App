# GestureClip

<p align="center">
  <img src="docs/images/gestureclip-icon.png" width="96" alt="GestureClip icon" />
</p>

GestureClip 是一个本地运行的 Windows 小工具：帮你保存剪贴板历史，并用鼠标手势快速复制、粘贴、回车、返回、前进。

一句话：**少点几下鼠标，少来回切窗口，办公更顺手。**

## 适合谁用

- 经常复制粘贴文字、图片的人
- 经常要找刚才复制过内容的人
- 想用鼠标一划就复制、粘贴、回车的人
- 想要一个本地、不上传云端的剪贴板工具的人

## 主要功能

- 剪贴板历史：保存文本和图片
- 快速搜索：打开后直接输入就能找
- 图片预览：复制图片后可在剪贴板窗口里看到缩略图
- 快速粘贴：选中后回车，或按数字键粘贴
- 多选复制：支持 `Shift` / `Ctrl` 多选，多条文本可合并复制
- 常用片段：常用内容可保存成片段
- 置顶：重要内容可固定
- 删除和清理：可删单条、删选中、清空、按数量/天数清理
- 鼠标手势：右键划一下执行常用动作
- 屏幕边缘快捷触发
- 黑名单：指定软件不记录剪贴板、不响应手势
- 开机自启
- 本地日志和诊断信息

## 快速开始

1. 下载 Release 里的 `GestureClip-v0.2.0-win-x64.zip`
2. 解压
3. 双击 `GestureClip.exe`
4. 看到托盘图标后，就可以用了

下载地址：[GitHub Releases](https://github.com/hhuhuwang-gif/GestureClip.App/releases)

## 默认快捷键

打开 / 关闭剪贴板窗口：

```text
Ctrl + `
```

剪贴板窗口里常用操作：

| 操作 | 快捷键 |
| --- | --- |
| 粘贴选中内容 | `Enter` |
| 按编号粘贴 | `1` - `9` |
| 聚焦搜索框 | `Ctrl + F` |
| 清空搜索 / 关闭窗口 | `Esc` |
| 切换筛选 | `Ctrl + 1` - `Ctrl + 5` |
| 复制选中内容 | `Ctrl + C` |
| 置顶 / 取消置顶 | `Ctrl + P` |
| 保存 / 取消片段 | `Ctrl + S` |
| 删除选中内容 | `Delete` |

## 默认鼠标手势

默认是“编辑增强模式”：

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

也可以切换到“剪贴板增强模式”：

| 手势 | 动作 |
| --- | --- |
| 上划 | 打开剪贴板历史 |
| 下划 | 粘贴最近一条历史 |

## 数据和隐私

GestureClip 默认只在本机工作：

- 不上传云端
- 不做账号同步
- 剪贴板文本保存在本地 SQLite
- 剪贴板图片以 PNG 数据保存在本地
- 可以暂停剪贴板记录
- 可以给密码管理器、浏览器等软件加黑名单
- 日志不记录剪贴板正文

默认数据位置：

```text
数据库：%LOCALAPPDATA%\GestureClip\gestureclip.db
日志：  %LOCALAPPDATA%\GestureClip\logs\
```

## 常见问题

### 为什么快捷键注册失败？

通常是被其他软件占用了。先关掉可能占用 ``Ctrl + ` `` 的软件，或在设置里重新尝试。

### 为什么管理员窗口里手势不稳定？

Windows 权限隔离导致的。普通权限程序不能稳定控制管理员权限窗口。需要时请用管理员身份运行 GestureClip。

### 会保存密码吗？

如果密码管理器没有加入黑名单，系统剪贴板里的内容理论上会被记录。建议把 `1Password.exe`、`Bitwarden.exe`、`KeePass.exe` 等加入黑名单。

## 开发运行

需要：

- Windows
- .NET 8 SDK

构建和测试：

```powershell
dotnet restore .\GestureClip.sln
dotnet build .\GestureClip.sln --no-restore
dotnet test .\GestureClip.sln --no-restore
```

源码运行：

```powershell
dotnet run --project .\src\GestureClip.App\GestureClip.App.csproj
```

发布 Windows x64 单文件版本：

```powershell
.\scripts\publish-win-x64.ps1
```

发布输出目录：

```text
artifacts/release/GestureClip/
```

## English Short Intro

GestureClip is a local-first Windows clipboard history and mouse gesture utility. It stores text and image clipboard history locally, opens with ``Ctrl + ` ``, and provides right-button gestures for copy, paste, enter, escape, back, and forward.
