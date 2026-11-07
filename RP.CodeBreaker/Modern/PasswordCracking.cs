using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace RP.CodeBreaker.Modern;

/// <summary>A recovered secret from a hash search.</summary>
/// <param name="Secret">The secret that produced the hash, or null if not found.</param>
/// <param name="Tried">How many guesses were hashed.</param>
/// <param name="SearchSpace">How many guesses the search could have tried.</param>
/// <param name="Elapsed">How long it took.</param>
public sealed record CrackResult(string? Secret, long Tried, BigInteger SearchSpace, TimeSpan Elapsed)
{
    /// <summary>True if the secret was found.</summary>
    public bool Found => this.Secret is not null;

    /// <summary>Guesses per second achieved.</summary>
    public double Rate => this.Elapsed.TotalSeconds <= 0 ? 0 : this.Tried / this.Elapsed.TotalSeconds;
}

/// <summary>
/// SHA-256 is unbroken — and that does not save a four-digit PIN. These searches show the most
/// important lesson in modern code breaking: <b>the cipher is rarely the weak point; the key is</b>.
/// </summary>
/// <remarks>
/// <para>
/// A hash function turns any input into a fixed-size fingerprint that can't be run backwards. Systems
/// store the hash of your password, not the password. But if the secret comes from a small set — 10,000
/// PINs, or the few thousand words people actually choose — an attacker simply hashes every candidate
/// and compares. The strength of SHA-256 (2²⁵⁶ outputs) is irrelevant; the work is the size of the
/// set the <i>secret</i> was chosen from.
/// </para>
/// <para>
/// This is the modern face of the crib: knowing how people choose keys (words, names, "Password1")
/// shrinks the search exactly as knowing a message says WETTERBERICHT did. Real systems defend with
/// slow, salted hashes (bcrypt, scrypt, Argon2) that make each guess cost thousands of times more.
/// </para>
/// </remarks>
public static class PasswordCracking
{
    /// <summary>The SHA-256 hash of a text (UTF-8), as lower-case hexadecimal.</summary>
    public static string Sha256Hex(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    /// <summary>
    /// Find a PIN of exactly <paramref name="digits"/> digits from its SHA-256 hash by trying all
    /// 10^digits of them.
    /// </summary>
    public static CrackResult CrackPin(string sha256Hex, int digits)
    {
        if (digits is < 1 or > 9)
        {
            throw new ArgumentOutOfRangeException(nameof(digits), "Between 1 and 9 digits.");
        }

        byte[] target = Convert.FromHexString(sha256Hex.Trim());
        var watch = Stopwatch.StartNew();
        long count = (long)Math.Pow(10, digits);
        string format = new('0', digits);
        Span<byte> hash = stackalloc byte[32];
        for (long i = 0; i < count; i++)
        {
            string guess = i.ToString(format, CultureInfo.InvariantCulture);
            SHA256.HashData(Encoding.ASCII.GetBytes(guess), hash);
            if (hash.SequenceEqual(target))
            {
                return new CrackResult(guess, i + 1, count, watch.Elapsed);
            }
        }

        return new CrackResult(null, count, count, watch.Elapsed);
    }

    /// <summary>
    /// A dictionary attack: try every known word, and each with the usual human decorations — a capital
    /// first letter, and a digit or two (or a "!") on the end.
    /// </summary>
    /// <param name="sha256Hex">The hash to match.</param>
    /// <param name="words">Words to try (defaults to the English word list, commonest first).</param>
    /// <param name="maxWords">How far down the list to go.</param>
    public static CrackResult CrackWord(string sha256Hex, IEnumerable<string>? words = null, int maxWords = 5000)
    {
        byte[] target = Convert.FromHexString(sha256Hex.Trim());
        var watch = Stopwatch.StartNew();
        string[] list = (words ?? LanguageModel.English.Words).Take(maxWords).ToArray();
        string[] suffixes = [string.Empty, "!", .. Enumerable.Range(0, 100).Select(i => i.ToString(CultureInfo.InvariantCulture))];
        long tried = 0;
        BigInteger space = (BigInteger)list.Length * 3 * suffixes.Length;
        Span<byte> hash = stackalloc byte[32];
        foreach (string w in list)
        {
            string lower = w.ToLowerInvariant();
            string[] forms = [lower, char.ToUpperInvariant(lower[0]) + lower[1..], lower.ToUpperInvariant()];
            foreach (string form in forms)
            {
                foreach (string suffix in suffixes)
                {
                    string guess = form + suffix;
                    tried++;
                    SHA256.HashData(Encoding.UTF8.GetBytes(guess), hash);
                    if (hash.SequenceEqual(target))
                    {
                        return new CrackResult(guess, tried, space, watch.Elapsed);
                    }
                }
            }
        }

        return new CrackResult(null, tried, space, watch.Elapsed);
    }

    /// <summary>
    /// The search space for a secret chosen uniformly at random: alphabet^length. A random 12-character
    /// password from 94 printable characters is 94¹² ≈ 4.8 × 10²³ (79 bits); a word plus two digits is
    /// a few hundred thousand.
    /// </summary>
    public static KeySpace RandomSecret(int alphabetSize, int length) =>
        KeySpace.Power(alphabetSize, length, $"{alphabetSize}^{length} random strings");
}
