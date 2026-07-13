# GestureClip v0.6.14 Beta 发版检查清单

## 1. 已自动完成（本机）

- [x] 版本号 bump 到 `0.6.14-beta` / `0.6.14 Beta`
- [x] `dotnet test ./GestureClip.sln`：**575 passed**
- [x] `scripts/publish-win-x64.ps1`：成功
- [x] `scripts/check-release.ps1`：通过
- [x] 发布包 smoke：`--smoke-exit-after-startup` exit 0
- [x] 日志确认：`GestureClip v0.6.14 Beta ... started.`

### 产物路径

| 产物 | 路径 |
|------|------|
| 解压目录 | `artifacts/release/GestureClip/` |
| 安装包 | `artifacts/release/GestureClip-v0.6.14-beta-win-x64.zip` |
| 校验和 | `artifacts/release/SHA256SUMS.txt` |
| 根目录 exe | `GestureClip.exe`（已同步更新） |
| Release 草稿 | `docs/github-release-v0.6.14-beta.md` |

### SHA256

```text
78B03DA4FF0738BE23FBA45042B180D6257D30FDFD0E95DD0DADE6E57CC52D33  GestureClip-v0.6.14-beta-win-x64.zip
```

包内文件应包含：

- `GestureClip.exe`
- `README.md` / `CHANGELOG.md` / `HELP.md` / `UPDATE.md` / `BETA_TEST.md` / `KNOWN_ISSUES.md`
- `SHA256SUMS.txt`

---

## 2. 发 GitHub Release 前（人工）

1. 确认分支干净或已提交/打 tag（建议 `v0.6.14-beta`）。
2. 打开 `docs/github-release-v0.6.14-beta.md`，把正文贴到 Release 说明。
3. 上传：
   - `artifacts/release/GestureClip-v0.6.14-beta-win-x64.zip`
   - `artifacts/release/SHA256SUMS.txt`
4. Tag 名：`v0.6.14-beta`（与 README 下载链接一致）。
5. 设为 **Latest**。
6. 核对 README 下载链接是否指向该 tag。

示例命令（需已登录 `gh`）：

```bash
gh release create v0.6.14-beta `
  "artifacts/release/GestureClip-v0.6.14-beta-win-x64.zip" `
  "artifacts/release/SHA256SUMS.txt" `
  --title "GestureClip v0.6.14 Beta" `
  --notes-file "docs/github-release-v0.6.14-beta.md"
```

---

## 3. 发布后冒烟（真机，约 10 分钟）

按 `docs/regression-checklist.md` 精简执行：

### 安装 / 启动

- [ ] 解压 zip，双击 `GestureClip.exe` 能启动
- [ ] 托盘图标出现；关设置窗后仍在托盘
- [ ] 关于页版本显示 **0.6.14 Beta**

### 核心功能

- [ ] `Ctrl + \`` 打开/关闭剪贴板历史
- [ ] 复制文本/图片进入历史；双击复制并关闭
- [ ] 右键上划复制、下划粘贴（或你的自定义绑定）
- [ ] `Ctrl + Shift + Q` 打开快捷动作，做一次「去空格」或「JSON 美化」
- [ ] 设置 → 动作绑定 → 左键增强可改一条并保存

### 工位小熊

- [ ] 托盘 → 工位小熊 → 打开 Hub
- [ ] 首次/未配月薪时出现 30 秒配置（或设置里已有月薪则不出现）
- [ ] 分区可折叠；今日已赚/下班倒计时正常
- [ ] 托盘「今日已赚摘要」弹出气泡

### 更新

- [ ] 关于页「检查更新」：当前应提示已是最新（发完本版后）
- [ ] 检查更新弹窗能显示说明文字，不直接报连接失败

### 退出

- [ ] 托盘退出后，任务管理器无残留 `GestureClip.exe`

---

## 4. 已知限制（可写进 Release）

- 未代码签名，SmartScreen 可能提示未知发布者
- 管理员窗口下，普通权限可能无法稳定模拟输入
- 覆盖更新前需先退出程序
- 大图历史首次缩略图仍可能稍慢

---

## 5. 发版后

- [ ] 在 Issues / 群里贴下载链接 + 本版重点（快捷动作、小熊 Hub、UI）
- [ ] 收集 1～2 天 Beta 反馈，再决定 0.6.15 或进 v0.7
