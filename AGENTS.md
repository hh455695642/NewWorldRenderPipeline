# AGENTS.md

This repository contains a custom Unity 6.3 / 6000.3 Scriptable Render Pipeline for mobile-first rendering.

The primary audience for this file is coding agents working inside this project. Follow these rules before making changes.

## Project Identity

- Project: `NewWorldRenderPipeline`
- Engine: Unity `6000.3.x` (`6000.3.12f1` at branch creation)
- Rendering target: custom SRP, not Built-in
- Primary platforms: `Android` and `iOS`
- Priority order:
  1. Performance on mobile
  2. Cross-device compatibility
  3. Long-term extensibility
  4. Controlled system complexity

## Unity 6.3 Migration Rules

- This branch targets Unity `6000.3.x`. Keep `ProjectSettings/ProjectVersion.txt` as the source of truth when the exact patch version changes.
- Preserve the custom SRP architecture during migration. Do not replace NWRP with URP `ScriptableRendererFeature`, URP `ScriptableRenderPass`, RenderGraph-only URP paths, or Built-in pipeline fallbacks.
- In this repository, "feature" means `NWRPFeature` unless explicitly stated otherwise. It does not mean URP `ScriptableRendererFeature`.
- Unity 6 API compatibility fixes should stay near NWRP-owned runtime, editor, or shader-library boundaries. Prefer small adapters/helpers over scattered version checks inside individual passes.
- Package upgrades must preserve the custom SRP boundary. URP may remain installed for testing, reference, and shader migration work only.
- When Unity rewrites serialized assets or settings, keep YAML churn scoped to files required by the migration or the current task.

## Current Repository Layout

- Runtime pipeline code lives in [`Assets/NWRP/Runtime`](Assets/NWRP/Runtime)
- Shared shader library lives in [`Assets/NWRP/ShaderLibrary`](Assets/NWRP/ShaderLibrary)
- NWRP-owned shaders live in [`Assets/NWRP/Shaders`](Assets/NWRP/Shaders)
- NWRP compute shaders live under [`Assets/NWRP/Shaders/Compute`](Assets/NWRP/Shaders/Compute)
- NWRP editor tooling lives in [`Assets/NWRP/Editor`](Assets/NWRP/Editor), grouped by domain while keeping the root asmdef there.
- Pipeline asset lives in [`Assets/Settings/NewWorldRP.asset`](Assets/Settings/NewWorldRP.asset)
- Sample scenes live in [`Assets/Scenes`](Assets/Scenes) and [`Assets/NWRP/Tests/Scenes`](Assets/NWRP/Tests/Scenes)

## Mandatory Architecture Rules

- Keep this project on custom SRP. Do not migrate it back to URP renderer features.
- Do not reintroduce monolithic renderer logic into [`CameraRenderer.cs`](Assets/NWRP/Runtime/CameraRenderer.cs) or [`NWRPRenderer.cs`](Assets/NWRP/Runtime/NWRPRenderer.cs).
- New rendering functionality must be implemented as:
  - one `NWRPFeature`
  - one or more focused `NWRPPass`
  - explicit toggles/config in `NewWorldRenderPipelineAsset`
- Do not build a "super feature" that owns unrelated systems.
- Pass communication must be explicit through frame data, global shader params, or named render targets. Avoid hidden coupling.

## Directory and Naming Rules

- Keep `Assets/NWRP/Runtime` root for pipeline core types only, such as renderer, frame data, base pass/feature contracts, shader IDs, and the pipeline asset.
- Built-in runtime feature systems should keep their established domain folders under `Assets/NWRP/Runtime/<FeatureArea>`, for example `CameraTextures`, `Fog`, `Outlines`, `MainLightShadows`, `PostProcessing`, and `VegetationIndirectShadows`.
- Optional pluggable feature systems currently live under `Assets/NWRP/Runtime/PluggableFeatures/<FeatureName>`.
- The current pluggable feature set is limited to `CloudShadowProjector`, `ScreenBlur`, `ValleyHeightFog`, and `ValleyHeightFogOverlay`.
- For pluggable features, the folder name must match the feature class name without the `Feature` suffix. Example: `CloudShadowProjectorFeature` belongs in `PluggableFeatures/CloudShadowProjector/CloudShadowProjectorFeature.cs`.
- Feature-owned passes, volume components, registries, and helpers should live beside that feature, using focused subfolders such as `Passes` or `Compatibility` when needed.
- Do not place NWRP-owned runtime systems under `Assets/NWRP/Plugins`; reserve plugin-style folders for third-party or externally sourced packages.
- Keep compute shaders under `Assets/NWRP/Shaders/Compute/<Domain>` and keep material-facing shaders under their shader family folders.
- Keep `Assets/NWRP/Editor` grouped by domain (`Pipeline`, `Shaders`, `PostProcessing`, `Lighting`, `Cameras`) while preserving existing namespaces and shader `CustomEditor` strings.
- New test assets and folders should use stable English names without typo drift, spaces, or parenthesized variants when avoidable. Do not mass-rename scene instance names unless a task explicitly requires YAML churn.

## Pass Order Contract

Respect the pass event sequence defined in [`NWRPPassEvent.cs`](Assets/NWRP/Runtime/NWRPPassEvent.cs):

- `BeforeShadowMap`
- `ShadowMap`
- `BeforeDepthPrepass`
- `DepthPrepass`
- `BeforeOpaque`
- `Opaque`
- `Skybox`
- `BeforeTransparent`
- `Transparent`
- `AfterTransparent`
- `AfterValleyHeightFog`
- `BeforePostProcess`
- `PostProcess`
- `AfterPostProcess`
- `DebugOverlay`

Do not introduce ad hoc pass ordering outside this contract unless there is a hard rendering dependency.

## Asset and Settings Rules

- Pipeline-facing settings must live in [`NewWorldRenderPipelineAsset.cs`](Assets/NWRP/Runtime/NewWorldRenderPipelineAsset.cs).
- Group new settings into existing sections when possible:
  - `GeneralSettings`
  - `LightingSettings`
  - `ShadowSettings`
  - `FeatureSettings`
  - `PlatformOverrides`
- Any new runtime feature must support an explicit enable/disable path.
- Platform-specific cost differences should be expressed through asset settings, not hardcoded checks spread across passes.

## Mobile Rendering Constraints

- Optimize for tile-based mobile GPUs first.
- Prefer fewer passes over cleaner abstraction if the pass count meaningfully impacts bandwidth.
- Avoid:
  - unnecessary `RenderTexture` allocations
  - repeated full-screen blits
  - high-resolution intermediate RT chains
  - MRT unless clearly justified
  - geometry shader usage
- Bandwidth is more important than ALU in most decisions here.
- SRP Batcher and GPU Instancing are preferred. `dynamic batching` is intentionally removed and must not be added back.

## Lighting and Shadow Rules

- The default real-time shadow path is:
  - one main directional light
  - stable cascaded shadow map
  - hard shadow only for the current stabilization branch
- Main light shadow bias semantics are:
  - `mainLightShadowBias` = user-facing depth bias
  - `mainLightShadowNormalBias` = user-facing normal bias
  - fixed raster depth bias remains an internal baseline, not a public tuning knob
- Do not add multi-light real-time shadowing as a default path for mobile.
- Additional lights may contribute lighting, but they should not silently become shadow casters.
- If changing shadow code, keep these files aligned:
  - [`MainLightShadowFeature.cs`](Assets/NWRP/Runtime/MainLightShadows/MainLightShadowFeature.cs)
  - [`MainLightShadowCasterPass.cs`](Assets/NWRP/Runtime/MainLightShadows/Passes/MainLightShadowCasterPass.cs)
  - [`Shadows.hlsl`](Assets/NWRP/ShaderLibrary/Shadows.hlsl)
  - [`Lighting.hlsl`](Assets/NWRP/ShaderLibrary/Lighting.hlsl)

## URP Compatibility Boundary

- `Packages/manifest.json` may keep URP installed for testing, reference, and shader migration work.
- The Unity 6 URP package is not an architectural dependency for NWRP runtime code.
- NWRP-owned runtime and shaders must not depend on URP package source:
  - no `UnityEngine.Rendering.Universal` in `Assets/NWRP`
  - no `Packages/com.unity.render-pipelines.universal/...` shader includes in `Assets/NWRP`
  - no `ScriptableRendererFeature` or `ScriptableRenderPass` implementations for NWRP features
- URP-style shader global names and helper names are allowed when they ease migration, for example `_CameraDepthTexture`, `_CameraOpaqueTexture`, `TransformObjectToWorld`, `GetNormalizedScreenSpaceUV`, and `SampleSceneDepth`.
- When using URP-style names, implement them in NWRP shader libraries or thin NWRP aliases. Naming compatibility must not imply package dependency.
- NWRP internal features, passes, debug paths, and private helpers should use `NWRP` or `NewWorld` naming to make ownership clear.

## Shader Rules

- NWRP-owned shaders should use these standard pass names/tags where applicable:
  - `NewWorldForward`
  - `ShadowCaster`
  - `DepthOnly`
  - `NewWorldOutline`
  - `NewWorldUnlit`
- New lit shaders should prefer reusing the shared `ShadowCaster` and `DepthOnly` pass pattern from [`NewWorld_Lit_StandardLit.shader`](Assets/NWRP/Shaders/Lit/NewWorld_Lit_StandardLit.shader).
- Prefer `half` for mobile shader math unless world-space precision or matrix math requires `float`.
- Prefer uniforms over shader keywords for runtime intensity/threshold toggles.
- Prefer `#pragma shader_feature_local` over broad `multi_compile`.
- Do not build giant shared "do everything" shaders across vegetation, characters, effects, and UI.
- Environment and vegetation shaders under [`Assets/NWRP/Shaders/Environment`](Assets/NWRP/Shaders/Environment) must use NWRP shader libraries and pass tags. Do not include URP shader libraries or keep URP LightMode tags in NWRP-owned variants.
- Keep grass and tree shaders separate. Grass shaders should default to receiving realtime shadows without casting them; tree shaders that need shadows must use their own ShadowCaster path instead of adding tree-specific complexity to grass shaders.

## Variant Control

Variant growth is a hard constraint.

- Every new keyword needs a reason.
- Avoid multiplying feature combinations across unrelated axes.
- If a feature is expensive and rarely used, split it into a dedicated shader instead of another branch stack.
- Keep mobile-facing shader variant counts predictable and bounded.
- When touching shaders under [`Assets/NWRP/Shaders/Environment`](Assets/NWRP/Shaders/Environment), reduce inherited URP keyword debt instead of copying it into NWRP.

## Instancing and Large-Scale Rendering

- For large instance counts such as vegetation, prefer GPU-driven paths.
- Do not implement large render loops with CPU-side per-instance `for` loops.
- GPU-driven vegetation should keep grass and tree render policies split by renderer or feature so culling distance, shadow casting, and receiver settings do not leak across vegetation types.
- Future large-scale systems should be organized around:
  - chunk or cluster grouping
  - GPU culling
  - indirect draw
  - shared visibility data between main and shadow passes

## Code Change Expectations

- Make changes that are directly usable in the project. Avoid speculative scaffolding with no integration point.
- Keep comments short and technical.
- Preserve existing file ownership boundaries where possible:
  - runtime orchestration in `Assets/NWRP/Runtime`
  - shared shader functions in `Assets/NWRP/ShaderLibrary`
  - material-facing shader definitions in `Assets/NWRP/Shaders`
- If you add a new feature/pass file, also wire it into the pipeline asset lifecycle.

## Rule Layering

- Keep one root `AGENTS.md` for global architecture policy.
- Add focused child `AGENTS.md` only for high-churn subsystems that need local constraints.
- Current child scope is intentionally limited to:
  - `Assets/NWRP/Runtime`
  - `Assets/NWRP/ShaderLibrary`
- Do not create `AGENTS.md` in every subfolder. Avoid rule drift and conflicting instructions.

## Shared Shader Includes

- Cross-shader reusable pass includes must live in `Assets/NWRP/ShaderLibrary/Passes`.
- Shader-family local include folders (for example `Assets/NWRP/Shaders/Lit/Includes`) should be thin wrappers only.
- New lit/NPR/effect shaders should reuse ShaderLibrary pass includes first, then add local wrappers only when needed for compatibility.

## Main Light Shadow Filtering Policy

- Mobile-first baseline shadow filtering defaults to:
  - `Hard`
- `MediumPCF` exists as an explicit asset-selected NWRP mode. Do not treat it as soft shadow support or as approval to add wider filtering tiers.
- Soft shadow support is temporarily removed on the stabilization branch.
- Shadow caster passes should use a dedicated shadow-light direction upload, not implicitly reuse forward-light globals.
- Soft-shadow artifact mitigation priority is:
  - correct shadow caster bias application
  - cascade correctness and atlas addressing
- Do not add PCSS/EVSM in baseline mobile path without explicit approval and profiling evidence.

## Validation Expectations

Before considering work complete, validate as much as the environment allows:

- Check for compile-time consistency between runtime IDs and shader globals
- Check that pass tags and shader pass names match renderer expectations
- Check that asset serialization still lines up with field names
- If Unity Editor is available, prefer opening or compiling the project over guessing

If Unity cannot be run in the current environment, state that explicitly in the final handoff.
