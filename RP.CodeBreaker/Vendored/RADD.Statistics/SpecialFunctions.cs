// Copyright (c) 2026 RADD Solutions Limited. All rights reserved. Basis: repository confirmed as wholly RADD's work.
namespace RP.CodeBreaker.Statistics;

/// <summary>
/// The special functions every distribution in this library is built from: the gamma and beta
/// functions, their regularised incomplete forms, and the error function.
/// </summary>
/// <implementation>
/// Log-gamma uses the Lanczos approximation with g = 671/128 and fourteen coefficients (Numerical
/// Recipes' gammln), good to better than 1e-15 relative across the positive reals, with the reflection
/// formula for negative arguments. The incomplete gamma
/// function is evaluated by its power series below <c>x &lt; a + 1</c> and by a continued fraction
/// (modified Lentz) above it; the incomplete beta function by its continued fraction, flipped through
/// the symmetry <c>I_x(a, b) = 1 - I_(1-x)(b, a)</c> so the fraction always converges quickly. The
/// complementary forms are computed directly rather than as <c>1 - P</c>, so very small tail
/// probabilities (p-values of 1e-30) survive instead of rounding to zero.
/// </implementation>
/// <Acknowledgement>
/// Press, Teukolsky, Vetterling and Flannery, Numerical Recipes (3rd edition), chapter 6; C. Lanczos,
/// "A precision approximation of the gamma function" (1964).
/// </Acknowledgement>
public static class SpecialFunctions
{
    #region Constants

    private const double Epsilon = 1e-15;
    private const double TinyFloat = 1e-300;
    private const int MaxIterations = 10_000;

    private static readonly double[] LanczosCoefficients =
    [
        57.1562356658629235, -59.5979603554754912, 14.1360979747417471, -0.491913816097620199,
        .339946499848118887e-4, .465236289270485756e-4, -.983744753048795646e-4, .158088703224912494e-3,
        -.210264441724104883e-3, .217439618115212643e-3, -.164318106536763890e-3, .844182239838527433e-4,
        -.261908384015814087e-4, .368991826595316234e-5,
    ];

    #endregion

    #region Error messages

    private const string ShapeMustBePositive = "The shape parameters must be greater than zero.";
    private const string XMustBeNonNegative = "x must be zero or greater.";
    private const string XMustBeUnitInterval = "x must lie between 0 and 1.";

    #endregion

    #region Gamma

    /// <summary>
    /// The natural logarithm of the gamma function, ln |Γ(x)|. Γ(n) = (n - 1)! for whole n.
    /// </summary>
    /// <param name="x">Any real number other than zero or a negative integer.</param>
    /// <returns>ln |Γ(x)|; positive infinity at the poles.</returns>
    public static double LogGamma(double x)
    {
        if (x <= 0 && System.Math.Floor(x) == x)
        {
            return double.PositiveInfinity;
        }

        if (x <= 0)
        {
            // Reflection: Γ(x) Γ(1 - x) = π / sin(πx).
            return System.Math.Log(System.Math.PI / System.Math.Abs(System.Math.Sin(System.Math.PI * x))) - LogGamma(1 - x);
        }

        double y = x;
        double tmp = x + 5.24218750000000000; // g = 671/128
        tmp = ((x + 0.5) * System.Math.Log(tmp)) - tmp;
        double series = 0.999999999999997092;
        foreach (double c in LanczosCoefficients)
        {
            series += c / ++y;
        }

        return tmp + System.Math.Log(2.5066282746310005 * series / x);
    }

    /// <summary>The gamma function Γ(x), the continuous extension of the factorial.</summary>
    /// <param name="x">Any real number other than zero or a negative integer.</param>
    /// <returns>Γ(x).</returns>
    public static double Gamma(double x)
    {
        if (x <= 0 && System.Math.Floor(x) == x)
        {
            return double.NaN;
        }

        double magnitude = System.Math.Exp(LogGamma(x));
        if (x > 0)
        {
            return magnitude;
        }

        // Γ alternates sign between the negative poles: negative on (-1, 0), positive on (-2, -1), ...
        return ((int)System.Math.Floor(x) % 2 == 0) ? magnitude : -magnitude;
    }

    /// <summary>The natural logarithm of n! for a whole number n.</summary>
    /// <param name="n">Zero or greater.</param>
    /// <returns>ln(n!).</returns>
    public static double LogFactorial(int n)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(n);
        return n < 2 ? 0 : LogGamma(n + 1.0);
    }

    /// <summary>The natural logarithm of the binomial coefficient, ln C(n, k).</summary>
    /// <param name="n">The number of items.</param>
    /// <param name="k">How many are chosen.</param>
    /// <returns>ln C(n, k), or negative infinity when k lies outside 0..n.</returns>
    public static double LogBinomial(int n, int k)
    {
        if (k < 0 || k > n)
        {
            return double.NegativeInfinity;
        }

        return LogFactorial(n) - LogFactorial(k) - LogFactorial(n - k);
    }

    #endregion

    #region Beta

    /// <summary>The natural logarithm of the beta function, ln B(a, b) = ln Γ(a) + ln Γ(b) - ln Γ(a + b).</summary>
    /// <param name="a">First shape, greater than zero.</param>
    /// <param name="b">Second shape, greater than zero.</param>
    /// <returns>ln B(a, b).</returns>
    public static double LogBeta(double a, double b)
    {
        if (a <= 0 || b <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(a), ShapeMustBePositive);
        }

        return LogGamma(a) + LogGamma(b) - LogGamma(a + b);
    }

    /// <summary>The beta function B(a, b).</summary>
    /// <param name="a">First shape, greater than zero.</param>
    /// <param name="b">Second shape, greater than zero.</param>
    /// <returns>B(a, b).</returns>
    public static double Beta(double a, double b) => System.Math.Exp(LogBeta(a, b));

    #endregion

    #region Incomplete gamma

    /// <summary>
    /// The regularised lower incomplete gamma function P(a, x) = γ(a, x) / Γ(a): the chance a gamma
    /// variable of shape <paramref name="a"/> (and unit scale) is at most <paramref name="x"/>.
    /// </summary>
    /// <param name="a">The shape, greater than zero.</param>
    /// <param name="x">Zero or greater.</param>
    /// <returns>P(a, x), between 0 and 1.</returns>
    public static double RegularizedGammaP(double a, double x)
    {
        ValidateGamma(a, x);
        if (x == 0)
        {
            return 0;
        }

        if (double.IsPositiveInfinity(x))
        {
            return 1;
        }

        return x < a + 1 ? GammaSeries(a, x) : 1 - GammaContinuedFraction(a, x);
    }

    /// <summary>
    /// The regularised upper incomplete gamma function Q(a, x) = 1 - P(a, x), computed directly so a
    /// tiny upper tail keeps its precision.
    /// </summary>
    /// <param name="a">The shape, greater than zero.</param>
    /// <param name="x">Zero or greater.</param>
    /// <returns>Q(a, x), between 0 and 1.</returns>
    public static double RegularizedGammaQ(double a, double x)
    {
        ValidateGamma(a, x);
        if (x == 0)
        {
            return 1;
        }

        if (double.IsPositiveInfinity(x))
        {
            return 0;
        }

        return x < a + 1 ? 1 - GammaSeries(a, x) : GammaContinuedFraction(a, x);
    }

    private static void ValidateGamma(double a, double x)
    {
        if (!(a > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(a), ShapeMustBePositive);
        }

        if (!(x >= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(x), XMustBeNonNegative);
        }
    }

    private static double GammaSeries(double a, double x)
    {
        double term = 1.0 / a;
        double sum = term;
        double ap = a;
        for (int n = 0; n < MaxIterations; n++)
        {
            ap += 1;
            term *= x / ap;
            sum += term;
            if (System.Math.Abs(term) < System.Math.Abs(sum) * Epsilon)
            {
                break;
            }
        }

        return sum * System.Math.Exp(-x + (a * System.Math.Log(x)) - LogGamma(a));
    }

    private static double GammaContinuedFraction(double a, double x)
    {
        double b = x + 1 - a;
        double c = 1.0 / TinyFloat;
        double d = 1.0 / b;
        double h = d;
        for (int i = 1; i < MaxIterations; i++)
        {
            double an = -i * (i - a);
            b += 2;
            d = (an * d) + b;
            if (System.Math.Abs(d) < TinyFloat)
            {
                d = TinyFloat;
            }

            c = b + (an / c);
            if (System.Math.Abs(c) < TinyFloat)
            {
                c = TinyFloat;
            }

            d = 1.0 / d;
            double delta = d * c;
            h *= delta;
            if (System.Math.Abs(delta - 1) < Epsilon)
            {
                break;
            }
        }

        return System.Math.Exp(-x + (a * System.Math.Log(x)) - LogGamma(a)) * h;
    }

    #endregion

    #region Incomplete beta

    /// <summary>
    /// The regularised incomplete beta function I_x(a, b): the chance a Beta(a, b) variable is at most
    /// <paramref name="x"/>. The cumulative distribution of Student's t, F and the binomial are all
    /// expressed through it.
    /// </summary>
    /// <param name="x">Between 0 and 1.</param>
    /// <param name="a">First shape, greater than zero.</param>
    /// <param name="b">Second shape, greater than zero.</param>
    /// <returns>I_x(a, b), between 0 and 1.</returns>
    public static double RegularizedIncompleteBeta(double x, double a, double b)
    {
        if (a <= 0 || b <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(a), ShapeMustBePositive);
        }

        if (!(x >= 0 && x <= 1))
        {
            throw new ArgumentOutOfRangeException(nameof(x), XMustBeUnitInterval);
        }

        if (x == 0 || x == 1)
        {
            return x;
        }

        double front = System.Math.Exp(
            LogGamma(a + b) - LogGamma(a) - LogGamma(b) + (a * System.Math.Log(x)) + (b * System.Math.Log(1 - x)));

        return x < (a + 1) / (a + b + 2)
            ? front * BetaContinuedFraction(x, a, b) / a
            : 1 - (front * BetaContinuedFraction(1 - x, b, a) / b);
    }

    /// <summary>
    /// The inverse of <see cref="RegularizedIncompleteBeta"/> in x: the value whose regularised
    /// incomplete beta is <paramref name="p"/>.
    /// </summary>
    /// <param name="p">A probability between 0 and 1.</param>
    /// <param name="a">First shape, greater than zero.</param>
    /// <param name="b">Second shape, greater than zero.</param>
    /// <returns>The x with I_x(a, b) = p.</returns>
    public static double InverseRegularizedIncompleteBeta(double p, double a, double b)
    {
        if (!(p >= 0 && p <= 1))
        {
            throw new ArgumentOutOfRangeException(nameof(p), XMustBeUnitInterval);
        }

        return Solve.Monotone(x => RegularizedIncompleteBeta(x, a, b), p, 0, 1);
    }

    private static double BetaContinuedFraction(double x, double a, double b)
    {
        double qab = a + b;
        double qap = a + 1;
        double qam = a - 1;
        double c = 1;
        double d = 1 - (qab * x / qap);
        if (System.Math.Abs(d) < TinyFloat)
        {
            d = TinyFloat;
        }

        d = 1 / d;
        double h = d;
        for (int m = 1; m < MaxIterations; m++)
        {
            int m2 = 2 * m;
            double aa = m * (b - m) * x / ((qam + m2) * (a + m2));
            d = 1 + (aa * d);
            if (System.Math.Abs(d) < TinyFloat)
            {
                d = TinyFloat;
            }

            c = 1 + (aa / c);
            if (System.Math.Abs(c) < TinyFloat)
            {
                c = TinyFloat;
            }

            d = 1 / d;
            h *= d * c;

            aa = -(a + m) * (qab + m) * x / ((a + m2) * (qap + m2));
            d = 1 + (aa * d);
            if (System.Math.Abs(d) < TinyFloat)
            {
                d = TinyFloat;
            }

            c = 1 + (aa / c);
            if (System.Math.Abs(c) < TinyFloat)
            {
                c = TinyFloat;
            }

            d = 1 / d;
            double delta = d * c;
            h *= delta;
            if (System.Math.Abs(delta - 1) < Epsilon)
            {
                break;
            }
        }

        return h;
    }

    #endregion

    #region Error function

    /// <summary>The error function erf(x) = 2/√π ∫₀ˣ e^(-t²) dt.</summary>
    /// <param name="x">Any real number.</param>
    /// <returns>erf(x), between -1 and 1.</returns>
    public static double Erf(double x)
    {
        if (double.IsNaN(x))
        {
            return double.NaN;
        }

        double magnitude = RegularizedGammaP(0.5, x * x);
        return x < 0 ? -magnitude : magnitude;
    }

    /// <summary>
    /// The complementary error function erfc(x) = 1 - erf(x), computed directly so it keeps its
    /// precision far into the tail.
    /// </summary>
    /// <param name="x">Any real number.</param>
    /// <returns>erfc(x), between 0 and 2.</returns>
    public static double Erfc(double x)
    {
        if (double.IsNaN(x))
        {
            return double.NaN;
        }

        return x < 0 ? 1 + RegularizedGammaP(0.5, x * x) : RegularizedGammaQ(0.5, x * x);
    }

    /// <summary>The inverse error function: the x with erf(x) = <paramref name="y"/>.</summary>
    /// <param name="y">Between -1 and 1.</param>
    /// <returns>erf⁻¹(y); infinite at ±1.</returns>
    public static double InverseErf(double y)
    {
        if (!(y >= -1 && y <= 1))
        {
            throw new ArgumentOutOfRangeException(nameof(y), "y must lie between -1 and 1.");
        }

        return NormalDistribution.Standard.Quantile((y + 1) / 2) / System.Math.Sqrt(2);
    }

    #endregion
}
