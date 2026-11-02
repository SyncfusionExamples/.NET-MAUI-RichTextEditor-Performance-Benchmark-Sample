using System.Globalization;

namespace SfRTEPerformance
{
    /// <summary>
    /// Shared formatting helpers for the benchmark result lines, so every
    /// benchmark in the sample reports timings the same way.
    /// </summary>
    public static class BenchmarkFormat
    {
        /// <summary>
        /// Formats a stopwatch reading as a millisecond string with two
        /// decimal places.
        /// </summary>
        /// <remarks>
        /// <see cref="System.Diagnostics.Stopwatch.ElapsedMilliseconds"/> is a
        /// <c>long</c> and truncates to whole milliseconds. On fast hardware a
        /// simple operation easily completes in under 1 ms, which would display
        /// as "0 ms" and look like the benchmark never ran. Dividing elapsed
        /// ticks by the stopwatch frequency keeps sub-millisecond precision.
        /// </remarks>
        /// <param name="stopwatch">The stopwatch to read.</param>
        /// <returns>A string such as "0.42" or "1234.56".</returns>
        public static string Milliseconds(System.Diagnostics.Stopwatch stopwatch)
        {
            double ms = stopwatch.ElapsedTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            return ms.ToString("0.00", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Formats an elapsed duration as a millisecond string with two
        /// decimal places.
        /// </summary>
        /// <param name="elapsed">The duration to format.</param>
        /// <returns>A string such as "0.42" or "1234.56".</returns>
        public static string Milliseconds(TimeSpan elapsed)
        {
            return elapsed.TotalMilliseconds.ToString("0.00", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Formats a millisecond count as a string with two decimal places.
        /// </summary>
        /// <param name="milliseconds">The millisecond count to format.</param>
        /// <returns>A string such as "0.42" or "1234.56".</returns>
        public static string Milliseconds(double milliseconds)
        {
            return milliseconds.ToString("0.00", CultureInfo.InvariantCulture);
        }
    }
}
