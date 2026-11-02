using System.Diagnostics;
using System.Text;

namespace SfRTEPerformance
{
    /// <summary>
    /// Main benchmark page. Each button starts one timed scenario using
    /// <see cref="Stopwatch"/>. Results are appended to the on-screen
    /// results section as they are produced.
    /// </summary>
    public partial class BenchmarkPage : ContentPage
    {
        // Word-count scenarios used by the Initial Load buttons.
        // The Dynamic Value Update scenario uses the same three sizes; the
        // three buttons for it live on the editor page itself (visible only
        // when the editor is opened in Dynamic Value mode), not here.
        private static readonly int[] WordScenarios = { 100, 5_000, 10_000 };

        // The small document used to seed the editor when it is opened in
        // Dynamic Value mode. The time to render this priming document is
        // NOT measured — only the subsequent Value reassignment is.
        private const int DynamicPrimeWordCount = 100;

        // Buffer for the on-screen results section.
        private readonly StringBuilder _results = new();

        // True while this page is the active subscriber to
        // BenchmarkResultsSink.LinePublished. Toggled in OnDisappearing /
        // OnAppearing so the subscription is active exactly while an
        // editor page (which can publish results asynchronously) is on
        // screen.
        private bool _resultsSubscribed;

        public BenchmarkPage()
        {
            InitializeComponent();
        }

        // =================================================================
        // 1. Initial Load (one button per scenario, including empty)
        // =================================================================

        private async void OnRunInitialLoadEmptyClicked(object? sender, EventArgs e) =>
            await RunInitialLoadEmptyAsync();

        private async void OnRunInitialLoad100Clicked(object? sender, EventArgs e) =>
            await RunInitialLoadAsync("100 Words", 100);

        private async void OnRunInitialLoad5KClicked(object? sender, EventArgs e) =>
            await RunInitialLoadAsync("5,000 Words", 5_000);

        private async void OnRunInitialLoad10KClicked(object? sender, EventArgs e) =>
            await RunInitialLoadAsync("10,000 Words", 10_000);

        // Empty editor: measures the cost of constructing the editor page
        // with no HTML pre-assigned. There is no marker to poll for, so the
        // measurement stops at the page construction.
        private async Task RunInitialLoadEmptyAsync()
        {
            AppendHeader("Initial Load (Empty)");

            var sw = Stopwatch.StartNew();
            var editorPage = new BenchmarkEditorPage(
                bannerMessage: "Initial Load (Empty) - measuring...");

            await Navigation.PushAsync(editorPage);
            await editorPage.WaitForContentAsync(sw, "Empty");
        }

        // Builds a BenchmarkEditorPage with the requested document pre-assigned
        // in the editor, pushes the page onto the navigation stack, and waits
        // for the editor to confirm it has rendered the document.
        private async Task RunInitialLoadAsync(string label, int wordCount)
        {
            AppendHeader($"Initial Load ({label})");

            // A unique marker per run guarantees the probe matches THIS run's
            // document, never leftover content from a previous run.
            string marker = HtmlGenerator.Marker($"{label.Replace(",", "").Replace(" ", "")}-{Guid.NewGuid():N}");
            string html = HtmlGenerator.GenerateWordsDocument(wordCount, marker);

            // Start the stopwatch immediately before the page is constructed
            // so it captures page construction, editor construction, the
            // pre-assignment of HTML, the layout pass, and the document
            // parse and render — i.e. the entire "click to content on screen"
            // pipeline.
            var sw = Stopwatch.StartNew();
            var editorPage = new BenchmarkEditorPage(
                initialHtml: html,
                marker: marker,
                bannerMessage: $"Initial Load ({label}) - measuring...");

            await Navigation.PushAsync(editorPage);
            await editorPage.WaitForContentAsync(sw, label);
        }

        // =================================================================
        // 2. Dynamic Value Update (single button on this page; the three
        //    per-scenario buttons live on the editor page itself).
        //
        // Tapping this button opens a freshly-primed editor page in Dynamic
        // Value mode. The three buttons (100 / 5K / 10K Words) below the
        // banner on that page let the user measure a single Value
        // reassignment per tap, on the SAME editor instance. The reported
        // number is the time for one reassignment, not the time to
        // construct the editor.
        // =================================================================

        private async void OnRunDynamicValueClicked(object? sender, EventArgs e)
        {
            AppendHeader("Dynamic Value Update");

            // Seed the editor with a small priming document so the
            // user sees content immediately. The time to render this
            // priming document is outside the measurement window — it
            // happens before any Dynamic button is tapped.
            string primeMarker = HtmlGenerator.Marker($"Prime-{Guid.NewGuid():N}");
            string primeHtml = HtmlGenerator.GenerateWordsDocument(
                DynamicPrimeWordCount, primeMarker);

            var editorPage = new BenchmarkEditorPage(
                initialHtml: primeHtml,
                marker: primeMarker,
                bannerMessage: "Editor ready. Tap a Word Count button to measure one Value reassignment.",
                isDynamicValueMode: true);

            await Navigation.PushAsync(editorPage);
        }

        // =================================================================
        // 3. 10,000 Paragraphs (unchanged — kept per spec)
        // =================================================================

        private async void OnRunTenKParagraphsClicked(object? sender, EventArgs e)
        {
            AppendHeader("10,000 Paragraphs");

            // Generate the 10,000 paragraph HTML once (we time only the
            // editor load, not HTML generation, which is constant per run).
            string marker = HtmlGenerator.Marker($"10K-{Guid.NewGuid():N}");
            string html = HtmlGenerator.GenerateParagraphsDocument(10_000, marker);

            var sw = Stopwatch.StartNew();
            var editorPage = new BenchmarkEditorPage(
                initialHtml: html,
                marker: marker,
                bannerMessage: "Measuring 10,000 paragraph load...");

            await Navigation.PushAsync(editorPage);
            await editorPage.WaitForContentAsync(sw, "10,000 Paragraphs");
        }

        // =================================================================
        // 4. Apply Format
        // =================================================================

        private async void OnRunApplyFormatClicked(object? sender, EventArgs e)
        {
            AppendHeader("Apply Format");

            await Navigation.PushAsync(new BenchmarkFormatPage());
        }

        // =================================================================
        // Result helpers
        // =================================================================

        private void OnClearResultsClicked(object? sender, EventArgs e)
        {
            _results.Clear();
            ResultsLabel.Text = "Results cleared.";
        }

        private void OnBenchmarkLinePublished(string line) => AppendLine(line);

        // The benchmark results sink is a static event. The editor page
        // (or format page) publishes to it whenever it completes a
        // measurement. This page must be subscribed for the entire window
        // in which an editor / format page is on screen — including the
        // Dynamic Value Update buttons on the editor page, which publish
        // results asynchronously (long after this page's click handler
        // has returned). Tying the subscription to OnDisappearing /
        // OnAppearing is the simplest way to get that window exactly
        // right.
        protected override void OnAppearing()
        {
            base.OnAppearing();
            UnsubscribeFromResults();
        }

        protected override void OnDisappearing()
        {
            SubscribeToResults();
            base.OnDisappearing();
        }

        private void SubscribeToResults()
        {
            if (_resultsSubscribed) return;
            BenchmarkResultsSink.LinePublished += OnBenchmarkLinePublished;
            _resultsSubscribed = true;
        }

        private void UnsubscribeFromResults()
        {
            if (!_resultsSubscribed) return;
            BenchmarkResultsSink.LinePublished -= OnBenchmarkLinePublished;
            _resultsSubscribed = false;
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