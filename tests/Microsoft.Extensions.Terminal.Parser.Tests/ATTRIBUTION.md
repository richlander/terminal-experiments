# Attribution

This terminal parser implementation was developed by studying and learning from several excellent open-source terminal emulator projects. We are grateful to these projects for their publicly available code and test suites, which encode decades of institutional knowledge about escape sequence handling.

## Primary Influences

### vte (Alacritty)
- **Repository**: https://github.com/alacritty/vte
- **License**: Apache-2.0 / MIT
- **Influence**: State machine design, `Perform` trait pattern adapted to `IParserHandler`
- **What we learned**: Clean match-based state transitions, SIMD optimization patterns for ground state

### xterm.js
- **Repository**: https://github.com/xtermjs/xterm.js  
- **License**: MIT
- **Influence**: Zero Default Mode for CSI parameters, handler registration pattern, test structure
- **What we learned**: Comprehensive edge case handling, async handler support patterns

### libvterm
- **Repository**: https://github.com/neovim/libvterm
- **License**: MIT
- **Influence**: Reference behavior for sequence parsing, UTF-8 boundary handling
- **What we learned**: Split sequence handling, OSC/DCS termination edge cases

### Windows Terminal
- **Repository**: https://github.com/microsoft/terminal
- **License**: MIT
- **Influence**: Microsoft's battle-tested parser edge cases
- **What we learned**: Real-world edge cases from production terminal

### VtNetCore
- **Repository**: https://github.com/darrenstarr/VtNetCore
- **License**: MIT
- **Influence**: Existing C# implementation for behavior comparison
- **What we learned**: C#-specific implementation patterns, handler registry approach

## Ported Tests

Many of our test cases are direct ports from the above projects, adapted to C# and xUnit. Each test file documents its source:

| Test File | Primary Source |
|-----------|----------------|
| CsiParsingTests.cs | xterm.js, libvterm, vte, Windows Terminal |
| OscParsingTests.cs | xterm.js, libvterm, Windows Terminal |
| EscParsingTests.cs | xterm.js, libvterm |
| ControlCodeTests.cs | xterm.js, libvterm, vte |
| Utf8Tests.cs | libvterm, vte |
| EdgeCaseTests.cs | xterm.js, vte, Windows Terminal |
| DcsParsingTests.cs | xterm.js, libvterm |
| StateMachineTests.cs | Windows Terminal, libvterm |
| RealWorldSequenceTests.cs | Observed from ls, vim, git, bash, etc. |
| ModeTests.cs | libvterm t/15state_mode.test, t/25state_input.test |
| EditingTests.cs | libvterm t/13state_edit.test |
| CursorMovementTests.cs | libvterm t/11state_movecursor.test |
| CharacterSetTests.cs | libvterm t/14state_encoding.test |
| TabStopTests.cs | libvterm t/21state_tabstops.test |
| QueryTests.cs | libvterm t/26state_query.test |
| StressTests.cs | Original stress testing |
| ScreenBufferTests.cs | Original implementation tests |

## Test Count by Source

| Source | Tests Ported |
|--------|--------------|
| xterm.js | ~50 |
| libvterm | ~90 |
| vte (Rust) | ~30 |
| Windows Terminal | ~25 |
| Real-world sequences | ~25 |
| VtNetCore H2H | ~10 |
| Stress tests | 26 |
| ScreenBuffer | 41 |
| Mode/Editing/Cursor | 86 |
| **Total** | **388** |

## References

- Paul Williams' VT500 State Machine: https://vt100.net/emu/dec_ansi_parser
- ECMA-48 Control Functions: https://www.ecma-international.org/publications-and-standards/standards/ecma-48/
- XTerm Control Sequences: https://invisible-island.net/xterm/ctlseqs/ctlseqs.html
- VT100 User Guide: https://vt100.net/docs/vt100-ug/

## License

This project is MIT licensed. The ported test cases retain their original project's license terms where applicable.
