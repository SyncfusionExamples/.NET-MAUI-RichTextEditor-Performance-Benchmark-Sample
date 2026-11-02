using System.Diagnostics;
using Syncfusion.Maui.RichTextEditor;

namespace SfRTEPerformance
{
    public partial class BenchmarkEditorPage : ContentPage
    {
        // Stopwatch started by the caller (the BenchmarkPage) right before
        // the constructor. It is stopped when the editor acknowledges that
        // it has ingested the assigned document — NOT in the control's
        // Loaded event.
        //
        // Loaded fires when the MAUI visual tree attaches the control, which
        // happens long before the WebView-hosted editor has parsed and
        // rendered the document. Stopping there measures the wrong thing.
        // See ContentProbe for the full rationale.
        private readonly Stopwatch? _initialLoadStopwatch;

        // The plain-text marker embedded in the assigned HTML. The editor's
        // live document text is polled until this marker shows up, which is
        // the point at which the content is genuinely loaded.
        private readonly string? _initialMarker;

        // The size of the assigned document, used to scale the probe's poll
        // interval so the probe overhead stays comparable across sizes.
        private readonly int _initialHtmlSizeBytes;

        // Optional HTML that should be pre-assigned to the editor BEFORE
        // the page is added to the visual tree. Assigned in the
        // constructor, right after InitializeComponent.
        private readonly string? _initialHtml;

        // Optional banner text shown above the editor while the benchmark runs.
        private readonly string _bannerMessage;

        // Set once the initial-load measurement has been published, so a
        // late Loaded event cannot publish a second, wrong number.
        private bool _initialLoadPublished;

        // The moment the control was attached to the visual tree. Captured so
        // the benchmark can report the gap between "control loaded" and
        // "content rendered" — the gap that made the original measurement
        // invalid.
        private long _loadedTimestamp;

        public BenchmarkEditorPage(
            Stopwatch? initialLoadStopwatch,
            string? initialHtml = null,
            string? marker = null,
            string? bannerMessage = null)
        {
            InitializeComponent();

            _initialLoadStopwatch = initialLoadStopwatch;
            _initialHtml = initialHtml;
            _initialMarker = marker;
            _initialHtmlSizeBytes = initialHtml?.Length ?? 0;
            _bannerMessage = bannerMessage ?? string.Empty;

            // Pre-assign the HTML BEFORE the page appears, so the
            // initial-load stopwatch captures the assignment too.
            if (_initialHtml != null)
            {
                richTextEditor.Value = _initialHtml;
            }

            // Show whatever banner text the caller passed in. For the
            // initial-load benchmark this is updated to the measured
            // time once the content is acknowledged.
            ResultLabel.Text = _bannerMessage;
        }

        /// <summary>
        /// Reassigns the editor's <see cref="SfRichTextEditor.Value"/> to the
        /// supplied HTML and reports the elapsed time as a benchmark line.
        /// Called by the dynamic-value-update benchmark from the
        /// <see cref="BenchmarkPage"/> after the editor is already on screen.
        /// </summary>
        /// <remarks>
        /// The stopwatch is stopped when the editor acknowledges the new
        /// document, not when the assignment returns. Assigning
        /// <c>Value</c> only enqueues the payload for the WebView; the parse
        /// and render happen afterwards. Stopping at the end of the
        /// assignment therefore reports the cost of an enqueue, not the cost
        /// of a load.
        /// </remarks>
        /// <param name="sizeLabel">The label for the document size.</param>
        /// <param name="html">The HTML to assign.</param>
        /// <param name="marker">
        /// The plain-text marker expected in the document once it has been
        /// ingested. Pass <c>null</c> to fall back to the synchronous
        /// assignment time.
        /// </param>
        public async Task MeasureValueUpdateAsync(
            string sizeLabel,
            string html,
            string? marker = null)
        {
            var stopwatch = Stopwatch.StartNew();

            richTextEditor.Value = html;

            bool acknowledged = true;
            if (!string.IsNullOrEmpty(marker))
            {
                // Publish the banner the instant the endpoint is detected,
                // rather than after the probe has finished watching for the
                // document to settle. The settle watch is diagnostic only.
                void OnFirstText(ContentProbeResult result)
                {
                    string line =
                        $"{sizeLabel} Value Update (first text): {BenchmarkFormat.Milliseconds(stopwatch)} ms";

                    BenchmarkResultsSink.Publish(line);
                    ResultLabel.Text = line;
                }

                acknowledged = await ContentProbe.WaitForContentAsync(
                    richTextEditor, marker, stopwatch, html.Length,
                    onFirstText: OnFirstText);
            }
            else
            {
                stopwatch.Stop();

                string line =
                    $"{sizeLabel} Value Update: {BenchmarkFormat.Milliseconds(stopwatch)} ms";

                BenchmarkResultsSink.Publish(line);
                ResultLabel.Text = line;
            }

            if (!acknowledged)
            {
                string line =
                    $"{sizeLabel} Value Update: timed out after {BenchmarkFormat.Milliseconds(ContentProbe.Timeout / 1000)} s";

                BenchmarkResultsSink.Publish(line);
                ResultLabel.Text = line;
            }
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

        /// <summary>
        /// Fires once the control is attached to the visual tree.
        /// </summary>
        /// <remarks>
        /// This event is deliberately NOT used as the measurement endpoint.
        /// It only marks that the visual tree attach has happened; the
        /// document is parsed and rendered afterwards. The timestamp is
        /// recorded here so the benchmark can report how much of the load
        /// happened after the control reported itself as loaded.
        /// </remarks>
        private void richTextEditor_Loaded(object? sender, EventArgs e)
        {
            _loadedTimestamp = Stopwatch.GetTimestamp();
        }

        /// <summary>
        /// Waits until the editor has ingested the document that was assigned
        /// in the constructor, then publishes the initial-load measurement.
        /// </summary>
        /// <returns><c>true</c> when the content was acknowledged in time.</returns>
        public async Task<bool> MeasureInitialLoadAsync()
        {
            if (_initialLoadStopwatch is null)
            {
                // Nothing to measure, but the caller is still waiting on the
                // ready signal.
                BenchmarkResultsSink.SignalReady();
                return true;
            }

            bool acknowledged = true;
            var probeResult = new ContentProbeResult();

            if (!string.IsNullOrEmpty(_initialMarker))
            {
                string sizeLabel = GetSizeLabel();

                // The probe invokes this callback on the very tick the
                // endpoint is detected, BEFORE it goes on to watch for the
                // document to settle. That is what makes the banner update
                // as soon as the text is actually there.
                //
                // The settle watch is diagnostic only: it shows how much
                // work the control does after the text is already on screen.
                // Waiting for it before publishing the headline number would
                // leave the banner blank long after the user could read the
                // text.
                void OnFirstText(ContentProbeResult result)
                {
                    if (_initialLoadPublished)
                    {
                        return;
                    }
                    _initialLoadPublished = true;

                    string line =
                        $"Initial Text ({sizeLabel}): {BenchmarkFormat.Milliseconds(_initialLoadStopwatch)} ms";

                    ResultLabel.Text = line;
                    BenchmarkResultsSink.Publish(line);

                    // The breakdown explains where the time went: how much
                    // happened before the control said it was loaded, and how
                    // much happened after. The second number is the gap that
                    // the original Loaded-based measurement missed entirely.
                    PublishTimingBreakdown(sizeLabel, result);
                }

                // Published once the document has stopped changing, so the
                // "kept changing for another N ms" line can be added after
                // the headline number.
                void OnSettled(ContentProbeResult result)
                {
                    PublishSettleLine(result);
                }

                acknowledged = await ContentProbe.WaitForContentAsync(
                    richTextEditor, _initialMarker, _initialLoadStopwatch,
                    _initialHtmlSizeBytes, probeResult,
                    onFirstText: OnFirstText,
                    onSettled: OnSettled);
            }
            else
            {
                // No marker means there is nothing to poll for. Fall back to
                // stopping at the control's Loaded event, which is the best
                // available signal for an empty document.
                _initialLoadStopwatch.Stop();

                if (!_initialLoadPublished)
                {
                    _initialLoadPublished = true;

                    string line =
                        $"Initial Text ({GetSizeLabel()}): {BenchmarkFormat.Milliseconds(_initialLoadStopwatch)} ms";

                    ResultLabel.Text = line;
                    BenchmarkResultsSink.Publish(line);
                }
            }

            if (!acknowledged && !_initialLoadPublished)
            {
                _initialLoadPublished = true;

                string line =
                    $"Initial Text ({GetSizeLabel()}): timed out after {BenchmarkFormat.Milliseconds(ContentProbe.Timeout / 1000)} s";

                ResultLabel.Text = line;
                BenchmarkResultsSink.Publish(line);
            }

            // Notify any waiting caller (the BenchmarkPage) that the
            // editor has ingested the content and is ready for
            // subsequent Value updates.
            BenchmarkResultsSink.SignalReady();

            return acknowledged;
        }

        /// <summary>
        /// Derives the human-readable size label from the assigned document.
        /// </summary>
        private string GetSizeLabel() => _initialHtml switch
        {
            null => "Empty",
            _ when _initialHtml.Length < 50 * 1024 => "10 KB",
            _ when _initialHtml.Length < 250 * 1024 => "100 KB",
            _ when _initialHtml.Length < 750 * 1024 => "500 KB",
            _ when _initialHtml.Length < 1_500_000 => "1 MB",
            _ => $"{_initialHtml.Length / (1024 * 1024)} MB"
        };

        /// <summary>
        /// Publishes the breakdown of the measured load.
        /// </summary>
        /// <param name="sizeLabel">The label for the document size.</param>
        /// <param name="probeResult">The outcome of the content probe.</param>
        private void PublishTimingBreakdown(string sizeLabel, ContentProbeResult probeResult)
        {
            if (_initialLoadStopwatch is null ||
                probeResult.FirstTextTimestamp == 0)
            {
                return;
            }

            // The stopwatch has already been stopped, so its elapsed ticks
            // are the total measured duration. The stopwatch's start
            // timestamp can be recovered from the observation timestamp the
            // probe recorded, because both live on the same clock
            // (Stopwatch.GetTimestamp):
            //
            //   start = observed - elapsed
            //
            // With that, the difference between the start and the control's
            // Loaded event gives the time the control spent being attached,
            // and the remainder is the time the content took to render after
            // the control already reported itself as loaded.
            long startTimestamp = probeResult.FirstTextTimestamp - _initialLoadStopwatch.ElapsedTicks;

            double totalMs = _initialLoadStopwatch.ElapsedTicks * 1000.0 / Stopwatch.Frequency;

            if (_loadedTimestamp != 0)
            {
                double loadedMs = (_loadedTimestamp - startTimestamp) * 1000.0 / Stopwatch.Frequency;

                if (loadedMs < 0)
                {
                    loadedMs = 0;
                }

                double gapMs = totalMs - loadedMs;
                if (gapMs < 0)
                {
                    gapMs = 0;
                }

                string gapLine =
                    $"  └ Loaded fired at {BenchmarkFormat.Milliseconds(loadedMs)} ms; " +
                    $"first text {BenchmarkFormat.Milliseconds(gapMs)} ms later";

                BenchmarkResultsSink.Publish(gapLine);
            }

        }

        /// <summary>
        /// Publishes the "the document kept changing" line once the probe has
        /// confirmed the document settled.
        /// </summary>
        /// <remarks>
        /// This is published separately from the headline number so that the
        /// banner can show the headline as soon as the first text is
        /// detected. The settle information arrives later and is appended
        /// afterwards.
        /// </remarks>
        /// <param name="probeResult">The outcome of the content probe.</param>
        private void PublishSettleLine(ContentProbeResult probeResult)
        {
            if (_initialLoadStopwatch is null ||
                !probeResult.Settled ||
                probeResult.SettledTimestamp == 0 ||
                probeResult.FirstTextTimestamp == 0)
            {
                return;
            }

            // If the document kept changing after the content was already in,
            // the control did more work behind the scenes. Report it, because
            // it is invisible to a user who only watches the text appear.
            double settleMs = (probeResult.SettledTimestamp - probeResult.FirstTextTimestamp)
                              * 1000.0 / Stopwatch.Frequency;

            if (settleMs <= 0)
            {
                return;
            }

            string settleLine =
                $"  └ document kept changing for another " +
                $"{BenchmarkFormat.Milliseconds(settleMs)} ms after the first text appeared";

            BenchmarkResultsSink.Publish(settleLine);
        }
    }
}
