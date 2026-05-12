using UnityEngine;
using UnityEngine.Serialization;

namespace NWRP
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class NWRPCameraData : MonoBehaviour
    {
        [SerializeField]
        [FormerlySerializedAs("renderPostProcessing")]
        [Tooltip("Enable NWRP-owned post-processing for this camera. Missing NWRPCameraData means off at runtime.")]
        private bool m_RenderPostProcessing = true;

        [SerializeField]
        [Tooltip("Volume layers sampled by NWRP post-processing for this camera.")]
        private LayerMask volumeLayerMask = 1;

        [SerializeField]
        [Tooltip("Optional transform used as the Volume sampling position. Falls back to the Camera transform.")]
        private Transform volumeTrigger;

        /// <summary>
        /// Matches URP AdditionalCameraData.renderPostProcessing naming. This is the
        /// user-facing camera setting, not the resolved per-frame runtime state.
        /// </summary>
        public bool renderPostProcessing
        {
            get => m_RenderPostProcessing;
            set => m_RenderPostProcessing = value;
        }

        /// <summary>
        /// Backward-compatible alias for older NWRP code. Prefer renderPostProcessing
        /// on the camera component and NWRPFrameData.postProcessingEnabled at runtime.
        /// </summary>
        public bool RenderPostProcessing => renderPostProcessing;

        public LayerMask VolumeLayerMask => volumeLayerMask;

        public Transform GetVolumeTrigger(Camera camera)
        {
            return volumeTrigger != null
                ? volumeTrigger
                : camera != null
                    ? camera.transform
                    : transform;
        }
    }
}
