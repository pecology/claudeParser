namespace ClaudeParser.Core;

/// <summary>
/// パース中の位置情報を表します。
/// テキストの場合は行・列情報を、バイナリの場合はオフセットを保持します。
/// </summary>
public readonly struct Position : IEquatable<Position>, IComparable<Position>
{
    /// <summary>
    /// ストリーム内のオフセット（0から開始）
    /// </summary>
    public long Offset { get; }
    
    /// <summary>
    /// 行番号（1から開始、テキストの場合のみ有効）
    /// </summary>
    public int Line { get; }
    
    /// <summary>
    /// 列番号（1から開始、テキストの場合のみ有効）
    /// </summary>
    public int Column { get; }
    
    /// <summary>
    /// ソース名（ファイル名など）
    /// </summary>
    public string SourceName { get; }

    public Position(long offset, int line, int column, string sourceName = "<unknown>")
    {
        Offset = offset;
        Line = line;
        Column = column;
        SourceName = sourceName;
    }

    /// <summary>
    /// 初期位置を作成します。
    /// </summary>
    public static Position Initial(string sourceName = "<unknown>") => 
        new(0, 1, 1, sourceName);

    /// <summary>
    /// 次の文字位置に進めます（改行を考慮）。
    /// </summary>
    public Position Advance(char c)
    {
        if (c == '\n')
            return new Position(Offset + 1, Line + 1, 1, SourceName);
        return new Position(Offset + 1, Line, Column + 1, SourceName);
    }

    /// <summary>
    /// バイナリ用：指定バイト数進めます。
    /// </summary>
    public Position AdvanceBytes(int count) =>
        new(Offset + count, Line, Column + count, SourceName);

    public override string ToString() => 
        $"{SourceName}:{Line}:{Column}";

    public string ToDetailedString() =>
        $"{SourceName} (行 {Line}, 列 {Column}, オフセット {Offset})";

    public bool Equals(Position other) =>
        Offset == other.Offset && 
        Line == other.Line && 
        Column == other.Column && 
        SourceName == other.SourceName;

    public override bool Equals(object? obj) =>
        obj is Position other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(Offset, Line, Column, SourceName);

    public int CompareTo(Position other) =>
        Offset.CompareTo(other.Offset);

    public static bool operator ==(Position left, Position right) => left.Equals(right);
    public static bool operator !=(Position left, Position right) => !left.Equals(right);
    public static bool operator <(Position left, Position right) => left.CompareTo(right) < 0;
    public static bool operator >(Position left, Position right) => left.CompareTo(right) > 0;
    public static bool operator <=(Position left, Position right) => left.CompareTo(right) <= 0;
    public static bool operator >=(Position left, Position right) => left.CompareTo(right) >= 0;
}
