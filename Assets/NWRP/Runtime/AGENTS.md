# Runtime AGENTS

Local rules for `Assets/NWRP/Runtime`.

## Ownership

- Runtime orchestration lives here: renderer, feature scheduling, pass lifecycle, shader global uploads.
- Keep custom SRP architecture (`NWRPFeature` + focused `NWRPPass`) and avoid monolithic logic.

## Pass and Feature Rules

- New runtime rendering behavior must be introduced by one focused feature and one or more focused passes.
- Pass ordering must follow `NWRPPassEvent` contract. Do not add ad hoc ordering outside the enum flow.
- Any feature affecting runtime cost must have an explicit enable/disable path in `NewWorldRenderPipelineAsset`.

## Mobile Cost Constraints

- Minimize pass count, intermediate RT allocation, and full-screen operations.
- Prioritize bandwidth savings over ALU savings for default mobile path decisions.
- Prefer stable per-frame global uploads over hidden coupling between passes.

## Shadow-Specific Constraints

- Main light real-time shadows are the default supported shadow path.
- Keep shader global names and runtime IDs aligned (`NWRPShaderIds` and shader library declarations).
- Shadow filtering policy in runtime is temporarily `Hard` only on the stabilization branch.
- Keep `mainLightShadowBias` as user-facing depth bias and `mainLightShadowNormalBias` as user-facing normal bias.
- Keep fixed raster depth bias internal; do not expose it as another public asset setting without strong need.
- Upload a dedicated shadow-light direction for caster passes instead of reusing forward-light globals.
- Prioritize correct caster bias upload/application before adding any new receiver-side fixes.
