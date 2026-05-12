using UnityEngine;

namespace NWRP
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class NWRPCameraData : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Enable NWRP-owned post-processing for this camera. Missing NWRPCameraData means off at runtime.")]
        private bool renderPostProcessing = true;

        [SerializeField]
        [Tooltip("Volume layers sampled by NWRP post-processing for this camera.")]
        private LayerMask volumeLayerMask = 1;

        [SerializeField]
        [Tooltip("Optional transform used as the Volume sampling position. Falls back to the Camera transform.")]
        private Transform volumeTrigger;

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
