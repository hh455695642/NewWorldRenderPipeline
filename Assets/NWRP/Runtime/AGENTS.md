# Runtime AGENTS

Local rules for `Assets/NWRP/Runtime`.

## Ownership

- Runtime orchestration lives here: renderer, feature scheduling, pass lifecycle, shader global uploads.
- Keep the `Runtime` root for pipeline core contracts and global orchestration types.
- Keep built-in features in their established domain folders, such as `CameraTextures`, `Fog`, `Outlines`, `MainLightShadows`, `PostProcessing`, and `VegetationIndirectShadows`.
- Put only optional pluggable features under `PluggableFeatures/<FeatureName>`.
- The current pluggable feature set is `CloudShadowProjector`, `ScreenBlur`, `ValleyHeightFog`, and `ValleyHeightFogOverlay`.
- For pluggable features, the folder name must match the feature class without the `Feature` suffix. For example, `ScreenBlurFeature` belongs in `PluggableFeatures/ScreenBlur/ScreenBlurFeature.cs`.
- Feature-owned passes, volume components, registries, and helpers should live beside their feature in local folders such as `Passes` or `Compatibility` unless they are shared built-in renderer passes.
- Keep custom SRP architecture (`NWRPFeature` + focused `NWRPPass`) and avoid monolithic logic.
- Do not use `UnityEngine.Rendering.Universal`, `ScriptableRendererFeature`, or `ScriptableRenderPass` in NWRP runtime code.
- URP-style shader global names are allowed for migration compatibility when their values are uploaded by NWRP-owned runtime code.

## Pass and Feature Rules

- New runtime rendering behavior must be introduced by one focused feature and one or more focused passes.
- Pass ordering must follow `NWRPPassEvent` contract. Do not add ad hoc ordering outside the enum flow.
- Any feature affecting runtime cost must have an explicit enable/disable path in `NewWorldRenderPipelineAsset`.
- GPU-driven renderer integrations should expose explicit provider/registry interfaces instead of adding renderer-specific loops to shadow or camera passes.
- NWRP-owned runtime systems should not be placed in plugin-style folders unless they are actually third-party package boundaries.

## Screen-Space Feature Implementation

- `AddPasses` must skip inactive effects, Preview cameras, and frames that do not satisfy required resources or platform settings.
- `TryGetFrameTargetRequirements` must declare intermediate camera color, depth texture, and opaque texture needs before pass scheduling. Do not allocate or request these resources opportunistically inside `Execute`.
- `GetFrameResourceUsage` must accurately describe camera color, camera depth, depth texture, and opaque texture reads/writes so final blit fusion and frame-target planning stay correct.
- Single-pass fullscreen effects should prefer `NWRPFullscreenChain` / `INWRPFullscreenEffectNode` and allow final pass fusion when they can present directly to the camera/backbuffer target.
- Multi-pass fullscreen effects must bound iteration count, resolution scale, and temporary RT count. Do not create unbounded blur or ping-pong chains from volume parameters.
- Depth-based effects must request depth through `DepthTextureFeature.GetFrameTargetRequirements` from the feature; do not patch missing depth resources inside the pass.
- Debug paths should stay Frame Debugger and RenderDoc friendly, using lightweight stats or markers only. Do not add complex runtime debug systems for individual screen effects.

## Mobile Cost Constraints

- Minimize pass count, intermediate RT allocation, and full-screen operations.
- Prioritize bandwidth savings over ALU savings for default mobile path decisions.
- Prefer stable per-frame global uploads over hidden coupling between passes.

## Shadow-Specific Constraints

- Main light real-time shadows are the default supported shadow path.
- Keep shader global names and runtime IDs aligned (`NWRPShaderIds` and shader library declarations).
- Shadow filtering defaults to `Hard`; `MediumPCF` is allowed only through explicit asset settings already exposed by NWRP.
- Keep `mainLightShadowBias` as user-facing depth bias and `mainLightShadowNormalBias` as user-facing normal bias.
- Keep fixed raster depth bias internal; do not expose it as another public asset setting without strong need.
- Upload a dedicated shadow-light direction for caster passes instead of reusing forward-light globals.
- Prioritize correct caster bias upload/application before adding any new receiver-side fixes.
