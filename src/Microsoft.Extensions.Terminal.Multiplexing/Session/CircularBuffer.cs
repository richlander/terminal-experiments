// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Extensions.Terminal.Multiplexing;

/// <summary>
/// A circular buffer for storing recent terminal output.
/// Thread-safe for single writer, multiple readers.
/// </summary>
internal sealed class CircularBuffer
{
    private readonly byte[] _buffer;
    private readonly object _lock = new();
    private int _writePosition;
    private int _length;

    /// <summary>
    /// Creates a new circular buffer with the specified capacity.
    /// </summary>
    /// <param name="capacity">The capacity in bytes.</param>
    public CircularBuffer(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(capacity, 0);
        _buffer = new byte[capacity];
    }

    /// <summary>
    /// Gets the current number of bytes in the buffer.
    /// </summary>
    public int Length
    {
        get
        {
            lock (_lock)
            {
                return _length;
            }
        }
    }

    /// <summary>
    /// Gets the capacity of the buffer.
    /// </summary>
    public int Capacity => _buffer.Length;

    /// <summary>
    /// Writes data to the buffer, overwriting old data if necessary.
    /// </summary>
    /// <param name="data">The data to write.</param>
    public void Write(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return;
        }

        lock (_lock)
        {
            if (data.Length >= _buffer.Length)
            {
                // Data is larger than buffer, just keep the last chunk
                data.Slice(data.Length - _buffer.Length).CopyTo(_buffer);
                _writePosition = 0;
                _length = _buffer.Length;
                return;
            }

            int firstChunkSize = Math.Min(data.Length, _buffer.Length - _writePosition);
            data.Slice(0, firstChunkSize).CopyTo(_buffer.AsSpan(_writePosition));

            if (firstChunkSize < data.Length)
            {
                // Wrap around
                data.Slice(firstChunkSize).CopyTo(_buffer);
                _writePosition = data.Length - firstChunkSize;
            }
            else
            {
                _writePosition += firstChunkSize;
                if (_writePosition == _buffer.Length)
                {
                    _writePosition = 0;
                }
            }

            _length = Math.Min(_length + data.Length, _buffer.Length);
        }
    }

    /// <summary>
    /// Reads all data currently in the buffer.
    /// </summary>
    /// <returns>A copy of the buffer contents in order.</returns>
    public byte[] ToArray()
    {
        lock (_lock)
        {
            if (_length == 0)
            {
                return [];
            }

            var result = new byte[_length];

            if (_length < _buffer.Length)
            {
                // Buffer hasn't wrapped yet
                int startPos = _writePosition - _length;
                if (startPos < 0)
                {
                    startPos += _buffer.Length;
                }

                if (startPos + _length <= _buffer.Length)
                {
                    _buffer.AsSpan(startPos, _length).CopyTo(result);
                }
                else
                {
                    int firstChunk = _buffer.Length - startPos;
                    _buffer.AsSpan(startPos, firstChunk).CopyTo(result);
                    _buffer.AsSpan(0, _length - firstChunk).CopyTo(result.AsSpan(firstChunk));
                }
            }
            else
            {
                // Buffer is full and has wrapped
                int firstChunk = _buffer.Length - _writePosition;
                _buffer.AsSpan(_writePosition, firstChunk).CopyTo(result);
                _buffer.AsSpan(0, _writePosition).CopyTo(result.AsSpan(firstChunk));
            }

            return result;
        }
    }

    /// <summary>
    /// Clears the buffer.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _writePosition = 0;
            _length = 0;
        }
    }
}
