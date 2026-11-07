using System.Numerics;

namespace RP.CodeBreaker.Modern;

/// <summary>
/// Textbook RSA with deliberately small keys — small enough to break, so you can watch where the
/// difficulty really lives.
/// </summary>
/// <remarks>
/// <para>
/// RSA (Rivest, Shamir and Adleman, 1977; found earlier, in secret, by Clifford Cocks at GCHQ in
/// 1973) works like this. Pick two primes p and q and publish their product n = p·q along with an
/// exponent e (usually 65537). To encrypt a number m, compute c = mᵉ mod n. To decrypt you need d, the
/// inverse of e modulo (p − 1)(q − 1) — and computing that needs p and q. So <b>the private key is
/// exactly as safe as n is hard to factor</b>.
/// </para>
/// <para>
/// There is no key to brute-force here in the usual sense; the attack is factoring. A 40-bit n falls
/// instantly to trial division; a 100-bit n to Pollard's rho in a moment; a 512-bit n took a team
/// with hundreds of computers months in 1999 and now takes hours in the cloud; 2048-bit n is beyond
/// any known classical method (see <see cref="Factoring.EstimateNumberFieldSieveOperations"/>).
/// This textbook form has no padding and must never be used for real secrets.
/// </para>
/// </remarks>
public sealed class ToyRsa
{
    /// <summary>The usual public exponent, 2¹⁶ + 1.</summary>
    public static readonly BigInteger DefaultExponent = 65_537;

    /// <summary>Build a key pair from two primes.</summary>
    public ToyRsa(BigInteger p, BigInteger q, BigInteger? e = null)
    {
        if (!NumberTheory.IsPrime(p) || !NumberTheory.IsPrime(q) || p == q)
        {
            throw new ArgumentException("p and q must be two different primes.");
        }

        this.P = p;
        this.Q = q;
        this.N = p * q;
        this.E = e ?? DefaultExponent;
        BigInteger phi = (p - 1) * (q - 1);
        this.D = NumberTheory.ModInverse(this.E, phi)
            ?? throw new ArgumentException("e shares a factor with (p − 1)(q − 1); choose another e or other primes.");
    }

    /// <summary>The first secret prime.</summary>
    public BigInteger P { get; }

    /// <summary>The second secret prime.</summary>
    public BigInteger Q { get; }

    /// <summary>The public modulus n = p·q.</summary>
    public BigInteger N { get; }

    /// <summary>The public exponent.</summary>
    public BigInteger E { get; }

    /// <summary>The private exponent.</summary>
    public BigInteger D { get; }

    /// <summary>The size of n in bits.</summary>
    public int Bits => (int)Math.Ceiling(BigInteger.Log(this.N, 2));

    /// <summary>
    /// Generate a key whose modulus has about <paramref name="bits"/> bits, from a seeded generator so
    /// examples are repeatable. (Real keys come from a cryptographic random source; this is for teaching.)
    /// </summary>
    public static ToyRsa Generate(int bits, int seed = 1)
    {
        var random = new Random(seed);
        int half = Math.Max(4, bits / 2);
        while (true)
        {
            BigInteger p = NumberTheory.NextPrime(RandomOfBits(random, half));
            BigInteger q = NumberTheory.NextPrime(RandomOfBits(random, bits - half));
            if (p == q)
            {
                continue;
            }

            BigInteger phi = (p - 1) * (q - 1);
            BigInteger e = phi > DefaultExponent * 2 ? DefaultExponent : 3;
            while (NumberTheory.ModInverse(e, phi) is null)
            {
                e += 2;
            }

            return new ToyRsa(p, q, e);
        }
    }

    /// <summary>
    /// A badly made key: q is simply the next prime after p, so n is almost a perfect square and
    /// Fermat's method factors it in a single step, however large it is. Keys like this have turned up
    /// in real devices.
    /// </summary>
    public static ToyRsa GenerateCareless(int bits, int seed = 1)
    {
        var random = new Random(seed);
        BigInteger p = NumberTheory.NextPrime(RandomOfBits(random, Math.Max(4, bits / 2)));
        BigInteger q = NumberTheory.NextPrime(p + 2);
        BigInteger phi = (p - 1) * (q - 1);
        BigInteger e = phi > DefaultExponent * 2 ? DefaultExponent : 3;
        while (NumberTheory.ModInverse(e, phi) is null)
        {
            e += 2;
        }

        return new ToyRsa(p, q, e);
    }

    /// <summary>Encrypt a number smaller than n: c = mᵉ mod n.</summary>
    public BigInteger Encrypt(BigInteger m) => BigInteger.ModPow(m, this.E, this.N);

    /// <summary>Decrypt: m = cᵈ mod n.</summary>
    public BigInteger Decrypt(BigInteger c) => BigInteger.ModPow(c, this.D, this.N);

    /// <summary>
    /// Recover the private exponent from a factorisation of n — what an attacker does after factoring.
    /// </summary>
    public static BigInteger PrivateExponentFromFactors(BigInteger p, BigInteger q, BigInteger e) =>
        NumberTheory.ModInverse(e, (p - 1) * (q - 1)) ?? throw new ArgumentException("No inverse exists.");

    private static BigInteger RandomOfBits(Random random, int bits)
    {
        var bytes = new byte[(bits / 8) + 1];
        random.NextBytes(bytes);
        var value = new BigInteger(bytes, isUnsigned: true);
        BigInteger top = BigInteger.One << (bits - 1);
        value %= top;
        return value | top; // make sure it really has the requested number of bits
    }
}
