using System.Text;

namespace RP.CodeBreaker.Ciphers;

/// <summary>
/// The rail fence cipher: write the message in a zigzag down and up across a number of "rails",
/// then read each rail off in turn. With three rails, WEAREDISCOVERED is written
/// </summary>
/// <remarks>
/// <code>
/// W . . . E . . . C . . . R . . .
/// . E . R . D . S . O . E . E . .
/// . . A . . . I . . . V . . . D .
/// </code>
/// <para>
/// and sent as WECR ERDSOEE AIVD. It is a <b>transposition</b>: every letter is still itself, just
/// moved — so the letter frequencies are exactly those of English, which is how you recognise one.
/// The key is just the number of rails, so there are only as many keys as the message is long.
/// </para>
/// </remarks>
public sealed class RailFenceCipher : ICipher
{
    /// <summary>Create a rail fence cipher with at least two rails.</summary>
    public RailFenceCipher(int rails)
    {
        if (rails < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(rails), "A rail fence needs at least two rails.");
        }

        this.Rails = rails;
    }

    /// <summary>The number of rails.</summary>
    public int Rails { get; }

    /// <inheritdoc/>
    public string Name => "Rail fence";

    /// <inheritdoc/>
    public string KeyDescription => $"{this.Rails} rails";

    /// <summary>For a message of n letters, at most n − 1 useful rail counts.</summary>
    public static KeySpace KeySpaceFor(int messageLength) => KeySpace.Of(Math.Max(1, messageLength - 2), "2 … n−1 rails");

    /// <summary>Which rail each position of an n-letter message sits on.</summary>
    public int[] RailPattern(int length)
    {
        var rail = new int[length];
        int cycle = (2 * this.Rails) - 2;
        for (int i = 0; i < length; i++)
        {
            int k = i % cycle;
            rail[i] = k < this.Rails ? k : cycle - k;
        }

        return rail;
    }

    /// <inheritdoc/>
    public string Encrypt(string plaintext)
    {
        string p = Letters.Normalise(plaintext);
        int[] rail = this.RailPattern(p.Length);
        var sb = new StringBuilder(p.Length);
        for (int r = 0; r < this.Rails; r++)
        {
            for (int i = 0; i < p.Length; i++)
            {
                if (rail[i] == r)
                {
                    sb.Append(p[i]);
                }
            }
        }

        return sb.ToString();
    }

    /// <inheritdoc/>
    public string Decrypt(string ciphertext)
    {
        string c = Letters.Normalise(ciphertext);
        int[] rail = this.RailPattern(c.Length);
        var result = new char[c.Length];
        int next = 0;
        for (int r = 0; r < this.Rails; r++)
        {
            for (int i = 0; i < c.Length; i++)
            {
                if (rail[i] == r)
                {
                    result[i] = c[next++];
                }
            }
        }

        return new string(result);
    }
}

/// <summary>
/// Columnar transposition: write the message in rows under a keyword, then read the columns off in
/// the alphabetical order of the keyword's letters. With the key ZEBRAS (order 6 3 2 4 1 5), the
/// column under A is read first, then B, E, R, S and finally Z.
/// </summary>
/// <remarks>
/// <para>
/// Only the <i>order</i> of the columns matters, so a key of k columns is one of k! orderings: 720 for
/// six columns, 3.6 million for ten, 1.3 × 10¹² for fifteen. Small keys fall to brute force; larger
/// ones to a hill climb that swaps columns and keeps the swaps that make the text more English. Used
/// in its doubled form (two transpositions in a row) by the German army in the First World War and by
/// SOE agents in the Second.
/// </para>
/// <para>
/// Here the last row is left short when the message doesn't fill it (an "irregular" or incomplete
/// columnar), which is harder to attack than padding it out with nulls.
/// </para>
/// </remarks>
public sealed class ColumnarTranspositionCipher : ICipher
{
    /// <summary>Create a columnar transposition from a keyword.</summary>
    public ColumnarTranspositionCipher(string keyword)
        : this(OrderFromKeyword(keyword))
    {
        this.Keyword = Letters.Normalise(keyword);
    }

    /// <summary>
    /// Create a columnar transposition from a reading order: <c>order[i]</c> is the column read
    /// <i>i</i>-th (0-based). [4, 2, 1, 3, 5, 0] reads column 4 first.
    /// </summary>
    public ColumnarTranspositionCipher(IReadOnlyList<int> order)
    {
        if (order.Count < 1 || order.OrderBy(x => x).Where((x, i) => x != i).Any())
        {
            throw new ArgumentException("The column order must be a permutation of 0 … k−1.", nameof(order));
        }

        this.Order = order.ToArray();
        this.Keyword = null;
    }

    /// <summary>The keyword, if the cipher was made from one.</summary>
    public string? Keyword { get; }

    /// <summary>The column reading order.</summary>
    public IReadOnlyList<int> Order { get; }

    /// <summary>The number of columns.</summary>
    public int Columns => this.Order.Count;

    /// <inheritdoc/>
    public string Name => "Columnar transposition";

    /// <inheritdoc/>
    public string KeyDescription => (this.Keyword is null ? string.Empty : this.Keyword + " = ") + "read columns " + string.Join(" ", this.Order.Select(i => i + 1));

    /// <summary>k! column orders for a key of k columns.</summary>
    public static KeySpace KeySpaceFor(int columns) => KeySpace.Factorial(columns, $"{columns}! column orders");

    /// <summary>
    /// The reading order a keyword gives: its letters sorted alphabetically, ties left to right.
    /// </summary>
    public static int[] OrderFromKeyword(string keyword)
    {
        string k = Letters.Normalise(keyword);
        if (k.Length == 0)
        {
            throw new ArgumentException("A transposition keyword needs at least one letter.", nameof(keyword));
        }

        return Enumerable.Range(0, k.Length).OrderBy(i => k[i]).ThenBy(i => i).ToArray();
    }

    /// <inheritdoc/>
    public string Encrypt(string plaintext) => Encrypt(Letters.Normalise(plaintext), this.Order);

    /// <inheritdoc/>
    public string Decrypt(string ciphertext) => Decrypt(Letters.Normalise(ciphertext), this.Order);

    /// <summary>Encrypt already-normalised text with a column order.</summary>
    internal static string Encrypt(string text, IReadOnlyList<int> order)
    {
        int k = order.Count;
        var sb = new StringBuilder(text.Length);
        foreach (int col in order)
        {
            for (int i = col; i < text.Length; i += k)
            {
                sb.Append(text[i]);
            }
        }

        return sb.ToString();
    }

    /// <summary>Decrypt already-normalised text with a column order (letters only, for speed).</summary>
    internal static string Decrypt(string text, IReadOnlyList<int> order)
    {
        var result = new char[text.Length];
        DecryptInto(text, order, result);
        return new string(result);
    }

    /// <summary>Decrypt into a buffer — the attack's inner loop.</summary>
    internal static void DecryptInto<T>(IReadOnlyList<T> text, IReadOnlyList<int> order, T[] result)
    {
        int k = order.Count;
        int n = text.Count;
        int fullRows = n / k;
        int longColumns = n % k; // columns 0 … longColumns−1 have one extra letter
        int next = 0;
        foreach (int col in order)
        {
            int height = fullRows + (col < longColumns ? 1 : 0);
            for (int r = 0; r < height; r++)
            {
                result[(r * k) + col] = text[next++];
            }
        }
    }

    private static void DecryptInto(string text, IReadOnlyList<int> order, char[] result) =>
        DecryptInto(text.ToCharArray(), order, result);
}
