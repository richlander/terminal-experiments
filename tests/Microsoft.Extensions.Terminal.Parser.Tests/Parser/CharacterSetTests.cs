// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

// Tests ported from libvterm t/14state_encoding.test
// Focus: Character set designation (G0-G3) and locking shift sequences

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// Tests for character set designation sequences.
/// These are ESC sequences for designating character sets to G0-G3 and shifting.
/// </summary>
public class CharacterSetTests : ParserTestBase
{
    #region G0 Designation (ESC ( final)

    [Fact]
    public void DesignateG0_USASCII()
    {
        // ESC ( B - Designate US-ASCII to G0
        // Source: libvterm t/14state_encoding.test line 100
        Parse("\u001b(B");

        var evt = Assert.Single(Handler.Events.OfType<EscEvent>());
        Assert.Equal('B', evt.Final);
        Assert.Equal('(', (char)evt.Intermediates);
    }

    [Fact]
    public void DesignateG0_UKCharset()
    {
        // ESC ( A - Designate UK character set to G0
        // Source: libvterm t/14state_encoding.test line 11
        Parse("\u001b(A");

        var evt = Assert.Single(Handler.Events.OfType<EscEvent>());
        Assert.Equal('A', evt.Final);
        Assert.Equal('(', (char)evt.Intermediates);
    }

    [Fact]
    public void DesignateG0_DECLineDrawing()
    {
        // ESC ( 0 - Designate DEC Special Graphics (line drawing) to G0
        // Source: libvterm t/14state_encoding.test line 16
        Parse("\u001b(0");

        var evt = Assert.Single(Handler.Events.OfType<EscEvent>());
        Assert.Equal('0', evt.Final);
        Assert.Equal('(', (char)evt.Intermediates);
    }

    #endregion

    #region G1 Designation (ESC ) final)

    [Fact]
    public void DesignateG1_DECLineDrawing()
    {
        // ESC ) 0 - Designate DEC Special Graphics to G1
        // Source: libvterm t/14state_encoding.test line 22
        Parse("\u001b)0");

        var evt = Assert.Single(Handler.Events.OfType<EscEvent>());
        Assert.Equal('0', evt.Final);
        Assert.Equal(')', (char)evt.Intermediates);
    }

    [Fact]
    public void DesignateG1_USASCII()
    {
        // ESC ) B - Designate US-ASCII to G1
        Parse("\u001b)B");

        var evt = Assert.Single(Handler.Events.OfType<EscEvent>());
        Assert.Equal('B', evt.Final);
        Assert.Equal(')', (char)evt.Intermediates);
    }

    #endregion

    #region G2 Designation (ESC * final)

    [Fact]
    public void DesignateG2_DECLineDrawing()
    {
        // ESC * 0 - Designate DEC Special Graphics to G2
        // Source: libvterm t/14state_encoding.test line 35
        Parse("\u001b*0");

        var evt = Assert.Single(Handler.Events.OfType<EscEvent>());
        Assert.Equal('0', evt.Final);
        Assert.Equal('*', (char)evt.Intermediates);
    }

    #endregion

    #region G3 Designation (ESC + final)

    [Fact]
    public void DesignateG3_DECLineDrawing()
    {
        // ESC + 0 - Designate DEC Special Graphics to G3
        // Source: libvterm t/14state_encoding.test line 46
        Parse("\u001b+0");

        var evt = Assert.Single(Handler.Events.OfType<EscEvent>());
        Assert.Equal('0', evt.Final);
        Assert.Equal('+', (char)evt.Intermediates);
    }

    #endregion

    #region Locking Shift Sequences

    [Fact]
    public void LockingShift_LS0()
    {
        // SI (0x0F) - Locking Shift 0 (select G0 into GL)
        // Source: libvterm t/14state_encoding.test line 30
        Parse("\u000F");

        var evt = Assert.Single(Handler.Events.OfType<ExecuteEvent>());
        Assert.Equal(0x0F, evt.Code);
    }

    [Fact]
    public void LockingShift_LS1()
    {
        // SO (0x0E) - Locking Shift 1 (select G1 into GL)
        // Source: libvterm t/14state_encoding.test line 26
        Parse("\u000E");

        var evt = Assert.Single(Handler.Events.OfType<ExecuteEvent>());
        Assert.Equal(0x0E, evt.Code);
    }

    [Fact]
    public void LockingShift_LS2()
    {
        // ESC n - Locking Shift 2 (select G2 into GL)
        // Source: libvterm t/14state_encoding.test line 38
        Parse("\u001bn");

        var evt = Assert.Single(Handler.Events.OfType<EscEvent>());
        Assert.Equal('n', evt.Final);
    }

    [Fact]
    public void LockingShift_LS3()
    {
        // ESC o - Locking Shift 3 (select G3 into GL)
        // Source: libvterm t/14state_encoding.test line 49
        Parse("\u001bo");

        var evt = Assert.Single(Handler.Events.OfType<EscEvent>());
        Assert.Equal('o', evt.Final);
    }

    [Fact]
    public void LockingShift_LS1R()
    {
        // ESC ~ - Locking Shift 1 Right (select G1 into GR)
        // Source: libvterm t/14state_encoding.test line 69
        Parse("\u001b~");

        var evt = Assert.Single(Handler.Events.OfType<EscEvent>());
        Assert.Equal('~', evt.Final);
    }

    [Fact]
    public void LockingShift_LS2R()
    {
        // ESC } - Locking Shift 2 Right (select G2 into GR)
        // Source: libvterm t/14state_encoding.test line 77
        Parse("\u001b}");

        var evt = Assert.Single(Handler.Events.OfType<EscEvent>());
        Assert.Equal('}', evt.Final);
    }

    [Fact]
    public void LockingShift_LS3R()
    {
        // ESC | - Locking Shift 3 Right (select G3 into GR)
        // Source: libvterm t/14state_encoding.test line 85
        Parse("\u001b|");

        var evt = Assert.Single(Handler.Events.OfType<EscEvent>());
        Assert.Equal('|', evt.Final);
    }

    #endregion

    #region Single Shift (SS2/SS3)

    [Fact]
    public void SingleShift_SS2_ESC()
    {
        // ESC N - Single Shift 2 (temporarily select G2)
        Parse("\u001bN");

        var evt = Assert.Single(Handler.Events.OfType<EscEvent>());
        Assert.Equal('N', evt.Final);
    }

    [Fact]
    public void SingleShift_SS3_ESC()
    {
        // ESC O - Single Shift 3 (temporarily select G3)
        Parse("\u001bO");

        var evt = Assert.Single(Handler.Events.OfType<EscEvent>());
        Assert.Equal('O', evt.Final);
    }

    [Fact]
    public void SingleShift_SS2_C1()
    {
        // 0x8E (C1 SS2) - Single Shift 2
        // Source: libvterm t/14state_encoding.test line 57
        Parse(new byte[] { 0x8E });

        var evt = Assert.Single(Handler.Events.OfType<ExecuteEvent>());
        Assert.Equal(0x8E, evt.Code);
    }

    [Fact]
    public void SingleShift_SS3_C1()
    {
        // 0x8F (C1 SS3) - Single Shift 3
        // Source: libvterm t/14state_encoding.test line 62
        Parse(new byte[] { 0x8F });

        var evt = Assert.Single(Handler.Events.OfType<ExecuteEvent>());
        Assert.Equal(0x8F, evt.Code);
    }

    #endregion

    #region 96-Character Set Designations (ESC - . / final)

    [Fact]
    public void DesignateG1_96Char()
    {
        // ESC - A - Designate ISO Latin-1 Supplemental to G1 (96-char set)
        Parse("\u001b-A");

        var evt = Assert.Single(Handler.Events.OfType<EscEvent>());
        Assert.Equal('A', evt.Final);
        Assert.Equal('-', (char)evt.Intermediates);
    }

    [Fact]
    public void DesignateG2_96Char()
    {
        // ESC . A - Designate to G2 (96-char set)
        Parse("\u001b.A");

        var evt = Assert.Single(Handler.Events.OfType<EscEvent>());
        Assert.Equal('A', evt.Final);
        Assert.Equal('.', (char)evt.Intermediates);
    }

    [Fact]
    public void DesignateG3_96Char()
    {
        // ESC / A - Designate to G3 (96-char set)
        Parse("\u001b/A");

        var evt = Assert.Single(Handler.Events.OfType<EscEvent>());
        Assert.Equal('A', evt.Final);
        Assert.Equal('/', (char)evt.Intermediates);
    }

    #endregion
}
