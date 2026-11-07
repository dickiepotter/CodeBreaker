namespace RP.CodeBreaker;

/// <summary>A sequence of letters that appears more than once in a ciphertext, and where.</summary>
/// <param name="Sequence">The repeated letters.</param>
/// <param name="Positions">Where each copy starts (letter index).</param>
/// <param name="Spacings">The distances between successive copies.</param>
public sealed record Repeat(string Sequence, IReadOnlyList<int> Positions, IReadOnlyList<int> Spacings);

/// <summary>A candidate key length (period) and the evidence for it.</summary>
/// <param name="Period">The key length.</param>
/// <param name="AverageIoC">The mean index of coincidence of the columns this period makes.</param>
/// <param name="KasiskiVotes">How many repeat spacings it divides.</param>
public sealed record PeriodCandidate(int Period, double AverageIoC, int KasiskiVotes);

/// <summary>
/// Finding the length of a repeating key — the first and hardest step against Vigenère-type ciphers,
/// and the reason they fell.
/// </summary>
/// <remarks>
/// <para>
/// A Vigenère key of length L means every L-th letter is shifted by the same amount. Cut the
/// ciphertext into L columns and each column is a plain Caesar cipher, which frequency analysis breaks
/// at once. So everything rests on finding L, and there are two classic ways:
/// </para>
/// <list type="bullet">
/// <item><b>Kasiski (1863)</b>: when a common word like THE happens to line up with the same part of
/// the key twice, it encrypts the same way twice. The distance between the repeats is then a
/// multiple of L, so the factors that keep turning up in the distances point at L.</item>
/// <item><b>Friedman's index of coincidence (1922)</b>: guess L, cut the columns, and measure each
/// column's IoC. With the right L each column is a Caesar cipher of English, IoC ≈ 0.066; with the
/// wrong L the columns are a mix of shifts, IoC ≈ 0.04. Try every L and the right one stands out.</item>
/// </list>
/// </remarks>
public static class Periodicity
{
    /// <summary>
    /// Kasiski examination: every letter sequence of at least <paramref name="minLength"/> letters
    /// that appears more than once, with the gaps between appearances.
    /// </summary>
    public static IReadOnlyList<Repeat> Repeats(string ciphertext, int minLength = 3, int maxLength = 8)
    {
        string text = Letters.Normalise(ciphertext);
        var found = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (int len = minLength; len <= maxLength; len++)
        {
            var seen = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            for (int i = 0; i + len <= text.Length; i++)
            {
                string s = text.Substring(i, len);
                if (!seen.TryGetValue(s, out List<int>? list))
                {
                    seen[s] = list = [];
                }

                list.Add(i);
            }

            foreach (var kv in seen.Where(kv => kv.Value.Count > 1))
            {
                found[kv.Key] = kv.Value;
            }
        }

        // Keep only maximal repeats: drop "THE" if it only ever appears inside a repeated "THEM".
        var result = new List<Repeat>();
        foreach (var kv in found.OrderByDescending(kv => kv.Key.Length).ThenBy(kv => kv.Value[0]))
        {
            bool inside = result.Any(r => r.Sequence.Contains(kv.Key, StringComparison.Ordinal)
                && r.Positions.Count == kv.Value.Count);
            if (inside)
            {
                continue;
            }

            var spacings = new List<int>();
            for (int i = 1; i < kv.Value.Count; i++)
            {
                spacings.Add(kv.Value[i] - kv.Value[i - 1]);
            }

            result.Add(new Repeat(kv.Key, kv.Value, spacings));
        }

        return result.OrderByDescending(r => r.Sequence.Length).ThenBy(r => r.Positions[0]).ToArray();
    }

    /// <summary>
    /// For each possible key length 2 … <paramref name="maxPeriod"/>, how many Kasiski spacings it
    /// divides exactly. The true key length and its multiples collect the most votes.
    /// </summary>
    public static IReadOnlyDictionary<int, int> KasiskiVotes(string ciphertext, int maxPeriod = 20)
    {
        var votes = Enumerable.Range(2, Math.Max(0, maxPeriod - 1)).ToDictionary(p => p, _ => 0);
        foreach (Repeat r in Repeats(ciphertext))
        {
            foreach (int s in r.Spacings)
            {
                foreach (int p in votes.Keys.ToArray())
                {
                    if (s % p == 0)
                    {
                        votes[p]++;
                    }
                }
            }
        }

        return votes;
    }

    /// <summary>
    /// Split a text into <paramref name="period"/> columns: column j holds letters j, j + period,
    /// j + 2·period… — the letters a Vigenère key of that length shifts by the same amount.
    /// </summary>
    public static string[] Columns(string text, int period)
    {
        string clean = Letters.Normalise(text);
        var columns = new System.Text.StringBuilder[period];
        for (int j = 0; j < period; j++)
        {
            columns[j] = new System.Text.StringBuilder();
        }

        for (int i = 0; i < clean.Length; i++)
        {
            columns[i % period].Append(clean[i]);
        }

        return columns.Select(c => c.ToString()).ToArray();
    }

    /// <summary>The mean index of coincidence of the columns a given period makes.</summary>
    public static double AverageColumnIoC(string text, int period) =>
        Columns(text, period).Average(FrequencyAnalysis.IndexOfCoincidence);

    /// <summary>
    /// Friedman's estimate of the key length from the whole text's IoC alone — rough, but it needs
    /// no guessing: L ≈ (κp − κr)·N / ((N − 1)·IoC − κr·N + κp), where κp ≈ 0.066 is English's IoC,
    /// κr = 1/26 is random letters' and N is the length. With English numbers that is the familiar
    /// L ≈ 0.027·N / ((N − 1)·IoC − 0.038·N + 0.066).
    /// </summary>
    public static double FriedmanEstimate(string ciphertext, LanguageModel? language = null)
    {
        language ??= LanguageModel.English;
        double n = Letters.Normalise(ciphertext).Length;
        double ic = FrequencyAnalysis.IndexOfCoincidence(ciphertext);
        double kp = language.ExpectedIndexOfCoincidence;
        const double Kr = 1.0 / 26;
        double denominator = ((n - 1) * ic) - (Kr * n) + kp;
        return denominator <= 0 ? double.PositiveInfinity : (kp - Kr) * n / denominator;
    }

    /// <summary>
    /// Every key length from 1 to <paramref name="maxPeriod"/>, ranked by how English-like its
    /// columns are. The first entry is the best guess.
    /// </summary>
    /// <remarks>
    /// Multiples of the true period (10 and 15 when it is 5) score just as well, because their columns
    /// are also single Caesar shifts. So the ranking prefers the <i>smallest</i> period whose IoC is
    /// close to the best — otherwise the attack would happily choose a key twice as long as needed.
    /// </remarks>
    public static IReadOnlyList<PeriodCandidate> RankPeriods(string ciphertext, int maxPeriod = 20, LanguageModel? language = null)
    {
        language ??= LanguageModel.English;
        string text = Letters.Normalise(ciphertext);
        maxPeriod = Math.Max(1, Math.Min(maxPeriod, text.Length / 4));
        IReadOnlyDictionary<int, int> votes = KasiskiVotes(text, Math.Max(2, maxPeriod));
        var candidates = Enumerable.Range(1, maxPeriod)
            .Select(p => new PeriodCandidate(p, AverageColumnIoC(text, p), votes.TryGetValue(p, out int v) ? v : 0))
            .ToList();

        double best = candidates.Max(c => c.AverageIoC);
        double random = 1.0 / 26;
        double threshold = random + (0.85 * (best - random));
        return candidates
            .OrderByDescending(c => c.AverageIoC >= threshold)
            .ThenBy(c => c.AverageIoC >= threshold ? c.Period : 0)
            .ThenByDescending(c => c.AverageIoC)
            .ToArray();
    }
}
