using RP.CodeBreaker.Attacks;
using RP.CodeBreaker.Ciphers;

namespace RP.CodeBreaker.Tests;

/// <summary>
/// Checking answers, and the crib handling the checks exposed: an attack's best key is not always the
/// right one, so every answer is judged against the cribs, the language and the message's length.
/// </summary>
[TestClass]
public class BreakCheckTests
{
    // The Convert page's default message: 49 letters, short enough to fool several attacks.
    private const string Short = "Attack at dawn. The enemy is weakest just before first light.";

    private const string Long =
        "Meet me at the old bridge after midnight. Bring the documents and tell nobody where you are going. " +
        "The courier will wait for one hour and then leave without you.";

    private static BreakResult Answer(string plaintext, string cipher = "Caesar") => new()
    {
        CipherName = cipher,
        Key = "?",
        Plaintext = plaintext,
        EnglishLikeness = LanguageModel.English.QuadgramScorePerLetter(plaintext),
        Difficulty = new Difficulty(CaesarCipher.KeySpace),
    };

    [TestMethod]
    public void AMissingCribMeansTheAnswerIsWrong()
    {
        BreakCheck check = BreakCheck.Check(Answer(Long), Long, new AttackOptions { Cribs = ["SUBMARINE"] });
        Assert.AreEqual(Confidence.Failed, check.Confidence);
        CollectionAssert.AreEqual(new[] { "SUBMARINE" }, check.CribsMissing.ToArray());
    }

    [TestMethod]
    public void GibberishFails()
    {
        string noise = "QXZJVKWPQZXJKVQPZWXJQKVZPWQJXKZVQPWJZXKQV";
        Assert.AreEqual(Confidence.Failed, BreakCheck.Check(Answer(noise), noise, new AttackOptions()).Confidence);
    }

    [TestMethod]
    public void ClearTextWithItsCribIsSolved()
    {
        BreakCheck check = BreakCheck.Check(Answer(Long), Long, new AttackOptions { Cribs = ["BRIDGE"] });
        Assert.AreEqual(Confidence.Solved, check.Confidence);
    }

    [TestMethod]
    public void TooShortForTheAttackIsDoubtfulEvenWhenItReads()
    {
        // 49 letters is plenty for a Caesar shift but too few to trust a substitution hill climb.
        Assert.AreEqual(Confidence.Solved, BreakCheck.Check(Answer(Short), Short, new AttackOptions()).Confidence);
        Assert.AreEqual(Confidence.Doubtful, BreakCheck.Check(Answer(Short, "Simple substitution"), Short, new AttackOptions()).Confidence);
    }

    [TestMethod]
    public void PolybiusDigitsCountAsTheLettersTheyStandFor()
    {
        string digits = new PolybiusCipher(string.Empty).Encrypt(Long);
        BreakCheck check = BreakCheck.Check(Answer(Long, "Polybius square"), digits, new AttackOptions());
        Assert.AreEqual(Letters.Normalise(Long).Length, check.Letters);
    }

    [TestMethod]
    public void AutoPrefersTheAnswerThatHoldsTheCrib()
    {
        // On 49 letters the statistics call a rail fence a substitution; the crib settles it.
        string c = new RailFenceCipher(3).Encrypt(Short);
        var options = new AttackOptions { Cribs = ["DAWN"], Restarts = 4 };
        var (_, best, _) = AutoBreaker.BreakAsync(c, options).AsTask().GetAwaiter().GetResult();
        Assert.AreEqual(Letters.Normalise(Short), Letters.Normalise(best!.Plaintext));
    }

    [TestMethod]
    public void AutoDoesNotSettleForAnOverfittedSubstitution()
    {
        string c = new AffineCipher(5, 8).Encrypt(Short);
        var (_, best, _) = AutoBreaker.BreakAsync(c, new AttackOptions { Cribs = ["DAWN"], Restarts = 4 }).AsTask().GetAwaiter().GetResult();
        Assert.AreEqual("Affine", best!.CipherName);
        Assert.AreEqual(Letters.Normalise(Short), Letters.Normalise(best.Plaintext));
    }

    [TestMethod]
    public void HillCribSolvesRowByRowWhenItsPairsCannotBeInverted()
    {
        // DAWN's pairs (D,A) and (W,N) make a matrix with determinant 13: no inverse mod 26, so the
        // algebra fails; each row of the decryption matrix can still be pinned down by the crib.
        string c = new HillCipher(new[,] { { 3, 3 }, { 2, 5 } }).Encrypt(Short);
        BreakResult r = HillAttack.Break(c, new AttackOptions { Cribs = ["DAWN"] });
        StringAssert.StartsWith(Letters.Normalise(r.Plaintext), "ATTACKATDAWNTHEENEMY");
    }

    [TestMethod]
    public void SubstitutionTriesEveryPlaceACribCouldFit()
    {
        // WAIT has no repeated letters, so it fits almost anywhere, and its real place is near the end:
        // trying only the first few placements forced wrong letters into the key.
        string c = SubstitutionCipher.FromKeyword("ZEBRAS").Encrypt(Long);
        BreakResult r = SubstitutionAttack.Break(c, new AttackOptions { Cribs = ["WAIT"], Restarts = 4 });
        Assert.AreEqual(Letters.Normalise(Long), Letters.Normalise(r.Plaintext));
    }

    [TestMethod]
    public void EveryInvertibleMatrixIsRecognisedAndInverted()
    {
        // Gauss–Jordan elimination mod 26 used to give up on matrices like [[13, 7], [14, 19]] (no entry
        // of the first column can be divided by), wrongly rejecting 7,488 of the 157,248 valid keys.
        int valid = 0;
        for (int a = 0; a < 26; a++)
        {
            for (int b = 0; b < 26; b++)
            {
                for (int c = 0; c < 26; c++)
                {
                    for (int d = 0; d < 26; d++)
                    {
                        var m = new[,] { { a, b }, { c, d } };
                        int[,]? inv = NumberTheory.MatrixInverse(m, 26);
                        if (inv is null)
                        {
                            continue;
                        }

                        valid++;
                        Assert.AreEqual(1, ((m[0, 0] * inv[0, 0]) + (m[0, 1] * inv[1, 0])) % 26);
                        Assert.AreEqual(0, ((m[0, 0] * inv[0, 1]) + (m[0, 1] * inv[1, 1])) % 26);
                        Assert.AreEqual(0, ((m[1, 0] * inv[0, 0]) + (m[1, 1] * inv[1, 0])) % 26);
                        Assert.AreEqual(1, ((m[1, 0] * inv[0, 1]) + (m[1, 1] * inv[1, 1])) % 26);
                    }
                }
            }
        }

        Assert.AreEqual(HillCipher.KeySpace2x2.Size, valid);
        Assert.IsTrue(HillCipher.IsValidKey(new[,] { { 13, 7 }, { 14, 19 } }));
        _ = new HillCipher(new[,] { { 13, 7 }, { 14, 19 } });
    }

    [TestMethod]
    public void AutoWithACribThatIsNotThereFailsHonestly()
    {
        // Every quick attack runs when no answer holds the crib; none may throw, and the verdict is "wrong".
        string c = new VigenereCipher("LEMON").Encrypt(Long);
        var options = new AttackOptions { Cribs = ["TANKS"], Restarts = 2 };
        var (_, best, _) = AutoBreaker.BreakAsync(c, options).AsTask().GetAwaiter().GetResult();
        Assert.AreEqual(Confidence.Failed, BreakCheck.Check(best!, c, options).Confidence);
    }

    [TestMethod]
    public void EveryExampleKeyIsWithinTheDefaultSearch()
    {
        // The Convert page's example keys should be breakable with the Break page's defaults.
        int max = new AttackOptions().MaxKeyLength;
        foreach (string name in new[] { "Vigenère", "Beaufort", "Autokey" })
        {
            Assert.IsTrue(Letters.Normalise(CipherRegistry.Named(name).ExampleKey).Length <= max, name);
        }
    }
}
