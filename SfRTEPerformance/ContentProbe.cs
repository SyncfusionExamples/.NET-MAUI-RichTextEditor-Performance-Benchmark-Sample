using System.Diagnostics;
using Syncfusion.Maui.RichTextEditor;

namespace SfRTEPerformance
{
    /// <summary>
    /// Waits until the <see cref="SfRichTextEditor"/> has rendered its
    /// first text after a document is assigned.
    /// </summary>
    /// <remarks>
    /// The control's <c>Loaded</c> event fires when the MAUI visual tree
    /// attaches the control, long before the WebView-hosted editor has parsed
    /// and rendered the document. Stopping a stopwatch at <c>Loaded</c> would
    /// miss the expensive parse and render that happen afterwards.
    ///
    /// The probe detects the real endpoint by polling the editor's public
    /// <see cref="SfRichTextEditor.GetText"/> until the assigned document's
    /// unique marker shows up in the text. The marker is placed at the very
    /// START of every generated document, so observing it means the editor
    /// has begun rendering text — the headline measurement is the time to
    /// the first text on screen, not the time to the whole document.
    /// </remarks>
    public static class ContentProbe
    {
        /// <summary>How long to keep polling before giving up.</summary>
        public const int TimeoutSeconds = 60;
        public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(TimeoutSeconds);

        // The poll interval bounds how late the reported endpoint can be.
        // 50 ms is small enough that timings stay accurate and large enough
        // that the probe's own polling does not dominate the measurement.
        private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

        /// <summary>
        /// Polls the editor until its text contains <paramref name="marker"/>,
        /// then stops <paramref name="stopwatch"/>.
        /// </summary>
        /// <returns>
        /// <c>true</c> when the marker was observed within
        /// <see cref="Timeout"/>; <c>false</c> when the editor never
        /// acknowledged the content.
        /// </returns>
        public static async Task<bool> WaitForContentAsync(
            SfRichTextEditor editor,
            string marker,
            Stopwatch stopwatch)
        {
            ArgumentNullException.ThrowIfNull(editor);
            ArgumentNullException.ThrowIfNull(marker);
            ArgumentNullException.ThrowIfNull(stopwatch);

            var deadline = DateTime.UtcNow + Timeout;

            while (DateTime.UtcNow < deadline)
            {
                string text = string.Empty;
                try
                {
                    text = await editor.GetText();
                }
                catch
                {
                    // The JS bridge can be transiently unavailable while
                    // the editor is still starting up. Treat that as
                    // "not ready yet" and keep polling.
                }

                if (!string.IsNullOrEmpty(text) && text.Contains(marker))
                {
                    stopwatch.Stop();
                    return true;
                }

                await Task.Delay(PollInterval);
            }

            stopwatch.Stop();
            return false;
        }
    }
}