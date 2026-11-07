using System.Globalization;
using System.Text;

namespace RP.CodeBreaker.Codes;

/// <summary>
/// A code: a fixed table that turns symbols into other symbols, and back. Unlike a cipher there is no
/// key — the table itself is the whole secret (or, for Morse and ASCII, no secret at all).
/// </summary>
/// <remarks>
/// <para>
/// The distinction matters to a code breaker. A <b>cipher</b> applies a rule and a secret key to each
/// letter; learn the rule and the key and every message opens. A <b>code</b> works on whole symbols,
/// words or phrases using a table. Public codes (Morse, ASCII, Base64) exist to <i>carry</i> text over a
/// wire, a radio, a punched tape or an email — they hide nothing. Secret codes (a codebook) hide by
/// keeping the table private, and are broken by rebuilding the table from context, a group at a time.
/// </para>
/// </remarks>
public interface ICode
{
    /// <summary>The code's name.</summary>
    string Name { get; }

    /// <summary>What it is for, in a sentence.</summary>
    string Description { get; }

    /// <summary>Turn ordinary text into the code.</summary>
    string Encode(string text);

    /// <summary>Turn coded text back into ordinary text.</summary>
    string Decode(string coded);
}

/// <summary>International Morse code: dots and dashes for radio and telegraph.</summary>
public sealed class MorseCodeConverter : ICode
{
    /// <inheritdoc/>
    public string Name => "Morse code";

    /// <inheritdoc/>
    public string Description => "Dots and dashes; letters separated by spaces, words by '/'. How Enigma traffic reached Bletchley.";

    /// <inheritdoc/>
    public string Encode(string text) => Ciphers.MorseCode.Encode(text);

    /// <inheritdoc/>
    public string Decode(string coded) => Ciphers.MorseCode.Decode(coded);
}

/// <summary>The NATO / ICAO spelling alphabet: Alfa, Bravo, Charlie…</summary>
public sealed class NatoPhoneticCode : ICode
{
    private static readonly string[] Words =
    [
        "ALFA", "BRAVO", "CHARLIE", "DELTA", "ECHO", "FOXTROT", "GOLF", "HOTEL", "INDIA", "JULIETT", "KILO", "LIMA", "MIKE",
        "NOVEMBER", "OSCAR", "PAPA", "QUEBEC", "ROMEO", "SIERRA", "TANGO", "UNIFORM", "VICTOR", "WHISKEY", "XRAY", "YANKEE", "ZULU",
    ];

    /// <inheritdoc/>
    public string Name => "NATO phonetic alphabet";

    /// <inheritdoc/>
    public string Description => "A word for each letter, so letters survive a crackly voice line.";

    /// <inheritdoc/>
    public string Encode(string text) => string.Join(" ", Letters.Normalise(text).Select(c => Words[c - 'A']));

    /// <inheritdoc/>
    public string Decode(string coded) =>
        string.Concat(coded.ToUpperInvariant().Replace("-", string.Empty, StringComparison.Ordinal).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w is "ALPHA" ? 'A' : w is "JULIET" ? 'J' : Array.IndexOf(Words, w) is int i and >= 0 ? (char)('A' + i) : '?'));
}

/// <summary>A1Z26: each letter as its position in the alphabet, A = 1 … Z = 26.</summary>
public sealed class A1Z26Code : ICode
{
    /// <inheritdoc/>
    public string Name => "A1Z26 (letter numbers)";

    /// <inheritdoc/>
    public string Description => "A = 1 … Z = 26, numbers separated by '-' and words by spaces.";

    /// <inheritdoc/>
    public string Encode(string text) =>
        string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => string.Join("-", Letters.Normalise(w).Select(c => (c - 'A' + 1).ToString(CultureInfo.InvariantCulture))))
            .Where(w => w.Length > 0));

    /// <inheritdoc/>
    public string Decode(string coded) =>
        string.Join(" ", coded.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => string.Concat(w.Split(new[] { '-', ',', '.' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(n => int.TryParse(n, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) && v is >= 1 and <= 26 ? (char)('A' + v - 1) : '?'))));
}

/// <summary>ASCII (UTF-8) bytes written in binary, eight bits per character.</summary>
public sealed class BinaryCode : ICode
{
    /// <inheritdoc/>
    public string Name => "Binary (ASCII / UTF-8)";

    /// <inheritdoc/>
    public string Description => "Each byte of the text as eight 0s and 1s: how a computer stores letters.";

    /// <inheritdoc/>
    public string Encode(string text) => string.Join(" ", Encoding.UTF8.GetBytes(text).Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));

    /// <inheritdoc/>
    public string Decode(string coded)
    {
        string bits = new(coded.Where(c => c is '0' or '1').ToArray());
        var bytes = new List<byte>();
        for (int i = 0; i + 8 <= bits.Length; i += 8)
        {
            bytes.Add(Convert.ToByte(bits.Substring(i, 8), 2));
        }

        return Encoding.UTF8.GetString(bytes.ToArray());
    }
}

/// <summary>UTF-8 bytes in hexadecimal.</summary>
public sealed class HexCode : ICode
{
    /// <inheritdoc/>
    public string Name => "Hexadecimal";

    /// <inheritdoc/>
    public string Description => "Each byte as two hex digits 00–FF — the usual way to show ciphertext bytes.";

    /// <inheritdoc/>
    public string Encode(string text) => Convert.ToHexString(Encoding.UTF8.GetBytes(text));

    /// <inheritdoc/>
    public string Decode(string coded)
    {
        string hex = new(coded.Where(Uri.IsHexDigit).ToArray());
        return Encoding.UTF8.GetString(Convert.FromHexString(hex.Length % 2 == 0 ? hex : hex[..^1]));
    }
}

/// <summary>Base64: bytes as letters, digits, '+' and '/'.</summary>
public sealed class Base64Code : ICode
{
    /// <inheritdoc/>
    public string Name => "Base64";

    /// <inheritdoc/>
    public string Description => "Any bytes as 64 safe characters, for email and the web. Often mistaken for encryption; it is not.";

    /// <inheritdoc/>
    public string Encode(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));

    /// <inheritdoc/>
    public string Decode(string coded)
    {
        string s = new(coded.Where(c => !char.IsWhiteSpace(c)).ToArray());
        s = s.PadRight(s.Length + ((4 - (s.Length % 4)) % 4), '=');
        return Encoding.UTF8.GetString(Convert.FromBase64String(s));
    }
}

/// <summary>
/// Francis Bacon's biliteral code (1605): each letter as five A/B symbols. Bacon's point was
/// steganography — hide the As and Bs as two typefaces in an innocent text.
/// </summary>
public sealed class BaconianCode : ICode
{
    /// <inheritdoc/>
    public string Name => "Baconian (biliteral)";

    /// <inheritdoc/>
    public string Description => "Each letter as 5 As and Bs (a 5-bit binary code, 1605), made to hide inside innocent text.";

    /// <inheritdoc/>
    public string Encode(string text) =>
        string.Join(" ", Letters.Normalise(text).Select(c => Convert.ToString(c - 'A', 2).PadLeft(5, '0').Replace('0', 'A').Replace('1', 'B')));

    /// <inheritdoc/>
    public string Decode(string coded)
    {
        string ab = new(coded.ToUpperInvariant().Where(c => c is 'A' or 'B').ToArray());
        var sb = new StringBuilder();
        for (int i = 0; i + 5 <= ab.Length; i += 5)
        {
            int v = Convert.ToInt32(ab.Substring(i, 5).Replace('A', '0').Replace('B', '1'), 2);
            sb.Append(v < 26 ? (char)('A' + v) : '?');
        }

        return sb.ToString();
    }
}

/// <summary>
/// The prisoners' tap code: a 5 × 5 grid (K shares C's cell), each letter tapped as row then column.
/// Used by American prisoners of war in Vietnam.
/// </summary>
public sealed class TapCode : ICode
{
    private const string Grid = "ABCDEFGHIJLMNOPQRSTUVWXYZ";

    /// <inheritdoc/>
    public string Name => "Tap code";

    /// <inheritdoc/>
    public string Description => "Row and column taps on a 5×5 grid (K = C); '.' is a tap, letters split by spaces, words by '/'.";

    /// <inheritdoc/>
    public string Encode(string text) =>
        string.Join(" / ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => string.Join(" ", Letters.Normalise(w).Replace('K', 'C').Select(c =>
            {
                int i = Grid.IndexOf(c);
                return new string('.', (i / 5) + 1) + "," + new string('.', (i % 5) + 1);
            })))
            .Where(w => w.Length > 0));

    /// <inheritdoc/>
    public string Decode(string coded)
    {
        var sb = new StringBuilder();
        foreach (string word in coded.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (sb.Length > 0)
            {
                sb.Append(' ');
            }

            foreach (string letter in word.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = letter.Split(',');
                int r = parts[0].Count(c => c == '.'), col = parts.Length > 1 ? parts[1].Count(c => c == '.') : 0;
                sb.Append(r is >= 1 and <= 5 && col is >= 1 and <= 5 ? Grid[((r - 1) * 5) + col - 1] : '?');
            }
        }

        return sb.ToString();
    }
}

/// <summary>
/// The Baudot–Murray ITA2 teleprinter code: five bits per character, with "letters" and "figures"
/// shift characters. The code the Lorenz SZ42 (Tunny) machine enciphered, bit by bit, and Colossus was
/// built to attack.
/// </summary>
public sealed class Ita2Code : ICode
{
    // Index = the 5-bit value. ITA2 international letters and figures cases; '\0' = unused or a
    // national-use position, \u000F and \u000E mark the FIGS (27) and LTRS (31) shift codes.
    private static readonly string LettersTable = new(
    [
        '\0', 'E', '\n', 'A', ' ', 'S', 'I', 'U', '\r', 'D', 'R', 'J', 'N', 'F', 'C', 'K',
        'T', 'Z', 'L', 'W', 'H', 'Y', 'P', 'Q', 'O', 'B', 'G', '\u000F', 'M', 'X', 'V', '\u000E',
    ]);

    private static readonly string FiguresTable = new(
    [
        '\0', '3', '\n', '-', ' ', '\'', '8', '7', '\r', '\u0005', '4', '\u0007', ',', '\0', ':', '(',
        '5', '+', ')', '2', '\0', '6', '0', '1', '9', '?', '\0', '\u000F', '.', '/', '=', '\u000E',
    ]);
    private const int FigureShift = 27, LetterShift = 31;

    /// <inheritdoc/>
    public string Name => "ITA2 (Baudot–Murray teleprinter)";

    /// <inheritdoc/>
    public string Description => "Five bits per character with letter/figure shifts — the code inside every Lorenz (Tunny) message.";

    /// <inheritdoc/>
    public string Encode(string text)
    {
        var codes = new List<int>();
        bool figures = false;
        foreach (char raw in text.ToUpperInvariant())
        {
            char ch = raw == '\r' ? '\r' : raw;
            int l = LettersTable.IndexOf(ch), f = FiguresTable.IndexOf(ch);
            if (ch is ' ' or '\n' or '\r')
            {
                codes.Add(LettersTable.IndexOf(ch));
            }
            else if (l > 0 && l != FigureShift && l != LetterShift)
            {
                if (figures)
                {
                    codes.Add(LetterShift);
                    figures = false;
                }

                codes.Add(l);
            }
            else if (f > 0 && char.IsAscii(ch) && !char.IsControl(ch))
            {
                if (!figures)
                {
                    codes.Add(FigureShift);
                    figures = true;
                }

                codes.Add(f);
            }
        }

        return string.Join(" ", codes.Select(c => Convert.ToString(c, 2).PadLeft(5, '0')));
    }

    /// <inheritdoc/>
    public string Decode(string coded)
    {
        string bits = new(coded.Where(c => c is '0' or '1').ToArray());
        var sb = new StringBuilder();
        bool figures = false;
        for (int i = 0; i + 5 <= bits.Length; i += 5)
        {
            int v = Convert.ToInt32(bits.Substring(i, 5), 2);
            if (v == FigureShift)
            {
                figures = true;
            }
            else if (v == LetterShift)
            {
                figures = false;
            }
            else
            {
                char ch = (figures ? FiguresTable : LettersTable)[v];
                if (ch != '\0' && (!char.IsControl(ch) || ch == '\n'))
                {
                    sb.Append(ch);
                }
            }
        }

        return sb.ToString();
    }
}

/// <summary>
/// A secret codebook: whole words and phrases replaced by number groups (with spelling-out by letter
/// for anything not in the book). The way diplomatic and naval codes worked — the German code 13040
/// that carried the Zimmermann telegram, or the Royal Navy's code books.
/// </summary>
/// <remarks>
/// A codebook has no key in the cipher sense; the book is the secret, and its "key space" is every
/// possible book. It is broken the way Room 40 broke German codes: collect many messages, notice which
/// groups recur and where (a group at the start of every message is probably an address; one before
/// every number is probably "NUMBER"), and rebuild the book group by group from context — cribs again.
/// </remarks>
public sealed class Codebook : ICode
{
    private readonly Dictionary<string, string> encode;
    private readonly Dictionary<string, string> decode;

    /// <summary>Make a codebook from phrase → group entries (phrases in capitals; longest phrases matched first).</summary>
    public Codebook(IReadOnlyDictionary<string, string> entries)
    {
        this.encode = entries.ToDictionary(kv => kv.Key.ToUpperInvariant(), kv => kv.Value, StringComparer.Ordinal);
        this.decode = entries.ToDictionary(kv => kv.Value, kv => kv.Key.ToUpperInvariant(), StringComparer.Ordinal);
        for (int i = 0; i < 26; i++)
        {
            string group = $"9{i:00}";
            this.encode.TryAdd($"#{(char)('A' + i)}", group);
            this.decode.TryAdd(group, $"#{(char)('A' + i)}");
        }
    }

    /// <summary>A small example book in the style of a 1917 diplomatic code.</summary>
    public static Codebook Example { get; } = new(new Dictionary<string, string>
    {
        ["WE INTEND"] = "4458", ["TO BEGIN"] = "1172", ["UNRESTRICTED"] = "3013", ["SUBMARINE"] = "6929",
        ["WARFARE"] = "2005", ["ON THE"] = "0801", ["FIRST"] = "7552", ["OF"] = "0010", ["FEBRUARY"] = "5864",
        ["ALLIANCE"] = "1250", ["MEXICO"] = "8784", ["PROPOSE"] = "2163", ["TO"] = "0100", ["THE"] = "0001",
        ["PRESIDENT"] = "4213", ["SECRET"] = "1999", ["UNITED STATES"] = "6301", ["NEUTRAL"] = "3407",
        ["ATTACK"] = "4071", ["DAWN"] = "2216", ["AT"] = "0005", ["AND"] = "0002", ["STOP"] = "0000",
    });

    /// <inheritdoc/>
    public string Name => "Codebook";

    /// <inheritdoc/>
    public string Description => "Whole words and phrases replaced by 4-digit groups; unknown words spelled out with 9xx letter groups.";

    /// <summary>The phrases in the book.</summary>
    public IEnumerable<string> Phrases => this.encode.Keys.Where(k => !k.StartsWith('#'));

    /// <inheritdoc/>
    public string Encode(string text)
    {
        string[] words = Letters.Normalise(text).Length == 0 ? [] : text.ToUpperInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(Letters.Normalise).Where(w => w.Length > 0).ToArray();
        var groups = new List<string>();
        for (int i = 0; i < words.Length;)
        {
            bool matched = false;
            for (int len = Math.Min(4, words.Length - i); len >= 1 && !matched; len--)
            {
                string phrase = string.Join(" ", words, i, len);
                if (this.encode.TryGetValue(phrase, out string? group))
                {
                    groups.Add(group);
                    i += len;
                    matched = true;
                }
            }

            if (!matched)
            {
                groups.AddRange(words[i].Select(c => this.encode[$"#{c}"]));
                i++;
            }
        }

        return string.Join(" ", groups);
    }

    /// <inheritdoc/>
    public string Decode(string coded)
    {
        var sb = new StringBuilder();
        bool spelling = false;
        foreach (string group in coded.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            string meaning = this.decode.TryGetValue(group, out string? m) ? m : $"[{group}?]";
            if (meaning.StartsWith('#'))
            {
                if (!spelling && sb.Length > 0)
                {
                    sb.Append(' ');
                }

                sb.Append(meaning[1]);
                spelling = true;
                continue;
            }

            if (sb.Length > 0)
            {
                sb.Append(' ');
            }

            sb.Append(meaning);
            spelling = false;
        }

        return sb.ToString();
    }
}

/// <summary>Every code in the library, for converters and menus.</summary>
public static class CodeCatalogue
{
    /// <summary>All the codes.</summary>
    public static IReadOnlyList<ICode> All { get; } =
    [
        new MorseCodeConverter(), new NatoPhoneticCode(), new A1Z26Code(), new BaconianCode(), new TapCode(),
        new BinaryCode(), new HexCode(), new Base64Code(), new Ita2Code(), Codebook.Example,
    ];

    /// <summary>Find a code by name (case-insensitive).</summary>
    public static ICode Named(string name) =>
        All.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"No code called '{name}'.", nameof(name));
}
