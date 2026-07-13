# GestureClip v0.6.16 Beta

## 下载

- `GestureClip-v0.6.16-beta-win-x64.zip` — 解压后运行 `GestureClip.exe`
- `SHA256SUMS.txt`

> 说明：曾尝试的 Setup 安装器因兼容问题已下线，请用便携 zip。

## 本次重点

### 粘贴力
- 全局 `Ctrl+Shift+V` 纯文本粘贴
- 快捷动作：纯文本 / HTML→文本 / Markdown / 链接净化
- 智能粘贴按应用策略；密码框/密码管理器默认不记历史

### UI
- 剪贴板：分段筛选、图标顶栏、类型色条卡片、空状态
- 设置：大卡片分组、字号节奏、深色模式
- 快捷动作 Spotlight 化；手势迷你轨迹；工位小熊 Emoji IP

### 更新
- 多路径下载 GitHub 便携包后覆盖程序目录（代理 / 直连 / 镜像）

## 本地数据

```text
%LOCALAPPDATA%\GestureClip\
```

覆盖程序文件不会删除上述数据。

## 校验

- `dotnet test ./GestureClip.sln`
- `scripts/publish-win-x64.ps1`
