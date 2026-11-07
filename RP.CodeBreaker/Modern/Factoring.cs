using System.Diagnostics;
using System.Numerics;

namespace RP.CodeBreaker.Modern;

/// <summary>The outcome of trying to factor a number.</summary>
/// <param name="N">The number.</param>
/// <param name="Factor">A non-trivial factor found, or null if the method gave up.</param>
/// <param name="Method">Which method found it.</param>
/// <param name="Steps">How many basic steps (divisions, multiplications) it took.</param>
/// <param name="Elapsed">How long it took.</param>
public sealed record FactorResult(BigInteger N, BigInteger? Factor, string Method, long Steps, TimeSpan Elapsed)
{
    /// <summary>The cofactor N / Factor, or null.</summary>
    public BigInteger? Cofactor => this.Factor is null ? null : this.N / this.Factor.Value;

    /// <summary>True if a factor was found.</summary>
    public bool Success => this.Factor is not null;
}

/// <summary>
/// Breaking RSA means factoring n. These three classic methods show how the work grows with the size
/// of n — and why the right size of key makes the problem hopeless.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>Trial division</b>: divide by 2, 3, 5, 7… up to √n. For an n of b bits that is up to
/// 2^(b/2) divisions: fine for 40 bits, hopeless by 128.</item>
/// <item><b>Fermat's method</b>: if p and q are close together, n is nearly a square, and searching up
/// from √n finds them almost at once. A real flaw: keys generated with p and q too close have been
/// found in the wild (printers and network devices, 2022) and broken this way.</item>
/// <item><b>Pollard's rho</b> (1975): a random walk modulo n that collides modulo the smaller prime p
/// after about √p steps — 2^(b/4) work, so a 100-bit n falls in a fraction of a second.</item>
/// </list>
/// <para>
/// The record-breakers use the general number field sieve, whose work grows more slowly than any power
/// of n but faster than any power of its number of digits. Its estimate is in
/// <see cref="EstimateNumberFieldSieveOperations"/>.
/// </para>
/// </remarks>
public static class Factoring
{
    /// <summary>Divide by 2 and then odd numbers up to √n, or until <paramref name="maxSteps"/>.</summary>
    public static FactorResult TrialDivision(BigInteger n, long maxSteps = 50_000_000)
    {
        var watch = Stopwatch.StartNew();
        if (n % 2 == 0)
        {
            return new FactorResult(n, 2, "trial division", 1, watch.Elapsed);
        }

        BigInteger limit = NumberTheory.IntegerSqrt(n);
        long steps = 0;
        for (BigInteger d = 3; d <= limit && steps < maxSteps; d += 2)
        {
            steps++;
            if (n % d == 0)
            {
                return new FactorResult(n, d, "trial division", steps, watch.Elapsed);
            }
        }

        return new FactorResult(n, null, "trial division", steps, watch.Elapsed);
    }

    /// <summary>
    /// Fermat's method: look for n = a² − b² = (a − b)(a + b), trying a = ⌈√n⌉, ⌈√n⌉ + 1, …
    /// </summary>
    public static FactorResult Fermat(BigInteger n, long maxSteps = 10_000_000)
    {
        var watch = Stopwatch.StartNew();
        BigInteger a = NumberTheory.IntegerSqrt(n);
        if (a * a < n)
        {
            a++;
        }

        for (long steps = 1; steps <= maxSteps; steps++, a++)
        {
            BigInteger b2 = (a * a) - n;
            BigInteger b = NumberTheory.IntegerSqrt(b2);
            if (b * b == b2)
            {
                BigInteger f = a - b;
                return new FactorResult(n, f == 1 ? null : f, "Fermat", steps, watch.Elapsed);
            }
        }

        return new FactorResult(n, null, "Fermat", maxSteps, watch.Elapsed);
    }

    /// <summary>Pollard's rho with Brent's cycle finding and batched gcds.</summary>
    public static FactorResult PollardRho(BigInteger n, long maxSteps = 50_000_000, int seed = 1)
    {
        var watch = Stopwatch.StartNew();
        if (n % 2 == 0)
        {
            return new FactorResult(n, 2, "Pollard's rho", 1, watch.Elapsed);
        }

        var random = new Random(seed);
        long steps = 0;
        for (int attempt = 0; attempt < 20 && steps < maxSteps; attempt++)
        {
            BigInteger c = random.Next(1, int.MaxValue) % n;
            BigInteger y = random.Next(1, int.MaxValue) % n;
            BigInteger g = 1, r = 1, q = 1, x = 0, ys = 0;
            const int Batch = 128;
            while (g == 1 && steps < maxSteps)
            {
                x = y;
                for (BigInteger i = 0; i < r; i++)
                {
                    y = ((y * y) + c) % n;
                }

                BigInteger k = 0;
                while (k < r && g == 1)
                {
                    ys = y;
                    BigInteger m = BigInteger.Min(Batch, r - k);
                    for (BigInteger i = 0; i < m; i++)
                    {
                        y = ((y * y) + c) % n;
                        q = q * BigInteger.Abs(x - y) % n;
                        steps++;
                    }

                    g = BigInteger.GreatestCommonDivisor(q, n);
                    k += Batch;
                }

                r *= 2;
            }

            if (g == n)
            {
                // The batch overshot; step back through it one at a time.
                do
                {
                    ys = ((ys * ys) + c) % n;
                    g = BigInteger.GreatestCommonDivisor(BigInteger.Abs(x - ys), n);
                }
                while (g == 1);
            }

            if (g != n && g != 1)
            {
                return new FactorResult(n, g, "Pollard's rho", steps, watch.Elapsed);
            }
        }

        return new FactorResult(n, null, "Pollard's rho", steps, watch.Elapsed);
    }

    /// <summary>
    /// Try the quick methods in turn — Fermat briefly (in case p and q are close), trial division for
    /// small n, then Pollard's rho.
    /// </summary>
    public static FactorResult Factor(BigInteger n)
    {
        FactorResult fermat = Fermat(n, 20_000);
        if (fermat.Success)
        {
            return fermat;
        }

        if (BigInteger.Log(n, 2) <= 44)
        {
            return TrialDivision(n);
        }

        return PollardRho(n);
    }

    /// <summary>
    /// The heuristic running time of the general number field sieve for an n of
    /// <paramref name="bits"/> bits, as log₂ of the number of operations:
    /// L(n) = exp((64/9)^(1/3) · (ln n)^(1/3) · (ln ln n)^(2/3)). Rough (it ignores constant
    /// factors), but it matches the usual guidance: about 2⁸⁰ for 1024 bits and 2¹¹⁷ for 2048.
    /// </summary>
    public static double EstimateNumberFieldSieveOperations(int bits)
    {
        double lnN = bits * Math.Log(2);
        double l = Math.Pow(64.0 / 9.0, 1.0 / 3.0) * Math.Pow(lnN, 1.0 / 3.0) * Math.Pow(Math.Log(lnN), 2.0 / 3.0);
        return l / Math.Log(2);
    }

    /// <summary>
    /// The work for trial division (up to √n, so 2^(bits/2)) and Pollard's rho (about √p ≈ 2^(bits/4)
    /// for a balanced key), as log₂ of operations.
    /// </summary>
    public static (double TrialDivisionBits, double PollardRhoBits, double NumberFieldSieveBits) WorkInBits(int bits) =>
        (bits / 2.0, bits / 4.0, EstimateNumberFieldSieveOperations(bits));
}
