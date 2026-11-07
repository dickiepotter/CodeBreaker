using System.Numerics;
using RP.CodeBreaker.Ciphers;

namespace RP.CodeBreaker.Tests;

[TestClass]
public class DifficultyTests
{
    [TestMethod]
    public void KeySpacesInBits()
    {
        Assert.AreEqual(4.70, CaesarCipher.KeySpace.Bits, 0.01);
        Assert.AreEqual(88.38, SubstitutionCipher.KeySpace.Bits, 0.01);
        Assert.AreEqual(KeySpace.Log2Factorial(26), SubstitutionCipher.KeySpace.Bits, 1e-9);
        Assert.AreEqual(BigInteger.Parse("403291461126605635584000000"), SubstitutionCipher.KeySpace.Size);
    }

    [TestMethod]
    public void EnigmaKeySpace()
    {
        Assert.AreEqual(BigInteger.Parse("150738274937250"), EnigmaMachine.PlugboardSettings(10));
        Assert.AreEqual(76.5, EnigmaMachine.KeySpaceFor(5, 10).Bits, 0.1);
    }

    [TestMethod]
    public void InvertibleHillMatricesAreCountedCorrectly()
    {
        int count = 0;
        for (int a = 0; a < 26; a++)
        {
            for (int b = 0; b < 26; b++)
            {
                for (int c = 0; c < 26; c++)
                {
                    for (int d = 0; d < 26; d++)
                    {
                        int det = ((a * d) - (b * c)) % 26;
                        if (RP.CodeBreaker.NumberTheory.Gcd(det, 26) == 1)
                        {
                            count++;
                        }
                    }
                }
            }
        }

        Assert.AreEqual(HillCipher.KeySpace2x2.Size, count);
    }

    [TestMethod]
    public void WorkEstimatesAndVerdicts()
    {
        Assert.AreEqual(Feasibility.Instant, CaesarCipher.KeySpace.ExhaustiveSearch(Attacker.Laptop).Feasibility);
        Assert.AreEqual(Feasibility.Infeasible, SubstitutionCipher.KeySpace.ExhaustiveSearch(Attacker.Laptop).Feasibility);
        // Deep Crack: the whole DES key space in about nine days (it found the 1998 key in 56 hours).
        Assert.AreEqual(Feasibility.Months, KeySpace.FromBits(56).ExhaustiveSearch(Attacker.DeepCrack).Feasibility);
        Assert.AreEqual(9.3, KeySpace.FromBits(56).ExhaustiveSearch(Attacker.DeepCrack).Seconds / 86400, 0.1);
    }

    [TestMethod]
    public void DifficultyFunnelRecordsEachReduction()
    {
        var d = new Difficulty(VigenereCipher.KeySpaceUpTo(12))
            .Then("Key length is 5", BigInteger.Pow(26, 5), "IoC")
            .Then("Columns independent", 130, "sum not product");
        Assert.AreEqual(130, d.Remaining);
        Assert.AreEqual(2, d.Steps.Count);
        Assert.AreEqual(56.5 - 7.0, d.BitsSaved, 0.1); // 26^12 is about 2^56; 130 is about 2^7
    }

    [TestMethod]
    public void CatalogueHasTheFewMinutesLineInTheRightPlace()
    {
        var easy = CipherCatalogue.BreakableInFewMinutes(Attacker.Laptop).Select(p => p.Name).ToList();
        CollectionAssert.Contains(easy, "Caesar");
        CollectionAssert.Contains(easy, "Simple substitution");
        CollectionAssert.Contains(easy, "Enigma with a crib (bombe)");
        CollectionAssert.DoesNotContain(easy, "AES-128");
        CollectionAssert.DoesNotContain(easy, "One-time pad");
        CollectionAssert.DoesNotContain(easy, "Enigma without a crib");
    }

    [TestMethod]
    public void UnicityDistance()
    {
        Assert.AreEqual(1.5, CaesarCipher.KeySpace.UnicityDistance(), 0.1);
        Assert.AreEqual(27.6, SubstitutionCipher.KeySpace.UnicityDistance(), 0.1);
        Assert.AreEqual(23.9, EnigmaMachine.KeySpaceFor().UnicityDistance(), 0.1);
    }

    [TestMethod]
    public void NumberTheoryBasics()
    {
        Assert.AreEqual(9, RP.CodeBreaker.NumberTheory.ModInverse(3, 26));
        Assert.IsNull(RP.CodeBreaker.NumberTheory.ModInverse(13, 26));
        Assert.IsTrue(RP.CodeBreaker.NumberTheory.IsPrime(BigInteger.Parse("2305843009213693951")));
        Assert.IsFalse(RP.CodeBreaker.NumberTheory.IsPrime(BigInteger.Parse("2305843009213693953")));
        Assert.AreEqual(new BigInteger(1_000_000), RP.CodeBreaker.NumberTheory.IntegerSqrt(BigInteger.Pow(10, 12) + 5));
    }
}
