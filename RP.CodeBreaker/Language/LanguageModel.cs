using System.IO.Compression;
using System.Text;

namespace RP.CodeBreaker;

/// <summary>
/// What English looks like, statistically: how often each letter, each pair of letters, each run of
/// four letters and each word turns up. A code breaker's sense of "that reads like English" made
/// into numbers a computer can maximise.
/// </summary>
/// <remarks>
/// <para>
/// The figures are counted from about two million letters of public-domain novels (Conan Doyle,
/// Austen, Dickens, Wells and Buchan, via Project Gutenberg) by <c>tools/build_language_data.py</c>
/// and embedded in the assembly, so nothing is downloaded at run time.
/// </para>
/// <para>
/// <b>Quadgrams</b> — runs of four letters such as <c>TION</c>, <c>THER</c>, <c>NTHE</c> — are the
/// workhorse. To judge a candidate decryption we slide a four-letter window along it and add up the
/// logarithm of how likely each window is in English. Real English scores far higher than gibberish,
/// and — crucially — text that is <i>nearly</i> right scores nearly as high, so a search can creep
/// towards the answer one small improvement at a time. Logarithms are used because multiplying
/// thousands of small probabilities underflows to zero, while adding their logs does not.
/// </para>
/// <para>
/// A quadgram never seen in the corpus is not impossible, just rare, so it is given a "floor"
/// probability of 0.01 occurrences rather than zero (whose log would be minus infinity and would
/// wreck the score of an otherwise perfect decryption with one odd name in it).
/// </para>
/// </remarks>
public sealed class LanguageModel
{
    private const int QuadCount = 26 * 26 * 26 * 26;

    private static readonly Lazy<LanguageModel> EnglishModel = new(() => Load("English", string.Empty), LazyThreadSafetyMode.ExecutionAndPublication);
    private static readonly Lazy<LanguageModel> GermanModel = new(() => Load("German", "german."), LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly float[] quadgrams;
    private readonly Dictionary<string, double> wordLogProbabilities;
    private readonly double unknownWordLogProbability;

    private LanguageModel(
        string name,
        double[] letterFrequencies,
        double[,] bigramFrequencies,
        float[] quadgrams,
        IReadOnlyList<(string Word, long Count)> words,
        double quadgramFloor)
    {
        this.Name = name;
        this.LetterFrequencies = letterFrequencies;
        this.BigramFrequencies = bigramFrequencies;
        this.quadgrams = quadgrams;
        this.QuadgramFloor = quadgramFloor;
        this.Words = words.Select(w => w.Word).ToArray();

        double total = words.Sum(w => (double)w.Count);
        this.wordLogProbabilities = new Dictionary<string, double>(words.Count, StringComparer.Ordinal);
        foreach ((string word, long count) in words)
        {
            this.wordLogProbabilities[word] = Math.Log10(count / total);
        }

        this.unknownWordLogProbability = Math.Log10(0.5 / total);

        double ic = 0;
        foreach (double p in letterFrequencies)
        {
            ic += p * p;
        }

        this.ExpectedIndexOfCoincidence = ic;
    }

    /// <summary>English, counted from public-domain novels. Loaded once, on first use.</summary>
    public static LanguageModel English => EnglishModel.Value;

    /// <summary>
    /// German, counted from public-domain Goethe, Kafka, Schiller and Fontane, with umlauts written AE,
    /// OE, UE and ß as SS — the way Enigma operators keyed them. Enigma traffic is German, and an
    /// English model judges a correct German decryption as gibberish.
    /// </summary>
    public static LanguageModel German => GermanModel.Value;

    /// <summary>The built-in languages.</summary>
    public static IReadOnlyList<LanguageModel> BuiltIn => [English, German];

    /// <summary>The language's name.</summary>
    public string Name { get; }

    /// <summary>
    /// The share of each letter A–Z (they add up to 1). In English E ≈ 0.125, T ≈ 0.09, … Z ≈ 0.0007.
    /// </summary>
    public IReadOnlyList<double> LetterFrequencies { get; }

    /// <summary>The share of each letter pair, indexed [first, second].</summary>
    public double[,] BigramFrequencies { get; }

    /// <summary>
    /// The chance that two letters picked at random from English text are the same letter: Σ p².
    /// About 0.066 for English, against 1/26 ≈ 0.0385 for random letters. See
    /// <see cref="FrequencyAnalysis.IndexOfCoincidence(string)"/>.
    /// </summary>
    public double ExpectedIndexOfCoincidence { get; }

    /// <summary>The log₁₀ probability given to a quadgram never seen in the corpus.</summary>
    public double QuadgramFloor { get; }

    /// <summary>The known words, most common first (THE, AND, OF, TO, A, I, IN, WAS…).</summary>
    public IReadOnlyList<string> Words { get; }

    /// <summary>log₁₀ of the probability of the quadgram a-b-c-d (letters as numbers 0–25).</summary>
    public double Quadgram(int a, int b, int c, int d) => this.quadgrams[(((((a * 26) + b) * 26) + c) * 26) + d];

    /// <summary>log₁₀ of the probability of a four-letter string such as "TION".</summary>
    public double Quadgram(string four) =>
        this.Quadgram(Letters.ToNumber(four[0]), Letters.ToNumber(four[1]), Letters.ToNumber(four[2]), Letters.ToNumber(four[3]));

    /// <summary>
    /// The quadgram fitness of a text given as letter numbers: the sum of log₁₀ P over every
    /// four-letter window. Higher (less negative) is more English. This is the inner loop of every
    /// hill-climbing attack, so it works on a plain array.
    /// </summary>
    public double QuadgramScore(ReadOnlySpan<int> letters)
    {
        if (letters.Length < 4)
        {
            return 0;
        }

        float[] q = this.quadgrams;
        int index = (((letters[0] * 26) + letters[1]) * 26) + letters[2];
        double score = 0;
        for (int i = 3; i < letters.Length; i++)
        {
            index = ((index * 26) + letters[i]) % QuadCount;
            score += q[index];
        }

        return score;
    }

    /// <summary>The quadgram fitness of a text (non-letters ignored).</summary>
    public double QuadgramScore(string text) => this.QuadgramScore(Letters.ToNumbers(text));

    /// <summary>
    /// The quadgram fitness per four-letter window — comparable between texts of different lengths.
    /// English prose sits around -4.4; shuffled or random letters around -6.5 or below.
    /// </summary>
    public double QuadgramScorePerLetter(string text)
    {
        int[] n = Letters.ToNumbers(text);
        return n.Length < 4 ? this.QuadgramFloor : this.QuadgramScore(n) / (n.Length - 3);
    }

    /// <summary>True if <paramref name="word"/> is in the word list.</summary>
    public bool IsWord(string word) => this.wordLogProbabilities.ContainsKey(Letters.Normalise(word));

    /// <summary>
    /// Put the spaces back into a run of capitals, choosing the most probable split into known words
    /// (dynamic programming over the word list): <c>"ATTACKATDAWN"</c> → <c>"ATTACK AT DAWN"</c>.
    /// Purely cosmetic, but it makes a decryption far easier to read.
    /// </summary>
    public string Segment(string letters)
    {
        string text = Letters.Normalise(letters);
        int n = text.Length;
        if (n == 0)
        {
            return string.Empty;
        }

        const int MaxWord = 20;
        var best = new double[n + 1];
        var from = new int[n + 1];
        best[0] = 0;
        for (int end = 1; end <= n; end++)
        {
            best[end] = double.NegativeInfinity;
            for (int start = Math.Max(0, end - MaxWord); start < end; start++)
            {
                string word = text.Substring(start, end - start);
                double p = this.wordLogProbabilities.TryGetValue(word, out double known)
                    ? known
                    : this.unknownWordLogProbability - (word.Length * 1.0); // unknown words: worse the longer they are
                double total = best[start] + p;
                if (total > best[end])
                {
                    best[end] = total;
                    from[end] = start;
                }
            }
        }

        var words = new List<string>();
        for (int end = n; end > 0; end = from[end])
        {
            words.Add(text.Substring(from[end], end - from[end]));
        }

        words.Reverse();
        return string.Join(" ", words);
    }

    private static LanguageModel Load(string name, string prefix)
    {
        var mono = new double[26];
        foreach ((string key, long count) in ReadTable(prefix + "monograms"))
        {
            mono[key[0] - 'A'] = count;
        }

        double monoTotal = mono.Sum();
        for (int i = 0; i < 26; i++)
        {
            mono[i] /= monoTotal;
        }

        var bi = new double[26, 26];
        double biTotal = 0;
        foreach ((string key, long count) in ReadTable(prefix + "bigrams"))
        {
            bi[key[0] - 'A', key[1] - 'A'] = count;
            biTotal += count;
        }

        for (int i = 0; i < 26; i++)
        {
            for (int j = 0; j < 26; j++)
            {
                bi[i, j] /= biTotal;
            }
        }

        var quadRows = ReadTable(prefix + "quadgrams").ToList();

        // Quadgrams seen only once were dropped when the table was built, so the true total is a
        // little larger than the sum of the rows; the letter count stands in for it (every position
        // in the corpus is the start of one quadgram).
        double quadTotal = monoTotal;
        float floor = (float)Math.Log10(0.01 / quadTotal);
        var quads = new float[QuadCount];
        Array.Fill(quads, floor);
        foreach ((string key, long count) in quadRows)
        {
            int index = ((((((key[0] - 'A') * 26) + (key[1] - 'A')) * 26) + (key[2] - 'A')) * 26) + (key[3] - 'A');
            quads[index] = (float)Math.Log10(count / quadTotal);
        }

        var words = ReadTable(prefix + "words").ToList();
        return new LanguageModel(name, mono, bi, quads, words, floor);
    }

    private static IEnumerable<(string Key, long Count)> ReadTable(string table)
    {
        string resource = $"RP.CodeBreaker.Language.{table}.txt.gz";
        using Stream raw = typeof(LanguageModel).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"The embedded table {resource} is missing.");
        using var gz = new GZipStream(raw, CompressionMode.Decompress);
        using var reader = new StreamReader(gz, Encoding.ASCII);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            int space = line.IndexOf(' ');
            if (space > 0)
            {
                yield return (line.Substring(0, space), long.Parse(line.AsSpan(space + 1), System.Globalization.CultureInfo.InvariantCulture));
            }
        }
    }
}
