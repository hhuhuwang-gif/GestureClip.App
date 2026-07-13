# GestureClip v0.6.15 Beta

## 下载

- GestureClip-v0.6.15-beta-win-x64.zip
- SHA256SUMS.txt

## 本次重点

- 剪贴板历史：**一键回顶部**（`Home` / 按钮）、`End` 到底、打开后选中最新。
- **重置视图**：清空搜索，必要时恢复「全部」筛选并回顶。
- **`Ctrl+Enter`** 粘贴后保持面板打开；`Enter` 仍为粘贴并关闭。
- 删除后自动选中下一条；**`Ctrl+Z` / 撤销删除**可恢复最近一次删除。
- **`Ctrl+Shift+C`** 复制为纯文本；右键与详情区有入口。
- **`?` / `F1`** 快捷键速查；底栏提示随状态变化。

## 使用提示

1. `Ctrl + \`` 打开剪贴板 → 试 `Home` 回顶、下滑后点「↑ 顶部」。
2. 复制一条后按 `Ctrl+Shift+C` 试纯文本；`Ctrl+Enter` 粘贴且不关窗。
3. 删除一条后立刻 `Ctrl+Z` 或点「撤销删除」。
4. 按 `?` 或 `F1` 查看完整快捷键表。

## 本地数据

- 覆盖更新会保留 `%LOCALAPPDATA%\GestureClip` 下的历史、设置与日志。
- 诊断包不包含剪贴板正文与图片原图。

## 校验

- `dotnet test ./GestureClip.sln`
- `scripts/publish-win-x64.ps1`
- `scripts/check-release.ps1`
