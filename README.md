# Syncfusion .NET MAUI RichTextEditor Performance Benchmark

A simple, easy-to-understand sample that lets you independently measure and
validate the performance of the Syncfusion .NET MAUI `SfRichTextEditor`
control. The sample is intentionally lightweight — no MVVM, no DI, no
benchmarking framework, no telemetry. Just `Stopwatch`, async/await, and
straightforward pages.

## Purpose

Provide an open, reproducible way to measure two key performance
characteristics of `SfRichTextEditor`:

1. **Initial Load** — how long it takes, from the moment the user taps
   a button, to construct an editor page, instantiate the
   `SfRichTextEditor`, assign the chosen HTML payload to its `Value`
   property, lay it out, navigate to the page, and have the editor
   render its first text. Measured per scenario (empty / 1,000 / 5,000 /
   10,000 words) because the time depends on the document size.
2. **Dynamic Value Update** — how long it takes to assign a new HTML
   payload to the `Value` property of an already-loaded editor and have
   it render its first text. The benchmark page has a single "Run
   Dynamic Value Update" button; tapping it opens a freshly-primed
   editor page that exposes three per-scenario buttons (100 / 5,000 /
   10,000 words). Each tap on those buttons times one Value
   reassignment on the SAME editor instance. The reported number
   excludes the editor's initial-load time so it reflects only the
   reassignment itself.

The numbers you see on screen are produced locally on your device or
emulator and depend on the host hardware, the OS, the available memory,
and whether the build is Debug or Release. Use the sample to compare
your environment against Syncfusion's published numbers, or to compare
different devices.

## Tested Scenarios

| Benchmark | Description | Document Sizes |
|-----------|-------------|----------------|
| Initial Load (per-scenario buttons on the benchmark page) | From click to the editor's first text being rendered, with the HTML pre-assigned in the editor page's constructor | Empty, 1,000 / 5,000 / 10,000 words |
| Dynamic Value Update (three buttons on the editor page) | Reassign `Value` once on an already-loaded editor; only the reassignment time is measured | 100 / 5,000 / 10,000 words |
| Apply Format | From format-button click to the editor's `FormatChanged` event | N/A — operates on the seeded text |

## How the Timing Values Are Measured

All measurements use `System.Diagnostics.Stopwatch`.

### The measurement endpoint: "content acknowledged", not "control loaded"

This is the single most important detail in the sample, and it is the
reason the measurement is trustworthy.

`SfRichTextEditor.Loaded` **cannot** be used as the end of a load
measurement. The control is a `WebView` that hosts a ProseMirror
document, so `Loaded` fires when the MAUI visual tree attaches the
control — long before any content exists in the editor. The real
pipeline is:

```
  click
    │
    ├─ page + control construction          (synchronous)
    ├─ control attached to the visual tree   ──► Loaded fires HERE
    │                                          (editor still EMPTY)
    ├─ WebView loads its HTML shell          (async)
    ├─ JS bridge wired, initialize() runs    (async)
    ├─ HTML payload parsed into the document (async)  ◄─ the expensive part
    └─ document mounted and painted          (async)
    │
  content actually on screen
```

Stopping the stopwatch at `Loaded` therefore measures only steps 1–2 and
reports a number that has nothing to do with how long the content took
to load. This is exactly the problem the sample was written to fix.

### How the endpoint is detected

The sample detects the real endpoint using **only the control's public
API**:

1. A unique plain-text marker (e.g. `BENCHMARK-MARKER-1000Words-<guid>`)
   is embedded in the document that gets assigned.
2. The editor's live document text is polled through the public
   `SfRichTextEditor.GetText()` method, which reads the document back
   out of the WebView through the JavaScript bridge.
3. The stopwatch is stopped the instant the marker is observed in that
   text.

When the marker shows up in `GetText()`, the editor has begun rendering
text — so the measurement covers the whole pipeline above, including the
parse and render of the first text the user actually sees.

The marker is placed in its own paragraph at the very **start** of the
generated document on purpose. Observing the marker therefore measures
the time to the **first text rendered**, not the time to the whole
document being committed. This is the user-perceived time.

`ContentProbe` implements this. Two details keep the probe honest:

* **Unique marker per run.** The marker contains a GUID, so the probe can
  never match leftover content from a previous run instead of the
  document that was just assigned.
* **Fixed poll interval.** Reading a large document back through the
  WebView bridge is itself slow. A short fixed interval (50 ms) keeps
  the endpoint pinned down accurately without dominating the
  measurement.

### The other scenarios

* **Dynamic Value Update** — the benchmark page has one "Run Dynamic
  Value Update" button. Tapping it pushes a fresh editor page
  pre-loaded with a small (100-word) priming document and opens it in
  Dynamic Value mode (the three per-scenario buttons become visible on
  the editor page itself, below the result banner). The user then taps
  one of those buttons — `1000 Words`, `5,000 Words`, or `10,000 Words`
  — to reassign `Value` on the SAME editor instance. The stopwatch
  starts immediately before the assignment and stops when the new
  document's marker is observed in `GetText()`. The reported number is
  therefore the time for the single reassignment, not the time to
  construct the editor. Note that the assignment itself only *enqueues*
  the payload for the WebView; stopping at the end of the assignment
  would report the cost of an enqueue, not a load.
* **Apply Format** — measured from the format-button click to the
  `FormatChanged` event. This path is genuinely synchronous inside the
  control, so the event is a valid endpoint here.

Timings are reported with two decimal places. `Stopwatch.ElapsedMilliseconds`
is a `long` that truncates, which would render a fast operation as a
misleading "0 ms".

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
   **Back** button on the editor page (or your device's back gesture)
   to return to the benchmark page.

### Running from the command line

```powershell
dotnet build SfRTEPerformance/SfRTEPerformance.csproj -t:Run -f net10.0-windows10.0.19041.0
```

Replace the target framework with the one you want to run.

### UX details

* Tapping any benchmark button pushes the editor page, runs the
  measurement, and leaves the editor page on screen. The benchmark
  page never auto-pops, so you can review the editor's content.
* For **Dynamic Value Update**, the benchmark page has one button that
  opens a primed editor page; the three per-scenario buttons then live
  on the editor page itself, below the result banner. Tap them in any
  order to measure a Value reassignment for the matching word count on
  the same loaded editor.
* The three Dynamic Value buttons on the editor page are hidden for
  Initial Load scenarios — they only appear when the editor was opened
  in Dynamic Value mode.
* The benchmark page subscribes to the results sink in `OnDisappearing`
  and unsubscribes in `OnAppearing`. The subscription is therefore
  active for the entire window in which any editor or format page is
  on screen — including the Dynamic Value Update buttons, which publish
  results asynchronously, long after this page's click handler has
  returned.
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
├── BenchmarkFormatPage.xaml / .xaml.cs  Page for the Apply Format benchmark
├── ContentProbe.cs                      Polls the editor until the assigned
│                                        document's marker is visible (the
│                                        measurement endpoint)
├── BenchmarkFormat.cs                   Shared millisecond formatting
├── HtmlGenerator.cs                     Helpers that build the test HTML
│                                        (words only — paragraphs are still
│                                        available in code but unused)
├── BenchmarkResultsSink.cs              Tiny static sink so the editor
│                                        page can push result lines back
│                                        to the benchmark page
├── App.xaml / App.xaml.cs
├── AppShell.xaml / AppShell.xaml.cs     Shell that points to BenchmarkPage
├── MauiProgram.cs                       Registers the Syncfusion Core
│                                        handler via ConfigureSyncfusionCore()
└── SfRTEPerformance.csproj              NuGet refs (incl.
                                         Syncfusion.Maui.RichTextEditor)
```
## Example Output

```
======================================
Initial Load (Empty)
======================================
Empty: 12.40 ms

======================================
Initial Load (1,000 Words)
======================================
1,000 Words: 38.10 ms

======================================
Initial Load (5,000 Words)
======================================
5,000 Words: 210.44 ms

======================================
Dynamic Value Update
======================================
1000 Words Value Update: 8.10 ms
5,000 Words Value Update: 180.22 ms
10,000 Words Value Update: 420.91 ms

======================================
Apply Format
======================================
Format (Bold): 4.12 ms
```

The Dynamic Value Update section shows three measurements: one per tap on
the corresponding button (100 Words, 5,000 Words, 10,000 Words) on the
editor page, all on the same loaded editor instance.

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
* The probe's read-back polls the editor, so it adds a small amount of
  overhead to every measurement. The overhead is bounded by the fixed
  poll interval (50 ms) and is small relative to the load times being
  measured, so it does not distort the comparison between scenarios.
