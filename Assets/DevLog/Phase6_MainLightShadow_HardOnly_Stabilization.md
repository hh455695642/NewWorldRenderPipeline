# Phase6 Main Light Shadow Hard-Only Stabilization

Date: `2026-04-14`

## Summary

This checkpoint intentionally rolls the main-light shadow path back to a minimal hard-shadow-only implementation.

The previous soft-shadow iterations accumulated multiple interacting variables:

- shadow compare direction issues on reversed-Z platforms
- incorrect soft-filter atlas texel interpretation
- missing shadow caster normal-bias application
- receiver-side compensations added before the caster path was correct

That stack was no longer a reliable baseline. This phase restores a simpler version that is easier to validate in Frame Debugger and safer to keep as a backup branch.

## What Changed

- Removed soft-shadow runtime settings from `NewWorldRenderPipelineAsset`
  - removed `MainLightShadowFilterMode`
  - removed `mainLightShadowSoftRadius`
  - removed `mainLightShadowReceiverBias`
- Removed soft-shadow receiver globals
  - removed `_MainLightShadowFilterParams`
- Removed soft-shadow shader helper code
  - removed tent/PCF helper include
  - removed receiver-side soft filter logic
  - receiver sampling is now a single hardware comparison sample
- Kept and fixed caster-side bias application
  - runtime now uploads `_ShadowBias`
  - `mainLightShadowBias` remains the user-facing URP-style depth bias control and `mainLightShadowNormalBias` remains the normal bias control
  - shared `ShadowCaster` pass applies bias in vertex space using position, normal, and main-light direction
  - the shadow caster path now uses a dedicated `_ShadowLightDirection` global instead of implicitly reusing `_MainLightPosition`
  - `_ShadowBias` is scaled from per-cascade shadow texel size instead of using raw asset values as world units
  - shadow map rendering restores a fixed raster depth bias baseline (`SetGlobalDepthBias(1.0, 2.5)`) to reduce large flat-surface acne
  - shadow-disabled or no-cascade paths explicitly reset raster bias and shadow globals before returning
  - Frame Debugger / profiling labels use `Main Light Shadows` naming to leave room for future additional-light shadow passes
- Removed pipeline-asset exposure of `maxAdditionalLights`
  - runtime now clamps additional lights only by the renderer-side constant limit

## Default Stabilization Parameters

The current backup baseline in `NewWorldRP.asset` is:

```yaml
mainLightShadowResolution: 2048
mainLightShadowDistance: 40
mainLightShadowCascadeCount: 2
mainLightShadowCascadeSplit: 0.2
mainLightShadowBias: 0.35
mainLightShadowNormalBias: 1.2
```

These defaults are intentionally conservative and meant to support correctness debugging first.

## Validation Intent

This phase is not a quality pass. It is a correctness reset.

Expected validation steps:

- verify `ShadowMap` stage still contains only one main-light shadow pass
- verify `StandardLit` uses the `ShadowCaster` pass
- verify hard shadows no longer depend on receiver-side soft-shadow compensation
- compare shadowed vs non-shadowed objects without soft filtering enabled

## Follow-Up Gates Before Reintroducing Soft Shadows

Do not reintroduce soft shadows until all of the following are confirmed:

- hard shadow compare is correct on current target platforms
- shadow caster bias behaves correctly without full-screen acne
- cascade selection and atlas addressing are stable
- hard-shadow baseline is visually acceptable in sample validation scenes

Only after that should soft filtering be reintroduced, and it should start from the hard-shadow baseline instead of layering more fixes onto a broken receiver path.
