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

### Current Status (Unity 6)

The project has been updated and verified to run in the **Unity 6 editor** on desktop. You can:

- Import images and videos into **Layer A** and **Layer B**.
- Crossfade and blend the two layers using the **A/B slider** and **Blend** dropdown.
- Use the main toolbar tools (Select, Warp, Mask, Draw, Text, Effects) with the in‑editor UI Toolkit layout.

Android builds and external display output are still the primary target, but the quickest way to explore Maptifier today is directly in the editor.

### Run in the Unity Editor

1. **Open the project**
   - Clone this repository.
   - Open the folder in **Unity 6**.
2. **Open the boot scene**
   - Load the scene that contains the **Bootstrap** GameObject (usually `Assets/_project/Scenes/Boot` or similar).
3. **Verify the Bootstrap object**
   - Select **Bootstrap** in the Hierarchy.
   - In the Inspector:
     - `App Bootstrapper` should be present (no overrides needed for basic use).
     - `UI Document` should have:
       - **Panel Settings**: `MaptifierPanelSettings`
       - **Source Asset**: `MainLayout` (`Assets/_project/UI/UXML/MainLayout.uxml`)
     - `Main UI Controller` should reference:
       - **Main Layout**: `MainLayout`
       - **Theme**: `MaptifierTheme`
4. **Press Play**
   - You should see the Maptifier UI (top bar, large canvas, bottom toolbar, and the right‑side **Layers** drawer).
5. **Load media into layers**
   - Open the **Layers** drawer (≡ button in the top bar if it’s hidden).
   - Click **Layer A** card → click the purple **+** in the top bar → choose an image or video.
   - Click **Layer B** card → purple **+** again → choose a *different* image or video.
6. **Mix and blend**
   - Use the **A–B slider** at the bottom to crossfade between layers.
   - Use **S** (Solo) and **M** (Mute) buttons on each layer to audition only A or B.
   - Change **Blend** (Normal, Screen, Multiply, Overlay, Difference) to see different compositing modes.

### Android Build (production target)

When you’re ready to deploy to a device:

- **Hardware**: Android device with **USB-C DisplayPort Alt Mode** (e.g., Samsung S‑series, Pixel 8, etc.).
- **Software**: Android 10 (API 29) or higher.

Build steps (high‑level):

1. Open the project in **Unity 6**.
2. Switch Build Target to **Android**.
3. Configure signing and store keys as needed.
4. Build an **Android App Bundle (AAB)** following `RELEASE_CHECKLIST.md`.
5. Install on a compatible phone, connect a USB‑C projector, and launch.

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
