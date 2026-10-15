using System.Text;

namespace SfRTEPerformance
{
    /// <summary>
    /// Generates HTML documents of varying sizes for the RichTextEditor benchmarks.
    /// Each method produces a self-contained HTML string that can be assigned to
    /// <c>SfRichTextEditor.Value</c>. The generated content includes a mix of
    /// headings, paragraphs, lists, hyperlinks, and basic text formatting.
    /// </summary>
    public static class HtmlGenerator
    {
        // A single repeating HTML block used to grow documents to a target size.
        // It is intentionally varied (heading, paragraph, list, link, formatting)
        // so the produced HTML exercises a realistic content mix.
        private const string BlockTemplate =
            "<h2>Section {n}</h2>" +
            "<p>This is paragraph <strong>number {n}</strong> with <em>italic</em>, " +
            "<u>underlined</u>, and <strike>strikethrough</strike> text. " +
            "It also references the Syncfusion <a href=\"https://www.syncfusion.com/maui-controls/rich-text-editor\">RichTextEditor documentation</a> " +
            "and includes inline <code>code</code> samples.</p>" +
            "<ul>" +
            "<li>List item {n}.1 — first bullet for section {n}.</li>" +
            "<li>List item {n}.2 — second bullet for section {n}.</li>" +
            "<li>List item {n}.3 — third bullet for section {n}.</li>" +
            "</ul>";

        /// <summary>
        /// Generates an HTML document that is at least <paramref name="targetBytes"/>
        /// bytes long by repeating the <see cref="BlockTemplate"/>.
        /// </summary>
        public static string GenerateHtmlBySize(int targetBytes)
        {
            var sb = new StringBuilder(targetBytes + 1024);

            int i = 0;
            while (sb.Length < targetBytes)
            {
                i++;
                sb.Append("<p>Paragraph ").Append(i).Append("</p>");
                if (i % 10 == 0)
                {
                    sb.Append(BlockTemplate.Replace("{n}", i.ToString()));
                }
            }

            return sb.ToString();
        }

        /// <summary>~10 KB HTML document.</summary>
        public static string GenerateSmallDocument() => GenerateHtmlBySize(10 * 1024);

        /// <summary>~100 KB HTML document.</summary>
        public static string GenerateMediumDocument() => GenerateHtmlBySize(100 * 1024);

        /// <summary>~500 KB HTML document.</summary>
        public static string GenerateLargeDocument() => GenerateHtmlBySize(500 * 1024);

        /// <summary>~1 MB HTML document.</summary>
        public static string GenerateExtraLargeDocument() => GenerateHtmlBySize(1024 * 1024);

        /// <summary>
        /// Generates an HTML document containing exactly <paramref name="paragraphCount"/>
        /// <c>&lt;p&gt;</c> elements. Used by the "10,000 paragraph" benchmark.
        /// </summary>
        public static string GenerateParagraphsDocument(int paragraphCount)
        {
            var sb = new StringBuilder(paragraphCount * 32);
            for (int i = 1; i <= paragraphCount; i++)
            {
                sb.Append("<p>Paragraph ").Append(i).Append("</p>");
            }
            return sb.ToString();
        }
    }
}
