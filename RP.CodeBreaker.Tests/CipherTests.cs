using RP.CodeBreaker.Ciphers;
using RP.CodeBreaker.Codes;
using RP.CodeBreaker.Examples;

namespace RP.CodeBreaker.Tests;

[TestClass]
public class CipherTests
{
    private const string Sample = "Attack at dawn! The enemy is weakest just before first light, so we move at five.";

    [TestMethod]
    public void EveryRegisteredCipherRoundTrips()
    {
        foreach (CipherDefinition definition in CipherRegistry.All)
        {
            string key = definition.Name == "One-time pad" ? OneTimePad.Generate(200).Pad : definition.ExampleKey;
            ICipher cipher = definition.Create(key);
            string encrypted = cipher.Encrypt(Sample);
            string decrypted = cipher.Decrypt(encrypted);
            string expected = Letters.Normalise(Sample);
            string got = Letters.Normalise(decrypted);
            if (definition.Name is "Playfair" or "Polybius square" or "Bifid")
            {
                expected = expected.Replace('J', 'I');
            }

            if (definition.Name == "Playfair")
            {
                got = got.Replace("X", string.Empty);
                expected = expected.Replace("X", string.Empty);
            }

            Assert.IsTrue(got.StartsWith(expected, StringComparison.Ordinal), $"{definition.Name}: {got} ≠ {expected}");
        }
    }

    [TestMethod]
    public void SubstitutionFamilyKeepsLayout()
    {
        Assert.AreEqual("Dwwdfn dw gdzq!", new CaesarCipher(3).Encrypt("Attack at dawn!"));
        Assert.AreEqual("Nggnpx", CaesarCipher.Rot13.Encrypt("Attack"));
        Assert.AreEqual("ZGGZXP", new AtbashCipher().Encrypt("ATTACK"));
    }

    [TestMethod]
    public void VigenereClassicExample() =>
        Assert.AreEqual("LXFOPVEFRNHR", new VigenereCipher("LEMON").Encrypt("ATTACKATDAWN"));

    [TestMethod]
    public void KryptosK1DecryptsToTheKnownPlaintext()
    {
        var k1 = new VigenereCipher("PALIMPSEST", "KRYPTOS");
        Assert.AreEqual(
            "BETWEENSUBTLESHADINGANDTHEABSENCEOFLIGHTLIESTHENUANCEOFIQLUSION",
            k1.Decrypt(FamousCases.KryptosK1.Ciphertext));
    }

    [TestMethod]
    public void AffineRejectsMultipliersWithoutAnInverse()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new AffineCipher(13, 1));
        Assert.AreEqual(12, AffineCipher.ValidMultipliers.Count);
    }

    [TestMethod]
    public void RailFenceTextbookExample() =>
        Assert.AreEqual("WECRLTEERDSOEEFEAOCAIVDEN", new RailFenceCipher(3).Encrypt("WE ARE DISCOVERED FLEE AT ONCE"));

    [TestMethod]
    public void ColumnarOrderFromKeyword() =>
        CollectionAssert.AreEqual(new[] { 4, 2, 1, 3, 5, 0 }, ColumnarTranspositionCipher.OrderFromKeyword("ZEBRAS"));

    [TestMethod]
    public void PlayfairWikipediaExample()
    {
        // The standard example: key PLAYFAIREXAMPLE, "Hide the gold in the tree stump".
        var p = new PlayfairCipher("PLAYFAIR EXAMPLE");
        Assert.AreEqual("BMODZBXDNABEKUDMUIXMMOUVIF", p.Encrypt("Hide the gold in the tree stump"));
    }

    [TestMethod]
    public void HillTextbookExample()
    {
        var h = new HillCipher(new[,] { { 3, 3 }, { 2, 5 } });
        Assert.AreEqual("HIAT", h.Encrypt("HELP"));
        Assert.AreEqual("HELP", h.Decrypt("HIAT"));
    }

    [TestMethod]
    public void EnigmaStandardTestVector()
    {
        var m = new EnigmaMachine(EnigmaSettings.Parse("I II III"));
        Assert.AreEqual("BDZGO", m.Encrypt("AAAAA"));
    }

    [TestMethod]
    public void EnigmaDoubleStep()
    {
        var m = new EnigmaMachine(EnigmaSettings.Parse("I II III", positions: "ADU"));
        CollectionAssert.AreEqual(new[] { "ADV", "AEW", "BFX" }, m.PositionsAfter(3).ToArray());
    }

    [TestMethod]
    public void EnigmaNeverEncryptsALetterToItselfAndIsReciprocal()
    {
        var settings = EnigmaSettings.Parse("II IV V", "B", "BUL", "WXC", "AV BS CG DL FU HZ IN KM OW RX");
        string plain = new string('E', 200);
        string cipher = new EnigmaMachine(settings).Encrypt(plain);
        Assert.IsFalse(cipher.Contains('E'));
        Assert.AreEqual(plain, new EnigmaMachine(settings).Decrypt(cipher));
    }

    [TestMethod]
    public void EveryCodeRoundTrips()
    {
        foreach (ICode code in CodeCatalogue.All)
        {
            string text = code is Codebook ? "ATTACK AT DAWN STOP" : "SOS SEND HELP";
            string decoded = code.Decode(code.Encode(text));
            Assert.AreEqual(Letters.Normalise(text), Letters.Normalise(decoded), code.Name);
        }
    }

    [TestMethod]
    public void MorseAndFriends()
    {
        Assert.AreEqual("... --- ...", MorseCode.Encode("SOS"));
        Assert.AreEqual("AAAAA AAAAB", new BaconianCode().Encode("AB"));
        Assert.AreEqual("8-5-12-12-15", new A1Z26Code().Encode("hello"));
        Assert.AreEqual("Hi there 42", new Base64Code().Decode(new Base64Code().Encode("Hi there 42")));
        Assert.AreEqual("HELLO 42", new Ita2Code().Decode(new Ita2Code().Encode("HELLO 42")));
    }

    [TestMethod]
    public void CodebookSpellsOutUnknownWords()
    {
        string coded = Codebook.Example.Encode("Attack Paris at dawn");
        Assert.AreEqual("ATTACK PARIS AT DAWN", Codebook.Example.Decode(coded));
    }
}
