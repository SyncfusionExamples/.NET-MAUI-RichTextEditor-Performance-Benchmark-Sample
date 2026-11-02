namespace SfRTEPerformance
{
    /// <summary>
    /// The tuning constants and arithmetic that govern how often the
    /// <see cref="ContentProbe"/> polls the editor.
    /// </summary>
    /// <remarks>
    /// This lives apart from <see cref="ContentProbe"/> so that the timing
    /// maths can be exercised by a plain test harness, without needing a
    /// MAUI platform or a real editor instance.
    /// </remarks>
    public static class ProbeTuning
    {
        /// <summary>
        /// The gap between two consecutive polls.
        /// </summary>
        /// <remarks>
        /// This is deliberately short. It is the dominant source of error in
        /// the measurement: the endpoint cannot be detected sooner than the
        /// next poll, so the reported time is at most one interval later
        /// than the true render.
        /// </remarks>
        public static readonly TimeSpan MinPollInterval = TimeSpan.FromMilliseconds(20);

        /// <summary>
        /// The longest gap between two consecutive polls. The probe only
        /// reaches this when a read-back itself was slow.
        /// </summary>
        public static readonly TimeSpan MaxPollInterval = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// Computes the initial poll interval for a document of the given size.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A flat interval is correct here — <b>not</b> a size-scaled one.
        /// </para>
        /// <para>
        /// The read-back only becomes expensive once the editor actually holds
        /// the document. Every poll before that returns an empty string and
        /// costs a couple of milliseconds at most, no matter how big the
        /// document is going to be. The document arrives in a single atomic
        /// commit, so there is exactly one expensive read-back — the one
        /// that detects it — and the probe stops immediately afterwards.
        /// </para>
        /// <para>
        /// A size-scaled interval (the previous behaviour) quantised the
        /// measurement: a 1 MB document polled every 128 ms could report a
        /// render time up to 128 ms too late, which is why the number in the
        /// banner did not line up with what the user saw.
        /// </para>
        /// </remarks>
        /// <param name="documentSizeBytes">The assigned document size in bytes.</param>
        /// <returns>The poll interval to use.</returns>
        public static TimeSpan GetPollInterval(int documentSizeBytes) => MinPollInterval;

        /// <summary>
        /// Computes the next poll interval.
        /// </summary>
        /// <remarks>
        /// The interval only grows when the last read-back was itself slow
        /// (twice its duration). Otherwise it stays tight, so the endpoint
        /// keeps being pinned down accurately. Growing the interval
        /// unconditionally would re-introduce the quantisation this class
        /// exists to avoid.
        /// </remarks>
        /// <param name="current">The interval used for the last poll.</param>
        /// <param name="lastReadDuration">How long the last read-back took.</param>
        /// <returns>The interval to use for the next poll.</returns>
        public static TimeSpan NextInterval(TimeSpan current, TimeSpan lastReadDuration)
        {
            // Never poll faster than the read itself takes.
            double minMs = lastReadDuration.TotalMilliseconds * 2.0;

            double nextMs = current.TotalMilliseconds;

            if (nextMs < minMs)
            {
                nextMs = minMs;
            }

            if (nextMs > MaxPollInterval.TotalMilliseconds)
            {
                nextMs = MaxPollInterval.TotalMilliseconds;
            }

            if (nextMs < MinPollInterval.TotalMilliseconds)
            {
                nextMs = MinPollInterval.TotalMilliseconds;
            }

            return TimeSpan.FromMilliseconds(nextMs);
        }
    }
}
