// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Test patterns inspired by:
// - tmux/regress/new-session-command.sh (multiple session creation)
// - terminal/src/winconpty/ft_pty/ConPtyTests.cpp::GoodCreateMultiple

using System.Runtime.InteropServices;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Extensions.Terminal.Multiplexing.Tests;

/// <summary>
/// Tests for handling multiple concurrent sessions.
/// Inspired by tmux regress tests for multiple session operations.
/// </summary>
public class MultipleSessionTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private SessionHost? _host;
    private readonly int _port = 18100 + Random.Shared.Next(1000);

    public MultipleSessionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        if (!CanRunTests)
        {
            return;
        }

        var options = new SessionHostOptions
        {
            WebSocketPort = _port,
            PipeName = $"multi-test-{Guid.NewGuid():N}",
            MaxSessions = 10
        };

        _host = new SessionHost(options);
        await _host.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_host != null)
        {
            await _host.DisposeAsync();
        }
    }

    private static bool CanRunTests =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
        RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    /// <summary>
    /// Verifies that multiple sessions can be created concurrently.
    /// Inspired by ConPtyTests::GoodCreateMultiple.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "Pty")]
    public void CreateMultipleSessions_Succeeds()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        var sessions = new List<ManagedSession>();

        for (int i = 0; i < 5; i++)
        {
            var session = _host!.CreateSession($"multi-{i}-{Guid.NewGuid():N}", new PtyOptions
            {
                Command = "/bin/sh",
                Arguments = ["-c", $"sleep {10 + i}"],
                Columns = 80,
                Rows = 24
            });
            sessions.Add(session);
        }

        Assert.Equal(5, _host!.SessionCount);

        foreach (var session in sessions)
        {
            Assert.Equal(SessionState.Running, session.State);
        }
    }

    /// <summary>
    /// Verifies that ListSessions returns all active sessions.
    /// Inspired by tmux ls command testing.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "Pty")]
    public void ListSessions_ReturnsAllSessions()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        var expectedIds = new List<string>();

        for (int i = 0; i < 3; i++)
        {
            var id = $"list-{i}-{Guid.NewGuid():N}";
            expectedIds.Add(id);
            _host!.CreateSession(id, new PtyOptions
            {
                Command = "/bin/sh",
                Arguments = ["-c", "sleep 10"],
                Columns = 80,
                Rows = 24
            });
        }

        var sessions = _host!.ListSessions();

        Assert.Equal(3, sessions.Count);
        foreach (var expectedId in expectedIds)
        {
            Assert.Contains(sessions, s => s.Id == expectedId);
        }
    }

    /// <summary>
    /// Verifies that max sessions limit is enforced.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "Pty")]
    public void CreateSession_ExceedsMaxSessions_ThrowsException()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        // Create sessions up to the limit (10)
        for (int i = 0; i < 10; i++)
        {
            _host!.CreateSession($"max-{i}-{Guid.NewGuid():N}", new PtyOptions
            {
                Command = "/bin/sh",
                Arguments = ["-c", "sleep 100"],
                Columns = 80,
                Rows = 24
            });
        }

        // Attempting to create one more should fail
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _host!.CreateSession($"max-overflow-{Guid.NewGuid():N}", new PtyOptions
            {
                Command = "/bin/sh",
                Arguments = ["-c", "sleep 100"],
                Columns = 80,
                Rows = 24
            }));

        Assert.Contains("Maximum session count", ex.Message);
    }

    /// <summary>
    /// Verifies that sessions can have different terminal sizes.
    /// Inspired by tmux/regress/new-session-size.sh.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "Pty")]
    public void CreateMultipleSessions_DifferentSizes_Succeeds()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        var sizes = new[] { (80, 24), (120, 40), (200, 50) };

        foreach (var (cols, rows) in sizes)
        {
            var session = _host!.CreateSession($"size-{cols}x{rows}-{Guid.NewGuid():N}", new PtyOptions
            {
                Command = "/bin/sh",
                Arguments = ["-c", "sleep 10"],
                Columns = cols,
                Rows = rows
            });

            Assert.Equal(cols, session.Columns);
            Assert.Equal(rows, session.Rows);
        }
    }

    /// <summary>
    /// Verifies that multiple clients can attach to different sessions.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "Pty")]
    public async Task MultipleSessions_MultipleClients_EachReceivesCorrectOutput()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        var session1Id = $"client1-{Guid.NewGuid():N}";
        var session2Id = $"client2-{Guid.NewGuid():N}";

        _host!.CreateSession(session1Id, new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "echo SESSION_ONE; sleep 2"],
            Columns = 80,
            Rows = 24,
            Environment = new Dictionary<string, string> { ["TERM"] = "xterm-256color" }
        });

        _host.CreateSession(session2Id, new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "echo SESSION_TWO; sleep 2"],
            Columns = 80,
            Rows = 24,
            Environment = new Dictionary<string, string> { ["TERM"] = "xterm-256color" }
        });

        // Connect two clients to different sessions
        using var client1 = await SessionClient.ConnectAsync($"ws://localhost:{_port}/");
        using var client2 = await SessionClient.ConnectAsync($"ws://localhost:{_port}/");

        await using var attach1 = await client1.AttachAsync(session1Id);
        await using var attach2 = await client2.AttachAsync(session2Id);

        // Read from each
        var output1 = new StringBuilder();
        var output2 = new StringBuilder();

        var task1 = Task.Run(async () =>
        {
            await foreach (var data in attach1.ReadOutputAsync(CancellationToken.None))
            {
                output1.Append(Encoding.UTF8.GetString(data.Span));
                if (output1.ToString().Contains("SESSION_ONE"))
                {
                    break;
                }
            }
        });

        var task2 = Task.Run(async () =>
        {
            await foreach (var data in attach2.ReadOutputAsync(CancellationToken.None))
            {
                output2.Append(Encoding.UTF8.GetString(data.Span));
                if (output2.ToString().Contains("SESSION_TWO"))
                {
                    break;
                }
            }
        });

        await Task.WhenAll(task1, task2).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Contains("SESSION_ONE", output1.ToString());
        Assert.Contains("SESSION_TWO", output2.ToString());

        // Ensure outputs are not mixed
        Assert.DoesNotContain("SESSION_TWO", output1.ToString());
        Assert.DoesNotContain("SESSION_ONE", output2.ToString());
    }

    /// <summary>
    /// Verifies that killing one session doesn't affect others.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "Pty")]
    public async Task KillSession_DoesNotAffectOtherSessions()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        var session1Id = $"survive1-{Guid.NewGuid():N}";
        var session2Id = $"survive2-{Guid.NewGuid():N}";
        var session3Id = $"kill-target-{Guid.NewGuid():N}";

        _host!.CreateSession(session1Id, new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "sleep 100"],
            Columns = 80,
            Rows = 24
        });

        _host.CreateSession(session2Id, new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "sleep 100"],
            Columns = 80,
            Rows = 24
        });

        _host.CreateSession(session3Id, new PtyOptions
        {
            Command = "/bin/sh",
            Arguments = ["-c", "sleep 100"],
            Columns = 80,
            Rows = 24
        });

        Assert.Equal(3, _host.SessionCount);

        // Kill middle session
        await _host.KillSessionAsync(session3Id, force: true);

        Assert.Equal(2, _host.SessionCount);
        Assert.NotNull(_host.GetSession(session1Id));
        Assert.NotNull(_host.GetSession(session2Id));
        Assert.Null(_host.GetSession(session3Id));

        // Verify remaining sessions are still running
        Assert.Equal(SessionState.Running, _host.GetSession(session1Id)!.State);
        Assert.Equal(SessionState.Running, _host.GetSession(session2Id)!.State);
    }

    /// <summary>
    /// Verifies that session list is accurate via client protocol.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "Pty")]
    public async Task ListSessions_ViaClient_ReturnsCorrectList()
    {
        Skip.IfNot(CanRunTests, "PTY tests only run on Unix");

        var ids = new[] { $"proto-list-1-{Guid.NewGuid():N}", $"proto-list-2-{Guid.NewGuid():N}" };

        foreach (var id in ids)
        {
            _host!.CreateSession(id, new PtyOptions
            {
                Command = "/bin/sh",
                Arguments = ["-c", "sleep 10"],
                Columns = 80,
                Rows = 24
            });
        }

        using var client = await SessionClient.ConnectAsync($"ws://localhost:{_port}/");
        var sessions = await client.ListSessionsAsync();

        Assert.Equal(2, sessions.Count);
        foreach (var id in ids)
        {
            Assert.Contains(sessions, s => s.Id == id);
        }
    }
}
