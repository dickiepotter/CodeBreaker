using System.Text;

namespace RP.CodeBreaker.Ciphers;

/// <summary>
/// A 5 × 5 grid of 25 letters (I and J share a cell), the basis of the Polybius, Playfair and Bifid
/// ciphers. Usually filled with a keyword alphabet: keyword first, then the remaining letters.
/// </summary>
public sealed class PolybiusGrid
{
    /// <summary>The 25-letter alphabet with no J.</summary>
    public const string Alphabet25 = "ABCDEFGHIKLMNOPQRSTUVWXYZ";

    private readonly (int Row, int Col)[] where = new (int, int)[26];

    /// <summary>Make a grid from a keyword (blank keyword = the plain A–Z order).</summary>
    public PolybiusGrid(string keyword)
    {
        this.Letters = CodeBreaker.Letters.KeywordAlphabet(keyword.ToUpperInvariant().Replace('J', 'I'), Alphabet25);
        for (int i = 0; i < 25; i++)
        {
            this.where[this.Letters[i] - 'A'] = (i / 5, i % 5);
        }

        this.where['J' - 'A'] = this.where['I' - 'A'];
    }

    /// <summary>The 25 letters, row by row.</summary>
    public string Letters { get; }

    /// <summary>The letter in a cell (rows and columns 0–4, wrapping).</summary>
    public char At(int row, int col) => this.Letters[(CodeBreaker.Letters.Mod(row, 5) * 5) + CodeBreaker.Letters.Mod(col, 5)];

    /// <summary>Where a letter sits (J is treated as I).</summary>
    public (int Row, int Col) Find(char letter) => this.where[CodeBreaker.Letters.ToNumber(letter)];

    /// <summary>The grid as five lines of five letters.</summary>
    public override string ToString() =>
        string.Join(Environment.NewLine, Enumerable.Range(0, 5).Select(r => string.Join(" ", this.Letters.Substring(r * 5, 5).ToCharArray())));

    /// <summary>The letters of a text with J folded into I.</summary>
    internal static string Fold(string text) => CodeBreaker.Letters.Normalise(text).Replace('J', 'I');
}

/// <summary>
/// The Polybius square: each letter becomes its row and column number in a 5 × 5 grid, so H in the
/// plain grid is "23". Described by the Greek historian Polybius for signalling with torches.
/// </summary>
/// <remarks>
/// On its own it is just a simple substitution with two-digit symbols — replace each pair of digits
/// with a letter and every substitution attack applies. Its value is as a building block: Bifid and
/// ADFGVX split the coordinates apart and that is where the strength comes from.
/// </remarks>
public sealed class PolybiusCipher : ICipher
{
    /// <summary>Create a Polybius square cipher with a keyword grid.</summary>
    public PolybiusCipher(string keyword = "")
    {
        this.Grid = new PolybiusGrid(keyword);
    }

    /// <summary>The grid.</summary>
    public PolybiusGrid Grid { get; }

    /// <inheritdoc/>
    public string Name => "Polybius square";

    /// <inheritdoc/>
    public string KeyDescription => this.Grid.Letters;

    /// <summary>25! grids.</summary>
    public static KeySpace KeySpace { get; } = KeySpace.Factorial(25, "25! grids");

    /// <inheritdoc/>
    public string Encrypt(string plaintext)
    {
        var sb = new StringBuilder();
        foreach (char c in PolybiusGrid.Fold(plaintext))
        {
            (int r, int col) = this.Grid.Find(c);
            if (sb.Length > 0)
            {
                sb.Append(' ');
            }

            sb.Append((char)('1' + r)).Append((char)('1' + col));
        }

        return sb.ToString();
    }

    /// <inheritdoc/>
    public string Decrypt(string ciphertext)
    {
        string digits = new(ciphertext.Where(ch => ch >= '1' && ch <= '5').ToArray());
        var sb = new StringBuilder();
        for (int i = 0; i + 1 < digits.Length; i += 2)
        {
            sb.Append(this.Grid.At(digits[i] - '1', digits[i + 1] - '1'));
        }

        return sb.ToString();
    }
}

/// <summary>
/// The Playfair cipher: encrypt <i>pairs</i> of letters using a 5 × 5 keyword grid. Invented by
/// Charles Wheatstone in 1854, promoted by Lord Playfair, and used by the British in the Boer War and
/// the First World War, and by Australian coastwatchers in the Second.
/// </summary>
/// <remarks>
/// <para>The rules for a pair:</para>
/// <list type="bullet">
/// <item>Same row: take the letters to the right of each (wrapping).</item>
/// <item>Same column: take the letters below each (wrapping).</item>
/// <item>Otherwise: the two letters mark opposite corners of a rectangle; take the other two corners,
/// each staying in its own row.</item>
/// </list>
/// <para>
/// Before encrypting, the message is split into pairs; a pair of identical letters is split with an X
/// (so BALLOON becomes BA LX LO ON), and an odd final letter gets an X too. Working on pairs means
/// 600 symbols instead of 26, so single-letter frequency analysis fails — but pair frequencies still
/// show (TH, HE, IN…) and the grid has structure a search can climb. See
/// <see cref="Attacks.PlayfairAttack"/>.
/// </para>
/// </remarks>
public sealed class PlayfairCipher : ICipher
{
    /// <summary>Create a Playfair cipher from a keyword.</summary>
    public PlayfairCipher(string keyword)
    {
        this.Grid = new PolybiusGrid(keyword);
    }

    private PlayfairCipher(PolybiusGrid grid)
    {
        this.Grid = grid;
    }

    /// <summary>The grid.</summary>
    public PolybiusGrid Grid { get; }

    /// <summary>
    /// 25! orderings of the grid — but each grid can be rotated through its 5 rows and 5 columns
    /// (cyclically) without changing the cipher, so there are 25!/25 = 24! genuinely different keys,
    /// about 6.2 × 10²³ (79 bits).
    /// </summary>
    public static KeySpace KeySpace { get; } = KeySpace.Factorial(24, "25! grids ÷ 25 equivalent rotations = 24!");

    /// <inheritdoc/>
    public string Name => "Playfair";

    /// <inheritdoc/>
    public string KeyDescription => this.Grid.Letters;

    /// <summary>A Playfair cipher from a full 25-letter grid (as the attack produces).</summary>
    public static PlayfairCipher FromGrid(string letters25) => new(new PolybiusGrid(letters25));

    /// <summary>
    /// Prepare a plaintext the Playfair way: J→I, split doubled letters with X (Q if the letter is X),
    /// pad an odd length with X.
    /// </summary>
    public static string Prepare(string plaintext)
    {
        string p = PolybiusGrid.Fold(plaintext);
        var sb = new StringBuilder();
        for (int i = 0; i < p.Length;)
        {
            char a = p[i];
            char b = i + 1 < p.Length ? p[i + 1] : '\0';
            if (b == '\0' || b == a)
            {
                sb.Append(a).Append(a == 'X' ? 'Q' : 'X');
                i++;
            }
            else
            {
                sb.Append(a).Append(b);
                i += 2;
            }
        }

        return sb.ToString();
    }

    /// <inheritdoc/>
    public string Encrypt(string plaintext) => this.Transform(Prepare(plaintext), 1);

    /// <inheritdoc/>
    public string Decrypt(string ciphertext)
    {
        string c = PolybiusGrid.Fold(ciphertext);
        if (c.Length % 2 == 1)
        {
            c += "X";
        }

        return this.Transform(c, -1);
    }

    private string Transform(string text, int direction)
    {
        var sb = new StringBuilder(text.Length);
        for (int i = 0; i + 1 < text.Length; i += 2)
        {
            (int r1, int c1) = this.Grid.Find(text[i]);
            (int r2, int c2) = this.Grid.Find(text[i + 1]);
            if (r1 == r2)
            {
                sb.Append(this.Grid.At(r1, c1 + direction)).Append(this.Grid.At(r2, c2 + direction));
            }
            else if (c1 == c2)
            {
                sb.Append(this.Grid.At(r1 + direction, c1)).Append(this.Grid.At(r2 + direction, c2));
            }
            else
            {
                sb.Append(this.Grid.At(r1, c2)).Append(this.Grid.At(r2, c1));
            }
        }

        return sb.ToString();
    }
}

/// <summary>
/// The Bifid cipher (Félix Delastelle, 1895): write each letter's row and column from a Polybius grid
/// one above the other, read the digits off row-wise in blocks, and turn them back into letters. Each
/// ciphertext letter then depends on <i>two</i> plaintext letters — "fractionation" — which smears the
/// statistics far more than a plain substitution.
/// </summary>
public sealed class BifidCipher : ICipher
{
    /// <summary>Create a Bifid cipher.</summary>
    /// <param name="keyword">The grid keyword.</param>
    /// <param name="period">Block length (0 = the whole message as one block).</param>
    public BifidCipher(string keyword, int period = 5)
    {
        this.Grid = new PolybiusGrid(keyword);
        this.Period = Math.Max(0, period);
    }

    /// <summary>The grid.</summary>
    public PolybiusGrid Grid { get; }

    /// <summary>The block length.</summary>
    public int Period { get; }

    /// <inheritdoc/>
    public string Name => "Bifid";

    /// <inheritdoc/>
    public string KeyDescription => $"{this.Grid.Letters}, period {this.Period}";

    /// <inheritdoc/>
    public string Encrypt(string plaintext)
    {
        string p = PolybiusGrid.Fold(plaintext);
        var sb = new StringBuilder(p.Length);
        foreach (string block in Blocks(p, this.Period))
        {
            var rows = block.Select(ch => this.Grid.Find(ch).Row).ToList();
            var cols = block.Select(ch => this.Grid.Find(ch).Col).ToList();
            List<int> stream = [.. rows, .. cols];
            for (int i = 0; i < stream.Count; i += 2)
            {
                sb.Append(this.Grid.At(stream[i], stream[i + 1]));
            }
        }

        return sb.ToString();
    }

    /// <inheritdoc/>
    public string Decrypt(string ciphertext)
    {
        string c = PolybiusGrid.Fold(ciphertext);
        var sb = new StringBuilder(c.Length);
        foreach (string block in Blocks(c, this.Period))
        {
            var stream = new List<int>();
            foreach (char ch in block)
            {
                (int r, int col) = this.Grid.Find(ch);
                stream.Add(r);
                stream.Add(col);
            }

            int n = block.Length;
            for (int i = 0; i < n; i++)
            {
                sb.Append(this.Grid.At(stream[i], stream[n + i]));
            }
        }

        return sb.ToString();
    }

    private static IEnumerable<string> Blocks(string text, int period)
    {
        if (period <= 0)
        {
            yield return text;
            yield break;
        }

        for (int i = 0; i < text.Length; i += period)
        {
            yield return text.Substring(i, Math.Min(period, text.Length - i));
        }
    }
}

/// <summary>
/// The Hill cipher (Lester Hill, 1929): treat each block of n letters as a vector of numbers and
/// multiply it by an n × n key matrix, modulo 26. The first cipher built squarely on linear algebra.
/// </summary>
/// <remarks>
/// <para>
/// With the 2 × 2 key [[3, 3], [2, 5]], the pair "HE" = (7, 4) becomes (3·7 + 3·4, 2·7 + 5·4) =
/// (33, 34) ≡ (7, 8) = "HI". Decryption multiplies by the inverse matrix modulo 26, which exists only
/// when the determinant shares no factor with 26.
/// </para>
/// <para>
/// Being linear is its downfall. Know n pairs of plaintext and ciphertext blocks and the key is the
/// solution of a set of simultaneous equations — a known-plaintext attack needs only a handful of
/// letters. And each <i>row</i> of the matrix produces one letter of each block independently, so even
/// without a crib the rows can be searched one at a time: 2 × 26² instead of 26⁴ for a 2 × 2 key.
/// </para>
/// </remarks>
public sealed class HillCipher : ICipher
{
    private readonly int[,] key;
    private readonly int[,] inverse;

    /// <summary>Create a Hill cipher from a square key matrix that is invertible modulo 26.</summary>
    public HillCipher(int[,] key)
    {
        int n = key.GetLength(0);
        if (n != key.GetLength(1) || n < 1)
        {
            throw new ArgumentException("The Hill key must be a square matrix.", nameof(key));
        }

        this.key = (int[,])key.Clone();
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                this.key[i, j] = Letters.Mod(this.key[i, j], 26);
            }
        }

        this.inverse = NumberTheory.MatrixInverse(this.key, 26)
            ?? throw new ArgumentException("That matrix has no inverse modulo 26 (its determinant shares a factor with 26), so messages could never be decrypted.", nameof(key));
    }

    /// <summary>The block size n.</summary>
    public int Size => this.key.GetLength(0);

    /// <summary>A copy of the key matrix.</summary>
    public int[,] Key => (int[,])this.key.Clone();

    /// <summary>A copy of the decryption (inverse) matrix.</summary>
    public int[,] InverseKey => (int[,])this.inverse.Clone();

    /// <inheritdoc/>
    public string Name => $"Hill ({this.Size}×{this.Size})";

    /// <inheritdoc/>
    public string KeyDescription => FormatMatrix(this.key);

    /// <summary>
    /// The number of invertible 2 × 2 matrices modulo 26: 157,248 of the 456,976 (26⁴) possible —
    /// only about a third can be keys.
    /// </summary>
    public static KeySpace KeySpace2x2 { get; } = KeySpace.Of(157_248, "invertible 2×2 matrices mod 26");

    /// <summary>Is a matrix usable as a Hill key (invertible modulo 26)?</summary>
    public static bool IsValidKey(int[,] matrix) => NumberTheory.MatrixInverse(matrix, 26) is not null;

    /// <summary>Write a matrix as [[a, b], [c, d]].</summary>
    public static string FormatMatrix(int[,] m)
    {
        int n = m.GetLength(0);
        return "[" + string.Join(", ", Enumerable.Range(0, n).Select(i => "[" + string.Join(", ", Enumerable.Range(0, n).Select(j => m[i, j])) + "]")) + "]";
    }

    /// <inheritdoc/>
    public string Encrypt(string plaintext) => Multiply(this.key, Pad(plaintext, this.Size));

    /// <inheritdoc/>
    public string Decrypt(string ciphertext) => Multiply(this.inverse, Pad(ciphertext, this.Size));

    /// <summary>Multiply each block of letters by a matrix, modulo 26.</summary>
    internal static string Multiply(int[,] m, int[] letters)
    {
        int n = m.GetLength(0);
        var result = new int[letters.Length];
        for (int b = 0; b + n <= letters.Length; b += n)
        {
            for (int i = 0; i < n; i++)
            {
                int sum = 0;
                for (int j = 0; j < n; j++)
                {
                    sum += m[i, j] * letters[b + j];
                }

                result[b + i] = sum % 26;
            }
        }

        return Letters.FromNumbers(result);
    }

    private static int[] Pad(string text, int n)
    {
        var numbers = Letters.ToNumbers(text).ToList();
        while (numbers.Count % n != 0)
        {
            numbers.Add('X' - 'A');
        }

        return numbers.ToArray();
    }
}

/// <summary>
/// ADFGVX (Fritz Nebel, German army, 1918): a 6 × 6 Polybius square of letters and digits labelled
/// A, D, F, G, V, X — chosen because they sound unlike in Morse — followed by a columnar
/// transposition of the resulting letter pairs.
/// </summary>
/// <remarks>
/// Substitution then transposition, so each ciphertext letter is half a plaintext symbol and has been
/// moved away from its partner. The French cryptanalyst Georges Painvin broke it in June 1918 —
/// working himself to exhaustion and losing 15 kg — from messages that happened to share keys, in
/// time to warn of the German offensive at Compiègne. Still hard for a computer without such luck.
/// </remarks>
public sealed class AdfgvxCipher : ICipher
{
    /// <summary>The six coordinate letters.</summary>
    public const string Labels = "ADFGVX";

    private const string Symbols36 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private readonly ColumnarTranspositionCipher transposition;

    /// <summary>Create an ADFGVX cipher.</summary>
    /// <param name="squareKeyword">Keyword for the 6 × 6 square of A–Z and 0–9.</param>
    /// <param name="transpositionKeyword">Keyword for the columnar transposition.</param>
    public AdfgvxCipher(string squareKeyword, string transpositionKeyword)
    {
        var sb = new StringBuilder();
        foreach (char c in squareKeyword.ToUpperInvariant() + Symbols36)
        {
            if (Symbols36.IndexOf(c) >= 0 && sb.ToString().IndexOf(c) < 0)
            {
                sb.Append(c);
            }
        }

        this.Square = sb.ToString();
        this.TranspositionKeyword = Letters.Normalise(transpositionKeyword);
        this.transposition = new ColumnarTranspositionCipher(this.TranspositionKeyword);
    }

    /// <summary>The 36 symbols of the square, row by row.</summary>
    public string Square { get; }

    /// <summary>The transposition keyword.</summary>
    public string TranspositionKeyword { get; }

    /// <inheritdoc/>
    public string Name => "ADFGVX";

    /// <inheritdoc/>
    public string KeyDescription => $"square {this.Square}, transposition {this.TranspositionKeyword}";

    /// <inheritdoc/>
    public string Encrypt(string plaintext)
    {
        var sb = new StringBuilder();
        foreach (char ch in plaintext.ToUpperInvariant())
        {
            int i = this.Square.IndexOf(ch);
            if (i >= 0 && char.IsLetterOrDigit(ch))
            {
                sb.Append(Labels[i / 6]).Append(Labels[i % 6]);
            }
        }

        return this.transposition.Encrypt(sb.ToString());
    }

    /// <inheritdoc/>
    public string Decrypt(string ciphertext)
    {
        string pairs = this.transposition.Decrypt(new string(ciphertext.ToUpperInvariant().Where(c => Labels.IndexOf(c) >= 0).ToArray()));
        var sb = new StringBuilder();
        for (int i = 0; i + 1 < pairs.Length; i += 2)
        {
            sb.Append(this.Square[(Labels.IndexOf(pairs[i]) * 6) + Labels.IndexOf(pairs[i + 1])]);
        }

        return sb.ToString();
    }
}
