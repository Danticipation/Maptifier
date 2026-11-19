# Maptifier — Launch Plan

> **Owner:** Founder (Danticipation / Maptifier)
> **Repo:** `E:/My-Applications/Maptifier`
> **Engine:** Unity 6.3.10f1 (Unity 6.3 LTS) · URP 17.3.0 · Android-first
> **Date:** 2026-11-18

---

## Executive Summary

### Current state

Maptifier is a Unity 6-based, Android-first projection-mapping application with a
well-modular C# architecture (per-context Assembly Definitions, ServiceLocator,
EventBus, interfaces-first services, UI Toolkit UX/USS).

The project's own roadmap (Jira import: 8 epics — MAPT-SETUP, MAPT-INFRA, MAPT-MAPPING,
MAPT-CREATIVE, MAPT-LAYERS, MAPT-EXPORT, MAPT-POLISH, MAPT-TEST, MAPT-RELEASE; ~194 story
points total) indicates that **the core engine, media, mapping, creative, layer, and export
work (roughly Phases 1–6) is substantially implemented** in code.

What exists today:

- Display service (Android `DisplayManager`/`Presentation`, hot-plug events, editor mock).
- Composite renderer, render texture pool, custom shaders (warp, mask, draw, compositing, effects).
- Media import (gallery, image, video, SVG, text), layers A/B, 6 blend modes, crossfade.
- 8 GPU effects; max 3 effects/layer; adaptive quality, thermal/perf/ANR monitors.
- Projects (JSON save/load/autosave/thumbnail), screenshot + video export.
- UI Toolkit: main UI, onboarding, coach marks, settings, text editor.
- Analytics + crash handling scaffold (Firebase behind `#if` defines; consent via PlayerPrefs).

### Readiness verdict

**The app is not launch-ready today.** The feature code appears substantially complete, but
launch-critical operational readiness is immature or unverified:

- Build entry-point wiring is inconsistent (EditorBuildSettings → `Scenes/MAPTIFIER-SCENE`,
  `BuildScripts` → `Scenes/Boot`, two boot scenes, bootstrap wiring partly manual).
- Release configuration (IL2CPP/ARM64/stripping/R8/ProGuard/`link.xml`) unverified; release build
  not built/validated; size budget unverified.
- Store assets missing (screenshots, feature graphic, icon/splash, demo video).
- Privacy policy exists as local HTML only; not hosted; retention/consent/PII-event handling
  needs tightening.
- Monetization: **not implemented** (no IAP code despite `com.unity.purchasing` in manifest).
- CI: build-only workflows; no test-run CI; release automation absent.

---

## Codebase & Readiness Assessment

### Architecture / tech stack

- **Engine:** Unity 6.3.10f1 (Unity 6.3 LTS), URP 17.3.0, Linear color space.
- **Language:** C# with modular asmdefs per bounded context.
- **Patterns:** ServiceLocator (single DI), EventBus, interfaces-first, thin MonoBehaviours,
  ScriptableObject events, `AppConfig` tuning asset.
- **UI:** Unity UI Toolkit (UXML + USS).

### Strengths

- Strong architectural discipline documented in `CODING_STANDARDS.md`.
- Interface-first services (enables editor mocks / testing).
- No backend dependency (favorable for low-touch ops).
- Existing stability stack (exception handler + recovery dialog, ANR watchdog, thermal monitor,
  performance monitor, shader warmup, adaptive quality).

### Weaknesses / debt

- Large UI MonoBehaviours (e.g., ~800-line `MainUIController`) with inline caching/wiring.
- `CoachMarkManager` uses `FindObjectOfType` despite coding standards prohibition.
- `JsonUtility`-based project serialization (no polymorphism/dictionaries).
- Minimal automated test coverage (3 edit-mode tests); no device-farm CI.
- Known fragility: Burst + assembly resolution error path (documented in README).

### Feature completeness vs. roadmap

| Area | Status |
|---|---|
| Core engine / display / input / composite | Implemented |
| Warp / mask / drawing / effects / layers | Implemented |
| Media import / projects / export | Implemented |
| UI / onboarding / coach marks / settings / analytics | Implemented |
| Templates, splash, full release QA, monetization, store | **Not done / unverified** |

---

## Critical Gaps

### Launch-blocking

1. **Build/scene wiring inconsistency** (highest priority).
2. Release build config & signed AAB not validated; `proguard-user.txt` and PlayerSettings
   state needs verification.
3. Store assets & hosted privacy policy missing.
4. Content rating (IARC) & export compliance not finalized.
5. Firebase/Crashlytics release wiring unverified.

### Product / compliance / monetization

1. No monetization implementation.
2. Privacy policy claims (retention, no-PII) vs. analytics events that may include
   user-generated project names — needs alignment.
3. EU consent flow user-facing status unclear.

### Technical debt / risk

1. Minimal test/CI safety net.
2. Performance claims unverified on hardware.

---

## Recommended Go-to-Market Plan (Phased, Automation-First)

### Phase 0 — Weeks 0–2: Known-good internal build

- Align EditorBuildSettings + BuildScripts + scenes; verify clean boot; run tests.
- Clean stale docs.
- Add release-build CI skeleton.

**Effort:** ~40–70 founder hrs.

### Phase 1 — Weeks 2–6: Release candidate & store readiness

- Version 1.0.0; configure IL2CPP/ARM64/stripping/R8/link.xml; build signed AAB; internal QA.
- Store assets, hosted privacy policy, content rating, analytics/consent, export compliance.
- Firebase prod wiring; Play Console internal track.

**Effort:** ~60–90 founder hrs (+ optional designer).

### Phase 2 — Weeks 6–9: Public launch

- Closed testing → Open Testing → Production on Google Play.
- Complete store listing, ASO, demo video.

**Effort:** ~30–50 founder hrs.

### Phase 3 — Post-launch & monetization

- Freemium / Pro IAP (or paid-first); content engine; iterate via CI builds.

---

## Technical Recommendations to Enable Low-Touch Operation

1. Release CI/CD: tag → AAB → artifact / Play internal track.
2. Build pipeline correctness: single canonical scene for builds.
3. No-backend retention; `AppConfig`-driven tuning.
4. Firebase/consent/PII cleanup.
5. Monitoring: Crashlytics alerts + Play Console + in-app recovery.

---

## Prioritized Action List

1. **First:** Fix build/entry wiring + docs; run tests. *(Launch-blocking)*
2. **Second:** Release candidate build + validation.
3. **Third:** Store & launch readiness → launch.

---

## Success Metrics (minimal human review)

- Crash-free ≥ 99.5%; ANR low; cold start < 3 s; 60 fps sustained; memory < 512 MB.
- Funnel: onboard → display connected → import → export.
- Business: installs, DAU/MAU, rating, conversion, uninstall.

---

## Risks & Mitigations

| Risk | Mitigation |
|---|---|
| Release build failure / wrong scene | CI + editor automation |
| Store rejection | Pre-launch report, compliance checklist, closed testing |
| Device-specific display bugs | Firebase Test Lab, closed testing, Crashlytics |
| Performance miss | AdaptiveQuality, Performance Mode, Test Lab, monitoring |

---

## Timeline & Effort Summary

- **To public launch:** ~8–12 weeks (range ~6–14 weeks).
- **Founder effort pre-launch:** ~130–210 hrs.
- **Post-launch founder time target:** ~5–10 hrs/week.

---

## Bottom Line

The codebase is far from launch-ready operationally, though the core feature set appears
substantially built. The shortest realistic path to a viable public launch is Phase 0 → Phase 2
above, without completing the remaining roadmap epics.

---

*See also: `README.md`, `RELEASE_CHECKLIST.md`, `RUN_FIRST_TIME.md`, `SETUP_BOOTSTRAP.md`,
`privacy-policy.html`, `Maptifier — Production-Grade Phase Roadmap.docx`,
`maptifier_full_jira_import.csv`.*
