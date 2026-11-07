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
    /// The inverse of a square matrix modulo m (by Gauss–Jordan elimination, using modular inverses
    /// in place of division). Null if the matrix has no inverse — its determinant shares a factor
    /// with m.
    /// </summary>
    public static int[,]? MatrixInverse(int[,] matrix, int m)
    {
        int n = matrix.GetLength(0);
        var a = new int[n, 2 * n];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                a[i, j] = Letters.Mod(matrix[i, j], m);
            }

            a[i, n + i] = 1;
        }

        for (int col = 0; col < n; col++)
        {
            int pivot = -1;
            for (int row = col; row < n; row++)
            {
                if (ModInverse(a[row, col], m) is not null)
                {
                    pivot = row;
                    break;
                }
            }

            if (pivot < 0)
            {
                return null;
            }

            for (int j = 0; j < 2 * n; j++)
            {
                (a[col, j], a[pivot, j]) = (a[pivot, j], a[col, j]);
            }

            int inv = ModInverse(a[col, col], m)!.Value;
            for (int j = 0; j < 2 * n; j++)
            {
                a[col, j] = Letters.Mod(a[col, j] * inv, m);
            }

            for (int row = 0; row < n; row++)
            {
                if (row == col || a[row, col] == 0)
                {
                    continue;
                }

                int factor = a[row, col];
                for (int j = 0; j < 2 * n; j++)
                {
                    a[row, j] = Letters.Mod(a[row, j] - (factor * a[col, j]), m);
                }
            }
        }

        var result = new int[n, n];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                result[i, j] = a[i, n + j];
            }
        }

        return result;
    }
}
