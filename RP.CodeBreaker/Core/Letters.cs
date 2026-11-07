using System.Text;

namespace RP.CodeBreaker;

/// <summary>
/// The 26-letter Latin alphabet and the small conversions every cipher and attack in this library
/// leans on: a letter to its number (A = 0 … Z = 25) and back, arithmetic "modulo 26", and the
/// clean-up that turns real text into the run of capital letters classical ciphers work on.
/// </summary>
/// <remarks>
/// Almost every hand cipher treats letters as numbers on a clock face with 26 hours. Adding 3 to
/// X (23) gives 26, which wraps round to 0, which is A. That wrap-around is "modulo 26", written
/// <c>mod 26</c>, and <see cref="Mod"/> does it correctly for negative numbers too (C#'s own
/// <c>%</c> operator does not: <c>-1 % 26</c> is <c>-1</c>, not <c>25</c>).
/// </remarks>
public static class Letters
{
    /// <summary>The 26 capital letters, in order.</summary>
    public const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>The number of letters in <see cref="Alphabet"/>.</summary>
    public const int Count = 26;

    /// <summary>True for A–Z and a–z only (accented letters are not part of the classical alphabet).</summary>
    public static bool IsLetter(char c) => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');

    /// <summary>The number of a letter: A (or a) = 0 … Z = 25. Throws for anything else.</summary>
    public static int ToNumber(char c)
    {
        if (c >= 'A' && c <= 'Z')
        {
            return c - 'A';
        }

        if (c >= 'a' && c <= 'z')
        {
            return c - 'a';
        }

        throw new ArgumentOutOfRangeException(nameof(c), $"'{c}' is not a letter A–Z.");
    }

    /// <summary>The capital letter for a number, wrapping any integer onto 0–25 first.</summary>
    public static char ToLetter(int n) => (char)('A' + Mod(n, Count));

    /// <summary>
    /// The true mathematical remainder: always in 0 … m-1, even when <paramref name="n"/> is
    /// negative.
    /// </summary>
    public static int Mod(int n, int m)
    {
        int r = n % m;
        return r < 0 ? r + m : r;
    }

    /// <summary>
    /// Strip a text down to its letters, in capitals: <c>"Attack at dawn!"</c> becomes
    /// <c>"ATTACKATDAWN"</c>. This is the form transposition ciphers, Playfair, Hill and Enigma work
    /// on, and the form every statistic in this library is measured on.
    /// </summary>
    public static string Normalise(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (IsLetter(c))
            {
                sb.Append(char.ToUpperInvariant(c));
            }
        }

        return sb.ToString();
    }

    /// <summary>The letters of a text as numbers 0–25 (non-letters dropped).</summary>
    public static int[] ToNumbers(string text)
    {
        string clean = Normalise(text);
        var result = new int[clean.Length];
        for (int i = 0; i < clean.Length; i++)
        {
            result[i] = clean[i] - 'A';
        }

        return result;
    }

    /// <summary>Numbers (any integers, wrapped mod 26) back to capital letters.</summary>
    public static string FromNumbers(IEnumerable<int> numbers)
    {
        var sb = new StringBuilder();
        foreach (int n in numbers)
        {
            sb.Append(ToLetter(n));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Write letters in the traditional blocks of five (<c>ATTAC KATDA WN</c>), the way cipher
    /// clerks sent them so that word lengths gave nothing away.
    /// </summary>
    public static string Group(string text, int size = 5)
    {
        string clean = Normalise(text);
        if (size <= 0)
        {
            return clean;
        }

        var sb = new StringBuilder(clean.Length + (clean.Length / size));
        for (int i = 0; i < clean.Length; i++)
        {
            if (i > 0 && i % size == 0)
            {
                sb.Append(' ');
            }

            sb.Append(clean[i]);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Pour a run of letters back into the shape of an original text: its spaces, punctuation and
    /// capitalisation. Handy for showing a decryption the way the message was written. Extra letters
    /// are appended; missing ones leave the rest of the template's letters out.
    /// </summary>
    /// <param name="template">The text whose layout to copy.</param>
    /// <param name="letters">The letters to pour into it.</param>
    public static string RestoreLayout(string template, string letters)
    {
        var sb = new StringBuilder(template.Length);
        int next = 0;
        foreach (char c in template)
        {
            if (!IsLetter(c))
            {
                sb.Append(c);
                continue;
            }

            if (next >= letters.Length)
            {
                break;
            }

            char l = letters[next++];
            sb.Append(char.IsLower(c) ? char.ToLowerInvariant(l) : char.ToUpperInvariant(l));
        }

        if (next < letters.Length)
        {
            sb.Append(letters, next, letters.Length - next);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Apply a per-letter transform to a text, leaving everything that is not a letter alone and
    /// keeping lower case lower case. The substitution ciphers (Caesar, affine, Vigenère…) use this
    /// so <c>"Hello, World"</c> encrypts to something that still looks like a sentence.
    /// </summary>
    /// <param name="text">The text to transform.</param>
    /// <param name="transform">
    /// Given the letter's number (0–25) and its index among the letters seen so far, return the
    /// new number (it is wrapped mod 26).
    /// </param>
    public static string MapLetters(string text, Func<int, int, int> transform)
    {
        var sb = new StringBuilder(text.Length);
        int index = 0;
        foreach (char c in text)
        {
            if (!IsLetter(c))
            {
                sb.Append(c);
                continue;
            }

            char mapped = ToLetter(transform(ToNumber(c), index++));
            sb.Append(char.IsLower(c) ? char.ToLowerInvariant(mapped) : mapped);
        }

        return sb.ToString();
    }

    /// <summary>
    /// A keyword with repeated letters removed, then the rest of the alphabet in order — the
    /// classic way to remember a mixed alphabet. <c>"KRYPTOS"</c> gives
    /// <c>KRYPTOSABCDEFGHIJLMNQUVWXZ</c>.
    /// </summary>
    /// <param name="keyword">The keyword (non-letters ignored).</param>
    /// <param name="alphabet">The alphabet to complete it from (the 25-letter Playfair alphabet, say).</param>
    public static string KeywordAlphabet(string keyword, string alphabet = Alphabet)
    {
        var sb = new StringBuilder(alphabet.Length);
        foreach (char c in Normalise(keyword) + alphabet)
        {
            if (alphabet.IndexOf(c) >= 0 && sb.ToString().IndexOf(c) < 0)
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}
