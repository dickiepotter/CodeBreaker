using System.Globalization;

namespace RP.CodeBreaker;

/// <summary>The kinds of cipher the identifier can recognise.</summary>
public enum CipherFamily
{
    /// <summary>Caesar, affine, Atbash — one fixed alphabet with a tiny key space.</summary>
    ShiftOrAffine,

    /// <summary>A general simple substitution.</summary>
    Substitution,

    /// <summary>Letters moved, not changed: rail fence, columnar.</summary>
    Transposition,

    /// <summary>Vigenère, Beaufort and other repeating keys.</summary>
    PeriodicPolyalphabetic,

    /// <summary>Autokey, running key: polyalphabetic with no period.</summary>
    NonPeriodicPolyalphabetic,

    /// <summary>Playfair: pairs of letters, 25-letter alphabet.</summary>
    Playfair,

    /// <summary>Polybius square: digit pairs 1–5.</summary>
    Polybius,

    /// <summary>ADFGVX: only the letters A, D, F, G, V, X.</summary>
    Adfgvx,

    /// <summary>Hill or another digraphic cipher.</summary>
    Digraphic,

    /// <summary>Statistically flat: a one-time pad, Enigma, or a modern cipher.</summary>
    Random,
}

/// <summary>A guess at the cipher type, with the reason.</summary>
/// <param name="Family">The cipher family.</param>
/// <param name="Confidence">0–1, how sure the identifier is.</param>
/// <param name="Reason">The evidence.</param>
public sealed record CipherGuess(CipherFamily Family, double Confidence, string Reason);

/// <summary>
/// Working out <i>what kind</i> of cipher a message is in, before knowing anything about the key —
/// the step every code breaker takes first, using only the statistics.
/// </summary>
/// <remarks>
/// <para>The decision runs roughly like this:</para>
/// <list type="number">
/// <item><b>Which symbols?</b> Only digits 1–5 means Polybius; only ADFGVX means ADFGVX; no J and an even
/// length with no doubled pairs points to Playfair.</item>
/// <item><b>Index of coincidence.</b> Near English (≈ 0.066) means a single alphabet — the letters keep
/// their lumpy distribution. Near random (≈ 0.038–0.045) means many alphabets, or none.</item>
/// <item><b>Do the letters look English as they stand?</b> If the frequencies match English (low
/// chi-squared) but the text doesn't read, it is a transposition. If they're lumpy but wrong, a
/// substitution — and if a single Caesar shift makes them match, a Caesar.</item>
/// <item><b>Is there a period?</b> For a low IoC, if some key length makes the columns English-like, it is
/// Vigenère-type; if none does, autokey, a one-time pad or a machine.</item>
/// </list>
/// </remarks>
public static class CipherIdentifier
{
    /// <summary>The likely cipher families for a ciphertext, most likely first.</summary>
    public static IReadOnlyList<CipherGuess> Identify(string ciphertext, LanguageModel? language = null)
    {
        language ??= LanguageModel.English;
        var guesses = new List<CipherGuess>();
        string trimmed = new(ciphertext.Where(c => !char.IsWhiteSpace(c)).ToArray());
        if (trimmed.Length == 0)
        {
            return guesses;
        }

        if (trimmed.All(c => c >= '1' && c <= '5'))
        {
            guesses.Add(new CipherGuess(CipherFamily.Polybius, 0.95, "Only the digits 1–5: the coordinates of a 5×5 Polybius square."));
            return guesses;
        }

        string text = Letters.Normalise(ciphertext);
        if (text.Length > 0 && text.All(c => "ADFGVX".IndexOf(c) >= 0))
        {
            guesses.Add(new CipherGuess(CipherFamily.Adfgvx, 0.95, "Only the letters A, D, F, G, V and X: the ADFGVX cipher's coordinate letters."));
            return guesses;
        }

        if (text.Length < 20)
        {
            guesses.Add(new CipherGuess(CipherFamily.ShiftOrAffine, 0.3, "Too short for statistics to say much; try the small key spaces first."));
            return guesses;
        }

        double ic = FrequencyAnalysis.IndexOfCoincidence(text);
        double chi = FrequencyAnalysis.ChiSquared(text, language) / text.Length;
        int bestShift = Attacks.CaesarAttack.ShiftByFrequency(text, language);
        double shiftedChi = FrequencyAnalysis.ChiSquared(new Ciphers.CaesarCipher(bestShift).Decrypt(text), language) / text.Length;
        double english = language.ExpectedIndexOfCoincidence;
        string icText = string.Format(CultureInfo.InvariantCulture, "IoC {0:0.0000} (English {1:0.0000}, random 0.0385)", ic, english);

        bool noJ = text.IndexOf('J') < 0;
        bool evenNoDoubles = text.Length % 2 == 0 && Enumerable.Range(0, text.Length / 2).All(i => text[2 * i] != text[(2 * i) + 1]);
        if (noJ && evenNoDoubles && text.Length >= 40)
        {
            guesses.Add(new CipherGuess(CipherFamily.Playfair, 0.8, "Even length, no J, and no pair of letters is a double — exactly what Playfair produces."));
        }

        if (ic > english - 0.012)
        {
            if (chi < 0.25)
            {
                guesses.Add(new CipherGuess(CipherFamily.Transposition, 0.85, $"{icText}, and the letter frequencies already match English: the letters are unchanged, only moved."));
            }
            else if (shiftedChi < 0.25)
            {
                guesses.Add(new CipherGuess(CipherFamily.ShiftOrAffine, 0.85, $"{icText}, and shifting by {bestShift} makes the frequencies match English: a Caesar shift."));
                guesses.Add(new CipherGuess(CipherFamily.Substitution, 0.4, "Any shift is also a substitution."));
            }
            else
            {
                guesses.Add(new CipherGuess(CipherFamily.Substitution, 0.75, $"{icText}: one fixed alphabet, but the letters are relabelled."));
                guesses.Add(new CipherGuess(CipherFamily.ShiftOrAffine, 0.35, "An affine cipher is a substitution with only 312 keys — cheap to rule out."));
            }

            return guesses.OrderByDescending(g => g.Confidence).ToArray();
        }

        IReadOnlyList<PeriodCandidate> periods = Periodicity.RankPeriods(text, 20, language);
        PeriodCandidate top = periods[0];
        if (top.Period > 1 && top.AverageIoC > english - 0.012)
        {
            guesses.Add(new CipherGuess(CipherFamily.PeriodicPolyalphabetic, 0.85, string.Format(CultureInfo.InvariantCulture, "{0}, but cut into {1} columns each column scores {2:0.0000}: a repeating key of length {1}.", icText, top.Period, top.AverageIoC)));
        }
        else if (ic > 0.042)
        {
            guesses.Add(new CipherGuess(CipherFamily.Digraphic, 0.45, $"{icText}: flattened, but not flat, with no period — consistent with Hill, Playfair or Bifid."));
            guesses.Add(new CipherGuess(CipherFamily.NonPeriodicPolyalphabetic, 0.45, "Or an autokey or running-key cipher, whose key never repeats."));
        }
        else
        {
            guesses.Add(new CipherGuess(CipherFamily.Random, 0.6, $"{icText}: as flat as random letters, with no period. A one-time pad, Enigma, or a modern cipher — brute force is hopeless without a crib or a mistake."));
            guesses.Add(new CipherGuess(CipherFamily.NonPeriodicPolyalphabetic, 0.3, "A long-primer autokey can look like this too."));
        }

        return guesses.OrderByDescending(g => g.Confidence).ToArray();
    }
}
