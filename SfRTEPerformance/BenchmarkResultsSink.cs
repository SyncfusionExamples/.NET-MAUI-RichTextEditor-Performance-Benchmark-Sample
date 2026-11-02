namespace SfRTEPerformance
{
    /// <summary>
    /// Simple shared message sink used by <see cref="BenchmarkPage"/> and
    /// <see cref="BenchmarkEditorPage"/> to publish benchmark result lines
    /// without introducing MVVM, dependency injection, or messaging frameworks.
    /// The <see cref="BenchmarkPage"/> subscribes to <see cref="LinePublished"/>
    /// before pushing the editor page, and unsubscribes after.
    /// </summary>
    public static class BenchmarkResultsSink
    {
        public static event Action<string>? LinePublished;

        public static void Publish(string line)
        {
            LinePublished?.Invoke(line);
        }
    }
}