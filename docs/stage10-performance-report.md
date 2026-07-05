# GestureClip 第十阶段性能治理报告

## 参考资料结论

已检索并采用的资料：

- Microsoft Learn：WPF Threading Model。结论：WPF UI 对象有线程亲和性，耗时任务应离开 UI 线程，只把轻量 UI 更新切回 Dispatcher。
- Microsoft Learn：WPF Optimizing Performance: Controls。结论：ListBox/ListView 大量数据应启用 `VirtualizingStackPanel` 与 `VirtualizationMode=Recycling`。
- Microsoft Learn：BitmapImage.DecodePixelWidth。结论：图片应按显示尺寸 decode，避免原图进入列表渲染。
- Microsoft Learn：Freezable/Object Behavior。结论：可冻结的图像资源应 `Freeze()`，减少跨线程和变更通知开销。
- SQLite 官方 WAL 文档。结论：WAL、`busy_timeout`、短事务和批量写入可改善桌面应用读写并发。
- Microsoft.Data.Sqlite / SQLite 事务实践。结论：高频小写入应合并到短事务，避免每次点击独立写库。

未采用：

- FTS 全文索引：当前阶段不做大 migration 和搜索架构重做，先用 limit/debounce/索引控制风险。
- 第三方虚拟化/日志框架：避免新增大型依赖。
- 图片文件外置存储：会改变数据存储策略，本阶段先在 SQLite 中分离原图与缩略图。

## 卡顿来源排序

| 优先级 | 来源 | 文件/方法 | 高频 | 修复策略 |
|---|---|---|---|---|
| P0 | 复制后整页刷新 | `ClipboardOverlayViewModel.CopySelectedAsync` | 快速点击每次触发 | 改为只更新当前项 UseCount，不再 `SearchAsync()` |
| P0 | 使用次数每次单独写库 | `ClipboardService.RecordUseCountInBackground` | 快速点击每次触发 | 250ms 合并，`IncrementUseCountsAsync` 单事务批量写 |
| P0 | 图片列表加载原图 base64 | `ClipboardRepository.SearchAsync` / `ClipboardOverlayWindow.xaml` | 打开/滚动图片列表触发 | 新增 `ThumbnailContent`，列表只取缩略图，复制/粘贴按 ID 取原图 |
| P0 | 图片 decode 在列表中太重 | `ImageBase64ToSourceConverter` / XAML Binding | 滚动和选中图片触发 | `IsAsync=True`，降低 DecodePixelWidth，缓存并 Freeze |
| P0 | 只有首屏限制，没有后续分页 | `ClipboardOverlayViewModel` / `ClipboardRepository.SearchAsync` | 大历史滚动后触发 | 新增 offset 分页、滚动到底自动加载、手动“加载更多”兜底 |
| P0 | 分类筛选只过滤当前页 | `ClipboardOverlayViewModel.SelectedFilter` | 图片/文本/固定筛选触发 | 筛选切换重新走数据库级条件查询，分页时携带筛选条件 |
| P1 | SQLite 锁竞争 | `SqliteConnectionFactory` / `ClipboardRepository` | 捕获、复制、搜索并发 | 已启用 WAL、busy_timeout；新增 contentType/favorite 索引；批量写事务 |
| P1 | 剪贴板 busy 导致异常 | `WpfClipboardWriter` | 快速复制/粘贴偶发 | `ClipboardRetryPolicy` 短重试，UI 层轻提示不崩溃 |

## 代码改动摘要

### UI 线程减负

- `ClipboardOverlayWindow.xaml`
  - ListBox 显式启用虚拟化和 Recycling。
  - 图片 Binding 使用 `IsAsync=True`。
  - 剪贴板图片读取只在 UI Dispatcher 中抓取快照，PNG/DIB/WinForms 编码移到后台队列线程。
  - 列表缩略图 decode 72px，详情预览 220px。

- `ClipboardOverlayViewModel.cs`
  - 复制后不再刷新整页。
  - 删除/置顶/片段只增量更新当前页集合。
  - 复制异常变为轻量错误文案。
  - 首屏只加载 50 条，滚动到底或点击“加载更多”按 offset 追加下一页。
  - 筛选从“当前页内存过滤”改为“数据库级筛选分页”，避免前 50 条没有图片时图片分类为空。

### 数据库优化

- `ClipboardRepository.cs`
  - 新增 `GetByIdAsync`。
  - 新增 `IncrementUseCountsAsync` 批量计数更新。
  - `SearchAsync` 对图片不返回原图 `TextContent`，只返回 `ThumbnailContent`。
  - `SearchAsync(keyword, limit, offset)` 支持分页查询，避免 UI 持有全部历史。
  - `SearchAsync(keyword, limit, offset, filter)` 支持图片/文本/固定/片段数据库级筛选，不新增 migration。

- `DatabaseInitializer.cs`
  - 新增 migration v4：过滤索引。
  - 新增 migration v5：图片缩略图列。

### 图片优化

- `ClipboardItem.cs`
  - 新增 `ThumbnailContent`。

- `ClipboardImageFactory.cs`
  - 新增缩略图生成。
  - BitmapImage 使用 `CacheOption=OnLoad`，可冻结则 `Freeze()`。

- `ClipboardService.cs`
  - 捕获图片时生成缩略图。
  - 复制/粘贴图片时按需读取原图。
  - 旧图片无缩略图时列表不读取原图；用户选中图片预览或复制时才按 ID 读取原图。

### 快速复制优化

- `ClipboardService.cs`
  - 使用次数合并写库。
  - 退出 Stop 时 flush pending usage。

- `WpfClipboardWriter.cs`
  - 写剪贴板加入短重试。

## 测试结果

已运行：

```text
dotnet restore .\GestureClip.sln：通过
dotnet build .\GestureClip.sln --no-restore：通过，0 error
dotnet test .\\GestureClip.sln --no-restore：通过，391 passed
scripts/publish-win-x64.ps1：通过
scripts/check-stage10-performance.ps1：通过，30 targeted tests + release smoke exit passed
```

专项覆盖：

- 1000 条文本历史：首屏只加载 50 条。
- 100 张图片历史：首屏只加载 50 条。
- 增量分页：加载更多追加下一页，不替换已有列表；短页后关闭继续加载入口。
- Repository offset：第二页返回正确数据，不重新读取第一页。
- 数据库级筛选：先筛选再 limit/offset，图片分类不会只看当前 50 条。
- Overlay 筛选：切换图片/文本/固定/片段会重新查询数据库，并且“加载更多”继续携带筛选条件。
- 快速复制 20 次：不触发列表重新查询。
- 快速复制 100 次：不触发重查、不增长列表、UseCount 稳定内存更新。
- 搜索防抖：只执行最后一次关键词查询。
- 旧搜索结果不会覆盖新结果。
- 搜索加载更多继续携带关键词，不会回到全量列表。
- 清空搜索恢复最近历史首屏 50 条，不全量加载。
- 内部复制设置短 suppress，防止监听器把历史复制回流成重复记录；suppress 过期后外部复制恢复。
- 图片列表项只返回缩略图，不返回原图。
- XAML 图片源只绑定 `ThumbnailContent`，防止把图片原图 `TextContent` 重新接回列表/详情预览。
- 点击图片复制时按需加载原图。
- 使用次数快速点击合并写入。
- 手势 HUD 轨迹更新 33ms 节流，工位小熊状态快照 750ms 节流并后台构建。
- 性能日志补充阶段十指标名：`ClipboardCopyDurationMs`、`DbUpdateDurationMs`、`UiRefreshDurationMs`、`SearchDurationMs`、`ThumbnailDecodeDurationMs`、`HudUpdateDurationMs`。
- UI 线程阻塞守护：核心 App/Features/Infrastructure 源码禁止新增 `.Result`、无界 `.Wait()`、`Thread.Sleep`、同步 `Dispatcher.Invoke`，只允许日志 Dispose 的 2 秒限时 flush。
- SQLite WAL/busy_timeout 已测试。
- Clipboard busy 短重试已测试。
- 退出时 usage 队列 flush 已有自动化测试覆盖；release smoke exit 已自动验证；托盘菜单真实点击退出仍建议手动抽查。

## 风险说明

- 旧数据：不删除；新增 `ThumbnailContent` 列。旧图片没有缩略图时，列表不读取原图；选中预览/复制时才按 ID 读取原图。
- 旧设置：不影响。
- 旧手势：不影响 Hook、SendInput、右键拦截。
- 剪贴板历史：保留原图，复制/粘贴图片仍使用原图。
- 需要 migration：需要 v5，低风险，只加列。
- 数据库级筛选不需要额外 migration：复用已有 `ContentType`、`IsPinned`、`IsFavorite` 字段与现有索引。
- 适合打包：适合 v0.5.0 Beta，但建议先手动跑图片历史/快速复制/退出回归。

## 手动验收建议

1. 准备 1000 条文本历史，打开剪贴板，确认窗口不明显卡。
2. 连续点击复制 20 条不同文本，确认 UI 不未响应。
3. 连续点击同一条 20 次，确认没有重复插入风暴。
4. 快速输入搜索词，确认输入不卡，结果稳定。
5. 复制 100 张图片或多次截图，打开图片分类滚动。
6. 点击图片记录，确认能看到缩略图，不异常关闭。
7. 点复制图片，再粘贴到画图/微信/Word，确认是原图。
8. 快速复制后立刻退出，确认 2 秒内进程消失；release smoke exit 已由脚本自动验证。



