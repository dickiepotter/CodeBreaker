using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using RP.CodeBreaker.Ciphers;

namespace RP.CodeBreaker.Attacks;

/// <summary>Which repeating-key cipher to attack.</summary>
public enum PolyalphabeticVariant
{
    /// <summary>Vigenère: c = p + k.</summary>
    Vigenere,

    /// <summary>Beaufort: c = k − p.</summary>
    Beaufort,
}

/// <summary>
/// Breaking a Vigenère (or Beaufort) cipher the Babbage–Kasiski–Friedman way: find the key length,
/// split the text into that many columns, and break each column as a Caesar cipher.
/// </summary>
/// <remarks>
/// <para>
/// This is the purest example in the library of turning a <b>product into a sum</b>. A five-letter key
/// has 26⁵ ≈ 11.9 million possibilities, and every one of them changes the whole decryption. But once
/// we know the length is 5, the first key letter only affects letters 1, 6, 11…, so it can be found on
/// its own by trying 26 shifts on that column; then the second letter; and so on. 26⁵ trials become
/// 5 × 26 = 130.
/// </para>
/// <para>
/// With a crib, it gets better still. Subtracting a known word from the ciphertext at the right place
/// gives the key letters directly — and keys were often words themselves, so the fragment that pops
/// out (say "EMONLE") is recognisable. That is "crib dragging": slide the crib along and look for key
/// fragments that make sense.
/// </para>
/// </remarks>
public static class VigenereAttack
{
    /// <summary>Break a repeating-key cipher.</summary>
    /// <param name="ciphertext">The ciphertext (non-letters are kept in place in the result).</param>
    /// <param name="options">Cribs, maximum key length, language.</param>
    /// <param name="variant">Vigenère (default) or Beaufort.</param>
    public static BreakResult Break(string ciphertext, AttackOptions? options = null, PolyalphabeticVariant variant = PolyalphabeticVariant.Vigenere)
    {
        options ??= new AttackOptions();
        var watch = Stopwatch.StartNew();
        LanguageModel language = options.Language;
        string text = Letters.Normalise(ciphertext);
        int[] cipher = Letters.ToNumbers(text);
        string name = variant == PolyalphabeticVariant.Vigenere ? "Vigenère" : "Beaufort";
        var log = new List<string>();
        var difficulty = new Difficulty(VigenereCipher.KeySpaceUpTo(options.MaxKeyLength));

        IReadOnlyList<PeriodCandidate> periods = Periodicity.RankPeriods(text, options.MaxKeyLength, language);
        double friedman = Periodicity.FriedmanEstimate(text, language);
        log.Add(string.Format(CultureInfo.InvariantCulture, "Index of coincidence {0:0.0000} (English {1:0.0000}, random {2:0.0000}); Friedman estimates a key of about {3:0.0} letters.", FrequencyAnalysis.IndexOfCoincidence(text), language.ExpectedIndexOfCoincidence, 1.0 / 26, friedman));
        foreach (PeriodCandidate p in periods.Take(4))
        {
            log.Add(string.Format(CultureInfo.InvariantCulture, "  Period {0,2}: column IoC {1:0.0000}, Kasiski votes {2}", p.Period, p.AverageIoC, p.KasiskiVotes));
        }

        long tried = 0;
        (string Key, int[] Plain, double Score, int Period, int KnownFromCrib)? best = null;
        // Statistics alone pick the period well for long texts; with a crib, every period is cheap to
        // test (the crib must give a self-consistent key), and short texts need that.
        IEnumerable<PeriodCandidate> toTry = options.NormalisedCribs.Count > 0 ? periods.OrderBy(p => p.Period) : periods.Take(3);
        foreach (PeriodCandidate p in toTry)
        {
            int L = p.Period;
            // Score every starting key cheaply, then polish only the most promising few.
            var starts = StartingKeys(cipher, L, variant, options, language)
                .Select(s => (s.Key, s.Known, Score: language.QuadgramScore(Decrypt(cipher, s.Key, variant))
                    - (options.ContainsAllCribs(Letters.FromNumbers(Decrypt(cipher, s.Key, variant))) ? 0 : 1000)))
                .OrderByDescending(s => s.Score)
                .Take(3)
                .ToList();
            foreach ((int[] key, int known, _) in starts)
            {
                tried += 26L * L;
                int[] k = Refine(cipher, key, variant, language, ref tried);
                int[] plain = Decrypt(cipher, k, variant);
                double score = language.QuadgramScore(plain);
                if (!options.ContainsAllCribs(Letters.FromNumbers(plain)))
                {
                    score -= 1000; // a decryption without the expected words is very unlikely to be right
                }

                // A longer key can always fit at least as well (a key of 12 can mimic one of 6), so
                // demand a real improvement before preferring it.
                if (best is null || score > best.Value.Score + (L > best.Value.Period ? 2.0 : 0))
                {
                    best = (Letters.FromNumbers(k), plain, score, L, known);
                }
            }
        }

        (string bestKey, int[] bestPlain, double bestScore, int period, int knownFromCrib) = best!.Value;
        PeriodCandidate chosen = periods.First(x => x.Period == period);
        difficulty.Then(
            $"Key length is {period}",
            BigInteger.Pow(26, period),
            string.Format(CultureInfo.InvariantCulture, "With {0} columns each column's IoC is {1:0.000}, close to English; Kasiski repeats agree ({2} votes).", period, chosen.AverageIoC, chosen.KasiskiVotes));
        if (knownFromCrib > 0)
        {
            difficulty.Then(
                $"Crib reveals {knownFromCrib} of the {period} key letters",
                BigInteger.Pow(26, period - knownFromCrib),
                "Ciphertext minus the expected word gives the key letters under it directly.");
        }

        difficulty.Then(
            $"Each column is a Caesar cipher",
            26 * Math.Max(1, period - knownFromCrib),
            "A key letter only affects its own column, so the letters are found one at a time: a sum of 26s, not a product.");
        log.Add($"Key {bestKey} (length {period}).");

        ICipher found = variant == PolyalphabeticVariant.Vigenere ? new VigenereCipher(bestKey) : new BeaufortCipher(bestKey);
        string plaintext = found.Decrypt(ciphertext);
        return new BreakResult
        {
            CipherName = name,
            Key = bestKey,
            Plaintext = plaintext,
            Score = bestScore,
            EnglishLikeness = language.QuadgramScorePerLetter(plaintext),
            KeysTried = tried,
            Elapsed = watch.Elapsed,
            Difficulty = difficulty,
            Candidates = [new Candidate(bestKey, plaintext, bestScore)],
            Log = log,
        };
    }

    /// <summary>
    /// Crib dragging: slide <paramref name="crib"/> along the ciphertext and show, at each position,
    /// the key letters it would imply. When the key is a word, the right position stands out to the eye.
    /// </summary>
    public static IReadOnlyList<(int Position, string KeyFragment, double Readability)> DragCrib(string ciphertext, string crib, LanguageModel? language = null)
    {
        language ??= LanguageModel.English;
        int[] c = Letters.ToNumbers(ciphertext);
        int[] p = Letters.ToNumbers(crib);
        var result = new List<(int, string, double)>();
        for (int i = 0; i + p.Length <= c.Length; i++)
        {
            string fragment = Letters.FromNumbers(p.Select((x, j) => c[i + j] - x));
            result.Add((i, fragment, language.QuadgramScorePerLetter(fragment)));
        }

        return result;
    }

    /// <summary>The key a column's letter frequencies suggest, plus (with cribs) keys built from crib placements.</summary>
    private static IEnumerable<(int[] Key, int Known)> StartingKeys(int[] cipher, int period, PolyalphabeticVariant variant, AttackOptions options, LanguageModel language)
    {
        int[] byFrequency = new int[period];
        for (int j = 0; j < period; j++)
        {
            byFrequency[j] = BestShift(cipher, period, j, variant, language);
        }

        yield return (byFrequency, 0);

        foreach (string crib in options.NormalisedCribs)
        {
            int[] p = Letters.ToNumbers(crib);
            var seen = new HashSet<string>();
            for (int at = 0; at + p.Length <= cipher.Length; at++)
            {
                int[] key = (int[])byFrequency.Clone();
                var known = new int?[period];
                bool consistent = true;
                for (int t = 0; t < p.Length && consistent; t++)
                {
                    int slot = (at + t) % period;
                    int k = variant == PolyalphabeticVariant.Vigenere
                        ? Letters.Mod(cipher[at + t] - p[t], 26)
                        : Letters.Mod(cipher[at + t] + p[t], 26);
                    if (known[slot] is int existing && existing != k)
                    {
                        consistent = false;
                    }

                    known[slot] = k;
                    key[slot] = k;
                }

                if (consistent && seen.Add(Letters.FromNumbers(key)))
                {
                    yield return (key, known.Count(k => k is not null));
                }
            }
        }
    }

    private static int BestShift(int[] cipher, int period, int column, PolyalphabeticVariant variant, LanguageModel language)
    {
        var counts = new int[26];
        int best = 0;
        double bestChi = double.MaxValue;
        for (int k = 0; k < 26; k++)
        {
            Array.Clear(counts);
            for (int i = column; i < cipher.Length; i += period)
            {
                counts[DecryptLetter(cipher[i], k, variant)]++;
            }

            double chi = FrequencyAnalysis.ChiSquared(counts, language);
            if (chi < bestChi)
            {
                bestChi = chi;
                best = k;
            }
        }

        return best;
    }

    /// <summary>Polish each key letter in turn against the full-text quadgram score (helps short texts).</summary>
    private static int[] Refine(int[] cipher, int[] key, PolyalphabeticVariant variant, LanguageModel language, ref long tried)
    {
        int[] k = (int[])key.Clone();
        double score = language.QuadgramScore(Decrypt(cipher, k, variant));
        for (int pass = 0; pass < 2; pass++)
        {
            for (int j = 0; j < k.Length; j++)
            {
                int keep = k[j];
                for (int s = 0; s < 26; s++)
                {
                    k[j] = s;
                    double t = language.QuadgramScore(Decrypt(cipher, k, variant));
                    tried++;
                    if (t > score)
                    {
                        score = t;
                        keep = s;
                    }
                }

                k[j] = keep;
            }
        }

        return k;
    }

    private static int[] Decrypt(int[] cipher, int[] key, PolyalphabeticVariant variant)
    {
        var plain = new int[cipher.Length];
        for (int i = 0; i < cipher.Length; i++)
        {
            plain[i] = DecryptLetter(cipher[i], key[i % key.Length], variant);
        }

        return plain;
    }

    private static int DecryptLetter(int c, int k, PolyalphabeticVariant variant) =>
        variant == PolyalphabeticVariant.Vigenere ? Letters.Mod(c - k, 26) : Letters.Mod(k - c, 26);
}

/// <summary>
/// Breaking an autokey cipher: the key never repeats, so there is no period to find — but only the
/// primer is secret. Guess its length, then find its letters one at a time by how English the
/// letters they unlock look.
/// </summary>
/// <remarks>
/// With primer length L, plaintext letter i + L is decrypted using plaintext letter i. So primer letter
/// j alone determines plaintext letters j, j + L, j + 2L, … — a chain. Each chain can be scored on its
/// own (by letter frequencies), so a primer of L letters costs 26 × L trials, not 26^L. The same "make
/// it a sum" trick as Vigenère, reached by a different road.
/// </remarks>
public static class AutokeyAttack
{
    /// <summary>Break an autokey cipher.</summary>
    public static BreakResult Break(string ciphertext, AttackOptions? options = null)
    {
        options ??= new AttackOptions();
        var watch = Stopwatch.StartNew();
        LanguageModel language = options.Language;
        int[] cipher = Letters.ToNumbers(ciphertext);
        var log = new List<string>();
        long tried = 0;
        (string Primer, double Score)? best = null;
        for (int L = 1; L <= Math.Min(options.MaxKeyLength, Math.Max(1, cipher.Length / 3)); L++)
        {
            var primer = new int[L];
            for (int j = 0; j < L; j++)
            {
                double bestChi = double.MaxValue;
                for (int s = 0; s < 26; s++)
                {
                    tried++;
                    var counts = new int[26];
                    int prev = s;
                    for (int i = j; i < cipher.Length; i += L)
                    {
                        int p = Letters.Mod(cipher[i] - prev, 26);
                        counts[p]++;
                        prev = p;
                    }

                    double chi = FrequencyAnalysis.ChiSquared(counts, language);
                    if (chi < bestChi)
                    {
                        bestChi = chi;
                        primer[j] = s;
                    }
                }
            }

            string guess = Letters.FromNumbers(primer);
            string plain = new AutokeyCipher(guess).Decrypt(Letters.Normalise(ciphertext));
            double score = language.QuadgramScorePerLetter(plain) + (options.ContainsAllCribs(plain) ? 0 : -1);
            log.Add(string.Format(CultureInfo.InvariantCulture, "Primer length {0,2}: {1,-12} score {2:0.00}", L, guess, score));
            if (best is null || score > best.Value.Score + 0.05)
            {
                best = (guess, score);
            }
        }

        string key = best!.Value.Primer;
        string plaintext = new AutokeyCipher(key).Decrypt(ciphertext);
        var difficulty = new Difficulty(VigenereCipher.KeySpaceUpTo(options.MaxKeyLength))
            .Then($"Try each primer length 1–{options.MaxKeyLength}", BigInteger.Pow(26, key.Length), $"The best-scoring length was {key.Length}.")
            .Then("Each primer letter starts its own chain", 26 * key.Length, "Primer letter j alone decides plaintext letters j, j+L, j+2L… so each is found separately.");
        return new BreakResult
        {
            CipherName = "Autokey",
            Key = key,
            Plaintext = plaintext,
            Score = best.Value.Score,
            EnglishLikeness = language.QuadgramScorePerLetter(plaintext),
            KeysTried = tried,
            Elapsed = watch.Elapsed,
            Difficulty = difficulty,
            Candidates = [new Candidate(key, plaintext, best.Value.Score)],
            Log = log,
        };
    }
}
