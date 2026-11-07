using System.Diagnostics;
using System.Numerics;
using RP.CodeBreaker.Ciphers;

namespace RP.CodeBreaker.Attacks;

/// <summary>
/// Breaking a columnar transposition: for each possible number of columns, find the column order
/// that makes the text most English — by trying every order when there are few columns, and by hill
/// climbing (swapping columns) when there are many.
/// </summary>
/// <remarks>
/// <para>
/// A transposition is spotted before any work is done: its letter frequencies are exactly English
/// (IoC ≈ 0.066, chi-squared low), because no letter has been changed, only moved.
/// </para>
/// <para>
/// Up to eight columns there are at most 8! = 40,320 orders, so every one can be tried. Beyond that the
/// factorial runs away — 12! is 479 million, 20! is 2.4 × 10¹⁸ — and the attack switches to a hill
/// climb. It works for the same reason as the substitution attack: an order with most columns in the
/// right place produces text with long readable stretches, and the quadgram score rewards it.
/// </para>
/// </remarks>
public static class ColumnarAttack
{
    /// <summary>The largest column count searched exhaustively.</summary>
    public const int ExhaustiveLimit = 8;

    /// <summary>Break a columnar transposition (synchronously).</summary>
    public static BreakResult Break(string ciphertext, AttackOptions? options = null) =>
        BreakAsync(ciphertext, options).AsTask().GetAwaiter().GetResult();

    /// <summary>Break a columnar transposition, yielding through <see cref="AttackOptions.Breathe"/>.</summary>
    public static async ValueTask<BreakResult> BreakAsync(string ciphertext, AttackOptions? options = null)
    {
        options ??= new AttackOptions();
        var watch = Stopwatch.StartNew();
        LanguageModel language = options.Language;
        int[] cipher = Letters.ToNumbers(ciphertext);
        int maxColumns = Math.Min(options.MaxKeyLength, Math.Max(2, cipher.Length / 2));
        BigInteger total = 0;
        for (int k = 2; k <= maxColumns; k++)
        {
            total += KeySpace.FactorialOf(k);
        }

        var log = new List<string>();
        var difficulty = new Difficulty(KeySpace.Of(total, $"every order of 2–{maxColumns} columns"));
        var random = new Random(options.Seed);
        long tried = 0;
        (int[] Order, double Score)? best = null;
        int[] plain = new int[cipher.Length];
        for (int k = 2; k <= maxColumns; k++)
        {
            (int[] order, double score, long evaluations) = k <= ExhaustiveLimit
                ? Exhaustive(cipher, k, language, options, plain)
                : Climb(cipher, k, language, options, random, plain);
            tried += evaluations;
            log.Add($"{k,2} columns ({(k <= ExhaustiveLimit ? "all " + KeySpace.Format(KeySpace.FactorialOf(k)) + " orders" : "hill climb")}): best {score / Math.Max(1, cipher.Length - 3):0.00} per letter — {BruteForce.Preview(ColumnarTranspositionCipher.Decrypt(Letters.FromNumbers(cipher), order), 40)}");
            if (best is null || score > best.Value.Score)
            {
                best = (order, score);
            }

            options.Progress?.Report(new AttackProgress((double)(k - 1) / (maxColumns - 1), $"{k} columns", ColumnarTranspositionCipher.Decrypt(Letters.FromNumbers(cipher), best.Value.Order), best.Value.Score));
            await options.PauseAsync().ConfigureAwait(true);
        }

        int[] bestOrder = best!.Value.Order;
        var found = new ColumnarTranspositionCipher(bestOrder);
        string plaintext = found.Decrypt(ciphertext);
        int columns = bestOrder.Length;
        difficulty.Then($"Key has {columns} columns", KeySpace.FactorialOf(columns), "The column count whose best order reads most like English.");
        if (columns > ExhaustiveLimit)
        {
            difficulty.Then("Hill climb on column swaps", 1, $"Too many orders to try; a climb that keeps improving swaps found it in {tried:N0} trials.");
        }
        else
        {
            difficulty.Then("Every order tried and scored", 1, "Small enough to search completely.");
        }

        return new BreakResult
        {
            CipherName = "Columnar transposition",
            Key = found.KeyDescription,
            Plaintext = plaintext,
            Score = best.Value.Score,
            EnglishLikeness = language.QuadgramScorePerLetter(plaintext),
            KeysTried = tried,
            Elapsed = watch.Elapsed,
            Difficulty = difficulty,
            Candidates = [new Candidate(found.KeyDescription, plaintext, best.Value.Score)],
            Log = log,
        };
    }

    private static double Score(int[] cipher, int[] order, LanguageModel language, AttackOptions options, int[] plain)
    {
        ColumnarTranspositionCipher.DecryptInto(cipher, order, plain);
        double s = language.QuadgramScore(plain);
        if (options.NormalisedCribs.Count > 0 && !options.ContainsAllCribs(Letters.FromNumbers(plain)))
        {
            s -= 1000;
        }

        return s;
    }

    /// <summary>Every permutation of k columns, by Heap's algorithm.</summary>
    private static (int[] Order, double Score, long Evaluations) Exhaustive(int[] cipher, int k, LanguageModel language, AttackOptions options, int[] plain)
    {
        int[] a = Enumerable.Range(0, k).ToArray();
        int[] c = new int[k];
        int[] best = (int[])a.Clone();
        double bestScore = Score(cipher, a, language, options, plain);
        long evaluations = 1;
        int i = 0;
        while (i < k)
        {
            if (c[i] < i)
            {
                int j = i % 2 == 0 ? 0 : c[i];
                (a[j], a[i]) = (a[i], a[j]);
                double s = Score(cipher, a, language, options, plain);
                evaluations++;
                if (s > bestScore)
                {
                    bestScore = s;
                    best = (int[])a.Clone();
                }

                c[i]++;
                i = 0;
            }
            else
            {
                c[i] = 0;
                i++;
            }
        }

        return (best, bestScore, evaluations);
    }

    /// <summary>
    /// Iterated local search: climb to a local best, then "kick" it (a few random changes) and climb
    /// again, keeping the kick only if it leads somewhere better. Plain restarts throw away a nearly
    /// right order; kicks keep most of it and shake loose the part that is stuck.
    /// </summary>
    private static (int[] Order, double Score, long Evaluations) Climb(int[] cipher, int k, LanguageModel language, AttackOptions options, Random random, int[] plain)
    {
        int[]? best = null;
        double bestScore = double.NegativeInfinity;
        long evaluations = 0;
        int kicks = 6 * k;
        for (int r = 0; r < Math.Max(1, options.Restarts); r++)
        {
            int[] current = Enumerable.Range(0, k).OrderBy(_ => random.Next()).ToArray();
            (current, double currentScore) = LocalClimb(cipher, current, language, options, plain, ref evaluations);
            for (int kick = 0; kick < kicks; kick++)
            {
                int[] tried = Kick(current, random);
                (tried, double triedScore) = LocalClimb(cipher, tried, language, options, plain, ref evaluations);
                if (triedScore > currentScore)
                {
                    current = tried;
                    currentScore = triedScore;
                }
            }

            if (currentScore > bestScore)
            {
                bestScore = currentScore;
                best = current;
            }
        }

        return (best!, bestScore, evaluations);
    }

    private static int[] Kick(int[] order, Random random)
    {
        int[] o = (int[])order.Clone();
        int k = o.Length;
        if (random.Next(2) == 0)
        {
            // Move a block of columns elsewhere.
            int len = 1 + random.Next(Math.Max(1, k / 3));
            int from = random.Next(k - len + 1);
            var list = o.ToList();
            var block = list.GetRange(from, len);
            list.RemoveRange(from, len);
            list.InsertRange(random.Next(list.Count + 1), block);
            return [.. list];
        }

        for (int i = 0; i < 2; i++)
        {
            int a = random.Next(k), b = random.Next(k);
            (o[a], o[b]) = (o[b], o[a]);
        }

        return o;
    }

    private static (int[] Order, double Score) LocalClimb(int[] cipher, int[] start, LanguageModel language, AttackOptions options, int[] plain, ref long evaluations)
    {
        int k = start.Length;
        int[] order = (int[])start.Clone();
        double score = Score(cipher, order, language, options, plain);
        bool improved = true;
        while (improved)
        {
            improved = false;

            // Swaps of two columns…
            for (int i = 0; i < k; i++)
            {
                for (int j = i + 1; j < k; j++)
                {
                    (order[i], order[j]) = (order[j], order[i]);
                    double s = Score(cipher, order, language, options, plain);
                    evaluations++;
                    if (s > score)
                    {
                        score = s;
                        improved = true;
                    }
                    else
                    {
                        (order[i], order[j]) = (order[j], order[i]);
                    }
                }
            }

            // …and moving one column to another place (a run of correctly-ordered columns can then
            // slide into position together, which no single swap can do).
            for (int from = 0; from < k; from++)
            {
                for (int to = 0; to < k; to++)
                {
                    if (to == from)
                    {
                        continue;
                    }

                    List<int> moved = [.. order];
                    int col = moved[from];
                    moved.RemoveAt(from);
                    moved.Insert(to, col);
                    int[] candidate = [.. moved];
                    double s = Score(cipher, candidate, language, options, plain);
                    evaluations++;
                    if (s > score)
                    {
                        score = s;
                        order = candidate;
                        improved = true;
                    }
                }
            }
        }

        return (order, score);
    }
}
