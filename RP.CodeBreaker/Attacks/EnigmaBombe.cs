using System.Diagnostics;
using System.Numerics;
using RP.CodeBreaker.Ciphers;

namespace RP.CodeBreaker.Attacks;

/// <summary>One link in a bombe menu: at crib offset <paramref name="Offset"/>, plaintext letter <paramref name="Plain"/> enciphered to <paramref name="Cipher"/>.</summary>
/// <param name="Plain">The crib letter.</param>
/// <param name="Cipher">The ciphertext letter under it.</param>
/// <param name="Offset">Its position within the crib.</param>
public sealed record MenuLink(char Plain, char Cipher, int Offset);

/// <summary>
/// A bombe menu: the crib laid against the ciphertext, seen as a graph whose letters are joined by
/// the positions at which they encipher to each other.
/// </summary>
/// <param name="Crib">The crib.</param>
/// <param name="CipherSegment">The ciphertext under it.</param>
/// <param name="Position">Where the crib starts in the message.</param>
/// <param name="Links">One link per crib letter.</param>
/// <param name="Loops">How many independent closed loops the graph has (more loops = fewer false stops).</param>
/// <param name="TestLetter">The best-connected letter, whose plugboard partner the bombe hypothesises.</param>
public sealed record BombeMenu(string Crib, string CipherSegment, int Position, IReadOnlyList<MenuLink> Links, int Loops, char TestLetter)
{
    /// <summary>The distinct letters in the menu.</summary>
    public IReadOnlyList<char> Letters => this.Links.SelectMany(l => new[] { l.Plain, l.Cipher }).Distinct().OrderBy(c => c).ToArray();
}

/// <summary>
/// A bombe "stop": a rotor order and position at which the plugboard hypotheses did not all
/// contradict themselves — a candidate the checking machine must then try.
/// </summary>
/// <param name="Rotors">The rotor order, left to right.</param>
/// <param name="CorePosition">The rotor cores at the first crib letter (ring setting A assumed), as three letters.</param>
/// <param name="Steckers">The plugboard pairs the surviving hypothesis implies.</param>
public sealed record BombeStop(IReadOnlyList<EnigmaRotor> Rotors, string CorePosition, IReadOnlyDictionary<char, char> Steckers)
{
    /// <inheritdoc/>
    public override string ToString() =>
        $"{string.Join(" ", this.Rotors.Select(r => r.Name))} at {this.CorePosition}: {string.Join(" ", this.Steckers.Where(kv => kv.Key < kv.Value).Select(kv => $"{kv.Key}{kv.Value}"))}";
}

/// <summary>What a bombe run produced.</summary>
public sealed class BombeResult
{
    /// <summary>The menu(s) used.</summary>
    public required IReadOnlyList<BombeMenu> Menus { get; init; }

    /// <summary>Every stop the bombe made.</summary>
    public required IReadOnlyList<BombeStop> Stops { get; init; }

    /// <summary>Rotor positions tested (orders × 17,576 per menu).</summary>
    public long PositionsTested { get; init; }

    /// <summary>The full settings recovered by the checking machine, or null.</summary>
    public EnigmaSettings? Settings { get; init; }

    /// <summary>The decrypted message, or empty.</summary>
    public string Plaintext { get; init; } = string.Empty;

    /// <summary>How long it took.</summary>
    public TimeSpan Elapsed { get; init; }

    /// <summary>The funnel from Enigma's full key space to the answer.</summary>
    public required Difficulty Difficulty { get; init; }

    /// <summary>What happened, step by step.</summary>
    public IReadOnlyList<string> Log { get; init; } = [];

    /// <summary>True if the settings were recovered.</summary>
    public bool Solved => this.Settings is not null;
}

/// <summary>
/// The Turing–Welchman bombe, in software: given a crib — a stretch of plaintext you are confident is
/// in the message — find the rotor order and positions by <b>logical deduction</b> instead of trying
/// plugboard settings at all.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the plugboard was the problem.</b> Enigma's 1.07 × 10²³ keys are mostly plugboard: 1.5 × 10¹⁴
/// ways of plugging ten cables. Rotor orders and positions are "only" 60 × 17,576 ≈ 1 million. If the
/// plugboard could be dealt with some other way, the rest could be searched by machine.
/// </para>
/// <para>
/// <b>Turing's idea.</b> Write S for the plugboard (it swaps letters in pairs) and Eᵢ for the rotors
/// and reflector at crib position i. A crib letter p enciphering to c at position i means
/// c = S(Eᵢ(S(p))), so S(c) = Eᵢ(S(p)). Now <i>suppose</i> the plugboard partner of some letter — say
/// S(E) = A. Every crib link touching E then tells you another partner, and each of those tells you
/// more. If the guess was wrong, the chain soon demands that one letter has two different partners — a
/// contradiction — and the guess is thrown out. If all 26 guesses for S(E) contradict themselves at a
/// rotor position, the position is impossible. Loops in the menu (crib letters linking back to
/// themselves) make contradictions come quickly.
/// </para>
/// <para>
/// <b>Welchman's diagonal board</b> added the observation that the plugboard is symmetric: if S(E) = A
/// then S(A) = E. That doubles the deductions and slashes the false stops; this implementation applies
/// it on every step.
/// </para>
/// <para>
/// <b>The crib.</b> Enigma never enciphers a letter to itself, so a crib can only sit where no crib
/// letter lies over the same ciphertext letter — see <see cref="PossibleCribPositions"/>. Typical cribs
/// were WETTERBERICHT ("weather report"), KEINEBESONDERENEREIGNISSE ("nothing special to report") and
/// ANX ("to"), from routine messages sent every day in the same form.
/// </para>
/// <para>
/// <b>The assumption.</b> Like the real bombe, this one assumes the middle rotor does not move while
/// the crib is being enciphered (it tests rotor <i>cores</i>, so ring settings drop out). If no stop
/// survives checking, it reruns each half of the crib separately: a crib shorter than 26 letters can
/// contain at most one turnover, so one half is always clean.
/// </para>
/// </remarks>
public static class EnigmaBombe
{
    /// <summary>The most stops kept for the checking machine; a menu that makes more is too weak to use.</summary>
    public const int MaxStopsKept = 2_000;

    /// <summary>
    /// Positions where <paramref name="crib"/> could lie: no crib letter sits above the same ciphertext
    /// letter, because Enigma never enciphers a letter to itself.
    /// </summary>
    public static IReadOnlyList<int> PossibleCribPositions(string ciphertext, string crib)
    {
        string c = Letters.Normalise(ciphertext);
        string p = Letters.Normalise(crib);
        var result = new List<int>();
        for (int at = 0; at + p.Length <= c.Length; at++)
        {
            bool ok = true;
            for (int i = 0; i < p.Length && ok; i++)
            {
                ok = c[at + i] != p[i];
            }

            if (ok)
            {
                result.Add(at);
            }
        }

        return result;
    }

    /// <summary>Lay a crib against the ciphertext at a position and build its menu.</summary>
    public static BombeMenu BuildMenu(string ciphertext, string crib, int position)
    {
        string c = Letters.Normalise(ciphertext);
        string p = Letters.Normalise(crib);
        if (position < 0 || position + p.Length > c.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(position), "The crib must lie within the ciphertext.");
        }

        string segment = c.Substring(position, p.Length);
        var links = p.Select((ch, i) => new MenuLink(ch, segment[i], i)).ToArray();
        var degree = new int[26];
        foreach (MenuLink l in links)
        {
            degree[l.Plain - 'A']++;
            degree[l.Cipher - 'A']++;
        }

        char test = (char)('A' + Array.IndexOf(degree, degree.Max()));

        // Loops = edges − vertices + components (the cycle rank of the menu graph).
        var parent = Enumerable.Range(0, 26).ToArray();
        int Find(int x) => parent[x] == x ? x : parent[x] = Find(parent[x]);
        foreach (MenuLink l in links)
        {
            parent[Find(l.Plain - 'A')] = Find(l.Cipher - 'A');
        }

        int vertices = links.SelectMany(l => new[] { l.Plain, l.Cipher }).Distinct().Count();
        int components = links.SelectMany(l => new[] { l.Plain - 'A', l.Cipher - 'A' }).Select(Find).Distinct().Count();
        int loops = links.Length - vertices + components;
        return new BombeMenu(p, segment, position, links, loops, test);
    }

    /// <summary>Run the bombe (synchronously).</summary>
    public static BombeResult Run(string ciphertext, string crib, int position, IReadOnlyList<EnigmaRotor>? rotors = null, EnigmaReflector? reflector = null, AttackOptions? options = null) =>
        RunAsync(ciphertext, crib, position, rotors, reflector, options).AsTask().GetAwaiter().GetResult();

    /// <summary>
    /// Run the bombe over every order of three rotors from <paramref name="rotors"/> (default I–V) and
    /// all 17,576 core positions, then check each stop and recover the full settings.
    /// </summary>
    /// <param name="ciphertext">The intercepted message.</param>
    /// <param name="crib">Plaintext believed to be in it.</param>
    /// <param name="position">Where the crib starts (see <see cref="PossibleCribPositions"/>).</param>
    /// <param name="rotors">The rotors that might be in the machine.</param>
    /// <param name="reflector">The reflector (default B).</param>
    /// <param name="options">Progress, cancellation, and the language (set <see cref="LanguageModel.German"/> for German traffic).</param>
    public static async ValueTask<BombeResult> RunAsync(string ciphertext, string crib, int position, IReadOnlyList<EnigmaRotor>? rotors = null, EnigmaReflector? reflector = null, AttackOptions? options = null)
    {
        options ??= new AttackOptions();
        rotors ??= EnigmaRotor.ArmyRotors;
        reflector ??= EnigmaReflector.B;
        var watch = Stopwatch.StartNew();
        string text = Letters.Normalise(ciphertext);
        string p = Letters.Normalise(crib);
        var log = new List<string>();
        var orders = Orders(rotors).ToList();
        var difficulty = new Difficulty(EnigmaMachine.KeySpaceFor(rotors.Count, 10));

        int possible = PossibleCribPositions(text, p).Count;
        log.Add($"No letter enciphers to itself: the crib {p} can sit at {possible} of {Math.Max(0, text.Length - p.Length + 1)} positions.");

        var menus = new List<BombeMenu>();
        var stops = new List<BombeStop>();
        long tested = 0;
        (EnigmaSettings Settings, string Plain)? solution = null;

        // Try the whole crib, then (if that fails) each half — one half is always free of a turnover.
        var attempts = new List<(int From, int Length)> { (0, p.Length) };
        if (p.Length >= 8)
        {
            attempts.Add((0, p.Length / 2));
            attempts.Add((p.Length / 2, p.Length - (p.Length / 2)));
        }

        foreach ((int from, int length) in attempts)
        {
            BombeMenu menu = BuildMenu(text, p.Substring(from, length), position + from);
            if (menus.Count > 0 && menu.Loops == 0)
            {
                log.Add($"Menu {menu.Crib} has no loops: it could not contradict a wrong guess, so every position would stop. Not worth running.");
                continue;
            }

            if (menu.Loops == 0)
            {
                log.Add("Warning: this menu has no loops, so expect a great many false stops. A longer crib, or a different position, would help.");
            }

            menus.Add(menu);
            log.Add($"Menu {menu.Crib} over {menu.CipherSegment} at {menu.Position}: {menu.Letters.Count} letters, {menu.Loops} loop(s), test letter {menu.TestLetter}.");
            var menuStops = new List<BombeStop>();
            long stopCount = 0;
            for (int o = 0; o < orders.Count; o++)
            {
                EnigmaRotor[] order = orders[o];
                for (int left = 0; left < 26; left++)
                {
                    for (int middle = 0; middle < 26; middle++)
                    {
                        int[][] scramblers = ScramblersForRightPositions(order, left, middle, reflector);
                        for (int right = 0; right < 26; right++)
                        {
                            tested++;
                            foreach (int[] steckers in Deduce(menu, scramblers, right))
                            {
                                var map = new Dictionary<char, char>();
                                for (int i = 0; i < 26; i++)
                                {
                                    if (steckers[i] >= 0)
                                    {
                                        map[(char)('A' + i)] = (char)('A' + steckers[i]);
                                    }
                                }

                                stopCount++;
                                if (menuStops.Count < MaxStopsKept)
                                {
                                    menuStops.Add(new BombeStop(order, Letters.FromNumbers([left, middle, right]), map));
                                }
                            }
                        }
                    }

                    options.Progress?.Report(new AttackProgress(
                        ((o * 26.0) + left + 1) / (orders.Count * 26.0),
                        $"Menu {menus.Count}: rotors {string.Join(" ", order.Select(r => r.Name))}, left rotor at {Letters.ToLetter(left)} — {stopCount:N0} stop(s)",
                        string.Empty,
                        0));
                    await options.PauseAsync().ConfigureAwait(true);
                }
            }

            stops.AddRange(menuStops);
            log.Add($"{stopCount:N0} stop(s) from {orders.Count * 17_576:N0} rotor positions{(stopCount > menuStops.Count ? $" (the first {menuStops.Count:N0} kept for checking)" : string.Empty)}.");
            solution = Check(text, p, position, menu, menuStops, reflector, options.Language, log);
            if (solution is not null)
            {
                break;
            }

            log.Add("No stop survived checking — perhaps the middle rotor turned during the crib. Trying half the crib.");
        }

        difficulty.Then(
            "Plugboard handled by deduction, rings drop out",
            orders.Count * (BigInteger)17_576,
            "For each rotor position the bombe assumes a plug partner for one letter and follows the crib's consequences; contradictions eliminate the position for every plugboard at once.");
        difficulty.Then($"Bombe stops", Math.Max(1, stops.Count), "Positions where some plug hypothesis survived all the deductions.");
        difficulty.Then("Checking machine", 1, "Each stop is tried with the 26 right-hand ring settings; the plugboard is completed by hill climbing on the full message.");

        return new BombeResult
        {
            Menus = menus,
            Stops = stops,
            PositionsTested = tested,
            Settings = solution?.Settings,
            Plaintext = solution?.Plain ?? string.Empty,
            Elapsed = watch.Elapsed,
            Difficulty = difficulty,
            Log = log,
        };
    }

    /// <summary>Every ordered choice of three rotors.</summary>
    public static IEnumerable<EnigmaRotor[]> Orders(IReadOnlyList<EnigmaRotor> rotors)
    {
        for (int a = 0; a < rotors.Count; a++)
        {
            for (int b = 0; b < rotors.Count; b++)
            {
                for (int c = 0; c < rotors.Count; c++)
                {
                    if (a != b && b != c && a != c)
                    {
                        yield return [rotors[a], rotors[b], rotors[c]];
                    }
                }
            }
        }
    }

    /// <summary>
    /// The scrambler (rotors and reflector, no plugboard) for each of the 26 right-rotor cores, with the
    /// left and middle cores fixed. The left–middle–reflector part is the same for all 26 and is built
    /// once.
    /// </summary>
    internal static int[][] ScramblersForRightPositions(EnigmaRotor[] order, int left, int middle, EnigmaReflector reflector)
    {
        var inner = new int[26];
        for (int x = 0; x < 26; x++)
        {
            int c = Through(order[1].Forward, x, middle);
            c = Through(order[0].Forward, c, left);
            c = reflector.Map[c];
            c = Through(order[0].Backward, c, left);
            inner[x] = Through(order[1].Backward, c, middle);
        }

        var result = new int[26][];
        for (int r = 0; r < 26; r++)
        {
            var map = new int[26];
            for (int x = 0; x < 26; x++)
            {
                map[x] = Through(order[2].Backward, inner[Through(order[2].Forward, x, r)], r);
            }

            result[r] = map;
        }

        return result;
    }

    /// <summary>
    /// The heart of the bombe. For each hypothesis "the test letter is plugged to x", propagate every
    /// consequence through the menu (and the diagonal board's symmetry) and return the hypotheses that
    /// never contradict themselves, as full stecker maps (−1 = not determined).
    /// </summary>
    internal static IEnumerable<int[]> Deduce(BombeMenu menu, int[][] scramblers, int rightCore)
    {
        // Adjacency: for each letter, the (other letter, crib offset) pairs it is linked to.
        var links = menu.Links;
        int test = menu.TestLetter - 'A';
        var survivors = new List<int[]>();
        var steck = new int[26];
        var queue = new Queue<int>();
        for (int hypothesis = 0; hypothesis < 26; hypothesis++)
        {
            Array.Fill(steck, -1);
            queue.Clear();
            bool contradiction = !Assign(steck, queue, test, hypothesis);
            while (!contradiction && queue.Count > 0)
            {
                int a = queue.Dequeue();
                int sa = steck[a];
                foreach (MenuLink l in links)
                {
                    int p = l.Plain - 'A', c = l.Cipher - 'A';
                    int[] e = scramblers[(rightCore + l.Offset) % 26];
                    if (p == a && !Assign(steck, queue, c, e[sa]))
                    {
                        contradiction = true;
                        break;
                    }

                    if (c == a && !Assign(steck, queue, p, e[sa]))
                    {
                        contradiction = true;
                        break;
                    }
                }
            }

            if (!contradiction)
            {
                survivors.Add((int[])steck.Clone());
            }
        }

        // A position where many hypotheses survive tells us nothing (the real bombe would not stop).
        return survivors.Count is >= 1 and <= 3 ? survivors : [];
    }

    private static bool Assign(int[] steck, Queue<int> queue, int letter, int partner)
    {
        if (steck[letter] == partner)
        {
            return true;
        }

        if (steck[letter] >= 0 || (steck[partner] >= 0 && steck[partner] != letter))
        {
            return false;
        }

        steck[letter] = partner;
        steck[partner] = letter; // the diagonal board: plugging is symmetric
        queue.Enqueue(letter);
        if (partner != letter)
        {
            queue.Enqueue(partner);
        }

        return true;
    }

    /// <summary>
    /// The checking machine: for each stop, try the 26 right-ring settings (and the start positions they
    /// imply), keep a setting that enciphers the whole crib correctly, then complete the plugboard by
    /// hill climbing on the full message.
    /// </summary>
    private static (EnigmaSettings Settings, string Plain)? Check(string text, string crib, int cribStart, BombeMenu menu, List<BombeStop> stops, EnigmaReflector reflector, LanguageModel language, List<string> log)
    {
        (EnigmaSettings Settings, double Score)? best = null;
        foreach (BombeStop stop in stops.OrderByDescending(s => s.Steckers.Count).Take(400))
        {
            string plugs = string.Join(" ", stop.Steckers.Where(kv => kv.Key < kv.Value).Select(kv => $"{kv.Key}{kv.Value}"));
            int left = stop.CorePosition[0] - 'A', middle = stop.CorePosition[1] - 'A', right = stop.CorePosition[2] - 'A';
            int stepsBefore = menu.Position + 1; // key presses up to and including the first menu letter
            for (int ring = 0; ring < 26; ring++)
            {
                int windowRight = (right + ring) % 26;
                foreach (int dm in new[] { 0, 1, 2 })
                {
                    foreach (int dl in new[] { 0, 1 })
                    {
                        string start = Letters.FromNumbers([left - dl, middle - dm, windowRight - stepsBefore]);
                        var settings = new EnigmaSettings(stop.Rotors, reflector, "AA" + Letters.ToLetter(ring), start, plugs);
                        var machine = new EnigmaMachine(settings);
                        IReadOnlyList<string> windows = machine.PositionsAfter(stepsBefore);
                        string expected = Letters.FromNumbers([left, middle, windowRight]);
                        if (windows[^1] != expected)
                        {
                            continue;
                        }

                        string plain = machine.Decrypt(text);
                        int matches = crib.Where((ch, i) => cribStart + i < plain.Length && plain[cribStart + i] == ch).Count();
                        if (matches < crib.Length - 2)
                        {
                            continue;
                        }

                        (EnigmaSettings completed, string full) = CompletePlugboard(text, settings, crib, cribStart, language);
                        double score = language.QuadgramScorePerLetter(full);
                        if (best is null || score > best.Value.Score)
                        {
                            best = (completed, score);
                            log.Add($"Stop {stop} checks out with right ring {Letters.ToLetter(ring)}, start {start}: {BruteForce.Preview(full, 50)}");
                        }
                    }
                }
            }
        }

        if (best is null)
        {
            return null;
        }

        return (best.Value.Settings, new EnigmaMachine(best.Value.Settings).Decrypt(text));
    }

    /// <summary>
    /// Fill in the plugboard pairs the menu could not reach: try adding each possible cable and keep it
    /// if the whole decryption becomes more English (and the crib still reads).
    /// </summary>
    internal static (EnigmaSettings Settings, string Plain) CompletePlugboard(string text, EnigmaSettings settings, string crib, int cribStart, LanguageModel language)
    {
        var pairs = settings.Plugboard.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        double Score(List<string> ps)
        {
            string plain = new EnigmaMachine(settings with { Plugboard = string.Join(" ", ps) }).Decrypt(text);
            int cribHits = crib.Where((ch, i) => cribStart + i < plain.Length && plain[cribStart + i] == ch).Count();
            return language.QuadgramScore(plain) + (cribHits * 5);
        }

        double score = Score(pairs);
        bool improved = true;
        while (improved && pairs.Count < 13)
        {
            improved = false;
            string used = string.Concat(pairs);
            string free = new(Letters.Alphabet.Where(c => used.IndexOf(c) < 0).ToArray());
            (string Pair, double Score)? bestAdd = null;
            for (int i = 0; i < free.Length; i++)
            {
                for (int j = i + 1; j < free.Length; j++)
                {
                    string pair = $"{free[i]}{free[j]}";
                    double s = Score([.. pairs, pair]);
                    if (s > score && (bestAdd is null || s > bestAdd.Value.Score))
                    {
                        bestAdd = (pair, s);
                    }
                }
            }

            if (bestAdd is not null)
            {
                pairs.Add(bestAdd.Value.Pair);
                score = bestAdd.Value.Score;
                improved = true;
            }
        }

        EnigmaSettings result = settings with { Plugboard = string.Join(" ", pairs.OrderBy(x => x)) };
        return (result, new EnigmaMachine(result).Decrypt(text));
    }

    private static int Through(int[] wiring, int c, int position) => (wiring[(c + position) % 26] - position + 26) % 26;
}
