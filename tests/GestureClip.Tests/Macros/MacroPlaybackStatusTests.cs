using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Xml.Linq;
using System.Windows.Threading;
using GestureClip.App.Services;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Macros;
using GestureClip.Features.Macros;
using GestureClip.Infrastructure.Macros;
using GestureClip.Infrastructure.Paths;
using GestureClip.Tests.Support;
using Xunit;

namespace GestureClip.Tests.Macros;

public sealed class MacroPlaybackStatusTests
{
    [Fact]
    public async Task Status_progress_can_bind_to_the_actual_readonly_service_property()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { VerifyRuntimeBinding(); completion.SetResult(); }
            catch (Exception error) { completion.SetException(error); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completion.Task.WaitAsync(TestPlatformTimeouts.UiThread);
    }

    private static void VerifyRuntimeBinding()
    {
        // Parse the real element: a source-string assertion cannot expose WPF's default binding mode.
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "GestureClip.sln"))) root = root.Parent;
        var document = XDocument.Load(Path.Combine(root!.FullName, "src", "GestureClip.App", "MacroPlaybackStatusWindow.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var element = document.Descendants(presentation + "ProgressBar").Single();
        var progress = (ProgressBar)XamlReader.Parse(element.ToString());
        var input = new UnusedInput();
        var paths = new AppPathProvider(Path.Combine(Path.GetTempPath(), "MacroStatusTests", Guid.NewGuid().ToString("N")));
        using var service = new MacroRecorderService(input, new MacroPlayback(input), new MacroFileStore(paths));
        progress.DataContext = service;
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        progress.GetBindingExpression(ProgressBar.ValueProperty)!.UpdateTarget();
        Assert.Equal(BindingStatus.Active, progress.GetBindingExpression(ProgressBar.ValueProperty)!.Status);
        Assert.Equal(service.PlaybackPercent, progress.Value);
    }

    private sealed class UnusedInput : IMacroCapture, IMacroInputSink
    {
        public Task StartAsync(nint excludedWindow) => throw new NotSupportedException();
        public Task StartPlaybackStopAsync(Action stop) => throw new NotSupportedException();
        public Task ExcludeGestureAsync(MacroGestureInterval gesture) => throw new NotSupportedException();
        public Task<IReadOnlyList<MacroEvent>> StopAsync() => throw new NotSupportedException();
        public void Send(MacroEvent input) => throw new NotSupportedException();
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
