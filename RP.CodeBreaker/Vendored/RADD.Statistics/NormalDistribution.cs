// Copyright (c) 2026 RADD Solutions Limited. All rights reserved. Basis: repository confirmed as wholly RADD's work.
namespace RP.CodeBreaker.Statistics;

/// <summary>The normal (Gaussian) distribution N(μ, σ²).</summary>
/// <implementation>
/// The quantile starts from Acklam's rational approximation (relative error 1.15e-9) and takes one
/// Halley step against the erfc-based cdf, which brings it to full double precision.
/// </implementation>
/// <Acknowledgement>Peter J. Acklam, "An algorithm for computing the inverse normal cumulative distribution function" (2003).</Acknowledgement>
public sealed class NormalDistribution : IContinuousDistribution
{
    #region Acklam coefficients

    private static readonly double[] A = [-3.969683028665376e+01, 2.209460984245205e+02, -2.759285104469687e+02, 1.383577518672690e+02, -3.066479806614716e+01, 2.506628277459239e+00];
    private static readonly double[] B = [-5.447609879822406e+01, 1.615858368580409e+02, -1.556989798598866e+02, 6.680131188771972e+01, -1.328068155288572e+01];
    private static readonly double[] C = [-7.784894002430293e-03, -3.223964580411365e-01, -2.400758277161838e+00, -2.549732539343734e+00, 4.374664141464968e+00, 2.938163982698783e+00];
    private static readonly double[] D = [7.784695709041462e-03, 3.224671290700398e-01, 2.445134137142996e+00, 3.754408661907416e+00];

    #endregion

    /// <summary>Create N(<paramref name="mean"/>, <paramref name="standardDeviation"/>²).</summary>
    public NormalDistribution(double mean, double standardDeviation)
    {
        DistributionGuard.Positive(standardDeviation, nameof(standardDeviation));
        this.Mean = mean;
        this.StandardDeviation = standardDeviation;
    }

    /// <summary>The standard normal, N(0, 1).</summary>
    public static NormalDistribution Standard { get; } = new(0, 1);

    /// <inheritdoc/>
    public double Mean { get; }

    /// <summary>The standard deviation σ.</summary>
    public double StandardDeviation { get; }

    /// <inheritdoc/>
    public double Variance => this.StandardDeviation * this.StandardDeviation;

    /// <inheritdoc/>
    public double Pdf(double x)
    {
        double z = (x - this.Mean) / this.StandardDeviation;
        return System.Math.Exp(-0.5 * z * z) / (this.StandardDeviation * System.Math.Sqrt(2 * System.Math.PI));
    }

    /// <inheritdoc/>
    public double Cdf(double x) => 0.5 * SpecialFunctions.Erfc(-(x - this.Mean) / (this.StandardDeviation * System.Math.Sqrt(2)));

    /// <inheritdoc/>
    public double Survival(double x) => 0.5 * SpecialFunctions.Erfc((x - this.Mean) / (this.StandardDeviation * System.Math.Sqrt(2)));

    /// <inheritdoc/>
    public double Quantile(double p)
    {
        DistributionGuard.Probability(p);
        return this.Mean + (this.StandardDeviation * StandardQuantile(p));
    }

    /// <summary>The two-sided p-value of a standard normal statistic: P(|Z| ≥ |<paramref name="z"/>|).</summary>
    public static double TwoTailed(double z) => SpecialFunctions.Erfc(System.Math.Abs(z) / System.Math.Sqrt(2));

    private static double StandardQuantile(double p)
    {
        if (p == 0)
        {
            return double.NegativeInfinity;
        }

        if (p == 1)
        {
            return double.PositiveInfinity;
        }

        const double low = 0.02425;
        double x;
        if (p < low)
        {
            double q = System.Math.Sqrt(-2 * System.Math.Log(p));
            x = (((((C[0] * q + C[1]) * q + C[2]) * q + C[3]) * q + C[4]) * q + C[5]) / ((((D[0] * q + D[1]) * q + D[2]) * q + D[3]) * q + 1);
        }
        else if (p <= 1 - low)
        {
            double q = p - 0.5;
            double r = q * q;
            x = (((((A[0] * r + A[1]) * r + A[2]) * r + A[3]) * r + A[4]) * r + A[5]) * q / (((((B[0] * r + B[1]) * r + B[2]) * r + B[3]) * r + B[4]) * r + 1);
        }
        else
        {
            double q = System.Math.Sqrt(-2 * System.Math.Log(1 - p));
            x = -(((((C[0] * q + C[1]) * q + C[2]) * q + C[3]) * q + C[4]) * q + C[5]) / ((((D[0] * q + D[1]) * q + D[2]) * q + D[3]) * q + 1);
        }

        // One Halley refinement step against the precise cdf.
        double e = (0.5 * SpecialFunctions.Erfc(-x / System.Math.Sqrt(2))) - p;
        double u = e * System.Math.Sqrt(2 * System.Math.PI) * System.Math.Exp(x * x / 2);
        return x - (u / (1 + (x * u / 2)));
    }
}
