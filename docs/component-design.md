# Component Layer Design

This document captures the design for Layers 3-4 (Layout and Components) of Microsoft.Extensions.Terminal, informed by real-world analysis of Spectre.Console usage and its limitations for interactive applications.

## Background: Lessons from Spectre.Console

### Case Study: Narrated Code Reviewer Dashboard

We analyzed a terminal dashboard application (`narrated-code-reviewer`) that uses Spectre.Console for rendering. The app provides:

- Session list with keyboard navigation
- Session detail view with tabs (Summary/Actions)
- Scrollable action lists with selection
- Real-time updates from file watchers

**Spectre.Console features used:**

| Component | Usage |
|-----------|-------|
| `Table` | Session lists, action lists, tool breakdowns, statistics |
| `Panel` | Header, footer, main content framing |
| `Layout` | Multi-region composition (Header/Main/Footer) |
| `Rule` | Horizontal dividers with styled titles |
| `Rows` | Vertical stacking of renderables |
| `Markup` | Styled text with `[bold]`, `[cyan]`, etc. |
| `BarChart` | Tool usage visualization |

### Where Spectre Falls Short

The dashboard implements its own:

1. **Event loop** - `while (_running)` with `Console.KeyAvailable`
2. **State machine** - `ViewState` enum, selected indices, scroll offsets
3. **Navigation** - Up/down/enter/escape handling
4. **Full redraws** - `Console.SetCursorPosition(0, 0)` + `AnsiConsole.Write(layout)`

At this point, Spectre is just a "pretty printer" for individual components. The abstraction overhead isn't paying for itself because:

- No differential rendering (full screen redraws cause flicker)
- No input model (you build your own key handler)
- No focus concept (you track selection indices manually)
- No dirty tracking (everything redraws every frame)

### Design Goals for Microsoft.Extensions.Terminal

Learn from Spectre's strengths while addressing its interactive weaknesses:

1. **Components are renderable** - Like Spectre, components can render themselves
2. **Differential updates** - Only redraw changed regions
3. **Optional input model** - Built-in keyboard handling for interactive components
4. **Direct ANSI control** - Components use `ITerminal` directly, no intermediate representation
5. **Allocation-conscious** - Minimize per-frame allocations with pooling/structs

## Layer 3: Layout System

### Core Concept: Screen Buffer with Regions

```csharp
/// <summary>
/// A screen buffer that tracks dirty regions for efficient partial updates.
/// </summary>
public sealed class ScreenBuffer
{
    private readonly char[] _current;
    private readonly char[] _previous;
    private readonly TerminalColor[] _colors;
    private readonly BitArray _dirty;

    public int Width { get; }
    public int Height { get; }

    /// <summary>
    /// Write text at a position, marking cells as dirty if changed.
    /// </summary>
    public void Write(int x, int y, ReadOnlySpan<char> text, TerminalColor color);

    /// <summary>
    /// Clear a rectangular region.
    /// </summary>
    public void Clear(int x, int y, int width, int height);

    /// <summary>
    /// Flush only dirty cells to the terminal.
    /// </summary>
    public void Flush(ITerminal terminal);

    /// <summary>
    /// Force full redraw on next flush (e.g., after terminal resize).
    /// </summary>
    public void Invalidate();
}
```

### Layout Regions

```csharp
/// <summary>
/// Defines a rectangular region of the screen.
/// </summary>
public readonly record struct Region(int X, int Y, int Width, int Height)
{
    public static Region FromTerminal(ITerminal terminal)
        => new(0, 0, terminal.Width, terminal.Height);

    /// <summary>
    /// Split this region horizontally into rows.
    /// </summary>
    public void SplitRows(ReadOnlySpan<LayoutSize> sizes, Span<Region> results);

    /// <summary>
    /// Split this region vertically into columns.
    /// </summary>
    public void SplitColumns(ReadOnlySpan<LayoutSize> sizes, Span<Region> results);
}

/// <summary>
/// Specifies how a layout region should be sized.
/// </summary>
public readonly record struct LayoutSize
{
    public LayoutSizeKind Kind { get; init; }
    public int Value { get; init; }

    public static LayoutSize Fixed(int size) => new() { Kind = LayoutSizeKind.Fixed, Value = size };
    public static LayoutSize Percent(int percent) => new() { Kind = LayoutSizeKind.Percent, Value = percent };
    public static LayoutSize Fill => new() { Kind = LayoutSizeKind.Fill };
}

public enum LayoutSizeKind { Fixed, Percent, Fill }
```

### Layout Container

```csharp
/// <summary>
/// A container that arranges child components in rows or columns.
/// </summary>
public sealed class Layout
{
    private readonly List<(IComponent Component, LayoutSize Size)> _children = new();

    public LayoutDirection Direction { get; set; } = LayoutDirection.Vertical;

    public Layout Add(IComponent component, LayoutSize size)
    {
        _children.Add((component, size));
        return this;
    }

    /// <summary>
    /// Render children into the given region.
    /// </summary>
    public void Render(ScreenBuffer buffer, Region region);
}

public enum LayoutDirection { Horizontal, Vertical }
```

### Usage Example

```csharp
var layout = new Layout { Direction = LayoutDirection.Vertical }
    .Add(header, LayoutSize.Fixed(3))
    .Add(main, LayoutSize.Fill)
    .Add(footer, LayoutSize.Fixed(3));

var buffer = new ScreenBuffer(terminal.Width, terminal.Height);
layout.Render(buffer, Region.FromTerminal(terminal));
buffer.Flush(terminal);  // Only writes changed cells
```

## Layer 4: Components

### Base Interface

```csharp
/// <summary>
/// A component that can render itself to a screen buffer region.
/// </summary>
public interface IComponent
{
    /// <summary>
    /// Render this component into the given region.
    /// </summary>
    void Render(ScreenBuffer buffer, Region region);

    /// <summary>
    /// Optional: preferred size for layout calculations.
    /// </summary>
    Size? PreferredSize => null;
}

/// <summary>
/// A component that can handle keyboard input.
/// </summary>
public interface IInteractiveComponent : IComponent
{
    /// <summary>
    /// Handle a key press. Return true if handled.
    /// </summary>
    bool HandleKey(ConsoleKeyInfo key);

    /// <summary>
    /// Whether this component currently has focus.
    /// </summary>
    bool IsFocused { get; set; }
}

public readonly record struct Size(int Width, int Height);
```

### Table Component

```csharp
/// <summary>
/// A table with columns and rows, supporting selection and scrolling.
/// </summary>
public sealed class Table : IInteractiveComponent
{
    private readonly List<TableColumn> _columns = new();
    private readonly List<TableRow> _rows = new();
    private int _selectedIndex = -1;
    private int _scrollOffset;

    public TableBorderStyle Border { get; set; } = TableBorderStyle.Rounded;
    public TerminalColor BorderColor { get; set; } = TerminalColor.Gray;
    public bool IsSelectable { get; set; }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set => _selectedIndex = Math.Clamp(value, -1, _rows.Count - 1);
    }

    public Table AddColumn(string header, int? width = null, Alignment alignment = Alignment.Left)
    {
        _columns.Add(new TableColumn(header, width, alignment));
        return this;
    }

    public Table AddRow(params TableCell[] cells)
    {
        _rows.Add(new TableRow(cells));
        return this;
    }

    public Table AddRow(params string[] values)
    {
        var cells = values.Select(v => new TableCell(v)).ToArray();
        _rows.Add(new TableRow(cells));
        return this;
    }

    public void Clear()
    {
        _rows.Clear();
        _selectedIndex = -1;
        _scrollOffset = 0;
    }

    // IInteractiveComponent
    public bool IsFocused { get; set; }

    public bool HandleKey(ConsoleKeyInfo key)
    {
        if (!IsSelectable) return false;

        switch (key.Key)
        {
            case ConsoleKey.UpArrow or ConsoleKey.K:
                if (_selectedIndex > 0)
                {
                    _selectedIndex--;
                    EnsureVisible(_selectedIndex);
                    return true;
                }
                break;

            case ConsoleKey.DownArrow or ConsoleKey.J:
                if (_selectedIndex < _rows.Count - 1)
                {
                    _selectedIndex++;
                    EnsureVisible(_selectedIndex);
                    return true;
                }
                break;

            case ConsoleKey.PageUp:
                _selectedIndex = Math.Max(0, _selectedIndex - VisibleRowCount);
                EnsureVisible(_selectedIndex);
                return true;

            case ConsoleKey.PageDown:
                _selectedIndex = Math.Min(_rows.Count - 1, _selectedIndex + VisibleRowCount);
                EnsureVisible(_selectedIndex);
                return true;
        }

        return false;
    }

    public void Render(ScreenBuffer buffer, Region region)
    {
        // Calculate visible rows based on region height
        var headerHeight = 2; // Header row + border
        var visibleRows = region.Height - headerHeight - 1; // -1 for bottom border

        // Render border
        RenderBorder(buffer, region);

        // Render header
        RenderHeader(buffer, region);

        // Render visible rows with scroll offset
        for (int i = 0; i < visibleRows && i + _scrollOffset < _rows.Count; i++)
        {
            var rowIndex = i + _scrollOffset;
            var isSelected = rowIndex == _selectedIndex && IsFocused;
            RenderRow(buffer, region, i, _rows[rowIndex], isSelected);
        }

        // Render scroll indicators if needed
        if (_scrollOffset > 0)
            RenderScrollIndicator(buffer, region, ScrollDirection.Up);
        if (_scrollOffset + visibleRows < _rows.Count)
            RenderScrollIndicator(buffer, region, ScrollDirection.Down);
    }

    private void EnsureVisible(int index)
    {
        if (index < _scrollOffset)
            _scrollOffset = index;
        else if (index >= _scrollOffset + VisibleRowCount)
            _scrollOffset = index - VisibleRowCount + 1;
    }
}

public readonly record struct TableColumn(string Header, int? Width, Alignment Alignment);
public readonly record struct TableRow(TableCell[] Cells);
public readonly record struct TableCell(string Text, TerminalColor? Color = null);

public enum TableBorderStyle { None, Simple, Rounded, Double }
public enum Alignment { Left, Center, Right }
```

### Panel Component

```csharp
/// <summary>
/// A bordered container with an optional header.
/// </summary>
public sealed class Panel : IComponent
{
    public IComponent? Content { get; set; }
    public string? Header { get; set; }
    public TerminalColor HeaderColor { get; set; } = TerminalColor.White;
    public BoxBorderStyle Border { get; set; } = BoxBorderStyle.Rounded;
    public TerminalColor BorderColor { get; set; } = TerminalColor.Gray;

    public void Render(ScreenBuffer buffer, Region region)
    {
        // Draw border
        RenderBorder(buffer, region);

        // Draw header if present
        if (!string.IsNullOrEmpty(Header))
        {
            buffer.Write(region.X + 2, region.Y, Header, HeaderColor);
        }

        // Render content in interior region
        if (Content != null)
        {
            var interior = new Region(
                region.X + 1,
                region.Y + 1,
                region.Width - 2,
                region.Height - 2);
            Content.Render(buffer, interior);
        }
    }
}

public enum BoxBorderStyle { None, Simple, Rounded, Double }
```

### Text Component

```csharp
/// <summary>
/// Styled text content.
/// </summary>
public sealed class Text : IComponent
{
    private readonly List<TextSpan> _spans = new();

    public Text Append(string text, TerminalColor color = TerminalColor.Default)
    {
        _spans.Add(new TextSpan(text, color));
        return this;
    }

    public Text AppendLine(string text = "", TerminalColor color = TerminalColor.Default)
    {
        _spans.Add(new TextSpan(text + "\n", color));
        return this;
    }

    public void Clear() => _spans.Clear();

    public void Render(ScreenBuffer buffer, Region region)
    {
        int x = region.X, y = region.Y;

        foreach (var span in _spans)
        {
            foreach (var ch in span.Text)
            {
                if (ch == '\n')
                {
                    x = region.X;
                    y++;
                    continue;
                }

                if (y >= region.Y + region.Height) return;
                if (x >= region.X + region.Width)
                {
                    x = region.X;
                    y++;
                    if (y >= region.Y + region.Height) return;
                }

                buffer.Write(x, y, ch.ToString(), span.Color);
                x++;
            }
        }
    }
}

public readonly record struct TextSpan(string Text, TerminalColor Color);
```

### Rule Component

```csharp
/// <summary>
/// A horizontal line, optionally with a title.
/// </summary>
public sealed class Rule : IComponent
{
    public string? Title { get; set; }
    public TerminalColor Color { get; set; } = TerminalColor.Gray;
    public char LineChar { get; set; } = '─';

    public void Render(ScreenBuffer buffer, Region region)
    {
        if (string.IsNullOrEmpty(Title))
        {
            var line = new string(LineChar, region.Width);
            buffer.Write(region.X, region.Y, line, Color);
        }
        else
        {
            // ─── Title ───
            var titleWithPadding = $" {Title} ";
            var remainingWidth = region.Width - titleWithPadding.Length;
            var leftWidth = remainingWidth / 2;
            var rightWidth = remainingWidth - leftWidth;

            buffer.Write(region.X, region.Y, new string(LineChar, leftWidth), Color);
            buffer.Write(region.X + leftWidth, region.Y, titleWithPadding, Color);
            buffer.Write(region.X + leftWidth + titleWithPadding.Length, region.Y,
                new string(LineChar, rightWidth), Color);
        }
    }
}
```

## Interactive Application Pattern

### Application Loop

```csharp
public sealed class TerminalApp
{
    private readonly ITerminal _terminal;
    private readonly ScreenBuffer _buffer;
    private readonly Layout _layout;
    private readonly List<IInteractiveComponent> _focusableComponents = new();
    private int _focusIndex;
    private bool _running = true;

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _terminal.HideCursor();

        try
        {
            var ticker = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
            bool needsRender = true;

            while (_running && !cancellationToken.IsCancellationRequested)
            {
                // Process input
                while (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true);
                    if (HandleKey(key))
                        needsRender = true;
                }

                // Render if needed
                if (needsRender)
                {
                    _layout.Render(_buffer, Region.FromTerminal(_terminal));
                    _buffer.Flush(_terminal);  // Differential update
                    needsRender = false;
                }

                await ticker.WaitForNextTickAsync(cancellationToken);
            }
        }
        finally
        {
            _terminal.ShowCursor();
        }
    }

    private bool HandleKey(ConsoleKeyInfo key)
    {
        // Global keys
        if (key.Key == ConsoleKey.Q)
        {
            _running = false;
            return true;
        }

        if (key.Key == ConsoleKey.Tab)
        {
            MoveFocus(key.Modifiers.HasFlag(ConsoleModifiers.Shift) ? -1 : 1);
            return true;
        }

        // Delegate to focused component
        if (_focusIndex >= 0 && _focusIndex < _focusableComponents.Count)
        {
            return _focusableComponents[_focusIndex].HandleKey(key);
        }

        return false;
    }

    private void MoveFocus(int direction)
    {
        if (_focusableComponents.Count == 0) return;

        _focusableComponents[_focusIndex].IsFocused = false;
        _focusIndex = (_focusIndex + direction + _focusableComponents.Count) % _focusableComponents.Count;
        _focusableComponents[_focusIndex].IsFocused = true;
    }
}
```

### Dashboard Example (Ported from Spectre)

```csharp
// Before (Spectre.Console):
var layout = new Layout("Root")
    .SplitRows(
        new Layout("Header").Size(3),
        new Layout("Main"),
        new Layout("Footer").Size(3));
layout["Header"].Update(new Panel(...));
AnsiConsole.Write(layout);  // Full redraw

// After (Microsoft.Extensions.Terminal):
var layout = new Layout { Direction = LayoutDirection.Vertical }
    .Add(_headerPanel, LayoutSize.Fixed(3))
    .Add(_sessionTable, LayoutSize.Fill)
    .Add(_footerPanel, LayoutSize.Fixed(3));

// In render loop:
layout.Render(_buffer, Region.FromTerminal(_terminal));
_buffer.Flush(_terminal);  // Only changed cells
```

## Border Characters

### Box Drawing

```csharp
public static class BoxChars
{
    public static class Rounded
    {
        public const char TopLeft = '╭';
        public const char TopRight = '╮';
        public const char BottomLeft = '╰';
        public const char BottomRight = '╯';
        public const char Horizontal = '─';
        public const char Vertical = '│';
    }

    public static class Simple
    {
        public const char TopLeft = '┌';
        public const char TopRight = '┐';
        public const char BottomLeft = '└';
        public const char BottomRight = '┘';
        public const char Horizontal = '─';
        public const char Vertical = '│';
    }

    public static class Double
    {
        public const char TopLeft = '╔';
        public const char TopRight = '╗';
        public const char BottomLeft = '╚';
        public const char BottomRight = '╝';
        public const char Horizontal = '═';
        public const char Vertical = '║';
    }
}
```

## Implementation Priority

Based on the dashboard case study, implement in this order:

### Phase 1: Core Layout

1. `ScreenBuffer` - differential rendering foundation
2. `Region` - screen subdivision
3. `Layout` - row/column container
4. `LayoutSize` - fixed/percent/fill sizing

### Phase 2: Essential Components

1. `Text` - styled text spans
2. `Panel` - bordered container
3. `Rule` - horizontal dividers
4. `Table` - with selection and scrolling

### Phase 3: Interactive Framework

1. `IInteractiveComponent` interface
2. Focus management
3. `TerminalApp` base class
4. Key event routing

### Phase 4: Additional Components

1. `BarChart` - horizontal bar visualization
2. `Tree` - hierarchical data display
3. `ProgressBar` - with spinner variants
4. `Menu` - vertical selection menu

## Testing Strategy

### Unit Tests

- `ScreenBuffer` dirty tracking
- `Region` splitting calculations
- Component rendering to fixed-size buffers

### Integration Tests

- Full layout rendering
- Interactive component navigation
- Resize handling

### Visual Tests

- Capture terminal output for comparison
- Border rendering correctness
- Color application

## Migration Path from Spectre.Console

For applications currently using Spectre.Console:

1. **Keep Spectre for non-interactive output** - One-shot displays, progress bars
2. **Use Microsoft.Extensions.Terminal for interactive dashboards** - Where differential rendering and input handling matter
3. **Gradual component replacement** - Replace Spectre components one at a time as this library's components mature

The two libraries can coexist, using Spectre for its rich formatting in simple scenarios and this library for interactive applications.

---

## Lessons Learned from Porting

After actually porting the Narrated Code Reviewer dashboard from Spectre.Console to this library, several gaps and opportunities for improvement emerged.

### What Worked Well

| Aspect | Result |
|--------|--------|
| Differential rendering | `ScreenBuffer` only flushes changed cells - no flicker |
| Component reuse | Tables/panels as fields, content updated each frame |
| NativeAOT compatibility | No reflection concerns, clean trimming |
| Layout composition | `Layout` with `LayoutSize.Fixed`/`Fill` works well |

### What Didn't Work

The dashboard **did not use `TerminalApp`** because it has complex view state (multiple screens, tabs, drill-down navigation) that doesn't fit the simple focus model. Instead, it implemented its own run loop, duplicating much of what `TerminalApp` provides.

This reveals that `TerminalApp` is too simple for real applications with navigation.

### Missing ANSI Constants

The dashboard had to write raw escape codes:

```csharp
_terminal.Append($"{AnsiCodes.CSI}2J");  // Clear screen
_terminal.Append($"{AnsiCodes.CSI}H");   // Move cursor home
```

**Add to `AnsiCodes`:**

```csharp
/// <summary>
/// Clears the entire screen.
/// </summary>
public const string ClearScreen = $"{CSI}2J";

/// <summary>
/// Moves cursor to home position (1,1).
/// </summary>
public const string MoveCursorHome = $"{CSI}H";

/// <summary>
/// Clears screen and moves cursor to home - common combination.
/// </summary>
public const string ClearScreenAndHome = $"{CSI}2J{CSI}H";
```

### Higher-Level Components Needed

#### View Stack / Navigator

The dashboard has this pattern:

```csharp
enum ViewState { SessionList, SessionDetail, ChangeDetail }

void NavigateInto() {
    _currentView = ViewState.SessionDetail;
}

void NavigateBack() {
    _currentView = ViewState.SessionList;
}
```

**Proposed component:**

```csharp
/// <summary>
/// Manages a stack of views with push/pop navigation.
/// </summary>
public class ViewStack : IInteractiveComponent
{
    private readonly Stack<IComponent> _views = new();

    /// <summary>
    /// Gets the currently visible view.
    /// </summary>
    public IComponent? Current => _views.TryPeek(out var view) ? view : null;

    /// <summary>
    /// Push a new view onto the stack.
    /// </summary>
    public void Push(IComponent view)
    {
        _views.Push(view);
    }

    /// <summary>
    /// Pop the current view and return to the previous one.
    /// </summary>
    public bool Pop()
    {
        if (_views.Count > 1)
        {
            _views.Pop();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Replace all views with a single root view.
    /// </summary>
    public void Reset(IComponent root)
    {
        _views.Clear();
        _views.Push(root);
    }

    public bool IsFocused { get; set; }

    public bool HandleKey(ConsoleKeyInfo key)
    {
        // Escape/Backspace pops the stack
        if (key.Key is ConsoleKey.Escape or ConsoleKey.Backspace)
        {
            return Pop();
        }

        // Delegate to current view if interactive
        if (Current is IInteractiveComponent interactive)
        {
            return interactive.HandleKey(key);
        }

        return false;
    }

    public void Render(ScreenBuffer buffer, Region region)
    {
        Current?.Render(buffer, region);
    }
}
```

#### Tab View

The dashboard manually tracks tabs:

```csharp
enum SessionTab { Summary, Actions }
SessionTab _currentTab;

// In HandleInput:
case ConsoleKey.LeftArrow:
case ConsoleKey.RightArrow:
    _currentTab = /* cycle */;
    break;

// In Render:
_mainPanel.Header = _currentTab == SessionTab.Summary
    ? "Session  [Summary]  Actions"
    : "Session  Summary  [Actions]";
```

**Proposed component:**

```csharp
/// <summary>
/// A tabbed container with keyboard navigation.
/// </summary>
public class TabView : IInteractiveComponent
{
    private readonly List<(string Title, IComponent Content)> _tabs = new();
    private int _selectedIndex;

    public int SelectedIndex
    {
        get => _selectedIndex;
        set => _selectedIndex = Math.Clamp(value, 0, Math.Max(0, _tabs.Count - 1));
    }

    public TerminalColor ActiveTabColor { get; set; } = TerminalColor.Cyan;
    public TerminalColor InactiveTabColor { get; set; } = TerminalColor.Gray;

    public TabView Add(string title, IComponent content)
    {
        _tabs.Add((title, content));
        return this;
    }

    public bool IsFocused { get; set; }

    public bool HandleKey(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.LeftArrow:
                if (_selectedIndex > 0)
                {
                    _selectedIndex--;
                    return true;
                }
                break;

            case ConsoleKey.RightArrow:
                if (_selectedIndex < _tabs.Count - 1)
                {
                    _selectedIndex++;
                    return true;
                }
                break;
        }

        // Delegate to active tab content if interactive
        if (_tabs.Count > 0 && _tabs[_selectedIndex].Content is IInteractiveComponent interactive)
        {
            return interactive.HandleKey(key);
        }

        return false;
    }

    public void Render(ScreenBuffer buffer, Region region)
    {
        // Render tab bar (first row)
        int x = region.X;
        for (int i = 0; i < _tabs.Count; i++)
        {
            var color = i == _selectedIndex ? ActiveTabColor : InactiveTabColor;
            var title = i == _selectedIndex ? $"[{_tabs[i].Title}]" : $" {_tabs[i].Title} ";
            buffer.Write(x, region.Y, title.AsSpan(), color);
            x += title.Length + 1;
        }

        // Render active tab content
        if (_tabs.Count > 0)
        {
            var contentRegion = new Region(region.X, region.Y + 1, region.Width, region.Height - 1);
            _tabs[_selectedIndex].Content.Render(buffer, contentRegion);
        }
    }
}
```

### Key Deduplication

The dashboard implements key deduplication to prevent key-repeat from causing multiple navigation actions per frame:

```csharp
private void ProcessKeys(List<ConsoleKeyInfo> keys)
{
    var processedActions = new HashSet<ConsoleKey>();

    foreach (var key in keys)
    {
        var isNavigationKey = key.Key is ConsoleKey.UpArrow or ConsoleKey.DownArrow;

        if (isNavigationKey)
        {
            if (processedActions.Contains(key.Key))
                continue;
            processedActions.Add(key.Key);
        }

        HandleInput(key);
    }
}
```

**This should be built into `TerminalApp`:**

```csharp
public class TerminalApp
{
    /// <summary>
    /// Keys that should be deduplicated per frame to prevent key-repeat issues.
    /// </summary>
    public HashSet<ConsoleKey> DeduplicatedKeys { get; } = new()
    {
        ConsoleKey.UpArrow, ConsoleKey.DownArrow,
        ConsoleKey.LeftArrow, ConsoleKey.RightArrow,
        ConsoleKey.PageUp, ConsoleKey.PageDown,
        ConsoleKey.Home, ConsoleKey.End,
        ConsoleKey.J, ConsoleKey.K  // vim-style navigation
    };

    // In RunAsync, batch keys and deduplicate before processing
}
```

### Data Binding for Tables

Currently, updating a table requires:

```csharp
_sessionTable.Clear();
foreach (var session in _sessions)
{
    _sessionTable.AddRow(
        new TableCell(session.ProjectName),
        new TableCell(session.IsActive ? "Active" : "Idle",
            session.IsActive ? TerminalColor.Green : TerminalColor.Gray),
        // ...
    );
}
```

**Consider a binding API:**

```csharp
public class Table
{
    /// <summary>
    /// Bind the table to a data source with a row mapper.
    /// </summary>
    public void Bind<T>(IReadOnlyList<T> items, Func<T, int, TableRow> rowMapper)
    {
        Clear();
        for (int i = 0; i < items.Count; i++)
        {
            _rows.Add(rowMapper(items[i], i));
        }
    }
}

// Usage:
_sessionTable.Bind(_sessions, (session, i) => new TableRow(
    new TableCell(session.ProjectName),
    new TableCell(session.IsActive ? "Active" : "Idle",
        session.IsActive ? TerminalColor.Green : TerminalColor.Gray)
));
```

### ScreenBuffer Resize Handling

The dashboard has a bug in resize handling:

```csharp
if (_buffer.Width != _terminal.Width || _buffer.Height != _terminal.Height)
{
    var newBuffer = new ScreenBuffer(_terminal.Width, _terminal.Height);
    newBuffer.Invalidate();
    _dataChanged = true;
    // BUG: newBuffer is never assigned to _buffer!
}
```

**Options:**

1. Make `ScreenBuffer` mutable with a `Resize(width, height)` method
2. Have `TerminalApp` manage the buffer internally and recreate it on resize
3. Document clearly that apps must reassign the buffer

**Recommended: Option 2** - `TerminalApp` should own the buffer and handle resize automatically.

### Revised Implementation Priority

Based on the porting experience, update the priority:

#### Phase 1: Core Layout ✅ (Complete)

- `ScreenBuffer`, `Region`, `Layout`, `LayoutSize`

#### Phase 2: Essential Components ✅ (Complete)

- `Text`, `Panel`, `Rule`, `Table`

#### Phase 3: Interactive Framework ⚠️ (Needs Work)

Current:
- `IInteractiveComponent` interface ✅
- Focus management ✅
- `TerminalApp` base class ✅
- Key event routing ✅

**Add:**
- Key deduplication in `TerminalApp`
- Automatic resize handling in `TerminalApp`
- Missing `AnsiCodes` constants

#### Phase 4: Navigation Components 🆕 (New Priority)

Before additional components, add navigation:
1. `ViewStack` - push/pop view navigation
2. `TabView` - tabbed content switching

#### Phase 5: Additional Components

- `BarChart`, `Tree`, `ProgressBar`, `Menu`

#### Phase 6: Data Binding

- `Table.Bind<T>()` for declarative data updates
- Observable collection support (future)
