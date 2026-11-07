using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using RP.CodeBreaker;
using RP.CodeBreaker.Attacks;
using RP.CodeBreaker.Ciphers;
using RP.CodeBreaker.Codes;
using RP.CodeBreaker.Examples;
using RP.CodeBreaker.Modern;

namespace RP.CodeBreaker.Showcase;

/// <summary>
/// The showcase's whole interface to the library, as JSON in and JSON out. The ASP.NET server maps it
/// onto <c>POST /api/{operation}</c>; the WebAssembly build exports it to JavaScript, where it runs in a
/// Web Worker. Both therefore answer every request identically — only the speed differs (the server
/// runs native .NET; the browser runs interpreted WebAssembly).
/// </summary>
/// <remarks>
/// JSON is built with <see cref="JsonNode"/> rather than reflection-based serialisation, so nothing
/// here is lost when the WebAssembly build is trimmed.
/// </remarks>
public static class ShowcaseApi
{
    /// <summary>The operations, by name.</summary>
    private static readonly Dictionary<string, Func<JsonObject, Action<string>?, JsonNode>> Operations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["meta"] = (_, _) => Meta(),
        ["convert"] = (r, _) => Convert(r),
        ["analyse"] = (r, _) => Analyse(r),
        ["break"] = Break,
        ["enigma"] = (r, _) => Enigma(r),
        ["crib-positions"] = (r, _) => CribPositions(r),
        ["menu"] = (r, _) => Menu(r),
        ["bombe"] = Bombe,
        ["two-time-pad"] = (r, _) => TwoTimePad(r),
        ["rsa"] = (r, _) => Rsa(r),
        ["crack"] = (r, _) => Crack(r),
        ["keyspace"] = (r, _) => KeySpaceCalculator(r),
    };

    /// <summary>
    /// Run an operation. <paramref name="progress"/>, if given, receives JSON progress reports
    /// (<c>{ fraction, message, best }</c>) from the long searches.
    /// </summary>
    /// <exception cref="ArgumentException">An unknown operation or a bad request (the message says why).</exception>
    public static string Call(string operation, string requestJson, Action<string>? progress = null)
    {
        if (!Operations.TryGetValue(operation, out var handler))
        {
            throw new ArgumentException($"There is no operation called '{operation}'.");
        }

        JsonObject request = string.IsNullOrWhiteSpace(requestJson) ? [] : JsonNode.Parse(requestJson) as JsonObject ?? [];
        try
        {
            return handler(request, progress).ToJsonString();
        }
        catch (Exception e) when (e is FormatException or OverflowException or InvalidOperationException or KeyNotFoundException or IndexOutOfRangeException)
        {
            throw new ArgumentException(e.Message, e);
        }
    }

    // ---------------------------------------------------------------- meta

    private static JsonObject Meta() => new()
    {
        ["attackers"] = new JsonArray(Attacker.All.Select(a => (JsonNode)new JsonObject
        {
            ["name"] = a.Name,
            ["rate"] = a.KeysPerSecond,
            ["notes"] = a.Notes,
            ["fewMinutesBits"] = Math.Log2(a.KeysPerSecond * WorkEstimate.FewMinutesSeconds),
        }).ToArray()),
        ["catalogue"] = new JsonArray(CipherCatalogue.All.Select(p => (JsonNode)new JsonObject
        {
            ["name"] = p.Name,
            ["era"] = p.Era,
            ["keySpaceBits"] = p.KeySpace.Bits,
            ["keySpace"] = KeySpace.Format(p.KeySpace.Size),
            ["keySpaceDescription"] = p.KeySpace.Description,
            ["effectiveBits"] = p.EffectiveBits,
            ["attack"] = p.Attack,
            ["explanation"] = p.Explanation,
            ["famous"] = p.Famous,
            ["breakable"] = p.Breakable,
            ["unicity"] = p.KeySpace.UnicityDistance(),
            ["times"] = new JsonArray(Attacker.All.Select(a => (JsonNode)new JsonObject
            {
                ["brute"] = Time(p.BruteForce(a)),
                ["best"] = Time(p.BestAttack(a)),
            }).ToArray()),
        }).ToArray()),
        ["ciphers"] = new JsonArray(CipherRegistry.All.Select(c => (JsonNode)new JsonObject
        {
            ["name"] = c.Name,
            ["keyFormat"] = c.KeyFormat,
            ["exampleKey"] = c.ExampleKey,
        }).ToArray()),
        ["codes"] = new JsonArray(CodeCatalogue.All.Select(c => (JsonNode)new JsonObject
        {
            ["name"] = c.Name,
            ["description"] = c.Description,
            ["phrases"] = c is Codebook book ? new JsonArray(book.Phrases.Select(ph => (JsonNode)new JsonObject { ["phrase"] = ph, ["group"] = book.Encode(ph) }).ToArray()) : null,
        }).ToArray()),
        ["cases"] = new JsonArray(FamousCases.All.Select(c => (JsonNode)new JsonObject
        {
            ["title"] = c.Title,
            ["year"] = c.Year,
            ["story"] = c.Story,
            ["cipher"] = c.Cipher,
            ["key"] = c.Key,
            ["ciphertext"] = c.Ciphertext,
            ["cribs"] = Strings(c.Cribs),
            ["howToBreak"] = c.HowToBreak,
            ["historical"] = c.Historical,
        }).ToArray()),
        ["rotors"] = Strings(EnigmaRotor.All.Select(r => r.Name)),
        ["reflectors"] = Strings(["B", "C"]),
        ["attacks"] = Strings(AttackNames),
    };

    private static JsonObject Time(WorkEstimate w) => new()
    {
        ["seconds"] = double.IsFinite(w.Seconds) ? w.Seconds : double.MaxValue,
        ["text"] = WorkEstimate.Describe(w.Seconds),
        ["feasibility"] = w.Feasibility.ToString(),
    };

    // ---------------------------------------------------------------- convert

    private static JsonObject Convert(JsonObject r)
    {
        string kind = Str(r, "kind", "cipher");
        string name = Str(r, "name");
        string text = Str(r, "text");
        bool forward = Str(r, "direction", "encrypt") is "encrypt" or "encode";
        if (kind == "code")
        {
            ICode code = CodeCatalogue.Named(name);
            return new JsonObject { ["output"] = forward ? code.Encode(text) : code.Decode(text) };
        }

        CipherDefinition definition = CipherRegistry.Named(name);
        ICipher cipher = definition.Create(Str(r, "key", definition.ExampleKey));
        var result = new JsonObject
        {
            ["output"] = forward ? cipher.Encrypt(text) : cipher.Decrypt(text),
            ["keyDescription"] = cipher.KeyDescription,
        };
        KeySpace? space = KeySpaceFor(cipher, text);
        if (space is not null)
        {
            result["keySpace"] = space.ToString();
            result["keySpaceBits"] = space.Bits;
        }

        return result;
    }

    private static KeySpace? KeySpaceFor(ICipher cipher, string text) => cipher switch
    {
        CaesarCipher => CaesarCipher.KeySpace,
        AtbashCipher => AtbashCipher.KeySpace,
        AffineCipher => AffineCipher.KeySpace,
        SubstitutionCipher => SubstitutionCipher.KeySpace,
        VigenereCipher v => VigenereCipher.KeySpaceForLength(v.Key.Length),
        BeaufortCipher b => VigenereCipher.KeySpaceForLength(b.Key.Length),
        AutokeyCipher a => VigenereCipher.KeySpaceForLength(a.Primer.Length),
        OneTimePad => OneTimePad.KeySpaceForLength(Math.Max(1, Letters.Normalise(text).Length)),
        RailFenceCipher => RailFenceCipher.KeySpaceFor(Letters.Normalise(text).Length),
        ColumnarTranspositionCipher c => ColumnarTranspositionCipher.KeySpaceFor(c.Columns),
        PolybiusCipher => PolybiusCipher.KeySpace,
        PlayfairCipher => PlayfairCipher.KeySpace,
        HillCipher h when h.Size == 2 => HillCipher.KeySpace2x2,
        EnigmaMachine => EnigmaMachine.KeySpaceFor(),
        _ => null,
    };

    // ---------------------------------------------------------------- analyse

    private static JsonObject Analyse(JsonObject r)
    {
        string text = Str(r, "text");
        LanguageModel language = Language(r);
        string letters = Letters.Normalise(text);
        int maxPeriod = Math.Clamp(Int(r, "maxPeriod", 20), 1, 40);
        var result = new JsonObject
        {
            ["length"] = letters.Length,
            ["letters"] = new JsonArray(FrequencyAnalysis.Ranked(text, language)
                .OrderBy(l => l.Letter)
                .Select(l => (JsonNode)new JsonObject { ["letter"] = l.Letter.ToString(), ["count"] = l.Count, ["share"] = l.Share, ["expected"] = l.Expected })
                .ToArray()),
            ["ioc"] = FrequencyAnalysis.IndexOfCoincidence(text),
            ["englishIoc"] = language.ExpectedIndexOfCoincidence,
            ["randomIoc"] = 1.0 / 26,
            ["entropy"] = FrequencyAnalysis.Entropy(text),
            ["chiSquared"] = FrequencyAnalysis.ChiSquared(text, language),
            ["pValue"] = letters.Length == 0 ? 1 : FrequencyAnalysis.ChiSquaredPValue(text, language),
            ["quadgramScore"] = language.QuadgramScorePerLetter(text),
            ["friedman"] = letters.Length < 2 ? 0 : Periodicity.FriedmanEstimate(text, language),
            ["bigrams"] = Grams(FrequencyAnalysis.NGrams(text, 2, 12)),
            ["trigrams"] = Grams(FrequencyAnalysis.NGrams(text, 3, 12)),
            ["guesses"] = new JsonArray(CipherIdentifier.Identify(text, language)
                .Select(g => (JsonNode)new JsonObject { ["family"] = g.Family.ToString(), ["confidence"] = g.Confidence, ["reason"] = g.Reason })
                .ToArray()),
        };

        if (letters.Length >= 8)
        {
            IReadOnlyDictionary<int, int> votes = Periodicity.KasiskiVotes(letters, maxPeriod);
            result["periods"] = new JsonArray(Enumerable.Range(1, Math.Min(maxPeriod, Math.Max(1, letters.Length / 4)))
                .Select(p => (JsonNode)new JsonObject { ["period"] = p, ["ioc"] = Periodicity.AverageColumnIoC(letters, p), ["votes"] = votes.TryGetValue(p, out int v) ? v : 0 })
                .ToArray());
            result["bestPeriod"] = Periodicity.RankPeriods(letters, maxPeriod, language)[0].Period;
            result["repeats"] = new JsonArray(Periodicity.Repeats(letters).Take(25)
                .Select(x => (JsonNode)new JsonObject { ["sequence"] = x.Sequence, ["positions"] = Ints(x.Positions), ["spacings"] = Ints(x.Spacings) })
                .ToArray());
        }

        return result;
    }

    private static JsonArray Grams(IEnumerable<KeyValuePair<string, int>> grams) =>
        new(grams.Select(g => (JsonNode)new JsonObject { ["gram"] = g.Key, ["count"] = g.Value }).ToArray());

    // ---------------------------------------------------------------- break

    /// <summary>The attacks the break operation accepts.</summary>
    public static IReadOnlyList<string> AttackNames { get; } =
        ["Auto", "Caesar", "Affine", "Substitution", "Vigenère", "Beaufort", "Autokey", "Rail fence", "Columnar", "Playfair", "Hill", "Polybius", "XOR"];

    private static JsonObject Break(JsonObject r, Action<string>? progress)
    {
        string attack = Str(r, "attack", "Auto");
        string ciphertext = Str(r, "ciphertext");
        var options = new AttackOptions
        {
            Language = Language(r),
            Cribs = StrArray(r, "cribs"),
            Restarts = Math.Clamp(Int(r, "restarts", 8), 1, 100),
            MaxKeyLength = Math.Clamp(Int(r, "maxKeyLength", 12), 2, 30),
            Iterations = Math.Clamp(Int(r, "iterations", 400_000), 1_000, 10_000_000),
            Seed = Int(r, "seed", 1),
            Progress = Reporter(progress),
        };

        JsonArray? guesses = null;
        BreakResult result;
        switch (attack)
        {
            case "Auto":
                var (identified, best, _) = AutoBreaker.BreakAsync(ciphertext, options).AsTask().GetAwaiter().GetResult();
                guesses = new JsonArray(identified.Select(g => (JsonNode)new JsonObject { ["family"] = g.Family.ToString(), ["confidence"] = g.Confidence, ["reason"] = g.Reason }).ToArray());
                result = best ?? throw new ArgumentException("The statistics point to a cipher this library cannot attack automatically (or the text is too short).");
                break;
            case "Caesar": result = CaesarAttack.Break(ciphertext, options); break;
            case "Affine": result = AffineAttack.Break(ciphertext, options); break;
            case "Substitution": result = SubstitutionAttack.Break(ciphertext, options); break;
            case "Vigenère": result = VigenereAttack.Break(ciphertext, options); break;
            case "Beaufort": result = VigenereAttack.Break(ciphertext, options, PolyalphabeticVariant.Beaufort); break;
            case "Autokey": result = AutokeyAttack.Break(ciphertext, options); break;
            case "Rail fence": result = RailFenceAttack.Break(ciphertext, options); break;
            case "Columnar": result = ColumnarAttack.Break(ciphertext, options); break;
            case "Playfair": result = PlayfairAttack.Break(ciphertext, options); break;
            case "Hill": result = HillAttack.Break(ciphertext, options); break;
            case "Polybius": result = PolybiusAttack.Break(ciphertext, options); break;
            case "XOR": result = XorAttack.BreakHex(ciphertext, Math.Max(options.MaxKeyLength, 40)); break;
            default: throw new ArgumentException($"There is no attack called '{attack}'.");
        }

        BreakCheck check = BreakCheck.Check(result, attack == "XOR" ? string.Empty : ciphertext, options);
        return new JsonObject
        {
            ["check"] = new JsonObject
            {
                ["confidence"] = check.Confidence.ToString(),
                ["summary"] = check.Summary,
                ["reasons"] = Strings(check.Reasons),
                ["cribsFound"] = Strings(check.CribsFound),
                ["cribsMissing"] = Strings(check.CribsMissing),
                ["letters"] = check.Letters,
                ["lettersNeeded"] = check.LettersNeeded,
            },
            ["cipher"] = result.CipherName,
            ["key"] = result.Key,
            ["plaintext"] = result.Plaintext,
            ["segmented"] = attack == "XOR" ? result.Plaintext : options.Language.Segment(result.Plaintext),
            ["englishLikeness"] = result.EnglishLikeness,
            ["looksLikeEnglish"] = result.LooksLikeEnglish,
            ["keysTried"] = result.KeysTried,
            ["elapsedMs"] = result.Elapsed.TotalMilliseconds,
            ["difficulty"] = DifficultyJson(result.Difficulty),
            ["log"] = Strings(result.Log),
            ["candidates"] = new JsonArray(result.Candidates.Take(8).Select(c => (JsonNode)new JsonObject
            {
                ["key"] = c.Key,
                ["plaintext"] = c.Plaintext,
                ["score"] = c.Score,
            }).ToArray()),
            ["guesses"] = guesses,
        };
    }

    private static JsonObject DifficultyJson(Difficulty d) => new()
    {
        ["start"] = new JsonObject { ["description"] = d.Start.Description, ["size"] = KeySpace.Format(d.Start.Size), ["bits"] = d.Start.Bits },
        ["steps"] = new JsonArray(d.Steps.Select(s => (JsonNode)new JsonObject
        {
            ["insight"] = s.Insight,
            ["why"] = s.Why,
            ["size"] = KeySpace.Format(s.After),
            ["bits"] = KeySpace.Log2(BigInteger.Max(1, s.After)),
            ["bitsSaved"] = s.BitsSaved,
        }).ToArray()),
        ["remainingBits"] = d.RemainingBits,
        ["bitsSaved"] = d.BitsSaved,
        ["times"] = new JsonArray(Attacker.All.Select(a => (JsonNode)new JsonObject
        {
            ["attacker"] = a.Name,
            ["naive"] = Time(d.NaiveWork(a)),
            ["remaining"] = Time(d.RemainingWork(a)),
        }).ToArray()),
    };

    // ---------------------------------------------------------------- Enigma

    private static EnigmaSettings Settings(JsonObject r) => EnigmaSettings.Parse(
        Str(r, "rotors", "I II III"),
        Str(r, "reflector", "B"),
        Str(r, "rings", "AAA"),
        Str(r, "positions", "AAA"),
        Str(r, "plugs", string.Empty));

    private static JsonObject Enigma(JsonObject r)
    {
        var machine = new EnigmaMachine(Settings(r));
        string text = Str(r, "text");
        string output = machine.Run(text, out string final);
        return new JsonObject
        {
            ["output"] = output,
            ["grouped"] = Letters.Group(output),
            ["final"] = final,
            ["windows"] = Strings(machine.PositionsAfter(Math.Min(Letters.Normalise(text).Length, 400))),
            ["keySpace"] = EnigmaMachine.KeySpaceFor().ToString(),
        };
    }

    private static JsonObject CribPositions(JsonObject r)
    {
        string ciphertext = Str(r, "ciphertext"), crib = Str(r, "crib");
        IReadOnlyList<int> positions = EnigmaBombe.PossibleCribPositions(ciphertext, crib);
        return new JsonObject
        {
            ["positions"] = Ints(positions),
            ["total"] = Math.Max(0, Letters.Normalise(ciphertext).Length - Letters.Normalise(crib).Length + 1),
        };
    }

    private static JsonObject Menu(JsonObject r)
    {
        BombeMenu m = EnigmaBombe.BuildMenu(Str(r, "ciphertext"), Str(r, "crib"), Int(r, "position", 0));
        return MenuJson(m);
    }

    private static JsonObject MenuJson(BombeMenu m) => new()
    {
        ["crib"] = m.Crib,
        ["cipherSegment"] = m.CipherSegment,
        ["position"] = m.Position,
        ["links"] = new JsonArray(m.Links.Select(l => (JsonNode)new JsonObject { ["plain"] = l.Plain.ToString(), ["cipher"] = l.Cipher.ToString(), ["offset"] = l.Offset }).ToArray()),
        ["loops"] = m.Loops,
        ["letters"] = Strings(m.Letters.Select(c => c.ToString())),
        ["testLetter"] = m.TestLetter.ToString(),
    };

    private static JsonObject Bombe(JsonObject r, Action<string>? progress)
    {
        EnigmaRotor[] rotors = StrArray(r, "rotors").Select(EnigmaRotor.Named).ToArray();
        if (rotors.Length < 3)
        {
            throw new ArgumentException("Choose at least three rotors for the bombe to try.");
        }

        var options = new AttackOptions { Language = Language(r, "German"), Progress = Reporter(progress) };
        BombeResult b = EnigmaBombe.Run(
            Str(r, "ciphertext"),
            Str(r, "crib"),
            Int(r, "position", 0),
            rotors,
            EnigmaReflector.Named(Str(r, "reflector", "B")),
            options);
        return new JsonObject
        {
            ["solved"] = b.Solved,
            ["settings"] = b.Settings?.ToString(),
            ["rotors"] = b.Settings is null ? null : string.Join(" ", b.Settings.Rotors.Select(x => x.Name)),
            ["rings"] = b.Settings?.RingSettings,
            ["positions"] = b.Settings?.Positions,
            ["plugs"] = b.Settings?.Plugboard,
            ["plaintext"] = b.Plaintext,
            ["stops"] = Strings(b.Stops.Take(50).Select(s => s.ToString())),
            ["stopCount"] = b.Stops.Count,
            ["positionsTested"] = b.PositionsTested,
            ["elapsedMs"] = b.Elapsed.TotalMilliseconds,
            ["menus"] = new JsonArray(b.Menus.Select(m => (JsonNode)MenuJson(m)).ToArray()),
            ["difficulty"] = DifficultyJson(b.Difficulty),
            ["log"] = Strings(b.Log),
        };
    }

    // ---------------------------------------------------------------- one-time pad reused

    private static JsonObject TwoTimePad(JsonObject r)
    {
        var drags = TwoTimePadAttack.DragCrib(Str(r, "first"), Str(r, "second"), Str(r, "crib"));
        return new JsonObject
        {
            ["results"] = new JsonArray(drags.Take(Math.Clamp(Int(r, "top", 15), 1, 100)).Select(d => (JsonNode)new JsonObject
            {
                ["position"] = d.Position,
                ["other"] = d.OtherMessage,
                ["readability"] = d.Readability,
            }).ToArray()),
        };
    }

    // ---------------------------------------------------------------- modern

    private static JsonObject Rsa(JsonObject r)
    {
        int bits = Math.Clamp(Int(r, "bits", 48), 12, 120);
        bool careless = r["close"] is JsonValue c && c.TryGetValue(out bool close) && close;
        var rsa = careless ? ToyRsa.GenerateCareless(bits, Int(r, "seed", 1)) : ToyRsa.Generate(bits, Int(r, "seed", 1));
        BigInteger message = BigInteger.Parse(Str(r, "message", "42"), CultureInfo.InvariantCulture) % rsa.N;
        BigInteger cipher = rsa.Encrypt(message);
        string method = Str(r, "method", "auto");
        FactorResult f = method switch
        {
            "none" => new FactorResult(rsa.N, null, "none", 0, TimeSpan.Zero), // just make the key
            "trial" => Factoring.TrialDivision(rsa.N),
            "fermat" => Factoring.Fermat(rsa.N),
            "rho" => Factoring.PollardRho(rsa.N),
            _ => Factoring.Factor(rsa.N),
        };
        var result = new JsonObject
        {
            ["careless"] = careless,
            ["n"] = rsa.N.ToString(CultureInfo.InvariantCulture),
            ["bits"] = rsa.Bits,
            ["e"] = rsa.E.ToString(CultureInfo.InvariantCulture),
            ["p"] = rsa.P.ToString(CultureInfo.InvariantCulture),
            ["q"] = rsa.Q.ToString(CultureInfo.InvariantCulture),
            ["message"] = message.ToString(CultureInfo.InvariantCulture),
            ["ciphertext"] = cipher.ToString(CultureInfo.InvariantCulture),
            ["method"] = f.Method,
            ["success"] = f.Success,
            ["steps"] = f.Steps,
            ["elapsedMs"] = f.Elapsed.TotalMilliseconds,
            ["factor"] = f.Factor?.ToString(CultureInfo.InvariantCulture),
            ["cofactor"] = f.Cofactor?.ToString(CultureInfo.InvariantCulture),
        };
        if (f.Success)
        {
            BigInteger d = ToyRsa.PrivateExponentFromFactors(f.Factor!.Value, f.Cofactor!.Value, rsa.E);
            result["d"] = d.ToString(CultureInfo.InvariantCulture);
            result["recovered"] = BigInteger.ModPow(cipher, d, rsa.N).ToString(CultureInfo.InvariantCulture);
        }

        result["work"] = new JsonArray(new[] { 512, 1024, 2048, 4096 }.Select(b =>
        {
            var w = Factoring.WorkInBits(b);
            return (JsonNode)new JsonObject { ["bits"] = b, ["trial"] = w.TrialDivisionBits, ["rho"] = w.PollardRhoBits, ["nfs"] = w.NumberFieldSieveBits };
        }).ToArray());
        return result;
    }

    private static JsonObject Crack(JsonObject r)
    {
        string kind = Str(r, "kind", "pin");
        string secret = Str(r, "secret");
        string hash = PasswordCracking.Sha256Hex(secret);
        CrackResult c = kind == "pin"
            ? PasswordCracking.CrackPin(hash, Math.Clamp(Int(r, "digits", Math.Max(1, secret.Length)), 1, 8))
            : PasswordCracking.CrackWord(hash, maxWords: Math.Clamp(Int(r, "maxWords", 300), 10, 20_000));
        return new JsonObject
        {
            ["hash"] = hash,
            ["found"] = c.Found,
            ["secret"] = c.Secret,
            ["tried"] = c.Tried,
            ["space"] = KeySpace.Format(c.SearchSpace),
            ["elapsedMs"] = c.Elapsed.TotalMilliseconds,
            ["rate"] = c.Rate,
        };
    }

    private static JsonObject KeySpaceCalculator(JsonObject r)
    {
        int a = Math.Clamp(Int(r, "a", 26), 1, 100_000);
        int b = Math.Clamp(Int(r, "b", 5), 0, 4096);
        KeySpace space = Str(r, "kind", "power") switch
        {
            "factorial" => KeySpace.Factorial(Math.Min(a, 2000)),
            "bits" => KeySpace.FromBits(Math.Min(a, 4096)),
            _ => KeySpace.Power(a, b),
        };
        return new JsonObject
        {
            ["size"] = KeySpace.Format(space.Size),
            ["description"] = space.Description,
            ["bits"] = space.Bits,
            ["unicity"] = space.UnicityDistance(),
            ["times"] = new JsonArray(Attacker.All.Select(x => (JsonNode)new JsonObject
            {
                ["attacker"] = x.Name,
                ["all"] = Time(space.ExhaustiveSearch(x)),
                ["expected"] = Time(space.ExpectedSearch(x)),
            }).ToArray()),
        };
    }

    // ---------------------------------------------------------------- helpers

    private static IProgress<AttackProgress>? Reporter(Action<string>? progress) =>
        progress is null ? null : new DirectProgress(p => progress(new JsonObject
        {
            ["fraction"] = p.Fraction,
            ["message"] = p.Message,
            ["best"] = p.BestPlaintext.Length > 200 ? p.BestPlaintext[..200] : p.BestPlaintext,
        }.ToJsonString()));

    /// <summary>
    /// Reports progress on the calling thread, at once. <see cref="Progress{T}"/> would post to a
    /// synchronisation context or the thread pool — which, in a single-threaded WebAssembly worker busy
    /// running the attack, would deliver every report only after the attack had finished.
    /// </summary>
    private sealed class DirectProgress(Action<AttackProgress> report) : IProgress<AttackProgress>
    {
        public void Report(AttackProgress value) => report(value);
    }

    private static LanguageModel Language(JsonObject r, string fallback = "English") =>
        string.Equals(Str(r, "language", fallback), "German", StringComparison.OrdinalIgnoreCase) ? LanguageModel.German : LanguageModel.English;

    private static string Str(JsonObject r, string name, string fallback = "") =>
        r[name] is JsonValue v && v.TryGetValue(out string? s) ? s : fallback;

    private static int Int(JsonObject r, string name, int fallback) => r[name] switch
    {
        JsonValue v when v.TryGetValue(out int i) => i,
        JsonValue v when v.TryGetValue(out double d) => (int)d,
        JsonValue v when v.TryGetValue(out string? s) && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int p) => p,
        _ => fallback,
    };

    private static string[] StrArray(JsonObject r, string name) => r[name] switch
    {
        JsonArray a => a.Select(x => x?.GetValue<string>() ?? string.Empty).Where(x => x.Trim().Length > 0).ToArray(),
        JsonValue v when v.TryGetValue(out string? s) => s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        _ => [],
    };

    private static JsonArray Strings(IEnumerable<string> items) => new(items.Select(s => (JsonNode)JsonValue.Create(s)!).ToArray());

    private static JsonArray Ints(IEnumerable<int> items) => new(items.Select(i => (JsonNode)JsonValue.Create(i)).ToArray());
}
