---
sessionId: session-261026-152336-orcg
---

# Findings & Issues

## Overview

The changes on the `unity6` branch add Unity 6 (6000.x) support by introducing version-gated plugin folders and a `Utils.cs` facade for deprecated APIs. Overall the approach is sound, but there are **3 compatibility issues** that need fixing.

---

### Issue 1: Wrong preprocessor define in `Utils.cs` (Blocks Unity 6.0–6.6)

**File:** `Assets/Scripts/Editor/Utils.cs`

The facade uses `UNITY_6000_7_OR_NEWER` in all three methods (`GetAssemblies`, `GetAssemblyLocation`, `LoadAssemblyFromPath`). However, the new APIs (`CurrentAssemblies.GetLoadedAssemblies()`, `Assembly.GetLoadedAssemblyPath()`, `CurrentAssemblies.LoadFromPath()`) were introduced in **Unity 6000.0** (Unity 6.0 LTS), as confirmed by the [Unity documentation](https://docs.unity3d.com/6000.4/Documentation/ScriptReference/Assemblies.CurrentAssemblies.html).

**Impact:** Unity 6000.0 through 6000.6 will fall through to the `#else` branch and use the deprecated APIs (`AppDomain.CurrentDomain.GetAssemblies()`, `assembly.Location`, `Assembly.LoadFrom()`). These APIs are marked as incompatible in Unity 6 and may cause `TypeLoadException`, `NullReferenceException`, memory leaks, or Editor crashes.

**Fix:** Change all three `#if UNITY_6000_7_OR_NEWER` to `#if UNITY_6000_0_OR_NEWER`.

---

### Issue 2: Inconsistent preprocessor define in `DotnetExeCompilator.cs`

**File:** `Assets/Scripts/Editor/Compilation/DotnetExeCompilator.cs` (line 128)

Uses `#if UNITY_6000_6_OR_NEWER` for the main-thread assembly loading workaround, while `Utils.cs` uses `UNITY_6000_7_OR_NEWER`. These should be consistent.

**Impact:** If `Utils.cs` is fixed to `UNITY_6000_0_OR_NEWER`, then Unity 6000.0–6000.5 would call `Utils.LoadAssemblyFromPath()` (which uses the new API), but the `#if UNITY_6000_6_OR_NEWER` block in `DotnetExeCompilator.cs` would **not** execute — meaning the main-thread dispatch wouldn't happen for those versions. If the main-thread requirement applies to all Unity 6.x, the define should match.

**Fix:** Align the preprocessor define with `Utils.cs` (likely `UNITY_6000_0_OR_NEWER` if the main-thread requirement applies to all Unity 6).

---

### Issue 3: `ProjectTypeCache.cs` not updated (Runtime code)

**File:** `Assets/Scripts/Runtime/ProjectTypeCache.cs` (line 34)

Still calls `AppDomain.CurrentDomain.GetAssemblies()` directly. Since this is Runtime code, it can't reference the Editor-only `Utils` class.

**Impact:** On Unity 6000.0+, this will use the deprecated API and may return stale assemblies, causing crashes or logical errors during hot reload.

**Fix:** Either:
- Create a Runtime-equivalent polyfill (e.g., extend the existing `Polyfills.cs` or create a new `RuntimeUtils.cs`) with the same version-gated logic, or
- Add the `#if UNITY_6000_0_OR_NEWER` guard directly in `ProjectTypeCache.cs`.

---

### What Looks Correct

- **Plugin folder structure:** The `2019+` / `6000.6+` split for Harmony and Roslyn with proper `defineConstraints` in `.meta` files is correct. The `!UNITY_6000_6_OR_NEWER` negation constraint correctly excludes the older DLLs on Unity 6+.
- **Removing `net48` Harmony folder:** Clean removal, replaced by version-specific folders.
- **Cecil assembly resolver:** The new `DefaultAssemblyResolver` setup in `AddInternalsVisibleToForAllUserAssembliesPostProcess.cs` correctly adds Unity's Managed folder — this improves compatibility.
- **`FileWatcherSetupEntry.cs` string split:** The `[Obsolete]` string concatenation workaround is harmless (likely for Roslyn analyzer compatibility).
- **`asmdef` changes:** Adding Roslyn DLL references and setting `autoReferenced: true` looks fine.
- **`package.json`** correctly declares `"unity": "2022.3"` as the minimum version.


# Delivery Steps

###   Step 1: Fix preprocessor defines in Utils.cs
Change all three `#if UNITY_6000_7_OR_NEWER` to `#if UNITY_6000_0_OR_NEWER` in `Assets/Scripts/Editor/Utils.cs`. This ensures Unity 6000.0–6000.6 correctly uses the new APIs instead of deprecated ones.

###   Step 2: Align preprocessor define in DotnetExeCompilator.cs and fix ProjectTypeCache.cs
- Change `#if UNITY_6000_6_OR_NEWER` to `#if UNITY_6000_0_OR_NEWER` in `Assets/Scripts/Editor/Compilation/DotnetExeCompilator.cs` (line 128) to match the Utils facade.
- Add version-gated logic to `Assets/Scripts/Runtime/ProjectTypeCache.cs` (line 34): either inline `#if UNITY_6000_0_OR_NEWER` using `UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies()`, or create a small Runtime polyfill class if the pattern is reused elsewhere.