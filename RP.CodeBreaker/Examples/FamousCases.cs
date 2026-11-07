using RP.CodeBreaker.Ciphers;
using RP.CodeBreaker.Codes;

namespace RP.CodeBreaker.Examples;

/// <summary>
/// A famous situation in code breaking, with a message to work on. Where the historical ciphertext is
/// public and short it is used as it stands (Kryptos K1); otherwise the message is a reconstruction —
/// new text in the spirit of the original, enciphered with this library — so it can be broken here
/// with the same method that broke the real thing.
/// </summary>
/// <param name="Title">The case.</param>
/// <param name="Year">When.</param>
/// <param name="Story">What happened, and why it matters.</param>
/// <param name="Cipher">The cipher used.</param>
/// <param name="Key">The key (the answer).</param>
/// <param name="Ciphertext">The message to break.</param>
/// <param name="Cribs">Words the context suggests (may be empty).</param>
/// <param name="HowToBreak">The method, in a sentence or two.</param>
/// <param name="Historical">True if <paramref name="Ciphertext"/> is the real historical text.</param>
public sealed record FamousCase(
    string Title,
    string Year,
    string Story,
    string Cipher,
    string Key,
    string Ciphertext,
    IReadOnlyList<string> Cribs,
    string HowToBreak,
    bool Historical = false);

/// <summary>The famous cases, oldest first.</summary>
public static class FamousCases
{
    /// <summary>Caesar's dispatch.</summary>
    public static FamousCase Caesar { get; } = new(
        "Caesar's dispatch",
        "c. 55 BC",
        "Suetonius records that Julius Caesar wrote to his friends with every letter shifted three places along the alphabet. Anyone who suspected a shift could try all 26 in an afternoon; its only security was that few people could read at all.",
        "Caesar",
        "3",
        new CaesarCipher(3).Encrypt("The legions will cross the river at first light. Hold the bridge until the cavalry arrives, and send word to Labienus that the Belgae are gathering in the forest to the north."),
        [],
        "Try all 26 shifts and keep the one that reads.");

    /// <summary>Mary, Queen of Scots and the Babington plot.</summary>
    public static FamousCase Babington { get; } = new(
        "Mary, Queen of Scots — the Babington plot",
        "1586",
        "Imprisoned Mary wrote to Anthony Babington in a nomenclator — a substitution alphabet with extra symbols for common words — smuggled in beer barrels. Walsingham's decipherer Thomas Phelippes broke it by letter frequencies, read her approval of the plot to kill Elizabeth, and even forged a postscript asking for the conspirators' names. Mary was executed in 1587: a cipher trusted beyond its strength is worse than none.",
        "Simple substitution",
        SubstitutionCipher.FromKeyword("WALSINGHAM").CipherAlphabet,
        SubstitutionCipher.FromKeyword("WALSINGHAM").Encrypt(
            "My good friend, I have received your letter and understand the design. When the six gentlemen have done their work and the queen is dead, let the forces from abroad be ready at the coast, and let me be taken from this place with all speed. Burn this letter once it is read, and tell no one more than he must know. Let the gentlemen be sure of their horses and their weapons, and send me word by the brewer when all is prepared, for I am closely watched and cannot write often."),
        ["QUEEN", "LETTER"],
        "Count the letters; the commonest are E, T, A, O. A hill climb on quadgrams finishes the job; placing QUEEN by its letter pattern fixes four letters at once.");

    /// <summary>Le chiffre indéchiffrable.</summary>
    public static FamousCase Indechiffrable { get; } = new(
        "Le chiffre indéchiffrable",
        "1553 – 1863",
        "For three centuries the Vigenère cipher was thought unbreakable because it hides letter frequencies. Charles Babbage broke it in the 1850s (but never published) and the Prussian officer Friedrich Kasiski published the method in 1863: repeated words give the key length away, and then it is only a handful of Caesar ciphers.",
        "Vigenère",
        "BABBAGE",
        new VigenereCipher("BABBAGE").Encrypt("It has long been said that this cipher cannot be broken, for it hides the frequencies of the letters that betray every simple substitution. But the key repeats, and when the same word meets the same part of the key it is written the same way twice. Measure the distances between such repeats and the length of the key appears among their factors. Then each column is nothing more than the cipher of Caesar."),
        [],
        "Find the period by Kasiski repeats or the index of coincidence, then solve each column as a Caesar shift.");

    /// <summary>The Playfair message that rescued John F. Kennedy.</summary>
    public static FamousCase Pt109 { get; } = new(
        "PT-109",
        "1943",
        "When John F. Kennedy's torpedo boat was sunk in the Solomon Islands, coastwatcher Arthur Evans sent the news by Playfair, keyed ROYAL NEW ZEALAND NAVY. Playfair was chosen for field work because it needs only a pencil and a remembered keyword; it was secure enough for news that would be stale in hours, not days. (This is the message re-enciphered by this library, which may split doubled letters slightly differently from the original.)",
        "Playfair",
        "ROYALNEWZEALANDNAVY",
        new PlayfairCipher("ROYALNEWZEALANDNAVY").Encrypt("PT BOAT ONE OWE NINE LOST IN ACTION IN BLACKETT STRAIT TWO MILES SW MERESU COVE X CREW OF TWELVE X REQUEST ANY INFORMATION"),
        ["BLACKETT"],
        "Simulated annealing over 5×5 grids, scoring the decryption on quadgrams. Short messages like this one are hard; longer ones fall in seconds.");

    /// <summary>Kryptos K1, the real ciphertext.</summary>
    public static FamousCase KryptosK1 { get; } = new(
        "Kryptos, panel 1",
        "1990",
        "Jim Sanborn's sculpture outside CIA headquarters carries four encrypted passages. The first, K1, is a keyed Vigenère (Quagga) on a KRYPTOS alphabet with key PALIMPSEST; it was solved by James Gillogly with a computer in 1999 (and, it turned out, by the NSA in 1992). The deliberate misspelling IQLUSION is Sanborn's. K4, the last panel, is still unsolved.",
        "Keyed Vigenère (Quagga)",
        "PALIMPSEST, KRYPTOS",
        "EMUFPHZLRFAXYUSDJKZLDKRNSHGNFIVJYQTQUXQBQVYUVLLTREVJYQTMKYRDMFD",
        [],
        "Find the period (10) by the index of coincidence; with a keyed alphabet, solve the columns jointly or guess the KRYPTOS alphabet from the sculpture itself.",
        Historical: true);

    /// <summary>ADFGVX and Painvin.</summary>
    public static FamousCase Adfgvx { get; } = new(
        "The radiogram of victory",
        "1918",
        "In spring 1918 the German army introduced ADFGVX for its great offensive. Georges Painvin broke a day's key by exploiting two messages with similar beginnings, revealing an order to hurry munitions to Remaugies — and so the target of the next attack. It took him days and much of his health; this library offers no automatic attack on it, which is itself the lesson.",
        "ADFGVX",
        "PRIVACY, GERMAN",
        new AdfgvxCipher("PRIVACY", "GERMAN").Encrypt("MUNITIONIERUNG BESCHLEUNIGEN PUNKT SOWEIT NICHT EINGESEHEN AUCH BEI TAG"),
        ["MUNITION"],
        "Needs several messages on the same key: matching their beginnings reveals the transposition, after which the square is a substitution.");

    /// <summary>An Enigma weather report and the bombe.</summary>
    public static FamousCase WeatherReport { get; } = new(
        "WETTERBERICHT — the bombe's favourite crib",
        "1940 – 1945",
        "German weather stations sent reports every morning in a fixed form beginning WETTERBERICHT (weather report). Bletchley Park knew the word would be there, and roughly where; that crib, fed into a Turing–Welchman bombe, recovered the day's rotor order and positions — and with them every other message on that key. Routine, not weakness of the machine, broke Enigma.",
        "Enigma",
        "II IV I | B | AAF | FEA | AT BS DE FM IR KN LZ OW PV XY",
        new EnigmaMachine(EnigmaSettings.Parse("II IV I", "B", "AAF", "FEA", "AT BS DE FM IR KN LZ OW PV XY"))
            .Encrypt("WETTERBERICHTFUERDIENORDSEEXWINDAUSWESTSUEDWESTSTAERKEFUENFXSICHTGUTXREGENSCHAUERAMNACHMITTAGX"),
        ["WETTERBERICHT"],
        "Place the crib where no letter matches itself, build a menu, and let the bombe eliminate rotor positions by deduction; check each stop and complete the plugboard.");

    /// <summary>VENONA and the two-time pad.</summary>
    public static FamousCase Venona { get; } = new(
        "VENONA",
        "1943 – 1980",
        "A one-time pad is perfect only if it is used once. Under wartime pressure in 1942 the Soviet pad factory printed some pages twice. American and British analysts spent decades matching messages that shared pad pages and reading them by crib dragging, exposing Klaus Fuchs, Donald Maclean and the atomic spy ring. Below are two messages enciphered with the same pad; try the crib THE.",
        "One-time pad (reused)",
        "pad reused",
        VenonaPair.Item1 + " | " + VenonaPair.Item2,
        ["THE"],
        "Subtract one ciphertext from the other: the pad cancels. Drag a likely word along one message and watch for English in the other.");

    /// <summary>The Zimmermann telegram, as a codebook.</summary>
    public static FamousCase Zimmermann { get; } = new(
        "The Zimmermann telegram",
        "1917",
        "Germany's foreign minister cabled his ambassador in Mexico proposing an alliance against the United States, in the numeric diplomatic code 13040. Room 40 in the Admiralty had partly rebuilt that codebook from captured material and context; their reading of it helped bring America into the First World War. A code has no key to search — the book is the secret, rebuilt one group at a time.",
        "Codebook",
        "the book",
        Codebook.Example.Encode("We intend to begin on the first of February unrestricted submarine warfare. We propose alliance to Mexico."),
        ["FEBRUARY", "MEXICO"],
        "Collect many messages and reconstruct groups from context: the group before every date is a month name, the one repeated at the start is the address.");

    /// <summary>The XOR "encryption" of a configuration file.</summary>
    public static FamousCase HomeMadeXor { get; } = new(
        "Home-made XOR",
        "Today",
        "Software that 'encrypts' with a short repeating XOR key is a modern classic. It is Vigenère on bytes, and falls the same way: the Hamming distance finds the key length, then each key byte is one of 256.",
        "Repeating-key XOR",
        "SECRET",
        new XorCipher("SECRET").EncryptToHex("The database password is kept in this file so that the service can start without asking anyone. Nobody outside the company will ever look at it, and anyway it is encrypted, so it should be perfectly safe to leave here."),
        [],
        "Score key lengths by normalised Hamming distance, then solve each byte column by character frequency.");

    /// <summary>Every case.</summary>
    public static IReadOnlyList<FamousCase> All { get; } = [Caesar, Babington, Indechiffrable, Adfgvx, Zimmermann, WeatherReport, Pt109, Venona, KryptosK1, HomeMadeXor];

    /// <summary>Two messages sent on the same pad (the VENONA exercise).</summary>
    public static (string, string) VenonaPair
    {
        get
        {
            const string Pad = "QWHZNBXKVUTRMEPLOSAGYCDFJIZKXMQPWNBVLTEOCRHAUSGDYJFIXQZMWKPRNOBTVLESCAHDUGFYJIWQXZ";
            string m1 = "THE SOURCE REPORTS THAT THE WORK ON THE PROJECT AT THE LABORATORY CONTINUES";
            string m2 = "MEET THE COURIER ON THURSDAY AT THE USUAL PLACE AND BRING THE DOCUMENTS";
            var pad = new OneTimePad(Pad);
            return (Letters.Group(pad.Encrypt(m1)), Letters.Group(pad.Encrypt(m2)));
        }
    }
}
