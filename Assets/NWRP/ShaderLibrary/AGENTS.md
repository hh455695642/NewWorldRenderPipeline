# ShaderLibrary AGENTS

Local rules for `Assets/NWRP/ShaderLibrary`.

## Ownership

- Shared shader functions, shared lighting/shadow sampling, and cross-shader pass includes live here.
- Keep this folder reusable across lit/NPR/effect shaders.

## Include Organization

- Put cross-shader pass includes in `Assets/NWRP/ShaderLibrary/Passes`.
- Keep shader-family include folders (for example `Shaders/Lit/Includes`) as thin compatibility wrappers.
- Avoid duplicating pass logic across shader families.
- Do not include `Packages/com.unity.render-pipelines.universal/...` from NWRP-owned shader libraries.
- URP-style helper names are allowed for migration compatibility, but implementations must live in NWRP or Unity Core includes.

## Mobile Shader Constraints

- Prefer `half` precision for shading math unless precision requires `float`.
- Avoid unnecessary texture samples and dynamic branches in baseline path.
- Keep variant growth bounded; prefer uniforms over new keywords for runtime toggles.

## Shadow Filtering Constraints

- Main light receiver filtering defaults to `Hard`.
- `MediumPCF` is an explicit NWRP asset-selected mode. Do not add soft shadow, PCSS, or EVSM paths without approval and profiling.
- Keep shadow globals and semantics stable with runtime upload code.
- Shadow caster includes should consume the dedicated shadow-light direction upload used by the runtime shadow pass.
- Keep `Depth Bias` and `Normal Bias` semantics distinct in shared shader code; do not collapse both into one offset path.
- Prefer the simplest possible receiver path while hard-shadow correctness is being restored.
