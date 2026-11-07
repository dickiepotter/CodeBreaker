using System.Diagnostics;
using System.Numerics;

namespace RP.CodeBreaker.Attacks;

/// <summary>
/// The simplest attack of all: try every key, score every decryption for how English it looks, keep
/// the best. It is only possible when the key space is small — but when it is, nothing beats it.
/// </summary>
/// <remarks>
/// <para>
/// The scoring is what makes it automatic. A human glances at 26 Caesar decryptions and spots the one
/// that reads; a computer adds up quadgram log-probabilities (see <see cref="LanguageModel"/>) and
/// picks the highest. For very short texts the letter-count chi-squared is used alongside, because
/// four-letter windows need a few words to say anything.
/// </para>
/// <para>
/// Cribs (expected words) are used as a filter: a key whose decryption doesn't contain every crib is
/// thrown away before scoring. The <see cref="Difficulty"/> records how many keys survived, which shows
/// directly how much a single expected word is worth.
/// </para>
/// </remarks>
public static class BruteForce
{
    /// <summary>Try every key in <paramref name="keys"/>.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="cipherName">The cipher's name, for the result.</param>
    /// <param name="ciphertext">The ciphertext.</param>
    /// <param name="keySpace">The full key space (for the difficulty story).</param>
    /// <param name="keys">Every key to try.</param>
    /// <param name="decrypt">Decrypt the ciphertext under a key.</param>
    /// <param name="describe">Write a key for a person.</param>
    /// <param name="options">Cribs, language and so on.</param>
    public static BreakResult Run<TKey>(
        string cipherName,
        string ciphertext,
        KeySpace keySpace,
        IEnumerable<TKey> keys,
        Func<TKey, string> decrypt,
        Func<TKey, string> describe,
        AttackOptions? options = null)
    {
        options ??= new AttackOptions();
        var watch = Stopwatch.StartNew();
        LanguageModel language = options.Language;
        var log = new List<string> { $"Trying all {KeySpace.Format(keySpace.Size)} keys ({keySpace.Description})." };
        var candidates = new List<Candidate>();
        long tried = 0;
        long survived = 0;
        foreach (TKey key in keys)
        {
            options.Cancellation.ThrowIfCancellationRequested();
            tried++;
            string plain = decrypt(key);
            if (!options.ContainsAllCribs(plain))
            {
                continue;
            }

            survived++;
            candidates.Add(new Candidate(describe(key), plain, Fitness(plain, language)));
        }

        var difficulty = new Difficulty(keySpace);
        if (options.NormalisedCribs.Count > 0)
        {
            string cribs = string.Join(", ", options.NormalisedCribs);
            difficulty.Then($"Plaintext must contain {cribs}", survived, "Keys whose decryption lacks the expected words are impossible.");
            log.Add($"{survived} of {tried} keys give a decryption containing {cribs}.");
        }

        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
        if (candidates.Count == 0)
        {
            log.Add("No key survived. Either the cipher is something else, or a crib is wrong.");
            return new BreakResult
            {
                CipherName = cipherName,
                Key = "(none)",
                Plaintext = string.Empty,
                Score = double.NegativeInfinity,
                EnglishLikeness = language.QuadgramFloor,
                KeysTried = tried,
                Elapsed = watch.Elapsed,
                Difficulty = difficulty,
                Log = log,
            };
        }

        Candidate best = candidates[0];
        difficulty.Then("Rank the rest by English score", 1, "The decryption that reads most like English is taken as the answer.");
        log.Add($"Best key {best.Key}: {Preview(best.Plaintext)}");
        if (candidates.Count > 1)
        {
            log.Add($"Runner-up {candidates[1].Key}: {Preview(candidates[1].Plaintext)}");
        }

        return new BreakResult
        {
            CipherName = cipherName,
            Key = best.Key,
            Plaintext = best.Plaintext,
            Score = best.Score,
            EnglishLikeness = language.QuadgramScorePerLetter(best.Plaintext),
            KeysTried = tried,
            Elapsed = watch.Elapsed,
            Difficulty = difficulty,
            Candidates = candidates.Take(10).ToArray(),
            Log = log,
        };
    }

    /// <summary>
    /// The fitness used to rank decryptions: quadgram score, plus a letter-frequency term that
    /// matters only for very short texts.
    /// </summary>
    public static double Fitness(string plaintext, LanguageModel language)
    {
        int[] n = Letters.ToNumbers(plaintext);
        if (n.Length >= 12)
        {
            return language.QuadgramScore(n);
        }

        // Too short for quadgrams to be reliable: lean on letter frequencies as well.
        return language.QuadgramScore(n) - (FrequencyAnalysis.ChiSquared(plaintext, language) / 10);
    }

    internal static string Preview(string text, int length = 60) =>
        text.Length <= length ? text : text.Substring(0, length) + "…";

    internal static BigInteger Count<T>(IEnumerable<T> items) => items.LongCount();
}
