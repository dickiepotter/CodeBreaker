namespace RP.CodeBreaker;

/// <summary>How far to trust an attack's answer.</summary>
public enum Confidence
{
    /// <summary>It reads as the language, contains every crib, and the message was long enough to be sure.</summary>
    Solved,

    /// <summary>Plausible but not certain: partly readable, or too short for this attack to be trusted.</summary>
    Doubtful,

    /// <summary>Wrong: a crib is missing, the text is gibberish, or no key survived.</summary>
    Failed,
}

/// <summary>
/// A second opinion on a <see cref="BreakResult"/>: does the answer actually contain the words we
/// were sure of, does it read, and was there enough ciphertext for the attack to be trusted?
/// </summary>
/// <param name="Confidence">The verdict.</param>
/// <param name="Summary">One sentence a person can act on.</param>
/// <param name="Reasons">Every finding behind the verdict, most important first.</param>
/// <param name="CribsFound">The cribs that appear in the answer.</param>
/// <param name="CribsMissing">The cribs that don't — any one of these means the answer is wrong.</param>
/// <param name="Letters">How many letters of ciphertext there were.</param>
/// <param name="LettersNeeded">Roughly how many this attack needs to be reliable without help.</param>
/// <remarks>
/// <para>
/// An attack always returns its <i>best</i> key, and the best of a bad lot is still wrong. Code breakers
/// checked every result: Bletchley tested every bombe stop on a replica Enigma before it was believed.
/// This is that check, and it is deliberately blunt:
/// </para>
/// <list type="bullet">
/// <item><b>A missing crib is decisive.</b> If you were sure the word was there and it isn't, either this
/// key is wrong or the crib is (misspelt, in another language, or simply not in this message).</item>
/// <item><b>Readability.</b> Real text scores about −4.0 to −4.5 per letter on quadgrams; random letters
/// −6 or lower. Between −4.7 and −5.1 is the "half-reads" zone of a near-miss key.</item>
/// <item><b>Length.</b> Below a few dozen letters many keys give believable text (the unicity distance), and a
/// hill climb has too little to steer by long before that. The figures in <see cref="ReliableLength"/>
/// are practical rules of thumb from running these attacks, not theorems; a crib lowers them a lot.</item>
/// </list>
/// </remarks>
public sealed record BreakCheck(
    Confidence Confidence,
    string Summary,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<string> CribsFound,
    IReadOnlyList<string> CribsMissing,
    int Letters,
    int LettersNeeded)
{
    /// <summary>Per-letter quadgram score above which text reads clearly.</summary>
    public const double ReadsClearly = -4.7;

    /// <summary>Per-letter quadgram score below which text is gibberish (matches <see cref="BreakResult.LooksLikeEnglish"/>).</summary>
    public const double Gibberish = -5.1;

    /// <summary>
    /// Roughly how many letters each attack needs to be reliable from the ciphertext alone. A crib
    /// lets it work on much less, which <see cref="Check"/> allows for.
    /// </summary>
    public static IReadOnlyDictionary<string, int> ReliableLength { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["Caesar"] = 15,
        ["Affine"] = 20,
        ["Rail fence"] = 25,
        ["Columnar transposition"] = 60,
        ["Vigenère"] = 40,
        ["Beaufort"] = 40,
        ["Autokey"] = 80,
        ["Simple substitution"] = 100,
        ["Polybius square"] = 100,
        ["Hill (2×2)"] = 60,
        ["Playfair"] = 150,
    };

    /// <summary>Check an attack's answer against the cribs, the language and the length of the message.</summary>
    /// <param name="result">What the attack returned.</param>
    /// <param name="ciphertext">The ciphertext it was given.</param>
    /// <param name="options">The options it ran with (for the cribs).</param>
    public static BreakCheck Check(BreakResult result, string ciphertext, AttackOptions options)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(options);
        string answer = RP.CodeBreaker.Letters.Normalise(result.Plaintext);
        // Count letters — or, for a Polybius square's digits, the letters the digit pairs stand for.
        int letters = Math.Max(
            RP.CodeBreaker.Letters.Normalise(ciphertext ?? string.Empty).Length,
            (ciphertext ?? string.Empty).Count(char.IsAsciiDigit) / 2);
        IReadOnlyList<string> cribs = options.NormalisedCribs;
        string[] found = cribs.Where(c => answer.Contains(c, StringComparison.Ordinal)).ToArray();
        string[] missing = cribs.Where(c => !answer.Contains(c, StringComparison.Ordinal)).ToArray();

        // A crib is worth several times its own length in ciphertext; never ask for less than 20 letters.
        int needed = ReliableLength.TryGetValue(result.CipherName, out int n) ? n : 0;
        if (needed > 0 && found.Length > 0)
        {
            needed = Math.Max(20, needed - (5 * found.Sum(c => c.Length)));
        }

        var reasons = new List<string>();
        Confidence confidence;
        string summary;

        if (answer.Length == 0)
        {
            confidence = Confidence.Failed;
            summary = "No key survived, so there is no answer.";
            reasons.Add(cribs.Count > 0
                ? "No key gave a decryption containing every crib. Either the cipher is something else, or a crib isn't really in the message."
                : "The attack found no candidate at all; the cipher is probably something else.");
        }
        else if (missing.Length > 0)
        {
            confidence = Confidence.Failed;
            string list = string.Join(", ", missing);
            summary = $"Wrong answer: {list} {(missing.Length == 1 ? "is" : "are")} not in it.";
            reasons.Add($"You said the message contains {list}, and this decryption doesn't, so this key is wrong — or the crib is (check its spelling and the language, and that it really is in this message).");
            reasons.Add("Try another attack (the statistics may have chosen the wrong cipher), more restarts, or a longer crib.");
            AddPeriodHint(result, options, reasons);
        }
        else if (result.EnglishLikeness < Gibberish)
        {
            confidence = Confidence.Failed;
            summary = "Wrong answer: the decryption doesn't read as language.";
            reasons.Add($"It scores {result.EnglishLikeness:0.00} per letter; real text scores about −4.0 to −4.5, random letters −6 or lower.");
            reasons.Add("Try another attack, or add a crib — a word you are sure is in the message.");
            AddPeriodHint(result, options, reasons);
        }
        else if (result.EnglishLikeness < ReadsClearly)
        {
            confidence = Confidence.Doubtful;
            summary = "Probably a near miss: it partly reads, but not cleanly.";
            reasons.Add($"It scores {result.EnglishLikeness:0.00} per letter, between gibberish (below −5.1) and clear text (above −4.7). A hill climb that stops on a nearby key gives text like this.");
            reasons.Add("Run it again with more restarts, or add a crib.");
        }
        else
        {
            confidence = Confidence.Solved;
            summary = found.Length > 0
                ? $"Solved: it reads as language and contains {string.Join(", ", found)}."
                : "Solved: it reads clearly as language.";
        }

        if (needed > 0 && letters < needed)
        {
            reasons.Add($"The message has {letters} letters; {result.CipherName} usually needs about {needed}{(found.Length > 0 ? " even with the crib" : string.Empty)}. With so little text, wrong keys can give believable-looking text and the search has little to steer by.");
            if (confidence == Confidence.Solved)
            {
                confidence = Confidence.Doubtful;
                summary = "Reads well, but the message is too short to be sure.";
            }
        }

        if (confidence == Confidence.Solved && cribs.Count == 0 && letters < 2 * Math.Max(needed, 20))
        {
            reasons.Add("A crib would make this certain: with one, a wrong key is caught at once.");
        }

        return new BreakCheck(confidence, summary, reasons, found, missing, letters, needed);
    }

    /// <summary>A repeating-key attack only looks for keys up to <see cref="AttackOptions.MaxKeyLength"/> letters.</summary>
    private static void AddPeriodHint(BreakResult result, AttackOptions options, List<string> reasons)
    {
        if (result.CipherName is "Vigenère" or "Beaufort")
        {
            reasons.Add($"This attack only looks for keys of up to {options.MaxKeyLength} letters; if the key could be longer, raise \"Longest key\".");
        }
    }

    /// <summary>
    /// Order results for trust: an answer containing every crib first, then one this check would call
    /// solved (readable, and the message long enough for that attack), then the most English.
    /// </summary>
    /// <param name="results">The candidates, from one or several attacks.</param>
    /// <param name="ciphertext">The ciphertext they came from.</param>
    /// <param name="options">The options, for the cribs.</param>
    public static IEnumerable<BreakResult> MostTrusted(IEnumerable<BreakResult> results, string ciphertext, AttackOptions options) =>
        results
            .Where(r => r.Plaintext.Length > 0)
            .Select(r => (Result: r, Check: Check(r, ciphertext, options)))
            .OrderByDescending(x => x.Check.CribsMissing.Count == 0)
            .ThenByDescending(x => x.Check.Confidence == Confidence.Solved)
            .ThenByDescending(x => x.Result.EnglishLikeness)
            .Select(x => x.Result);
}
