namespace RP.CodeBreaker;

/// <summary>
/// A cipher with its key already chosen: something that turns plaintext into ciphertext and back.
/// </summary>
/// <remarks>
/// Every cipher in this library is an immutable object built from its key —
/// <c>new CaesarCipher(3)</c>, <c>new VigenereCipher("LEMON")</c> — so "the cipher" and "the cipher
/// with this key" are the same thing, exactly as they were for the clerk with the codebook.
/// </remarks>
public interface ICipher
{
    /// <summary>The cipher's name ("Caesar", "Vigenère"…).</summary>
    string Name { get; }

    /// <summary>The key, written the way a person would note it down.</summary>
    string KeyDescription { get; }

    /// <summary>Turn plaintext into ciphertext.</summary>
    string Encrypt(string plaintext);

    /// <summary>Turn ciphertext back into plaintext.</summary>
    string Decrypt(string ciphertext);
}
