using ClaudeParser.Core;
using ClaudeParser.Combinators;

namespace ClaudeParser.Examples;

/// <summary>
/// MIDI ファイル (SMF: Standard MIDI File) パーサー
/// バイナリデータのパース例として実装しています。
/// </summary>
public static class MidiParser
{
    #region MIDIデータ構造

    /// <summary>
    /// MIDIファイル全体を表します
    /// </summary>
    public record MidiFile(MidiHeader Header, IReadOnlyList<MidiTrack> Tracks)
    {
        public override string ToString() =>
            $"MidiFile(Format={Header.Format}, Tracks={Tracks.Count}, Division={Header.Division})";
    }

    /// <summary>
    /// MIDIヘッダーチャンク
    /// </summary>
    public record MidiHeader(int Format, int TrackCount, int Division)
    {
        /// <summary>
        /// フォーマット: 0=単一トラック, 1=複数トラック同時再生, 2=複数トラック独立
        /// </summary>
        public int Format { get; } = Format;
        
        /// <summary>
        /// トラック数
        /// </summary>
        public int TrackCount { get; } = TrackCount;
        
        /// <summary>
        /// 時間分解能（正: ティック/四分音符、負: SMPTE形式）
        /// </summary>
        public int Division { get; } = Division;
    }

    /// <summary>
    /// MIDIトラック
    /// </summary>
    public record MidiTrack(IReadOnlyList<MidiEvent> Events)
    {
        public override string ToString() =>
            $"MidiTrack(Events={Events.Count})";
    }

    /// <summary>
    /// MIDIイベントの基底クラス
    /// </summary>
    public abstract record MidiEvent(uint DeltaTime)
    {
        /// <summary>
        /// 前のイベントからの経過時間（ティック）
        /// </summary>
        public uint DeltaTime { get; } = DeltaTime;
    }

    /// <summary>
    /// ノートオフイベント
    /// </summary>
    public record NoteOffEvent(uint DeltaTime, byte Channel, byte Note, byte Velocity) 
        : MidiEvent(DeltaTime)
    {
        public override string ToString() =>
            $"NoteOff(ch={Channel}, note={Note}, vel={Velocity}, dt={DeltaTime})";
    }

    /// <summary>
    /// ノートオンイベント
    /// </summary>
    public record NoteOnEvent(uint DeltaTime, byte Channel, byte Note, byte Velocity) 
        : MidiEvent(DeltaTime)
    {
        public override string ToString() =>
            $"NoteOn(ch={Channel}, note={Note}, vel={Velocity}, dt={DeltaTime})";
    }

    /// <summary>
    /// 後鍵タッチ（ポリフォニックキープレッシャー）
    /// </summary>
    public record PolyphonicKeyPressureEvent(uint DeltaTime, byte Channel, byte Note, byte Pressure) 
        : MidiEvent(DeltaTime);

    /// <summary>
    /// コントロールチェンジ
    /// </summary>
    public record ControlChangeEvent(uint DeltaTime, byte Channel, byte Controller, byte Value) 
        : MidiEvent(DeltaTime)
    {
        public override string ToString() =>
            $"ControlChange(ch={Channel}, cc={Controller}, val={Value}, dt={DeltaTime})";
    }

    /// <summary>
    /// プログラムチェンジ（楽器変更）
    /// </summary>
    public record ProgramChangeEvent(uint DeltaTime, byte Channel, byte Program) 
        : MidiEvent(DeltaTime)
    {
        public override string ToString() =>
            $"ProgramChange(ch={Channel}, prog={Program}, dt={DeltaTime})";
    }

    /// <summary>
    /// チャンネルプレッシャー（アフタータッチ）
    /// </summary>
    public record ChannelPressureEvent(uint DeltaTime, byte Channel, byte Pressure) 
        : MidiEvent(DeltaTime);

    /// <summary>
    /// ピッチベンド
    /// </summary>
    public record PitchBendEvent(uint DeltaTime, byte Channel, int Value) 
        : MidiEvent(DeltaTime)
    {
        public override string ToString() =>
            $"PitchBend(ch={Channel}, val={Value}, dt={DeltaTime})";
    }

    /// <summary>
    /// システムエクスクルーシブ
    /// </summary>
    public record SysExEvent(uint DeltaTime, byte[] Data) 
        : MidiEvent(DeltaTime)
    {
        public override string ToString() =>
            $"SysEx(len={Data.Length}, dt={DeltaTime})";
    }

    /// <summary>
    /// メタイベント
    /// </summary>
    public record MetaEvent(uint DeltaTime, byte Type, byte[] Data) 
        : MidiEvent(DeltaTime)
    {
        public override string ToString() =>
            $"Meta(type=0x{Type:X2}, len={Data.Length}, dt={DeltaTime})";
    }

    /// <summary>
    /// テンポ設定メタイベント
    /// </summary>
    public record TempoEvent(uint DeltaTime, int MicrosecondsPerBeat) 
        : MidiEvent(DeltaTime)
    {
        public double Bpm => 60_000_000.0 / MicrosecondsPerBeat;
        public override string ToString() =>
            $"Tempo(bpm={Bpm:F2}, dt={DeltaTime})";
    }

    /// <summary>
    /// 拍子記号メタイベント
    /// </summary>
    public record TimeSignatureEvent(uint DeltaTime, byte Numerator, byte DenominatorPower, byte ClocksPerClick, byte NotesPerQuarter)
        : MidiEvent(DeltaTime)
    {
        public int Denominator => 1 << DenominatorPower;
        public override string ToString() =>
            $"TimeSignature({Numerator}/{Denominator}, dt={DeltaTime})";
    }

    /// <summary>
    /// 調号メタイベント
    /// </summary>
    public record KeySignatureEvent(uint DeltaTime, sbyte Key, bool IsMinor) 
        : MidiEvent(DeltaTime)
    {
        public override string ToString() =>
            $"KeySignature(key={Key}, minor={IsMinor}, dt={DeltaTime})";
    }

    /// <summary>
    /// テキストメタイベント
    /// </summary>
    public record TextEvent(uint DeltaTime, byte Type, string Text) 
        : MidiEvent(DeltaTime)
    {
        public string TypeName => Type switch
        {
            0x01 => "Text",
            0x02 => "Copyright",
            0x03 => "TrackName",
            0x04 => "InstrumentName",
            0x05 => "Lyric",
            0x06 => "Marker",
            0x07 => "CuePoint",
            _ => $"Text(0x{Type:X2})"
        };
        public override string ToString() =>
            $"{TypeName}(\"{Text}\", dt={DeltaTime})";
    }

    /// <summary>
    /// トラック終端メタイベント
    /// </summary>
    public record EndOfTrackEvent(uint DeltaTime) 
        : MidiEvent(DeltaTime)
    {
        public override string ToString() => $"EndOfTrack(dt={DeltaTime})";
    }

    /// <summary>
    /// 不明なイベント
    /// </summary>
    public record UnknownEvent(uint DeltaTime, byte Status, byte[] Data) 
        : MidiEvent(DeltaTime)
    {
        public override string ToString() =>
            $"Unknown(status=0x{Status:X2}, len={Data.Length}, dt={DeltaTime})";
    }

    #endregion

    #region パーサー定義

    /// <summary>
    /// ヘッダーチャンク "MThd"
    /// </summary>
    private static Parser<MidiHeader, byte> HeaderChunk =>
        (from magic in BinaryParsers.Bytes(0x4D, 0x54, 0x68, 0x64).WithExpected("MThd")  // "MThd"
         from length in BinaryParsers.UInt32BE
         from format in BinaryParsers.UInt16BE
         from tracks in BinaryParsers.UInt16BE
         from division in BinaryParsers.UInt16BE
         from _ in BinaryParsers.Take((int)length - 6).OptionalOr(Array.Empty<byte>()) // 追加データをスキップ
         select new MidiHeader(format, tracks, division))
        .Named("HeaderChunk")
        .WithContext("MIDIヘッダー");

    /// <summary>
    /// トラックチャンク "MTrk"
    /// </summary>
    private static Parser<MidiTrack, byte> TrackChunk =>
        (from magic in BinaryParsers.Bytes(0x4D, 0x54, 0x72, 0x6B).WithExpected("MTrk")  // "MTrk"
         from length in BinaryParsers.UInt32BE
         from events in TrackEvents((int)length)
         select new MidiTrack(events))
        .Named("TrackChunk")
        .WithContext("MIDIトラック");

    /// <summary>
    /// トラック内のイベント列をパースします
    /// </summary>
    private static Parser<IReadOnlyList<MidiEvent>, byte> TrackEvents(int length)
    {
        return new Parser<IReadOnlyList<MidiEvent>, byte>((input, ctx) =>
        {
            var events = new List<MidiEvent>();
            var current = input;
            var startOffset = current.Position.Offset;
            byte runningStatus = 0;

            while (current.Position.Offset - startOffset < length && !current.IsAtEnd)
            {
                // デルタタイム
                var deltaResult = BinaryParsers.VariableLengthQuantity.Parse(current, ctx);
                if (deltaResult is not SuccessResult<uint, byte> deltaSuccess)
                    return ((FailureResult<uint, byte>)deltaResult).Cast<IReadOnlyList<MidiEvent>>();
                
                var deltaTime = deltaSuccess.Value;
                current = deltaSuccess.Remaining;

                // イベント種別
                var eventResult = ParseEvent(deltaTime, ref runningStatus).Parse(current, ctx);
                if (eventResult is not SuccessResult<MidiEvent, byte> eventSuccess)
                    return ((FailureResult<MidiEvent, byte>)eventResult).Cast<IReadOnlyList<MidiEvent>>();

                events.Add(eventSuccess.Value);
                current = eventSuccess.Remaining;

                // End of Track で終了
                if (eventSuccess.Value is EndOfTrackEvent)
                    break;
            }

            return ParseResult<IReadOnlyList<MidiEvent>, byte>.Success(events, current);
        }, "TrackEvents");
    }

    /// <summary>
    /// 単一イベントをパースします
    /// </summary>
    private static Parser<MidiEvent, byte> ParseEvent(uint deltaTime, ref byte runningStatus)
    {
        var rs = runningStatus;
        return new Parser<MidiEvent, byte>((input, ctx) =>
        {
            if (input.IsAtEnd)
                return ParseResult<MidiEvent, byte>.Failure(
                    new ParseError(input.Position, ErrorMessage.EndOfInput()), input);

            var status = input.Current;
            var current = input;

            // ランニングステータス対応
            if ((status & 0x80) != 0)
            {
                // 新しいステータス
                current = current.Advance();
            }
            else
            {
                // ランニングステータスを使用
                status = rs;
            }

            // MIDIチャンネルメッセージ
            if (status >= 0x80 && status < 0xF0)
            {
                rs = status;
                var channel = (byte)(status & 0x0F);
                var type = (byte)(status & 0xF0);

                return type switch
                {
                    0x80 => ParseNoteOff(deltaTime, channel).Parse(current, ctx),
                    0x90 => ParseNoteOn(deltaTime, channel).Parse(current, ctx),
                    0xA0 => ParsePolyKeyPressure(deltaTime, channel).Parse(current, ctx),
                    0xB0 => ParseControlChange(deltaTime, channel).Parse(current, ctx),
                    0xC0 => ParseProgramChange(deltaTime, channel).Parse(current, ctx),
                    0xD0 => ParseChannelPressure(deltaTime, channel).Parse(current, ctx),
                    0xE0 => ParsePitchBend(deltaTime, channel).Parse(current, ctx),
                    _ => ParseResult<MidiEvent, byte>.Failure(
                        new ParseError(current.Position, ErrorMessage.Unexpected($"ステータス 0x{status:X2}")), current)
                };
            }

            // システムメッセージ
            return status switch
            {
                0xF0 => ParseSysEx(deltaTime, 0xF0).Parse(current, ctx),
                0xF7 => ParseSysEx(deltaTime, 0xF7).Parse(current, ctx),
                0xFF => ParseMeta(deltaTime).Parse(current, ctx),
                _ => ParseResult<MidiEvent, byte>.Failure(
                    new ParseError(current.Position, ErrorMessage.Unexpected($"ステータス 0x{status:X2}")), current)
            };
        }, "Event");
    }

    private static Parser<MidiEvent, byte> ParseNoteOff(uint deltaTime, byte channel) =>
        from note in BinaryParsers.UInt8
        from velocity in BinaryParsers.UInt8
        select (MidiEvent)new NoteOffEvent(deltaTime, channel, note, velocity);

    private static Parser<MidiEvent, byte> ParseNoteOn(uint deltaTime, byte channel) =>
        from note in BinaryParsers.UInt8
        from velocity in BinaryParsers.UInt8
        select velocity == 0
            ? (MidiEvent)new NoteOffEvent(deltaTime, channel, note, velocity)
            : new NoteOnEvent(deltaTime, channel, note, velocity);

    private static Parser<MidiEvent, byte> ParsePolyKeyPressure(uint deltaTime, byte channel) =>
        from note in BinaryParsers.UInt8
        from pressure in BinaryParsers.UInt8
        select (MidiEvent)new PolyphonicKeyPressureEvent(deltaTime, channel, note, pressure);

    private static Parser<MidiEvent, byte> ParseControlChange(uint deltaTime, byte channel) =>
        from controller in BinaryParsers.UInt8
        from value in BinaryParsers.UInt8
        select (MidiEvent)new ControlChangeEvent(deltaTime, channel, controller, value);

    private static Parser<MidiEvent, byte> ParseProgramChange(uint deltaTime, byte channel) =>
        from program in BinaryParsers.UInt8
        select (MidiEvent)new ProgramChangeEvent(deltaTime, channel, program);

    private static Parser<MidiEvent, byte> ParseChannelPressure(uint deltaTime, byte channel) =>
        from pressure in BinaryParsers.UInt8
        select (MidiEvent)new ChannelPressureEvent(deltaTime, channel, pressure);

    private static Parser<MidiEvent, byte> ParsePitchBend(uint deltaTime, byte channel) =>
        from lsb in BinaryParsers.UInt8
        from msb in BinaryParsers.UInt8
        select (MidiEvent)new PitchBendEvent(deltaTime, channel, ((msb << 7) | lsb) - 8192);

    private static Parser<MidiEvent, byte> ParseSysEx(uint deltaTime, byte status) =>
        from length in BinaryParsers.VariableLengthQuantity
        from data in BinaryParsers.Take((int)length)
        select (MidiEvent)new SysExEvent(deltaTime, data);

    private static Parser<MidiEvent, byte> ParseMeta(uint deltaTime) =>
        from type in BinaryParsers.UInt8
        from length in BinaryParsers.VariableLengthQuantity
        from data in BinaryParsers.Take((int)length)
        select CreateMetaEvent(deltaTime, type, data);

    private static MidiEvent CreateMetaEvent(uint deltaTime, byte type, byte[] data) => type switch
    {
        0x00 => new MetaEvent(deltaTime, type, data), // シーケンス番号
        >= 0x01 and <= 0x07 => new TextEvent(deltaTime, type, System.Text.Encoding.ASCII.GetString(data)),
        0x20 => new MetaEvent(deltaTime, type, data), // MIDIチャンネルプレフィックス
        0x21 => new MetaEvent(deltaTime, type, data), // MIDIポート
        0x2F => new EndOfTrackEvent(deltaTime),
        0x51 when data.Length >= 3 => new TempoEvent(deltaTime, (data[0] << 16) | (data[1] << 8) | data[2]),
        0x54 => new MetaEvent(deltaTime, type, data), // SMPTEオフセット
        0x58 when data.Length >= 4 => new TimeSignatureEvent(deltaTime, data[0], data[1], data[2], data[3]),
        0x59 when data.Length >= 2 => new KeySignatureEvent(deltaTime, (sbyte)data[0], data[1] != 0),
        0x7F => new MetaEvent(deltaTime, type, data), // シーケンサー固有
        _ => new MetaEvent(deltaTime, type, data)
    };

    /// <summary>
    /// MIDIファイル全体のパーサー
    /// </summary>
    public static Parser<MidiFile, byte> MidiFileParser =>
        (from header in HeaderChunk
         from tracks in TrackChunk.Count(header.TrackCount)
         select new MidiFile(header, tracks))
        .Named("MidiFile")
        .WithContext("MIDIファイル");

    #endregion

    /// <summary>
    /// MIDIファイルをパースします
    /// </summary>
    public static MidiFile Parse(byte[] data)
    {
        var input = new ByteInputStream(data, "<midi>");
        var result = MidiFileParser.Parse(input);
        return result.GetValueOrThrow();
    }

    /// <summary>
    /// MIDIファイルをパースします（ファイルパスから）
    /// </summary>
    public static MidiFile ParseFile(string path)
    {
        var data = File.ReadAllBytes(path);
        var input = new ByteInputStream(data, path);
        var result = MidiFileParser.Parse(input);
        return result.GetValueOrThrow();
    }

    /// <summary>
    /// MIDIファイルの内容を人間が読める形式で出力します
    /// </summary>
    public static string Dump(MidiFile midi)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"MIDI File: Format {midi.Header.Format}, {midi.Header.TrackCount} tracks, Division {midi.Header.Division}");
        sb.AppendLine();

        for (int i = 0; i < midi.Tracks.Count; i++)
        {
            var track = midi.Tracks[i];
            sb.AppendLine($"Track {i + 1} ({track.Events.Count} events):");
            
            uint absoluteTime = 0;
            foreach (var ev in track.Events)
            {
                absoluteTime += ev.DeltaTime;
                sb.AppendLine($"  {absoluteTime,8}: {ev}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
