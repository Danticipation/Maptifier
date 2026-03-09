# 🌌 Maptifier

**Professional Projection Mapping. In Your Pocket.**

Maptifier is a high-performance, Android-first projection mapping suite that turns your smartphone into a powerful media server. Warp, mask, and blend high-resolution content onto real-world surfaces with zero-latency output via USB-C.

![Maptifier](Maptifier-1.png)

---

## 🚀 Why Maptifier?

Projection mapping has traditionally required bulky laptops and expensive software. **Maptifier is the first professional-grade solution built from the ground up for Android.** 

By leveraging native Android hardware acceleration (`MediaCodec`) and a custom GPU-driven rendering pipeline, Maptifier delivers features previously reserved for desktop workstations:

- **Ultra-Portable**: Arrive at your gig with just a phone and a USB-C projector.
- **Low Latency**: Direct hardware-to-display output with optimized render-to-texture pipelines.
- **Unified Media**: Seamlessly blend 4K video, SVG vector logos, and dynamic live text.

---

## ✨ Pro Features

### 📐 Advanced Geometry
- **Dual-Mode Warping**: Choose between **Four-Corner Projective Warp** for flat surfaces or a **Custom Mesh Grid** with Catmull-Rom interpolation for curved objects.
- **Precision Masking**: Brush-based masks and polygon triangulation for isolating complex architectural details.
- **On-Canvas Drawing**: Annotate or draw custom light-overlays directly on top of your projection layers in real-time.

### 🎭 Multi-Source Mixing
- **A/B Dual-Layer Engine**: Professional crossfader with cinematic blend modes: *Normal, Additive, Multiply, Screen, Overlay, and Difference.*
- **SVG Vector Support**: Unlike raster-only tools, Maptifier renders SVG logos at native resolution, ensuring crisp edges at any projection size.
- **Live Text Rendering**: Update projection content on the fly for events and presentations.

### 🌪️ Real-time FX Pipeline
A stackable, high-performance effect chain including:
- **Distortion**: Kaleidoscope, Tunnel, Wave Distortion.
- **Stylization**: Pixelate, Chromatic Aberration, Edge Glow.
- **Movement**: Color Cycle, Blur, and more.

---

## 🛠️ The Tech Stack

Maptifier is engineered for stability and performance on the Android ecosystem:
- **Native Android Encoder**: Custom Java bridge to `MediaCodec` for high-bitrate MP4 exports.
- **Adaptive Quality Service**: Proactive resolution scaling to maintain a locked **60 FPS** regardless of effect load.
- **Homogeneous Math**: Robust Ear-Clipping and Bilinear interpolation engines for stable geometry.
- **Service-Oriented Architecture**: Decoupled `ServiceLocator` and `EventBus` design for modularity.

---

## 🏁 Getting Started

### Requirements
- **Hardware**: Android device with **USB-C DisplayPort Alt Mode** (e.g., Samsung S-series, Pixel 8, etc.).
- **Software**: Android 10 (API 29) or higher.

### Installation
1. Clone this repository.
2. Open in **Unity 6** (or 2022 LTS).
3. Build the **Android App Bundle (AAB)** using the provided `RELEASE_CHECKLIST.md`.
4. Connect your projector via USB-C and launch!

---

## 🔧 Troubleshooting

### Burst: "Failed to resolve assembly: 'Maptifier.Masking'"
If the Unity Console shows a `Mono.Cecil.AssemblyResolutionException` for `Maptifier.Masking` (or similar project assemblies), Burst is running before those assemblies are available. Two options:

1. **Disable Burst (quick fix)**  
   In the Unity menu: **Jobs > Burst > Enable Burst Compilation** — uncheck it. The project includes `Assets/Editor/DisableBurstOnLoad.cs`, which can also turn Burst off on load so the editor opens without this error. Re-enable Burst later when the resolution issue is fixed.

2. **Full recompile**  
   Close Unity, delete the project’s `Library` folder (or at least `Library/ScriptAssemblies`), then reopen the project so all assemblies rebuild. Fix any **red C# errors** in the Console first; Burst can’t resolve assemblies that failed to compile.

### "Internal build system error" / "Read the full binlog without getting a BuildFinishedMessage"
Usually means the build backend didn’t finish cleanly (e.g. process still running or crash). Try: close Unity, kill any leftover `Unity.exe` or `Unity.Backend.*` processes in Task Manager, then reopen the project. If it persists, clear the `Library` folder and reopen.

---

## 🤝 Contributing & Feedback

Maptifier is a unique tool in the mobile ecosystem. If you find a bug or have a feature request for a specific projection scenario:
- 🐛 **Issues**: [Open an issue](https://github.com/Danticipation/Maptifier/issues)
- 💡 **Discussions**: Reach out via the repository link.

---
*Created for the next generation of digital artists and guerrilla projectionists.*
