using System.Globalization;
using System.Numerics;

namespace RP.CodeBreaker;

/// <summary>
/// Someone (or something) trying keys, described by how many keys they can test per second. The
/// presets span eighty years of code breaking so that the same key space can be timed against a
/// clerk with a pencil, a Bletchley Park bombe and a modern graphics card.
/// </summary>
/// <param name="Name">Who is searching.</param>
/// <param name="KeysPerSecond">How many keys they test each second.</param>
/// <param name="Notes">Where the rate comes from.</param>
public sealed record Attacker(string Name, double KeysPerSecond, string Notes)
{
    /// <summary>
    /// One person with pencil and paper: about one key every ten seconds for a simple cipher
    /// (write out a trial alphabet, decrypt a few letters, see if it reads).
    /// </summary>
    public static Attacker PencilAndPaper { get; } = new("Pencil and paper", 0.1, "one trial decryption every ten seconds or so");

    /// <summary>
    /// A single Bletchley Park bombe. It worked through the 17,576 rotor positions of one wheel order
    /// in roughly twenty minutes — about fifteen positions a second — testing every plugboard
    /// hypothesis for each position at once, electrically.
    /// </summary>
    public static Attacker Bombe { get; } = new("A Bletchley bombe (1943)", 15, "17,576 positions per wheel order in about 20 minutes");

    /// <summary>
    /// One core of an ordinary laptop running a cheap key test (a few dozen letters decrypted and
    /// scored): roughly ten million keys a second.
    /// </summary>
    public static Attacker Laptop { get; } = new("A laptop (one core)", 1e7, "a cheap trial decryption of a few dozen letters");

    /// <summary>
    /// A modern gaming graphics card testing a simple hash or cipher: around ten billion keys a
    /// second.
    /// </summary>
    public static Attacker GraphicsCard { get; } = new("A gaming graphics card", 1e10, "SHA-256 or DES-class key tests on thousands of cores");

    /// <summary>
    /// The EFF's "Deep Crack" machine, which in 1998 searched DES keys at about 90 billion a second
    /// and found one in 56 hours — the demonstration that 56-bit keys were finished.
    /// </summary>
    public static Attacker DeepCrack { get; } = new("EFF Deep Crack (1998)", 9e10, "custom DES-searching hardware, $250,000");

    /// <summary>
    /// Every Bitcoin miner on Earth, which together compute about 10²¹ SHA-256 hashes a second —
    /// the largest key-testing effort humanity has ever assembled. The honest upper bound for "how
    /// fast could anyone search?".
    /// </summary>
    public static Attacker WholeBitcoinNetwork { get; } = new("The whole Bitcoin network", 1e21, "≈ 10^21 hashes a second, the largest search ever built");

    /// <summary>All the presets, slowest first.</summary>
    public static IReadOnlyList<Attacker> All { get; } = [PencilAndPaper, Bombe, Laptop, GraphicsCard, DeepCrack, WholeBitcoinNetwork];
}

/// <summary>
/// The verdict on a search: can it be done while you wait, or not in the lifetime of the universe?
/// </summary>
public enum Feasibility
{
    /// <summary>Under a second.</summary>
    Instant,

    /// <summary>Up to five minutes — "breakable in a few minutes", the line this library cares about.</summary>
    FewMinutes,

    /// <summary>Up to a day: a long wait, but entirely practical.</summary>
    Hours,

    /// <summary>Up to a year: practical for a determined, funded attacker.</summary>
    Months,

    /// <summary>Up to a century: possible in principle, useless in practice for most secrets.</summary>
    Lifetime,

    /// <summary>Longer than a century — and usually longer than the age of the universe.</summary>
    Infeasible,
}

/// <summary>How long a number of key trials takes a given <see cref="Attacker"/>, and what that means.</summary>
public readonly struct WorkEstimate
{
    /// <summary>The threshold this library calls "a few minutes": five minutes.</summary>
    public const double FewMinutesSeconds = 5 * 60;

    private const double SecondsPerYear = 365.25 * 24 * 3600;
    private const double AgeOfUniverseYears = 1.38e10;

    private WorkEstimate(BigInteger trials, Attacker attacker, double seconds)
    {
        this.Trials = trials;
        this.Attacker = attacker;
        this.Seconds = seconds;
    }

    /// <summary>The number of keys to be tried.</summary>
    public BigInteger Trials { get; }

    /// <summary>Who is trying them.</summary>
    public Attacker Attacker { get; }

    /// <summary>
    /// How long it takes, in seconds. A <c>double</c> reaches 10³⁰⁸, so even 2²⁵⁶ trials at a
    /// handful a second fit; anything past that is reported as infinity.
    /// </summary>
    public double Seconds { get; }

    /// <summary>The verdict for this amount of time.</summary>
    public Feasibility Feasibility => Classify(this.Seconds);

    /// <summary>Can it be done in "a few minutes" (five minutes or less)?</summary>
    public bool WithinFewMinutes => this.Seconds <= FewMinutesSeconds;

    /// <summary>The time for <paramref name="trials"/> key tests by <paramref name="attacker"/>.</summary>
    public static WorkEstimate For(BigInteger trials, Attacker attacker)
    {
        double logSeconds = (trials <= 0 ? 0 : BigInteger.Log(trials)) - Math.Log(attacker.KeysPerSecond);
        double seconds = trials <= 0 ? 0 : Math.Exp(logSeconds);
        return new WorkEstimate(trials, attacker, seconds);
    }

    /// <summary>The verdict for a number of seconds.</summary>
    public static Feasibility Classify(double seconds) => seconds switch
    {
        < 1 => Feasibility.Instant,
        <= FewMinutesSeconds => Feasibility.FewMinutes,
        <= 24 * 3600 => Feasibility.Hours,
        <= SecondsPerYear => Feasibility.Months,
        <= 100 * SecondsPerYear => Feasibility.Lifetime,
        _ => Feasibility.Infeasible,
    };

    /// <summary>
    /// A duration in friendly words: "0.3 seconds", "4 minutes", "11 days", "3.2 × 10^14 years
    /// (23,000 times the age of the universe)".
    /// </summary>
    public static string Describe(double seconds)
    {
        if (double.IsInfinity(seconds) || double.IsNaN(seconds))
        {
            return "longer than any number can say";
        }

        static string N(double v) => v >= 100 ? v.ToString("N0", CultureInfo.InvariantCulture) : v.ToString("0.#", CultureInfo.InvariantCulture);

        if (seconds < 1e-3)
        {
            return "under a millisecond";
        }

        if (seconds < 1)
        {
            return $"{N(seconds * 1000)} milliseconds";
        }

        if (seconds < 120)
        {
            return $"{N(seconds)} seconds";
        }

        if (seconds < 2 * 3600)
        {
            return $"{N(seconds / 60)} minutes";
        }

        if (seconds < 2 * 86400)
        {
            return $"{N(seconds / 3600)} hours";
        }

        double years = seconds / SecondsPerYear;
        if (years < 1)
        {
            return $"{N(seconds / 86400)} days";
        }

        if (years < 1e6)
        {
            return $"{N(years)} years";
        }

        string big = KeySpace.Format(new BigInteger(Math.Min(years, 1e300))) + " years";
        double ages = years / AgeOfUniverseYears;
        return ages >= 1
            ? $"{big.TrimStart('≈', ' ')} ({KeySpace.Format(new BigInteger(Math.Min(ages, 1e300))).TrimStart('≈', ' ')} times the age of the universe)"
            : big.TrimStart('≈', ' ');
    }

    /// <inheritdoc/>
    public override string ToString() => $"{Describe(this.Seconds)} for {this.Attacker.Name}";
}
