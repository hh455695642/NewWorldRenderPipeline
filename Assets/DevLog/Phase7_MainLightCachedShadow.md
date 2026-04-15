# Phase7 Main Light Cached Shadow

Date: `2026-04-15`

## Summary

This phase extends the Phase6 hard-shadow-only baseline with a cached main light shadow path for NWRP.

The goal is not to reproduce the plugin architecture. The goal is to keep the default realtime path intact while adding a mobile-first cached path inside NWRP:

- static main light shadow atlas cache
- optional per-frame dynamic shadow overlay
- no fullscreen combine pass
- no new shader keywords

`Phase6_MainLightShadow_HardOnly_Stabilization.md` remains the hard-shadow correctness baseline. This phase records the cached shadow integration and the follow-up fixes needed to make it stable in the editor.

## Public Controls

The pipeline asset now exposes three layered controls for the main light shadow path:

- `Enable Main Light Shadow`
- `Enable Cached Shadow`
- `Enable Dynamic Shadow`

Cached shadow controls also include:

- `Static Caster Layer Mask`
- `Dynamic Caster Layer Mask`
- `Camera Position Invalidation Threshold`
- `Camera Rotation Invalidation Threshold`
- `Light Direction Invalidation Threshold`

Runtime API:

- `MarkMainLightShadowCacheDirty()`
- `ClearMainLightShadowCache()`

Current update policy is `OnDirty`. This is not the plugin's `Manual` or `EverySecond` mode.

## Runtime Structure

Main light cached shadow runtime code now lives under:

| Path | Responsibility |
|------|------|
| `Assets/NWRP/Runtime/MainLightShadows/MainLightShadowFeature.cs` | Chooses disabled / realtime / static cache / dynamic overlay path |
| `Assets/NWRP/Runtime/MainLightShadows/MainLightShadowCacheState.cs` | Long-lived cached atlas state, cascade data, invalidation signature |
| `Assets/NWRP/Runtime/MainLightShadows/MainLightShadowPassUtils.cs` | Shared atlas sizing, culling, cascade setup, shadow global upload helpers |
| `Assets/NWRP/Runtime/MainLightShadows/Passes/MainLightShadowCasterPass.cs` | Legacy realtime full-atlas update path |
| `Assets/NWRP/Runtime/MainLightShadows/Passes/MainLightShadowStaticCachePass.cs` | Static atlas rebuild on dirty frames |
| `Assets/NWRP/Runtime/MainLightShadows/Passes/MainLightShadowDynamicOverlayPass.cs` | Per-frame dynamic caster overlay atlas |
| `Assets/NWRP/Runtime/MainLightShadows/Passes/MainLightShadowDisabledPass.cs` | Explicit shadow-disabled global upload |

Receiver-side sampling remains in `Assets/NWRP/ShaderLibrary/Shadows.hlsl`:

- `_MainLightShadowmapTexture` for static or realtime atlas
- `_MainLightDynamicShadowmapTexture` for dynamic overlay atlas
- dual-sample result combined with `min(static, dynamic)`

Behavior rules in the current implementation:

- cached shadow is only used by `Game Camera`
- `SceneView` and `Preview` cameras fall back to realtime main light shadows
- when cached shadow is disabled, NWRP uses the original realtime main light shadow pass
- when cached shadow is enabled and dynamic shadow is disabled, only the static atlas is sampled

The pipeline asset currently has `FeatureCount = 0`, so this system runs through `NewWorldRenderPipelineAsset`'s runtime fallback `MainLightShadowFeature` instead of a serialized feature asset.

## Fixes Completed In This Phase

This phase resolved three concrete issues in the initial cached shadow integration:

1. Static cache first frame could build an empty atlas

- Root cause: atlas rendering used cached `CascadeCount` before `CommitStaticCache()` had written it.
- Fix: render helpers now take the current frame's `cascadeCount` explicitly, and static cache is committed only after at least one cascade is actually rendered.

2. Editor multi-camera rendering could keep the Game View cache dirty

- Root cause: `Game Camera`, `SceneView`, and `Preview` shared one cached state, while invalidation also depended on camera pose.
- Fix: cached shadow is now limited to `Game Camera`; editor helper cameras use realtime fallback and do not overwrite the shared cache state.

3. Dynamic overlay could disappear because it was running on top of an invalid static cache

- Root cause: dynamic overlay required `HasValidCache`, but the previous initialization path could leave the static atlas invalid.
- Fix: `HasValidCache` is now only set after the static atlas has really been rendered, and dynamic overlay cleanly falls back to the empty shadowmap when no valid static cache exists.

## Current Limits

This checkpoint is intentionally narrow:

- only one main directional light
- only `1-2` cascades
- hard shadow only
- cached path only for `Game Camera`
- static caster transform changes do not auto-refresh the cache
- static cache rebuild still depends on Game Camera pose, because current cascade matrices are camera-relative

Expected consequence:

- moving a static-layer caster will not immediately update its shadow
- cache rebuild happens after `MarkMainLightShadowCacheDirty()` or when the main light / Game Camera exceeds the configured invalidation thresholds

## Validation Notes

Current validation result for this checkpoint:

- Unity Console: `0` error / `0` warning
- `Enable Cached Shadow = false`: realtime main light shadow path still works
- `Enable Cached Shadow = true` + `Enable Dynamic Shadow = true`: dynamic layer casters recover per-frame shadow projection
- `SceneView` no longer dirties the shared `Game Camera` cached atlas
- shader globals remain aligned between `NWRPShaderIds` and `Shadows.hlsl`

## Next Candidates

Not part of this commit, but valid follow-up directions:

- `Manual` cached update mode
- `ThrottledOnDirty` cached update mode

`EverySecond` is intentionally not part of this phase.
