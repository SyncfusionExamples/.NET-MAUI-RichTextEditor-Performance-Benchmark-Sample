using System.Diagnostics;
using System.Text;

namespace SfRTEPerformance
{
    /// <summary>
    /// Main benchmark page. Each button starts one or more timed scenarios
    /// using <see cref="Stopwatch"/>. Results are appended to the on-screen
    /// results section as they are produced.
    /// </summary>
    public partial class BenchmarkPage : ContentPage
    {
        // Buffer for the on-screen results section. A StringBuilder is kept
        // around so we don't allocate a new one for every appended line.
        private readonly StringBuilder _results = new();

        // Inter-measurement pause for the multi-step benchmarks (Dynamic
        // Value Update, Run All). A short delay between iterations
        // makes the per-iteration timing visible on the editor page
        // banner — without it the four measurements fire back-to-back
        // and the user only sees the last one. The delay is OUTSIDE
        // the stopwatch window, so it does not affect the timings.
        private const int InterMeasurementDelayMs = 1000;

        public BenchmarkPage()
        {
            InitializeComponent();
        }

        // -----------------------------------------------------------------
        // 1. Initial Control Load Benchmark (per-size buttons)
        // -----------------------------------------------------------------

        // The five initial-load buttons all share the same body — they
        // only differ in which HTML payload is pre-assigned in the editor
        // page's constructor. The stopwatch is started on the call site
        // so it captures every nanosecond from "user tapped the button"
        // up to the editor's first paint.
        private async void OnRunInitialLoadEmptyClicked(object? sender, EventArgs e)
        {
            AppendHeader("Initial Load");
            await RunInitialLoadAsync(sizeLabel: "Empty", html: null);
        }

        private async void OnRunInitialLoad10KbClicked(object? sender, EventArgs e)
        {
            AppendHeader("Initial Load (10 KB)");
            await RunInitialLoadAsync("10 KB", HtmlGenerator.GenerateSmallDocument());
        }

        private async void OnRunInitialLoad500KbClicked(object? sender, EventArgs e)
        {
            AppendHeader("Initial Load (500 KB)");
            await RunInitialLoadAsync("500 KB", HtmlGenerator.GenerateLargeDocument());
        }

        private async void OnRunInitialLoad1MbClicked(object? sender, EventArgs e)
        {
            AppendHeader("Initial Load (1 MB)");
            await RunInitialLoadAsync("1 MB", HtmlGenerator.GenerateExtraLargeDocument());
        }

        // Shared body for the five initial-load buttons. The stopwatch
        // starts immediately before `new BenchmarkEditorPage(...)` so it
        // captures the page constructor, the editor construction, the
        // pre-assignment of the HTML, the layout pass, and the first
        // paint — i.e. the entire "from click to control rendering"
        // pipeline the spec asks for.
        private async Task RunInitialLoadAsync(string sizeLabel, string? html)
        {
            // Start the stopwatch IMMEDIATELY before the constructor so
            // the measurement includes page construction, editor
            // construction, the pre-assignment of HTML, the layout
            // pass, the navigation animation, and the first paint.
            var stopwatch = Stopwatch.StartNew();

            var editorPage = new BenchmarkEditorPage(
                initialLoadStopwatch: stopwatch,
                initialHtml: html,
                bannerMessage: $"Initial Load ({sizeLabel}) - measuring...");

            // Wait for the editor page to fully appear before returning,
            // so the timing line is published only after the page is
            // rendered. The user stays on the editor page afterwards
            // and uses the Back button to return here.
            await RunEditorPageAsync(editorPage, afterReady: null);

            // Sanity-print what we just measured, in case the user
            // navigates back before reading the on-page banner.
            AppendLine($"(See editor page banner for the {sizeLabel} load time.)");
        }

        // -----------------------------------------------------------------
        // 2. Dynamic Value Update Benchmark (10 KB, 100 KB, 500 KB, 1 MB)
        // -----------------------------------------------------------------
        private async void OnRunDynamicValueClicked(object? sender, EventArgs e)
        {
            AppendHeader("Dynamic Value Update");

            // Open ONE editor page, load a small initial document, and then
            // reassign the Value property four times — once per target size.
            // This matches the spec: re-measure Value assignment on a control
            // that has already been loaded.
            string initialHtml = HtmlGenerator.GenerateSmallDocument();

            var stopwatch = Stopwatch.StartNew();
            var editorPage = new BenchmarkEditorPage(
                initialLoadStopwatch: stopwatch,
                initialHtml: initialHtml,
                bannerMessage: "Measuring value updates...");

            // The four Value-update measurements, each running on the
            // already-loaded editor. They are only kicked off AFTER the
            // editor page has signalled it is ready, otherwise the first
            // assignment runs against a half-constructed control and
            // reports 0 ms.
            //
            // Between each measurement we wait
            // InterMeasurementDelayMs so the user can see the per-iteration
            // timing on the editor's banner before the next one fires.
            // The delay is outside the stopwatch window, so it does not
            // pollute the timings.
            async Task BodyAsync(BenchmarkEditorPage page)
            {
                await MeasureValueUpdateWithPreviewAsync(
                    page, "500 KB", HtmlGenerator.GenerateLargeDocument());
                await MeasureValueUpdateWithPreviewAsync(
                    page, "1 MB", HtmlGenerator.GenerateExtraLargeDocument());
            }

            await RunEditorPageAsync(editorPage, afterReady: BodyAsync);
        }

        // -----------------------------------------------------------------
        // 3. Large Document Benchmark (10,000 paragraphs)
        // -----------------------------------------------------------------
        private async void OnRunTenKParagraphsClicked(object? sender, EventArgs e)
        {
            AppendHeader("10,000 Paragraphs");

            // Generate the 10,000 paragraph HTML once (we time only the
            // editor load, not HTML generation, which is constant per run).
            string html = HtmlGenerator.GenerateParagraphsDocument(10_000);

            var stopwatch = Stopwatch.StartNew();

            var editorPage = new BenchmarkEditorPage(
                initialLoadStopwatch: stopwatch,
                initialHtml: html,
                bannerMessage: "Measuring 10,000 paragraph load...");

            await RunEditorPageAsync(editorPage, afterReady: null);
        }

        // -----------------------------------------------------------------
        // 5. Apply Format Benchmark
        // -----------------------------------------------------------------
        // Pushes the BenchmarkFormatPage, which has the SfRichTextEditor
        // and a row of basic formatting buttons (Bold, Italic, Underline,
        // Strikethrough, Bullets, Numbers, Left, Center, Right, Justify).
        // The page itself owns the timing — each format button starts a
        // Stopwatch before calling into the editor and stops it in the
        // FormatChanged event handler. Timing lines are pushed back here
        // through the shared results sink.
        private async void OnRunApplyFormatClicked(object? sender, EventArgs e)
        {
            AppendHeader("Apply Format");

            var formatPage = new BenchmarkFormatPage();

            BenchmarkResultsSink.LinePublished += OnBenchmarkLinePublished;
            try
            {
                await Navigation.PushAsync(formatPage);

                // We do not auto-pop. The user stays on the format page
                // so they can apply multiple formats in a row, then
                // navigates back via the Back button.
            }
            finally
            {
                BenchmarkResultsSink.LinePublished -= OnBenchmarkLinePublished;
            }
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        // Pushes the editor page, waits for it to fully appear, then
        // optionally runs the supplied body of measurements against the
        // editor. The editor page STAYS on screen after the measurement
        // runs — the user navigates back to this page themselves (via
        // the device's back gesture or the Back button on the editor
        // page). Auto-popping the page after every benchmark confused
        // users, so it was removed.
        //
        // The "wait for ready" step is the critical piece: it guarantees
        // that any Value-update measurements run against a fully
        // constructed and laid-out SfRichTextEditor, not a half-built one.
        private async Task RunEditorPageAsync(
            BenchmarkEditorPage editorPage,
            Func<BenchmarkEditorPage, Task>? afterReady)
        {
            // Subscribe so the editor page can push its result line back here.
            BenchmarkResultsSink.LinePublished += OnBenchmarkLinePublished;

            // Set up the ready signal BEFORE pushing the page so we never
            // miss the event if OnAppearing fires immediately after push.
            var readyTcs = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            void OnReady()
            {
                readyTcs.TrySetResult(true);
            }
            BenchmarkResultsSink.EditorReady += OnReady;

            try
            {
                await Navigation.PushAsync(editorPage);

                // Wait for the editor page to finish its first OnAppearing.
                await readyTcs.Task;

                // Run the per-scenario measurement body, if any.
                if (afterReady != null)
                {
                    await afterReady(editorPage);
                }

                // NOTE: we intentionally do NOT pop the editor page here.
                // The user stays on the editor page so they can inspect
                // the content, and navigates back via the back button /
                // the Back button on the editor page's toolbar.
            }
            finally
            {
                BenchmarkResultsSink.EditorReady -= OnReady;
                BenchmarkResultsSink.LinePublished -= OnBenchmarkLinePublished;
            }
        }

        private void OnClearResultsClicked(object? sender, EventArgs e)
        {
            _results.Clear();
            ResultsLabel.Text = "Results cleared.";
        }

        // Runs a single Value-update measurement with a "preview" banner
        // and a short inter-measurement pause so the user can see each
        // timing as the benchmark progresses. Without the pause the
        // four measurements fire back-to-back and only the last one
        // remains visible on the editor's banner.
        //
        // The pause is OUTSIDE the stopwatch window, so it does not
        // affect the reported timings.
        private static async Task MeasureValueUpdateWithPreviewAsync(
            BenchmarkEditorPage page, string sizeLabel, string html)
        {
            // Show what is about to run, so the user has a clear
            // "next" indicator on the banner.
            page.ShowPreview($"Running {sizeLabel}...");

            // Small delay so the preview is actually visible before
            // the editor starts churning on the next Value assignment.
            await Task.Delay(InterMeasurementDelayMs / 2);

            // The actual measurement — this is what the benchmark
            // reports. The Stopwatch is started and stopped inside
            // MeasureValueUpdate, so the preview delay above is not
            // included in the timing.
            page.MeasureValueUpdate(sizeLabel, html);

            // Pause again so the user can read the timing before the
            // next iteration's preview overwrites it.
            await Task.Delay(InterMeasurementDelayMs);
        }

        // Receives a benchmark result line from the editor page and appends
        // it to the on-screen results section.
        private void OnBenchmarkLinePublished(string line)
        {
            AppendLine(line);
        }

        private void AppendHeader(string header)
        {
            AppendLine("======================================");
            AppendLine(header);
            AppendLine("======================================");
        }

        private void AppendLine(string line)
        {
            _results.AppendLine(line);
            ResultsLabel.Text = _results.ToString();
        }
    }
}
