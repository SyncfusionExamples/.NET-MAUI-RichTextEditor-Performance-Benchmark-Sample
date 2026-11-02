using System.Diagnostics;
using Syncfusion.Maui.RichTextEditor;

namespace SfRTEPerformance
{
    public partial class BenchmarkEditorPage : ContentPage
    {
        // HTML assigned to the editor BEFORE the page is pushed onto the
        // navigation stack. Pre-assigning in the constructor lets the
        // caller's stopwatch capture the assignment itself.
        private readonly string? _initialHtml;

        // Plain-text marker embedded as the FIRST paragraph of the assigned
        // HTML. The benchmark stops when this marker appears in the editor's
        // text — i.e. when the editor has rendered its first text.
        private readonly string? _initialMarker;

        public BenchmarkEditorPage(
            string? initialHtml = null,
            string? marker = null,
            string? bannerMessage = null,
            bool isDynamicValueMode = false)
        {
            InitializeComponent();

            _initialHtml = initialHtml;
            _initialMarker = marker;

            if (_initialHtml != null)
            {
                richTextEditor.Value = _initialHtml;
            }

            ResultLabel.Text = bannerMessage ?? string.Empty;

            // The three Dynamic Value Update buttons live on this page only
            // when the editor was opened in Dynamic Value mode. Hidden for
            // Initial Load scenarios so they don't distract the user.
            DynamicButtonRow.IsVisible = isDynamicValueMode;
        }

        /// <summary>
        /// Waits until the editor has rendered its first text after the
        /// document assigned in the constructor, then stops the supplied
        /// stopwatch and reports the elapsed time as a benchmark line.
        /// </summary>
        public async Task WaitForContentAsync(Stopwatch stopwatch, string label)
        {
            if (string.IsNullOrEmpty(_initialMarker))
            {
                // Empty editor: nothing to poll for. Stop immediately so the
                // measurement is just the cost of constructing the page.
                stopwatch.Stop();
                PublishResult($"{label}: {BenchmarkFormat.Milliseconds(stopwatch)} ms");
                return;
            }

            bool acknowledged = await ContentProbe.WaitForContentAsync(
                richTextEditor, _initialMarker, stopwatch);

            string line = acknowledged
                ? $"{label}: {BenchmarkFormat.Milliseconds(stopwatch)} ms"
                : $"{label}: timed out after {ContentProbe.TimeoutSeconds} s";

            PublishResult(line);
        }

        /// <summary>
        /// Reassigns <see cref="SfRichTextEditor.Value"/> to <paramref name="html"/>
        /// on the already-loaded editor and measures the time until the new
        /// document renders its first text.
        /// </summary>
        public async Task MeasureValueUpdateAsync(
            string label, string html, string marker)
        {
            var sw = Stopwatch.StartNew();
            richTextEditor.Value = html;

            bool acknowledged = await ContentProbe.WaitForContentAsync(
                richTextEditor, marker, sw);

            string line = acknowledged
                ? $"{label} Value Update: {BenchmarkFormat.Milliseconds(sw)} ms"
                : $"{label} Value Update: timed out after {ContentProbe.TimeoutSeconds} s";

            PublishResult(line);
        }

        // ---- Dynamic Value Update buttons (only visible when the editor
        //      was opened in Dynamic Value mode) ----
        // Each click reassigns Value on the same editor instance and reports
        // a single measurement. The user can tap the three buttons in any
        // order on the same loaded editor to compare timings across sizes.

        private void OnDynamicValue100Clicked(object? s, EventArgs e) =>
            _ = RunDynamicValueAsync("100 Words", 100);

        private void OnDynamicValue5KClicked(object? s, EventArgs e) =>
            _ = RunDynamicValueAsync("5,000 Words", 5_000);

        private void OnDynamicValue10KClicked(object? s, EventArgs e) =>
            _ = RunDynamicValueAsync("10,000 Words", 10_000);

        // Generates a fresh marker so the probe matches THIS reassignment
        // (never leftover content from a previous click), then measures the
        // single Value reassignment on the already-loaded editor.
        private async Task RunDynamicValueAsync(string label, int wordCount)
        {
            string marker = HtmlGenerator.Marker(
                $"Dyn-{label.Replace(",", "").Replace(" ", "")}-{Guid.NewGuid():N}");
            string html = HtmlGenerator.GenerateWordsDocument(wordCount, marker);
            await MeasureValueUpdateAsync(label, html, marker);
        }

        /// <summary>Updates the on-screen banner and forwards the line to the results sink.</summary>
        private void PublishResult(string line)
        {
            ResultLabel.Text = line;
            BenchmarkResultsSink.Publish(line);
        }
    }
}