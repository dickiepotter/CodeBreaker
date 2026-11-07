using System.Numerics;
using RP.CodeBreaker.Ciphers;
using RP.CodeBreaker.Modern;

namespace RP.CodeBreaker;

/// <summary>
/// One cipher's difficulty at a glance: how many keys it has, how much of that the best attack actually
/// has to search, and what that means in time.
/// </summary>
/// <param name="Name">The cipher (and the situation, where it matters — "Enigma with a crib").</param>
/// <param name="Era">When it was in use.</param>
/// <param name="KeySpace">The full key space — the naive brute force.</param>
/// <param name="EffectiveWork">The number of trials the best known attack needs.</param>
/// <param name="Attack">The attack, in a few words.</param>
/// <param name="Explanation">Why the effective work is so much smaller (or not).</param>
/// <param name="Famous">A famous use or break.</param>
/// <param name="Breakable">False only where no amount of work can succeed (the one-time pad).</param>
public sealed record CipherProfile(
    string Name,
    string Era,
    KeySpace KeySpace,
    BigInteger EffectiveWork,
    string Attack,
    string Explanation,
    string Famous,
    bool Breakable = true)
{
    /// <summary>log₂ of the effective work.</summary>
    public double EffectiveBits => KeySpace.Log2(BigInteger.Max(1, this.EffectiveWork));

    /// <summary>How long the naive brute force takes <paramref name="attacker"/>.</summary>
    public WorkEstimate BruteForce(Attacker attacker) => this.KeySpace.ExhaustiveSearch(attacker);

    /// <summary>How long the best attack takes <paramref name="attacker"/>.</summary>
    public WorkEstimate BestAttack(Attacker attacker) => WorkEstimate.For(this.EffectiveWork, attacker);
}

/// <summary>
/// The difficulty ladder: every cipher in the library (and a few modern ones for scale), from Caesar to
/// AES, with the work its best attack needs. This is the table behind the question "which of these can
/// I break in a few minutes?".
/// </summary>
/// <remarks>
/// <para>
/// The effective-work figures are deliberately rough — orders of magnitude for a message of a few
/// hundred letters — because the point is the <i>shape</i> of the ladder: how a clever attack collapses
/// an enormous key space, and how modern ciphers are built so that no such collapse exists and the key
/// space is the real work.
/// </para>
/// </remarks>
public static class CipherCatalogue
{
    /// <summary>Every profile, easiest first.</summary>
    public static IReadOnlyList<CipherProfile> All { get; } = Build().OrderBy(p => p.Breakable ? p.EffectiveBits : double.MaxValue).ToArray();

    /// <summary>The profiles an attacker can finish within "a few minutes".</summary>
    public static IEnumerable<CipherProfile> BreakableInFewMinutes(Attacker attacker) =>
        All.Where(p => p.Breakable && p.BestAttack(attacker).WithinFewMinutes);

    private static IEnumerable<CipherProfile> Build()
    {
        yield return new("Atbash", "c. 500 BC", AtbashCipher.KeySpace, 1, "Recognise it",
            "One fixed alphabet: there is no key to find.", "Jeremiah's 'Sheshach' for Babel.");
        yield return new("Caesar", "c. 50 BC", CaesarCipher.KeySpace, 26, "Try all 26 shifts",
            "The key space is so small that the brute force is the attack.", "Julius Caesar's letters; ROT13 on Usenet.");
        yield return new("Affine", "Classical", AffineCipher.KeySpace, 312, "Try all 312 keys",
            "Still small enough to try every key.", "A favourite of puzzle books.");
        yield return new("Rail fence", "American Civil War", RailFenceCipher.KeySpaceFor(200), 198, "Try every rail count",
            "The key is one small number.", "Field ciphers of the 1860s.");
        yield return new("Hill (2×2)", "1929", HillCipher.KeySpace2x2, 1_352, "Search each matrix row alone",
            "Each row of the decryption matrix makes its own letters, so the rows are searched separately: 2 × 26², not 26⁴. Four letters of crib solve it by algebra.", "Lester Hill's linear-algebra cipher; used for radio call signs.");
        yield return new("Vigenère (key ≤ 12)", "1553–1863", VigenereCipher.KeySpaceUpTo(12), 12 * 26 * 6, "Find the period, break each column",
            "The index of coincidence finds the key length; each column is then a Caesar cipher. 26^L becomes 26 × L.", "'Le chiffre indéchiffrable', broken by Babbage and Kasiski; Kryptos K1 and K2.");
        yield return new("Autokey (primer ≤ 12)", "1586", VigenereCipher.KeySpaceUpTo(12), 12 * 12 * 26, "Find each primer letter's chain",
            "Each primer letter starts its own chain of plaintext, scored separately.", "Described by Blaise de Vigenère himself.");
        yield return new("Repeating-key XOR", "Software, 1970s–", XorCipher.KeySpaceForLength(16), 16 * 256 * 4, "Hamming distance, then byte by byte",
            "The computer-age Vigenère: find the key length, then 256 tries per byte.", "Countless home-made 'encryptions'.");
        yield return new("Columnar transposition (8 columns)", "WWI", ColumnarTranspositionCipher.KeySpaceFor(8), 40_320, "Try every column order",
            "8! orders is small enough to try them all.", "German army ÜBCHI, 1914.");
        yield return new("Simple substitution", "9th century –", SubstitutionCipher.KeySpace, 30_000, "Hill climb on quadgrams",
            "88 bits of key, but each letter is found separately by frequency: a few thousand trial keys per climb.", "Mary, Queen of Scots, 1586; every newspaper cryptogram.");
        yield return new("Polybius square", "c. 150 BC", PolybiusCipher.KeySpace, 30_000, "Relabel, then substitution attack",
            "Digit pairs are just new symbols for letters.", "Polybius's fire signals; prisoners' tap codes.");
        yield return new("Columnar transposition (15 columns)", "WWII (SOE)", ColumnarTranspositionCipher.KeySpaceFor(15), 200_000, "Hill climb on column swaps",
            "Too many orders to try, but partly-right orders show as partly-readable text.", "SOE agents' double transposition.");
        yield return new("Playfair", "1854–1945", PlayfairCipher.KeySpace, 1_000_000, "Simulated annealing",
            "79 bits of key, but the search can climb towards the answer; a few hundred thousand trial grids.", "Boer War, WWI; JFK's PT-109 rescue message, 1943.");
        yield return new("Enigma with a crib (bombe)", "1940–45", EnigmaMachine.KeySpaceFor(5, 10), 60 * 17_576 * 26, "Turing–Welchman bombe",
            "Deduction removes the 1.5 × 10¹⁴ plugboard settings and the ring settings from the search, leaving 60 × 17,576 rotor positions, each tested against 26 plug hypotheses.", "Bletchley Park: 'WETTERBERICHT' and 'KEINE BESONDEREN EREIGNISSE'.");
        yield return new("ADFGVX", "1918", KeySpace.Factorial(36, "36! squares").Times(ColumnarTranspositionCipher.KeySpaceFor(20)), BigInteger.Pow(10, 12), "Needs several messages with the same key",
            "Substitution and transposition together leave no single statistic to climb; Painvin needed luck and depth.", "Painvin's 'radiogram of victory', June 1918.");
        yield return new("Enigma without a crib", "1940–45", EnigmaMachine.KeySpaceFor(5, 10), EnigmaMachine.KeySpaceFor(5, 10).Size / 2, "Brute force",
            "With nothing known about the message, no deduction is possible: half of 2⁷⁷ on average.", "Why Bletchley hunted for cribs and operator habits.");
        yield return new("4-digit PIN (SHA-256)", "Today", KeySpace.FromBits(256, "2^256 hash outputs"), 10_000, "Hash every PIN",
            "The hash is strong; the secret was chosen from only 10,000.", "Why phones limit guesses rather than relying on the hash.");
        yield return new("Word + 2 digits (SHA-256)", "Today", KeySpace.FromBits(256, "2^256 hash outputs"), 3 * 10_000 * 102, "Dictionary attack",
            "People choose words, not random strings.", "Every leaked password database.");
        yield return new("RSA-100 bit", "Teaching", KeySpace.FromBits(50, "√n candidate factors"), BigInteger.Pow(2, 25), "Pollard's rho",
            "Factoring n gives the private key; rho needs about the fourth root of n.", "Textbook examples.");
        yield return new("RSA-512", "1990s", KeySpace.FromBits(256, "√n candidate factors"), BigInteger.Pow(2, (int)Factoring.EstimateNumberFieldSieveOperations(512)), "Number field sieve",
            "Factored in 1999 by a large team; now hours of cloud time.", "RSA-155 (1999); FREAK attack keys (2015).");
        yield return new("DES", "1977–1999", KeySpace.FromBits(56, "2^56 keys"), BigInteger.Pow(2, 55), "Brute force",
            "No shortcut worth having: the whole difficulty is the key space, and 56 bits is too few.", "EFF Deep Crack, 56 hours, 1998.");
        yield return new("RSA-2048", "Today", KeySpace.FromBits(1024, "√n candidate factors"), BigInteger.Pow(2, (int)Factoring.EstimateNumberFieldSieveOperations(2048)), "Number field sieve",
            "The best known factoring method still needs around 2¹¹⁷ operations.", "Today's TLS certificates.");
        yield return new("AES-128", "2001–", KeySpace.FromBits(128, "2^128 keys"), BigInteger.Pow(2, 127), "Brute force",
            "Designed so that no attack beats the brute force by more than a whisker.", "Everything you do online.");
        yield return new("AES-256", "2001–", KeySpace.FromBits(256, "2^256 keys"), BigInteger.Pow(2, 255), "Brute force",
            "Even a quantum computer (Grover's algorithm) only halves the bits to 128.", "Top Secret government data.");
        yield return new("One-time pad", "1917–", OneTimePad.KeySpaceForLength(200), OneTimePad.KeySpaceForLength(200).Size, "None possible",
            "Every plaintext of the right length is equally likely; trying keys gives every possible message and no way to choose.", "The Moscow–Washington hotline.", Breakable: false);
    }
}
