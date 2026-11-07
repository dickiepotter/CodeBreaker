namespace RP.CodeBreaker;

/// <summary>A key an attack considered, with the plaintext it gave and how English that looked.</summary>
/// <param name="Key">The key, written for a person.</param>
/// <param name="Plaintext">The decryption under that key.</param>
/// <param name="Score">The fitness — higher is more English-like. Only comparable within one attack.</param>
public sealed record Candidate(string Key, string Plaintext, double Score);

/// <summary>What happened when an attack ran: the best key, the text, the work done and why it was that much.</summary>
public sealed class BreakResult
{
    /// <summary>The cipher that was attacked.</summary>
    public required string CipherName { get; init; }

    /// <summary>The best key found, written for a person.</summary>
    public required string Key { get; init; }

    /// <summary>The ciphertext decrypted with <see cref="Key"/>.</summary>
    public required string Plaintext { get; init; }

    /// <summary>The fitness of <see cref="Plaintext"/> (higher is better; compare within one attack only).</summary>
    public double Score { get; init; }

    /// <summary>
    /// Fitness per letter, measured the same way for every attack (mean quadgram log₁₀ probability):
    /// English sits around -4.4, random letters around -6.5 or lower.
    /// </summary>
    public double EnglishLikeness { get; init; }

    /// <summary>True when the result reads like English — the attack is confident it has the answer.</summary>
    public bool LooksLikeEnglish => this.EnglishLikeness > -5.1;

    /// <summary>How many keys (or candidate states) were actually tried.</summary>
    public long KeysTried { get; init; }

    /// <summary>How long the attack took.</summary>
    public TimeSpan Elapsed { get; init; }

    /// <summary>The funnel from the full key space to the work actually needed.</summary>
    public required Difficulty Difficulty { get; init; }

    /// <summary>The best few keys, best first.</summary>
    public IReadOnlyList<Candidate> Candidates { get; init; } = [];

    /// <summary>A narrative of what the attack did, step by step — the "show your working".</summary>
    public IReadOnlyList<string> Log { get; init; } = [];

    /// <inheritdoc/>
    public override string ToString() => $"{this.CipherName} key {this.Key}: {this.Plaintext}";
}

/// <summary>A snapshot of a long-running attack, for progress bars.</summary>
/// <param name="Fraction">How far through, 0–1 (an estimate for searches that end on convergence).</param>
/// <param name="Message">What it is doing.</param>
/// <param name="BestPlaintext">The best decryption so far.</param>
/// <param name="BestScore">Its fitness.</param>
public sealed record AttackProgress(double Fraction, string Message, string BestPlaintext, double BestScore);

/// <summary>
/// Settings shared by the attacks: what we know or guess about the message, how hard to try, and
/// hooks for progress and cancellation.
/// </summary>
public sealed class AttackOptions
{
    /// <summary>The language the plaintext is expected to be in. Defaults to <see cref="LanguageModel.English"/>.</summary>
    public LanguageModel Language { get; init; } = LanguageModel.English;

    /// <summary>
    /// Words or phrases expected somewhere in the message, because of its context — a weather
    /// report will say WETTERBERICHT, a letter will say DEAR, a German army message may end HEIL
    /// HITLER. Attacks use these to discard keys (and to score the survivors).
    /// </summary>
    public IReadOnlyList<string> Cribs { get; init; } = [];

    /// <summary>The seed for any random choices, so a run can be repeated exactly.</summary>
    public int Seed { get; init; } = 1;

    /// <summary>How many times a randomised search starts again from scratch (more = slower, surer).</summary>
    public int Restarts { get; init; } = 8;

    /// <summary>
    /// Trial keys per restart, for searches that need a budget (Playfair's simulated annealing). Playfair
    /// typically needs several hundred thousand to a million trial grids: a few seconds on a laptop,
    /// far longer in a browser.
    /// </summary>
    public int Iterations { get; init; } = 400_000;

    /// <summary>The largest key length (or column count, or rail count) to try.</summary>
    public int MaxKeyLength { get; init; } = 12;

    /// <summary>Stop early when asked.</summary>
    public CancellationToken Cancellation { get; init; }

    /// <summary>Receives progress reports from long searches.</summary>
    public IProgress<AttackProgress>? Progress { get; init; }

    /// <summary>
    /// Called now and again by the long searches. In a browser (Blazor WebAssembly runs on a single
    /// thread) supply <c>async () =&gt; { StateHasChanged(); await Task.Delay(1); }</c> so the page can
    /// repaint; elsewhere leave it null and the search never yields.
    /// </summary>
    public Func<Task>? Breathe { get; init; }

    /// <summary>The normalised cribs (capital letters only, blanks dropped).</summary>
    public IReadOnlyList<string> NormalisedCribs => this.Cribs.Select(Letters.Normalise).Where(c => c.Length > 0).ToArray();

    internal async ValueTask PauseAsync()
    {
        this.Cancellation.ThrowIfCancellationRequested();
        if (this.Breathe is not null)
        {
            await this.Breathe().ConfigureAwait(true);
        }
    }

    internal bool ContainsAllCribs(string plaintext)
    {
        foreach (string crib in this.NormalisedCribs)
        {
            if (!Letters.Normalise(plaintext).Contains(crib, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
