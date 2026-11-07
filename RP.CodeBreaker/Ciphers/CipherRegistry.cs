using System.Globalization;

namespace RP.CodeBreaker.Ciphers;

/// <summary>
/// A cipher that can be built from a key typed as text — what a converter page or a command line needs.
/// </summary>
/// <param name="Name">The cipher's name.</param>
/// <param name="KeyFormat">How to write the key.</param>
/// <param name="ExampleKey">A key that works.</param>
/// <param name="Create">Build the cipher from a typed key (throws with a helpful message on a bad key).</param>
public sealed record CipherDefinition(string Name, string KeyFormat, string ExampleKey, Func<string, ICipher> Create);

/// <summary>Every cipher in the library, buildable from a typed key.</summary>
public static class CipherRegistry
{
    /// <summary>All the cipher definitions.</summary>
    public static IReadOnlyList<CipherDefinition> All { get; } =
    [
        new("Caesar", "a shift 0–25, or the letter A becomes", "3", k => new CaesarCipher(ParseShift(k))),
        new("ROT13", "no key", string.Empty, _ => CaesarCipher.Rot13),
        new("Atbash", "no key", string.Empty, _ => new AtbashCipher()),
        new("Affine", "a, b (a odd and not 13)", "5, 8", k =>
        {
            int[] n = Numbers(k, 2);
            return new AffineCipher(n[0], n[1]);
        }),
        new("Simple substitution", "a keyword, or all 26 letters of the cipher alphabet", "ZEBRAS", k =>
            Letters.Normalise(k).Length == 26 && Letters.Normalise(k).Distinct().Count() == 26 ? new SubstitutionCipher(k) : SubstitutionCipher.FromKeyword(k)),
        new("Vigenère", "a keyword", "LEMON", k => new VigenereCipher(k)),
        new("Keyed Vigenère (Quagga)", "key, alphabet keyword", "PALIMPSEST, KRYPTOS", k =>
        {
            string[] parts = Parts(k, 2);
            return new VigenereCipher(parts[0], parts[1]);
        }),
        new("Beaufort", "a keyword", "FORTIFICATION", k => new BeaufortCipher(k)),
        new("Autokey", "a primer keyword", "QUEEN", k => new AutokeyCipher(k)),
        new("One-time pad", "pad letters at least as long as the message", "XMCKLQPZTRBWVNAEFJHUGYDSOI", k => new OneTimePad(k)),
        new("Rail fence", "number of rails", "3", k => new RailFenceCipher(Numbers(k, 1)[0])),
        new("Columnar transposition", "a keyword", "ZEBRAS", k => new ColumnarTranspositionCipher(k)),
        new("Polybius square", "a keyword (or blank)", "", k => new PolybiusCipher(k)),
        new("Playfair", "a keyword", "PLAYFAIREXAMPLE", k => new PlayfairCipher(k)),
        new("Bifid", "keyword, period", "DELASTELLE, 5", k =>
        {
            string[] parts = Parts(k, 1);
            return new BifidCipher(parts[0], parts.Length > 1 ? Numbers(parts[1], 1)[0] : 5);
        }),
        new("Hill (2×2)", "four numbers a b c d for [[a, b], [c, d]]", "3 3 2 5", k =>
        {
            int[] n = Numbers(k, 4);
            return new HillCipher(new[,] { { n[0], n[1] }, { n[2], n[3] } });
        }),
        new("ADFGVX", "square keyword, transposition keyword", "PRIVACY, GERMAN", k =>
        {
            string[] parts = Parts(k, 2);
            return new AdfgvxCipher(parts[0], parts[1]);
        }),
        new("Enigma", "rotors | reflector | rings | start | plugs", "I II III | B | AAA | AAA | AB CD EF", k =>
        {
            string[] p = k.Split('|').Select(x => x.Trim()).ToArray();
            if (p.Length < 4)
            {
                throw new ArgumentException("Write the Enigma key as: rotors | reflector | rings | start | plugs, e.g. 'I II III | B | AAA | AAA | AB CD'.");
            }

            return new EnigmaMachine(EnigmaSettings.Parse(p[0], p[1], p[2], p[3], p.Length > 4 ? p[4] : string.Empty));
        }),
    ];

    /// <summary>Find a cipher definition by name (case-insensitive).</summary>
    public static CipherDefinition Named(string name) =>
        All.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"No cipher called '{name}'.", nameof(name));

    private static int ParseShift(string key)
    {
        string k = key.Trim();
        if (int.TryParse(k, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
        {
            return n;
        }

        string letters = Letters.Normalise(k);
        return letters.Length == 1 ? letters[0] - 'A' : throw new ArgumentException("A Caesar key is a number 0–25 or a single letter.");
    }

    private static int[] Numbers(string key, int count)
    {
        int[] n = key.Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : throw new ArgumentException($"'{s}' is not a whole number."))
            .ToArray();
        return n.Length >= count ? n : throw new ArgumentException($"This key needs {count} number(s).");
    }

    private static string[] Parts(string key, int count)
    {
        string[] p = key.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
        return p.Length >= count ? p : throw new ArgumentException($"This key needs {count} part(s) separated by commas.");
    }
}
