using System.Globalization;
using System.Numerics;
using RP.CodeBreaker.Statistics;

namespace RP.CodeBreaker;

/// <summary>
/// The number of different keys a cipher could have been set up with — the haystack a brute-force
/// search has to work through. It is the single most important number in this library.
/// </summary>
/// <remarks>
/// <para>
/// Key spaces get big very quickly, so as well as the exact count (<see cref="Size"/>, a
/// <see cref="BigInteger"/> so that even 2²⁵⁶ is held exactly) each one is also given in
/// <b>bits</b>: <c>log₂(Size)</c>. Bits are how cryptographers talk, because each extra bit
/// <i>doubles</i> the work. A Caesar cipher has 26 keys, about 4.7 bits; a simple substitution
/// has 26! keys, about 88.4 bits; AES-128 has exactly 128 bits.
/// </para>
/// <para>
/// A key space is only the <i>upper bound</i> on the work. The whole point of cryptanalysis is to
/// avoid searching it all: see <see cref="Difficulty"/> for how each piece of insight or known
/// information cuts it down.
/// </para>
/// </remarks>
public sealed class KeySpace
{
    /// <summary>Create a key space of an exact size.</summary>
    public KeySpace(BigInteger size, string description)
    {
        if (size < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "A key space holds at least one key.");
        }

        this.Size = size;
        this.Description = description;
    }

    /// <summary>The exact number of keys.</summary>
    public BigInteger Size { get; }

    /// <summary>How the size was worked out, in words (for example "26 shifts").</summary>
    public string Description { get; }

    /// <summary>The size in bits, log₂(<see cref="Size"/>).</summary>
    public double Bits => Log2(this.Size);

    /// <summary>The size in decimal digits, log₁₀(<see cref="Size"/>).</summary>
    public double Log10 => BigInteger.Log10(this.Size);

    /// <summary>
    /// The key space of a key made of <paramref name="length"/> independent symbols, each one of
    /// <paramref name="choices"/> possibilities: choices^length. A five-letter Vigenère key is
    /// 26⁵ = 11,881,376.
    /// </summary>
    public static KeySpace Power(int choices, int length, string? description = null) =>
        new(BigInteger.Pow(choices, length), description ?? $"{choices}^{length}");

    /// <summary>
    /// The key space of every ordering of <paramref name="n"/> things: n! ("n factorial",
    /// n × (n-1) × … × 1). A simple substitution alphabet is one ordering of 26 letters: 26!.
    /// </summary>
    public static KeySpace Factorial(int n, string? description = null) => new(FactorialOf(n), description ?? $"{n}!");

    /// <summary>A key space of 2^<paramref name="bits"/> keys, the way modern ciphers are quoted.</summary>
    public static KeySpace FromBits(int bits, string? description = null) => new(BigInteger.Pow(2, bits), description ?? $"2^{bits}");

    /// <summary>A key space of a plain count.</summary>
    public static KeySpace Of(BigInteger size, string description) => new(size, description);

    /// <summary>
    /// Two independent choices made together multiply: 60 rotor orders × 17,576 start positions.
    /// </summary>
    public KeySpace Times(KeySpace other) => new(this.Size * other.Size, $"{this.Description} × {other.Description}");

    /// <summary>How long it would take <paramref name="attacker"/> to try every key.</summary>
    public WorkEstimate ExhaustiveSearch(Attacker attacker) => WorkEstimate.For(this.Size, attacker);

    /// <summary>
    /// How long, on average, before <paramref name="attacker"/> hits the right key: half the space.
    /// </summary>
    public WorkEstimate ExpectedSearch(Attacker attacker) => WorkEstimate.For((this.Size + 1) / 2, attacker);

    /// <summary>
    /// English's redundancy in bits per letter: log₂ 26 ≈ 4.7 bits of room per letter, of which real
    /// English uses only about 1.5. The remaining ≈ 3.2 bits are spelling, grammar and sense — what
    /// lets a code breaker recognise the right answer.
    /// </summary>
    public const double EnglishRedundancy = 3.2;

    /// <summary>
    /// Shannon's unicity distance: roughly how many letters of ciphertext are needed before only one key
    /// gives a sensible plaintext — Bits ÷ redundancy. Below it, several keys give believable messages
    /// and no amount of computing can say which was meant; above it, the answer is unique and only the
    /// work stands in the way.
    /// </summary>
    /// <remarks>
    /// Caesar: 4.7 ÷ 3.2 ≈ 1.5 letters. Simple substitution: 88.4 ÷ 3.2 ≈ 28 letters. Enigma: about 24.
    /// A one-time pad's key grows with the message, so its unicity distance is never reached. It is an
    /// estimate for an idealised attacker — in practice you want several times as much text.
    /// </remarks>
    /// <param name="redundancyBitsPerLetter">The plaintext language's redundancy (default English).</param>
    public double UnicityDistance(double redundancyBitsPerLetter = EnglishRedundancy) => this.Bits / redundancyBitsPerLetter;

    /// <summary>n! as an exact big integer.</summary>
    public static BigInteger FactorialOf(int n)
    {
        BigInteger result = BigInteger.One;
        for (int i = 2; i <= n; i++)
        {
            result *= i;
        }

        return result;
    }

    /// <summary>
    /// log₂ of a big integer. <see cref="BigInteger.Log(BigInteger)"/> works for any size, so this
    /// stays accurate long after the number itself has outgrown a <c>double</c>.
    /// </summary>
    public static double Log2(BigInteger value) => value <= 0 ? double.NegativeInfinity : BigInteger.Log(value, 2);

    /// <summary>
    /// log₂(n!) without building n! — via the vendored log-gamma, since ln n! = ln Γ(n + 1). Useful
    /// for very large n (a 300-letter transposition) where the exact factorial is enormous.
    /// </summary>
    public static double Log2Factorial(int n) => SpecialFunctions.LogFactorial(n) / Math.Log(2);

    /// <summary>A short human-readable form: the exact number when small, otherwise "≈ 4.03 × 10^26".</summary>
    public static string Format(BigInteger value)
    {
        if (value < 1_000_000_000_000)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        double log10 = BigInteger.Log10(value);
        int exponent = (int)Math.Floor(log10);
        double mantissa = Math.Pow(10, log10 - exponent);
        if (mantissa >= 9.995)
        {
            mantissa = 1;
            exponent++;
        }

        return string.Format(CultureInfo.InvariantCulture, "≈ {0:0.00} × 10^{1}", mantissa, exponent);
    }

    /// <inheritdoc/>
    public override string ToString() =>
        string.Format(CultureInfo.InvariantCulture, "{0} keys ({1:0.0} bits) — {2}", Format(this.Size), this.Bits, this.Description);
}
