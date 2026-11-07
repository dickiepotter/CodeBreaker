using System.Diagnostics;
using RP.CodeBreaker.Ciphers;

namespace RP.CodeBreaker.Attacks;

/// <summary>
/// Breaking the Hill cipher two ways: with a little known plaintext (algebra — solve for the key), or
/// with none (search each row of the decryption matrix on its own).
/// </summary>
/// <remarks>
/// <para>
/// <b>Known plaintext.</b> The cipher is C = K·P (mod 26), with plaintext blocks as the columns of P.
/// Know n blocks of plaintext whose matrix P is invertible and K = C·P⁻¹. For a 2 × 2 key that is just
/// four letters of crib — Hill's cipher gives itself away completely to anyone who can guess a word.
/// </para>
/// <para>
/// <b>Ciphertext only.</b> Decryption is P = D·C. The first row of D makes the first letter of every
/// plaintext pair, and nothing else; the second row makes the second letters. So each row can be found
/// separately by trying its 26² = 676 values and asking which gives English-looking letter frequencies.
/// The search is 2 × 676 = 1,352 trials, not 26⁴ = 456,976.
/// </para>
/// </remarks>
public static class HillAttack
{
    /// <summary>
    /// Recover an n × n key from aligned plaintext and ciphertext (both starting at a block boundary).
    /// Returns null if the known text contains no invertible set of blocks.
    /// </summary>
    public static HillCipher? FromKnownPlaintext(string plaintext, string ciphertext, int n = 2)
    {
        int[] p = Letters.ToNumbers(plaintext);
        int[] c = Letters.ToNumbers(ciphertext);
        int blocks = Math.Min(p.Length, c.Length) / n;
        foreach (int[] chosen in Combinations(blocks, n))
        {
            var pm = new int[n, n];
            var cm = new int[n, n];
            for (int col = 0; col < n; col++)
            {
                for (int row = 0; row < n; row++)
                {
                    pm[row, col] = p[(chosen[col] * n) + row];
                    cm[row, col] = c[(chosen[col] * n) + row];
                }
            }

            int[,]? pInverse = NumberTheory.MatrixInverse(pm, 26);
            if (pInverse is null)
            {
                continue;
            }

            int[,] key = MultiplyMod(cm, pInverse);
            if (HillCipher.IsValidKey(key))
            {
                return new HillCipher(key);
            }
        }

        return null;
    }

    /// <summary>
    /// Break a 2 × 2 Hill cipher. With cribs, each crib is tried at every block boundary as known
    /// plaintext; otherwise the rows of the decryption matrix are searched one at a time.
    /// </summary>
    public static BreakResult Break(string ciphertext, AttackOptions? options = null)
    {
        options ??= new AttackOptions();
        var watch = Stopwatch.StartNew();
        LanguageModel language = options.Language;
        string text = Letters.Normalise(ciphertext);
        if (text.Length % 2 == 1)
        {
            text = text[..^1];
        }

        var log = new List<string>();
        var difficulty = new Difficulty(HillCipher.KeySpace2x2);
        long tried = 0;

        foreach (string crib in options.NormalisedCribs.Where(c => c.Length >= 4))
        {
            var found = new List<(HillCipher Cipher, string Plain, double Score, int At)>();
            for (int at = 0; at + crib.Length <= text.Length; at++)
            {
                // The crib may start mid-block; drop its first letter to realign.
                int skip = at % 2;
                string known = crib[skip..];
                HillCipher? h = FromKnownPlaintext(known, text.Substring(at + skip, known.Length));
                tried++;
                if (h is null)
                {
                    continue;
                }

                string plain = h.Decrypt(text);
                if (plain.Contains(crib, StringComparison.Ordinal))
                {
                    found.Add((h, plain, language.QuadgramScore(plain), at));
                }
            }

            if (found.Count > 0)
            {
                var best = found.OrderByDescending(f => f.Score).First();
                log.Add($"Crib {crib} at position {best.At} solves the key equations: K = C·P⁻¹ = {best.Cipher.KeyDescription}.");
                difficulty.Then($"Crib {crib} as known plaintext", found.Count, $"Each placement gives the key by algebra; {found.Count} placement(s) decrypt consistently.");
                difficulty.Then("Pick the most English", 1, "The decryption that reads best.");
                return Result(best.Cipher, ciphertext, best.Score, tried, watch, difficulty, log, language);
            }

            log.Add($"Crib {crib} gave no consistent key at any position.");
        }

        // Ciphertext only: score each candidate row of the decryption matrix by letter frequencies.
        int[] c = Letters.ToNumbers(text);
        var rowScores = new List<(int A, int B, double Chi)>();
        var counts = new int[26];
        for (int a = 0; a < 26; a++)
        {
            for (int b = 0; b < 26; b++)
            {
                Array.Clear(counts);
                for (int i = 0; i + 1 < c.Length; i += 2)
                {
                    counts[((a * c[i]) + (b * c[i + 1])) % 26]++;
                }

                rowScores.Add((a, b, FrequencyAnalysis.ChiSquared(counts, language)));
                tried++;
            }
        }

        var top = rowScores.OrderBy(r => r.Chi).Take(40).ToList();
        log.Add($"Best rows by letter frequency: {string.Join(", ", top.Take(5).Select(r => $"({r.A},{r.B})"))}");
        difficulty.Then("Each row of the decryption matrix is independent", 2 * 676, "Row 1 makes the first letter of every pair, row 2 the second: search them separately.");

        (int[,] D, double Score)? bestD = null;
        foreach (var r1 in top)
        {
            foreach (var r2 in top)
            {
                var d = new int[,] { { r1.A, r1.B }, { r2.A, r2.B } };
                if (!HillCipher.IsValidKey(d))
                {
                    continue;
                }

                tried++;
                string plain = HillCipher.Multiply(d, c);
                double s = language.QuadgramScore(plain) + (options.ContainsAllCribs(plain) ? 0 : -1000);
                if (bestD is null || s > bestD.Value.Score)
                {
                    bestD = (d, s);
                }
            }
        }

        difficulty.Then("Combine the best rows", 1, $"The {top.Count}×{top.Count} pairings of the best rows, scored on quadgrams.");
        var keyFound = new HillCipher(NumberTheory.MatrixInverse(bestD!.Value.D, 26)!);
        log.Add($"Decryption matrix {HillCipher.FormatMatrix(bestD.Value.D)}, so the key is {keyFound.KeyDescription}.");
        return Result(keyFound, ciphertext, bestD.Value.Score, tried, watch, difficulty, log, language);
    }

    private static BreakResult Result(HillCipher cipher, string ciphertext, double score, long tried, Stopwatch watch, Difficulty difficulty, List<string> log, LanguageModel language)
    {
        string plaintext = cipher.Decrypt(ciphertext);
        return new BreakResult
        {
            CipherName = cipher.Name,
            Key = cipher.KeyDescription,
            Plaintext = plaintext,
            Score = score,
            EnglishLikeness = language.QuadgramScorePerLetter(plaintext),
            KeysTried = tried,
            Elapsed = watch.Elapsed,
            Difficulty = difficulty,
            Candidates = [new Candidate(cipher.KeyDescription, plaintext, score)],
            Log = log,
        };
    }

    private static int[,] MultiplyMod(int[,] a, int[,] b)
    {
        int n = a.GetLength(0);
        var r = new int[n, n];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                int s = 0;
                for (int k = 0; k < n; k++)
                {
                    s += a[i, k] * b[k, j];
                }

                r[i, j] = Letters.Mod(s, 26);
            }
        }

        return r;
    }

    private static IEnumerable<int[]> Combinations(int count, int choose)
    {
        int[] idx = Enumerable.Range(0, choose).ToArray();
        if (choose > count)
        {
            yield break;
        }

        while (true)
        {
            yield return (int[])idx.Clone();
            int i = choose - 1;
            while (i >= 0 && idx[i] == count - choose + i)
            {
                i--;
            }

            if (i < 0)
            {
                yield break;
            }

            idx[i]++;
            for (int j = i + 1; j < choose; j++)
            {
                idx[j] = idx[j - 1] + 1;
            }
        }
    }
}

/// <summary>
/// Breaking a Polybius square: it is a simple substitution wearing digits. Turn each digit pair into a
/// letter (any letter — 11 → A, 12 → B, …) and hand the result to <see cref="SubstitutionAttack"/>.
/// </summary>
/// <remarks>
/// A lovely demonstration that changing the <i>symbols</i> adds no security. The key space is 25!,
/// but once the pairs are relabelled it is exactly the substitution problem, solved the same way.
/// </remarks>
public static class PolybiusAttack
{
    /// <summary>Break a Polybius square cipher given as digit pairs.</summary>
    public static async ValueTask<BreakResult> BreakAsync(string digits, AttackOptions? options = null)
    {
        string d = new(digits.Where(ch => ch >= '1' && ch <= '5').ToArray());
        var letters = new System.Text.StringBuilder();
        for (int i = 0; i + 1 < d.Length; i += 2)
        {
            letters.Append(Letters.ToLetter(((d[i] - '1') * 5) + (d[i + 1] - '1')));
        }

        BreakResult inner = await SubstitutionAttack.BreakAsync(letters.ToString(), options).ConfigureAwait(true);
        var difficulty = new Difficulty(PolybiusCipher.KeySpace)
            .Then("Relabel each digit pair as a letter", SubstitutionCipher.KeySpace.Size, "It is now an ordinary simple substitution (of 25 symbols).");
        foreach (Reduction step in inner.Difficulty.Steps)
        {
            difficulty.Then(step.Insight, step.After, step.Why);
        }

        return new BreakResult
        {
            CipherName = "Polybius square",
            Key = "pairs 11–55 relabelled A–Y, then " + inner.Key,
            Plaintext = inner.Plaintext,
            Score = inner.Score,
            EnglishLikeness = inner.EnglishLikeness,
            KeysTried = inner.KeysTried,
            Elapsed = inner.Elapsed,
            Difficulty = difficulty,
            Candidates = inner.Candidates,
            Log = ["Digit pairs relabelled as letters: " + BruteForce.Preview(letters.ToString()), .. inner.Log],
        };
    }

    /// <summary>Break a Polybius square cipher (synchronously).</summary>
    public static BreakResult Break(string digits, AttackOptions? options = null) =>
        BreakAsync(digits, options).AsTask().GetAwaiter().GetResult();
}
