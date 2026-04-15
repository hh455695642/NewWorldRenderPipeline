# Phase8 Main Light Shadow Medium PCF

Date: `2026-04-15`

## Summary

This phase keeps the current main light shadow behavior intact and focuses on low-risk cleanup around the Medium PCF receiver path.

The shadow system still supports:

- `Hard`
- `MediumPCF`
- receiver-side dynamic depth and normal bias
- `shadowCoord.z` out-of-range protection
- optional main light shadow caster cull override

This checkpoint does not change the tent kernel, sample count, or the static/dynamic shadow combine rule. It only improves maintainability, inspector clarity, and shader pass consistency.

## Public Controls

Main light shadow controls now include:

- `Main Light Shadow Filter Mode`
- `Main Light Shadow Filter Radius`
- `Main Light Shadow Receiver Depth Bias`
- `Main Light Shadow Receiver Normal Bias`
- `Shadow Caster Cull Mode`

Control semantics:

- `Main Light Shadow Filter Mode = Hard` uses a single comparison sample
- `Main Light Shadow Filter Mode = MediumPCF` uses a fixed `3x3` tent kernel
- `Main Light Shadow Filter Radius` is expressed in shadow texels
- `mainLightShadowFilterRadius = 1.0` matches the current baseline footprint
- receiver bias stays active for both `Hard` and `MediumPCF`

Inspector behavior in this phase:

- `Filter Radius` is only shown when `MediumPCF` is selected
- `Receiver Depth Bias` and `Receiver Normal Bias` remain always visible
- when `MediumPCF` and `Enable Dynamic Shadow` are both enabled, the inspector shows a mobile-cost reminder because the receiver may evaluate two `9-tap` compare filters

## Runtime Notes

Receiver-side filtering still lives in `Shadows.hlsl`.

Behavior rules remain:

- static atlas sampling and dynamic overlay sampling are combined with `min(static, dynamic)`
- `shadowCoord.z` outside `[0, 1]` returns `1` to avoid invalid tail-end shadows
- receiver dynamic bias is derived from `1 - saturate(dot(normalWS, lightDirWS))`
- `MainLightShadowCasterCullMode` is an explicit compatibility override, not the default acne fix strategy

Implementation cleanup in this phase:

- the static and dynamic `MediumPCF` tent paths now share one helper instead of maintaining two duplicated `9-tap` implementations
- the compatibility overload `SampleMainLightShadow(float3 positionWS, float3 lightDirectionWS)` now forwards directly to the single-parameter path, making it explicit that receiver bias is not applied there

## Fixes Completed In This Phase

1. Medium PCF tent sampling drift risk reduced

- Root cause: static shadowmap sampling and dynamic overlay sampling used duplicated `3x3` tent logic
- Fix: both paths now call one shared tent helper while preserving the existing weights, radius semantics, and compare order

2. Inspector no longer exposes a misleading radius control in `Hard` mode

- Root cause: `Filter Radius` was always visible even though only `MediumPCF` reads it
- Fix: the custom pipeline asset inspector now shows `Filter Radius` only for `MediumPCF`

3. Instancing support aligned for affected crystal environment shaders

- Root cause: `MineralCrystal` and `ShardCrystal` used `UNITY_*INSTANCE*` macros in multiple passes without enabling instancing variants in those passes
- Fix: added `#pragma multi_compile_instancing` to the relevant passes so forward, shadow, depth-only, and depth-normals rendering can participate in instancing correctly

## Mobile Cost Notes

This phase adds no new render passes, render targets, or shadow keywords.

Receiver shadow filtering cost remains:

- `Hard`: one comparison sample per atlas, plus optional dynamic overlay compare
- `MediumPCF`: nine comparison samples per atlas, plus optional dynamic overlay tent compare

Variant impact in this phase is intentionally narrow:

- no new shadow filter keywords
- no new runtime branching keywords
- the only intentional variant increase is `multi_compile_instancing` on `MineralCrystal.shader` and `ShardCrystal.shader`

That tradeoff is acceptable because those passes were already authored around instancing macros, and the change restores expected batching behavior instead of expanding the shadow feature matrix.

## Validation Notes

Validation target for this checkpoint:

- Unity Console stays at `0` error / `0` warning after import
- `NWRPShaderIds`, runtime uploads, and `Shadows.hlsl` global names remain aligned
- `Hard` keeps current output
- `MediumPCF` with `mainLightShadowFilterRadius = 1.0` keeps current visual behavior
- `MediumPCF + Enable Dynamic Shadow` shows the cost hint in the asset inspector

Visual scene validation is still required in-editor for:

- softness difference across `0.5 / 1.0 / 2.0`
- receiver acne versus peter-panning balance
- dynamic overlay consistency under `MediumPCF`

## Next Candidates

Not part of this phase, but still valid follow-up work:

- share hard-shadow static/dynamic compare boilerplate the same way the tent path is now shared
- evaluate whether `MediumPCF + Dynamic Overlay` should expose a clearer quality/cost label for mobile content teams
- profile crystal-heavy scenes to confirm the instancing-enabled passes recover measurable batching wins
