# Maptifier

**Android-first projection mapping** — warp, mask, and blend images, video, SVG logos, and live text onto real-world surfaces via USB-C display output.

![Maptifier](Maptifier-1.png)

## Features

- **Multi-source layers** — Images, video, **SVG vector logos**, and **dynamic text** as mappable content
- **Dual-layer mixing** — Two layers (A/B) with crossfade and blend modes (Normal, Additive, Multiply, Screen, Overlay, Difference)
- **Warping** — Four-corner projective warp or mesh grid for complex surfaces
- **Masking** — Brush-based masks with polygon support
- **Drawing** — On-canvas drawing overlay per layer
- **Effects** — Blur, Chromatic Aberration, Color Cycle, Edge Glow, Kaleidoscope, Pixelate, Tunnel, Wave Distortion
- **External display** — Output to projector or second screen via USB-C
- **Project save/load** — Persist projects with media, warp, mask, and effect settings

## Requirements

- **Unity 6** (or Unity 2022 LTS+)
- **Android** — Target SDK 34, Minimum SDK 29
- **Device** — USB-C display output support (MHL/DisplayPort Alt Mode)

## Project Structure

```
Assets/_Project/
├── Scripts/
│   ├── Core/          # AppBootstrapper, ServiceLocator, EventBus
│   ├── Display/       # AndroidDisplayService, EditorDisplayService
│   ├── Layers/        # LayerManager, Layer compositing
│   ├── Media/         # ImageSource, VideoSource, VectorSource, TextSource
│   ├── Warping/       # WarpService, WarpMeshRenderer
│   ├── Masking/       # MaskService, EarClipTriangulator
│   ├── Drawing/       # DrawingService, DrawingCanvas
│   ├── Effects/       # EffectPipeline, Blur, Kaleidoscope, etc.
│   ├── Projects/      # ProjectManager, ExportService
│   └── UI/            # MainUIController, TextEditorPanel, Settings
├── Shaders/           # Compositing, Warp, Effects, Masking
└── UI/                # UXML layouts, USS theme
```

## Getting Started

1. Clone the repository
2. Open the project in Unity 6
3. Build for Android (File → Build Settings → Android)
4. Deploy to a device with USB-C display support
5. Connect a projector or external monitor via USB-C

## Architecture

- **ServiceLocator** — Single DI container; all services register and resolve via interfaces
- **EventBus** — ScriptableObject-based events for decoupling (e.g. `DisplayConnectedEvent`, `ToolChangedEvent`)
- **IMediaSource** — Unified interface for image, video, vector (SVG), and text sources; all feed `RenderTexture` into the layer pipeline

## License

See [LICENSE](LICENSE) for details.

## Feedback

[Open an issue](https://github.com/Danticipation/Maptifier/issues) or contribute via pull request.
