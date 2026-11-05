namespace Rikka.Remote.Wire.Tests;

public sealed class DatagramTests
{
    [Fact]
    public void VideoShardsFollowTheDocumentedLayout()
    {
        var shard = new VideoShard
        {
            Seq = 0x0102_0304,
            Stream = 0x0506,
            Frame = 0x0708_090A,
            Reference = 0x0B0C_0D0E,
            Flags = FrameFlags.Intra,
            CaptureUs = 0x1112_1314,
            FrameBytes = 0x2122_2324,
            BlockOffset = 0x3132_3334,
            Block = 0x4142,
            Blocks = 0x5152,
            DataShards = 0x61,
            ParityShards = 0x62,
            Shard = 0x63,
            Payload = [0xAA, 0xBB],
        };
        var bytes = new byte[shard.EncodedLength];

        shard.Encode(bytes);

        byte[] expected =
        [
            0x01,
            0x04, 0x03, 0x02, 0x01,
            0x06, 0x05,
            0x0A, 0x09, 0x08, 0x07,
            0x0E, 0x0D, 0x0C, 0x0B,
            0x01,
            0x14, 0x13, 0x12, 0x11,
            0x24, 0x23, 0x22, 0x21,
            0x34, 0x33, 0x32, 0x31,
            0x42, 0x41,
            0x52, 0x51,
            0x61, 0x62, 0x63, 0x00,
            0x02, 0x00,
            0xAA, 0xBB,
        ];
        Assert.Equal(expected, bytes);
        Assert.Equal(VideoShard.HeaderBytes + 2, bytes.Length);
    }

    [Fact]
    public void VideoShardsRoundTrip()
    {
        var payload = Enumerable.Repeat((byte)1, 1000).ToArray();
        var shard = new VideoShard
        {
            Seq = 4,
            Stream = 1,
            Frame = 10,
            Reference = 9,
            Flags = FrameFlags.Refinement,
            CaptureUs = 99,
            FrameBytes = 3000,
            Blocks = 1,
            DataShards = 3,
            ParityShards = 1,
            Shard = 3,
            Payload = payload,
        };
        var bytes = new byte[shard.EncodedLength];
        shard.Encode(bytes);

        var decoded = ServerDatagram.Decode(bytes);

        Assert.Equal(DatagramType.VideoShard, decoded.Type);
        Assert.Equal(4u, decoded.Seq);
        var video = decoded.Video;
        Assert.Equal(
            (shard.Stream, shard.Frame, shard.Reference, shard.Flags, shard.CaptureUs, shard.FrameBytes, shard.BlockOffset, shard.Block, shard.Blocks),
            (video.Stream, video.Frame, video.Reference, video.Flags, video.CaptureUs, video.FrameBytes, video.BlockOffset, video.Block, video.Blocks));
        Assert.Equal((shard.DataShards, shard.ParityShards, shard.Shard), (video.DataShards, video.ParityShards, video.Shard));
        Assert.True(video.Payload.SequenceEqual(payload));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AudioPacketsRoundTrip(bool redundant)
    {
        var packet = new AudioPacket { Seq = 5, Timestamp = 4800, Primary = [1, 2, 3], HasPrevious = redundant, Previous = redundant ? [4, 5] : [] };
        var bytes = new byte[packet.EncodedLength];
        packet.Encode(bytes);

        var decoded = ServerDatagram.Decode(bytes);

        Assert.Equal(DatagramType.Audio, decoded.Type);
        Assert.Equal((5u, 4800u, redundant), (decoded.Audio.Seq, decoded.Audio.Timestamp, decoded.Audio.HasPrevious));
        Assert.Equal([1, 2, 3], decoded.Audio.Primary.ToArray());
        Assert.Equal(redundant ? [4, 5] : [], decoded.Audio.Previous.ToArray());
    }

    [Fact]
    public void AudioPacketsFollowTheDocumentedLayout()
    {
        var packet = new AudioPacket { Seq = 1, Timestamp = 0x01E0, Primary = [7], HasPrevious = true, Previous = [8, 9] };
        var bytes = new byte[packet.EncodedLength];

        packet.Encode(bytes);

        Assert.Equal([0x02, 1, 0, 0, 0, 0xE0, 0x01, 0, 0, 0x01, 1, 0, 7, 8, 9], bytes);
    }

    [Fact]
    public void PaddingAndPointerRoundTrip()
    {
        var padding = new byte[1200];
        Assert.Equal(1200, ServerDatagram.EncodePadding(6, 1200, padding));
        var decodedPadding = ServerDatagram.Decode(padding);
        Assert.Equal((DatagramType.Padding, 6u), (decodedPadding.Type, decodedPadding.Seq));

        var pointer = new byte[13];
        ServerDatagram.EncodePointer(7, -3, 1080, pointer);
        var decodedPointer = ServerDatagram.Decode(pointer);
        Assert.Equal((DatagramType.Pointer, 7u, -3, 1080), (decodedPointer.Type, decodedPointer.Seq, decodedPointer.PointerX, decodedPointer.PointerY));
    }

    public static TheoryData<byte, byte, byte, ushort, ushort, uint, uint> InvalidShards() => new()
    {
        { 2, 1, 3, 0, 1, 10, 0 },
        { 0, 1, 0, 0, 1, 10, 0 },
        { 200, 56, 0, 0, 1, 10, 0 },
        { 2, 1, 0, 1, 1, 10, 0 },
        { 2, 1, 0, 0, 0, 10, 0 },
        { 2, 1, 0, 0, 1, 10, 10 },
    };

    [Theory]
    [MemberData(nameof(InvalidShards))]
    public void ShardsOutsideTheirBlockAreRejected(byte data, byte parity, byte index, ushort block, ushort blocks, uint frameBytes, uint offset)
    {
        var shard = new VideoShard
        {
            FrameBytes = frameBytes,
            BlockOffset = offset,
            Block = block,
            Blocks = blocks,
            DataShards = data,
            ParityShards = parity,
            Shard = index,
            Payload = new byte[5],
        };
        var bytes = new byte[shard.EncodedLength];
        shard.Encode(bytes);

        var error = Assert.Throws<WireFormatException>(() => ServerDatagram.Decode(bytes));
        Assert.Equal(DecodeError.InvalidValue, error.Error);
    }

    [Theory]
    [InlineData(new byte[] { }, DecodeError.Empty)]
    [InlineData(new byte[] { 0x09, 0, 0, 0, 0 }, DecodeError.UnknownType)]
    [InlineData(new byte[] { 0x04, 0, 0, 0 }, DecodeError.Truncated)]
    [InlineData(new byte[] { 0x04, 0, 0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0, 9 }, DecodeError.Trailing)]
    public void MalformedServerDatagramsFailExplicitly(byte[] bytes, DecodeError expected)
    {
        var error = Assert.Throws<WireFormatException>(() => ServerDatagram.Decode(bytes));
        Assert.Equal(expected, error.Error);
    }

    [Fact]
    public void FeedbackKeepsArrivalTimesWithinOneUnit()
    {
        Arrival[] arrivals =
        [
            new(100, 4_294_966_000),
            new(101, 4_294_966_013),
            new(103, 4_294_967_280),
            new(104, 4_294_967_200),
            new(107, 300),
        ];
        var feedback = new Feedback(100, 9, arrivals, 3, 41, [39, 40]);
        var bytes = new byte[Feedback.BytesFor(9, 5, 2)];

        Assert.Equal(bytes.Length, feedback.Encode(bytes));
        var decoded = Feedback.Decode(bytes);

        Assert.Equal((100u, (ushort)9, (ushort)3, (uint?)41), (decoded.BaseSeq, decoded.Count, decoded.Stream, decoded.Good));
        Assert.Equal([39u, 40u], decoded.Lost);
        Assert.Equal([100u, 101u, 103u, 104u, 107u], decoded.Arrivals.Select(arrival => arrival.Seq));
        foreach (var (sent, got) in arrivals.Zip(decoded.Arrivals))
        {
            var drift = (int)(got.AtUs - sent.AtUs);
            Assert.InRange(Math.Abs(drift), 0, (int)Protocol.DeltaUnitUs / 2);
        }
    }

    [Fact]
    public void FeedbackFollowsTheDocumentedLayout()
    {
        var feedback = new Feedback(0x0A, 10, [new Arrival(0x0A, 1000), new Arrival(0x0C, 1100), new Arrival(0x13, 1000)], 2, null, [5]);
        var bytes = new byte[Feedback.BytesFor(10, 3, 1)];

        feedback.Encode(bytes);

        byte[] expected =
        [
            0x42,
            0x0A, 0, 0, 0,
            10, 0,
            0xE8, 0x03, 0, 0,
            0b0000_0101, 0b0000_0010,
            2, 0,
            0xFE, 0xFF,
            2, 0,
            0xFF, 0xFF, 0xFF, 0xFF,
            1,
            5, 0, 0, 0,
        ];
        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void EmptyFeedbackStillReportsFrames()
    {
        var bytes = new byte[Feedback.BytesFor(0, 0, 0)];
        Feedback.Encode(bytes, 77, 0, [], 4, 12, []);

        Assert.Equal(new Feedback(77, 0, [], 4, 12, []), Feedback.Decode(bytes));
    }

    [Fact]
    public void MotionRoundTrips()
    {
        var motion = new Motion(65535, new Position(1, 65535));
        var bytes = new byte[Motion.Bytes];

        motion.Encode(bytes);

        Assert.Equal([0x41, 0xFF, 0xFF, 1, 0, 0xFF, 0xFF], bytes);
        Assert.Equal(motion, Motion.Decode(bytes));
    }
}
