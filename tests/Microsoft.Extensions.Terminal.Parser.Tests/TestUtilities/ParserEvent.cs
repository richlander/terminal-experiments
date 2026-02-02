// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.Extensions.Terminal.Parser.Tests;

/// <summary>
/// Base class for parser events captured by <see cref="RecordingHandler"/>.
/// </summary>
public abstract record ParserEvent;

/// <summary>Printable character event.</summary>
public record PrintEvent(char Char) : ParserEvent;

/// <summary>C0/C1 control code execution.</summary>
public record ExecuteEvent(byte Code) : ParserEvent;

/// <summary>CSI sequence dispatch.</summary>
public record CsiEvent(int[] Params, byte PrivateMarker, byte Intermediates, char Command) : ParserEvent
{
    /// <summary>Final character of the CSI sequence.</summary>
    public char Final => Command;
    
    /// <summary>Intermediate characters as string.</summary>
    public string IntermediatesString => Intermediates == 0 ? "" : ((char)Intermediates).ToString();
    
    /// <summary>Whether this is a private sequence (started with ? > = &lt;).</summary>
    public bool Private => PrivateMarker is (byte)'?' or (byte)'>' or (byte)'=' or (byte)'<';
}

// Alias for test compatibility
public record CsiDispatchEvent(int[] Params, string Intermediates, char Final, bool Private = false) : ParserEvent;

/// <summary>ESC sequence dispatch.</summary>
public record EscEvent(byte Intermediates, char Command) : ParserEvent
{
    /// <summary>Final character of the ESC sequence.</summary>
    public char Final => Command;
    
    /// <summary>Intermediate characters as string.</summary>
    public string IntermediatesString => Intermediates == 0 ? "" : ((char)Intermediates).ToString();
}

// Alias for test compatibility  
public record EscDispatchEvent(string Intermediates, char Final) : ParserEvent;

/// <summary>OSC sequence dispatch.</summary>
public record OscEvent(int Command, byte[] Data) : ParserEvent
{
    /// <summary>The OSC data as a string.</summary>
    public string DataString => System.Text.Encoding.UTF8.GetString(Data);
}

// Alias for test compatibility
public record OscDispatchEvent(int Command, string Data) : ParserEvent;

/// <summary>DCS hook start.</summary>
public record DcsHookEvent(int[] Params, string Intermediates, char Final) : ParserEvent
{
    /// <summary>Final character of the DCS sequence.</summary>
    public char Command => Final;
}

/// <summary>DCS data.</summary>
public record DcsPutEvent(string Data) : ParserEvent;

/// <summary>DCS sequence complete.</summary>
public record DcsUnhookEvent() : ParserEvent;
