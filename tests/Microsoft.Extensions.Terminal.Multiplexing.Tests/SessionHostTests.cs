// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using Xunit;

namespace Microsoft.Extensions.Terminal.Multiplexing.Tests;

/// <summary>
/// Tests for SessionHost that don't require spawning processes.
/// </summary>
public class SessionHostTests
{
    [Fact]
    public void Constructor_CreatesEmptyHost()
    {
        var options = new SessionHostOptions
        {
            WebSocketPort = 0,
            PipeName = null
        };

        using var host = new SessionHost(options);

        Assert.Equal(0, host.SessionCount);
    }

    [Fact]
    public void GetSession_ReturnsNullForUnknown()
    {
        var options = new SessionHostOptions
        {
            WebSocketPort = 0,
            PipeName = null
        };

        using var host = new SessionHost(options);

        var session = host.GetSession("nonexistent");

        Assert.Null(session);
    }

    [Fact]
    public void ListSessions_ReturnsEmptyWhenNoSessions()
    {
        var options = new SessionHostOptions
        {
            WebSocketPort = 0,
            PipeName = null
        };

        using var host = new SessionHost(options);

        var list = host.ListSessions();

        Assert.Empty(list);
    }

    [Fact]
    public async Task KillSessionAsync_ReturnsFalseForUnknown()
    {
        var options = new SessionHostOptions
        {
            WebSocketPort = 0,
            PipeName = null
        };

        await using var host = new SessionHost(options);

        var killed = await host.KillSessionAsync("nonexistent", force: true);

        Assert.False(killed);
    }

    [Fact]
    public void SessionHostOptions_HasCorrectDefaults()
    {
        var options = new SessionHostOptions();

        Assert.Equal(7777, options.WebSocketPort);
        Assert.Equal("termhost", options.PipeName);
        Assert.Equal(100, options.MaxSessions);
        Assert.Equal(64 * 1024, options.DefaultBufferSize);
        Assert.Equal(1, options.ProtocolVersion);
    }
}
