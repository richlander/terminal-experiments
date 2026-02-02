// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Terminal.Parser;

namespace Microsoft.Extensions.Terminal.Multiplexing;

/// <summary>
/// A managed terminal session that owns a PTY and its child process.
/// </summary>
public sealed class ManagedSession : IAsyncDisposable, IDisposable
{
    private const int DefaultBufferSize = 64 * 1024; // 64KB

    private readonly string _id;
    private readonly string _command;
    private readonly string? _workingDirectory;
    private readonly DateTimeOffset _created;
    private readonly IPty _pty;
    private readonly CircularBuffer _outputBuffer;
    private readonly List<Channel<ReadOnlyMemory<byte>>> _subscribers = [];
    private readonly object _lock = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _readTask;
    private readonly TimeSpan? _idleTimeout;

    // Virtual terminal emulation
    private readonly VtParser _parser;
    private ScreenBuffer _screenBuffer;
    private readonly object _screenLock = new();

    private SessionState _state;
    private int? _exitCode;
    private int _columns;
    private int _rows;
    private bool _disposed;
    private DateTimeOffset _lastActivityTime;

    /// <summary>
    /// Creates a new managed session.
    /// </summary>
    /// <param name="id">The unique session identifier.</param>
    /// <param name="options">The PTY options.</param>
    /// <param name="bufferSize">The size of the output buffer in bytes.</param>
    public ManagedSession(string id, PtyOptions options, int bufferSize = DefaultBufferSize)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(bufferSize, 0);

        _id = id;
        _command = options.Command;
        _workingDirectory = options.WorkingDirectory;
        _columns = options.Columns;
        _rows = options.Rows;
        _idleTimeout = options.IdleTimeout;
        _created = DateTimeOffset.UtcNow;
        _lastActivityTime = _created;
        _state = SessionState.Starting;
        _outputBuffer = new CircularBuffer(bufferSize);

        // Initialize virtual terminal
        _screenBuffer = new ScreenBuffer(_columns, _rows);
        _parser = new VtParser(_screenBuffer);

        try
        {
            _pty = Pty.Create(options);
            _state = SessionState.Running;
        }
        catch
        {
            _state = SessionState.Failed;
            throw;
        }

        // Start reading output from PTY
        _readTask = ReadOutputAsync(_cts.Token);
    }

    /// <summary>
    /// Gets the session identifier.
    /// </summary>
    public string Id => _id;

    /// <summary>
    /// Gets the current session state.
    /// </summary>
    public SessionState State => _state;

    /// <summary>
    /// Gets the terminal width in columns.
    /// </summary>
    public int Columns => _columns;

    /// <summary>
    /// Gets the terminal height in rows.
    /// </summary>
    public int Rows => _rows;

    /// <summary>
    /// Gets the session information.
    /// </summary>
    public SessionInfo Info => new(
        _id,
        _command,
        _workingDirectory,
        _state,
        _created,
        _exitCode,
        _columns,
        _rows);

    /// <summary>
    /// Gets the idle timeout for this session, or null if no timeout is set.
    /// </summary>
    public TimeSpan? IdleTimeout => _idleTimeout;

    /// <summary>
    /// Gets the last activity time for this session.
    /// </summary>
    public DateTimeOffset LastActivityTime => _lastActivityTime;

    /// <summary>
    /// Gets whether this session has exceeded its idle timeout.
    /// </summary>
    public bool IsIdleTimedOut =>
        _idleTimeout.HasValue &&
        _state == SessionState.Running &&
        DateTimeOffset.UtcNow - _lastActivityTime > _idleTimeout.Value;

    /// <summary>
    /// Sends input to the session.
    /// </summary>
    /// <param name="data">The input data.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public async ValueTask SendInputAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_state != SessionState.Running)
        {
            throw new InvalidOperationException($"Cannot send input to session in state {_state}");
        }

        _lastActivityTime = DateTimeOffset.UtcNow;
        await _pty.WriteAsync(data, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resizes the terminal.
    /// </summary>
    /// <param name="columns">The new width in columns.</param>
    /// <param name="rows">The new height in rows.</param>
    public void Resize(int columns, int rows)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_state != SessionState.Running)
        {
            return;
        }

        _pty.Resize(columns, rows);
        _columns = columns;
        _rows = rows;

        // Resize screen buffer (create new one with new dimensions)
        lock (_screenLock)
        {
            var newBuffer = new ScreenBuffer(columns, rows);
            // Copy as much content as possible from old buffer
            int copyWidth = Math.Min(_screenBuffer.Width, columns);
            int copyHeight = Math.Min(_screenBuffer.Height, rows);
            for (int y = 0; y < copyHeight; y++)
            {
                var oldRow = _screenBuffer.GetRow(y);
                var newRow = newBuffer.GetRow(y);
                for (int x = 0; x < copyWidth; x++)
                {
                    newRow[x] = oldRow[x];
                }
            }
            _screenBuffer = newBuffer;
        }
    }

    /// <summary>
    /// Gets the buffered output from the session.
    /// This is useful for replaying output to newly attached clients.
    /// </summary>
    /// <returns>The buffered output.</returns>
    public byte[] GetBufferedOutput()
    {
        return _outputBuffer.ToArray();
    }

    /// <summary>
    /// Renders the current screen buffer as ANSI escape sequences.
    /// This gives a properly sized snapshot for the current terminal dimensions.
    /// </summary>
    /// <returns>The rendered screen as ANSI bytes.</returns>
    public byte[] RenderScreen()
    {
        lock (_screenLock)
        {
            return ScreenBufferRenderer.RenderFull(_screenBuffer);
        }
    }

    /// <summary>
    /// Renders the current screen buffer to fit a specific terminal size.
    /// </summary>
    /// <param name="targetWidth">Target terminal width.</param>
    /// <param name="targetHeight">Target terminal height.</param>
    /// <returns>The rendered screen as ANSI bytes.</returns>
    public byte[] RenderScreen(int targetWidth, int targetHeight)
    {
        // For now, resize the session to match the target.
        // A more sophisticated implementation could render at current size
        // and let the terminal handle scrolling.
        if (targetWidth != _columns || targetHeight != _rows)
        {
            Resize(targetWidth, targetHeight);
        }

        return RenderScreen();
    }

    /// <summary>
    /// Subscribes to live output from the session.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>An async enumerable of output chunks.</returns>
    public async IAsyncEnumerable<ReadOnlyMemory<byte>> SubscribeAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateBounded<ReadOnlyMemory<byte>>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        lock (_lock)
        {
            _subscribers.Add(channel);
        }

        try
        {
            await foreach (var data in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return data;
            }
        }
        finally
        {
            lock (_lock)
            {
                _subscribers.Remove(channel);
            }
        }
    }

    /// <summary>
    /// Waits for the session to exit.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The exit code.</returns>
    public async Task<int> WaitForExitAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return await _pty.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Terminates the session.
    /// </summary>
    /// <param name="force">If true, forcefully kills the process.</param>
    public void Kill(bool force = false)
    {
        if (_state != SessionState.Running)
        {
            return;
        }

        _pty.Kill(force);
    }

    private async Task ReadOutputAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int bytesRead = await _pty.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

                if (bytesRead == 0)
                {
                    // PTY closed
                    break;
                }

                var data = buffer.AsMemory(0, bytesRead);

                // Update activity time
                _lastActivityTime = DateTimeOffset.UtcNow;

                // Add to circular buffer (raw bytes)
                _outputBuffer.Write(data.Span);

                // Parse through VT parser to update screen buffer
                lock (_screenLock)
                {
                    _parser.Parse(data.Span);
                }

                // Broadcast to subscribers
                Channel<ReadOnlyMemory<byte>>[] subscribers;
                lock (_lock)
                {
                    subscribers = [.. _subscribers];
                }

                // Make a copy for async distribution
                var copy = data.ToArray().AsMemory();
                foreach (var subscriber in subscribers)
                {
                    subscriber.Writer.TryWrite(copy);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        catch (IOException)
        {
            // PTY closed
        }
        finally
        {
            // Update state
            _exitCode = _pty.ExitCode;
            _state = _exitCode.HasValue ? SessionState.Exited : SessionState.Failed;

            // Complete all subscriber channels
            lock (_lock)
            {
                foreach (var subscriber in _subscribers)
                {
                    subscriber.Writer.TryComplete();
                }
            }
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
        _cts.Dispose();
        _pty.Dispose();
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

        try
        {
            await _readTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        _cts.Dispose();
        await _pty.DisposeAsync().ConfigureAwait(false);
    }
}
