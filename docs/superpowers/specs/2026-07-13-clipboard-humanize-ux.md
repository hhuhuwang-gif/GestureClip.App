# 剪贴板面板人性化快捷 · 规格

**状态**：已实现（测试 578 通过）  
**版本目标**：v0.6.15（本迭代功能合入；未发版前可先落 main）  
**范围**：剪贴板历史面板（`ClipboardOverlayWindow` + `ClipboardOverlayViewModel`）

## 背景

长列表缺少「回顶 / 选最新」导航；删除后焦点跳失；粘贴只能关窗；误删无撤销；快捷键可发现性弱。

## 本迭代交付（批 1 + B3 + B8）

| ID | 功能 | 验收 |
| --- | --- | --- |
| A1 | 一键回顶部 | 列表滚动后显示「↑ 顶部」；`Home` / `Ctrl+Home` 滚顶 |
| A2 | 滚到底部 | `End` / `Ctrl+End` |
| A3 | 打开后选最新 | `Load` 后滚顶并选中第 1 条 |
| A4/B5 | 重置视图 | 「清空」：清搜索；若已无搜索则筛回复「全部」；滚顶 |
| B2 | 粘贴不关 | `Ctrl+Enter` 粘贴且保持面板 |
| B7 | 删后选下一条 | 删除后选中原位置下一条（或上一条） |
| B9 | 状态反馈 | 复制/粘贴/删除/撤销有明确 `StatusText` |
| C1 | 快捷键速查 | `?` / `F1` 切换浮层 |
| C2 | 底栏提示 | 常驻快捷摘要；随可撤销状态变化 |
| B3 | 复制纯文本 | 文本：去首尾空行、压多余空行后只写 Unicode 文本；`Ctrl+Shift+C`；右键菜单 |
| B8 | 误删撤销 | 最近一次删除可 `Ctrl+Z` / 状态栏「撤销」恢复 DB 记录（内存缓存完整 `ClipboardItem`） |

## 非目标

- 云同步、跨设备、账号  
- 永久回收站 / 删除历史页  
- 大改布局  
- 打开面板「记住滚动位置」（后续 A6）

## 技术要点

- 导航：`ScrollViewer` from `HistoryList`；`ScrollToVerticalOffset(0)` / `ScrollToEnd`
- 撤销：`IClipboardService.RestoreItemsAsync` → `IClipboardRepository.InsertAsync`；仅保留最近一批
- 纯文本：ViewModel 对全文 `CleanText` 后构造带 `TextContent` 的文本项再 `CopyItemsAsync`（避免再拉脏全文覆盖）
- 测试：ViewModel 单测覆盖删除选中、撤销、纯文本、重置视图；不测纯 UI 滚动

## 文档

- 更新 `HELP.md` 剪贴板快捷键表  
- `CHANGELOG` 记入 v0.6.15 草稿条目（若暂不 bump 版本则写「未发布」）
