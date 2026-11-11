using System.Globalization;
using System.Numerics;

namespace RP.CodeBreaker;

/// <summary>
/// One step in cutting a search down to size: a piece of insight ("the key length is 5") or of
/// known information ("the message begins WETTERBERICHT") and what it did to the number of keys
/// still to be tried.
/// </summary>
/// <param name="Insight">What was learned or assumed.</param>
/// <param name="Before">Keys left before this step.</param>
/// <param name="After">Keys left after it.</param>
/// <param name="Why">Why it shrinks the search, in a sentence.</param>
public sealed record Reduction(string Insight, BigInteger Before, BigInteger After, string Why)
{
    /// <summary>How many bits of work this step removed: log₂(Before / After).</summary>
    public double BitsSaved => KeySpace.Log2(this.Before) - KeySpace.Log2(this.After);

    /// <summary>The factor the search shrank by (Before ÷ After), for display.</summary>
    public string Factor => this.After <= 0 ? "∞" : KeySpace.Format(this.Before / BigInteger.Max(this.After, 1));
}

/// <summary>
/// The story of how hard a cipher was to break: the key space we started with, each insight that
/// shrank it, and the search that was actually left. This is the library's answer to "how difficult
/// is this?" — not a single number, but the funnel from the naive brute force to the real work.
/// </summary>
/// <remarks>
/// <para>
/// Every attack in <c>RP.CodeBreaker.Attacks</c> returns one of these. A Vigenère break, for
/// example, reads:
/// </para>
/// <code>
/// Start: every key up to 12 letters   ≈ 9.98 × 10^16 keys (56.5 bits)
/// Key length is 5 (index of coincidence)    → 11,881,376  (23.5 bits)
/// Columns are independent Caesar ciphers    → 130         (7.0 bits)
/// </code>
/// <para>
/// The last line is the point: the work is not 26⁵ trial decryptions but 5 × 26, because once the
/// key length is known the five key letters can be found <i>one at a time</i>. A product has become
/// a sum. Most of code breaking is finding a way to make that happen.
/// </para>
/// </remarks>
public sealed class Difficulty
{
    private readonly List<Reduction> steps = [];

    /// <summary>Start the story from a cipher's full key space.</summary>
    public Difficulty(KeySpace start)
    {
        this.Start = start;
    }

    /// <summary>The naive search: every possible key.</summary>
    public KeySpace Start { get; }

    /// <summary>Each insight in the order it was applied.</summary>
    public IReadOnlyList<Reduction> Steps => this.steps;

    /// <summary>The number of keys still to search after every step.</summary>
    public BigInteger Remaining => this.steps.Count == 0 ? this.Start.Size : this.steps[^1].After;

    /// <summary>The remaining search in bits.</summary>
    public double RemainingBits => KeySpace.Log2(BigInteger.Max(this.Remaining, 1));

    /// <summary>The total bits of work the insights saved.</summary>
    public double BitsSaved => this.Start.Bits - this.RemainingBits;

    /// <summary>
    /// Record an insight that leaves <paramref name="remaining"/> keys to try. Returns this object
    /// so steps can be chained.
    /// </summary>
    public Difficulty Then(string insight, BigInteger remaining, string why)
    {
        BigInteger before = this.Remaining;
        this.steps.Add(new Reduction(insight, before, BigInteger.Max(BigInteger.Min(remaining, before), 1), why));
        return this;
    }

    /// <summary>How long the remaining search takes <paramref name="attacker"/>.</summary>
    public WorkEstimate RemainingWork(Attacker attacker) => WorkEstimate.For(this.Remaining, attacker);

    /// <summary>How long the naive search would have taken <paramref name="attacker"/>.</summary>
    public WorkEstimate NaiveWork(Attacker attacker) => WorkEstimate.For(this.Start.Size, attacker);

    /// <summary>A plain-text table of the funnel, one line per step.</summary>
    public override string ToString()
    {
        var lines = new List<string>
        {
            string.Format(CultureInfo.InvariantCulture, "Start: {0,-44} {1} keys ({2:0.0} bits)", this.Start.Description, KeySpace.Format(this.Start.Size), this.Start.Bits),
        };
        foreach (Reduction r in this.steps)
        {
            lines.Add(string.Format(CultureInfo.InvariantCulture, "  -> {0,-44} {1} keys ({2:0.0} bits)", r.Insight, KeySpace.Format(r.After), KeySpace.Log2(r.After)));
        }

        return string.Join(Environment.NewLine, lines);
    }
}
