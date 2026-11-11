using System.Numerics;
using RP.CodeBreaker.Attacks;
using RP.CodeBreaker.Ciphers;
using RP.CodeBreaker.Examples;
using RP.CodeBreaker.Modern;

namespace RP.CodeBreaker.Tests;

[TestClass]
public class AttackTests
{
    private const string Prose =
        "It was the best of times and the worst of times, and in the long winter that followed the town learned to keep its secrets. " +
        "Letters went out sealed and came back opened, and the clerks at the post office began to read every word with care. " +
        "So the merchants agreed among themselves to write in a cipher, believing that no one would trouble to break it, " +
        "and for a season they were right, until a patient schoolmaster counted the letters one evening and saw the shape of the language underneath.";

    private static string Plain => Letters.Normalise(Prose);

    [TestMethod]
    public void Caesar()
    {
        BreakResult r = CaesarAttack.Break(FamousCases.Caesar.Ciphertext);
        Assert.AreEqual("shift 3 (A->D)", r.Key);
        Assert.IsTrue(r.LooksLikeEnglish);
        Assert.AreEqual(3, CaesarAttack.ShiftByFrequency(FamousCases.Caesar.Ciphertext));
    }

    [TestMethod]
    public void CaesarWithCribLeavesOneKey()
    {
        string c = new CaesarCipher(11).Encrypt("Meet me by the old bridge");
        BreakResult r = CaesarAttack.Break(c, new AttackOptions { Cribs = ["BRIDGE"] });
        Assert.AreEqual(1, (int)r.Difficulty.Steps[0].After);
        Assert.AreEqual("MEETMEBYTHEOLDBRIDGE", Letters.Normalise(r.Plaintext));
    }

    [TestMethod]
    public void Affine()
    {
        BreakResult r = AffineAttack.Break(new AffineCipher(7, 19).Encrypt(Prose));
        Assert.AreEqual("a = 7, b = 19", r.Key);
    }

    [TestMethod]
    public void Substitution()
    {
        BreakResult r = SubstitutionAttack.Break(FamousCases.Babington.Ciphertext);
        Assert.AreEqual(FamousCases.Babington.Ciphertext.Length, r.Plaintext.Length);
        StringAssert.Contains(Letters.Normalise(r.Plaintext), "BURNTHISLETTER");
    }

    [TestMethod]
    public void SubstitutionCribFixesLetters()
    {
        var options = new AttackOptions { Cribs = FamousCases.Babington.Cribs, Restarts = 2 };
        BreakResult r = SubstitutionAttack.Break(FamousCases.Babington.Ciphertext, options);
        StringAssert.Contains(Letters.Normalise(r.Plaintext), "THEQUEENISDEAD");
        Assert.IsTrue(r.Difficulty.Steps[0].After < SubstitutionCipher.KeySpace.Size / 1_000_000);
    }

    [TestMethod]
    public void Vigenere()
    {
        BreakResult r = VigenereAttack.Break(FamousCases.Indechiffrable.Ciphertext);
        Assert.AreEqual("BABBAGE", r.Key);
        Assert.AreEqual(26 * 7, (int)r.Difficulty.Remaining);
    }

    [TestMethod]
    public void VigenereShortTextWithCrib()
    {
        string c = new VigenereCipher("SECRET").Encrypt("The meeting is moved to the boathouse at midnight");
        BreakResult r = VigenereAttack.Break(c, new AttackOptions { Cribs = ["BOATHOUSE"] });
        Assert.AreEqual("SECRET", r.Key);
    }

    [TestMethod]
    public void Beaufort()
    {
        BreakResult r = VigenereAttack.Break(new BeaufortCipher("FORTIFY").Encrypt(Prose), variant: PolyalphabeticVariant.Beaufort);
        Assert.AreEqual("FORTIFY", r.Key);
    }

    [TestMethod]
    public void Autokey()
    {
        BreakResult r = AutokeyAttack.Break(new AutokeyCipher("QUEENLY").Encrypt(Prose));
        Assert.AreEqual("QUEENLY", r.Key);
    }

    [TestMethod]
    public void RailFence()
    {
        BreakResult r = RailFenceAttack.Break(new RailFenceCipher(5).Encrypt(Prose));
        Assert.AreEqual("5 rails", r.Key);
    }

    [TestMethod]
    public void ColumnarSmall()
    {
        BreakResult r = ColumnarAttack.Break(new ColumnarTranspositionCipher("GERMAN").Encrypt(Prose), new AttackOptions { MaxKeyLength = 8 });
        Assert.AreEqual(Plain, r.Plaintext);
    }

    [TestMethod]
    public void ColumnarLargeByHillClimb()
    {
        BreakResult r = ColumnarAttack.Break(new ColumnarTranspositionCipher("SPECIALOPERATION").Encrypt(Prose), new AttackOptions { MaxKeyLength = 16, Restarts = 6 });
        Assert.AreEqual(Plain, r.Plaintext);
    }

    [TestMethod]
    public void Playfair()
    {
        BreakResult r = PlayfairAttack.Break(new PlayfairCipher("CHARLES WHEATSTONE").Encrypt(Prose), new AttackOptions { Restarts = 6, Iterations = 50_000 });
        Assert.IsTrue(r.LooksLikeEnglish, r.Plaintext);
    }

    [TestMethod]
    public void HillCiphertextOnly()
    {
        var h = new HillCipher(new[,] { { 5, 17 }, { 4, 15 } });
        BreakResult r = HillAttack.Break(h.Encrypt(Prose));
        Assert.AreEqual(h.KeyDescription, r.Key);
    }

    [TestMethod]
    public void HillKnownPlaintext()
    {
        var h = new HillCipher(new[,] { { 3, 3 }, { 2, 5 } });
        HillCipher? found = HillAttack.FromKnownPlaintext("SCHOOLMASTER", h.Encrypt("SCHOOLMASTER"));
        Assert.IsNotNull(found);
        Assert.AreEqual(h.KeyDescription, found.KeyDescription);
        BreakResult r = HillAttack.Break(h.Encrypt(Prose), new AttackOptions { Cribs = ["SCHOOLMASTER"] });
        Assert.AreEqual(h.KeyDescription, r.Key);
    }

    [TestMethod]
    public void Polybius()
    {
        BreakResult r = PolybiusAttack.Break(new PolybiusCipher("SECRET").Encrypt(Prose));
        Assert.IsTrue(r.LooksLikeEnglish, r.Plaintext);
    }

    [TestMethod]
    public void Xor()
    {
        BreakResult r = XorAttack.BreakHex(FamousCases.HomeMadeXor.Ciphertext);
        Assert.AreEqual("\"SECRET\"", r.Key);
        StringAssert.StartsWith(r.Plaintext, "The database password");
    }

    [TestMethod]
    public void TwoTimePad()
    {
        string[] parts = FamousCases.Venona.Ciphertext.Split('|');
        var drags = TwoTimePadAttack.DragCrib(parts[1], parts[0], "THECOURIER");
        Assert.IsTrue(drags.Take(3).Any(d => d.Position == 4), string.Join(", ", drags.Take(3)));
    }

    [TestMethod]
    public void Identifier()
    {
        Assert.AreEqual(CipherFamily.ShiftOrAffine, CipherIdentifier.Identify(FamousCases.Caesar.Ciphertext)[0].Family);
        Assert.AreEqual(CipherFamily.Substitution, CipherIdentifier.Identify(FamousCases.Babington.Ciphertext)[0].Family);
        Assert.AreEqual(CipherFamily.Transposition, CipherIdentifier.Identify(new RailFenceCipher(4).Encrypt(Prose))[0].Family);
        Assert.AreEqual(CipherFamily.PeriodicPolyalphabetic, CipherIdentifier.Identify(FamousCases.Indechiffrable.Ciphertext)[0].Family);
        Assert.AreEqual(CipherFamily.Polybius, CipherIdentifier.Identify(new PolybiusCipher("X").Encrypt(Prose))[0].Family);
        Assert.AreEqual(CipherFamily.Adfgvx, CipherIdentifier.Identify(FamousCases.Adfgvx.Ciphertext)[0].Family);
        Assert.AreEqual(CipherFamily.Random, CipherIdentifier.Identify(OneTimePad.Generate(1000).Encrypt(Prose + Prose))[0].Family);
    }

    [TestMethod]
    public async Task AutoBreakerPicksTheRightAttack()
    {
        var (_, best, _) = await AutoBreaker.BreakAsync(new VigenereCipher("WINTER").Encrypt(Prose));
        Assert.IsNotNull(best);
        Assert.AreEqual("WINTER", best.Key);
    }

    [TestMethod]
    public void BombeRecoversTheWeatherReport()
    {
        FamousCase c = FamousCases.WeatherReport;
        Assert.IsTrue(EnigmaBombe.PossibleCribPositions(c.Ciphertext, "WETTERBERICHT").Contains(0));
        BombeResult r = EnigmaBombe.Run(c.Ciphertext, "WETTERBERICHT", 0, [EnigmaRotor.I, EnigmaRotor.II, EnigmaRotor.IV], options: new AttackOptions { Language = LanguageModel.German });
        Assert.AreEqual(1, r.Stops.Count, "a three-loop menu should stop only at the right place");
        Assert.AreEqual("AAF", r.Settings!.RingSettings);
        Assert.AreEqual("FEA", r.Settings.Positions);
        Assert.IsTrue(r.Solved, string.Join(Environment.NewLine, r.Log));
        StringAssert.StartsWith(r.Plaintext, "WETTERBERICHTFUERDIENORDSEE");
    }

    [TestMethod]
    public void Factoring()
    {
        var rsa = ToyRsa.Generate(64, seed: 7);
        FactorResult f = Modern.Factoring.Factor(rsa.N);
        Assert.IsTrue(f.Success);
        Assert.IsTrue(f.Factor == rsa.P || f.Factor == rsa.Q);
        BigInteger d = ToyRsa.PrivateExponentFromFactors(f.Factor!.Value, f.Cofactor!.Value, rsa.E);
        Assert.AreEqual(new BigInteger(42), BigInteger.ModPow(rsa.Encrypt(42), d, rsa.N));

        // Close primes fall to Fermat at once.
        var close = new ToyRsa(1_000_003, 1_000_033);
        Assert.AreEqual(1, Modern.Factoring.Fermat(close.N).Steps);
        Assert.AreEqual(117, Modern.Factoring.EstimateNumberFieldSieveOperations(2048), 3);
    }

    [TestMethod]
    public void PasswordCracking()
    {
        CrackResult pin = Modern.PasswordCracking.CrackPin(Modern.PasswordCracking.Sha256Hex("4071"), 4);
        Assert.AreEqual("4071", pin.Secret);
        CrackResult word = Modern.PasswordCracking.CrackWord(Modern.PasswordCracking.Sha256Hex("Letter7"), maxWords: 500);
        Assert.AreEqual("Letter7", word.Secret);
    }

    [TestMethod]
    public void LanguageModelScoresEnglishAboveGibberish()
    {
        LanguageModel en = LanguageModel.English;
        double english = en.QuadgramScorePerLetter(Prose);
        double shuffled = en.QuadgramScorePerLetter(new RailFenceCipher(7).Encrypt(Prose));
        Assert.IsTrue(english > -5.0, $"English {english}");
        Assert.IsTrue(shuffled < -5.5, $"Shuffled {shuffled}");
        Assert.AreEqual("ATTACK AT DAWN", en.Segment("ATTACKATDAWN"));
        Assert.AreEqual(0.066, en.ExpectedIndexOfCoincidence, 0.003);
    }

    [TestMethod]
    public void HillCiphertextOnlyOnAnotherText()
    {
        var h = new HillCipher(new[,] { { 5, 17 }, { 4, 15 } });
        string plain = new VigenereCipher("BABBAGE").Decrypt(FamousCases.Indechiffrable.Ciphertext);
        Assert.AreEqual(h.KeyDescription, HillAttack.Break(h.Encrypt(plain)).Key);
    }

    [TestMethod]
    public void GermanModelPrefersGerman()
    {
        const string german = "DIEWETTERLAGEISTUNVERAENDERTXWINDAUSWESTEN";
        Assert.IsTrue(LanguageModel.German.QuadgramScorePerLetter(german) > LanguageModel.English.QuadgramScorePerLetter(german));
        Assert.AreEqual(0.074, LanguageModel.German.ExpectedIndexOfCoincidence, 0.004);
    }

    [TestMethod]
    public void Periodicity()
    {
        IReadOnlyList<PeriodCandidate> periods = RP.CodeBreaker.Periodicity.RankPeriods(FamousCases.Indechiffrable.Ciphertext);
        Assert.AreEqual(7, periods[0].Period);
        Assert.AreEqual(10, RP.CodeBreaker.Periodicity.RankPeriods(FamousCases.KryptosK1.Ciphertext, 12)[0].Period);
    }
}
