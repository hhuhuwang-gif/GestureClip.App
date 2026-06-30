using GestureClip.Features.Clipboard;
using Xunit;

namespace GestureClip.Tests.Clipboard;

public sealed class ClipboardHashServiceTests
{
    [Fact]
    public void ComputeHash_uses_exact_text()
    {
        var service = new ClipboardHashService();

        var first = service.ComputeHash("hello");
        var second = service.ComputeHash("hello ");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ComputePlainTextHash_normalizes_line_endings_and_outer_whitespace()
    {
        var service = new ClipboardHashService();

        var first = service.ComputePlainTextHash("  hello\r\nworld  ");
        var second = service.ComputePlainTextHash("hello\nworld");

        Assert.Equal(first, second);
    }
}
