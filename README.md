# GestureClip

> Windows 本地优先的剪贴板历史 + 鼠标手势快捷工具。
> 大白话：复制过的文字和图片能找回来；按住鼠标右键一划，可以复制、粘贴、回车、后退；还能带一个可关闭的“工位小熊”HUD，顺手看今天赚了多少、多久下班。

**本地优先，不登录，不上传，不云同步。**

![GestureClip 首页](docs/images/settings-home.png)

## 30 秒看懂

- `Ctrl + \`` 打开 / 关闭剪贴板历史。
- 复制过的文字、图片和截图可以找回来。
- 按住鼠标右键上划复制、下划粘贴、左划后退、右划前进。
- 自定义手势可以绑定搜索、截图、标签页、粘贴并回车等动作。
- 工位小熊是可选趣味模块，可显示今日收益、下班倒计时、休息提醒和今日报告。

> TODO：这里建议放一张 30 秒演示 GIF 或短视频，展示剪贴板面板、右键手势和工位小熊 HUD。

## 适合谁用？

- 每天频繁复制粘贴的办公用户。
- 客服、运营、开发、写作、数据整理人员。
- 喜欢鼠标手势快捷操作，但不想安装太重软件的人。
- 想要本地剪贴板历史，但不想把内容上传到云端的人。
- 想要一个轻量、有趣、不打扰的办公状态小工具的人。

## 现在是什么版本？

当前版本：**v0.6.2 Beta**

下载地址：

- [GestureClip-v0.6.2-beta-win-x64.zip](https://github.com/hhuhuwang-gif/GestureClip.App/releases/download/v0.6.2-beta/GestureClip-v0.6.2-beta-win-x64.zip)
- [Release 页面](https://github.com/hhuhuwang-gif/GestureClip.App/releases/tag/v0.6.2-beta)

解压后双击：

```text
GestureClip.exe
```

发布包是 Windows x64 self-contained 版本，通常不用额外安装 .NET Runtime。

## 它能解决什么问题？

平时办公经常会遇到这些事：

- 刚复制过一段话，下一秒被别的内容覆盖了。
- 经常复制、粘贴、回车、后退，手离键盘很麻烦。
- 想用鼠标做快捷动作，但又不想安装太重的软件。
- 复制过图片或截图，后面想重新找回来。
- 想知道今天复制/粘贴/手势用了多少次，顺便看看“打工人状态”。

GestureClip 就是把这些都放进一个本地小工具里。

## 核心功能

### 1. 剪贴板历史

- 保存文本剪贴板历史。
- 保存图片/截图剪贴板历史。
- 支持搜索。
- 支持固定常用记录。
- 支持收藏片段。
- 支持删除单条、删除选中、清空历史。
- 支持多选文本后合并复制。
- 图片显示缩略图，点击可重新复制回系统剪贴板。
- 可暂停剪贴板记录。

![剪贴板设置与数据清理](docs/images/settings-clipboard.png)

### 2. 快捷键打开剪贴板

默认快捷键：

```text
Ctrl + `
```

作用：打开 / 关闭剪贴板历史面板。

### 3. 鼠标右键手势

按住鼠标右键，往一个方向划一下，松开就执行动作。

默认常用手势：

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
| 右键按住 + 左键点击 | 组合手势，可绑定动作 |

![鼠标手势设置](docs/images/settings-gestures.png)

### 4. 自定义手势绑定

可以自己设计常用动作，例如：

- 粘贴并回车
- Google 搜索选中文本
- 百度搜索选中文本
- 新建标签页
- 下一标签页 / 上一标签页
- 截图
- 缩放重置

常用手势默认显示，高级手势默认收起，避免一打开就很乱。

![手势绑定页面](docs/images/gesture-bindings.png)

### 5. 手势 HUD + 工位小熊

右键划动时会出现 HUD。它不只是提示“执行了什么动作”，还会显示一点打工人状态：

- 当前动作
- 快捷键
- 手势码
- 当前模式
- 今日工资估算
- 距离下班多久
- 距离发薪日多久
- 今日复制次数
- 今日粘贴次数
- 今日手势次数
- 打工人等级 / XP

![手势 HUD](docs/images/gesture-hud-workbear.png)

### 6. 工位小熊面板

工位小熊是一个轻量娱乐状态面板，主要给办公过程一点反馈感。

它可以显示：

- 今日已赚多少钱。
- 还有多久下班。
- 还有多久发薪水。
- 今日复制 / 粘贴 / 手势次数。
- 休息提醒。
- 过劳提醒。
- 下班后生存报告。

所有统计默认只在本机计算，不上传。

![工位小熊设置](docs/images/workbear-panel.png)

## 数据与隐私

GestureClip 默认本地运行：

- 不需要账号。
- 不上传剪贴板内容。
- 不上传图片。
- 不上传工资和工位小熊统计。
- 不读取浏览器正文、密码、Token、Cookie。
- 日志不记录剪贴板正文。
- 诊断包不包含数据库和图片原始内容。
- 可以暂停剪贴板记录。
- 可以暂停鼠标手势。
- 可以配置应用黑名单。

默认数据目录：

```text
%LOCALAPPDATA%\GestureClip\gestureclip.db
```

默认日志目录：

```text
%LOCALAPPDATA%\GestureClip\logs\
```

覆盖更新程序文件，不会删除你的历史记录和设置。

更多说明：

- [PRIVACY.md](PRIVACY.md)：保存什么、不保存什么、如何清空数据。
- [ROADMAP.md](ROADMAP.md)：后续版本计划。
- [KNOWN_ISSUES.md](KNOWN_ISSUES.md)：当前 Beta 已知问题。

## 如何覆盖更新？

老用户下载新版 zip 后直接覆盖旧程序目录即可：

```text
GestureClip-v0.6.2-beta-win-x64.zip
```

步骤：

1. 退出 GestureClip。
2. 任务管理器确认没有 `GestureClip.exe`。
3. 解压新版 zip。
4. 覆盖旧程序目录。
5. 双击 `GestureClip.exe`。

## 常见问题

### Windows 提示未知发布者？

当前 Beta 包还没有代码签名，SmartScreen 可能提示未知发布者。

### 管理员窗口里手势不生效？

普通权限运行的 GestureClip 可能无法控制管理员权限窗口。如确实需要，请以管理员身份运行 GestureClip。

### 图片为什么搜不到？

如果搜索框里有文字，图片可能被搜索过滤掉。请清空搜索框，或者点击“图片”筛选。

### 剪贴板图片复制到别的软件无效？

v0.6.2 Beta 保留了图片兼容性增强：复制图片时会同时写入 Bitmap、PNG、DIB 三种剪贴板格式。

### 页面看起来还不够完美？

这是 Beta 版本，还在持续打磨。当前优先级是：稳定、不丢数据、不卡、手势准确、剪贴板图片可用。

## 开发与发布

构建：

```powershell
dotnet build .\GestureClip.sln -c Release
```

测试：

```powershell
dotnet test .\GestureClip.sln
```

发布 Windows x64：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-win-x64.ps1
```

发布产物默认在：

```text
artifacts\release\GestureClip\
```

## linux.do 分享文案

标题可以写：

```text
我做了一个 Windows 本地剪贴板历史 + 鼠标手势工具：GestureClip
```

正文可以直接用：

```text
最近在做一个 Windows 小工具 GestureClip。

它主要做两件事：

1. 剪贴板历史
复制过的文字和图片可以找回来，支持搜索、固定、删除、数据清理。默认数据都存在本机 SQLite，不上传。

2. 鼠标右键手势
按住右键划一下就能执行动作，比如上划复制、下划粘贴、左划后退、右划前进、下左粘贴并回车。也可以自己绑定手势。

我还加了一个比较抽象的“工位小熊”HUD：右键划手势的时候，会顺手显示今日工资、距离下班多久、发薪日倒计时、复制/粘贴/手势次数，有点像打工人状态栏。

目前是 v0.6.2 Beta，Windows x64，解压双击 GestureClip.exe 就能跑。还在持续优化 UI、图片剪贴板兼容性和手势体验。

项目地址：
https://github.com/hhuhuwang-gif/GestureClip.App
```

## 截图总览

![首页](docs/images/settings-home.png)

![剪贴板](docs/images/settings-clipboard.png)

![手势](docs/images/settings-gestures.png)

![动作绑定](docs/images/gesture-bindings.png)

![HUD](docs/images/gesture-hud-workbear.png)

![小熊](docs/images/workbear-panel.png)
