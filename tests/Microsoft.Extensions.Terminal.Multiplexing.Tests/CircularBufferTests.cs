// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit;

namespace Microsoft.Extensions.Terminal.Multiplexing.Tests;

public class CircularBufferTests
{
    [Fact]
    public void Write_SingleChunk_StoresData()
    {
        var buffer = new CircularBuffer(100);
        var data = new byte[] { 1, 2, 3, 4, 5 };

        buffer.Write(data);

        Assert.Equal(5, buffer.Length);
        Assert.Equal(data, buffer.ToArray());
    }

    [Fact]
    public void Write_MultipleChunks_StoresAllData()
    {
        var buffer = new CircularBuffer(100);

        buffer.Write(new byte[] { 1, 2, 3 });
        buffer.Write(new byte[] { 4, 5, 6 });

        Assert.Equal(6, buffer.Length);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, buffer.ToArray());
    }

    [Fact]
    public void Write_OverflowsBuffer_KeepsRecentData()
    {
        var buffer = new CircularBuffer(5);

        buffer.Write(new byte[] { 1, 2, 3 });
        buffer.Write(new byte[] { 4, 5, 6, 7 });

        Assert.Equal(5, buffer.Length);
        Assert.Equal(new byte[] { 3, 4, 5, 6, 7 }, buffer.ToArray());
    }

    [Fact]
    public void Write_ExactlyFillsBuffer_WorksCorrectly()
    {
        var buffer = new CircularBuffer(5);

        buffer.Write(new byte[] { 1, 2, 3, 4, 5 });

        Assert.Equal(5, buffer.Length);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, buffer.ToArray());
    }

    [Fact]
    public void Write_LargerThanBuffer_KeepsLastChunk()
    {
        var buffer = new CircularBuffer(5);

        buffer.Write(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });

        Assert.Equal(5, buffer.Length);
        Assert.Equal(new byte[] { 6, 7, 8, 9, 10 }, buffer.ToArray());
    }

    [Fact]
    public void Write_WrapAround_MaintainsOrder()
    {
        var buffer = new CircularBuffer(10);

        // Fill buffer partially
        buffer.Write(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        // This should wrap around
        buffer.Write(new byte[] { 9, 10, 11, 12 });

        Assert.Equal(10, buffer.Length);
        Assert.Equal(new byte[] { 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, buffer.ToArray());
    }

    [Fact]
    public void Clear_ResetsBuffer()
    {
        var buffer = new CircularBuffer(10);
        buffer.Write(new byte[] { 1, 2, 3, 4, 5 });

        buffer.Clear();

        Assert.Equal(0, buffer.Length);
        Assert.Empty(buffer.ToArray());
    }

    [Fact]
    public void Write_EmptyData_DoesNothing()
    {
        var buffer = new CircularBuffer(10);
        buffer.Write(new byte[] { 1, 2, 3 });

        buffer.Write(ReadOnlySpan<byte>.Empty);

        Assert.Equal(3, buffer.Length);
        Assert.Equal(new byte[] { 1, 2, 3 }, buffer.ToArray());
    }

    [Fact]
    public void ToArray_EmptyBuffer_ReturnsEmptyArray()
    {
        var buffer = new CircularBuffer(10);

        var result = buffer.ToArray();

        Assert.Empty(result);
    }
}
