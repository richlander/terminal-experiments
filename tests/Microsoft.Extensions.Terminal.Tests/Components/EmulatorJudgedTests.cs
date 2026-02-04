// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Text;
using Microsoft.Extensions.Terminal.Components;
using Microsoft.Extensions.Terminal.Parser;
using Xunit;
using ParserScreenBuffer = Microsoft.Extensions.Terminal.Parser.ScreenBuffer;
using ComponentScreenBuffer = Microsoft.Extensions.Terminal.Components.ScreenBuffer;

namespace Microsoft.Extensions.Terminal.Tests.Components;

/// <summary>
/// Tests that use the VT parser's ScreenBuffer as a judge to verify
/// that component rendering produces correct terminal output.
/// 
/// Flow: Component → ComponentScreenBuffer → TestTerminal (ANSI) → VtParser → ParserScreenBuffer → Assert
/// </summary>
public class EmulatorJudgedTests
{
    /// <summary>
    /// Renders a component and returns the parsed screen state.
    /// </summary>
    private static ParserScreenBuffer RenderAndParse(IComponent component, int width, int height)
    {
        // Step 1: Render component to our component screen buffer
        var componentBuffer = new ComponentScreenBuffer(width, height);
        component.Render(componentBuffer, new Region(0, 0, width, height));

        // Step 2: Flush to test terminal (captures ANSI output)
        var terminal = new TestTerminal(width, height);
        componentBuffer.Flush(terminal);

        // Step 3: Parse ANSI output through VT parser into parser screen buffer
        var parserBuffer = new ParserScreenBuffer(width, height);
        var parser = new VtParser(parserBuffer);
        parser.Parse(terminal.OutputBytes);

        return parserBuffer;
    }

    #region Text Component Tests

    [Fact]
    public void Text_SimpleString_AppearsAtCorrectPosition()
    {
        var text = new Text().Append("Hello");
        var screen = RenderAndParse(text, 20, 5);

        // Verify each character is at the expected position
        Assert.Equal('H', screen.GetCell(0, 0).Character);
        Assert.Equal('e', screen.GetCell(1, 0).Character);
        Assert.Equal('l', screen.GetCell(2, 0).Character);
        Assert.Equal('l', screen.GetCell(3, 0).Character);
        Assert.Equal('o', screen.GetCell(4, 0).Character);
    }

    [Fact]
    public void Text_MultiLine_RendersOnSeparateRows()
    {
        var text = new Text()
            .AppendLine("Line1")
            .Append("Line2");

        var screen = RenderAndParse(text, 20, 5);

        // First line
        Assert.Equal('L', screen.GetCell(0, 0).Character);
        Assert.Equal('i', screen.GetCell(1, 0).Character);
        Assert.Equal('n', screen.GetCell(2, 0).Character);
        Assert.Equal('e', screen.GetCell(3, 0).Character);
        Assert.Equal('1', screen.GetCell(4, 0).Character);

        // Second line
        Assert.Equal('L', screen.GetCell(0, 1).Character);
        Assert.Equal('i', screen.GetCell(1, 1).Character);
        Assert.Equal('n', screen.GetCell(2, 1).Character);
        Assert.Equal('e', screen.GetCell(3, 1).Character);
        Assert.Equal('2', screen.GetCell(4, 1).Character);
    }

    [Fact]
    public void Text_WrapsAtBoundary_ContinuesOnNextLine()
    {
        var text = new Text().Append("ABCDEFGHIJ"); // 10 chars into 5-wide region

        // Render into a 5x3 region
        var componentBuffer = new ComponentScreenBuffer(10, 5);
        text.Render(componentBuffer, new Region(0, 0, 5, 3));

        var terminal = new TestTerminal(10, 5);
        componentBuffer.Flush(terminal);

        var screen = new ParserScreenBuffer(10, 5);
        var parser = new VtParser(screen);
        parser.Parse(terminal.OutputBytes);

        // First row: ABCDE
        Assert.Equal('A', screen.GetCell(0, 0).Character);
        Assert.Equal('E', screen.GetCell(4, 0).Character);

        // Second row: FGHIJ
        Assert.Equal('F', screen.GetCell(0, 1).Character);
        Assert.Equal('J', screen.GetCell(4, 1).Character);
    }

    #endregion

    #region Panel Component Tests

    [Fact]
    public void Panel_SimpleBorder_DrawsCorrectCorners()
    {
        var panel = new Panel { Border = BoxBorderStyle.Simple };
        var screen = RenderAndParse(panel, 10, 5);

        // Corners
        Assert.Equal('┌', screen.GetCell(0, 0).Character);
        Assert.Equal('┐', screen.GetCell(9, 0).Character);
        Assert.Equal('└', screen.GetCell(0, 4).Character);
        Assert.Equal('┘', screen.GetCell(9, 4).Character);
    }

    [Fact]
    public void Panel_RoundedBorder_DrawsRoundedCorners()
    {
        var panel = new Panel { Border = BoxBorderStyle.Rounded };
        var screen = RenderAndParse(panel, 10, 5);

        Assert.Equal('╭', screen.GetCell(0, 0).Character);
        Assert.Equal('╮', screen.GetCell(9, 0).Character);
        Assert.Equal('╰', screen.GetCell(0, 4).Character);
        Assert.Equal('╯', screen.GetCell(9, 4).Character);
    }

    [Fact]
    public void Panel_DoubleBorder_DrawsDoubleLines()
    {
        var panel = new Panel { Border = BoxBorderStyle.Double };
        var screen = RenderAndParse(panel, 10, 5);

        Assert.Equal('╔', screen.GetCell(0, 0).Character);
        Assert.Equal('╗', screen.GetCell(9, 0).Character);
        Assert.Equal('╚', screen.GetCell(0, 4).Character);
        Assert.Equal('╝', screen.GetCell(9, 4).Character);
        Assert.Equal('═', screen.GetCell(1, 0).Character); // Top horizontal
        Assert.Equal('║', screen.GetCell(0, 1).Character); // Left vertical
    }

    [Fact]
    public void Panel_WithHeader_HeaderAppearsOnTopBorder()
    {
        var panel = new Panel
        {
            Header = "Test",
            Border = BoxBorderStyle.Simple
        };
        var screen = RenderAndParse(panel, 20, 5);

        // Header should appear after corner, offset by 2
        Assert.Equal('T', screen.GetCell(2, 0).Character);
        Assert.Equal('e', screen.GetCell(3, 0).Character);
        Assert.Equal('s', screen.GetCell(4, 0).Character);
        Assert.Equal('t', screen.GetCell(5, 0).Character);
    }

    [Fact]
    public void Panel_WithContent_ContentAppearsInside()
    {
        var panel = new Panel
        {
            Border = BoxBorderStyle.Simple,
            Content = new Text().Append("Inside")
        };
        var screen = RenderAndParse(panel, 20, 5);

        // Content starts at (1, 1) inside the border
        Assert.Equal('I', screen.GetCell(1, 1).Character);
        Assert.Equal('n', screen.GetCell(2, 1).Character);
        Assert.Equal('s', screen.GetCell(3, 1).Character);
        Assert.Equal('i', screen.GetCell(4, 1).Character);
        Assert.Equal('d', screen.GetCell(5, 1).Character);
        Assert.Equal('e', screen.GetCell(6, 1).Character);
    }

    #endregion

    #region Table Component Tests

    [Fact]
    public void Table_HeaderRow_AppearsAtTop()
    {
        var table = new Table { Border = TableBorderStyle.None, ShowHeader = true };
        table.AddColumn("Name", width: 10);
        table.AddRow("Alice");

        var screen = RenderAndParse(table, 20, 5);

        // Header "Name" should appear
        Assert.Equal('N', screen.GetCell(0, 0).Character);
        Assert.Equal('a', screen.GetCell(1, 0).Character);
        Assert.Equal('m', screen.GetCell(2, 0).Character);
        Assert.Equal('e', screen.GetCell(3, 0).Character);
    }

    [Fact]
    public void Table_DataRow_AppearsAfterHeader()
    {
        var table = new Table { Border = TableBorderStyle.None, ShowHeader = true };
        table.AddColumn("Name", width: 10);
        table.AddRow("Bob");

        var screen = RenderAndParse(table, 20, 5);

        // Data row should be on line 1 (after header on line 0)
        Assert.Equal('B', screen.GetCell(0, 1).Character);
        Assert.Equal('o', screen.GetCell(1, 1).Character);
        Assert.Equal('b', screen.GetCell(2, 1).Character);
    }

    [Fact]
    public void Table_RightAlignment_PadsLeft()
    {
        var table = new Table { Border = TableBorderStyle.None, ShowHeader = false };
        table.AddColumn("Value", width: 10, alignment: Alignment.Right);
        table.AddRow("123");

        var screen = RenderAndParse(table, 20, 5);

        // "123" right-aligned in 10 chars = 7 spaces then "123"
        Assert.Equal(' ', screen.GetCell(0, 0).Character);
        Assert.Equal(' ', screen.GetCell(6, 0).Character);
        Assert.Equal('1', screen.GetCell(7, 0).Character);
        Assert.Equal('2', screen.GetCell(8, 0).Character);
        Assert.Equal('3', screen.GetCell(9, 0).Character);
    }

    [Fact]
    public void Table_CenterAlignment_PadsBothSides()
    {
        var table = new Table { Border = TableBorderStyle.None, ShowHeader = false };
        table.AddColumn("Value", width: 10, alignment: Alignment.Center);
        table.AddRow("AB");

        var screen = RenderAndParse(table, 20, 5);

        // "AB" centered in 10 chars = 4 spaces, "AB", 4 spaces
        Assert.Equal(' ', screen.GetCell(0, 0).Character);
        Assert.Equal(' ', screen.GetCell(3, 0).Character);
        Assert.Equal('A', screen.GetCell(4, 0).Character);
        Assert.Equal('B', screen.GetCell(5, 0).Character);
        Assert.Equal(' ', screen.GetCell(6, 0).Character);
    }

    #endregion

    #region Rule Component Tests

    [Fact]
    public void Rule_NoTitle_DrawsFullLine()
    {
        var rule = new Rule { LineChar = '─' };

        var componentBuffer = new ComponentScreenBuffer(20, 3);
        rule.Render(componentBuffer, new Region(0, 1, 20, 1));

        var terminal = new TestTerminal(20, 3);
        componentBuffer.Flush(terminal);

        var screen = new ParserScreenBuffer(20, 3);
        var parser = new VtParser(screen);
        parser.Parse(terminal.OutputBytes);

        // Entire row should be line characters
        for (int x = 0; x < 20; x++)
        {
            Assert.Equal('─', screen.GetCell(x, 1).Character);
        }
    }

    [Fact]
    public void Rule_WithTitle_TitleIsCentered()
    {
        var rule = new Rule { Title = "Test", LineChar = '─' };

        var componentBuffer = new ComponentScreenBuffer(20, 3);
        rule.Render(componentBuffer, new Region(0, 1, 20, 1));

        var terminal = new TestTerminal(20, 3);
        componentBuffer.Flush(terminal);

        var screen = new ParserScreenBuffer(20, 3);
        var parser = new VtParser(screen);
        parser.Parse(terminal.OutputBytes);

        // Find "Test" in the output - it should be roughly centered
        bool foundT = false;
        for (int x = 0; x < 20; x++)
        {
            if (screen.GetCell(x, 1).Character == 'T')
            {
                foundT = true;
                Assert.Equal('e', screen.GetCell(x + 1, 1).Character);
                Assert.Equal('s', screen.GetCell(x + 2, 1).Character);
                Assert.Equal('t', screen.GetCell(x + 3, 1).Character);
                break;
            }
        }
        Assert.True(foundT, "Title 'Test' should appear in the rule");
    }

    #endregion

    #region Layout Component Tests

    [Fact]
    public void Layout_VerticalSplit_DividesHeight()
    {
        var layout = new Layout { Direction = LayoutDirection.Vertical };
        layout.Add(new Text().Append("Top"), LayoutSize.Fixed(2));
        layout.Add(new Text().Append("Bot"), LayoutSize.Fixed(2));

        var screen = RenderAndParse(layout, 10, 4);

        // "Top" at row 0
        Assert.Equal('T', screen.GetCell(0, 0).Character);
        Assert.Equal('o', screen.GetCell(1, 0).Character);
        Assert.Equal('p', screen.GetCell(2, 0).Character);

        // "Bot" at row 2
        Assert.Equal('B', screen.GetCell(0, 2).Character);
        Assert.Equal('o', screen.GetCell(1, 2).Character);
        Assert.Equal('t', screen.GetCell(2, 2).Character);
    }

    [Fact]
    public void Layout_HorizontalSplit_DividesWidth()
    {
        var layout = new Layout { Direction = LayoutDirection.Horizontal };
        layout.Add(new Text().Append("L"), LayoutSize.Fixed(5));
        layout.Add(new Text().Append("R"), LayoutSize.Fixed(5));

        var screen = RenderAndParse(layout, 10, 3);

        // "L" at column 0
        Assert.Equal('L', screen.GetCell(0, 0).Character);

        // "R" at column 5
        Assert.Equal('R', screen.GetCell(5, 0).Character);
    }

    #endregion

    #region Unicode Tests

    [Fact]
    public void Text_BoxDrawingCharacters_PreservedThroughParsing()
    {
        var text = new Text().Append("╭──╮");

        var screen = RenderAndParse(text, 10, 3);

        Assert.Equal('╭', screen.GetCell(0, 0).Character);
        Assert.Equal('─', screen.GetCell(1, 0).Character);
        Assert.Equal('─', screen.GetCell(2, 0).Character);
        Assert.Equal('╮', screen.GetCell(3, 0).Character);
    }

    [Fact]
    public void Text_Emoji_PreservedThroughParsing()
    {
        var text = new Text().Append("✓OK");

        var screen = RenderAndParse(text, 10, 3);

        Assert.Equal('✓', screen.GetCell(0, 0).Character);
        Assert.Equal('O', screen.GetCell(1, 0).Character);
        Assert.Equal('K', screen.GetCell(2, 0).Character);
    }

    #endregion

    #region Stress Tests with Verification

    [Fact]
    public void Table_ManyRows_AllRowsAccessibleViaScrolling()
    {
        var table = new Table { IsSelectable = true, Border = TableBorderStyle.None, ShowHeader = false };
        table.AddColumn("Index", width: 10);

        const int rowCount = 100;
        for (int i = 0; i < rowCount; i++)
        {
            table.AddRow($"Row{i:D3}");
        }

        // Render to a small viewport (only 5 rows visible)
        var componentBuffer = new ComponentScreenBuffer(20, 5);
        
        // Initial render to set _visibleRowCount
        table.IsFocused = true;
        table.SelectedIndex = 0;
        table.Render(componentBuffer, new Region(0, 0, 20, 5));
        
        // Navigate to end
        table.HandleKey(new ConsoleKeyInfo('\0', ConsoleKey.End, false, false, false));

        Assert.Equal(rowCount - 1, table.SelectedIndex);

        // Render and parse
        componentBuffer.Clear();
        table.Render(componentBuffer, new Region(0, 0, 20, 5));

        var terminal = new TestTerminal(20, 5);
        componentBuffer.Flush(terminal);

        var screen = new ParserScreenBuffer(20, 5);
        var parser = new VtParser(screen);
        parser.Parse(terminal.OutputBytes);

        // The last row "Row099" should be visible somewhere
        bool foundLastRow = terminal.Output.Contains("Row099");
        Assert.True(foundLastRow, $"Last row should be visible after scrolling to end. Output: {terminal.Output}");
    }

    [Fact]
    public void NestedPanels_ContentRendersInCorrectPosition()
    {
        var innerPanel = new Panel
        {
            Border = BoxBorderStyle.Simple,
            Content = new Text().Append("X")
        };

        var outerPanel = new Panel
        {
            Border = BoxBorderStyle.Simple,
            Content = innerPanel
        };

        var screen = RenderAndParse(outerPanel, 10, 6);

        // Outer border at (0,0), (9,0), (0,5), (9,5)
        Assert.Equal('┌', screen.GetCell(0, 0).Character);
        Assert.Equal('┐', screen.GetCell(9, 0).Character);

        // Inner border at (1,1), (8,1), (1,4), (8,4)
        Assert.Equal('┌', screen.GetCell(1, 1).Character);
        Assert.Equal('┐', screen.GetCell(8, 1).Character);

        // Content "X" at (2,2) - inside both borders
        Assert.Equal('X', screen.GetCell(2, 2).Character);
    }

    #endregion
}
