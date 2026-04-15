# AGENTS.md

This repository contains a custom Unity 2022.3 Scriptable Render Pipeline for mobile-first rendering.

The primary audience for this file is coding agents working inside this project. Follow these rules before making changes.

## Project Identity

- Project: `NewWorldRenderPipeline`
- Engine: Unity `2022.3`
- Rendering target: custom SRP, not Built-in
- Primary platforms: `Android` and `iOS`
- Priority order:
  1. Performance on mobile
  2. Cross-device compatibility
  3. Long-term extensibility
  4. Controlled system complexity

## Current Repository Layout

- Runtime pipeline code lives in [`Assets/NWRP/Runtime`](E:/UnityProject/Unity2022/NewWorldRenderPipeline/Assets/NWRP/Runtime)
- Shared shader library lives in [`Assets/NWRP/ShaderLibrary`](E:/UnityProject/Unity2022/NewWorldRenderPipeline/Assets/NWRP/ShaderLibrary)
- NWRP-owned shaders live in [`Assets/NWRP/Shaders`](E:/UnityProject/Unity2022/NewWorldRenderPipeline/Assets/NWRP/Shaders)
- Pipeline asset lives in [`Assets/Settings/NewWorldRP.asset`](E:/UnityProject/Unity2022/NewWorldRenderPipeline/Assets/Settings/NewWorldRP.asset)
- Sample scenes live in [`Assets/Scenes`](E:/UnityProject/Unity2022/NewWorldRenderPipeline/Assets/Scenes) and [`Assets/NWRP/Tests/Scenes`](E:/UnityProject/Unity2022/NewWorldRenderPipeline/Assets/NWRP/Tests/Scenes)

## Mandatory Architecture Rules

- Keep this project on custom SRP. Do not migrate it back to URP renderer features.
- Do not reintroduce monolithic renderer logic into [`CameraRenderer.cs`](E:/UnityProject/Unity2022/NewWorldRenderPipeline/Assets/NWRP/Runtime/CameraRenderer.cs) or [`NWRPRenderer.cs`](E:/UnityProject/Unity2022/NewWorldRenderPipeline/Assets/NWRP/Runtime/NWRPRenderer.cs).
- New rendering functionality must be implemented as:
  - one `NWRPFeature`
  - one or more focused `NWRPPass`
  - explicit toggles/config in `NewWorldRenderPipelineAsset`
- Do not build a "super feature" that owns unrelated systems.
- Pass communication must be explicit through frame data, global shader params, or named render targets. Avoid hidden coupling.

## Pass Order Contract

Respect the pass event sequence defined in [`NWRPPassEvent.cs`](E:/UnityProject/Unity2022/NewWorldRenderPipeline/Assets/NWRP/Runtime/NWRPPassEvent.cs):

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
- `PostProcess`
- `DebugOverlay`

Do not introduce ad hoc pass ordering outside this contract unless there is a hard rendering dependency.

## Asset and Settings Rules

- Pipeline-facing settings must live in [`NewWorldRenderPipelineAsset.cs`](E:/UnityProject/Unity2022/NewWorldRenderPipeline/Assets/NWRP/Runtime/NewWorldRenderPipelineAsset.cs).
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
  - [`MainLightShadowFeature.cs`](E:/UnityProject/Unity2022/NewWorldRenderPipeline/Assets/NWRP/Runtime/MainLightShadows/MainLightShadowFeature.cs)
  - [`MainLightShadowCasterPass.cs`](E:/UnityProject/Unity2022/NewWorldRenderPipeline/Assets/NWRP/Runtime/MainLightShadows/Passes/MainLightShadowCasterPass.cs)
  - [`Shadows.hlsl`](E:/UnityProject/Unity2022/NewWorldRenderPipeline/Assets/NWRP/ShaderLibrary/Shadows.hlsl)
  - [`Lighting.hlsl`](E:/UnityProject/Unity2022/NewWorldRenderPipeline/Assets/NWRP/ShaderLibrary/Lighting.hlsl)

## Shader Rules

- NWRP-owned shaders should use these standard pass names/tags where applicable:
  - `NewWorldForward`
  - `ShadowCaster`
  - `DepthOnly`
  - `NewWorldOutline`
  - `NewWorldUnlit`
- New lit shaders should prefer reusing the shared `ShadowCaster` and `DepthOnly` pass pattern from [`NewWorld_Lit_StandardLit.shader`](E:/UnityProject/Unity2022/NewWorldRenderPipeline/Assets/NWRP/Shaders/Lit/NewWorld_Lit_StandardLit.shader).
- Prefer `half` for mobile shader math unless world-space precision or matrix math requires `float`.
- Prefer uniforms over shader keywords for runtime intensity/threshold toggles.
- Prefer `#pragma shader_feature_local` over broad `multi_compile`.
- Do not build giant shared "do everything" shaders across vegetation, characters, effects, and UI.

## Variant Control

Variant growth is a hard constraint.

- Every new keyword needs a reason.
- Avoid multiplying feature combinations across unrelated axes.
- If a feature is expensive and rarely used, split it into a dedicated shader instead of another branch stack.
- Keep mobile-facing shader variant counts predictable and bounded.
- When touching shaders under [`Assets/Shaders/Environment`](E:/UnityProject/Unity2022/NewWorldRenderPipeline/Assets/Shaders/Environment), reduce inherited URP keyword debt instead of copying it into NWRP.

## Instancing and Large-Scale Rendering

- For large instance counts such as vegetation, prefer GPU-driven paths.
- Do not implement large render loops with CPU-side per-instance `for` loops.
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

- Mobile-first shadow filtering tiers are limited to:
  - `Hard`
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
