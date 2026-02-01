// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Net;
using System.Net.WebSockets;

namespace Microsoft.Extensions.Terminal.Multiplexing;

/// <summary>
/// Hosts and manages multiple terminal sessions, exposing them via WebSocket and named pipe.
/// </summary>
public sealed class SessionHost : IAsyncDisposable, IDisposable
{
    private readonly SessionHostOptions _options;
    private readonly ConcurrentDictionary<string, ManagedSession> _sessions = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly List<Task> _clientTasks = [];
    private readonly object _clientLock = new();
    private readonly Timer _idleCheckTimer;

    private HttpListener? _httpListener;
    private NamedPipeServerStream? _pipeServer;
    private Task? _webSocketTask;
    private Task? _pipeTask;
    private bool _disposed;

    /// <summary>
    /// Creates a new session host with default options.
    /// </summary>
    public SessionHost() : this(new SessionHostOptions())
    {
    }

    /// <summary>
    /// Creates a new session host with the specified options.
    /// </summary>
    public SessionHost(SessionHostOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        // Start idle check timer (every 30 seconds)
        _idleCheckTimer = new Timer(CheckIdleSessions, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Gets the number of active sessions.
    /// </summary>
    public int SessionCount => _sessions.Count;

    /// <summary>
    /// Starts the session host.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_options.WebSocketPort > 0)
        {
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add($"http://localhost:{_options.WebSocketPort}/");
            _httpListener.Start();
            _webSocketTask = AcceptWebSocketClientsAsync(_cts.Token);
        }

        if (!string.IsNullOrEmpty(_options.PipeName))
        {
            _pipeTask = AcceptPipeClientsAsync(_cts.Token);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates a new session.
    /// </summary>
    public ManagedSession CreateSession(string id, PtyOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(options);

        if (_sessions.Count >= _options.MaxSessions)
        {
            throw new InvalidOperationException($"Maximum session count ({_options.MaxSessions}) reached");
        }

        var session = new ManagedSession(id, options, _options.DefaultBufferSize);

        if (!_sessions.TryAdd(id, session))
        {
            session.Dispose();
            throw new InvalidOperationException($"Session with ID '{id}' already exists");
        }

        return session;
    }

    /// <summary>
    /// Gets a session by ID.
    /// </summary>
    public ManagedSession? GetSession(string id)
    {
        _sessions.TryGetValue(id, out var session);
        return session;
    }

    /// <summary>
    /// Lists all sessions.
    /// </summary>
    public IReadOnlyList<SessionInfo> ListSessions()
    {
        return _sessions.Values.Select(s => s.Info).ToList();
    }

    /// <summary>
    /// Kills a session.
    /// </summary>
    public async ValueTask<bool> KillSessionAsync(string id, bool force = false)
    {
        if (!_sessions.TryRemove(id, out var session))
        {
            return false;
        }

        session.Kill(force);
        await session.DisposeAsync().ConfigureAwait(false);
        return true;
    }

    private void CheckIdleSessions(object? state)
    {
        if (_disposed)
        {
            return;
        }

        // Find sessions that have exceeded their idle timeout
        var idleSessions = _sessions.Values
            .Where(s => s.IsIdleTimedOut)
            .Select(s => s.Id)
            .ToList();

        // Kill idle sessions
        foreach (var sessionId in idleSessions)
        {
            if (_sessions.TryRemove(sessionId, out var session))
            {
                session.Kill(force: false);
                session.Dispose();
            }
        }
    }

    private async Task AcceptWebSocketClientsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _httpListener is not null)
        {
            try
            {
                var context = await _httpListener.GetContextAsync().ConfigureAwait(false);

                if (!context.Request.IsWebSocketRequest)
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                    continue;
                }

                var wsContext = await context.AcceptWebSocketAsync(null).ConfigureAwait(false);
                var clientTask = HandleWebSocketClientAsync(wsContext.WebSocket, cancellationToken);

                lock (_clientLock)
                {
                    _clientTasks.Add(clientTask);
                }
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }
    }

    private async Task AcceptPipeClientsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _pipeServer = new NamedPipeServerStream(
                    _options.PipeName!,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await _pipeServer.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                var clientPipe = _pipeServer;
                _pipeServer = null;

                var clientTask = HandlePipeClientAsync(clientPipe, cancellationToken);

                lock (_clientLock)
                {
                    _clientTasks.Add(clientTask);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException)
            {
                // Pipe was closed, continue
            }
        }
    }

    private async Task HandleWebSocketClientAsync(WebSocket webSocket, CancellationToken cancellationToken)
    {
        using var stream = new WebSocketStream(webSocket);
        await HandleClientAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    private async Task HandlePipeClientAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        await using var _ = pipe.ConfigureAwait(false);
        await HandleClientAsync(pipe, cancellationToken).ConfigureAwait(false);
    }

    private async Task HandleClientAsync(Stream stream, CancellationToken cancellationToken)
    {
        var reader = new ProtocolReader(stream);
        var writer = new ProtocolWriter(stream);

        ManagedSession? attachedSession = null;
        CancellationTokenSource? outputCts = null;
        Task? outputTask = null;

        try
        {
            // Send hello
            await writer.WriteHelloAsync(_options.ProtocolVersion, cancellationToken).ConfigureAwait(false);

            // Wait for client hello
            var msg = await reader.ReadMessageAsync(cancellationToken).ConfigureAwait(false);
            if (msg is null || msg.Value.Type != MessageType.Hello)
            {
                return;
            }

            // Handle messages
            while (!cancellationToken.IsCancellationRequested)
            {
                msg = await reader.ReadMessageAsync(cancellationToken).ConfigureAwait(false);
                if (msg is null)
                {
                    break;
                }

                var (type, payload) = msg.Value;

                switch (type)
                {
                    case MessageType.ListSessions:
                        await writer.WriteSessionListAsync(ListSessions(), cancellationToken).ConfigureAwait(false);
                        break;

                    case MessageType.CreateSession:
                        {
                            var (id, options) = ProtocolReader.ParseCreateSession(payload);
                            try
                            {
                                var session = CreateSession(id, options);
                                await writer.WriteSessionCreatedAsync(session.Info, cancellationToken).ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                await writer.WriteErrorAsync(ex.Message, cancellationToken).ConfigureAwait(false);
                            }
                        }
                        break;

                    case MessageType.Attach:
                        {
                            var sessionId = ProtocolReader.ParseAttach(payload);
                            var session = GetSession(sessionId);
                            if (session is null)
                            {
                                await writer.WriteErrorAsync($"Session '{sessionId}' not found", cancellationToken).ConfigureAwait(false);
                                break;
                            }

                            attachedSession = session;
                            var buffered = session.GetBufferedOutput();
                            await writer.WriteAttachedAsync(session.Info, buffered, cancellationToken).ConfigureAwait(false);

                            // Start streaming output
                            outputCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                            outputTask = StreamOutputAsync(session, writer, outputCts.Token);
                        }
                        break;

                    case MessageType.Detach:
                        if (outputCts is not null)
                        {
                            await outputCts.CancelAsync().ConfigureAwait(false);
                            outputCts.Dispose();
                            outputCts = null;
                        }
                        if (outputTask is not null)
                        {
                            try { await outputTask.ConfigureAwait(false); } catch (OperationCanceledException) { }
                            outputTask = null;
                        }
                        attachedSession = null;
                        break;

                    case MessageType.Input:
                        if (attachedSession is not null)
                        {
                            await attachedSession.SendInputAsync(payload, cancellationToken).ConfigureAwait(false);
                        }
                        break;

                    case MessageType.Resize:
                        if (attachedSession is not null)
                        {
                            var (cols, rows) = ProtocolReader.ParseResize(payload);
                            attachedSession.Resize(cols, rows);
                        }
                        break;

                    case MessageType.KillSession:
                        {
                            var (sessionId, force) = ProtocolReader.ParseKillSession(payload);
                            var killed = await KillSessionAsync(sessionId, force).ConfigureAwait(false);
                            if (!killed)
                            {
                                await writer.WriteErrorAsync($"Session '{sessionId}' not found", cancellationToken).ConfigureAwait(false);
                            }
                        }
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (ProtocolException)
        {
            // Protocol error, close connection
        }
        catch (IOException)
        {
            // Connection closed
        }
        finally
        {
            if (outputCts is not null)
            {
                await outputCts.CancelAsync().ConfigureAwait(false);
                outputCts.Dispose();
            }
            if (outputTask is not null)
            {
                try { await outputTask.ConfigureAwait(false); } catch (OperationCanceledException) { }
            }
        }
    }

    private static async Task StreamOutputAsync(ManagedSession session, ProtocolWriter writer, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var data in session.SubscribeAsync(cancellationToken).ConfigureAwait(false))
            {
                await writer.WriteOutputAsync(data, cancellationToken).ConfigureAwait(false);
            }

            // Session ended
            await writer.WriteSessionExitedAsync(session.Id, session.Info.ExitCode ?? -1, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts.Cancel();
        _idleCheckTimer.Dispose();

        _httpListener?.Close();
        _pipeServer?.Dispose();

        foreach (var session in _sessions.Values)
        {
            session.Dispose();
        }

        _sessions.Clear();
        _cts.Dispose();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _cts.CancelAsync().ConfigureAwait(false);
        await _idleCheckTimer.DisposeAsync().ConfigureAwait(false);

        _httpListener?.Close();

        if (_pipeServer is not null)
        {
            await _pipeServer.DisposeAsync().ConfigureAwait(false);
        }

        // Wait for listener tasks
        if (_webSocketTask is not null)
        {
            try { await _webSocketTask.ConfigureAwait(false); } catch { }
        }
        if (_pipeTask is not null)
        {
            try { await _pipeTask.ConfigureAwait(false); } catch { }
        }

        // Wait for client tasks
        Task[] clientTasks;
        lock (_clientLock)
        {
            clientTasks = [.. _clientTasks];
        }
        await Task.WhenAll(clientTasks).ConfigureAwait(false);

        // Dispose sessions
        foreach (var session in _sessions.Values)
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }

        _sessions.Clear();
        _cts.Dispose();
    }
}
