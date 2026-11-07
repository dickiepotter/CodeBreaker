using System.Diagnostics;
using RP.CodeBreaker.Ciphers;

namespace RP.CodeBreaker.Attacks;

/// <summary>
/// Breaking Playfair by <b>simulated annealing</b>: a hill climb that is allowed to go downhill
/// sometimes, less and less often as it "cools".
/// </summary>
/// <remarks>
/// <para>
/// Playfair's landscape is rougher than a simple substitution's. Swapping two letters in the grid
/// changes every digraph that touches either of them, and the rules (same row, same column,
/// rectangle) mean a nearly-right grid can still produce nonsense in places. A pure hill climb gets
/// stuck on foothills almost every time.
/// </para>
/// <para>
/// Simulated annealing borrows from metalwork. At a high "temperature" the search accepts many changes
/// that make the score worse, so it roams widely. As the temperature falls it becomes pickier, until at
/// the end it only accepts improvements. A change that worsens the score by d is accepted with
/// probability e^(−d/T). That willingness to step back out of a dip is what lets it find the deep valley.
/// </para>
/// <para>
/// The moves are those that make sense for a grid: swap two letters (most of the time), swap two rows
/// or two columns, flip the grid, or reverse it. It needs a couple of hundred letters of ciphertext and
/// typically several hundred thousand to a million trial grids — a few seconds on a laptop, against
/// 24! ≈ 6 × 10²³ keys — and some runs still end in a dead end, which is why it restarts.
/// </para>
/// </remarks>
public static class PlayfairAttack
{
    /// <summary>Break Playfair (synchronously).</summary>
    public static BreakResult Break(string ciphertext, AttackOptions? options = null) =>
        BreakAsync(ciphertext, options).AsTask().GetAwaiter().GetResult();

    /// <summary>Break Playfair, yielding through <see cref="AttackOptions.Breathe"/>.</summary>
    public static async ValueTask<BreakResult> BreakAsync(string ciphertext, AttackOptions? options = null)
    {
        options ??= new AttackOptions();
        var watch = Stopwatch.StartNew();
        LanguageModel language = options.Language;
        string text = PolybiusGrid.Fold(ciphertext);
        if (text.Length % 2 == 1)
        {
            text += "X";
        }

        int[] cipher = text.Select(c => c - 'A').ToArray();
        var random = new Random(options.Seed);
        var log = new List<string>();
        long tried = 0;
        char[]? bestGrid = null;
        double bestScore = double.NegativeInfinity;
        int restarts = Math.Max(1, options.Restarts);
        int[] plain = new int[cipher.Length];
        int steps = 50;
        int perStep = Math.Max(100, options.Iterations / steps);
        double startTemperature = Math.Max(5, 10 + (0.087 * (cipher.Length - 84)));

        for (int r = 0; r < restarts; r++)
        {
            char[] grid = PolybiusGrid.Alphabet25.OrderBy(_ => random.Next()).ToArray();
            double score = Score(cipher, grid, plain, language, options);
            char[] localBest = (char[])grid.Clone();
            double localBestScore = score;
            for (int step = 0; step < steps; step++)
            {
                double t = startTemperature * (1 - ((double)step / steps));
                for (int i = 0; i < perStep; i++)
                {
                    char[] child = Mutate(grid, random);
                    double s = Score(cipher, child, plain, language, options);
                    tried++;
                    double d = s - score;
                    if (d >= 0 || (t > 0 && random.NextDouble() < Math.Exp(d / t)))
                    {
                        grid = child;
                        score = s;
                        if (score > localBestScore)
                        {
                            localBestScore = score;
                            localBest = (char[])grid.Clone();
                        }
                    }
                }

                if (step % 5 == 4)
                {
                    options.Progress?.Report(new AttackProgress(
                        (r + ((step + 1.0) / steps)) / restarts,
                        $"Restart {r + 1}, temperature {t:0.0}",
                        PlayfairCipher.FromGrid(new string(bestScore > localBestScore ? bestGrid! : localBest)).Decrypt(text),
                        Math.Max(bestScore, localBestScore)));
                    await options.PauseAsync().ConfigureAwait(true);
                }
            }

            log.Add($"Restart {r + 1}: best {localBestScore / Math.Max(1, cipher.Length - 3):0.00} per letter with grid {new string(localBest)}");
            if (localBestScore > bestScore)
            {
                bestScore = localBestScore;
                bestGrid = localBest;
            }

            if (language.QuadgramScorePerLetter(PlayfairCipher.FromGrid(new string(bestGrid!)).Decrypt(text)) > -4.6)
            {
                log.Add("That reads as English; stopping early.");
                break;
            }
        }

        var found = PlayfairCipher.FromGrid(new string(bestGrid!));
        string plaintext = found.Decrypt(text);
        var difficulty = new Difficulty(PlayfairCipher.KeySpace)
            .Then($"Simulated annealing, {tried:N0} trial grids", 1, "Moves that improve the text are always kept; worse ones sometimes, less often as the search cools — so it can climb out of dead ends.");
        log.Add("Equivalent grids (rows or columns rotated) give the same cipher, so the grid found may be a rotation of the one used.");
        return new BreakResult
        {
            CipherName = "Playfair",
            Key = found.Grid.Letters,
            Plaintext = plaintext,
            Score = bestScore,
            EnglishLikeness = language.QuadgramScorePerLetter(plaintext),
            KeysTried = tried,
            Elapsed = watch.Elapsed,
            Difficulty = difficulty,
            Candidates = [new Candidate(found.Grid.Letters, plaintext, bestScore)],
            Log = log,
        };
    }

    private static double Score(int[] cipher, char[] grid, int[] plain, LanguageModel language, AttackOptions options)
    {
        Span<int> row = stackalloc int[26];
        Span<int> col = stackalloc int[26];
        for (int i = 0; i < 25; i++)
        {
            row[grid[i] - 'A'] = i / 5;
            col[grid[i] - 'A'] = i % 5;
        }

        for (int i = 0; i + 1 < cipher.Length; i += 2)
        {
            int a = cipher[i], b = cipher[i + 1];
            int r1 = row[a], c1 = col[a], r2 = row[b], c2 = col[b];
            if (r1 == r2)
            {
                plain[i] = grid[(r1 * 5) + ((c1 + 4) % 5)] - 'A';
                plain[i + 1] = grid[(r2 * 5) + ((c2 + 4) % 5)] - 'A';
            }
            else if (c1 == c2)
            {
                plain[i] = grid[(((r1 + 4) % 5) * 5) + c1] - 'A';
                plain[i + 1] = grid[(((r2 + 4) % 5) * 5) + c2] - 'A';
            }
            else
            {
                plain[i] = grid[(r1 * 5) + c2] - 'A';
                plain[i + 1] = grid[(r2 * 5) + c1] - 'A';
            }
        }

        double s = language.QuadgramScore(plain);
        if (options.NormalisedCribs.Count > 0 && !options.ContainsAllCribs(Letters.FromNumbers(plain)))
        {
            s -= 50;
        }

        return s;
    }

    private static char[] Mutate(char[] grid, Random random)
    {
        char[] g = (char[])grid.Clone();
        int move = random.Next(50);
        if (move < 45)
        {
            int a = random.Next(25), b = random.Next(25);
            (g[a], g[b]) = (g[b], g[a]);
        }
        else if (move < 47)
        {
            int r1 = random.Next(5), r2 = random.Next(5);
            for (int c = 0; c < 5; c++)
            {
                (g[(r1 * 5) + c], g[(r2 * 5) + c]) = (g[(r2 * 5) + c], g[(r1 * 5) + c]);
            }
        }
        else if (move < 49)
        {
            int c1 = random.Next(5), c2 = random.Next(5);
            for (int r = 0; r < 5; r++)
            {
                (g[(r * 5) + c1], g[(r * 5) + c2]) = (g[(r * 5) + c2], g[(r * 5) + c1]);
            }
        }
        else
        {
            Array.Reverse(g);
        }

        return g;
    }
}
