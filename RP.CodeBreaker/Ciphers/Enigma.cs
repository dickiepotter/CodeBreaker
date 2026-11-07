using System.Numerics;
using System.Text;

namespace RP.CodeBreaker.Ciphers;

/// <summary>
/// One Enigma rotor (Walze): a disc of 26 contacts on each face, wired together in a scrambled
/// order, with a notch that kicks the next rotor along once per revolution.
/// </summary>
/// <param name="Name">The rotor's Roman numeral.</param>
/// <param name="Wiring">Where each contact A–Z on the entry side leads, at position A with ring A.</param>
/// <param name="Notches">The window letter(s) at which this rotor turns its left-hand neighbour on the next key press.</param>
public sealed record EnigmaRotor(string Name, string Wiring, string Notches)
{
    /// <summary>Rotor I (1930). Turns over at Q.</summary>
    public static EnigmaRotor I { get; } = new("I", "EKMFLGDQVZNTOWYHXUSPAIBRCJ", "Q");

    /// <summary>Rotor II (1930). Turns over at E.</summary>
    public static EnigmaRotor II { get; } = new("II", "AJDKSIRUXBLHWTMCQGZNPYFVOE", "E");

    /// <summary>Rotor III (1930). Turns over at V.</summary>
    public static EnigmaRotor III { get; } = new("III", "BDFHJLCPRTXVZNYEIWGAKMUSQO", "V");

    /// <summary>Rotor IV (1938). Turns over at J.</summary>
    public static EnigmaRotor IV { get; } = new("IV", "ESOVPZJAYQUIRHXLNFTGKDCMWB", "J");

    /// <summary>Rotor V (1938). Turns over at Z.</summary>
    public static EnigmaRotor V { get; } = new("V", "VZBRGITYUPSDNHLXAWMJQOFECK", "Z");

    /// <summary>Rotor VI (Kriegsmarine). Two notches: Z and M.</summary>
    public static EnigmaRotor VI { get; } = new("VI", "JPGVOUMFYQBENHZRDKASXLICTW", "ZM");

    /// <summary>Rotor VII (Kriegsmarine). Two notches: Z and M.</summary>
    public static EnigmaRotor VII { get; } = new("VII", "NZJHGRCXMYSWBOUFAIVLPEKQDT", "ZM");

    /// <summary>Rotor VIII (Kriegsmarine). Two notches: Z and M.</summary>
    public static EnigmaRotor VIII { get; } = new("VIII", "FKQHTLXOCBJSPDZRAMEWNIUYGV", "ZM");

    /// <summary>The five army and air force rotors, I–V.</summary>
    public static IReadOnlyList<EnigmaRotor> ArmyRotors { get; } = [I, II, III, IV, V];

    /// <summary>All eight rotors.</summary>
    public static IReadOnlyList<EnigmaRotor> All { get; } = [I, II, III, IV, V, VI, VII, VIII];

    /// <summary>Look a rotor up by its numeral ("III").</summary>
    public static EnigmaRotor Named(string name) =>
        All.FirstOrDefault(r => string.Equals(r.Name, name.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"There is no Enigma rotor called '{name}'.", nameof(name));

    internal int[] Forward { get; } = Wiring.Select(c => c - 'A').ToArray();

    internal int[] Backward { get; } = Invert(Wiring);

    internal bool IsNotch(int window) => this.Notches.IndexOf(Letters.ToLetter(window)) >= 0;

    private static int[] Invert(string wiring)
    {
        var inv = new int[26];
        for (int i = 0; i < 26; i++)
        {
            inv[wiring[i] - 'A'] = i;
        }

        return inv;
    }
}

/// <summary>
/// The reflector (Umkehrwalze): sends the current back through the rotors by a different path. It
/// pairs letters up, which is what makes Enigma its own inverse (the same settings decrypt) — and why
/// <b>no letter can ever encrypt to itself</b>, the flaw at the heart of the Bletchley attack.
/// </summary>
/// <param name="Name">The reflector's name.</param>
/// <param name="Wiring">Where each letter is sent.</param>
public sealed record EnigmaReflector(string Name, string Wiring)
{
    /// <summary>Reflector B, the standard wartime reflector.</summary>
    public static EnigmaReflector B { get; } = new("B", "YRUHQSLDPXNGOKMIEBFZCWVJAT");

    /// <summary>Reflector C.</summary>
    public static EnigmaReflector C { get; } = new("C", "FVPJIAOYEDRZXWGCTKUQSBNMHL");

    /// <summary>Look a reflector up by name.</summary>
    public static EnigmaReflector Named(string name) => name.Trim().ToUpperInvariant() switch
    {
        "B" => B,
        "C" => C,
        _ => throw new ArgumentException($"There is no reflector called '{name}'.", nameof(name)),
    };

    internal int[] Map { get; } = Wiring.Select(c => c - 'A').ToArray();
}

/// <summary>
/// The daily key of an Enigma machine — everything an operator set up from the key sheet, plus the
/// message's start position.
/// </summary>
/// <param name="Rotors">The rotors from left to right (three of them).</param>
/// <param name="Reflector">The reflector.</param>
/// <param name="RingSettings">The ring setting of each rotor, left to right, as letters ("AAA" = 01 01 01).</param>
/// <param name="Positions">The start position shown in each window, left to right ("AAA").</param>
/// <param name="Plugboard">Plugboard cables as letter pairs: "AB CD EF" (up to 13).</param>
public sealed record EnigmaSettings(
    IReadOnlyList<EnigmaRotor> Rotors,
    EnigmaReflector Reflector,
    string RingSettings = "AAA",
    string Positions = "AAA",
    string Plugboard = "")
{
    /// <summary>
    /// Parse the usual written form: <c>EnigmaSettings.Parse("II IV V", "B", "BUL", "AAA", "AV BS CG DL")</c>.
    /// </summary>
    public static EnigmaSettings Parse(string rotors, string reflector = "B", string rings = "AAA", string positions = "AAA", string plugboard = "") =>
        new(
            rotors.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(EnigmaRotor.Named).ToArray(),
            EnigmaReflector.Named(reflector),
            rings.ToUpperInvariant(),
            positions.ToUpperInvariant(),
            plugboard.ToUpperInvariant());

    /// <summary>The settings with a different start position.</summary>
    public EnigmaSettings WithPositions(string positions) => this with { Positions = positions.ToUpperInvariant() };

    /// <inheritdoc/>
    public override string ToString() =>
        $"{string.Join(" ", this.Rotors.Select(r => r.Name))} / UKW-{this.Reflector.Name} / rings {this.RingSettings} / start {this.Positions} / plugs {(this.Plugboard.Length == 0 ? "none" : this.Plugboard)}";
}

/// <summary>
/// The German Enigma machine (Wehrmacht/Luftwaffe three-rotor model, Enigma I): plugboard → three
/// rotors → reflector → back through the rotors → plugboard, with the rotors stepping like an odometer
/// before every letter.
/// </summary>
/// <remarks>
/// <para>
/// The path of one key press:
/// </para>
/// <code>
/// key → plugboard → right rotor → middle → left → reflector
///                                                    ↓
/// lamp ← plugboard ← right rotor ← middle ← left ←───┘
/// </code>
/// <para>
/// <b>Stepping.</b> The right rotor moves on every key press. When it passes its notch it kicks the
/// middle rotor; when the middle rotor reaches its own notch it moves again on the next press <i>and
/// takes the left rotor with it</i> — the famous "double step", a mechanical quirk that makes the
/// middle rotor move twice in a row. The steps happen <i>before</i> the letter is encrypted.
/// </para>
/// <para>
/// <b>Rings.</b> The alphabet ring on each rotor can be turned relative to its wiring. That changes
/// which letter shows in the window for a given wiring position (and so when the notch acts) without
/// changing the wiring itself.
/// </para>
/// <para>
/// <b>The key space.</b> Choose 3 of 5 rotors in order (60 ways), a start position (26³ = 17,576),
/// ring settings (26² = 676 that matter — the left ring changes nothing that a different start
/// position would not) and ten plugboard cables (150,738,274,937,250 ways). Together about
/// 1.07 × 10²³, roughly 77 bits. See <see cref="KeySpaceFor"/>.
/// </para>
/// </remarks>
public sealed class EnigmaMachine : ICipher
{
    private readonly int[] plug = new int[26];

    /// <summary>Set up a machine from its settings.</summary>
    public EnigmaMachine(EnigmaSettings settings)
    {
        if (settings.Rotors.Count != 3)
        {
            throw new ArgumentException("This machine takes exactly three rotors.", nameof(settings));
        }

        if (settings.RingSettings.Length != 3 || settings.Positions.Length != 3)
        {
            throw new ArgumentException("Ring settings and start positions are three letters each, such as \"AAA\".", nameof(settings));
        }

        this.Settings = settings;
        for (int i = 0; i < 26; i++)
        {
            this.plug[i] = i;
        }

        foreach (string pair in settings.Plugboard.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (pair.Length != 2 || !Letters.IsLetter(pair[0]) || !Letters.IsLetter(pair[1]) || pair[0] == pair[1])
            {
                throw new ArgumentException($"'{pair}' is not a plugboard pair of two different letters.", nameof(settings));
            }

            int a = Letters.ToNumber(pair[0]), b = Letters.ToNumber(pair[1]);
            if (this.plug[a] != a || this.plug[b] != b)
            {
                throw new ArgumentException($"A letter in '{pair}' is already plugged.", nameof(settings));
            }

            this.plug[a] = b;
            this.plug[b] = a;
        }
    }

    /// <summary>The settings.</summary>
    public EnigmaSettings Settings { get; }

    /// <inheritdoc/>
    public string Name => "Enigma";

    /// <inheritdoc/>
    public string KeyDescription => this.Settings.ToString();

    /// <summary>
    /// The number of ways to plug <paramref name="cables"/> cables into the 26-socket plugboard:
    /// 26! / ((26 − 2c)! · c! · 2^c). It peaks at 11 cables; the Germans used 10.
    /// </summary>
    public static BigInteger PlugboardSettings(int cables)
    {
        if (cables < 0 || cables > 13)
        {
            throw new ArgumentOutOfRangeException(nameof(cables));
        }

        return KeySpace.FactorialOf(26) / (KeySpace.FactorialOf(26 - (2 * cables)) * KeySpace.FactorialOf(cables) * BigInteger.Pow(2, cables));
    }

    /// <summary>
    /// The full key space of a three-rotor Enigma: rotor order × start position × ring settings ×
    /// plugboard.
    /// </summary>
    /// <param name="rotorsAvailable">How many rotors to choose from (5 for the army, 8 for the navy).</param>
    /// <param name="cables">Plugboard cables used (10 was standard from 1939).</param>
    public static KeySpace KeySpaceFor(int rotorsAvailable = 5, int cables = 10)
    {
        int orders = rotorsAvailable * (rotorsAvailable - 1) * (rotorsAvailable - 2);
        return KeySpace.Of(orders, $"{orders} rotor orders")
            .Times(KeySpace.Of(17_576, "26³ start positions"))
            .Times(KeySpace.Of(676, "26² ring settings"))
            .Times(KeySpace.Of(PlugboardSettings(cables), $"plugboard with {cables} cables"));
    }

    /// <inheritdoc/>
    public string Encrypt(string plaintext) => this.Run(plaintext, out _);

    /// <inheritdoc/>
    public string Decrypt(string ciphertext) => this.Encrypt(ciphertext);

    /// <summary>
    /// Encrypt (or, identically, decrypt) and also report the window letters after the last key press.
    /// </summary>
    public string Run(string text, out string finalPositions)
    {
        EnigmaRotor[] rotors = this.Settings.Rotors.ToArray();
        int[] ring = this.Settings.RingSettings.Select(Letters.ToNumber).ToArray();
        int[] pos = this.Settings.Positions.Select(Letters.ToNumber).ToArray();
        int[] reflector = this.Settings.Reflector.Map;
        string clean = Letters.Normalise(text);
        var sb = new StringBuilder(clean.Length);
        foreach (char ch in clean)
        {
            Step(rotors, pos);
            int c = this.plug[ch - 'A'];
            for (int r = 2; r >= 0; r--)
            {
                c = Through(rotors[r].Forward, c, pos[r], ring[r]);
            }

            c = reflector[c];
            for (int r = 0; r <= 2; r++)
            {
                c = Through(rotors[r].Backward, c, pos[r], ring[r]);
            }

            sb.Append(Letters.ToLetter(this.plug[c]));
        }

        finalPositions = Letters.FromNumbers(pos);
        return sb.ToString();
    }

    /// <summary>
    /// The window letters after each of <paramref name="presses"/> key presses, from the start
    /// position — useful for watching the double step.
    /// </summary>
    public IReadOnlyList<string> PositionsAfter(int presses)
    {
        EnigmaRotor[] rotors = this.Settings.Rotors.ToArray();
        int[] pos = this.Settings.Positions.Select(Letters.ToNumber).ToArray();
        var result = new List<string>(presses);
        for (int i = 0; i < presses; i++)
        {
            Step(rotors, pos);
            result.Add(Letters.FromNumbers(pos));
        }

        return result;
    }

    /// <summary>
    /// The rotors and reflector alone, without the plugboard and without stepping: the permutation of
    /// the alphabet given by rotor order, ring settings and window positions. The bombe tests these.
    /// </summary>
    internal static int[] Scrambler(EnigmaRotor[] rotors, int[] ring, int[] pos, EnigmaReflector reflector)
    {
        var map = new int[26];
        for (int x = 0; x < 26; x++)
        {
            int c = x;
            for (int r = 2; r >= 0; r--)
            {
                c = Through(rotors[r].Forward, c, pos[r], ring[r]);
            }

            c = reflector.Map[c];
            for (int r = 0; r <= 2; r++)
            {
                c = Through(rotors[r].Backward, c, pos[r], ring[r]);
            }

            map[x] = c;
        }

        return map;
    }

    /// <summary>Advance the rotors for one key press, double step included.</summary>
    internal static void Step(EnigmaRotor[] rotors, int[] pos)
    {
        bool middleAtNotch = rotors[1].IsNotch(pos[1]);
        bool rightAtNotch = rotors[2].IsNotch(pos[2]);
        if (middleAtNotch)
        {
            pos[1] = (pos[1] + 1) % 26; // the double step: the middle rotor moves itself…
            pos[0] = (pos[0] + 1) % 26; // …and the left rotor
        }
        else if (rightAtNotch)
        {
            pos[1] = (pos[1] + 1) % 26;
        }

        pos[2] = (pos[2] + 1) % 26;
    }

    private static int Through(int[] wiring, int c, int position, int ring)
    {
        int shift = position - ring;
        int entry = (c + shift + 26) % 26;
        return (wiring[entry] - shift + 26) % 26;
    }
}
