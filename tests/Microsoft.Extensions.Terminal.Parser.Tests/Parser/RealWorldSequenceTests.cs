// Real-world escape sequence tests
// Tests actual sequences used by common terminal applications

using Xunit;

namespace Microsoft.Extensions.Terminal.Parser.Tests.Parser;

/// <summary>
/// Tests for real-world escape sequences from popular terminal applications.
/// </summary>
/// <remarks>
/// These tests ensure the parser correctly handles actual sequences 
/// found in the wild from tools like ls, vim, git, shell prompts, etc.
/// </remarks>
public class RealWorldSequenceTests : ParserTestBase
{
    #region ls colored output

    [Fact]
    public void Ls_Directory_BlueColor()
    {
        // ls shows directories in blue: ESC[1;34m
        Parse("\u001b[1;34mdirectory\u001b[0m");
        
        var csis = Events.OfType<CsiEvent>().ToList();
        Assert.Equal(2, csis.Count);
        
        // Bold blue
        Assert.Equal('m', csis[0].Command);
        Assert.Equal(new[] { 1, 34 }, csis[0].Params);
        
        // Reset
        Assert.Equal('m', csis[1].Command);
        Assert.Equal(new[] { 0 }, csis[1].Params);
        
        Assert.Equal("directory", Handler.GetPrintedText());
    }

    [Fact]
    public void Ls_Executable_GreenColor()
    {
        // ls shows executables in green: ESC[1;32m
        Parse("\u001b[1;32mscript.sh\u001b[0m");
        
        var csi = Events.OfType<CsiEvent>().First();
        Assert.Equal(new[] { 1, 32 }, csi.Params);
    }

    [Fact]
    public void Ls_Symlink_CyanColor()
    {
        // ls shows symlinks in cyan: ESC[1;36m
        Parse("\u001b[1;36mlink\u001b[0m -> target");
        
        var csi = Events.OfType<CsiEvent>().First();
        Assert.Equal(new[] { 1, 36 }, csi.Params);
    }

    #endregion

    #region vim/neovim sequences

    [Fact]
    public void Vim_CursorShape_BlockToBeam()
    {
        // vim changes cursor: DECSCUSR (CSI Ps SP q)
        // Block cursor: CSI 1 SP q
        // Beam cursor: CSI 5 SP q
        Parse("\u001b[1 q");
        
        var csi = Events.OfType<CsiEvent>().First();
        Assert.Equal('q', csi.Command);
        Assert.Equal((byte)' ', csi.Intermediates);
        Assert.Equal(new[] { 1 }, csi.Params);
    }

    [Fact]
    public void Vim_AlternateScreen_EnableDisable()
    {
        // vim uses alternate screen buffer
        Parse("\u001b[?1049h");  // Enable
        Parse("\u001b[?1049l");  // Disable
        
        var csis = Events.OfType<CsiEvent>().ToList();
        Assert.Equal(2, csis.Count);
        Assert.Equal('h', csis[0].Command);
        Assert.Equal('l', csis[1].Command);
    }

    [Fact]
    public void Vim_ClearScreen()
    {
        // CSI 2 J - clear entire screen
        Parse("\u001b[2J");
        
        var csi = Events.OfType<CsiEvent>().First();
        Assert.Equal('J', csi.Command);
        Assert.Equal(new[] { 2 }, csi.Params);
    }

    [Fact]
    public void Vim_CursorPosition()
    {
        // CSI row ; col H - move cursor
        Parse("\u001b[10;25H");
        
        var csi = Events.OfType<CsiEvent>().First();
        Assert.Equal('H', csi.Command);
        Assert.Equal(new[] { 10, 25 }, csi.Params);
    }

    #endregion

    #region Shell prompts

    [Fact]
    public void Bash_ColoredPrompt()
    {
        // Typical bash prompt: user@host in green, path in blue
        Parse("\u001b[32muser@host\u001b[0m:\u001b[34m~/git\u001b[0m$ ");
        
        var csis = Events.OfType<CsiEvent>().ToList();
        Assert.Equal(4, csis.Count);
        
        Assert.Equal("user@host:~/git$ ", Handler.GetPrintedText());
    }

    [Fact]
    public void Zsh_OscTitle()
    {
        // zsh sets terminal title via OSC 0
        Parse("\u001b]0;user@host:~/git\x07");
        
        var osc = Assert.Single(Events.OfType<OscEvent>());
        Assert.Equal(0, osc.Command);
        Assert.Equal("user@host:~/git", osc.DataString);
    }

    [Fact]
    public void Fish_256Color()
    {
        // fish shell uses 256 colors: CSI 38;5;208 m (orange)
        Parse("\u001b[38;5;208mfish\u001b[0m");
        
        var csi = Events.OfType<CsiEvent>().First();
        Assert.Equal('m', csi.Command);
        Assert.Equal(new[] { 38, 5, 208 }, csi.Params);
    }

    #endregion

    #region Git output

    [Fact]
    public void Git_Status_ModifiedRed()
    {
        // git status shows modified files in red
        Parse("\u001b[31m\tmodified:   file.txt\u001b[0m");
        
        var csi = Events.OfType<CsiEvent>().First();
        Assert.Equal(new[] { 31 }, csi.Params);
    }

    [Fact]
    public void Git_Diff_AddedGreen()
    {
        // git diff shows added lines in green
        Parse("\u001b[32m+added line\u001b[0m");
        
        var csi = Events.OfType<CsiEvent>().First();
        Assert.Equal(new[] { 32 }, csi.Params);
    }

    [Fact]
    public void Git_Diff_RemovedRed()
    {
        // git diff shows removed lines in red
        Parse("\u001b[31m-removed line\u001b[0m");
        
        var csi = Events.OfType<CsiEvent>().First();
        Assert.Equal(new[] { 31 }, csi.Params);
    }

    #endregion

    #region True Color (24-bit)

    [Fact]
    public void TrueColor_Foreground()
    {
        // CSI 38;2;R;G;B m - set RGB foreground
        Parse("\u001b[38;2;255;128;64mtext\u001b[0m");
        
        var csi = Events.OfType<CsiEvent>().First();
        Assert.Equal('m', csi.Command);
        Assert.Equal(new[] { 38, 2, 255, 128, 64 }, csi.Params);
    }

    [Fact]
    public void TrueColor_Background()
    {
        // CSI 48;2;R;G;B m - set RGB background
        Parse("\u001b[48;2;0;64;128mtext\u001b[0m");
        
        var csi = Events.OfType<CsiEvent>().First();
        Assert.Equal('m', csi.Command);
        Assert.Equal(new[] { 48, 2, 0, 64, 128 }, csi.Params);
    }

    [Fact]
    public void TrueColor_ForegroundAndBackground()
    {
        // Both foreground and background in one sequence
        Parse("\u001b[38;2;255;255;255;48;2;0;0;0mwhite on black\u001b[0m");
        
        var csi = Events.OfType<CsiEvent>().First();
        Assert.Equal('m', csi.Command);
        Assert.Equal(new[] { 38, 2, 255, 255, 255, 48, 2, 0, 0, 0 }, csi.Params);
    }

    #endregion

    #region Hyperlinks

    [Fact]
    public void Hyperlink_GitHub()
    {
        // OSC 8 hyperlinks as used by modern terminals
        Parse("\u001b]8;;https://github.com\x07Link\u001b]8;;\x07");
        
        var oscs = Events.OfType<OscEvent>().ToList();
        Assert.Equal(2, oscs.Count);
        Assert.Equal(8, oscs[0].Command);
        Assert.Equal(";https://github.com", oscs[0].DataString);
        Assert.Equal(8, oscs[1].Command);
        Assert.Equal(";", oscs[1].DataString);  // End hyperlink
        
        Assert.Equal("Link", Handler.GetPrintedText());
    }

    #endregion

    #region Progress indicators

    [Fact]
    public void Progress_CarriageReturn()
    {
        // Many CLIs use CR to overwrite progress
        Parse("Loading...  50%\rLoading... 100%");
        
        var execs = Events.OfType<ExecuteEvent>().ToList();
        Assert.Contains(execs, e => e.Code == 0x0D);  // CR
    }

    [Fact]
    public void Progress_CursorSaveRestore()
    {
        // Some tools save/restore cursor for progress
        Parse("\u001b7Progress...\u001b8Done!");
        
        var escs = Events.OfType<EscEvent>().ToList();
        Assert.Equal(2, escs.Count);
        Assert.Equal('7', escs[0].Command);  // DECSC
        Assert.Equal('8', escs[1].Command);  // DECRC
    }

    #endregion

    #region Mouse reporting

    [Fact]
    public void Mouse_EnableSGRMode()
    {
        // Enable SGR mouse mode: CSI ?1006h
        Parse("\u001b[?1006h");
        
        var csi = Events.OfType<CsiEvent>().First();
        Assert.Equal('h', csi.Command);
        Assert.Equal(new[] { 1006 }, csi.Params);
    }

    [Fact]
    public void Mouse_EnableButtonEvents()
    {
        // Enable mouse button events: CSI ?1000h
        Parse("\u001b[?1000h");
        
        var csi = Events.OfType<CsiEvent>().First();
        Assert.Equal('h', csi.Command);
        Assert.Equal(new[] { 1000 }, csi.Params);
    }

    #endregion

    #region Bracketed paste

    [Fact]
    public void BracketedPaste_Enable()
    {
        // Enable bracketed paste: CSI ?2004h
        Parse("\u001b[?2004h");
        
        var csi = Events.OfType<CsiEvent>().First();
        Assert.Equal('h', csi.Command);
        Assert.Equal(new[] { 2004 }, csi.Params);
    }

    #endregion

    #region Window operations

    [Fact]
    public void Window_Maximize()
    {
        // CSI 9;1t - maximize window
        Parse("\u001b[9;1t");
        
        var csi = Events.OfType<CsiEvent>().First();
        Assert.Equal('t', csi.Command);
        Assert.Equal(new[] { 9, 1 }, csi.Params);
    }

    [Fact]
    public void Window_SetSize()
    {
        // CSI 8;rows;cols t - resize window
        Parse("\u001b[8;24;80t");
        
        var csi = Events.OfType<CsiEvent>().First();
        Assert.Equal('t', csi.Command);
        Assert.Equal(new[] { 8, 24, 80 }, csi.Params);
    }

    #endregion
}
