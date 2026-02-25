# Maptifier Coding Standards

## Architecture

- **Pattern**: Lean MVC with ScriptableObject-based events for decoupling.
- **Dependency Injection**: `ServiceLocator` (in `Core/`) is the **only** allowed singleton. All services register via `ServiceLocator.Register<T>()` and resolve via `ServiceLocator.Get<T>()`.
- **No other singletons**. No `static` service references. No `FindObjectOfType` in hot paths.
- **Interfaces first**: Every service must have an interface (`IDisplayService`, `IInputService`, etc.) to enable editor mocks and testing.

## Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Namespace | `Maptifier.{Module}` | `Maptifier.Display` |
| Class / Struct | PascalCase | `WarpMesh`, `LayerMixer` |
| Interface | `I` + PascalCase | `IDisplayService` |
| Public method | PascalCase | `ConnectDisplay()` |
| Private field | `_camelCase` | `_compositeRT` |
| Local variable | camelCase | `frameTime` |
| Constant | PascalCase | `MaxEffectsPerLayer` |
| Enum | PascalCase (type and values) | `BlendMode.Additive` |
| Event | `On` + PascalCase | `OnDisplayConnected` |
| ScriptableObject event | `{Action}Event` | `DisplayConnectedEvent` |
| Shader property | `_PascalCase` | `_MixValue` |
| USS class | `maptifier-{element}--{modifier}` | `maptifier-toolbar--active` |

## File Organization

- One public class per file, filename matches class name.
- Assembly definitions per module (`Maptifier.Core.asmdef`, `Maptifier.Display.asmdef`, etc.).
- Keep `MonoBehaviour`s thin — delegate logic to plain C# service classes.
- Shaders in `Assets/_Project/Shaders/{Category}/`.
- UI layouts in `Assets/_Project/UI/UXML/`, styles in `Assets/_Project/UI/USS/`.

## Performance Rules

- **Zero per-frame GC allocations** on hot paths. Use `NativeArray`, `NativeList`, `stackalloc`, `StringBuilder`.
- **No `GetComponent<T>()` in Update/LateUpdate** — cache in `Awake()` or `OnEnable()`.
- **No `string` concatenation in loops** — use `StringBuilder` or `Span<char>`.
- **`half` precision** in all shaders except UV coordinates.
- **No branching in fragment shaders** — use `step()`, `lerp()`, `saturate()`.
- **`MaterialPropertyBlock`** over material instances for per-object shader properties.
- Profile every PR touching rendering with Unity Profiler before merge.

## Branching Strategy

- `main` — release-ready, tagged with version numbers.
- `develop` — integration branch, CI builds on every push.
- `feature/{epic}-{description}` — e.g., `feature/MAPT-DISPLAY-hot-plug`.
- `bugfix/{description}` — for bug fixes.
- `spike/{description}` — for technical spikes (Phase 0).
- Squash-merge feature branches into `develop`. Merge `develop` into `main` for releases.

## Code Review Checklist

- [ ] No per-frame GC allocations
- [ ] No new singletons (only ServiceLocator)
- [ ] Interface defined for any new service
- [ ] Assembly definition references updated
- [ ] Shader uses `half` precision where possible
- [ ] Touch targets >= 48dp (prefer 56dp)
- [ ] Error handling for Android-specific edge cases
- [ ] Works with external display connected AND disconnected
