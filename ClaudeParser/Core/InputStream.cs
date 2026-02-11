namespace ClaudeParser.Core;

/// <summary>
/// パーサーへの入力を抽象化するインターフェース。
/// 文字列、バイト列、その他のストリームを統一的に扱えるようにします。
/// </summary>
/// <typeparam name="TToken">入力の要素型（charやbyteなど）</typeparam>
public interface IInputStream<TToken> : IEquatable<IInputStream<TToken>>
{
    /// <summary>
    /// 現在の位置
    /// </summary>
    Position Position { get; }
    
    /// <summary>
    /// 入力の終端かどうか
    /// </summary>
    bool IsAtEnd { get; }
    
    /// <summary>
    /// 現在のトークンを取得します（終端の場合はdefault）
    /// </summary>
    TToken? Current { get; }
    
    /// <summary>
    /// 現在のトークンを消費して次に進め、新しいストリームを返します。
    /// この操作はイミュータブルです。
    /// </summary>
    IInputStream<TToken> Advance();
    
    /// <summary>
    /// 指定した位置までの入力を文字列として取得します（デバッグ用）
    /// </summary>
    string GetContext(int maxLength = 20);
}

/// <summary>
/// 文字列入力ストリーム
/// </summary>
public class StringInputStream : IInputStream<char>
{
    private readonly string _source;
    private readonly int _index;
    
    public Position Position { get; }
    
    public bool IsAtEnd => _index >= _source.Length;
    
    public char Current => IsAtEnd ? '\0' : _source[_index];

    public StringInputStream(string source, string sourceName = "<input>")
        : this(source, 0, Position.Initial(sourceName)) { }

    private StringInputStream(string source, int index, Position position)
    {
        _source = source;
        _index = index;
        Position = position;
    }

    public IInputStream<char> Advance()
    {
        if (IsAtEnd)
            return this;
        return new StringInputStream(_source, _index + 1, Position.Advance(Current));
    }

    public string GetContext(int maxLength = 20)
    {
        if (IsAtEnd)
            return "<EOF>";
        
        var remaining = _source.Length - _index;
        var length = Math.Min(remaining, maxLength);
        var text = _source.Substring(_index, length);
        
        if (remaining > maxLength)
            text += "...";
        
        return text.Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
    }

    /// <summary>
    /// 元のソース文字列を取得します（エラー表示用）
    /// </summary>
    public string SourceText => _source;

    public bool Equals(IInputStream<char>? other)
    {
        if (other is not StringInputStream s) return false;
        return ReferenceEquals(_source, s._source) && _index == s._index;
    }

    public override bool Equals(object? obj) => Equals(obj as IInputStream<char>);
    public override int GetHashCode() => HashCode.Combine(_source.GetHashCode(), _index);
}

/// <summary>
/// バイト配列入力ストリーム
/// </summary>
public class ByteInputStream : IInputStream<byte>
{
    private readonly byte[] _data;
    private readonly int _index;
    
    public Position Position { get; }
    
    public bool IsAtEnd => _index >= _data.Length;
    
    public byte Current => IsAtEnd ? (byte)0 : _data[_index];

    public ByteInputStream(byte[] data, string sourceName = "<binary>")
        : this(data, 0, Position.Initial(sourceName)) { }

    private ByteInputStream(byte[] data, int index, Position position)
    {
        _data = data;
        _index = index;
        Position = position;
    }

    public IInputStream<byte> Advance()
    {
        if (IsAtEnd)
            return this;
        return new ByteInputStream(_data, _index + 1, Position.AdvanceBytes(1));
    }
    
    /// <summary>
    /// 複数バイトを一度に進めます。
    /// </summary>
    public ByteInputStream AdvanceBy(int count)
    {
        var newIndex = Math.Min(_index + count, _data.Length);
        return new ByteInputStream(_data, newIndex, Position.AdvanceBytes(newIndex - _index));
    }
    
    /// <summary>
    /// 現在位置から指定バイト数を取得します。
    /// </summary>
    public byte[] GetBytes(int count)
    {
        var available = Math.Min(count, _data.Length - _index);
        var result = new byte[available];
        Array.Copy(_data, _index, result, 0, available);
        return result;
    }

    public string GetContext(int maxLength = 20)
    {
        if (IsAtEnd)
            return "<EOF>";
        
        var remaining = _data.Length - _index;
        var length = Math.Min(remaining, maxLength);
        var bytes = GetBytes(length);
        var hex = BitConverter.ToString(bytes).Replace("-", " ");
        
        if (remaining > maxLength)
            hex += " ...";
        
        return hex;
    }

    public bool Equals(IInputStream<byte>? other)
    {
        if (other is not ByteInputStream b) return false;
        return ReferenceEquals(_data, b._data) && _index == b._index;
    }

    public override bool Equals(object? obj) => Equals(obj as IInputStream<byte>);
    public override int GetHashCode() => HashCode.Combine(_data.GetHashCode(), _index);
}

/// <summary>
/// リスト入力ストリーム（任意の型のトークンリスト用）
/// </summary>
public class ListInputStream<T> : IInputStream<T>
{
    private readonly IReadOnlyList<T> _tokens;
    private readonly int _index;
    private readonly Func<T, Position, Position> _advancePosition;
    
    public Position Position { get; }
    
    public bool IsAtEnd => _index >= _tokens.Count;
    
    public T? Current => IsAtEnd ? default : _tokens[_index];

    public ListInputStream(
        IReadOnlyList<T> tokens, 
        string sourceName = "<tokens>",
        Func<T, Position, Position>? advancePosition = null)
        : this(tokens, 0, Position.Initial(sourceName), 
               advancePosition ?? ((_, pos) => pos.AdvanceBytes(1))) { }

    private ListInputStream(
        IReadOnlyList<T> tokens, 
        int index, 
        Position position,
        Func<T, Position, Position> advancePosition)
    {
        _tokens = tokens;
        _index = index;
        Position = position;
        _advancePosition = advancePosition;
    }

    public IInputStream<T> Advance()
    {
        if (IsAtEnd)
            return this;
        var newPos = _advancePosition(Current!, Position);
        return new ListInputStream<T>(_tokens, _index + 1, newPos, _advancePosition);
    }

    public string GetContext(int maxLength = 20)
    {
        if (IsAtEnd)
            return "<EOF>";
        
        var remaining = _tokens.Count - _index;
        var length = Math.Min(remaining, maxLength);
        var items = _tokens.Skip(_index).Take(length).Select(t => t?.ToString() ?? "null");
        var text = string.Join(", ", items);
        
        if (remaining > maxLength)
            text += ", ...";
        
        return $"[{text}]";
    }

    public bool Equals(IInputStream<T>? other)
    {
        if (other is not ListInputStream<T> l) return false;
        return ReferenceEquals(_tokens, l._tokens) && _index == l._index;
    }

    public override bool Equals(object? obj) => Equals(obj as IInputStream<T>);
    public override int GetHashCode() => HashCode.Combine(_tokens.GetHashCode(), _index);
}
