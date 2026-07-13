# GestureClip v0.6.16 Beta

## 下载（推荐顺序）

1. **安装包（推荐）**  
   `GestureClip-Setup-v0.6.16-beta-win-x64.zip`  
   解压后双击 `Setup.cmd`，装到 `%LOCALAPPDATA%\Programs\GestureClip`  
   静默：`Setup.cmd /S`

2. **便携包**  
   `GestureClip-v0.6.16-beta-win-x64.zip`  
   解压后运行 `GestureClip.exe`

3. 校验：`SHA256SUMS.txt`

## 本次重点

### 安装与更新
- 正式 **Setup 安装包**：开始菜单、可卸载、数据与程序分离
- 应用内更新 **优先下载 Setup**，没有安装包再回退 portable 覆盖
- 下载仍支持：系统代理 → 直连 → 镜像加速

### 粘贴力
- 全局 `Ctrl+Shift+V` 纯文本粘贴
- 快捷动作：纯文本 / HTML→文本 / Markdown / 链接净化
- 智能粘贴按应用策略；密码框/密码管理器默认不记历史

### UI
- 剪贴板：分段筛选、图标顶栏、类型色条卡片、空状态
- 设置：大卡片分组、字号节奏、深色模式
- 快捷动作 Spotlight 化；手势迷你轨迹；工位小熊 Emoji IP

## 本地数据

覆盖 / 安装 / 卸载程序文件都不会删除：

```text
%LOCALAPPDATA%\GestureClip\
```

## 校验

- `dotnet test ./GestureClip.sln`
- `scripts/publish-win-x64.ps1`
- `scripts/build-setup.ps1 -SkipPublish`
