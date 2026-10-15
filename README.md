# Syncfusion .NET MAUI RichTextEditor Performance Benchmark

A simple, easy-to-understand sample that lets you independently measure and
validate the performance of the Syncfusion .NET MAUI `SfRichTextEditor`
control. The sample is intentionally lightweight — no MVVM, no DI, no
benchmarking framework, no telemetry. Just `Stopwatch`, async/await, and
straightforward pages.

## Purpose

Provide an open, reproducible way to measure three key performance
characteristics of `SfRichTextEditor`:

1. **Initial Load** — how long it takes, from the moment the user taps
   a button, to construct an editor page, instantiate the
   `SfRichTextEditor`, assign the chosen HTML payload to its `Value`
   property, lay it out, navigate to the page, and produce the first
   paint. Measured per size (empty / 10 KB / 500 KB / 1 MB)
   because the time depends on the document size.
2. **Dynamic Value Update** — how long it takes to assign a new HTML
   payload to the `Value` property of an already-loaded editor, across
   four document sizes (500 KB, 1 MB).
3. **Large Document Load** — how long it takes to load a 10,000-paragraph
   HTML document.

The numbers you see on screen are produced locally on your device or
emulator and depend on the host hardware, the OS, the available memory,
and whether the build is Debug or Release. Use the sample to compare
your environment against Syncfusion's published numbers, or to compare
different devices.

## Tested Scenarios

| Benchmark | Description | Document Sizes |
|-----------|-------------|----------------|
| Initial Load (per-size buttons) | From click to first paint, with the HTML pre-assigned in the editor page's constructor | Empty, 10 KB, 500 KB, 1 MB |
| Dynamic Value Update | Reassign `Value` on a loaded editor | 500 KB, 1 MB |
| 10,000 Paragraphs | Assign a large document to `Value` | 10,000 `<p>` elements |

## How the Timing Values Are Measured

All measurements use `System.Diagnostics.Stopwatch`.

* **Initial Load (per-size buttons)** — the stopwatch starts on
  `BenchmarkPage` **immediately before the `BenchmarkEditorPage`
  constructor** runs. The page's constructor itself assigns the chosen
  HTML to `richTextEditor.Value` *before* the page is added to the
  visual tree. The stopwatch then stops in
  `SfRichTextEditor.Loaded` event. The elapsed time therefore
  captures the entire "from click to control rendering" pipeline:
  page construction, `SfRichTextEditor` construction, the
  pre-assignment of HTML, the layout pass, the navigation animation,
  and the first paint.

* **Dynamic Value Update** — the stopwatch starts immediately before
  `richTextEditor.Value = html;` and stops immediately after the
  assignment. The editor is already on screen; the assignment is the
  only thing being measured.

* **10,000 Paragraphs** — the stopwatch starts immediately before
  `richTextEditor.Value = html;` and stops immediately after the
  assignment. The HTML is generated once, up front, so HTML generation
  is **not** included in the measurement.

## How to Run the Sample

1. Clone this repository.
2. Open `SfRTEPerformance.slnx` in Visual Studio 2022 (with the .NET MAUI
   workload installed) or run from the command line.
3. Restore NuGet packages — the project depends on
   `Syncfusion.Maui.RichTextEditor` (already referenced in the `.csproj`).
4. Select a target framework and run:
   * **Windows** — `net10.0-windows10.0.19041.0`
   * **Android** — `net10.0-android` (emulator or device)
   * **iOS** — `net10.0-ios` (simulator or device)
   * **macOS** — `net10.0-maccatalyst` (Mac required)
5. The app launches into the **Benchmarks** page. Tap a button to run a
   scenario. The editor page is pushed automatically, the benchmark
   runs against it, and the editor page **stays on screen** so you can
   inspect the loaded content. Results are appended to the on-screen
   results section on the benchmark page in real time. Use the
   **Back to Benchmarks** button (or your device's back gesture) to
   return to the benchmark page.

### Running from the command line

```powershell
dotnet build SfRTEPerformance/SfRTEPerformance.csproj -t:Run -f net10.0-windows10.0.19041.0
```

Replace the target framework with the one you want to run.

### UX details

* Tapping any benchmark button pushes the editor page, runs the
  measurement, and leaves the editor page on screen. The benchmark
  page never auto-pops, so you can review the editor's content.
* For the **Dynamic Value Update** buttons,
  the benchmark waits for the editor page to finish its first
  `OnAppearing` before measuring any `Value` assignment. This avoids
  measuring against a half-constructed control, which would otherwise
  produce bogus "0 ms" timings on the first assignment.
* The **Clear Results** button resets the on-screen results section.
* The results panel is fixed at 240 px tall and the buttons live in a
  scroll region above it, so the two never visually overlap.

## Supported Platforms

* Android (API 21+)
* iOS (15.0+)
* Windows (10.0.17763+)
* macOS / Mac Catalyst (15.0+)

## Project Layout

```
SfRTEPerformance/
├── BenchmarkPage.xaml / .xaml.cs        Main benchmark UI (buttons + results)
├── BenchmarkEditorPage.xaml / .xaml.cs  Editor host page that is timed
├── HtmlGenerator.cs                     Helpers that build the test HTML
├── BenchmarkResultsSink.cs              Tiny static sink so the editor
│                                        page can push result lines back
│                                        to the benchmark page
├── App.xaml / App.xaml.cs
├── AppShell.xaml / .xaml.cs             Shell that points to BenchmarkPage
├── MauiProgram.cs                       Registers the Syncfusion Core
│                                        handler via ConfigureSyncfusionCore()
└── SfRTEPerformance.csproj              NuGet refs (incl.
                                         Syncfusion.Maui.RichTextEditor)
```

## Example Output

```
======================================
Initial Load (500 KB)
======================================
Initial Load (500 KB): 210 ms

======================================
Dynamic Value Update
======================================
500 KB Value Update: 180 ms
1 MB Value Update: 420 ms

======================================
10,000 Paragraphs
======================================
10,000 Paragraph Document Load: 2450 ms
```

## Notes for an Honest Comparison

* Run a **Release** build when comparing numbers — Debug builds add JIT,
  XAML inflation, and logging overhead that distorts timings.
* Close any background applications on the target device so the
  measurement is not affected by CPU contention.
* For Android, prefer a physical device over an emulator; emulator
  numbers are typically much higher and not representative.
* Run each scenario more than once and use the median. The first run
  after a cold start is usually slower because of disk and runtime
  warm-up.
