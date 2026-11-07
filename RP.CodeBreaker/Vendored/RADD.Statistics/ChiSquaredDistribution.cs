// Copyright (c) 2026 RADD Solutions Limited. All rights reserved. Basis: repository confirmed as wholly RADD's work.
namespace RP.CodeBreaker.Statistics;

/// <summary>The chi-squared distribution with k degrees of freedom.</summary>
public sealed class ChiSquaredDistribution : IContinuousDistribution
{
    /// <summary>Create a chi-squared distribution with <paramref name="degreesOfFreedom"/> degrees of freedom.</summary>
    public ChiSquaredDistribution(double degreesOfFreedom)
    {
        DistributionGuard.Positive(degreesOfFreedom, nameof(degreesOfFreedom));
        this.DegreesOfFreedom = degreesOfFreedom;
    }

    /// <summary>The degrees of freedom k.</summary>
    public double DegreesOfFreedom { get; }

    /// <inheritdoc/>
    public double Mean => this.DegreesOfFreedom;

    /// <inheritdoc/>
    public double Variance => 2 * this.DegreesOfFreedom;

    /// <inheritdoc/>
    public double Pdf(double x)
    {
        if (x < 0)
        {
            return 0;
        }

        double k = this.DegreesOfFreedom;
        if (x == 0)
        {
            return k < 2 ? double.PositiveInfinity : k == 2 ? 0.5 : 0;
        }

        return System.Math.Exp(((k / 2) - 1) * System.Math.Log(x) - (x / 2) - (k / 2 * System.Math.Log(2)) - SpecialFunctions.LogGamma(k / 2));
    }

    /// <inheritdoc/>
    public double Cdf(double x) => x <= 0 ? 0 : SpecialFunctions.RegularizedGammaP(this.DegreesOfFreedom / 2, x / 2);

    /// <inheritdoc/>
    public double Survival(double x) => x <= 0 ? 1 : SpecialFunctions.RegularizedGammaQ(this.DegreesOfFreedom / 2, x / 2);

    /// <inheritdoc/>
    public double Quantile(double p)
    {
        DistributionGuard.Probability(p);
        if (p == 1)
        {
            return double.PositiveInfinity;
        }

        return Solve.Monotone(this.Cdf, p, 0, double.PositiveInfinity);
    }

    /// <summary>The upper-tail p-value P(χ² ≥ <paramref name="statistic"/>).</summary>
    public static double UpperTail(double statistic, double degreesOfFreedom) => new ChiSquaredDistribution(degreesOfFreedom).Survival(statistic);
}
