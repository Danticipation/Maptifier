# Maptifier Release Checklist

## Build Configuration
- [ ] Version code incremented
- [ ] Version name updated (semantic versioning)
- [ ] IL2CPP backend selected
- [ ] ARM64 architecture only
- [ ] Stripping level: High (with link.xml preservations)
- [ ] Release signing keystore configured (NOT committed to repo)
- [ ] ProGuard/R8 rules tested (proguard-user.txt)
- [ ] Target SDK: 34
- [ ] Minimum SDK: 29

## Asset Optimization
- [ ] AAB base size <= 95 MB
- [ ] Asset packs <= 40 MB total
- [ ] All textures compressed with ASTC
- [ ] Shader variants stripped (fog, lightmaps, etc.)
- [ ] No unused assets in build

## Functionality Verification
- [ ] All features work in release build
- [ ] JSON serialization works with R8 enabled
- [ ] Shader loading works with stripping enabled
- [ ] External display connects and renders
- [ ] Video import and playback works
- [ ] Drawing engine responsive
- [ ] All 8 effects render correctly
- [ ] Dual-layer mixing with crossfade
- [ ] Project save/load roundtrip
- [ ] Video export produces valid MP4
- [ ] Screenshot export saves to gallery

## No Debug Artifacts
- [ ] No Debug.Log output in release
- [ ] No development watermarks
- [ ] No placeholder content
- [ ] No debug overlay visible
- [ ] Performance mode toast only on first switch
- [ ] Onboarding shows only on first launch

## Analytics & Crash Reporting
- [ ] Firebase Analytics events firing
- [ ] Firebase Crashlytics working (test crash)
- [ ] Analytics consent dialog (if EU distribution)
- [ ] No PII in analytics events

## Store Listing
- [ ] 5-8 screenshots (phone format)
- [ ] Feature graphic (1024x500)
- [ ] App icon (512x512 hi-res)
- [ ] Short description (80 char max)
- [ ] Full description with keywords
- [ ] Demo video (30-60 seconds)
- [ ] Privacy policy URL set
- [ ] Content rating completed (IARC)
- [ ] Category: Tools or Video Players

## Device Testing
- [ ] Samsung Galaxy S23/S24 (flagship)
- [ ] Samsung Galaxy A54/A55 (mid-range)
- [ ] Google Pixel 7a/8a (mid-range)
- [ ] Xiaomi Redmi Note 12/13 Pro
- [ ] OnePlus Nord 3/4
- [ ] Samsung Galaxy Tab S9

## Pre-Launch
- [ ] Firebase Test Lab crawl test (10+ devices)
- [ ] Pre-launch report reviewed
- [ ] All critical issues resolved
- [ ] Play Store listing preview approved
