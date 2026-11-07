using System.Text;

namespace RP.CodeBreaker.Ciphers;

/// <summary>
/// International Morse code — included because it is a <b>code</b>, not a cipher, and the difference
/// matters.
/// </summary>
/// <remarks>
/// <para>
/// A <i>cipher</i> transforms letters by a rule and a secret key; anyone who knows the key can
/// reverse it. A <i>code</i> replaces whole symbols (or words, or phrases) by others according to a
/// table. Morse's table is public, so it hides nothing — it exists to send letters over a wire or
/// radio. Secret codes keep the table secret: a codebook that turns ATTACK into 4071 and DAWN into
/// 2216. Breaking those is reconstructing the book, word by word, from context — the work Room 40 did
/// on the Zimmermann telegram in 1917. Enigma traffic at Bletchley was intercepted as Morse, then
/// deciphered.
/// </para>
/// </remarks>
public static class MorseCode
{
    private static readonly Dictionary<char, string> ToMorse = new()
    {
        ['A'] = ".-", ['B'] = "-...", ['C'] = "-.-.", ['D'] = "-..", ['E'] = ".", ['F'] = "..-.",
        ['G'] = "--.", ['H'] = "....", ['I'] = "..", ['J'] = ".---", ['K'] = "-.-", ['L'] = ".-..",
        ['M'] = "--", ['N'] = "-.", ['O'] = "---", ['P'] = ".--.", ['Q'] = "--.-", ['R'] = ".-.",
        ['S'] = "...", ['T'] = "-", ['U'] = "..-", ['V'] = "...-", ['W'] = ".--", ['X'] = "-..-",
        ['Y'] = "-.--", ['Z'] = "--..",
        ['0'] = "-----", ['1'] = ".----", ['2'] = "..---", ['3'] = "...--", ['4'] = "....-",
        ['5'] = ".....", ['6'] = "-....", ['7'] = "--...", ['8'] = "---..", ['9'] = "----.",
    };

    private static readonly Dictionary<string, char> FromMorse = ToMorse.ToDictionary(kv => kv.Value, kv => kv.Key);

    /// <summary>Encode text as Morse: letters separated by spaces, words by " / ".</summary>
    public static string Encode(string text)
    {
        var words = text.ToUpperInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" / ", words.Select(w => string.Join(" ", w.Where(ToMorse.ContainsKey).Select(c => ToMorse[c]))).Where(w => w.Length > 0));
    }

    /// <summary>Decode Morse written with dots, dashes, spaces between letters and "/" between words.</summary>
    public static string Decode(string morse)
    {
        var sb = new StringBuilder();
        foreach (string word in morse.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (sb.Length > 0)
            {
                sb.Append(' ');
            }

            foreach (string symbol in word.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                sb.Append(FromMorse.TryGetValue(symbol, out char c) ? c : '?');
            }
        }

        return sb.ToString();
    }
}
