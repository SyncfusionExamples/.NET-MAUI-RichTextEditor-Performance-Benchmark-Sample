using System.Text;

namespace SfRTEPerformance
{
    /// <summary>
    /// Builds the HTML documents used by the benchmarks. Every document is a
    /// plain string the sample can assign to <c>SfRichTextEditor.Value</c>.
    /// </summary>
    public static class HtmlGenerator
    {
        // The word-count generator splits its words into fixed-size paragraphs
        // so the produced HTML looks like real prose rather than one giant run-on line.
        private const int WordsPerParagraph = 50;

        // A short list of Latin-style filler words, cycled to fill any word count.
        // ~60 entries is more than enough — the cycle repeats every paragraph.
        private static readonly string[] WordBank =
        {
            "lorem", "ipsum", "dolor", "sit", "amet", "consectetur", "adipiscing",
            "elit", "sed", "do", "eiusmod", "tempor", "incididunt", "ut", "labore",
            "et", "dolore", "magna", "aliqua", "enim", "ad", "minim", "veniam",
            "quis", "nostrud", "exercitation", "ullamco", "laboris", "nisi", "aliquip",
            "ex", "ea", "commodo", "consequat", "duis", "aute", "irure", "reprehenderit",
            "voluptate", "velit", "esse", "cillum", "fugiat", "nulla", "pariatur",
            "excepteur", "sint", "occaecat", "cupidatat", "non", "proident", "sunt",
            "culpa", "qui", "officia", "deserunt", "mollit", "anim", "id", "est", "laborum"
        };

        /// <summary>
        /// Builds the unique plain-text marker that every generated document
        /// embeds. Plain text (no markup characters) so it survives the
        /// editor's HTML-to-text projection unchanged.
        /// </summary>
        public static string Marker(string id) => $"BENCHMARK-MARKER-{id}";

        /// <summary>
        /// Generates an HTML document containing approximately
        /// <paramref name="wordCount"/> words, split into paragraphs of
        /// <see cref="WordsPerParagraph"/> words each.
        /// </summary>
        /// <param name="wordCount">The number of words to emit.</param>
        /// <param name="marker">
        /// A plain-text marker to embed as its own paragraph. Pass
        /// <c>null</c> to omit it.
        /// </param>
        public static string GenerateWordsDocument(int wordCount, string? marker = null)
        {
            var sb = new StringBuilder(wordCount * 6);
            int written = 0;

            // The marker goes as the FIRST paragraph so the probe can detect
            // the moment the editor starts rendering text, without having to
            // wait for the whole document to be committed.
            if (!string.IsNullOrEmpty(marker))
            {
                sb.Append("<p>").Append(marker).Append("</p>");
            }

            while (written < wordCount)
            {
                sb.Append("<p>");
                for (int w = 0; w < WordsPerParagraph && written < wordCount; w++, written++)
                {
                    if (w > 0)
                    {
                        sb.Append(' ');
                    }
                    sb.Append(WordBank[written % WordBank.Length]);
                }
                // Trailing space INSIDE the paragraph so a tag-stripping text
                // projection (e.g. "HtmlGenerator.GetText()") still sees a
                // word boundary between the last word of this paragraph and
                // the first word of the next.
                sb.Append(" </p>");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Generates an HTML document containing exactly
        /// <paramref name="paragraphCount"/> <c>&lt;p&gt;</c> elements.
        /// Used by the "10,000 Paragraphs" benchmark.
        /// </summary>
        public static string GenerateParagraphsDocument(int paragraphCount, string? marker = null)
        {
            var sb = new StringBuilder(paragraphCount * 40);

            // The marker goes as the FIRST paragraph so the probe can detect
            // the moment the editor starts rendering text, without having to
            // wait for the whole document to be committed.
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