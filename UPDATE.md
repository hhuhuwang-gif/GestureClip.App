# UPDATE

GestureClip 支持两种分发方式：

1. **Setup 安装包（推荐）**：开始菜单 + 可卸载，默认装到当前用户目录  
2. **便携 zip**：解压即用，适合随身拷贝

## 首次安装（推荐 Setup）

1. 下载 `GestureClip-Setup-v*-win-x64.zip`（若有 `Setup.exe` 也可）
2. 解压后双击 **Setup.cmd**（无需管理员；不要双击 .txt）
3. 默认安装目录：`%LOCALAPPDATA%\Programs\GestureClip`
4. 开始菜单会出现 GestureClip

静默安装 / 覆盖升级：

```bat
Setup.cmd /S
```

## 便携 zip

1. 下载 `GestureClip-v*-win-x64.zip`
2. 解压到任意目录
3. 双击 `GestureClip.exe`

self-contained，一般**不需要**单独安装 .NET。

## 覆盖更新

### 应用内更新

托盘或设置里的「检查更新 / 一键覆盖更新」会：

1. 优先下载 **Setup.exe / Setup zip**（若 Release 里有）
2. 否则回退 **portable win-x64 zip** 覆盖当前程序目录

用户数据目录不会被删除。

### 手动更新

1. 托盘退出 GestureClip（任务管理器确认无 `GestureClip.exe`）
2. Setup 用户：再跑一遍新版 `Setup.cmd`（或 Setup.exe）
3. 便携用户：下载新版 zip，**覆盖**旧程序目录

## 用户数据在哪里

```text
%LOCALAPPDATA%\GestureClip\
```

包括剪贴板历史、设置、手势、工位小熊等。  
**安装 / 升级 / 卸载程序文件都不会删除这里。**

## 卸载

- 设置 → 应用 → GestureClip → 卸载  
- 或运行安装目录下的 `uninstall.ps1`  
- 卸载**只删程序目录**，不删 `%LOCALAPPDATA%\GestureClip\`

## 代码签名

正式发布流水线可配置：

- `GESTURECLIP_SIGN_PFX` + `GESTURECLIP_SIGN_PASSWORD`，或  
- `GESTURECLIP_SIGN_THUMBPRINT`

未配置证书时，`scripts/sign-release.ps1` 会跳过签名（本地构建不失败）。  
未签名时 Windows SmartScreen 可能提示未知发布者。

## 更新失败

- 若提示连接 GitHub 失败：检查网络/代理，或浏览器打开 Release 手动下载  
- 若提示 exe 被占用：先退出托盘，任务管理器结束残留进程后再覆盖  

## 不要做

- 不要删除 `%LOCALAPPDATA%\GestureClip\`，除非刻意清空全部数据  
- 不要把数据库拷进程序目录  
- 不要在程序运行时覆盖 exe  
