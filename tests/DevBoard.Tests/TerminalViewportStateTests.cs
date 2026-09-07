using DevBoard.DevSpaces;
using Xunit;

namespace DevBoard.Tests;

public class TerminalViewportStateTests
{
    [Fact]
    public void Attach_ReplaysSizeObservedBeforePtyExists()
    {
        var state = new TerminalViewportState();
        state.Update(132, 44);

        (int Columns, int Rows)? applied = null;
        state.Attach((columns, rows) => applied = (columns, rows));

        Assert.Equal((132, 44), applied);
    }

    [Fact]
    public void Update_AppliesNewSizeAfterPtyIsAttached()
    {
        var state = new TerminalViewportState();
        (int Columns, int Rows)? applied = null;
        state.Attach((columns, rows) => applied = (columns, rows));

        state.Update(101, 37);

        Assert.Equal((101, 37), applied);
    }

    [Fact]
    public void InvalidSize_DoesNotReplaceLastValidViewport()
    {
        var state = new TerminalViewportState();
        state.Update(120, 40);
        state.Update(0, 0);

        (int Columns, int Rows)? applied = null;
        state.Attach((columns, rows) => applied = (columns, rows));

        Assert.Equal((120, 40), applied);
    }
}
