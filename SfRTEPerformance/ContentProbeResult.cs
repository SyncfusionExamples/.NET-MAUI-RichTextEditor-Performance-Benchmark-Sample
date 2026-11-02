using System.Diagnostics;

namespace SfRTEPerformance
{
    /// <summary>
    /// The outcome of probing an editor for the document it has ingested.
    /// </summary>
    /// <remarks>
    /// This type carries no platform dependency, so the measurement
    /// semantics it encodes can be verified by a plain test harness
    /// without a MAUI platform or a real editor instance.
    /// </remarks>
    public sealed class ContentProbeResult
    {
        /// <summary>
        /// <c>true</c> when the editor acknowledged the content within the
        /// probe's timeout.
        /// </summary>
        public bool Acknowledged { get; set; }

        /// <summary>
        /// The moment the editor first reported text, as a
        /// <see cref="Stopwatch.GetTimestamp"/> value. Zero when the probe
        /// timed out.
        /// </summary>
        /// <remarks>
        /// This is the "initial text rendered" endpoint — the first moment
        /// the editor holds any of the assigned content. It is the headline
        /// measurement.
        /// </remarks>
        public long FirstTextTimestamp { get; set; }

        /// <summary>
        /// The moment the whole document was present, as a
        /// <see cref="Stopwatch.GetTimestamp"/> value. Zero when the probe
        /// timed out.
        /// </summary>
        /// <remarks>
        /// For this control the document is committed atomically, so this
        /// normally equals <see cref="FirstTextTimestamp"/>. It is tracked
        /// separately so that a future change which streamed content in
        /// would show up as a non-zero difference rather than silently
        /// skewing the headline number.
        /// </remarks>
        public long CompleteTextTimestamp { get; set; }

        /// <summary>
        /// The number of read-backs performed before the content was seen.
        /// </summary>
        public int PollCount { get; set; }

        /// <summary>
        /// The moment the document stopped changing, as a
        /// <see cref="Stopwatch.GetTimestamp"/> value. Zero when the editor
        /// never settled within the settle timeout.
        /// </summary>
        public long SettledTimestamp { get; set; }

        /// <summary>
        /// <c>true</c> when the document was observed to stop changing after
        /// the content appeared.
        /// </summary>
        public bool Settled { get; set; }
    }
}
