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

## Screen-Space and Post-Process Runtime Rules

- New fullscreen or screen-space effects should default to `NWRPFullscreenChain` plus `INWRPFullscreenEffectNode`. Add direct blit or custom temporary RT code only when the shared chain cannot express the effect, and keep the reason local to the pass.
- `TryGetFrameTargetRequirements` must declare intermediate color, depth texture, depth prepass/copy, and opaque texture needs before pass scheduling. Depth-based effects should request depth through `DepthTextureFeature.GetFrameTargetRequirements` instead of creating depth resources inside the pass.
- `AddPasses` must skip inactive effects, null renderer/camera cases, Preview cameras, and frames where required targets are unavailable. Do not enqueue a pass that will only no-op because required resources were not requested.
- `GetFrameResourceUsage` must accurately describe camera color, camera depth, depth texture, opaque texture, transient color, backbuffer write, and final-present behavior so the lightweight frame graph can fuse or discard resources safely.
- Single-pass screen effects should allow final camera color presentation to the backbuffer when they are the last camera color writer. Multi-pass effects must bound iteration count, temporary RT count, and resolution scale explicitly.
- Prefer existing fullscreen debug stats and Frame Debugger/RenderDoc-visible passes for diagnostics. Do not add heavy runtime debug systems for individual screen effects.

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
