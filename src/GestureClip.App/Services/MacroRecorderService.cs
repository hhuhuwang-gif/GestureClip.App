using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Macros;
using GestureClip.Features.Macros;
using GestureClip.Infrastructure.Macros;

namespace GestureClip.App.Services;

public sealed partial class MacroRecorderService(IMacroCapture capture, MacroPlayback playback, MacroFileStore store) : INotifyPropertyChanged, IDisposable, IMacroGestureController
{
    private MacroRecorderWindow? _window;
    private CancellationTokenSource? _playCancellation;
    private Task _operation = Task.CompletedTask;
    private bool _transition;
    private bool _recording;
    private bool _playing;
    private string _status = "切换到目标应用后按 F8 录制，结束时再按 F8。";
    private string? _selectedPath;
    private RecordedMacro? _pendingRecording;
    private readonly Stopwatch _watch = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<MacroLibraryEntry> Files { get; } = [];
    public string RecordName { get; set; } = "";
    public string DirectoryPath => store.DirectoryPath;
    public bool IsRecording => _recording;
    public bool IsPlaying => _playing;
    public bool CanRecord => !_transition && !_playing && _hotkeys is not null;
    public bool CanPlay => CanRecord && !_recording && SelectedPath is not null;
    public bool CanChoose => !_recording && !_playing && !_transition;
    public bool CanStop => _recording || _playing;
    public bool CanSaveRecording => _pendingRecording is not null && !_transition;
    public string ToggleText => _recording ? "停止并保存  F8" : "开始录制  F8";
    public string Status => _status;
    public string ElapsedText => _recording ? $"正在录制  {_watch.Elapsed:hh\\:mm\\:ss}" : _playing ? "正在回放" : _idleHeading;
    public string LibrarySummary => Files.Count == 0 ? "还没有操作宏。按 F8 开始第一次录制。" : $"已保存 {Files.Count} 个操作宏，选择后可回放。";

    public string? SelectedPath
    {
        get => _selectedPath;
        set { _selectedPath = value; Notify(); Notify(nameof(CanPlay)); }
    }

    public void ShowWindow()
    {
        if (_window is null)
        {
            _window = new MacroRecorderWindow(this);
            _window.Closed += (_, _) => _window = null;
        }
        _window.Show();
        AttachHost(_window);
        if (_window.WindowState == System.Windows.WindowState.Minimized) _window.WindowState = System.Windows.WindowState.Normal;
        _window.Activate();
    }

    public Task ToggleRecordingAsync() => ExecuteAsync(async () =>
    {
        if (_recording) await StopRecordingAsync();
        else
        {
            if (_pendingRecording is not null) await SaveRecordingCoreAsync();
            await capture.StartAsync(_hostHandle);
            _recording = true;
            _watch.Restart();
            SetStatus("正在记录键盘、鼠标和滚轮。F8 停止并保存；面板内操作不会录入。");
        }
    });

    public Task PlayAsync() => ExecuteAsync(PlayCoreAsync);

    public void StopPlayback() => _playCancellation?.Cancel();

    public async Task StopAndSaveAsync()
    {
        StopPlayback();
        await _operation;
        if (_recording) await ExecuteAsync(StopRecordingAsync);
    }

    private async Task StopRecordingAsync()
    {
        var events = await capture.StopAsync();
        _recording = false;
        _watch.Stop();
        _idleHeading = "录制已保存";
        SetStatus("正在保存…");
        _pendingRecording = new RecordedMacro { Name = RecordName.Trim(), Events = events };
        await SaveRecordingCoreAsync();
    }

    public Task SaveRecordingAsync() => ExecuteAsync(SaveRecordingCoreAsync);

    private async Task SaveRecordingCoreAsync()
    {
        var macro = _pendingRecording ?? throw new InvalidOperationException("没有待保存的录制。");
        var path = await store.SaveAsync(macro);
        _pendingRecording = null;
        await RefreshFilesAsync();
        SelectedPath = path;
        SetStatus($"已保存 {macro.Events.Count} 个事件 · {TimeSpan.FromTicks(macro.Events.LastOrDefault().OffsetTicks).TotalSeconds:F1} 秒。切换到目标应用后按 F9 回放。");
    }

    private async Task PlayCoreAsync()
    {
        if (_recording || SelectedPath is null) throw new InvalidOperationException("请先停止录制并选择已保存的操作宏。");
        var macro = await store.LoadAsync(SelectedPath);
        using var cancellation = new CancellationTokenSource();
        await capture.StartPlaybackStopAsync(cancellation.Cancel);
        _playCancellation = cancellation;
        _playing = true;
        _playDuration = TimeSpan.FromTicks(macro.Events.LastOrDefault().OffsetTicks);
        _playEventCount = macro.Events.Count;
        _playProgress = new MacroPlaybackProgress();
        _watch.Reset();
        SetStatus("回放准备中，松开 F9 后开始。Esc 中止回放。");
        UpdateState();
        try
        {
            await WindowsMacroInputSink.WaitForReplayKeyReleaseAsync(cancellation.Token);
            _watch.Restart();
            SetStatus("正在按原录制时序回放。Esc 中止。");
            ShowPlaybackFeedback();
            await playback.RunAsync(macro, cancellation.Token, _playProgress);
            _idleHeading = "回放完成";
            SetStatus("回放完成。");
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            _idleHeading = "回放已中止";
            SetStatus("回放已中止，已释放回放按下的键与鼠标按钮。");
        }
        finally
        {
            _playing = false;
            _watch.Stop();
            _playCancellation = null;
            ClosePlaybackFeedback();
            await capture.StopAsync();
        }
    }

    private Task ExecuteAsync(Func<Task> action)
    {
        if (_transition) return _operation;
        _operation = ExecuteCoreAsync(action);
        return _operation;
    }

    private async Task ExecuteCoreAsync(Func<Task> action)
    {
        _transition = true;
        UpdateState();
        try { await action(); }
        catch (Exception error) { ReportError(error); }
        finally { _transition = false; UpdateState(); }
    }

    public void ReportError(Exception error)
    {
        Trace.TraceError("Macro recorder: {0}", error);
        _idleHeading = "操作失败";
        SetStatus($"失败：{error.Message}");
    }

    public void Tick()
    {
        if (_recording) Notify(nameof(ElapsedText));
        if (!_playing) return;
        Notify(nameof(PlaybackTime));
        Notify(nameof(PlaybackPercent));
    }

    public void UpdateState()
    {
        UpdateTimer();
        foreach (var property in new[] { nameof(CanSaveRecording), nameof(IsRecording), nameof(IsPlaying), nameof(CanRecord), nameof(CanPlay), nameof(CanChoose), nameof(CanStop), nameof(ToggleText), nameof(ElapsedText) }) Notify(property);
    }

    private async Task RefreshFilesAsync()
    {
        var selected = SelectedPath;
        var entries = await store.ListEntriesAsync();
        Files.Clear();
        foreach (var entry in entries) Files.Add(entry);
        SelectedPath = entries.Any(entry => entry.Path == selected) ? selected : entries.FirstOrDefault()?.Path;
        Notify(nameof(LibrarySummary));
    }

    private void SetStatus(string text) { _status = text; Notify(nameof(Status)); Notify(nameof(ElapsedText)); }
    private void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void Dispose()
    {
        StopPlayback();
        ReleaseHost();
        ClosePlaybackFeedback();
        if (_recording)
        {
            Task.Run(async () =>
            {
                var events = await capture.StopAsync();
                await store.SaveAsync(new RecordedMacro { Name = RecordName.Trim(), Events = events });
            }).GetAwaiter().GetResult();
            _recording = false;
        }
    }
}
