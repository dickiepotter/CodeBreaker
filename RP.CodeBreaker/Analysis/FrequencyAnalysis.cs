using RP.CodeBreaker.Statistics;

namespace RP.CodeBreaker;

/// <summary>A letter and how often it appeared.</summary>
/// <param name="Letter">The letter.</param>
/// <param name="Count">How many times it appeared.</param>
/// <param name="Share">Its share of all the letters (0–1).</param>
/// <param name="Expected">Its share in ordinary English, for comparison.</param>
public sealed record LetterCount(char Letter, int Count, double Share, double Expected);

/// <summary>
/// Counting letters — the oldest tool in code breaking, described by the Arab polymath al-Kindi in
/// the ninth century and still the first thing anyone does with an unknown ciphertext.
/// </summary>
/// <remarks>
/// <para>
/// In English, E makes up about one letter in eight and Z about one in fifteen hundred. A cipher
/// that swaps each letter for another (a <i>monoalphabetic substitution</i>) changes the letters'
/// names but not how often they turn up, so the commonest ciphertext letter is probably E, the next
/// T, then A, O, N, I… That one observation turns 26! ≈ 4 × 10²⁶ possible alphabets into a puzzle you
/// can do on paper.
/// </para>
/// <para>
/// The measures here also tell you <i>what kind</i> of cipher you are facing, before you know a single
/// letter of the key: see <see cref="IndexOfCoincidence(string)"/> and <see cref="CipherIdentifier"/>.
/// </para>
/// </remarks>
public static class FrequencyAnalysis
{
    /// <summary>How many times each letter A–Z appears (non-letters ignored).</summary>
    public static int[] Counts(string text)
    {
        var counts = new int[26];
        foreach (char c in text)
        {
            if (Letters.IsLetter(c))
            {
                counts[Letters.ToNumber(c)]++;
            }
        }

        return counts;
    }

    /// <summary>The share of each letter A–Z (adds up to 1; all zero for an empty text).</summary>
    public static double[] Frequencies(string text)
    {
        int[] counts = Counts(text);
        double total = counts.Sum();
        return counts.Select(c => total == 0 ? 0 : c / total).ToArray();
    }

    /// <summary>
    /// Every letter with its count and its expected English share, commonest first — the table a
    /// code breaker writes down first.
    /// </summary>
    public static IReadOnlyList<LetterCount> Ranked(string text, LanguageModel? language = null)
    {
        language ??= LanguageModel.English;
        int[] counts = Counts(text);
        double total = Math.Max(1, counts.Sum());
        return Enumerable.Range(0, 26)
            .Select(i => new LetterCount(Letters.ToLetter(i), counts[i], counts[i] / total, language.LetterFrequencies[i]))
            .OrderByDescending(l => l.Count)
            .ThenBy(l => l.Letter)
            .ToArray();
    }

    /// <summary>
    /// The index of coincidence (IoC): the probability that two letters picked at random from the
    /// text (without putting the first back) are the same letter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// IoC = Σ nᵢ(nᵢ - 1) / (N(N - 1)), where nᵢ is the count of letter i and N the total.
    /// </para>
    /// <para>
    /// William Friedman's great discovery (1922). English gives about <b>0.066</b>, because its
    /// letters are lumpy — lots of E, few Z. Perfectly random letters give <b>1/26 ≈ 0.0385</b>. And
    /// here is the magic: <i>any cipher that just relabels letters leaves the IoC unchanged</i>,
    /// because it doesn't care which letter is which, only how lumpy the counts are. So a ciphertext
    /// with IoC ≈ 0.066 is a substitution or a transposition; one near 0.04 is polyalphabetic
    /// (Vigenère, Enigma) or a one-time pad.
    /// </para>
    /// </remarks>
    public static double IndexOfCoincidence(string text) => IndexOfCoincidence(Counts(text));

    /// <summary>The index of coincidence of a set of letter counts.</summary>
    public static double IndexOfCoincidence(IReadOnlyList<int> counts)
    {
        double total = 0;
        double sum = 0;
        foreach (int n in counts)
        {
            total += n;
            sum += n * (double)(n - 1);
        }

        return total < 2 ? 0 : sum / (total * (total - 1));
    }

    /// <summary>
    /// The chi-squared statistic comparing a text's letter counts with the language's: Σ (observed -
    /// expected)² / expected. Zero is a perfect match; English text of a few hundred letters usually
    /// scores under 50; a Caesar shift of it scores in the hundreds or thousands.
    /// </summary>
    /// <remarks>
    /// This is the test that breaks a Caesar cipher in one line: try all 26 shifts and keep the one
    /// whose letters look most like English — the lowest chi-squared.
    /// </remarks>
    public static double ChiSquared(string text, LanguageModel? language = null) => ChiSquared(Counts(text), language);

    /// <summary>The chi-squared statistic for a set of letter counts.</summary>
    public static double ChiSquared(IReadOnlyList<int> counts, LanguageModel? language = null)
    {
        language ??= LanguageModel.English;
        double total = counts.Sum();
        if (total == 0)
        {
            return 0;
        }

        double chi = 0;
        for (int i = 0; i < 26; i++)
        {
            double expected = Math.Max(language.LetterFrequencies[i], 1e-5) * total;
            double diff = counts[i] - expected;
            chi += diff * diff / expected;
        }

        return chi;
    }

    /// <summary>
    /// How plausible it is that the text's letters were drawn from the language, as a p-value: the
    /// probability of a chi-squared at least this large if they were (25 degrees of freedom). Near 1
    /// is "very English", near 0 is "almost certainly not plain English letters".
    /// </summary>
    /// <remarks>
    /// Uses the chi-squared distribution vendored from <c>RADD.Statistics</c>. Treat it as a guide
    /// rather than gospel: real text is not a set of independent letter draws, and long texts reject
    /// "English" for tiny differences of style.
    /// </remarks>
    public static double ChiSquaredPValue(string text, LanguageModel? language = null) =>
        ChiSquaredDistribution.UpperTail(ChiSquared(text, language), 25);

    /// <summary>
    /// Shannon entropy in bits per letter: how many yes/no questions, on average, each letter
    /// answers. Random letters: log₂ 26 ≈ 4.70. English single-letter statistics: about 4.17. (English
    /// with all its context — spelling, grammar, meaning — is nearer 1–1.5 bits per letter, which is
    /// why a long ciphertext gives so much away.)
    /// </summary>
    public static double Entropy(string text)
    {
        double[] f = Frequencies(text);
        double h = 0;
        foreach (double p in f)
        {
            if (p > 0)
            {
                h -= p * Math.Log2(p);
            }
        }

        return h;
    }

    /// <summary>
    /// The commonest n-grams (runs of n letters) in a text, with counts: TH, HE, IN, ER for English
    /// bigrams; THE, AND, ING for trigrams.
    /// </summary>
    public static IReadOnlyList<KeyValuePair<string, int>> NGrams(string text, int n, int top = 20)
    {
        string clean = Letters.Normalise(text);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i + n <= clean.Length; i++)
        {
            string g = clean.Substring(i, n);
            counts[g] = counts.TryGetValue(g, out int c) ? c + 1 : 1;
        }

        return counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Take(top).ToArray();
    }

    /// <summary>
    /// A first-guess substitution key by frequency alone: line up the ciphertext letters from
    /// commonest to rarest against English from commonest to rarest. Rarely right in every letter, but
    /// a superb starting point for <see cref="Attacks.SubstitutionAttack"/>.
    /// </summary>
    /// <returns>A 26-letter string: position i is the plaintext letter for ciphertext letter i.</returns>
    public static string FrequencyMatchedKey(string ciphertext, LanguageModel? language = null)
    {
        language ??= LanguageModel.English;
        int[] counts = Counts(ciphertext);
        int[] cipherOrder = Enumerable.Range(0, 26).OrderByDescending(i => counts[i]).ThenBy(i => i).ToArray();
        int[] englishOrder = Enumerable.Range(0, 26).OrderByDescending(i => language.LetterFrequencies[i]).ToArray();
        var key = new char[26];
        for (int r = 0; r < 26; r++)
        {
            key[cipherOrder[r]] = Letters.ToLetter(englishOrder[r]);
        }

        return new string(key);
    }
}
