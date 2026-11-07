using System.Diagnostics;
using System.Numerics;
using RP.CodeBreaker.Ciphers;

namespace RP.CodeBreaker.Attacks;

/// <summary>
/// Breaking a simple substitution cipher by <b>hill climbing</b>: start from a guess at the key, swap
/// two letters, keep the swap if the decryption looks more English, and repeat until no swap helps.
/// </summary>
/// <remarks>
/// <para>
/// There are 26! ≈ 4 × 10²⁶ keys; at ten million a second, trying them all would take over a billion
/// years. But the key isn't a single lock that is either open or shut. A key with 20 letters right
/// produces text that is <i>mostly</i> readable, and its quadgram score shows it. So the search can
/// improve a key a little at a time — the way you'd solve a cryptogram by hand, pencilling in a
/// letter, seeing if the words get better, rubbing it out if not.
/// </para>
/// <para>
/// A hill climb can get stuck on a "foothill" — a key that no single swap improves but which is still
/// wrong. So it restarts several times from different starting keys and keeps the best. The first
/// start is the frequency-matched key (commonest ciphertext letter = E, and so on), which is usually
/// already close.
/// </para>
/// <para>
/// <b>Cribs.</b> An expected word can only sit where the ciphertext has the same letter pattern (see
/// <see cref="WordPatterns"/>). Once placed, every letter of the crib is a fixed part of the key, the
/// climb only swaps the others, and the key space falls from 26! to (26 − k)!.
/// </para>
/// </remarks>
public static class SubstitutionAttack
{
    /// <summary>Break a substitution cipher (synchronously).</summary>
    public static BreakResult Break(string ciphertext, AttackOptions? options = null) =>
        BreakAsync(ciphertext, options).AsTask().GetAwaiter().GetResult();

    /// <summary>Break a substitution cipher, yielding now and then through <see cref="AttackOptions.Breathe"/>.</summary>
    public static async ValueTask<BreakResult> BreakAsync(string ciphertext, AttackOptions? options = null)
    {
        options ??= new AttackOptions();
        var watch = Stopwatch.StartNew();
        LanguageModel language = options.Language;
        int[] cipher = Letters.ToNumbers(ciphertext);
        var log = new List<string>();
        var difficulty = new Difficulty(SubstitutionCipher.KeySpace);

        // Work out which key letters the cribs pin down, for each way of placing them.
        List<int[]> constraintSets = PlaceCribs(ciphertext, options.NormalisedCribs, log);
        if (options.NormalisedCribs.Count > 0 && constraintSets.Count == 0)
        {
            log.Add("No crib could be placed consistently; ignoring the cribs.");
        }

        if (constraintSets.Count == 0)
        {
            constraintSets.Add(Unconstrained());
        }

        int fixedLetters = constraintSets.Min(c => c.Count(x => x >= 0));
        if (fixedLetters > 0)
        {
            difficulty.Then(
                $"Cribs fix {fixedLetters} letters of the key",
                constraintSets.Count * SubstitutionCipher.KeysWithKnownLetters(fixedLetters),
                "A crib can only sit where the ciphertext repeats letters in the same pattern; once placed, its letters are known.");
        }

        var random = new Random(options.Seed);
        int[]? bestKey = null;
        double bestScore = double.NegativeInfinity;
        long tried = 0;
        int restarts = Math.Max(1, options.Restarts);
        int totalRuns = restarts * constraintSets.Count;
        int run = 0;
        foreach (int[] constraint in constraintSets)
        {
            for (int r = 0; r < restarts; r++)
            {
                int[] start = r == 0 ? FrequencyStart(ciphertext, language, constraint) : RandomStart(random, constraint);
                (int[] key, double score, long evaluations) = Climb(cipher, start, constraint, language);
                tried += evaluations;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestKey = key;
                    log.Add($"Restart {run + 1}: new best score {score:0.0} — {BruteForce.Preview(Letters.FromNumbers(Decrypt(cipher, key)))}");
                }

                run++;
                options.Progress?.Report(new AttackProgress((double)run / totalRuns, $"Hill climb {run} of {totalRuns}", Letters.FromNumbers(Decrypt(cipher, bestKey!)), bestScore));
                await options.PauseAsync().ConfigureAwait(true);
            }
        }

        var cipherFound = SubstitutionCipher.FromPlainAlphabet(Letters.FromNumbers(bestKey!));
        string plaintext = cipherFound.Decrypt(ciphertext);
        difficulty.Then(
            $"Hill climb: {restarts} restart(s) × ~{tried / Math.Max(1, totalRuns):N0} trial keys",
            1,
            "Each swap that makes the text more English is kept; the climb reaches the answer without visiting more than a few thousand keys.");
        log.Add($"Tried {tried:N0} keys in all — a {KeySpace.Format(SubstitutionCipher.KeySpace.Size / BigInteger.Max(1, tried))}th of the key space.");

        return new BreakResult
        {
            CipherName = "Simple substitution",
            Key = $"{cipherFound.CipherAlphabet} (plain A–Z → cipher)",
            Plaintext = plaintext,
            Score = bestScore,
            EnglishLikeness = language.QuadgramScorePerLetter(plaintext),
            KeysTried = tried,
            Elapsed = watch.Elapsed,
            Difficulty = difficulty,
            Candidates = [new Candidate(cipherFound.CipherAlphabet, plaintext, bestScore)],
            Log = log,
        };
    }

    /// <summary>
    /// One hill climb: try every swap of two unfixed key letters, keep improvements, repeat until a
    /// full pass finds none.
    /// </summary>
    /// <param name="cipher">The ciphertext letters.</param>
    /// <param name="start">The starting decryption key: start[c] is the plaintext letter for cipher letter c.</param>
    /// <param name="constraint">constraint[c] ≥ 0 pins cipher letter c to that plaintext letter.</param>
    /// <param name="language">The language model.</param>
    internal static (int[] Key, double Score, long Evaluations) Climb(int[] cipher, int[] start, int[] constraint, LanguageModel language)
    {
        int[] key = (int[])start.Clone();
        int[] plain = Decrypt(cipher, key);
        double score = language.QuadgramScore(plain);
        long evaluations = 1;
        int[] free = Enumerable.Range(0, 26).Where(c => constraint[c] < 0).ToArray();
        bool improved = true;
        while (improved)
        {
            improved = false;
            for (int i = 0; i < free.Length; i++)
            {
                for (int j = i + 1; j < free.Length; j++)
                {
                    int a = free[i], b = free[j];
                    (key[a], key[b]) = (key[b], key[a]);
                    DecryptInto(cipher, key, plain);
                    double s = language.QuadgramScore(plain);
                    evaluations++;
                    if (s > score)
                    {
                        score = s;
                        improved = true;
                    }
                    else
                    {
                        (key[a], key[b]) = (key[b], key[a]);
                    }
                }
            }
        }

        return (key, score, evaluations);
    }

    internal static int[] Decrypt(int[] cipher, int[] key)
    {
        var plain = new int[cipher.Length];
        DecryptInto(cipher, key, plain);
        return plain;
    }

    private static void DecryptInto(int[] cipher, int[] key, int[] plain)
    {
        for (int i = 0; i < cipher.Length; i++)
        {
            plain[i] = key[cipher[i]];
        }
    }

    private static int[] Unconstrained() => Enumerable.Repeat(-1, 26).ToArray();

    private static int[] FrequencyStart(string ciphertext, LanguageModel language, int[] constraint)
    {
        int[] key = FrequencyAnalysis.FrequencyMatchedKey(ciphertext, language).Select(c => c - 'A').ToArray();
        return ApplyConstraint(key, constraint);
    }

    private static int[] RandomStart(Random random, int[] constraint)
    {
        int[] key = Enumerable.Range(0, 26).ToArray();
        for (int i = 25; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (key[i], key[j]) = (key[j], key[i]);
        }

        return ApplyConstraint(key, constraint);
    }

    /// <summary>Force the pinned letters into a key, swapping to keep it a permutation.</summary>
    private static int[] ApplyConstraint(int[] key, int[] constraint)
    {
        for (int c = 0; c < 26; c++)
        {
            if (constraint[c] < 0 || key[c] == constraint[c])
            {
                continue;
            }

            int other = Array.IndexOf(key, constraint[c]);
            (key[c], key[other]) = (key[other], key[c]);
        }

        return key;
    }

    /// <summary>
    /// Every consistent way of placing the cribs (capped), each as a constraint array. A placement is
    /// consistent when no ciphertext letter has to mean two plaintext letters and no plaintext letter
    /// comes from two ciphertext letters.
    /// </summary>
    private static List<int[]> PlaceCribs(string ciphertext, IReadOnlyList<string> cribs, List<string> log)
    {
        const int MaxSets = 6;
        var sets = new List<int[]> { };
        if (cribs.Count == 0)
        {
            return sets;
        }

        string text = Letters.Normalise(ciphertext);
        List<int[]> partial = [Unconstrained()];
        foreach (string crib in cribs.OrderByDescending(c => c.Length))
        {
            IReadOnlyList<int> places = WordPatterns.PlacementsForSubstitution(text, crib);
            int possible = Math.Max(0, text.Length - crib.Length + 1);
            log.Add($"Crib {crib} (pattern {WordPatterns.Pattern(crib)}) fits at {places.Count} of {possible} positions{(places.Count is > 0 and <= 8 ? ": " + string.Join(", ", places) : string.Empty)}.");
            var next = new List<int[]>();
            foreach (int[] p in partial)
            {
                foreach (int at in places)
                {
                    int[]? merged = Merge(p, text.Substring(at, crib.Length), crib);
                    if (merged is not null && !next.Any(n => n.SequenceEqual(merged)))
                    {
                        next.Add(merged);
                    }

                    if (next.Count >= MaxSets)
                    {
                        break;
                    }
                }
            }

            if (next.Count > 0)
            {
                partial = next;
            }
            else
            {
                log.Add($"Crib {crib} could not be placed alongside the others; skipping it.");
            }
        }

        return partial.Where(p => p.Any(x => x >= 0)).ToList();
    }

    private static int[]? Merge(int[] constraint, string cipherPart, string crib)
    {
        int[] result = (int[])constraint.Clone();
        for (int i = 0; i < crib.Length; i++)
        {
            int c = cipherPart[i] - 'A', p = crib[i] - 'A';
            if (result[c] >= 0 && result[c] != p)
            {
                return null;
            }

            int existing = Array.IndexOf(result, p);
            if (existing >= 0 && existing != c)
            {
                return null;
            }

            result[c] = p;
        }

        return result;
    }
}
