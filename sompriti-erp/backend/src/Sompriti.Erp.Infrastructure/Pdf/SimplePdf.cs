using System.Globalization;
using System.Text;

namespace Sompriti.Erp.Infrastructure.Pdf;

/// <summary>
/// Tiny dependency-free PDF writer: A4 pages, Helvetica / Helvetica-Bold, text, lines, filled rectangles and a rotated watermark.
/// Coordinates use a top-left origin in points (1/72 inch). Text is encoded as WinAnsi (Latin-1).
/// </summary>
public sealed class SimplePdf
{
    public const float PageWidth = 595.28f;
    public const float PageHeight = 841.89f;

    public enum Align { Left, Right, Center }

    private readonly List<StringBuilder> _pages = new();
    private StringBuilder Current => _pages[^1];
    public int PageCount => _pages.Count;

    public SimplePdf() => NewPage();

    public void NewPage() => _pages.Add(new StringBuilder());

    /// <summary>Selects a page for drawing (0-based), e.g. to add footers after all pages exist.</summary>
    public void DrawOnPage(int index, Action<SimplePdf> draw)
    {
        var page = _pages[index];
        _pages.RemoveAt(index);
        _pages.Add(page);
        draw(this);
        _pages.RemoveAt(_pages.Count - 1);
        _pages.Insert(index, page);
    }

    public void Text(float x, float top, string text, float size = 9, bool bold = false, Align align = Align.Left, float gray = 0)
    {
        if (string.IsNullOrEmpty(text)) return;
        var width = Measure(text, size, bold);
        var left = align switch { Align.Right => x - width, Align.Center => x - width / 2, _ => x };
        var baseline = PageHeight - top - size * 0.8f;
        Current.Append(CultureInfo.InvariantCulture,
            $"BT {F(gray)} g /{(bold ? "F2" : "F1")} {F(size)} Tf {F(left)} {F(baseline)} Td ({Escape(text)}) Tj ET\n");
    }

    public void Line(float x1, float top1, float x2, float top2, float width = 0.5f, float gray = 0)
    {
        Current.Append(CultureInfo.InvariantCulture,
            $"{F(gray)} G {F(width)} w {F(x1)} {F(PageHeight - top1)} m {F(x2)} {F(PageHeight - top2)} l S\n");
    }

    public void FillRect(float x, float top, float width, float height, float gray)
    {
        Current.Append(CultureInfo.InvariantCulture,
            $"{F(gray)} g {F(x)} {F(PageHeight - top - height)} {F(width)} {F(height)} re f 0 g\n");
    }

    public void StrokeRect(float x, float top, float width, float height, float lineWidth = 0.5f, float gray = 0)
    {
        Current.Append(CultureInfo.InvariantCulture,
            $"{F(gray)} G {F(lineWidth)} w {F(x)} {F(PageHeight - top - height)} {F(width)} {F(height)} re S\n");
    }

    /// <summary>Large diagonal text across the page centre (drawn in light gray).</summary>
    public void Watermark(string text, float size = 110, float gray = 0.88f)
    {
        var width = Measure(text, size, true);
        const double angle = Math.PI / 4;
        var cos = (float)Math.Cos(angle);
        var sin = (float)Math.Sin(angle);
        // start so that the text centre lands on the page centre
        var cx = PageWidth / 2 - (width / 2) * cos + (size * 0.35f) * sin;
        var cy = PageHeight / 2 - (width / 2) * sin - (size * 0.35f) * cos;
        Current.Append(CultureInfo.InvariantCulture,
            $"BT {F(gray)} g /F2 {F(size)} Tf {F(cos)} {F(sin)} {F(-sin)} {F(cos)} {F(cx)} {F(cy)} Tm ({Escape(text)}) Tj ET 0 g\n");
    }

    /// <summary>Cuts text with "..." so it fits the given width.</summary>
    public static string Fit(string text, float maxWidth, float size, bool bold = false)
    {
        if (Measure(text, size, bold) <= maxWidth) return text;
        const string ellipsis = "...";
        var s = text;
        while (s.Length > 0 && Measure(s + ellipsis, size, bold) > maxWidth) s = s[..^1];
        return s + ellipsis;
    }

    /// <summary>Splits text into lines that fit the width (word wrap).</summary>
    public static List<string> Wrap(string text, float maxWidth, float size, bool bold = false)
    {
        var lines = new List<string>();
        foreach (var paragraph in text.Replace("\r", "").Split('\n'))
        {
            var current = "";
            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = current.Length == 0 ? word : current + " " + word;
                if (Measure(candidate, size, bold) <= maxWidth) { current = candidate; continue; }
                if (current.Length > 0) lines.Add(current);
                current = Measure(word, size, bold) <= maxWidth ? word : Fit(word, maxWidth, size, bold);
            }
            lines.Add(current);
        }
        return lines;
    }

    public static float Measure(string text, float size, bool bold)
    {
        var widths = bold ? BoldWidths : RegularWidths;
        var units = 0;
        foreach (var ch in text)
        {
            int c = ToWinAnsi(ch);
            units += c is >= 32 and <= 126 ? widths[c - 32] : 556;
        }
        return units * size / 1000f;
    }

    public byte[] Build()
    {
        var objects = new List<string>();
        // 1: catalog, 2: pages, 3: F1, 4: F2, then (page, content) pairs
        var pageObjectIds = new List<int>();
        var nextId = 5;
        var bodies = new Dictionary<int, byte[]>();

        foreach (var page in _pages)
        {
            var pageId = nextId++;
            var contentId = nextId++;
            pageObjectIds.Add(pageId);
            var content = Latin1.GetBytes(page.ToString());
            bodies[pageId] = Latin1.GetBytes(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {F(PageWidth)} {F(PageHeight)}] " +
                $"/Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {contentId} 0 R >>");
            var header = Latin1.GetBytes($"<< /Length {content.Length} >>\nstream\n");
            var footer = Latin1.GetBytes("\nendstream");
            bodies[contentId] = [.. header, .. content, .. footer];
        }

        bodies[1] = Latin1.GetBytes("<< /Type /Catalog /Pages 2 0 R >>");
        bodies[2] = Latin1.GetBytes($"<< /Type /Pages /Kids [{string.Join(" ", pageObjectIds.Select(id => $"{id} 0 R"))}] /Count {pageObjectIds.Count} >>");
        bodies[3] = Latin1.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        bodies[4] = Latin1.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");

        using var ms = new MemoryStream();
        void Write(string s) { var b = Latin1.GetBytes(s); ms.Write(b, 0, b.Length); }

        Write("%PDF-1.4\n%âãÏÓ\n");
        var offsets = new long[nextId];
        for (var id = 1; id < nextId; id++)
        {
            offsets[id] = ms.Position;
            Write($"{id} 0 obj\n");
            ms.Write(bodies[id]);
            Write("\nendobj\n");
        }
        var xref = ms.Position;
        Write($"xref\n0 {nextId}\n0000000000 65535 f \n");
        for (var id = 1; id < nextId; id++) Write($"{offsets[id]:D10} 00000 n \n");
        Write($"trailer\n<< /Size {nextId} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return ms.ToArray();
    }

    // ------------------------------------------------------------------ internals

    private static readonly Encoding Latin1 = Encoding.Latin1;

    private static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    private static char ToWinAnsi(char c) => c switch
    {
        '‘' or '’' => '\'',
        '“' or '”' => '"',
        '–' or '—' => '-',
        '৳' => '?', // Bengali Taka sign is not in the standard fonts; callers use "Tk"
        _ => c
    };

    private static string Escape(string text)
    {
        var sb = new StringBuilder(text.Length + 8);
        foreach (var raw in text)
        {
            var c = ToWinAnsi(raw);
            if (c > 255) c = '?';
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '(': sb.Append("\\("); break;
                case ')': sb.Append("\\)"); break;
                case '\n' or '\r' or '\t': sb.Append(' '); break;
                default:
                    if (c < 32) sb.Append(' ');
                    else if (c > 126) sb.Append('\\').Append(Convert.ToString(c, 8).PadLeft(3, '0'));
                    else sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    // Standard Helvetica AFM widths for characters 32..126
    private static readonly int[] RegularWidths =
    [
        278, 278, 355, 556, 556, 889, 667, 191, 333, 333, 389, 584, 278, 333, 278, 278,
        556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 278, 278, 584, 584, 584, 556,
        1015, 667, 667, 722, 722, 667, 611, 778, 722, 278, 500, 667, 556, 833, 722, 778,
        667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 278, 278, 278, 469, 556,
        333, 556, 556, 500, 556, 556, 278, 556, 556, 222, 222, 500, 222, 833, 556, 556,
        556, 556, 333, 500, 278, 556, 500, 722, 500, 500, 500, 334, 260, 334, 584
    ];

    // Standard Helvetica-Bold AFM widths for characters 32..126
    private static readonly int[] BoldWidths =
    [
        278, 333, 474, 556, 556, 889, 722, 238, 333, 333, 389, 584, 278, 333, 278, 278,
        556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 333, 333, 584, 584, 584, 611,
        975, 722, 722, 722, 722, 667, 611, 778, 722, 278, 556, 722, 611, 833, 722, 778,
        667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 333, 278, 333, 584, 556,
        333, 556, 611, 556, 611, 556, 333, 611, 611, 278, 278, 556, 278, 889, 611, 611,
        611, 611, 389, 556, 333, 611, 556, 778, 556, 556, 500, 389, 280, 389, 584
    ];
}
