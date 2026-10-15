using System.Diagnostics;
using Syncfusion.Maui.RichTextEditor;

namespace SfRTEPerformance
{
    public partial class BenchmarkEditorPage : ContentPage
    {
        // Stopwatch started by the caller (the BenchmarkPage) right before
        // the constructor. It is stopped in OnAppearing to capture the
        // time it takes to create the page, the editor, the layout, the
        // initial HTML assignment (if any), and the first paint.
        private readonly Stopwatch _initialLoadStopwatch;

        // Optional HTML that should be pre-assigned to the editor BEFORE
        // the page is added to the visual tree. Assigned in the
        // constructor, right after InitializeComponent. This way the
        // stopwatch in OnAppearing measures the FULL pipeline:
        //   construct page -> construct editor -> assign Value -> layout
        //   -> first paint
        // which is what the user is actually interested in for an
        // "initial load" benchmark.
        private readonly string? _initialHtml;

        // Optional banner text shown above the editor while the benchmark runs.
        private readonly string _bannerMessage;

        public BenchmarkEditorPage(
            Stopwatch initialLoadStopwatch,
            string? initialHtml = null,
            string? bannerMessage = null)
        {
            InitializeComponent();

            _initialLoadStopwatch = initialLoadStopwatch;
            _initialHtml = initialHtml;
            _bannerMessage = bannerMessage ?? string.Empty;

            // Pre-assign the HTML BEFORE the page appears, so the
            // initial-load stopwatch captures the assignment too. This
            // is the requested behaviour: "initial loading the html
            // control should be pre assigned, not should assign on
            // OnAppearing method".
            if (_initialHtml != null)
            {
                richTextEditor.Value = _initialHtml;
            }

            // Show whatever banner text the caller passed in. For the
            // initial-load benchmark this is updated to the measured
            // time in OnAppearing.
            ResultLabel.Text = _bannerMessage;
        }

        /// <summary>
        /// Reassigns the editor's <see cref="SfRichTextEditor.Value"/> to the
        /// supplied HTML and reports the elapsed time as a benchmark line.
        /// Called by the dynamic-value-update benchmark from the
        /// <see cref="BenchmarkPage"/> after the editor is already on screen.
        /// </summary>
        public void MeasureValueUpdate(string sizeLabel, string html)
        {
            var stopwatch = Stopwatch.StartNew();
            richTextEditor.Value = html;
            stopwatch.Stop();

            string line = $"{sizeLabel} Value Update: {stopwatch.ElapsedMilliseconds} ms";
            BenchmarkResultsSink.Publish(line);

            // Replace the banner with the most recent result so the
            // user can read each timing in turn as the benchmark
            // progresses. Earlier timings are still visible in the
            // BenchmarkPage results section, which accumulates them.
            ResultLabel.Text = line;
        }

        /// <summary>
        /// Shows a short preview message in the banner without
        /// measuring anything. Used by the BenchmarkPage between
        /// measurements so the user has something to look at during
        /// the inter-measurement pause.
        /// </summary>
        public void ShowPreview(string message)
        {
            ResultLabel.Text = message;
        }

        /// <summary>
        /// Handler for the "Back to Benchmarks" button. Pops this page
        /// off the navigation stack so the user returns to the
        /// <see cref="BenchmarkPage"/>.
        /// </summary>
        private async void OnBackClicked(object? sender, EventArgs e)
        {
            await Task.Delay(100);
            await Navigation.PopAsync();
        }

        private void richTextEditor_Loaded(object sender, EventArgs e)
        {
            // Stop the stopwatch and report the time it took to create
            // the page, construct the editor, pre-assign its Value (if
            // any), lay it out, and present it on screen. This is the
            // "from click to control rendering" measurement the spec
            // asks for.
            _initialLoadStopwatch.Stop();

            string sizeLabel = _initialHtml switch
            {
                null => "Empty",
                _ when _initialHtml.Length < 50 * 1024 => "10 KB",
                _ when _initialHtml.Length < 250 * 1024 => "100 KB",
                _ when _initialHtml.Length < 750 * 1024 => "500 KB",
                _ when _initialHtml.Length < 1_500_000 => "1 MB",
                _ => $"{_initialHtml.Length / (1024 * 1024)} MB"
            };

            string initialLoadLine =
                $"Initial Load ({sizeLabel}): {_initialLoadStopwatch.ElapsedMilliseconds} ms";

            ResultLabel.Text = initialLoadLine;
            BenchmarkResultsSink.Publish(initialLoadLine);

            // Notify any waiting caller (the BenchmarkPage) that the
            // editor is fully constructed, laid out, and ready for
            // subsequent Value updates. The benchmark page uses this
            // signal so it never measures a Value assignment against a
            // half-initialised control.
            BenchmarkResultsSink.SignalReady();
        }
    }
}
