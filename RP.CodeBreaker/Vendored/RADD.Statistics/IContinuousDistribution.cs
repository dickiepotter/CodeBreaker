// Copyright (c) 2026 RADD Solutions Limited. All rights reserved. Basis: repository confirmed as wholly RADD's work.
namespace RP.CodeBreaker.Statistics;

/// <summary>A probability distribution over the real numbers.</summary>
public interface IContinuousDistribution
{
    /// <summary>The expected value; NaN or infinity where it does not exist.</summary>
    double Mean { get; }

    /// <summary>The variance; NaN or infinity where it does not exist.</summary>
    double Variance { get; }

    /// <summary>The standard deviation, √<see cref="Variance"/>.</summary>
    double StandardDeviation => System.Math.Sqrt(this.Variance);

    /// <summary>The probability density at <paramref name="x"/>.</summary>
    double Pdf(double x);

    /// <summary>The cumulative probability P(X ≤ <paramref name="x"/>).</summary>
    double Cdf(double x);

    /// <summary>
    /// The upper tail P(X &gt; <paramref name="x"/>) = 1 - Cdf(x), computed directly where that keeps
    /// precision for small p-values.
    /// </summary>
    double Survival(double x);

    /// <summary>The inverse of <see cref="Cdf"/>: the x with P(X ≤ x) = <paramref name="p"/>.</summary>
    double Quantile(double p);
}

/// <summary>A probability distribution over the integers.</summary>
public interface IDiscreteDistribution
{
    /// <summary>The expected value.</summary>
    double Mean { get; }

    /// <summary>The variance.</summary>
    double Variance { get; }

    /// <summary>The probability P(X = <paramref name="k"/>).</summary>
    double Pmf(int k);

    /// <summary>The cumulative probability P(X ≤ <paramref name="k"/>).</summary>
    double Cdf(int k);

    /// <summary>The upper tail P(X &gt; <paramref name="k"/>).</summary>
    double Survival(int k);

    /// <summary>The smallest k with P(X ≤ k) ≥ <paramref name="p"/>.</summary>
    int Quantile(double p);
}

/// <summary>Argument checks shared by the distributions.</summary>
internal static class DistributionGuard
{
    public const string ProbabilityOutOfRange = "The probability must lie between 0 and 1.";

    public static void Probability(double p)
    {
        if (!(p >= 0 && p <= 1))
        {
            throw new ArgumentOutOfRangeException(nameof(p), ProbabilityOutOfRange);
        }
    }

    public static void Positive(double value, string name)
    {
        if (!(value > 0) || double.IsInfinity(value))
        {
            throw new ArgumentOutOfRangeException(name, $"{name} must be a finite number greater than zero.");
        }
    }
}
