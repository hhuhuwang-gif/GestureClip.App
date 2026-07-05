# GestureClip 第十阶段完成审计矩阵

状态说明：

- 已验证：有自动化测试、脚本、构建或代码证据。
- 待人工：必须真实桌面手感确认，自动化无法完全替代。

## P0 快速点击复制

| 要求 | 状态 | 证据 |
|---|---|---|
| 点击后 UI 立即反馈 | 已验证 | `ClipboardOverlayViewModel.CopySelectedAsync` 先更新 `StatusText`，不等待重新查询 |
| 复制后不刷新整个列表 | 已验证 | `ClipboardOverlayViewModelTests.CopySelectedAsync_does_not_reload_list_after_copy` |
| 连续复制 20 次不触发重查 | 已验证 | `ClipboardOverlayViewModelTests.CopySelectedAsync_handles_20_fast_clicks_without_reloading` |
| usageCount 后台合并写入 | 已验证 | `ClipboardServiceTests.CopyItemsAsync_coalesces_repeated_use_count_updates` |
| 退出前 flush pending usage | 已验证 | `ClipboardServiceTests.StopAsync_flushes_pending_use_count_updates` |
| Clipboard busy 不崩溃 | 已验证 | `ClipboardRetryPolicyTests` + `ClipboardOverlayViewModelTests.CopySelectedAsync_shows_error_without_throwing_when_clipboard_write_fails` |
| 内部复制短 suppress 防止剪贴板事件回流 | 已验证 | `ClipboardServiceTests.CopyItemsAsync_suppresses_capture_to_prevent_internal_copy_feedback_loop` |
| suppress 过期后不屏蔽真实外部复制 | 已验证 | `ClipboardServiceTests.CaptureTextAsync_resumes_after_suppress_window_expires` |
| 真实快速点击手感不卡 | 待人工 | 需在发布版实际连续点击验证 |

## P0 列表虚拟化与首屏限制

| 要求 | 状态 | 证据 |
|---|---|---|
| ListBox 启用虚拟化 | 已验证 | `ThemeResourceTests.ClipboardOverlayWindow_uses_glass_panel_styling` |
| Recycling 模式 | 已验证 | 同上 |
| 首屏限制 50 条 | 已验证 | `ClipboardOverlayViewModelTests.LoadAsync_uses_default_limit_50` |
| 1000 文本首屏只取 50 | 已验证 | `ClipboardOverlayViewModelTests.LoadAsync_with_1000_text_items_keeps_first_page_to_50` |
| 100 图片首屏只取 50 | 已验证 | `ClipboardOverlayViewModelTests.LoadAsync_with_100_image_items_keeps_first_page_to_50` |
| 滚动/加载更多增量页 | 已验证 | `ClipboardOverlayViewModelTests.LoadMoreAsync_appends_next_page_without_replacing_existing_items` + `ThemeResourceTests.ClipboardOverlayWindow_exposes_load_more_entry_point` |
| Repository 支持 offset 分页 | 已验证 | `ClipboardRepositoryTests.SearchAsync_with_offset_returns_next_page` |
| 图片/文本/固定/片段筛选先走数据库再分页 | 已验证 | `ClipboardRepositoryTests.SearchAsync_filters_images_before_limit_and_offset` |
| Overlay 切换筛选会重新查询数据库 | 已验证 | `ClipboardOverlayViewModelTests.SelectedFilter_queries_database_filter_instead_of_current_page` |
| 加载更多继续携带当前筛选 | 已验证 | `ClipboardOverlayViewModelTests.LoadMoreAsync_uses_selected_database_filter` |
| 真实滚动不卡 | 待人工 | 需发布版滚动测试 |

## P0 数据库与队列

| 要求 | 状态 | 证据 |
|---|---|---|
| SQLite WAL | 已验证 | `SqliteConnectionFactory` 设置 `PRAGMA journal_mode = WAL` |
| busy_timeout | 已验证 | `ClipboardRepositoryTests.ConnectionFactory_sets_busy_timeout_for_clipboard_contention` |
| 批量写事务 | 已验证 | `ClipboardRepository.IncrementUseCountsAsync` + `ClipboardRepositoryTests.IncrementUseCountsAsync_adds_counts_in_one_batch` |
| 内容类型/收藏索引 | 已验证 | `ClipboardPerformanceV2Migration` + `SqlMigrationRunnerTests.ClipboardPerformanceV2Migration_creates_filter_indexes` |
| 缩略图列 migration | 已验证 | `ClipboardThumbnailMigration` + `SqlMigrationRunnerTests.ClipboardThumbnailMigration_adds_thumbnail_column` |

## P0 图片路径

| 要求 | 状态 | 证据 |
|---|---|---|
| 列表不读取图片原图 | 已验证 | `ClipboardRepository.SearchAsync` 对 `image/png` 返回 `TextContent = NULL` |
| 列表读取缩略图 | 已验证 | `ClipboardRepositoryTests.SearchAsync_returns_image_thumbnail_without_full_image_content` |
| 旧图片列表不回退原图 | 已验证 | `ClipboardRepositoryTests.SearchAsync_does_not_return_full_image_for_legacy_rows_without_thumbnail` |
| 复制图片按需读取原图 | 已验证 | `ClipboardServiceTests.CopyItemsAsync_loads_full_image_content_when_list_item_only_has_thumbnail` |
| 剪贴板图片读取在 Dispatcher 中只抓首个可用格式快照，PNG/DIB 编码移出 UI 线程，并按 PNG→DIB→WPF→WinForms 短路 | 已验证 | `ClipboardImageDataReaderTests.WpfClipboardTextReader_captures_snapshot_before_encoding_image_data` |
| 图片 decode 小尺寸 | 已验证 | XAML `ConverterParameter=72/220`，`ImageBase64ToSourceConverterTests` |
| 图片 Binding 异步 | 已验证 | `ThemeResourceTests.ClipboardOverlayWindow_uses_glass_panel_styling` |
| 图片列表/详情预览不把原图 `TextContent` 绑定到 `Image.Source` | 已验证 | `ThemeResourceTests.ClipboardOverlay_uses_large_image_preview_cards_without_base64_text_for_images` |
| 真实图片点击不卡 | 待人工 | 需发布版点击图片项验证 |

## P0 搜索防抖

| 要求 | 状态 | 证据 |
|---|---|---|
| 搜索 debounce | 已验证 | `ClipboardOverlayViewModelTests.SearchText_debounces_rapid_input_and_searches_last_keyword` |
| 旧搜索不覆盖新搜索 | 已验证 | `ClipboardOverlayViewModelTests.Older_search_result_does_not_replace_newer_result` |
| 搜索限制返回数量 | 已验证 | `ClipboardService.SearchAsync`/`ClipboardRepository.SearchAsync` limit clamp + tests |
| 搜索分页继续携带关键词 | 已验证 | `ClipboardOverlayViewModelTests.LoadMoreAsync_keeps_search_keyword_when_loading_next_page` |
| 清空搜索恢复最近历史首屏，不全量加载 | 已验证 | `ClipboardOverlayViewModelTests.ClearSearchAsync_restores_recent_history_first_page_without_full_reload` |
| 真实输入不卡 | 待人工 | 需发布版搜索框快速输入验证 |

## P1 HUD / 统计 / 日志

| 要求 | 状态 | 证据 |
|---|---|---|
| 手势轨迹更新节流，避免鼠标移动时 UI 风暴 | 已验证 | `GestureOverlayWindowTests.GestureOverlayService_throttles_trace_updates_and_backgrounds_workstation_snapshot` |
| 工位小熊状态快照后台构建 | 已验证 | 同上，检查 `Task.Run(async () => BuildSnapshotAsync(...))` |
| 工位小熊状态刷新节流 | 已验证 | 同上，检查 `TotalMilliseconds < 750` |
| 日志异步队列，写文件不阻塞主路径 | 已验证 | `LoggingServiceCollectionExtensions` 使用 bounded `Channel<string>` 和后台 writer |
| 性能日志包含阶段十诊断指标名 | 已验证 | `ClipboardServiceTests.ClipboardService_exposes_stage10_performance_metric_names` 覆盖 `ClipboardCopyDurationMs`、`DbUpdateDurationMs`、`UiRefreshDurationMs`、`SearchDurationMs`、`ThumbnailDecodeDurationMs`、`HudUpdateDurationMs` |
| 核心路径无常见 UI 阻塞模式 | 已验证 | `Stage10UiThreadGuardTests.App_feature_and_infrastructure_sources_do_not_add_blocking_ui_thread_patterns` |

## P1/P2 退出和发布

| 要求 | 状态 | 证据 |
|---|---|---|
| release exe 可启动并退出 | 已验证 | `scripts/check-stage10-performance.ps1` release smoke exit |
| 退出释放 runtime | 已验证 | `AppLifecycleService.StopRuntimeAsync` stop hotkey/edge/mouse/clipboard/tray，每步 2 秒 timeout |
| 真实托盘菜单退出无残留 | 待人工 | 需右键托盘点退出后看任务管理器 |
| 发布包更新 | 已验证 | `GestureClip.exe` 与 release zip 时间 2026/7/5 1:05 后 |

## 命令证据

最近通过：

```text
dotnet build .\GestureClip.sln --no-restore：0 error
dotnet test .\GestureClip.sln --no-restore：398 passed
scripts\publish-win-x64.ps1：成功
scripts\check-stage10-performance.ps1：30 个分页/筛选/图片/快速复制/防回流/搜索/HUD/性能日志/UI阻塞守护/图片读取专项测试 + release smoke exit passed
```

## 结论

自动化、代码、发布包证据已覆盖第十阶段主要技术目标。

仍需人工确认的只剩真实手感：

1. 快速复制 20 次是否不卡。
2. 图片预览点击是否不卡。
3. 搜索快速输入是否不卡。
4. 托盘菜单退出后任务管理器无残留。


