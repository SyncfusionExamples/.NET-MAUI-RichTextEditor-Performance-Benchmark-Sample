using System.Diagnostics;
using Syncfusion.Maui.RichTextEditor;

namespace SfRTEPerformance
{
    public partial class BenchmarkFormatPage : ContentPage
    {
        // HTML seeded into the editor so the user has something to select.
        private const string SeedHtml =
            "<p>The <strong>quick brown fox</strong> jumps over the lazy dog. " +
            "Pack my box with five dozen liquor jugs. " +
            "How vexingly quick daft zebras jump.</p>" +
            "<p>Select a few words (or the whole text) and tap any of the format " +
            "buttons below. The time from your tap to the FormatChanged event " +
            "is measured and shown in the banner above.</p>";

        // Stopwatch used by the next format measurement. Set to a non-null
        // value while a measurement is "armed", cleared back to null once
        // it has been published so subsequent FormatChanged events are
        // ignored.
        private Stopwatch? _formatStopwatch;

        // True while a format measurement is armed. Both the synchronous
        // ApplyFormat completion path and the FormatChanged handler check
        // this flag to make sure only one of them publishes a line for the
        // same measurement.
        private bool _formatArmed;

        // Label of the format currently being measured, e.g. "Bold".
        private string _formatLabel = string.Empty;

        public BenchmarkFormatPage()
        {
            InitializeComponent();
            richTextEditor.Value = SeedHtml;
        }

        // ---- Character-format click handlers ----
        private void OnBoldClicked(object? s, EventArgs e) =>
            ApplyFormat("Bold", richTextEditor.ToggleBold);

        private void OnItalicClicked(object? s, EventArgs e) =>
            ApplyFormat("Italic", richTextEditor.ToggleItalic);

        private void OnUnderlineClicked(object? s, EventArgs e) =>
            ApplyFormat("Underline", richTextEditor.ToggleUnderline);

        private void OnStrikethroughClicked(object? s, EventArgs e) =>
            ApplyFormat("Strikethrough", richTextEditor.ToggleStrikethrough);

        // ---- Paragraph-format click handlers ----
        private void OnBulletsClicked(object? s, EventArgs e) =>
            ApplyFormat("Bullets", richTextEditor.ToggleBulletList);

        private void OnNumbersClicked(object? s, EventArgs e) =>
            ApplyFormat("Numbers", richTextEditor.ToggleNumberedList);

        private void OnAlignLeftClicked(object? s, EventArgs e) =>
            ApplyFormat("Align Left", richTextEditor.AlignLeft);

        private void OnAlignCenterClicked(object? s, EventArgs e) =>
            ApplyFormat("Align Center", richTextEditor.AlignCenter);

        private void OnAlignRightClicked(object? s, EventArgs e) =>
            ApplyFormat("Align Right", richTextEditor.AlignRight);

        private void OnAlignJustifyClicked(object? s, EventArgs e) =>
            ApplyFormat("Justify", richTextEditor.AlignJustify);

        // ---- Load Large Doc ----
        // Assigns a sizeable paragraph document to the editor so the user can
        // apply formatting on a realistically-sized body. The assignment is
        // timed and reported. FormatChanged events raised internally during
        // the load are ignored — the load is not a format-button click.
        private async void OnLoadLargeDocClicked(object? s, EventArgs e)
        {
            // Clear any in-flight format measurement so the load's internal
            // FormatChanged events are not recorded as format-button clicks.
            _formatStopwatch = null;
            _formatArmed = false;
            _formatLabel = string.Empty;

            string marker = HtmlGenerator.Marker($"1K-{Guid.NewGuid():N}");
            string html = HtmlGenerator.GenerateParagraphsDocument(1_000, marker);

            var sw = Stopwatch.StartNew();
            richTextEditor.Value = html;

            bool acknowledged = await ContentProbe.WaitForContentAsync(
                richTextEditor, marker, sw);

            string line = acknowledged
                ? $"Load Large Doc (1,000 paragraphs): {BenchmarkFormat.Milliseconds(sw)} ms"
                : $"Load Large Doc (1,000 paragraphs): timed out after {ContentProbe.TimeoutSeconds} s";

            ResultLabel.Text = line;
            BenchmarkResultsSink.Publish(line);
        }

        // ---- Back ----
        private async void OnBackClicked(object? s, EventArgs e) =>
            await Navigation.PopAsync();

        // ---- Shared measurement body ----

        // Arms a stopwatch, focuses the editor so the toggle acts on the
        // user's current selection, invokes the supplied format action, and
        // reports the elapsed time either immediately (if FormatChanged does
        // not fire) or from the FormatChanged handler (if it does). The
        // _formatArmed flag ensures only one of the two paths publishes a
        // line for the same measurement.
        private void ApplyFormat(string label, Action action)
        {
            // Disarm any previous measurement in case one is still pending.
            _formatStopwatch = null;
            _formatArmed = false;
            _formatLabel = label;

            // Re-focus the editor so the toggle acts on the user's current
            // selection. Tapping an external button moves focus away from the
            // editor and on some platforms collapses the selection.
            try { richTextEditor.Focus(); } catch { /* best-effort */ }

            var sw = Stopwatch.StartNew();
            _formatStopwatch = sw;
            _formatArmed = true;

            try
            {
                action();
            }
            catch
            {
                _formatStopwatch = null;
                _formatArmed = false;
                ResultLabel.Text = $"Format ({label}): error";
                throw;
            }

            // If FormatChanged fired synchronously the handler has already
            // cleared _formatArmed and published the result. Otherwise we
            // publish the synchronous reading ourselves so the banner is
            // never blank.
            if (_formatArmed)
            {
                _formatStopwatch = null;
                _formatArmed = false;
                sw.Stop();
                PublishFormatResult(label, sw);
            }
        }

        // Records the elapsed time if a measurement is currently armed.
        // Clears the flag so a second FormatChanged event for the same
        // measurement does not publish a duplicate line.
        private void OnFormatChanged(object? sender, RichTextEditorFormatChangedEventArgs e)
        {
            if (!_formatArmed || _formatStopwatch is not Stopwatch sw)
            {
                return;
            }
            _formatStopwatch = null;
            _formatArmed = false;
            sw.Stop();

            PublishFormatResult(_formatLabel, sw);
        }

        private void PublishFormatResult(string label, Stopwatch sw)
        {
            string line = $"Format ({label}): {BenchmarkFormat.Milliseconds(sw)} ms";
            ResultLabel.Text = line;
            BenchmarkResultsSink.Publish(line);
        }
    }
}