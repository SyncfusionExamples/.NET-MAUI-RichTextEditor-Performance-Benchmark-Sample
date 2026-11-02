using System.Diagnostics;
using Syncfusion.Maui.RichTextEditor;

namespace SfRTEPerformance
{
    public partial class BenchmarkFormatPage : ContentPage
    {
        // The HTML seeded into the editor so the user has something to
        // select. It is a few sentences of plain text — short enough to
        // keep the page responsive, long enough to allow a selection.
        private const string SeedHtml =
            "<p>The <strong>quick brown fox</strong> jumps over the lazy dog. " +
            "Pack my box with five dozen liquor jugs. " +
            "How vexingly quick daft zebras jump.</p>" +
            "<p>Select a few words (or the whole text) and tap any of the format " +
            "buttons below. The time from your tap to the FormatChanged event " +
            "is measured and shown in the banner above.</p>";

        // Stopwatch that is started inside a format-button click handler,
        // right before we call into SfRichTextEditor, and stopped inside
        // the FormatChanged event handler.
        //
        // "_formatStopwatch is not null" is the "armed" flag — when set,
        // the next FormatChanged event will record a measurement. We
        // clear it back to null in OnFormatChanged so that any further
        // FormatChanged events (e.g. selection changes) are ignored.
        private Stopwatch? _formatStopwatch;

        // The label of the format that is currently being measured, e.g.
        // "Bold" or "Align Center". Set right before the API call, read
        // inside the FormatChanged handler.
        private string _formatLabel = string.Empty;

        public BenchmarkFormatPage()
        {
            InitializeComponent();

            // Seed the editor with a small, varied paragraph so the user
            // can either select a few words or use Ctrl/Cmd+A to apply
            // formatting to the whole content.
            richTextEditor.Value = SeedHtml;
        }

        // -----------------------------------------------------------------
        // Character-format click handlers
        // -----------------------------------------------------------------
        private void OnBoldClicked(object? sender, EventArgs e) =>
            ApplyFormat("Bold", richTextEditor.ToggleBold);

        private void OnItalicClicked(object? sender, EventArgs e) =>
            ApplyFormat("Italic", richTextEditor.ToggleItalic);

        private void OnUnderlineClicked(object? sender, EventArgs e) =>
            ApplyFormat("Underline", richTextEditor.ToggleUnderline);

        private void OnStrikethroughClicked(object? sender, EventArgs e) =>
            ApplyFormat("Strikethrough", richTextEditor.ToggleStrikethrough);

        // -----------------------------------------------------------------
        // Paragraph-format click handlers
        // -----------------------------------------------------------------
        private void OnBulletsClicked(object? sender, EventArgs e) =>
            ApplyFormat("Bullets", richTextEditor.ToggleBulletList);

        private void OnNumbersClicked(object? sender, EventArgs e) =>
            ApplyFormat("Numbers", richTextEditor.ToggleNumberedList);

        private void OnAlignLeftClicked(object? sender, EventArgs e) =>
            ApplyFormat("Align Left", richTextEditor.AlignLeft);

        private void OnAlignCenterClicked(object? sender, EventArgs e) =>
            ApplyFormat("Align Center", richTextEditor.AlignCenter);

        private void OnAlignRightClicked(object? sender, EventArgs e) =>
            ApplyFormat("Align Right", richTextEditor.AlignRight);

        private void OnAlignJustifyClicked(object? sender, EventArgs e) =>
            ApplyFormat("Justify", richTextEditor.AlignJustify);

        // -----------------------------------------------------------------
        // Load Large Doc — assigns a sizeable HTML document to the editor
        // so the user can apply formatting on a realistically-sized body.
        // The Value assignment is timed and reported, but it does NOT
        // arm the format stopwatch, so any FormatChanged events raised
        // during the load are ignored (the load is not a format button
        // click).
        // -----------------------------------------------------------------
        private async void OnLoadLargeDocClicked(object? sender, EventArgs e)
        {
            // Make sure no stale format measurement is in flight. The
            // load below may raise FormatChanged internally; we do not
            // want it to be recorded as a format-button measurement.
            _formatStopwatch = null;
            _formatLabel = string.Empty;

            // Build a sizeable HTML document: 1,000 paragraphs of plain
            // text. Big enough to exercise the editor's layout path, but
            // not so large that the page becomes unresponsive.
            //
            // The timing stops when the editor acknowledges the new
            // document (see ContentProbe), not when the assignment
            // returns, because the assignment only enqueues the payload
            // for the WebView.
            string marker = HtmlGenerator.Marker($"1K-{Guid.NewGuid():N}");
            string html = HtmlGenerator.GenerateParagraphsDocument(1_000, marker);

            var sw = Stopwatch.StartNew();
            richTextEditor.Value = html;

            bool acknowledged = await ContentProbe.WaitForContentAsync(
                richTextEditor, marker, sw, html.Length);

            string line = acknowledged
                ? $"Load Large Doc (1,000 paragraphs): {BenchmarkFormat.Milliseconds(sw)} ms"
                : $"Load Large Doc (1,000 paragraphs): timed out after {BenchmarkFormat.Milliseconds(ContentProbe.Timeout / 1000)} s";

            ResultLabel.Text = line;
            BenchmarkResultsSink.Publish(line);
        }

        // -----------------------------------------------------------------
        // Shared measurement body
        // -----------------------------------------------------------------

        // Arms the stopwatch with the supplied label, focuses the
        // editor so the toggle acts on the user's current selection,
        // invokes the supplied format action against the
        // SfRichTextEditor, and ALWAYS updates the on-page banner with
        // the synchronous call time before returning. The
        // FormatChanged event, if it fires, may overwrite the banner
        // with a slightly more accurate reading (one that includes any
        // post-call layout work).
        //
        // Why we focus the editor first:
        //
        //   Tapping an external button moves focus away from the
        //   editor. On most platforms that also collapses the
        //   selection, which causes toggle methods like
        //   ToggleBulletList and ToggleNumberedList to behave
        //   inconsistently — the first tap may create the list, but
        //   a second tap has no list item to remove because the
        //   editor's "current paragraph" is now a non-list
        //   paragraph. Re-focusing the editor before the API call
        //   re-establishes a clean selection state and makes the
        //   toggles behave the way the user expects.
        //
        // Why we update the banner immediately rather than waiting for
        // FormatChanged:
        //
        //   Some format toggles — notably Bold and Italic when the
        //   editor has lost focus (e.g. immediately after the user
        //   tapped the format button) or when the selection is empty —
        //   do not raise a FormatChanged event at all. Recording the
        //   synchronous call duration here makes the benchmark visible
        //   for every format button.
        //
        // Why we report fractional milliseconds:
        //
        //   Stopwatch.ElapsedMilliseconds is a long and truncates to
        //   whole milliseconds. On fast hardware, a simple in-memory
        //   toggle (e.g. a Bold toggle on a small selection) easily
        //   completes in under 1 ms, which would otherwise display as
        //   "0 ms" and look like the benchmark did not run. Using
        //   ElapsedTicks divided by the stopwatch frequency gives
        //   sub-ms precision so the user always sees a meaningful
        //   number.
        private void ApplyFormat(string label, Action action)
        {
            // Disarm any previous measurement in case one is still
            // pending (defensive).
            _formatStopwatch = null;
            _formatLabel = label;

            // Re-focus the editor so the toggle acts on the user's
            // current selection. Without this, the focus shift caused
            // by tapping the button would either collapse the
            // selection or, on some platforms, cause the toggle to
            // apply to the "next typed character" instead of the
            // current selection — which is why Bullets/Numbers could
            // not be removed with a second tap.
            try { richTextEditor.Focus(); } catch { /* best-effort */ }

            // Start the stopwatch just before calling into the editor.
            var sw = Stopwatch.StartNew();
            _formatStopwatch = sw;

            try
            {
                action();
            }
            catch
            {
                // If the API call threw, disarm and surface the error
                // so the banner is not left blank.
                _formatStopwatch = null;
                ResultLabel.Text = $"Format ({label}): error - {sw.ElapsedMilliseconds} ms";
                throw;
            }

            // We always have a synchronous time by now. If FormatChanged
            // has already fired (synchronous raise), the handler will
            // have disarmed _formatStopwatch. If it has not, we record
            // the synchronous call time ourselves so the banner is
            // never left blank.
            //
            // We use Interlocked.Exchange so the FormatChanged handler
            // and this code cannot both publish a line for the same
            // measurement: whichever runs second will see _formatStopwatch
            // == null and bail out.
            var taken = Interlocked.Exchange(ref _formatStopwatch, null);
            if (taken is not null)
            {
                taken.Stop();
                string line = $"Format ({label}): {FormatElapsed(taken)} ms";
                ResultLabel.Text = line;
                BenchmarkResultsSink.Publish(line);
            }
        }

        // -----------------------------------------------------------------
        // FormatChanged handler — records the elapsed time
        // -----------------------------------------------------------------
        // If the editor raises FormatChanged after the format call, we
        // overwrite the banner with this more accurate (slightly later)
        // reading. We do this only if our own synchronous recorder has
        // not already disarmed the stopwatch; the Interlocked.Exchange
        // in ApplyFormat guarantees that only ONE of the two code paths
        // publishes a line for a given measurement.
        private void OnFormatChanged(object? sender, RichTextEditorFormatChangedEventArgs e)
        {
            // Atomically take ownership of the stopwatch. If it is null,
            // the synchronous recorder has already taken it; ignore this
            // event.
            var taken = Interlocked.Exchange(ref _formatStopwatch, null);
            if (taken is null)
            {
                return;
            }

            string label = _formatLabel;
            _formatLabel = string.Empty;
            taken.Stop();

            string line = $"Format ({label}): {FormatElapsed(taken)} ms";
            ResultLabel.Text = line;
            BenchmarkResultsSink.Publish(line);
        }

        // Formats a stopwatch reading as a human-readable millisecond
        // string with sub-ms precision. We always show two decimal
        // places so even very fast operations (a few hundred
        // microseconds) display as something like "0.42 ms" rather
        // than the misleading "0 ms" that ElapsedMilliseconds would
        // produce.
        private static string FormatElapsed(Stopwatch sw)
        {
            return BenchmarkFormat.Milliseconds(sw);
        }

        // -----------------------------------------------------------------
        // Navigation
        // -----------------------------------------------------------------
        private async void OnBackClicked(object? sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
    }
}
