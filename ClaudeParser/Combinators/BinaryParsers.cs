using ClaudeParser.Core;

namespace ClaudeParser.Combinators;

/// <summary>
/// バイナリデータ（byte）用のパーサーコンビネーター
/// </summary>
public static class BinaryParsers
{
    /// <summary>
    /// 指定したバイトをパースします
    /// </summary>
    public static Parser<byte, byte> Byte(byte b) =>
        Parsers.Satisfy<byte>(x => x == b, $"0x{b:X2}").Named($"Byte(0x{b:X2})");

    /// <summary>
    /// 任意の1バイトをパースします
    /// </summary>
    public static Parser<byte, byte> AnyByte =>
        Parsers.AnyToken<byte>().Named("AnyByte");

    /// <summary>
    /// 指定した範囲内のバイトをパースします
    /// </summary>
    public static Parser<byte, byte> ByteInRange(byte min, byte max) =>
        Parsers.Satisfy<byte>(b => b >= min && b <= max, $"0x{min:X2}-0x{max:X2}").Named($"ByteInRange(0x{min:X2}, 0x{max:X2})");

    /// <summary>
    /// 指定したバイト列をパースします
    /// </summary>
    public static Parser<byte[], byte> Bytes(params byte[] expected) =>
        new((input, ctx) =>
        {
            var current = input;
            foreach (var b in expected)
            {
                if (current.IsAtEnd || current.Current != b)
                {
                    var expectedStr = string.Join(" ", expected.Select(x => $"0x{x:X2}"));
                    return ParseResult<byte[], byte>.Failure(
                        new ParseError(input.Position, ErrorMessage.Expected(expectedStr)),
                        input);
                }
                current = current.Advance();
            }
            return ParseResult<byte[], byte>.Success(expected, current);
        }, $"Bytes({string.Join(" ", expected.Select(x => $"0x{x:X2}"))})");

    /// <summary>
    /// 指定した長さのバイト列を読み取ります
    /// </summary>
    public static Parser<byte[], byte> Take(int count) =>
        new((input, ctx) =>
        {
            if (input is ByteInputStream byteInput)
            {
                var bytes = byteInput.GetBytes(count);
                if (bytes.Length < count)
                {
                    return ParseResult<byte[], byte>.Failure(
                        new ParseError(input.Position, ErrorMessage.Expected($"{count}バイト以上のデータ")),
                        input);
                }
                return ParseResult<byte[], byte>.Success(bytes, byteInput.AdvanceBy(count));
            }

            // 一般的なIInputStream<byte>の場合
            var result = new byte[count];
            var current = input;
            for (int i = 0; i < count; i++)
            {
                if (current.IsAtEnd)
                {
                    return ParseResult<byte[], byte>.Failure(
                        new ParseError(input.Position, ErrorMessage.Expected($"{count}バイト以上のデータ")),
                        input);
                }
                result[i] = current.Current;
                current = current.Advance();
            }
            return ParseResult<byte[], byte>.Success(result, current);
        }, $"Take({count})");

    /// <summary>
    /// 8ビット符号なし整数をパースします
    /// </summary>
    public static Parser<byte, byte> UInt8 =>
        AnyByte.Named("UInt8");

    /// <summary>
    /// 8ビット符号付き整数をパースします
    /// </summary>
    public static Parser<sbyte, byte> Int8 =>
        AnyByte.Select(b => (sbyte)b).Named("Int8");

    /// <summary>
    /// 16ビット符号なし整数をパースします（ビッグエンディアン）
    /// </summary>
    public static Parser<ushort, byte> UInt16BE =>
        from bytes in Take(2)
        select (ushort)((bytes[0] << 8) | bytes[1]);

    /// <summary>
    /// 16ビット符号なし整数をパースします（リトルエンディアン）
    /// </summary>
    public static Parser<ushort, byte> UInt16LE =>
        from bytes in Take(2)
        select (ushort)((bytes[1] << 8) | bytes[0]);

    /// <summary>
    /// 16ビット符号付き整数をパースします（ビッグエンディアン）
    /// </summary>
    public static Parser<short, byte> Int16BE =>
        UInt16BE.Select(u => (short)u).Named("Int16BE");

    /// <summary>
    /// 16ビット符号付き整数をパースします（リトルエンディアン）
    /// </summary>
    public static Parser<short, byte> Int16LE =>
        UInt16LE.Select(u => (short)u).Named("Int16LE");

    /// <summary>
    /// 24ビット符号なし整数をパースします（ビッグエンディアン）
    /// </summary>
    public static Parser<uint, byte> UInt24BE =>
        from bytes in Take(3)
        select (uint)((bytes[0] << 16) | (bytes[1] << 8) | bytes[2]);

    /// <summary>
    /// 32ビット符号なし整数をパースします（ビッグエンディアン）
    /// </summary>
    public static Parser<uint, byte> UInt32BE =>
        from bytes in Take(4)
        select (uint)((bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3]);

    /// <summary>
    /// 32ビット符号なし整数をパースします（リトルエンディアン）
    /// </summary>
    public static Parser<uint, byte> UInt32LE =>
        from bytes in Take(4)
        select (uint)((bytes[3] << 24) | (bytes[2] << 16) | (bytes[1] << 8) | bytes[0]);

    /// <summary>
    /// 32ビット符号付き整数をパースします（ビッグエンディアン）
    /// </summary>
    public static Parser<int, byte> Int32BE =>
        UInt32BE.Select(u => (int)u).Named("Int32BE");

    /// <summary>
    /// 32ビット符号付き整数をパースします（リトルエンディアン）
    /// </summary>
    public static Parser<int, byte> Int32LE =>
        UInt32LE.Select(u => (int)u).Named("Int32LE");

    /// <summary>
    /// 可変長整数をパースします（MIDI VLQ形式）
    /// MSBが1の場合は続きがあることを示す
    /// </summary>
    public static Parser<uint, byte> VariableLengthQuantity =>
        new((input, ctx) =>
        {
            var current = input;
            uint result = 0;
            int count = 0;
            const int maxBytes = 4;

            while (!current.IsAtEnd && count < maxBytes)
            {
                var b = current.Current;
                current = current.Advance();
                count++;

                result = (result << 7) | (uint)(b & 0x7F);

                if ((b & 0x80) == 0)
                {
                    return ParseResult<uint, byte>.Success(result, current);
                }
            }

            if (count >= maxBytes)
            {
                return ParseResult<uint, byte>.Failure(
                    new ParseError(input.Position, ErrorMessage.Message("可変長整数が大きすぎます（4バイトを超過）")),
                    input);
            }

            return ParseResult<uint, byte>.Failure(
                new ParseError(input.Position, ErrorMessage.EndOfInput()),
                input);
        }, "VariableLengthQuantity");

    /// <summary>
    /// NULL終端の文字列をパースします
    /// </summary>
    public static Parser<string, byte> NullTerminatedString(System.Text.Encoding? encoding = null) =>
        new((input, ctx) =>
        {
            encoding ??= System.Text.Encoding.ASCII;
            var bytes = new List<byte>();
            var current = input;

            while (!current.IsAtEnd && current.Current != 0)
            {
                bytes.Add(current.Current);
                current = current.Advance();
            }

            if (!current.IsAtEnd)
            {
                // NULL文字をスキップ
                current = current.Advance();
            }

            try
            {
                var str = encoding.GetString(bytes.ToArray());
                return ParseResult<string, byte>.Success(str, current);
            }
            catch
            {
                return ParseResult<string, byte>.Failure(
                    new ParseError(input.Position, ErrorMessage.Message("無効な文字列エンコーディング")),
                    input);
            }
        }, "NullTerminatedString");

    /// <summary>
    /// 固定長の文字列をパースします
    /// </summary>
    public static Parser<string, byte> FixedLengthString(int length, System.Text.Encoding? encoding = null) =>
        from bytes in Take(length)
        select (encoding ?? System.Text.Encoding.ASCII).GetString(bytes).TrimEnd('\0');

    /// <summary>
    /// 32ビット浮動小数点数をパースします（ビッグエンディアン）
    /// </summary>
    public static Parser<float, byte> Float32BE =>
        from bytes in Take(4)
        select BitConverter.IsLittleEndian
            ? BitConverter.ToSingle(new[] { bytes[3], bytes[2], bytes[1], bytes[0] }, 0)
            : BitConverter.ToSingle(bytes, 0);

    /// <summary>
    /// 32ビット浮動小数点数をパースします（リトルエンディアン）
    /// </summary>
    public static Parser<float, byte> Float32LE =>
        from bytes in Take(4)
        select BitConverter.IsLittleEndian
            ? BitConverter.ToSingle(bytes, 0)
            : BitConverter.ToSingle(new[] { bytes[3], bytes[2], bytes[1], bytes[0] }, 0);

    /// <summary>
    /// 64ビット浮動小数点数をパースします（ビッグエンディアン）
    /// </summary>
    public static Parser<double, byte> Float64BE =>
        from bytes in Take(8)
        select BitConverter.IsLittleEndian
            ? BitConverter.ToDouble(bytes.Reverse().ToArray(), 0)
            : BitConverter.ToDouble(bytes, 0);

    /// <summary>
    /// 64ビット浮動小数点数をパースします（リトルエンディアン）
    /// </summary>
    public static Parser<double, byte> Float64LE =>
        from bytes in Take(8)
        select BitConverter.IsLittleEndian
            ? BitConverter.ToDouble(bytes, 0)
            : BitConverter.ToDouble(bytes.Reverse().ToArray(), 0);
}
