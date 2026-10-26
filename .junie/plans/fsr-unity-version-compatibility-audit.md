---
sessionId: session-261026-152248-x882
---

# Compatibility Audit

### Overview

FastScriptReload claims support for **Unity 2019.3, 2020.3, 2021.3, 2022.2** (per README), while `package.json` declares `"unity": "2022.3"` as the minimum. The codebase uses `#if` preprocessor directives to handle API differences across Unity versions. This audit identifies all version-gated code paths and checks for compatibility gaps.

### Findings

#### 1. **CRITICAL: Missing fallback in `AssemblyChangesLoader.cs` (lines 227-233)**

```
#if UNITY_6000_0_OR_NEWER
    Object.FindObjectsByType(...) // Unity 6+
#elif UNITY_2021_1_OR_NEWER
    Object.FindObjectsOfType(...) // Unity 2021.1+
#endif
```

**Problem**: Unity 2019.3 and 2020.x fall through the `#elif` gap — neither condition is true. The `OnScriptHotReload` callback **won't be invoked at all** for all MonoBehaviour instances on those versions. This is a silent failure.

**Fix**: Change `#elif UNITY_2021_1_OR_NEWER` to `#else` since `FindObjectsOfType` has been available since Unity 5.x.

#### 2. **`Utils.cs` — Unity 6.7+ API polyfills (lines 15, 26, 37)**

Three methods (`GetAssemblies()`, `GetAssemblyLocation()`, `LoadAssemblyFromPath()`) use `#if UNITY_6000_7_OR_NEWER` to switch between Unity 6.7+ `UnityEngine.Assemblies.CurrentAssemblies` API and legacy `AppDomain`/`Assembly.LoadFrom`.

**Status**: ✅ Correct. Fallbacks to older APIs are proper.

#### 3. **`FastScriptReloadManager.cs` — `ResolveRelativeToAssetDirectoryFilePath()` (lines 459-463)**

```
#if UNITY_6000_4_OR_NEWER
    AssetDatabase.GetAssetPath(obj.GetEntityId())
#else
    AssetDatabase.GetAssetPath(obj.GetInstanceID())
#endif
```

**Status**: ✅ Correct. `GetEntityId` replaced `GetInstanceID` in Unity 6.004+; fallback is proper.

#### 4. **`FastScriptReloadManager.cs` — Domain reload trigger (lines 541-548)**

```
#if UNITY_2019_3_OR_NEWER
    CompilationPipeline.RequestScriptCompilation()
#elif UNITY_2017_1_OR_NEWER
    // reflection-based fallback
#endif
```

**Status**: ✅ Correct. Covers 2017.1+ with reflection fallback, and 2019.3+ with native API.

#### 5. **`DotnetExeCompilator.cs` — Assembly loading (lines 128-136)**

```
#if UNITY_6000_6_OR_NEWER
    unityMainThreadDispatcher.EnqueueAsync(...).Wait()
#else
    Utils.LoadAssemblyFromPath(outLibraryPath)
#endif
```

**Status**: ✅ Correct. Uses `EnqueueAsync` only on Unity 6.6+ where it's available.

#### 6. **`FastScriptReloadWelcomeScreen.cs` — Render pipeline (lines 1070-1074)**

```
#if UNITY_6000_0_OR_NEWER
    GraphicsSettings.defaultRenderPipeline
#else
    GraphicsSettings.renderPipelineAsset
#endif
```

**Status**: ✅ Correct. Property renamed in Unity 6.

#### 7. **`WindowsFileSystemWatcher.cs` — Gated to Unity 2021.1+ (line 1)**

```
#if UNITY_2021_1_OR_NEWER
```

**Status**: ⚠️ The entire WindowsFileSystemWatcher is excluded on Unity 2019.x/2020.x. This is intentional (uses `Microsoft.Win32.SafeHandles` and other .NET Standard 2.1 features), but means **Windows users on Unity 2019/2020 won't get the fast file watcher**. The fallback to `CustomFileWatcher` (polling-based) should still work.

#### 8. **`DynamicAssemblyCompiler.cs` — Debug link (lines 27-31)**

```
#if UNITY_2021_1_OR_NEWER
    // clickable href in log
#else
    // plain text instructions
#endif
```

**Status**: ✅ Correct. Clickable log links are a Unity 2021+ feature.

#### 9. **`FastScriptReloadWelcomeScreen.cs` — SettingsProvider (line 889)**

```
#if UNITY_2019_1_OR_NEWER
    [SettingsProvider]
```

**Status**: ✅ Correct. SettingsProvider API introduced in 2019.1.

#### 10. **`package.json` declares minimum Unity 2022.3**

The `"unity": "2022.3"` field contradicts the README which claims support for 2019.3+. If installed via UPM, older Unity versions will reject the package.

### Summary Table

| Issue | File | Severity | Versions Affected |
|-------|------|----------|-------------------|
| Missing `#else` fallback for `FindObjectsOfType` | AssemblyChangesLoader.cs | **CRITICAL** | 2019.3, 2020.x |
| `package.json` min version too high | package.json | **MEDIUM** | All < 2022.3 |
| WindowsFileSystemWatcher unavailable | WindowsFileSystemWatcher.cs | LOW (has fallback) | 2019.3, 2020.x |

### Recommended Fixes

1. **`AssemblyChangesLoader.cs` line 230**: Change `#elif UNITY_2021_1_OR_NEWER` → `#else` (FindObjectsOfType works on all supported versions)
2. **`package.json` line 6**: Change `"unity": "2022.3"` → `"unity": "2019.3"` to match README claims


# Delivery Steps

###   Step 1: Fix critical FindObjectsOfType fallback gap
Fix the missing fallback in `AssemblyChangesLoader.cs` that breaks hot-reload callbacks on Unity 2019.3 and 2020.x.
- Change `#elif UNITY_2021_1_OR_NEWER` to `#else` at line 230 in `AssemblyChangesLoader.cs`
- `Object.FindObjectsOfType<T>()` has been available since Unity 5.x, so the version gate is unnecessary and creates a gap where older versions execute no code path

###   Step 2: Fix package.json minimum Unity version
Align `package.json` minimum Unity version with the README's claimed support.
- Change `"unity": "2022.3"` to `"unity": "2019.3"` in `Assets/package.json`
- This ensures the package can be installed via UPM on Unity 2019.3, 2020.3, 2021.3, and 2022.2 as documented in the README