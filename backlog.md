# Microsoft.Extensions.Terminal - Backlog

Issues identified during code review. Prioritized by severity and impact.

## Critical

### Thread Safety Issues

- [ ] **UnixPty.WaitForExitAsync race condition** (`UnixPty.cs:135-165`)
  - Multiple polling tasks can run concurrently if called multiple times
  - `_exitTcs` accessed outside lock in Task.Run body
  - Fire-and-forget task never tracked

- [ ] **ManagedSession potential deadlock** (`ManagedSession.cs:319-321`)
  - Parser operations under `_screenLock` while subscribers notified under `_lock`
  - If subscriber calls back into session methods, deadlock possible

### Resource Leaks

- [ ] **SessionHost client task accumulation** (`SessionHost.cs:169-174`)
  - Tasks added to `_clientTasks` but never removed after completion
  - Long-running hosts accumulate completed task objects

## High Priority

### API Design

- [ ] **ITerminal interface incomplete**
  - `MoveCursorUp`, `SetCursorHorizontal`, `EraseToEndOfScreen`, `EraseToEndOfLine` on `AnsiTerminal` but not `ITerminal`
  - Consumers must downcast to access common terminal operations
  - Consider: extend `ITerminal` or create `IAnsiTerminal` extension interface

- [ ] **Confusing ScreenBuffer naming collision**
  - `Microsoft.Extensions.Terminal.Components.ScreenBuffer` - TUI rendering
  - `Microsoft.Extensions.Terminal.Parser.ScreenBuffer` - VT parsing
  - Rename one to `RenderBuffer` or `ParserScreenBuffer`

### Missing Dispose Patterns

- [ ] **TerminalApp missing IDisposable** (`TerminalApp.cs`)
  - Owns `ScreenBuffer`, has no cleanup for registered components
  - Add `IDisposable` implementation

- [ ] **SessionHost timer disposal race** (`SessionHost.cs:44`)
  - `CheckIdleSessions` accesses `_sessions` after disposal could be in progress
  - Add proper synchronization or null checks

### Test Coverage Gaps

- [ ] **Empty PtyTests.cs**
  - No actual tests for critical PTY functionality
  - Add basic PTY spawn/read/write/resize tests

- [ ] **No component framework tests**
  - `TerminalApp` - keyboard handling, focus management, render loop
  - `Table` - selection, scrolling, data binding
  - `Layout` - splitting, sizing
  - `Panel`, `Text`, `Rule`, `TabView` - all untested

## Medium Priority

### Code Quality

- [ ] **Swallowed exceptions in AnsiTerminal** (`AnsiTerminal.cs:211-217`)
  ```csharp
  catch
  {
      // Ignore all GetDirectoryName errors.
  }
  ```
  - Log or handle specific exceptions instead

- [ ] **Pointless catch-rethrow in ManagedSession** (`ManagedSession.cs:73-77`)
  ```csharp
  catch
  {
      _state = SessionState.Failed;
      throw;
  }
  ```
  - State assignment lost when re-thrown; remove or fix

- [ ] **Unused SearchValues in VtParser** (`VtParser.cs:48-49`)
  - `ExecuteControls` defined but never used
  - Remove or utilize in `IsC0Control`

- [ ] **Unused FormatBytes method** (`dotnet-capture/Program.cs:400-404`)
  - Remove dead code

### Performance

- [ ] **String interpolation in hot path** (`Components/ScreenBuffer.cs:237`)
  ```csharp
  terminal.Append($"{AnsiCodes.CSI}{y + 1};{startX + 1}H");
  ```
  - Use `StringBuilder` or pooled buffers for cell flush

- [ ] **LINQ allocation in Table.AddRow** (`Table.cs:94`)
  ```csharp
  var cells = values.Select(v => new TableCell(v)).ToArray();
  ```
  - Avoid LINQ + `ToArray()` on every row

- [ ] **Redundant index calculation in ScreenBuffer flush** (`Components/ScreenBuffer.cs:254-271`)
  - Combine loops or use span slicing

### Hardcoded Values

- [ ] **KnownFileExtensions not configurable** (`AnsiTerminal.cs:20-38`)
  - Allow consumers to customize allowed link extensions

- [ ] **TerminalApp frame rate hardcoded** (`TerminalApp.cs:128`)
  - Make 50ms (20 FPS) tick rate configurable

## Low Priority

### Documentation

- [ ] **Missing XML docs on public APIs**
  - `IComponent.Render()` parameters
  - `LayoutSize` struct
  - `Region` struct
  - `TerminalColor` enum values

### Consistency

- [ ] **Inconsistent nullable handling**
  - Some use `ThrowIfNull`, others use null checks
  - Standardize approach

- [ ] **Inconsistent error messages**
  - `"Session '{id}' not found"` vs `"Session with ID '{id}' already exists"`
  - Standardize format

### Technical Debt

- [ ] **TODOs in production code** (`Parser/ScreenBuffer.cs:745-746`)
  ```csharp
  case 1049: // Alternate screen buffer
      // Would need separate buffer implementation
  ```
  - Implement or track separately

- [ ] **Unsafe code in UnixPty** (`UnixPty.cs:186-204`)
  - Could use `MemoryMarshal.GetReference` or similar safe patterns
  - Low priority since it works correctly

- [ ] **Memory leak in child process** (`UnixPty.cs:80-96`)
  - `Marshal.StringToHGlobalAnsi` never freed before `execvp`
  - Technically leaks if `execvp` fails, but process exits anyway
