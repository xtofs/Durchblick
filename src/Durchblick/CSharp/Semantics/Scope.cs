namespace Durchblick.CSharp.Semantics;

using Durchblick.Collections;

public sealed class Scope(Scope? parent = null)
{
    public Scope(Scope? parent, IEnumerable<Symbol>? symbols) : this(parent)
    {
        Symbols = symbols?.ToList() ?? [];
    }

    public Scope? Parent { get; } = parent;

    public List<Symbol> Symbols { get; } = [];

    [Obsolete("use `new Scope()` instead")]
    public static Scope Global() => new Scope();

    public void Add(Symbol symbol)
    {
        Symbols.Add(symbol);
    }

    public Symbol? Lookup(string name)
        => Symbols.FirstOrDefault(s => s.Name == name) ?? Parent?.Lookup(name);
}
