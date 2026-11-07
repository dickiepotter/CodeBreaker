using RP.CodeBreaker.Ciphers;

namespace RP.CodeBreaker.Attacks;

/// <summary>
/// Breaking a Caesar cipher: try all 26 shifts. The oldest brute-force attack there is, and the
/// clearest demonstration that a cipher with a small key space has no security at all.
/// </summary>
public static class CaesarAttack
{
    /// <summary>Try every shift and return the most English decryption.</summary>
    public static BreakResult Break(string ciphertext, AttackOptions? options = null) =>
        BruteForce.Run(
            "Caesar",
            ciphertext,
            CaesarCipher.KeySpace,
            Enumerable.Range(0, 26),
            s => new CaesarCipher(s).Decrypt(ciphertext),
            s => $"shift {s} (A→{Letters.ToLetter(s)})",
            options);

    /// <summary>
    /// The shift found by frequency alone: the one whose decryption has the lowest chi-squared against
    /// English letter frequencies. This is what you would do by hand — no word list needed.
    /// </summary>
    public static int ShiftByFrequency(string ciphertext, LanguageModel? language = null)
    {
        int[] counts = FrequencyAnalysis.Counts(ciphertext);
        int best = 0;
        double bestChi = double.MaxValue;
        var shifted = new int[26];
        for (int s = 0; s < 26; s++)
        {
            for (int i = 0; i < 26; i++)
            {
                shifted[i] = counts[(i + s) % 26];
            }

            double chi = FrequencyAnalysis.ChiSquared(shifted, language);
            if (chi < bestChi)
            {
                bestChi = chi;
                best = s;
            }
        }

        return best;
    }
}

/// <summary>Breaking an affine cipher: try all 312 (multiplier, shift) pairs.</summary>
public static class AffineAttack
{
    /// <summary>Try every key and return the most English decryption.</summary>
    public static BreakResult Break(string ciphertext, AttackOptions? options = null) =>
        BruteForce.Run(
            "Affine",
            ciphertext,
            AffineCipher.KeySpace,
            AffineCipher.ValidMultipliers.SelectMany(a => Enumerable.Range(0, 26).Select(b => (a, b))),
            k => new AffineCipher(k.a, k.b).Decrypt(ciphertext),
            k => $"a = {k.a}, b = {k.b}",
            options);
}

/// <summary>Breaking a rail fence: try every number of rails.</summary>
public static class RailFenceAttack
{
    /// <summary>Try 2 … n−1 rails and return the most English decryption.</summary>
    public static BreakResult Break(string ciphertext, AttackOptions? options = null)
    {
        int n = Letters.Normalise(ciphertext).Length;
        int max = Math.Max(2, n - 1);
        return BruteForce.Run(
            "Rail fence",
            ciphertext,
            RailFenceCipher.KeySpaceFor(n),
            Enumerable.Range(2, max - 1),
            r => new RailFenceCipher(r).Decrypt(ciphertext),
            r => $"{r} rails",
            options);
    }
}
