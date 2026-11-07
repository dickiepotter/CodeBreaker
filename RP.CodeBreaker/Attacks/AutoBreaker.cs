namespace RP.CodeBreaker.Attacks;

/// <summary>
/// "Just break it": identify the likely cipher family from the statistics, run the matching attacks,
/// and return the result that contains the cribs and reads most like the language.
/// </summary>
/// <remarks>
/// <para>
/// This is how a modern solver (and an experienced human) works: let the cheap statistics choose the
/// expensive attack, rather than throwing every attack at every message. Enigma, ADFGVX, Bifid and the
/// one-time pad are deliberately not attempted — they need a crib and a dedicated attack (or, for the
/// pad, are unbreakable).
/// </para>
/// <para>
/// The statistics can mislead, especially on short messages: a 50-letter transposition can look like a
/// substitution. So the guesses only set the order. Auto stops early only on a convincing answer — one
/// that reads clearly as the language and contains every crib. Otherwise it goes on to the remaining
/// quick attacks, and a result containing the cribs always beats one that doesn't.
/// </para>
/// </remarks>
public static class AutoBreaker
{
    /// <summary>Per-letter quadgram score above which an answer is convincing enough to stop searching.</summary>
    private const double Convincing = -4.3;

    /// <summary>The families cheap enough to try even when the statistics didn't suggest them.</summary>
    private static readonly CipherFamily[] QuickFamilies =
    [
        CipherFamily.ShiftOrAffine,
        CipherFamily.Transposition,
        CipherFamily.PeriodicPolyalphabetic,
        CipherFamily.Substitution,
        CipherFamily.NonPeriodicPolyalphabetic,
        CipherFamily.Digraphic,
    ];

    /// <summary>Identify and break a ciphertext.</summary>
    public static async ValueTask<(IReadOnlyList<CipherGuess> Guesses, BreakResult? Best, IReadOnlyList<BreakResult> All)> BreakAsync(string ciphertext, AttackOptions? options = null)
    {
        options ??= new AttackOptions();
        IReadOnlyList<CipherGuess> guesses = CipherIdentifier.Identify(ciphertext, options.Language);
        var results = new List<BreakResult>();
        var tried = new HashSet<CipherFamily>();

        // The suggested families first, in order of confidence; then, if nothing convincing has turned up,
        // the other quick ones (only for letter ciphertexts — digits mean Polybius, hex means XOR).
        IEnumerable<CipherFamily> order = guesses.Select(g => g.Family);
        if (Letters.Normalise(ciphertext).Length >= 10)
        {
            order = order.Concat(QuickFamilies);
        }

        var suggested = guesses.Select(g => g.Family).ToHashSet();

        // The columnar search is the slow one. A transposition only moves letters, so unless the statistics
        // suggested it, try it only when the letter counts already match the language. Chi-squared per
        // letter separates the two cleanly: about 0.1–0.7 for any transposition, 3 or more for anything
        // that changes letters. (The p-value doesn't: long texts "reject" English over tiny differences.)
        int letterCount = Letters.Normalise(ciphertext).Length;
        bool letterCountsFit = letterCount > 0
            && FrequencyAnalysis.ChiSquared(ciphertext, options.Language) / letterCount < 1.5;
        foreach (CipherFamily family in order)
        {
            if (!tried.Add(family))
            {
                continue;
            }


            if (Best(results, ciphertext, options) is { } sofar && IsConvincing(sofar, ciphertext, options))
            {
                break; // something already reads clearly as the language and holds every crib
            }

            switch (family)
            {
                case CipherFamily.ShiftOrAffine:
                    results.Add(CaesarAttack.Break(ciphertext, options));
                    results.Add(AffineAttack.Break(ciphertext, options));
                    break;
                case CipherFamily.Substitution:
                    results.Add(await SubstitutionAttack.BreakAsync(ciphertext, options).ConfigureAwait(true));
                    break;
                case CipherFamily.Transposition:
                    results.Add(RailFenceAttack.Break(ciphertext, options));
                    bool railSolvedIt = IsConvincing(results[^1], ciphertext, options);
                    if (!railSolvedIt && (suggested.Contains(CipherFamily.Transposition) || letterCountsFit))
                    {
                        results.Add(await ColumnarAttack.BreakAsync(ciphertext, options).ConfigureAwait(true));
                    }

                    break;
                case CipherFamily.PeriodicPolyalphabetic:
                    results.Add(VigenereAttack.Break(ciphertext, options));
                    results.Add(VigenereAttack.Break(ciphertext, options, PolyalphabeticVariant.Beaufort));
                    break;
                case CipherFamily.NonPeriodicPolyalphabetic:
                    results.Add(AutokeyAttack.Break(ciphertext, options));
                    break;
                case CipherFamily.Playfair:
                    results.Add(await PlayfairAttack.BreakAsync(ciphertext, options).ConfigureAwait(true));
                    break;
                case CipherFamily.Polybius:
                    results.Add(await PolybiusAttack.BreakAsync(ciphertext, options).ConfigureAwait(true));
                    break;
                case CipherFamily.Digraphic:
                    if (Letters.Normalise(ciphertext).Length >= 4)
                    {
                        results.Add(HillAttack.Break(ciphertext, options));
                    }

                    break;
            }

            await options.PauseAsync().ConfigureAwait(true);
        }

        return (guesses, Best(results, ciphertext, options), results);
    }

    /// <summary>
    /// The best result: one containing every crib beats one that doesn't; then one the message is long
    /// enough to trust (on 50 letters an over-fitted substitution can out-score the true affine key);
    /// then the most English.
    /// </summary>
    private static BreakResult? Best(List<BreakResult> results, string ciphertext, AttackOptions options) =>
        BreakCheck.MostTrusted(results, ciphertext, options).FirstOrDefault();

    /// <summary>
    /// Good enough to stop: the check calls it solved (readable, every crib present, the message long
    /// enough for that attack) and either a crib confirms it or it reads very clearly indeed.
    /// </summary>
    private static bool IsConvincing(BreakResult result, string ciphertext, AttackOptions options)
    {
        BreakCheck check = BreakCheck.Check(result, ciphertext, options);
        return check.Confidence == Confidence.Solved
            && (check.CribsFound.Count > 0 || result.EnglishLikeness > Convincing);
    }
}
