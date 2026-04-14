# ShaderLibrary AGENTS

Local rules for `Assets/NWRP/ShaderLibrary`.

## Ownership

- Shared shader functions, shared lighting/shadow sampling, and cross-shader pass includes live here.
- Keep this folder reusable across lit/NPR/effect shaders.

## Include Organization

- Put cross-shader pass includes in `Assets/NWRP/ShaderLibrary/Passes`.
- Keep shader-family include folders (for example `Shaders/Lit/Includes`) as thin compatibility wrappers.
- Avoid duplicating pass logic across shader families.

## Mobile Shader Constraints

- Prefer `half` precision for shading math unless precision requires `float`.
- Avoid unnecessary texture samples and dynamic branches in baseline path.
- Keep variant growth bounded; prefer uniforms over new keywords for runtime toggles.

## Shadow Filtering Constraints

- Main light receiver filtering is temporarily `Hard` only on the stabilization branch.
- Keep shadow globals and semantics stable with runtime upload code.
- Shadow caster includes should consume the dedicated shadow-light direction upload used by the runtime shadow pass.
- Keep `Depth Bias` and `Normal Bias` semantics distinct in shared shader code; do not collapse both into one offset path.
- Prefer the simplest possible receiver path while hard-shadow correctness is being restored.
