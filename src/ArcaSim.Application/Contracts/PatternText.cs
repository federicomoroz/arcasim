using System.Text;

namespace ArcaSim.Application.Contracts;

/// <summary>
/// Writes the shortest plain text an XSD pattern accepts, for the codes the
/// agro and customs services shape by regex (a RENSPA such as 01.234.5.67890/AB,
/// a despacho de importación). Covers what ARCA's schemas use: literals,
/// escapes, classes, groups with alternatives and quantifiers.
/// </summary>
public static class PatternText
{
    public static string? For(string pattern)
    {
        try
        {
            var position = 0;
            var text = new StringBuilder();
            Alternatives(pattern, ref position, text);
            return position == pattern.Length ? text.ToString() : null;
        }
        catch (Exception ex) when (ex is ArgumentException or IndexOutOfRangeException or FormatException)
        {
            return null;
        }
    }

    /// <summary>Writes the first alternative and skips the rest, up to a closing parenthesis or the end.</summary>
    private static void Alternatives(string p, ref int i, StringBuilder text)
    {
        Sequence(p, ref i, text);
        while (i < p.Length && p[i] == '|')
        {
            i++;
            Sequence(p, ref i, new StringBuilder());
        }
    }

    private static void Sequence(string p, ref int i, StringBuilder text)
    {
        while (i < p.Length && p[i] is not ('|' or ')'))
        {
            var atom = new StringBuilder();
            Atom(p, ref i, atom);
            var times = Quantifier(p, ref i);
            for (var n = 0; n < times; n++) text.Append(atom);
        }
    }

    private static void Atom(string p, ref int i, StringBuilder text)
    {
        var c = p[i++];
        switch (c)
        {
            case '(':
                if (i < p.Length - 1 && p[i] == '?' && p[i + 1] == ':') i += 2;
                Alternatives(p, ref i, text);
                if (p[i++] != ')') throw new FormatException();
                break;
            case '[':
                text.Append(Class(p, ref i));
                break;
            case '\\':
                text.Append(Escape(p[i++]));
                break;
            case '.':
                text.Append('0');
                break;
            case '^' or '$':
                break;
            default:
                text.Append(c);
                break;
        }
    }

    /// <summary>The first character a class accepts; a negated class gets a letter that is rarely excluded.</summary>
    private static char Class(string p, ref int i)
    {
        var negated = p[i] == '^';
        if (negated) i++;
        char? first = null;
        var excluded = new HashSet<char>();
        while (p[i] != ']')
        {
            var c = p[i++];
            if (c == '\\') c = Escape(p[i++]);
            if (p[i] == '-' && p[i + 1] != ']')
            {
                var last = p[i + 1] == '\\' ? Escape(p[i + 2]) : p[i + 1];
                i += p[i + 1] == '\\' ? 3 : 2;
                for (var x = c; x <= last; x++) excluded.Add(x);
            }
            else
            {
                excluded.Add(c);
            }
            first ??= c;
        }
        i++;
        if (!negated) return first ?? 'A';
        return "A0x_".FirstOrDefault(x => !excluded.Contains(x), 'Z');
    }

    private static char Escape(char c) => c switch
    {
        'd' => '0',
        'w' or 'i' or 'c' => 'A',
        's' => ' ',
        'n' => '\n',
        't' => '\t',
        'D' or 'W' or 'S' => '-',
        _ => c,
    };

    /// <summary>How many times to repeat the atom: the smallest the quantifier allows, but at least once for '+'.</summary>
    private static int Quantifier(string p, ref int i)
    {
        if (i >= p.Length) return 1;
        switch (p[i])
        {
            case '?' or '*':
                i++;
                return 0;
            case '+':
                i++;
                return 1;
            case '{':
                var close = p.IndexOf('}', i);
                var bounds = p[(i + 1)..close].Split(',');
                i = close + 1;
                return int.Parse(bounds[0]);
            default:
                return 1;
        }
    }
}
