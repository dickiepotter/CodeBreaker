using System.Numerics;

namespace RP.CodeBreaker.Ciphers;

/// <summary>
/// The Caesar cipher: shift every letter a fixed number of places along the alphabet. With a shift
/// of 3, A becomes D, B becomes E, … X wraps round to A. Suetonius tells us Julius Caesar used it
/// for his private letters. ROT13 is the same thing with a shift of 13.
/// </summary>
/// <remarks>
/// Encrypt: c = (p + k) mod 26. Decrypt: p = (c − k) mod 26. There are only 26 shifts (25 useful
/// ones), so the cipher is broken by simply trying them all — see <see cref="Attacks.CaesarAttack"/>.
/// </remarks>
public sealed class CaesarCipher : ICipher
{
    /// <summary>Create a Caesar cipher with a given shift (any integer; it is wrapped onto 0–25).</summary>
    public CaesarCipher(int shift)
    {
        this.Shift = Letters.Mod(shift, 26);
    }

    /// <summary>ROT13: the shift of 13 that is its own inverse, once used to hide joke punchlines on Usenet.</summary>
    public static CaesarCipher Rot13 { get; } = new(13);

    /// <summary>26 shifts, one of which (0) does nothing.</summary>
    public static KeySpace KeySpace { get; } = KeySpace.Of(26, "26 shifts");

    /// <summary>The shift, 0–25.</summary>
    public int Shift { get; }

    /// <inheritdoc/>
    public string Name => "Caesar";

    /// <inheritdoc/>
    public string KeyDescription => $"shift {this.Shift} (A→{Letters.ToLetter(this.Shift)})";

    /// <inheritdoc/>
    public string Encrypt(string plaintext) => Letters.MapLetters(plaintext, (p, _) => p + this.Shift);

    /// <inheritdoc/>
    public string Decrypt(string ciphertext) => Letters.MapLetters(ciphertext, (c, _) => c - this.Shift);
}

/// <summary>
/// The Atbash cipher: reverse the alphabet, so A↔Z, B↔Y, C↔X. Used for a handful of words in the
/// Hebrew Bible (Sheshach for Babel in Jeremiah). It has exactly one key, so it is not really a
/// cipher at all — just a code everyone knows.
/// </summary>
public sealed class AtbashCipher : ICipher
{
    /// <summary>One key: the reversed alphabet.</summary>
    public static KeySpace KeySpace { get; } = KeySpace.Of(1, "one fixed alphabet");

    /// <inheritdoc/>
    public string Name => "Atbash";

    /// <inheritdoc/>
    public string KeyDescription => "A↔Z (no key)";

    /// <inheritdoc/>
    public string Encrypt(string plaintext) => Letters.MapLetters(plaintext, (p, _) => 25 - p);

    /// <inheritdoc/>
    public string Decrypt(string ciphertext) => this.Encrypt(ciphertext);
}

/// <summary>
/// The affine cipher: multiply, then shift. c = (a·p + b) mod 26. Caesar is the special case a = 1,
/// Atbash is a = 25, b = 25.
/// </summary>
/// <remarks>
/// <para>
/// To decrypt we must undo the multiplication, which needs the inverse of a modulo 26 (see
/// <see cref="NumberTheory.ModInverse(int, int)"/>). That only exists when a shares no factor with
/// 26 = 2 × 13, leaving 12 usable multipliers (1, 3, 5, 7, 9, 11, 15, 17, 19, 21, 23, 25). With 26
/// shifts that makes 12 × 26 = 312 keys — still nothing to a brute-force search.
/// </para>
/// </remarks>
public sealed class AffineCipher : ICipher
{
    /// <summary>Create an affine cipher. <paramref name="a"/> must share no factor with 26.</summary>
    public AffineCipher(int a, int b)
    {
        this.A = Letters.Mod(a, 26);
        this.B = Letters.Mod(b, 26);
        this.InverseA = NumberTheory.ModInverse(this.A, 26)
            ?? throw new ArgumentException($"{a} has no inverse modulo 26 (it must be odd and not 13), so the message could never be decrypted.", nameof(a));
    }

    /// <summary>The twelve multipliers that have an inverse modulo 26.</summary>
    public static IReadOnlyList<int> ValidMultipliers { get; } = Enumerable.Range(1, 25).Where(a => NumberTheory.Gcd(a, 26) == 1).ToArray();

    /// <summary>12 multipliers × 26 shifts = 312 keys.</summary>
    public static KeySpace KeySpace { get; } = KeySpace.Of(312, "12 multipliers × 26 shifts");

    /// <summary>The multiplier.</summary>
    public int A { get; }

    /// <summary>The shift.</summary>
    public int B { get; }

    /// <summary>The multiplier that undoes <see cref="A"/>.</summary>
    public int InverseA { get; }

    /// <inheritdoc/>
    public string Name => "Affine";

    /// <inheritdoc/>
    public string KeyDescription => $"a = {this.A}, b = {this.B}";

    /// <inheritdoc/>
    public string Encrypt(string plaintext) => Letters.MapLetters(plaintext, (p, _) => (this.A * p) + this.B);

    /// <inheritdoc/>
    public string Decrypt(string ciphertext) => Letters.MapLetters(ciphertext, (c, _) => this.InverseA * (c - this.B));
}

/// <summary>
/// The simple (monoalphabetic) substitution cipher: any rearrangement of the alphabet. Each
/// plaintext letter is always replaced by the same ciphertext letter, but the replacement alphabet
/// can be any of 26! ≈ 4 × 10²⁶ orderings.
/// </summary>
/// <remarks>
/// <para>
/// This is the cipher that shows key space is not the same as security. 4 × 10²⁶ keys is about 88
/// bits — more than DES — yet a newspaper cryptogram of this kind is solved over breakfast, because
/// frequency analysis attacks the letters one at a time instead of the key all at once. Mary, Queen
/// of Scots trusted a cipher like this (with extra symbols for common words) in the Babington plot of
/// 1586; Thomas Phelippes broke it by letter frequencies and she was executed.
/// </para>
/// </remarks>
public sealed class SubstitutionCipher : ICipher
{
    private readonly int[] encrypt = new int[26];
    private readonly int[] decrypt = new int[26];

    /// <summary>
    /// Create a substitution cipher from its cipher alphabet: the 26 letters that A, B, C … Z become,
    /// in order. <c>"QWERTYUIOPASDFGHJKLZXCVBNM"</c> sends A→Q, B→W and so on.
    /// </summary>
    public SubstitutionCipher(string cipherAlphabet)
    {
        string alphabet = Letters.Normalise(cipherAlphabet);
        if (alphabet.Length != 26 || alphabet.Distinct().Count() != 26)
        {
            throw new ArgumentException("A substitution alphabet must use each of the 26 letters exactly once.", nameof(cipherAlphabet));
        }

        this.CipherAlphabet = alphabet;
        for (int p = 0; p < 26; p++)
        {
            int c = alphabet[p] - 'A';
            this.encrypt[p] = c;
            this.decrypt[c] = p;
        }
    }

    /// <summary>26! = 403,291,461,126,605,635,584,000,000 alphabets (≈ 88.4 bits).</summary>
    public static KeySpace KeySpace { get; } = KeySpace.Factorial(26, "26! orderings of the alphabet");

    /// <summary>The letters A–Z become, in order.</summary>
    public string CipherAlphabet { get; }

    /// <summary>The decryption alphabet: what ciphertext A, B, C … Z stand for.</summary>
    public string PlainAlphabet => Letters.FromNumbers(this.decrypt);

    /// <inheritdoc/>
    public string Name => "Simple substitution";

    /// <inheritdoc/>
    public string KeyDescription => this.CipherAlphabet;

    /// <summary>
    /// A keyword alphabet: the keyword without repeats, then the unused letters in order.
    /// <c>FromKeyword("ZEBRAS")</c> is <c>ZEBRASCDFGHIJKLMNOPQTUVWXY</c>.
    /// </summary>
    public static SubstitutionCipher FromKeyword(string keyword) => new(Letters.KeywordAlphabet(keyword));

    /// <summary>Build a cipher from a decryption alphabet (what each ciphertext letter means).</summary>
    public static SubstitutionCipher FromPlainAlphabet(string plainAlphabet)
    {
        string p = Letters.Normalise(plainAlphabet);
        var cipher = new char[26];
        for (int c = 0; c < 26; c++)
        {
            cipher[p[c] - 'A'] = Letters.ToLetter(c);
        }

        return new SubstitutionCipher(new string(cipher));
    }

    /// <summary>A uniformly random alphabet (Fisher–Yates shuffle).</summary>
    public static SubstitutionCipher Random(Random random)
    {
        char[] a = Letters.Alphabet.ToCharArray();
        for (int i = a.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (a[i], a[j]) = (a[j], a[i]);
        }

        return new SubstitutionCipher(new string(a));
    }

    /// <summary>
    /// The keys left once <paramref name="knownLetters"/> letters of the alphabet are pinned down: the
    /// remaining letters can still be in any order, (26 − k)!. Ten known letters cut 26! ≈ 4 × 10²⁶ to
    /// 16! ≈ 2 × 10¹³.
    /// </summary>
    public static BigInteger KeysWithKnownLetters(int knownLetters) => KeySpace.FactorialOf(26 - Math.Clamp(knownLetters, 0, 26));

    /// <inheritdoc/>
    public string Encrypt(string plaintext) => Letters.MapLetters(plaintext, (p, _) => this.encrypt[p]);

    /// <inheritdoc/>
    public string Decrypt(string ciphertext) => Letters.MapLetters(ciphertext, (c, _) => this.decrypt[c]);
}
