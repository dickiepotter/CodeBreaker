using System.Diagnostics;
using System.Globalization;
using System.Text;
using RP.CodeBreaker.Ciphers;

namespace RP.CodeBreaker.Attacks;

/// <summary>
/// Breaking repeating-key XOR — the Vigenère attack, moved from letters to bytes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Key length.</b> Take two blocks of ciphertext one key-length apart. Under the right length both
/// were XORed with the <i>same</i> key bytes, which cancel when the blocks are XORed together, leaving
/// plaintext ⊕ plaintext — and English bytes differ in fewer bits than random ones. So the length with
/// the smallest average Hamming distance (bits that differ, per byte) is probably the key length.
/// </para>
/// <para>
/// <b>Key bytes.</b> Every L-th byte was XORed with the same key byte: a single-byte XOR, broken by
/// trying all 256 values and keeping the one whose output looks most like English text (lots of
/// lower-case letters and spaces, nothing unprintable).
/// </para>
/// </remarks>
public static class XorAttack
{
    /// <summary>Break repeating-key XOR given hexadecimal ciphertext.</summary>
    public static BreakResult BreakHex(string hexCiphertext, int maxKeyLength = 40) =>
        Break(Convert.FromHexString(XorCipher.CleanHex(hexCiphertext)), maxKeyLength);

    /// <summary>Break repeating-key XOR.</summary>
    public static BreakResult Break(byte[] ciphertext, int maxKeyLength = 40)
    {
        var watch = Stopwatch.StartNew();
        var log = new List<string>();
        maxKeyLength = Math.Max(1, Math.Min(maxKeyLength, ciphertext.Length / 4));
        var sizes = Enumerable.Range(1, maxKeyLength)
            .Select(l => (Length: l, Distance: NormalisedDistance(ciphertext, l)))
            .OrderBy(x => x.Distance)
            .ToList();
        foreach (var s in sizes.Take(4))
        {
            log.Add(string.Format(CultureInfo.InvariantCulture, "Key length {0,2}: {1:0.000} differing bits per byte", s.Length, s.Distance));
        }

        long tried = 0;
        var results = new List<(byte[] Key, string Plain, double Score)>();
        foreach (var size in sizes.Take(6))
        {
            var key = new byte[size.Length];
            for (int j = 0; j < size.Length; j++)
            {
                double bestScore = double.NegativeInfinity;
                for (int k = 0; k < 256; k++)
                {
                    tried++;
                    double s = 0;
                    for (int i = j; i < ciphertext.Length; i += size.Length)
                    {
                        s += ByteScore((byte)(ciphertext[i] ^ k));
                    }

                    if (s > bestScore)
                    {
                        bestScore = s;
                        key[j] = (byte)k;
                    }
                }
            }

            byte[] shortest = ShortestPeriod(key);
            byte[] plainBytes = new XorCipher(shortest).Apply(ciphertext);
            results.Add((shortest, Encoding.UTF8.GetString(plainBytes), plainBytes.Sum(b => ByteScore(b))));
        }

        // Multiples of the true length (12, 18…) look just as good on Hamming distance, but have fewer
        // bytes per column, so their key bytes are guessed less reliably. Prefer the shortest key whose
        // decryption scores within a whisker of the best.
        double top = results.Max(r => r.Score);
        var best = results.Where(r => r.Score >= top - (Math.Abs(top) * 0.02)).OrderBy(r => r.Key.Length).First();
        byte[] bestKey = best.Key;
        string keyText = bestKey.All(b => b is >= 32 and < 127) ? $"\"{Encoding.ASCII.GetString(bestKey)}\"" : Convert.ToHexString(bestKey);
        var difficulty = new Difficulty(XorCipher.KeySpaceForLength(bestKey.Length))
            .Then("Each key byte is a single-byte XOR", 256L * bestKey.Length, "Every L-th byte shares one key byte; each is found on its own from character frequencies.");
        log.Add($"Key {keyText} ({bestKey.Length} bytes).");
        return new BreakResult
        {
            CipherName = "Repeating-key XOR",
            Key = keyText,
            Plaintext = best.Plain,
            Score = best.Score,
            EnglishLikeness = LanguageModel.English.QuadgramScorePerLetter(best.Plain),
            KeysTried = tried,
            Elapsed = watch.Elapsed,
            Difficulty = difficulty,
            Candidates = [new Candidate(keyText, best.Plain, best.Score)],
            Log = log,
        };
    }

    /// <summary>
    /// The mean number of differing bits per byte between successive blocks of
    /// <paramref name="keyLength"/> bytes.
    /// </summary>
    public static double NormalisedDistance(byte[] data, int keyLength)
    {
        int blocks = data.Length / keyLength;
        if (blocks < 2)
        {
            return double.MaxValue;
        }

        double total = 0;
        int pairs = 0;
        for (int b = 0; b + 1 < blocks; b++)
        {
            total += XorCipher.HammingDistance(data.AsSpan(b * keyLength, keyLength), data.AsSpan((b + 1) * keyLength, keyLength));
            pairs++;
        }

        return total / pairs / keyLength;
    }

    /// <summary>A key that is a repeat of a shorter one ("KEYKEY") reduced to that shorter one.</summary>
    private static byte[] ShortestPeriod(byte[] key)
    {
        for (int p = 1; p < key.Length; p++)
        {
            if (key.Length % p == 0 && Enumerable.Range(0, key.Length).All(i => key[i] == key[i % p]))
            {
                return key[..p];
            }
        }

        return key;
    }

    /// <summary>How plausible a byte is in English text.</summary>
    private static double ByteScore(byte b)
    {
        if (b == ' ')
        {
            return 1.0;
        }

        if (b >= 'a' && b <= 'z')
        {
            return 0.5 + (LanguageModel.English.LetterFrequencies[b - 'a'] * 6);
        }

        if (b >= 'A' && b <= 'Z')
        {
            return 0.2 + (LanguageModel.English.LetterFrequencies[b - 'A'] * 3);
        }

        if (b is (byte)'.' or (byte)',' or (byte)'\'' or (byte)'\n' or (byte)'-' or (byte)'"' or (byte)'?' or (byte)'!' or (byte)';' or (byte)':')
        {
            return 0.1;
        }

        if (b >= '0' && b <= '9')
        {
            return 0;
        }

        return b is >= 32 and < 127 ? -1 : -5;
    }
}

/// <summary>
/// Attacking a one-time pad used twice. Alone, a one-time pad is perfect; reused, it fails
/// catastrophically, because the pad cancels out.
/// </summary>
/// <remarks>
/// <para>
/// If c₁ = p₁ + k and c₂ = p₂ + k (mod 26) with the same pad k, then c₁ − c₂ = p₁ − p₂. The pad has
/// vanished: what's left is two English texts tangled together. Guess a word in one message (a crib),
/// subtract it at some position, and the other message's letters at that position fall out. Where the
/// guess is right they read as English; where it's wrong, as gibberish. Slide the crib along ("crib
/// dragging"), keep the positions that read, and extend them a word at a time.
/// </para>
/// <para>
/// This is how VENONA worked: Soviet one-time pads were duplicated under the pressures of 1942, and
/// American and British analysts (Meredith Gardner and others) read parts of thousands of messages,
/// exposing atomic spies including Klaus Fuchs and the Rosenbergs' network.
/// </para>
/// </remarks>
public static class TwoTimePadAttack
{
    /// <summary>
    /// Slide <paramref name="crib"/> along message one; at each position, report what message two
    /// would say there, and how English that looks. The best positions come first.
    /// </summary>
    public static IReadOnlyList<(int Position, string OtherMessage, double Readability)> DragCrib(string ciphertext1, string ciphertext2, string crib, LanguageModel? language = null)
    {
        language ??= LanguageModel.English;
        int[] c1 = Letters.ToNumbers(ciphertext1);
        int[] c2 = Letters.ToNumbers(ciphertext2);
        int[] p = Letters.ToNumbers(crib);
        int n = Math.Min(c1.Length, c2.Length);
        var result = new List<(int, string, double)>();
        for (int at = 0; at + p.Length <= n; at++)
        {
            // p2 = c2 − k, and k = c1 − p1, so p2 = c2 − c1 + p1.
            string other = Letters.FromNumbers(p.Select((x, j) => c2[at + j] - c1[at + j] + x));
            double readability = language.QuadgramScorePerLetter(other) + (language.IsWord(other) ? 1 : 0);
            result.Add((at, other, readability));
        }

        return result.OrderByDescending(r => r.Item3).ToArray();
    }

    /// <summary>
    /// The pad letters implied by a stretch of known plaintext: k = c − p. With them, that stretch of
    /// <i>every</i> message sent on the same pad can be read.
    /// </summary>
    public static string RecoverPad(string ciphertext, string knownPlaintext, int position)
    {
        int[] c = Letters.ToNumbers(ciphertext);
        int[] p = Letters.ToNumbers(knownPlaintext);
        return Letters.FromNumbers(p.Select((x, j) => c[position + j] - x));
    }
}
