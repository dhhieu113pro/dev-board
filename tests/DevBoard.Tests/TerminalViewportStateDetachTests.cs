using DevBoard.DevSpaces;
using Xunit;

namespace DevBoard.Tests;

public class TerminalViewportStateDetachTests
{
    [Fact]
    public void Detach_StopsApplyingFutureResizeEvents()
    {
        var state = new TerminalViewportState();
        var calls = 0;
        state.Attach((_, _) => calls++);
        state.Detach();

        state.Update(140, 48);

        Assert.Equal(1, calls);
    }
}
