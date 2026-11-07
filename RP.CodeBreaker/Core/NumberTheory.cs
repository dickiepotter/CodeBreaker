using System.Numerics;

namespace RP.CodeBreaker;

/// <summary>
/// The whole-number arithmetic ciphers are built from: greatest common divisors, inverses
/// "modulo m", fast powers, primes and factoring.
/// </summary>
/// <remarks>
/// <para>
/// Division doesn't exist on a clock face, but its stand-in does: the <b>modular inverse</b>. The
/// inverse of 3 modulo 26 is 9, because 3 × 9 = 27, which is 1 more than 26 — so multiplying by 9
/// undoes multiplying by 3. An inverse exists only when the number shares no factor with 26 (their
/// greatest common divisor is 1), which is why the affine cipher's multiplier must be odd and not 13,
/// and why only some Hill matrices can be used as keys.
/// </para>
/// </remarks>
public static class NumberTheory
{
    /// <summary>The greatest common divisor, by Euclid's algorithm (c. 300 BC, still the fastest way).</summary>
    public static long Gcd(long a, long b)
    {
        a = Math.Abs(a);
        b = Math.Abs(b);
        while (b != 0)
        {
            (a, b) = (b, a % b);
        }

        return a;
    }

    /// <summary>
    /// The inverse of <paramref name="a"/> modulo <paramref name="m"/>: the x in 0 … m-1 with
    /// a·x ≡ 1 (mod m), found by the extended Euclidean algorithm. Null when none exists
    /// (gcd(a, m) ≠ 1).
    /// </summary>
    public static int? ModInverse(int a, int m)
    {
        BigInteger? inv = ModInverse(new BigInteger(a), new BigInteger(m));
        return inv is null ? null : (int)inv.Value;
    }

    /// <summary>The modular inverse for big integers (as RSA needs). Null when none exists.</summary>
    public static BigInteger? ModInverse(BigInteger a, BigInteger m)
    {
        BigInteger oldR = ((a % m) + m) % m, r = m;
        BigInteger oldS = 1, s = 0;
        while (r != 0)
        {
            BigInteger q = oldR / r;
            (oldR, r) = (r, oldR - (q * r));
            (oldS, s) = (s, oldS - (q * s));
        }

        if (oldR != 1)
        {
            return null;
        }

        return ((oldS % m) + m) % m;
    }

    /// <summary>
    /// A deterministic Miller–Rabin primality test. With the fixed bases used it is exact for every
    /// number below 3.3 × 10²⁴, which covers every key the teaching RSA here can make.
    /// </summary>
    public static bool IsPrime(BigInteger n)
    {
        if (n < 2)
        {
            return false;
        }

        int[] bases = [2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41];
        foreach (int p in bases)
        {
            if (n == p)
            {
                return true;
            }

            if (n % p == 0)
            {
                return false;
            }
        }

        BigInteger d = n - 1;
        int s = 0;
        while (d.IsEven)
        {
            d /= 2;
            s++;
        }

        foreach (int a in bases)
        {
            BigInteger x = BigInteger.ModPow(a, d, n);
            if (x == 1 || x == n - 1)
            {
                continue;
            }

            bool composite = true;
            for (int i = 1; i < s; i++)
            {
                x = BigInteger.ModPow(x, 2, n);
                if (x == n - 1)
                {
                    composite = false;
                    break;
                }
            }

            if (composite)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The integer square root ⌊√n⌋, exactly, by Newton's method.</summary>
    public static BigInteger IntegerSqrt(BigInteger n)
    {
        if (n < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(n));
        }

        if (n < 2)
        {
            return n;
        }

        BigInteger x = (BigInteger)Math.Sqrt((double)n);
        while (x * x > n)
        {
            x--;
        }

        while ((x + 1) * (x + 1) <= n)
        {
            x++;
        }

        // For very large n the double estimate can be far off; finish with Newton steps.
        BigInteger y = (x + (n / x)) / 2;
        while (y < x)
        {
            x = y;
            y = (x + (n / x)) / 2;
        }

        return x;
    }

    /// <summary>The smallest prime greater than or equal to <paramref name="n"/>.</summary>
    public static BigInteger NextPrime(BigInteger n)
    {
        if (n <= 2)
        {
            return 2;
        }

        if (n.IsEven)
        {
            n++;
        }

        while (!IsPrime(n))
        {
            n += 2;
        }

        return n;
    }

    /// <summary>
    /// The inverse of a square matrix modulo m, or null if it has none. A matrix is invertible modulo m
    /// exactly when its determinant shares no factor with m, and the inverse is then the adjugate (the
    /// transposed matrix of cofactors) times the determinant's inverse.
    /// </summary>
    /// <remarks>
    /// Why not Gauss–Jordan elimination, as with ordinary numbers? Elimination divides by a pivot, and
    /// modulo 26 most numbers can't be divided by: 13 and every even number have no inverse. A matrix
    /// such as [[13, 7], [14, 19]] has determinant 19, which does have an inverse, so the matrix is
    /// invertible — yet neither entry of its first column can serve as a pivot, and elimination wrongly
    /// gives up. Working through the determinant avoids dividing by anything but the determinant itself.
    /// (The matrices here are 2 × 2 or 3 × 3, so cofactor expansion is quick.)
    /// </remarks>
    public static int[,]? MatrixInverse(int[,] matrix, int m)
    {
        int n = matrix.GetLength(0);
        var a = new int[n, n];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                a[i, j] = Letters.Mod(matrix[i, j], m);
            }
        }

        int? detInverse = ModInverse(Determinant(a, m), m);
        if (detInverse is null)
        {
            return null;
        }

        var result = new int[n, n];
        if (n == 1)
        {
            result[0, 0] = detInverse.Value;
            return result;
        }

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                // inverse[i, j] = cofactor[j, i] / det
                int cofactor = Determinant(Minor(a, j, i), m) * (((i + j) % 2 == 0) ? 1 : -1);
                result[i, j] = Letters.Mod(cofactor * detInverse.Value, m);
            }
        }

        return result;
    }

    /// <summary>The determinant of a square matrix modulo m, by cofactor expansion along the first row.</summary>
    public static int Determinant(int[,] matrix, int m)
    {
        int n = matrix.GetLength(0);
        if (n == 1)
        {
            return Letters.Mod(matrix[0, 0], m);
        }

        long sum = 0;
        for (int j = 0; j < n; j++)
        {
            long term = (long)matrix[0, j] * Determinant(Minor(matrix, 0, j), m);
            sum += (j % 2 == 0) ? term : -term;
        }

        return (int)(((sum % m) + m) % m);
    }

    /// <summary>The matrix without row r and column c.</summary>
    private static int[,] Minor(int[,] matrix, int r, int c)
    {
        int n = matrix.GetLength(0);
        var minor = new int[n - 1, n - 1];
        for (int i = 0, mi = 0; i < n; i++)
        {
            if (i == r)
            {
                continue;
            }

            for (int j = 0, mj = 0; j < n; j++)
            {
                if (j == c)
                {
                    continue;
                }

                minor[mi, mj++] = matrix[i, j];
            }

            mi++;
        }

        return minor;
    }
}
