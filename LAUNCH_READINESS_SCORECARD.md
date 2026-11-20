# Maptifier Launch Readiness Scorecard

Last updated: 2026-03-25

## Overall Confidence

- Current confidence: **Medium-Low**
- Rationale: core functionality is broad, but release hardening and device-level validation are incomplete.

## Critical Blockers (Must close before public paid launch)

- [ ] Scripted Android build path is validated end-to-end in CI after path casing updates.
- [ ] Telemetry symbols are confirmed in Player Settings for release (`FIREBASE_ANALYTICS` and/or `MAPTIFIER_ANALYTICS`, `FIREBASE_CRASHLYTICS` and/or `MAPTIFIER_CRASHLYTICS`).
- [ ] External display reliability pass on target devices/adapters (plug/unplug stress and reconnect recovery).
- [ ] Export validation on real Android devices (screenshot + video artifacts open and play correctly).
- [ ] Crash-free smoke pass on release build (not editor) with no blocking regressions.

## High Priority Risks

- [ ] Preview rendering path in UI should be performance-profiled on mid-range hardware.
- [ ] Device matrix coverage is not yet fully evidenced in repository artifacts.
- [ ] Store submission assets and listing copy should be finalized and quality-checked.

## Verification Evidence Checklist

- [ ] Unity Android build log attached for scripted build.
- [ ] Test run notes for at least top 5 target devices.
- [ ] Export proof files captured from device gallery.
- [ ] Crash/analytics test events visible in Firebase dashboard.
- [ ] Play Console pre-launch report reviewed with no critical issues.

## Next Actions (Execution Order)

1. Run scripted Android build (`BuildScripts.BuildAndroidAab`) and archive logs.
2. Verify scripting define symbols in Unity Player Settings for release.
3. Execute device smoke matrix (import, warp, mask, draw, export, external display).
4. Resolve any P0/P1 defects discovered and re-run smoke matrix.
5. Submit internal/closed testing track build in Play Console.
6. Review pre-launch report, fix critical issues, then proceed to production rollout.
