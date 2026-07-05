$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

Write-Host "== GestureClip Stage 10 performance checks =="
Write-Host "Workspace: $root"

$filter = "FullyQualifiedName~ClipboardOverlayViewModelTests.LoadAsync_with_1000|FullyQualifiedName~ClipboardOverlayViewModelTests.LoadAsync_with_100_image|FullyQualifiedName~ClipboardOverlayViewModelTests.LoadMoreAsync|FullyQualifiedName~ClipboardOverlayViewModelTests.CopySelectedAsync_handles_20|FullyQualifiedName~ClipboardOverlayViewModelTests.CopySelectedAsync_handles_100_fast|FullyQualifiedName~ClipboardOverlayViewModelTests.SearchText_debounces|FullyQualifiedName~ClipboardOverlayViewModelTests.SelectedFilter_queries_database_filter|FullyQualifiedName~ClipboardOverlayViewModelTests.LoadMoreAsync_uses_selected_database_filter|FullyQualifiedName~ClipboardOverlayViewModelTests.LoadMoreAsync_keeps_search_keyword|FullyQualifiedName~ClipboardOverlayViewModelTests.ClearSearchAsync_restores_recent_history_first_page|FullyQualifiedName~ClipboardRepositoryTests.SearchAsync_with_offset_returns_next_page|FullyQualifiedName~ClipboardRepositoryTests.SearchAsync_filters_images_before_limit_and_offset|FullyQualifiedName~ClipboardRepositoryTests.SearchAsync_returns_image_thumbnail|FullyQualifiedName~ClipboardRepositoryTests.SearchAsync_does_not_return_full_image_for_legacy|FullyQualifiedName~ClipboardServiceTests.CopyItemsAsync_coalesces|FullyQualifiedName~ClipboardServiceTests.CopyItemsAsync_loads_full_image|FullyQualifiedName~ClipboardServiceTests.CopyItemsAsync_suppresses_capture|FullyQualifiedName~ClipboardServiceTests.CaptureTextAsync_resumes_after_suppress_window_expires|FullyQualifiedName~ClipboardServiceTests.StopAsync_flushes_pending_use_count_updates|FullyQualifiedName~ClipboardServiceTests.ClipboardService_exposes_stage10_performance_metric_names|FullyQualifiedName~ClipboardRetryPolicyTests|FullyQualifiedName~ClipboardImageDataReaderTests.WpfClipboardTextReader_captures_snapshot|FullyQualifiedName~ClipboardImageDataReaderTests.WpfClipboardTextReader_short_circuits|FullyQualifiedName~SqlMigrationRunnerTests.ClipboardThumbnail|FullyQualifiedName~ThemeResourceTests.App_supports_smoke_exit|FullyQualifiedName~ThemeResourceTests.ClipboardOverlay_uses_large_image_preview_cards_without_base64_text_for_images|FullyQualifiedName~GestureOverlayWindowTests.GestureOverlayService_throttles_trace_updates|FullyQualifiedName~Stage10UiThreadGuardTests"

Write-Host "Running targeted tests..."
dotnet test .\GestureClip.sln --no-restore --filter $filter

Write-Host "Checking release exe..."
$exe = Join-Path $root "GestureClip.exe"
if (-not (Test-Path $exe)) {
    throw "GestureClip.exe not found: $exe"
}
Get-Item $exe | Select-Object FullName, Length, LastWriteTime | Format-List

Write-Host "Running release smoke exit..."
$process = Start-Process -FilePath $exe -ArgumentList "--smoke-exit-after-startup" -PassThru
if (-not $process.WaitForExit(8000)) {
    try { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue } catch {}
    throw "GestureClip release smoke exit timed out. ProcessId=$($process.Id)"
}
if ($process.ExitCode -ne 0) {
    throw "GestureClip release smoke exit failed. ExitCode=$($process.ExitCode)"
}
Write-Host "Stage 10 targeted checks passed. Manual GUI checks still recommended: fast copy, image preview, image paste, fast search."




