using System.Numerics;
using System.Text;

namespace RP.CodeBreaker.Ciphers;

/// <summary>
/// Repeating-key XOR on bytes: the computer-age Vigenère. Each byte of the message is combined with
/// the next byte of a short key using exclusive-or (XOR), the bitwise "one or the other but not
/// both", and the key repeats.
/// </summary>
/// <remarks>
/// <para>
/// XOR is its own inverse — (m ⊕ k) ⊕ k = m — so the same operation encrypts and decrypts. With a
/// truly random key as long as the message (never reused) this is a one-time pad and unbreakable;
/// with a short repeating key it falls exactly like Vigenère: find the key length (here by the
/// Hamming distance between blocks), then each key byte is a single-byte XOR that a frequency count
/// breaks. Home-made "encryption" in software has done this more often than anyone would like.
/// </para>
/// </remarks>
public sealed class XorCipher
{
    private readonly byte[] key;

    /// <summary>Create a repeating-key XOR cipher.</summary>
    public XorCipher(byte[] key)
    {
        if (key.Length == 0)
        {
            throw new ArgumentException("An XOR key needs at least one byte.", nameof(key));
        }

        this.key = (byte[])key.Clone();
    }

    /// <summary>Create a repeating-key XOR cipher from a text key (UTF-8).</summary>
    public XorCipher(string key)
        : this(Encoding.UTF8.GetBytes(key))
    {
    }

    /// <summary>A copy of the key.</summary>
    public byte[] Key => (byte[])this.key.Clone();

    /// <summary>256^length keys of a given length.</summary>
    public static KeySpace KeySpaceForLength(int length) => KeySpace.Power(256, length, $"256^{length} keys of {length} bytes");

    /// <summary>XOR the data with the repeating key (encrypts and decrypts).</summary>
    public byte[] Apply(byte[] data)
    {
        var result = new byte[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            result[i] = (byte)(data[i] ^ this.key[i % this.key.Length]);
        }

        return result;
    }

    /// <summary>Encrypt a text (UTF-8) and give the result as hexadecimal.</summary>
    public string EncryptToHex(string plaintext) => Convert.ToHexString(this.Apply(Encoding.UTF8.GetBytes(plaintext)));

    /// <summary>Decrypt hexadecimal ciphertext to text (UTF-8).</summary>
    public string DecryptFromHex(string hex) => Encoding.UTF8.GetString(this.Apply(Convert.FromHexString(CleanHex(hex))));

    /// <summary>The number of differing bits between two byte arrays of equal length.</summary>
    public static int HammingDistance(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        int d = 0;
        for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            d += BitOperations.PopCount((uint)(a[i] ^ b[i]));
        }

        return d;
    }

    internal static string CleanHex(string hex) => new(hex.Where(Uri.IsHexDigit).ToArray());
}
