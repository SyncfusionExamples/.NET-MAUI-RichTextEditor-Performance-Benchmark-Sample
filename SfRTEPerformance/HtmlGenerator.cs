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
        /// Builds the unique, plain-text marker that is embedded in every
        /// generated document.
        /// </summary>
        /// <param name="id">A caller-supplied identifier, e.g. "500KB".</param>
        /// <returns>
        /// A marker such as <c>BENCHMARK-MARKER-500KB</c>. It contains no HTML
        /// markup, so it survives the editor's HTML-to-plain-text projection
        /// unchanged and can be matched against <c>SfRichTextEditor.GetText()</c>.
        /// </returns>
        public static string Marker(string id) => $"BENCHMARK-MARKER-{id}";

        /// <summary>
        /// Generates an HTML document that is at least <paramref name="targetBytes"/>
        /// bytes long by repeating the <see cref="BlockTemplate"/>.
        /// </summary>
        /// <param name="targetBytes">The target document size in bytes.</param>
        /// <param name="marker">
        /// The plain-text marker to embed in the document. Pass
        /// <c>null</c> to omit it.
        /// </param>
        /// <returns>The generated HTML.</returns>
        public static string GenerateHtmlBySize(int targetBytes, string? marker = null)
        {
            var sb = new StringBuilder(targetBytes + 1024);

            int i = 0;
            // The marker goes at the very END of the document. The content
            // probe waits for this exact string to appear in the editor's
            // text, and because it sits after every other character,
            // observing it proves the WHOLE document was committed - not
            // just the first part of it. If the editor ever streamed content
            // in incrementally, this ordering would still hold.
            if (!string.IsNullOrEmpty(marker))
            {
                sb.Append("<p>").Append(marker).Append("</p>");
            }
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
        /// <param name="marker">Optional plain-text marker to embed.</param>
        public static string GenerateSmallDocument(string? marker = null) =>
            GenerateHtmlBySize(10 * 1024, marker);

        /// <summary>~100 KB HTML document.</summary>
        /// <param name="marker">Optional plain-text marker to embed.</param>
        public static string GenerateMediumDocument(string? marker = null) =>
            GenerateHtmlBySize(100 * 1024, marker);

        /// <summary>~500 KB HTML document.</summary>
        /// <param name="marker">Optional plain-text marker to embed.</param>
        public static string GenerateLargeDocument(string? marker = null) =>
            GenerateHtmlBySize(500 * 1024, marker);

        /// <summary>~1 MB HTML document.</summary>
        /// <param name="marker">Optional plain-text marker to embed.</param>
        public static string GenerateExtraLargeDocument(string? marker = null) =>
            GenerateHtmlBySize(1024 * 1024, marker);

        /// <summary>
        /// Generates an HTML document containing exactly <paramref name="paragraphCount"/>
        /// <c>&lt;p&gt;</c> elements. Used by the "10,000 paragraph" benchmark.
        /// </summary>
        /// <param name="paragraphCount">The number of paragraphs to emit.</param>
        /// <param name="marker">
        /// Optional plain-text marker to embed in the first paragraph.
        /// </param>
        public static string GenerateParagraphsDocument(int paragraphCount, string? marker = null)
        {
            var sb = new StringBuilder(paragraphCount * 32);
            // The marker goes after every paragraph, so observing it proves
            // the whole document was committed rather than just its head.
            if (!string.IsNullOrEmpty(marker))
            {
                sb.Append("<p>").Append(marker).Append("</p>");
            }
            for (int i = 1; i <= paragraphCount; i++)
            {
                sb.Append("<p>Paragraph ").Append(i).Append("</p>");
            }

            

            return sb.ToString();
        }
    }
}
