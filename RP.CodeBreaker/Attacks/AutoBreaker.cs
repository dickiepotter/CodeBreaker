namespace RP.CodeBreaker.Attacks;

/// <summary>
/// "Just break it": identify the likely cipher family from the statistics, run the matching attacks,
/// and return whichever result reads most like English.
/// </summary>
/// <remarks>
/// This is how a modern solver (and an experienced human) works: let the cheap statistics choose the
/// expensive attack, rather than throwing every attack at every message. Enigma, ADFGVX, Bifid and the
/// one-time pad are deliberately not attempted — they need a crib and a dedicated attack (or, for the
/// pad, are unbreakable).
/// </remarks>
public static class AutoBreaker
{
    /// <summary>Identify and break a ciphertext.</summary>
    public static async ValueTask<(IReadOnlyList<CipherGuess> Guesses, BreakResult? Best, IReadOnlyList<BreakResult> All)> BreakAsync(string ciphertext, AttackOptions? options = null)
    {
        options ??= new AttackOptions();
        IReadOnlyList<CipherGuess> guesses = CipherIdentifier.Identify(ciphertext, options.Language);
        var results = new List<BreakResult>();
        var tried = new HashSet<CipherFamily>();
        foreach (CipherGuess guess in guesses)
        {
            if (!tried.Add(guess.Family))
            {
                continue;
            }

            switch (guess.Family)
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
                    results.Add(await ColumnarAttack.BreakAsync(ciphertext, options).ConfigureAwait(true));
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
                    results.Add(HillAttack.Break(ciphertext, options));
                    break;
            }

            if (results.Count > 0 && results.Max(r => r.EnglishLikeness) > -4.8)
            {
                break; // something already reads as English — no need to go on
            }
        }

        BreakResult? best = results.OrderByDescending(r => r.EnglishLikeness).FirstOrDefault();
        return (guesses, best, results);
    }
}
