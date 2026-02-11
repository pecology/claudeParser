using ClaudeParser.Core;
using ClaudeParser.Combinators;

namespace ClaudeParser.Tests;

/// <summary>
/// バイナリパーサーのテスト
/// </summary>
public class BinaryParsersTests
{
    [Fact]
    public void Byte_ShouldMatchSpecificByte()
    {
        var input = new ByteInputStream(new byte[] { 0x42, 0x00 });
        var result = BinaryParsers.Byte(0x42).Parse(input);
        Assert.True(result.IsSuccess);
        Assert.Equal((byte)0x42, ((SuccessResult<byte, byte>)result).Value);
    }

    [Fact]
    public void Bytes_ShouldMatchByteSequence()
    {
        var input = new ByteInputStream(new byte[] { 0x4D, 0x54, 0x68, 0x64, 0x00 });
        var result = BinaryParsers.Bytes(0x4D, 0x54, 0x68, 0x64).Parse(input);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Take_ShouldReadSpecifiedBytes()
    {
        var input = new ByteInputStream(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 });
        var result = BinaryParsers.Take(3).Parse(input);
        Assert.True(result.IsSuccess);
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, ((SuccessResult<byte[], byte>)result).Value);
    }

    [Fact]
    public void UInt16BE_ShouldReadBigEndian()
    {
        var input = new ByteInputStream(new byte[] { 0x01, 0x02 });
        var result = BinaryParsers.UInt16BE.Parse(input);
        Assert.True(result.IsSuccess);
        Assert.Equal((ushort)0x0102, ((SuccessResult<ushort, byte>)result).Value);
    }

    [Fact]
    public void UInt16LE_ShouldReadLittleEndian()
    {
        var input = new ByteInputStream(new byte[] { 0x01, 0x02 });
        var result = BinaryParsers.UInt16LE.Parse(input);
        Assert.True(result.IsSuccess);
        Assert.Equal((ushort)0x0201, ((SuccessResult<ushort, byte>)result).Value);
    }

    [Fact]
    public void UInt32BE_ShouldReadBigEndian()
    {
        var input = new ByteInputStream(new byte[] { 0x00, 0x01, 0x02, 0x03 });
        var result = BinaryParsers.UInt32BE.Parse(input);
        Assert.True(result.IsSuccess);
        Assert.Equal(0x00010203u, ((SuccessResult<uint, byte>)result).Value);
    }

    [Fact]
    public void VariableLengthQuantity_ShouldParseSingleByte()
    {
        var input = new ByteInputStream(new byte[] { 0x7F });
        var result = BinaryParsers.VariableLengthQuantity.Parse(input);
        Assert.True(result.IsSuccess);
        Assert.Equal(127u, ((SuccessResult<uint, byte>)result).Value);
    }

    [Fact]
    public void VariableLengthQuantity_ShouldParseMultipleBytes()
    {
        // 0x81 0x00 = 128
        var input = new ByteInputStream(new byte[] { 0x81, 0x00 });
        var result = BinaryParsers.VariableLengthQuantity.Parse(input);
        Assert.True(result.IsSuccess);
        Assert.Equal(128u, ((SuccessResult<uint, byte>)result).Value);
    }

    [Fact]
    public void VariableLengthQuantity_ShouldParseLargeValue()
    {
        // 0xFF 0x7F = 16383
        var input = new ByteInputStream(new byte[] { 0xFF, 0x7F });
        var result = BinaryParsers.VariableLengthQuantity.Parse(input);
        Assert.True(result.IsSuccess);
        Assert.Equal(16383u, ((SuccessResult<uint, byte>)result).Value);
    }

    [Fact]
    public void FixedLengthString_ShouldReadString()
    {
        var input = new ByteInputStream(System.Text.Encoding.ASCII.GetBytes("HELLO\0\0\0"));
        var result = BinaryParsers.FixedLengthString(8).Parse(input);
        Assert.True(result.IsSuccess);
        Assert.Equal("HELLO", ((SuccessResult<string, byte>)result).Value);
    }

    [Fact]
    public void NullTerminatedString_ShouldReadUntilNull()
    {
        var input = new ByteInputStream(new byte[] { 0x48, 0x69, 0x00, 0xFF }); // "Hi\0"
        var result = BinaryParsers.NullTerminatedString().Parse(input);
        Assert.True(result.IsSuccess);
        Assert.Equal("Hi", ((SuccessResult<string, byte>)result).Value);
    }

    [Fact]
    public void ByteInRange_ShouldMatchBytesInRange()
    {
        var parser = BinaryParsers.ByteInRange(0x30, 0x39); // '0'-'9'
        
        var result1 = parser.Parse(new ByteInputStream(new byte[] { 0x35 })); // '5'
        Assert.True(result1.IsSuccess);
        
        var result2 = parser.Parse(new ByteInputStream(new byte[] { 0x41 })); // 'A'
        Assert.False(result2.IsSuccess);
    }
}
