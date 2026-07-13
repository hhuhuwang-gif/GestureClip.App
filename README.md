# GestureClip

<p align="center">
  <img src="docs/images/gestureclip-icon.png" alt="GestureClip" width="96" />
</p>

<p align="center">
  <strong>Windows 本地优先</strong>的剪贴板历史 + 鼠标手势 + 本地文本助手
</p>

<p align="center">
  <a href="https://github.com/hhuhuwang-gif/GestureClip.App/releases/latest"><img src="https://img.shields.io/github/v/release/hhuhuwang-gif/GestureClip.App?include_prereleases&label=release&color=0071E3" alt="Release" /></a>
  <a href="https://github.com/hhuhuwang-gif/GestureClip.App/releases/latest"><img src="https://img.shields.io/badge/platform-Windows%20x64-lightgrey" alt="Platform" /></a>
  <img src="https://img.shields.io/badge/runtime-self--contained-success" alt="Self-contained" />
  <img src="https://img.shields.io/badge/privacy-local%20first-blue" alt="Local first" />
</p>

<p align="center">
  复制过的文字 / 图片能找回来 · 右键一划完成常用操作 · 本地文本处理 · 可选「工位小熊」状态 HUD<br />
  <strong>不登录 · 不上传 · 不云同步</strong>
</p>

<p align="center">
  <a href="https://github.com/hhuhuwang-gif/GestureClip.App/releases/download/v0.6.17-beta/GestureClip-v0.6.17-beta-win-x64.zip"><strong>⬇ 下载 v0.6.17 Beta</strong></a>
  ·
  <a href="https://github.com/hhuhuwang-gif/GestureClip.App/releases/tag/v0.6.17-beta">Release 说明</a>
  ·
  <a href="CHANGELOG.md">更新日志</a>
</p>

![GestureClip 设置首页](docs/images/settings-home.png)

---

## 30 秒上手

| 你想做的事 | 怎么做 |
| --- | --- |
| 打开剪贴板历史 | `Ctrl + \`` |
| 找回刚复制过的文字 / 图片 | 在历史里搜索、双击复制，或粘贴 |
| 右键手势 | 按住**右键**上划复制、下划粘贴、左划后退、右划前进 |
| 本地文本处理 | `Ctrl + Shift + Q` 打开快捷动作面板 |
| 工位小熊 | 托盘 → 工位小熊 → 打开 Hub，填月薪与上下班时间 |

解压 zip 后双击 `GestureClip.exe` 即可（Windows x64 self-contained，一般**无需**安装 .NET）。

---

## 当前版本：v0.6.17 Beta

| 资源 | 链接 |
| --- | --- |
| 安装包 | [GestureClip-v0.6.17-beta-win-x64.zip](https://github.com/hhuhuwang-gif/GestureClip.App/releases/download/v0.6.17-beta/GestureClip-v0.6.17-beta-win-x64.zip) |
| 校验文件 | [SHA256SUMS.txt](https://github.com/hhuhuwang-gif/GestureClip.App/releases/download/v0.6.17-beta/SHA256SUMS.txt) |
| 全部版本 | [Releases](https://github.com/hhuhuwang-gif/GestureClip.App/releases) |

### 本版亮点

- **全局 `Ctrl+Shift+V` 纯文本粘贴**；链接净化 / HTML→文本 / Markdown
- **智能粘贴按应用**；密码框不记历史
- **UI**：剪贴板分段筛选、深色模式、快捷动作 Spotlight、手势轨迹、小熊 Emoji
- **更新**：多路径下载 GitHub 便携包后覆盖程序目录（数据不丢）

完整说明见 [CHANGELOG.md](CHANGELOG.md) 与 [Release Notes](https://github.com/hhuhuwang-gif/GestureClip.App/releases/tag/v0.6.17-beta)。

---

## 适合谁？

- 每天大量复制粘贴的办公、客服、运营、开发、写作用户
- 想用鼠标手势提速，但不想装臃肿软件
- 需要**本机**剪贴板历史，拒绝内容上云
- 想要轻量、可关闭的「打工状态」反馈（工位小熊）

---

## 核心功能

### 1. 剪贴板历史

- 文本 + 图片 / 截图历史，支持搜索、固定、收藏
- 删除单条 / 选中 / 清空；多选文本可合并复制
- 图片缩略图一键写回系统剪贴板；可暂停记录

![剪贴板](docs/images/settings-clipboard.png)

### 2. 快捷键

| 默认快捷键 | 作用 |
| --- | --- |
| `Ctrl + \`` | 打开 / 关闭剪贴板历史 |
| `Ctrl + Shift + Q` | 打开 / 关闭快捷动作面板（本地文本助手） |

均可在设置中修改。

### 3. 快捷动作面板（本地助手）

对当前剪贴板文本做**纯本地**处理，例如：

- 去空格 / 去换行
- 大小写转换
- JSON 美化 / 压缩
- URL 编码 / 解码

结果可预览、写回剪贴板，或直接粘贴。**不登录、不上传。**

### 4. 鼠标右键手势

按住鼠标右键划出轨迹，松开执行动作。

| 手势 | 默认动作 |
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
| 右键按住 + 左键点击 | 左键增强（可自定义） |

![手势设置](docs/images/settings-gestures.png)

### 5. 自定义手势与动作绑定

- 粘贴并回车、搜索选中文本、标签页、截图、缩放等
- 常用手势默认展开，高级手势默认收起
- 卡片式动作绑定；新手推荐 3 个手势一键补齐
- 可把手势绑到本地助手动作

![动作绑定](docs/images/gesture-bindings.png)

### 6. 手势 HUD + 工位小熊

划手势时显示当前动作，并可选显示打工状态：

- 今日估算收益、距离下班 / 发薪
- 今日复制 / 粘贴 / 手势次数
- 等级 / XP、休息与过劳提醒

![手势 HUD](docs/images/gesture-hud-workbear.png)

### 7. 工位小熊 Hub

轻量娱乐面板，可关闭、可本地配置：

- 30 秒填月薪与上下班时间
- 今日 / 本周摘要、下班冲刺、本地周报月报
- 分享卡片多种样式；托盘快速入口

![工位小熊](docs/images/workbear-panel.png)

![工位小熊 Hub](docs/images/workbear-hub-v0.6.0-beta.png)

---

## 数据与隐私

| 原则 | 说明 |
| --- | --- |
| 本地优先 | 不需要账号，不云同步 |
| 不上云 | 不上传剪贴板正文、图片、工资与统计 |
| 最小权限 | 不读浏览器正文、密码、Token、Cookie |
| 可关可清 | 可暂停剪贴板 / 手势；可清空历史；可设应用黑名单 |
| 安全导出 | 诊断包不含数据库与图片原图；日志不含剪贴板正文 |

```text
数据：%LOCALAPPDATA%\GestureClip\gestureclip.db
日志：%LOCALAPPDATA%\GestureClip\logs\
```

**覆盖更新程序文件不会删除**历史与设置。

更多：[PRIVACY.md](PRIVACY.md) · [KNOWN_ISSUES.md](KNOWN_ISSUES.md) · [ROADMAP.md](ROADMAP.md)

---

## 如何安装 / 覆盖更新

1. [下载最新 zip](https://github.com/hhuhuwang-gif/GestureClip.App/releases/latest)
2. 若已在运行：先退出 GestureClip，任务管理器确认无 `GestureClip.exe`
3. 解压并覆盖旧目录（或新目录首次使用）
4. 双击 `GestureClip.exe`

应用内：**设置 / 托盘 → 检查更新**，可下载新版并覆盖当前目录。  
用户数据在 `%LOCALAPPDATA%\GestureClip\`，覆盖程序不会删除。详见 [UPDATE.md](UPDATE.md)。

> 当前 Beta 未做代码签名，SmartScreen 可能提示「未知发布者」，属预期现象。

---

## 常见问题

**管理员窗口里手势不生效？**  
普通权限进程通常无法控制管理员窗口。需要时请以管理员身份运行 GestureClip。

**图片搜不到？**  
清空搜索框，或切到「图片」筛选。

**剪贴板图片粘到别的软件无效？**  
复制图片时会同时写入 Bitmap / PNG / DIB，提升兼容性。

**检查更新失败？**  
若系统代理（如本机端口）未启动，会自动尝试直连；也可手动到 Releases 下载。

**页面还不够完美？**  
仍是 Beta：优先保证稳定、不丢数据、手势准确、剪贴板可用，UI 持续打磨中。

---

## 开发与发布

```powershell
# 构建
dotnet build .\GestureClip.sln -c Release

# 测试
dotnet test .\GestureClip.sln

# 发布 Windows x64（产物在 artifacts\release\）
powershell -ExecutionPolicy Bypass -File .\scripts\publish-win-x64.ps1
```

---

## 分享文案（可复制）

**标题**

```text
我做了一个 Windows 本地剪贴板历史 + 鼠标手势工具：GestureClip
```

**正文**

```text
最近在做一个 Windows 小工具 GestureClip。

它主要做这些事：

1. 剪贴板历史
复制过的文字和图片可以找回来，支持搜索、固定、删除。数据在本机 SQLite，不上传。

2. 鼠标右键手势
按住右键一划就能复制、粘贴、后退、前进、粘贴并回车等，也能自己绑定动作。

3. 本地快捷动作
Ctrl + Shift + Q 对剪贴板文本做去空格、JSON、URL 等处理，预览后复制或粘贴。

4. 工位小熊（可选）
划手势时顺带看今日收益、下班倒计时、复制/粘贴统计，像个打工人状态栏。

目前 v0.6.17 Beta，Windows x64，解压双击 GestureClip.exe 就能跑。

项目地址：
https://github.com/hhuhuwang-gif/GestureClip.App

下载：
https://github.com/hhuhuwang-gif/GestureClip.App/releases/latest
```

---

## 截图

| 首页 | 剪贴板 |
| --- | --- |
| ![首页](docs/images/settings-home.png) | ![剪贴板](docs/images/settings-clipboard.png) |

| 手势 | 动作绑定 |
| --- | --- |
| ![手势](docs/images/settings-gestures.png) | ![绑定](docs/images/gesture-bindings.png) |

| HUD | 工位小熊 |
| --- | --- |
| ![HUD](docs/images/gesture-hud-workbear.png) | ![小熊](docs/images/workbear-panel.png) |

---

<p align="center">
  Made for Windows · Local first · <a href="https://github.com/hhuhuwang-gif/GestureClip.App/issues">反馈 Issue</a>
</p>

