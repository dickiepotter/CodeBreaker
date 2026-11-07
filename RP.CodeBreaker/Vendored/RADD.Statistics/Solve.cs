// Copyright (c) 2026 RADD Solutions Limited. All rights reserved. Basis: repository confirmed as wholly RADD's work.
namespace RP.CodeBreaker.Statistics;

/// <summary>Root finding for the inverses of monotone functions (quantiles of a cumulative distribution).</summary>
internal static class Solve
{
    /// <summary>
    /// Find x in [<paramref name="low"/>, <paramref name="high"/>] with f(x) = <paramref name="target"/>
    /// for a non-decreasing f, by bisection. Either bound may be infinite; the bracket is then grown
    /// outwards from zero until it straddles the target.
    /// </summary>
    public static double Monotone(Func<double, double> f, double target, double low, double high)
    {
        if (double.IsNegativeInfinity(low) || double.IsPositiveInfinity(high))
        {
            double lo = double.IsNegativeInfinity(low) ? -1 : low;
            double hi = double.IsPositiveInfinity(high) ? 1 : high;
            for (int i = 0; i < 2000 && double.IsNegativeInfinity(low) && f(lo) > target; i++)
            {
                hi = System.Math.Min(hi, lo);
                lo *= 2;
            }

            for (int i = 0; i < 2000 && double.IsPositiveInfinity(high) && f(hi) < target; i++)
            {
                lo = System.Math.Max(lo, hi);
                hi *= 2;
            }

            low = lo;
            high = hi;
        }

        for (int i = 0; i < 200; i++)
        {
            double mid = 0.5 * (low + high);
            if (mid == low || mid == high)
            {
                break;
            }

            if (f(mid) < target)
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }

        return 0.5 * (low + high);
    }
}
