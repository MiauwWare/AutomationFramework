# AutomationFramework

`AutomationFramework` is a Windows desktop-automation library. It provides native mouse and keyboard input, window helpers, geometry/retry utilities, and a scale-aware OpenCV template-matching system intended for UI and game automation.

The project targets `net10.0-windows`. It uses `System.Drawing` for capture and `OpenCvSharp` for image processing and matching.

## Capabilities

- Move, click, and drag the cursor with `Cursor`.
- Send key-down, key-up, and key-press input with `Keyboard` and `VirtualKey`.
- Find, restore, minimize, and focus native windows through `Windows.WinWindowManager`.
- Retry asynchronous or synchronous operations with `Utils.Retry`.
- Manipulate screen rectangles with the extensions in `Extensions/`.
- Capture the virtual desktop or a selected region with `Vision`.
- Locate image templates despite Windows DPI scaling and moderate contrast changes.

## Vision quick start

```csharp
using AutomationFramework;
using AutomationFramework.Templates;
using AutomationFramework.VisionModels;

var templates = new VisionTemplateResourceManager();
using var vision = new Vision(templates, new VisionOptions
{
    EnableEdgeVerification = true,
    TemplateDpi = 96
});

// Relative names resolve from VisionOptions.TemplateDirectory.
using var playButton = vision.AcquireTemplate("play-button.png");

var match = vision.Find(playButton, new VisionSearchOptions
{
    MinimumConfidence = 0.85,
    SearchRegion = new Rectangle(0, 0, 1920, 1080)
});

if (match is not null)
{
    var screenBounds = match.GlobalBounds;
    // Use Cursor to click screenBounds.Center().
}
```

`VisionMatchResult.Bounds` is local to `SearchRegion`; use `GlobalBounds` when passing coordinates to mouse/window APIs.

For a live non-destructive check in the sample runner, run `vision-smoke-test` while the configured template is visible. It logs the template's DPI metadata and the match result without sending mouse or keyboard input.

## Vision pipeline

Each call to `Vision.Find` follows this flow:

1. Capture the requested screen region once.
2. Build grayscale, CLAHE-normalized, and edge representations once.
3. Build a four-level image pyramid for the screenshot.
4. Read the template image DPI metadata when it was acquired. If it is absent, use `VisionOptions.TemplateDpi` (96 by default).
5. Generate scales starting near the DPI estimate and search those scales in parallel.
6. Search the smallest pyramid level globally, then project the candidate into a small region on each finer level.
7. Verify the edge score at the exact grayscale candidate location.
8. Return the highest-confidence result that meets `MinimumConfidence`.

The screenshot is never resized for scale search. Instead, the template is scaled and cached.

## Templates and leases

Acquire templates through `Vision.AcquireTemplate` and dispose the returned `VisionTemplateLease` when finished:

```csharp
using var template = vision.AcquireTemplate(templatePath);
var result = vision.Find(template);
```

The resource manager reference-counts templates by full file path. While at least one lease exists, it caches:

- Original BGR image
- Grayscale image
- CLAHE image
- Edge image
- Base grayscale pyramid
- Scaled template pyramids, keyed by scale, normalization tolerance, and representation

Disposing the final lease frees all cached unmanaged OpenCV `Mat` instances. Treat Mats exposed by a lease as read-only and do not dispose them yourself.

### DPI metadata

PNG (`pHYs`) and JPEG density metadata is read during template acquisition and exposed through `VisionTemplateLease.TemplateDpi`. OpenCV itself does not expose this metadata, so the manager reads it separately through GDI+.

If metadata is missing, corrupt, or has different horizontal/vertical DPI, `TemplateDpi` is `null` and the framework uses the configured fallback DPI.

## Vision configuration

`VisionOptions` controls matcher-wide behaviour:

| Option | Default | Purpose |
| --- | ---: | --- |
| `TemplateMatchMode` | `CCoeffNormed` | OpenCV template-matching metric. |
| `TemplateDirectory` | `Assets/Templates` | Base directory for relative template filenames. |
| `EnableEdgeVerification` | `true` | Blends a same-location edge score into candidate confidence. |
| `MaxDegreeOfParallelism` | CPU count | Maximum concurrent scale-search workers. |
| `PyramidLevels` | `4` | Screenshot pyramid depth. |
| `TemplateDpi` | `96` | Fallback when image metadata is unavailable. |
| `MinimumRelativeTemplateSize` | `0.4` | Rejects a scale that reduces either template dimension below 40% of its original size. |

`VisionSearchOptions` controls an individual lookup:

| Option | Default | Purpose |
| --- | ---: | --- |
| `SearchRegion` | virtual desktop | Limits capture and matching to a region. |
| `MinimumConfidence` | `0.8` | Required confidence in the range 0–1. |
| `MinimumScale` / `MaximumScale` | `0.25` / `2.0` | Allowed template scale range. |
| `ScaleStep` | `0.05` | Distance between candidate scales. |
| `ColorTolerance` | `0` | Blends grayscale toward cached CLAHE normalization from 0–1. |

## Project layout

```text
AutomationFramework/
├── Vision.cs                 # Small public capture/match façade
├── Matching/                 # Scale generation, parallel search, pyramid refinement, verification
├── Processing/               # Gray, CLAHE, edges, pyramids, DPI estimation
├── Templates/                # Leases, resource manager, and scaled-template cache
├── Models/                   # Search configuration and match results
├── Windows/                  # Win32 input, window, and screen-metric helpers
├── Extensions/               # Geometry, task, time, and random helpers
├── Cursor.cs                 # Mouse control
└── Keyboard.cs               # Keyboard control
```

## Notes and limitations

- This is a Windows-only library.
- Template matching is sensitive to rotation and substantial visual redesigns. ORB/AKAZE fallback is a planned future extension.
- DPI metadata is a starting estimate, not a guarantee. Keep an appropriate scale range for applications that apply their own UI scaling.
- Restrict `SearchRegion` whenever possible; it has the largest effect on matching speed and false-positive reduction.
