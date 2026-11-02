using System.Diagnostics;
using Syncfusion.Maui.RichTextEditor;

namespace SfRTEPerformance
{
    /// <summary>
    /// Detects when the <see cref="SfRichTextEditor"/> has actually ingested the
    /// document that was assigned to it — i.e. when the assigned content is
    /// really present in the editor's document model and not merely "requested".
    ///
    /// <para>
    /// Why this exists: <c>SfRichTextEditor.Loaded</c> fires when the MAUI
    /// visual tree has attached the control. The editor is a WebView hosting a
    /// ProseMirror instance, so the real content pipeline is:
    /// </para>
    /// <list type="number">
    ///   <item><description>the WebView is created and its HTML shell is loaded,</description></item>
    ///   <item><description>the JS bridge is wired and <c>window.RichTextEditor.initialize(...)</c> runs,</description></item>
    ///   <item><description>the HTML payload is parsed into the ProseMirror document,</description></item>
    ///   <item><description>the document is mounted and painted.</description></item>
    /// </list>
    /// <para>
    /// Steps 2 to 4 all happen <i>after</i> the control's <c>Loaded</c> event and
    /// are asynchronous. That is why stopping the stopwatch in <c>Loaded</c>
    /// under-reports the real cost: it measures the visual tree attach while
    /// the expensive document parse and render still happen afterwards.
    /// </para>
    ///
    /// <para>
    /// The probe closes that gap using only the control's public API. It polls
    /// <see cref="SfRichTextEditor.GetText"/> — which reads the live document
    /// back out of the editor through the JS bridge — and waits until the
    /// assigned payload's unique marker is present in that text. Only when the
    /// marker is observed can we say the document really was parsed and
    /// committed into the editor.
    /// </para>
    /// </summary>
    public static class ContentProbe
    {
        /// <summary>
        /// How long to keep polling before giving up on the editor ever
        /// acknowledging the content.
        /// </summary>
        public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

        /// <summary>
        /// How long to keep watching for the document to stop changing after
        /// the marker was first observed.
        /// </summary>
        public static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// The number of consecutive identical read-backs required before the
        /// document is considered settled.
        /// </summary>
        public const int SettleReadCount = 2;

        /// <summary>
        /// The shortest gap between two consecutive polls.
        /// </summary>
        public static TimeSpan MinPollInterval => ProbeTuning.MinPollInterval;

        /// <summary>
        /// The longest gap between two consecutive polls. This bounds how far
        /// past the true completion point the probe may report.
        /// </summary>
        public static TimeSpan MaxPollInterval => ProbeTuning.MaxPollInterval;

        /// <summary>
        /// The duration of the final (successful) read-back. Together with the
        /// poll interval this tells the caller how precisely the endpoint was
        /// pinned down.
        /// </summary>
        public static TimeSpan LastReadDuration { get; private set; }

        /// <summary>
        /// Polls <paramref name="editor"/> until the document it holds contains
        /// <paramref name="marker"/>, and stops <paramref name="stopwatch"/> at
        /// the moment the marker is first observed.
        /// </summary>
        /// <param name="editor">The editor to probe.</param>
        /// <param name="marker">
        /// A string that is guaranteed to be present in the assigned document.
        /// It must be plain text so that it survives the HTML-to-plain-text
        /// projection performed by <see cref="SfRichTextEditor.GetText"/>.
        /// </param>
        /// <param name="stopwatch">
        /// The stopwatch that was started when the document was assigned. It is
        /// stopped as soon as the marker is observed, so the caller reads the
        /// time to the initial text render and not the time to the assignment.
        /// </param>
        /// <param name="documentSizeBytes">
        /// The size of the assigned document in bytes. Used to scale the poll
        /// interval so that reading a large document back through the bridge
        /// does not dominate the measurement.
        /// </param>
        /// <param name="result">
        /// Receives the full probe outcome, including the settle timing.
        /// </param>
        /// <returns>
        /// <c>true</c> when the marker was observed within <see cref="Timeout"/>;
        /// <c>false</c> when the editor never acknowledged the content.
        /// </returns>
        public static async Task<bool> WaitForContentAsync(
            SfRichTextEditor editor,
            string marker,
            Stopwatch stopwatch,
            int documentSizeBytes,
            ContentProbeResult? result = null,
            Action<ContentProbeResult>? onFirstText = null,
            bool watchForSettle = true,
            Action<ContentProbeResult>? onSettled = null)
        {
            ArgumentNullException.ThrowIfNull(editor);
            ArgumentNullException.ThrowIfNull(stopwatch);
            ArgumentNullException.ThrowIfNull(marker);

            var deadline = DateTime.UtcNow + Timeout;

            // Start from an interval scaled to the document size: reading a
            // big document back through the WebView bridge is itself slow,
            // so polling it every 20 ms would spend most of the measurement
            // inside the probe.
            var interval = ProbeTuning.GetPollInterval(documentSizeBytes);
            int polls = 0;

            while (DateTime.UtcNow < deadline)
            {
                var readWatch = Stopwatch.StartNew();
                string currentText = string.Empty;

                try
                {
                    currentText = await editor.GetText();
                }
                catch
                {
                    // The bridge can be transiently unavailable while the
                    // WebView is still starting up, and platforms throw when
                    // a script is evaluated before the first navigation has
                    // completed. Treat that as "not ready yet" and retry.
                }

                readWatch.Stop();
                polls++;

                if (!string.IsNullOrEmpty(currentText) &&
                    currentText.Contains(marker, StringComparison.Ordinal))
                {
                    LastReadDuration = readWatch.Elapsed;

                    long observed = Stopwatch.GetTimestamp();

                    // The editor now holds the assigned text. This is the
                    // "initial text rendered" endpoint, and it is where the
                    // headline measurement stops.
                    stopwatch.Stop();

                    if (result is not null)
                    {
                        result.Acknowledged = true;
                        result.FirstTextTimestamp = observed;
                        result.CompleteTextTimestamp = observed;
                        result.PollCount = polls;
                    }

                    // Report the endpoint to the caller IMMEDIATELY, before
                    // any further probing happens.
                    //
                    // The settle watch below is diagnostic only - it reveals
                    // work the control does after the text is already on
                    // screen. It must never delay the headline number: the
                    // caller updates the banner from the result, so waiting
                    // for the settle would leave the banner showing nothing
                    // long after the user could already read the text.
                    //
                    // onFirstText is invoked synchronously here, on the same
                    // tick the endpoint was detected.
                    onFirstText?.Invoke(result);

                    // Now watch for the document to stop changing, which
                    // reveals any work the control still does after the
                    // content is already on screen.
                    if (watchForSettle)
                    {
                        long settled = await WaitForSettleAsync(
                            editor, currentText, interval, observed);

                        if (result is not null)
                        {
                            result.SettledTimestamp = settled;
                            result.Settled = settled != 0;
                        }

                        onSettled?.Invoke(result);
                    }

                    return true;
                }

                // Back off so a slow load is not hammered with full-document
                // read-backs, while a fast load is still pinned down tightly.
                interval = ProbeTuning.NextInterval(interval, readWatch.Elapsed);

                await Task.Delay(interval);
            }

            LastReadDuration = TimeSpan.Zero;
            stopwatch.Stop();

            if (result is not null)
            {
                result.Acknowledged = false;
                result.PollCount = polls;
            }

            return false;
        }

        /// <summary>
        /// Keeps reading the editor's document until two consecutive read-backs
        /// return identical text, which means the document has stopped
        /// changing.
        /// </summary>
        /// <param name="editor">The editor to watch.</param>
        /// <param name="lastText">The text observed when the marker appeared.</param>
        /// <param name="interval">The poll interval to use.</param>
        /// <param name="observedTimestamp">
        /// The timestamp at which the marker was observed. Used as the
        /// baseline for the settle timeout.
        /// </param>
        /// <returns>
        /// The timestamp at which the document settled, or zero if it did not
        /// settle within <see cref="SettleTimeout"/>.
        /// </returns>
        private static async Task<long> WaitForSettleAsync(
            SfRichTextEditor editor,
            string lastText,
            TimeSpan interval,
            long observedTimestamp)
        {
            var deadline = DateTime.UtcNow + SettleTimeout;
            int stableReads = 0;

            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(interval);

                string currentText = string.Empty;

                try
                {
                    currentText = await editor.GetText();
                }
                catch
                {
                    // A transient bridge failure is not evidence that the
                    // document changed. Keep waiting.
                    continue;
                }

                if (string.Equals(currentText, lastText, StringComparison.Ordinal))
                {
                    stableReads++;
                }
                else
                {
                    // The document changed again — the control is still
                    // working. Restart the stability count.
                    stableReads = 0;
                    lastText = currentText;
                }

                if (stableReads >= SettleReadCount)
                {
                    return Stopwatch.GetTimestamp();
                }
            }

            return 0;
        }
    }
}
