using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP
{
    /// <summary>
    /// Compatibility facade kept for existing call sites.
    /// Actual rendering is handled by NWRPRenderer.
    /// </summary>
    public sealed class CameraRenderer
    {
        private readonly NWRPRenderer _renderer = new NWRPRenderer();

        public void Render(
            ScriptableRenderContext context,
            Camera camera,
            NewWorldRenderPipelineAsset asset
        )
        {
            _renderer.Render(context, camera, asset);
        }
    }
}
