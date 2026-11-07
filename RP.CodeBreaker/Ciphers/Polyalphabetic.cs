using System.Numerics;
using System.Text;

namespace RP.CodeBreaker.Ciphers;

/// <summary>
/// The Vigenère cipher: a Caesar cipher whose shift changes from letter to letter, following a
/// repeating keyword. With the key LEMON, the 1st, 6th, 11th… letters are shifted by L (11), the
/// 2nd, 7th, 12th… by E (4), and so on.
/// </summary>
/// <remarks>
/// <para>
/// Described by Giovan Battista Bellaso in 1553 (and misattributed to Blaise de Vigenère), it was
/// called <i>le chiffre indéchiffrable</i> for three hundred years, because it flattens the letter
/// frequencies that break a simple substitution. Charles Babbage broke it in the 1850s and Friedrich
/// Kasiski published the method in 1863: find the key length, and it collapses into L separate
/// Caesar ciphers. See <see cref="Periodicity"/> and <see cref="Attacks.VigenereAttack"/>.
/// </para>
/// <para>
/// Optionally the tableau can be built on a mixed alphabet instead of A–Z (a "keyed" Vigenère, or
/// Quagga). Jim Sanborn's Kryptos sculpture at the CIA uses one, keyed on KRYPTOS, for its first two
/// panels.
/// </para>
/// </remarks>
public sealed class VigenereCipher : ICipher
{
    private readonly int[] shifts;
    private readonly string alphabet;
    private readonly int[] position = new int[26];

    /// <summary>Create a Vigenère cipher with a keyword (letters only; anything else is ignored).</summary>
    /// <param name="key">The repeating keyword.</param>
    /// <param name="alphabetKeyword">
    /// Optional keyword for a mixed tableau alphabet (Kryptos used "KRYPTOS"); leave null for the
    /// ordinary A–Z tableau.
    /// </param>
    public VigenereCipher(string key, string? alphabetKeyword = null)
    {
        this.Key = Letters.Normalise(key);
        if (this.Key.Length == 0)
        {
            throw new ArgumentException("A Vigenère key needs at least one letter.", nameof(key));
        }

        this.alphabet = alphabetKeyword is null ? Letters.Alphabet : Letters.KeywordAlphabet(alphabetKeyword);
        this.AlphabetKeyword = alphabetKeyword is null ? null : Letters.Normalise(alphabetKeyword);
        for (int i = 0; i < 26; i++)
        {
            this.position[this.alphabet[i] - 'A'] = i;
        }

        this.shifts = this.Key.Select(k => this.position[k - 'A']).ToArray();
    }

    /// <summary>The repeating keyword.</summary>
    public string Key { get; }

    /// <summary>The keyword the tableau alphabet was mixed with, or null for A–Z.</summary>
    public string? AlphabetKeyword { get; }

    /// <inheritdoc/>
    public string Name => this.AlphabetKeyword is null ? "Vigenère" : "Keyed Vigenère (Quagga)";

    /// <inheritdoc/>
    public string KeyDescription => this.AlphabetKeyword is null ? this.Key : $"{this.Key} on alphabet {this.AlphabetKeyword}";

    /// <summary>
    /// The key space for keys of exactly <paramref name="length"/> letters: 26^length.
    /// </summary>
    public static KeySpace KeySpaceForLength(int length) => KeySpace.Power(26, length, $"26^{length} keys of {length} letters");

    /// <summary>
    /// The key space for every key from 1 to <paramref name="maxLength"/> letters: 26 + 26² + … +
    /// 26^maxLength.
    /// </summary>
    public static KeySpace KeySpaceUpTo(int maxLength)
    {
        BigInteger total = 0;
        for (int l = 1; l <= maxLength; l++)
        {
            total += BigInteger.Pow(26, l);
        }

        return KeySpace.Of(total, $"every key of 1–{maxLength} letters");
    }

    /// <inheritdoc/>
    public string Encrypt(string plaintext) =>
        Letters.MapLetters(plaintext, (p, i) => this.alphabet[(this.position[p] + this.shifts[i % this.shifts.Length]) % 26] - 'A');

    /// <inheritdoc/>
    public string Decrypt(string ciphertext) =>
        Letters.MapLetters(ciphertext, (c, i) => this.alphabet[Letters.Mod(this.position[c] - this.shifts[i % this.shifts.Length], 26)] - 'A');
}

/// <summary>
/// The Beaufort cipher: like Vigenère but subtracting the plaintext from the key, c = (k − p) mod 26.
/// Its charm is that encryption and decryption are the same operation, so one table or one machine
/// does both — which is why the US Army's M-209 field cipher machine (1940s) used it.
/// </summary>
public sealed class BeaufortCipher : ICipher
{
    private readonly int[] key;

    /// <summary>Create a Beaufort cipher with a repeating keyword.</summary>
    public BeaufortCipher(string key)
    {
        this.Key = Letters.Normalise(key);
        if (this.Key.Length == 0)
        {
            throw new ArgumentException("A Beaufort key needs at least one letter.", nameof(key));
        }

        this.key = Letters.ToNumbers(this.Key);
    }

    /// <summary>The repeating keyword.</summary>
    public string Key { get; }

    /// <inheritdoc/>
    public string Name => "Beaufort";

    /// <inheritdoc/>
    public string KeyDescription => this.Key;

    /// <inheritdoc/>
    public string Encrypt(string plaintext) => Letters.MapLetters(plaintext, (p, i) => this.key[i % this.key.Length] - p);

    /// <inheritdoc/>
    public string Decrypt(string ciphertext) => this.Encrypt(ciphertext);
}

/// <summary>
/// The autokey cipher: a short primer starts the key, and then <i>the message itself</i> is the
/// key. With primer QUEEN, "ATTACKATDAWN" is enciphered with key QUEENATTACKA. Because the key never
/// repeats, Kasiski and the index-of-coincidence trick find no period.
/// </summary>
/// <remarks>
/// The weakness is that the key is English. Guess the primer and the first plaintext letters fall
/// out, which give the next key letters, which give the next plaintext… An attacker only searches
/// the primer — 26^L for an L-letter primer — and can find it a letter at a time by scoring how
/// English each column of the decryption looks. See <see cref="Attacks.AutokeyAttack"/>.
/// </remarks>
public sealed class AutokeyCipher : ICipher
{
    private readonly int[] primer;

    /// <summary>Create an autokey cipher with a primer keyword.</summary>
    public AutokeyCipher(string primer)
    {
        this.Primer = Letters.Normalise(primer);
        if (this.Primer.Length == 0)
        {
            throw new ArgumentException("An autokey primer needs at least one letter.", nameof(primer));
        }

        this.primer = Letters.ToNumbers(this.Primer);
    }

    /// <summary>The primer that starts the key.</summary>
    public string Primer { get; }

    /// <inheritdoc/>
    public string Name => "Autokey";

    /// <inheritdoc/>
    public string KeyDescription => this.Primer;

    /// <inheritdoc/>
    public string Encrypt(string plaintext)
    {
        int[] p = Letters.ToNumbers(plaintext);
        return Letters.MapLetters(plaintext, (x, i) => x + (i < this.primer.Length ? this.primer[i] : p[i - this.primer.Length]));
    }

    /// <inheritdoc/>
    public string Decrypt(string ciphertext)
    {
        var plain = new List<int>();
        return Letters.MapLetters(ciphertext, (c, i) =>
        {
            int k = i < this.primer.Length ? this.primer[i] : plain[i - this.primer.Length];
            int p = Letters.Mod(c - k, 26);
            plain.Add(p);
            return p;
        });
    }
}

/// <summary>
/// The one-time pad: a Vigenère cipher whose key is truly random letters, as long as the message,
/// and never used again. It is the one cipher that is <i>provably</i> unbreakable (Claude Shannon,
/// 1949): every plaintext of the right length is equally possible, so the ciphertext carries no
/// information at all.
/// </summary>
/// <remarks>
/// <para>
/// All three conditions matter. Re-use a pad and two messages leak into each other (the Soviet
/// VENONA traffic, broken from the 1940s onward because pads were duplicated under wartime pressure).
/// Use a key with patterns and it can be attacked like a running-key cipher. See
/// <see cref="Attacks.TwoTimePadAttack"/>.
/// </para>
/// </remarks>
public sealed class OneTimePad : ICipher
{
    private readonly int[] pad;

    /// <summary>Create a one-time pad cipher from its pad of letters.</summary>
    public OneTimePad(string pad)
    {
        this.Pad = Letters.Normalise(pad);
        this.pad = Letters.ToNumbers(this.Pad);
    }

    /// <summary>The pad (key) letters.</summary>
    public string Pad { get; }

    /// <inheritdoc/>
    public string Name => "One-time pad";

    /// <inheritdoc/>
    public string KeyDescription => Letters.Group(this.Pad);

    /// <summary>A fresh pad of random letters (from a cryptographically strong generator).</summary>
    public static OneTimePad Generate(int length)
    {
        var sb = new StringBuilder(length);
        for (int i = 0; i < length; i++)
        {
            sb.Append(Letters.ToLetter(System.Security.Cryptography.RandomNumberGenerator.GetInt32(26)));
        }

        return new OneTimePad(sb.ToString());
    }

    /// <summary>
    /// For a message of <paramref name="length"/> letters, 26^length pads — and, unlike every other
    /// cipher here, no way to tell the right one from the wrong ones.
    /// </summary>
    public static KeySpace KeySpaceForLength(int length) => KeySpace.Power(26, length, $"26^{length} pads");

    /// <inheritdoc/>
    public string Encrypt(string plaintext) => Letters.MapLetters(plaintext, (p, i) => p + this.PadAt(i));

    /// <inheritdoc/>
    public string Decrypt(string ciphertext) => Letters.MapLetters(ciphertext, (c, i) => c - this.PadAt(i));

    private int PadAt(int i) => i < this.pad.Length
        ? this.pad[i]
        : throw new InvalidOperationException("The pad is shorter than the message — a pad must never be stretched or reused.");
}
