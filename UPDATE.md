# UPDATE · 安装与更新

GestureClip 支持两种交付方式：

| 方式 | 适合 | 说明 |
| --- | --- | --- |
| **Setup 安装包（推荐）** | 普通用户 | 一键装到本机、开始菜单、可卸载、应用内升级 |
| **Portable zip** | 绿色盘 / 临时试用 | 解压即用；一键更新时覆盖程序目录 |

用户数据**始终**在：

```text
%LOCALAPPDATA%\GestureClip\
```

与程序安装目录分离，安装 / 升级 / 卸载都不会清历史。

---

## 推荐：安装程序（客户电脑）

### 你怎么拿到安装包

发布流水线：

```powershell
# 1) 构建 self-contained 程序 + portable zip
powershell -ExecutionPolicy Bypass -File .\scripts\publish-win-x64.ps1

# 2) 打成 Setup 包（无需管理员）
powershell -ExecutionPolicy Bypass -File .\scripts\build-setup.ps1 -SkipPublish
```

产物：

```text
artifacts\release\GestureClip-Setup-v{version}-win-x64.zip   ← 给用户
artifacts\release\installer\GestureClip.iss                  ← 可选：Inno 做单文件 .exe
artifacts\release\GestureClip-v{version}-win-x64.zip         ← 便携包（兼容）
```

若本机装了 [Inno Setup](https://jrsoftware.org/isinfo.php) 且 `iscc` 在 PATH，`build-setup.ps1` 会额外生成：

```text
GestureClip-Setup-v{version}-win-x64.exe
```

### 用户怎么装

1. 下载 `GestureClip-Setup-v*-win-x64.zip` 并解压  
2. **双击 `Setup.cmd`**  
3. 默认安装到：

```text
%LOCALAPPDATA%\Programs\GestureClip\
```

4. 开始菜单出现 GestureClip  

静默安装 / 覆盖升级：

```bat
Setup.cmd /S
```

### 卸载

- Windows「设置 → 应用 → GestureClip → 卸载」  
- 或安装目录下 `uninstall.ps1`  

**不会删除** `%LOCALAPPDATA%\GestureClip\` 用户数据。

---

## 应用内更新（获取安装程序升级）

托盘 / 设置 → **检查更新 / 一键更新**：

1. 读取 GitHub Latest Release  
2. **优先**下载：  
   - `*Setup*.exe`（Inno 等安装程序）  
   - 或 `*Setup*-win-x64.zip`（内含 Setup.cmd）  
3. 若没有安装包，才回退 portable `GestureClip-v*-win-x64.zip` 覆盖当前目录  
4. 下载走多路径（系统代理 → 直连 → 镜像）  
5. 用户数据目录不碰  

发版时请在 GitHub Release **同时上传**：

- `GestureClip-Setup-vX.Y.Z-win-x64.zip`（或 `.exe`）← 主推  
- `GestureClip-vX.Y.Z-win-x64.zip`（可选便携）  
- `SHA256SUMS.txt`  

---

## 为什么以前是「解压覆盖」？

Beta 阶段优先：

- 无代码签名也能分发  
- 不依赖 Inno / WiX 工具链  
- 方便开发自测  

代价是：

- 没有标准「安装 / 卸载」体验  
- 用户要自己选目录  
- 部分杀软对裸 exe 更敏感  

现在补上 **当前用户安装目录 + Setup 包 + 更新优先装安装程序**，更接近正式产品。

---

## 便携包手动覆盖（仍可用）

1. 退出 GestureClip（任务管理器确认无进程）  
2. 解压新版 portable zip  
3. 覆盖旧程序目录  
4. 启动 `GestureClip.exe`  

---

## 不要做

- 不要删 `%LOCALAPPDATA%\GestureClip\`（除非刻意清空数据）  
- 不要把数据库拷进程序目录  
- 不要在程序运行时硬覆盖 exe（安装器会先结束进程）  
