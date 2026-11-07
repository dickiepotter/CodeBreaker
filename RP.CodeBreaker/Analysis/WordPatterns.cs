using System.Text;

namespace RP.CodeBreaker;

/// <summary>
/// Letter patterns: the shape of a word once you forget which letters it uses. THAT is
/// <c>ABCA</c>, LETTER is <c>ABCCBD</c>, WETTERBERICHT is <c>ABCCBDEBDFGHC</c>.
/// </summary>
/// <remarks>
/// <para>
/// A substitution cipher changes every letter but cannot change a word's pattern: if THAT encrypts
/// to XQPX, the first and last letters still match. So a crib — a word you expect in the message —
/// can only sit where the ciphertext has the same pattern. A long crib with repeated letters usually
/// fits in very few places, and once it is placed every one of its letters becomes a known part of
/// the key. This is the "isomorph" trick, and it is also how pattern-word dictionaries let people
/// solve newspaper cryptograms.
/// </para>
/// </remarks>
public static class WordPatterns
{
    /// <summary>The pattern of a word: each new letter gets the next letter of the alphabet.</summary>
    public static string Pattern(string word)
    {
        string w = Letters.Normalise(word);
        var map = new Dictionary<char, char>();
        var sb = new StringBuilder(w.Length);
        foreach (char c in w)
        {
            if (!map.TryGetValue(c, out char p))
            {
                map[c] = p = (char)('A' + map.Count);
            }

            sb.Append(p);
        }

        return sb.ToString();
    }

    /// <summary>Known words with the same pattern as <paramref name="cipherWord"/>, commonest first.</summary>
    public static IReadOnlyList<string> Matching(string cipherWord, LanguageModel? language = null, int max = 50)
    {
        language ??= LanguageModel.English;
        string pattern = Pattern(cipherWord);
        return language.Words.Where(w => w.Length == pattern.Length && Pattern(w) == pattern).Take(max).ToArray();
    }

    /// <summary>
    /// Every position where <paramref name="crib"/> could sit in a substitution ciphertext: where the
    /// ciphertext letters repeat exactly as the crib's do, and different crib letters meet different
    /// ciphertext letters.
    /// </summary>
    public static IReadOnlyList<int> PlacementsForSubstitution(string ciphertext, string crib)
    {
        string text = Letters.Normalise(ciphertext);
        string c = Letters.Normalise(crib);
        string want = Pattern(c);
        var result = new List<int>();
        for (int i = 0; i + c.Length <= text.Length; i++)
        {
            if (Pattern(text.Substring(i, c.Length)) == want)
            {
                result.Add(i);
            }
        }

        return result;
    }
}
