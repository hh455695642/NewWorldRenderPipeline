using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP
{
    public enum NWRPCameraAttachmentUsage
    {
        CameraSetup,
        ContinueCamera
    }

    public readonly struct NWRPCameraAttachmentPolicy
    {
        public readonly RenderBufferLoadAction colorLoadAction;
        public readonly RenderBufferStoreAction colorStoreAction;
        public readonly RenderBufferLoadAction depthLoadAction;
        public readonly RenderBufferStoreAction depthStoreAction;

        private NWRPCameraAttachmentPolicy(
            RenderBufferLoadAction colorLoadAction,
            RenderBufferStoreAction colorStoreAction,
            RenderBufferLoadAction depthLoadAction,
            RenderBufferStoreAction depthStoreAction)
        {
            this.colorLoadAction = colorLoadAction;
            this.colorStoreAction = colorStoreAction;
            this.depthLoadAction = depthLoadAction;
            this.depthStoreAction = depthStoreAction;
        }

        public static NWRPCameraAttachmentPolicy ForUsage(
            NWRPCameraAttachmentUsage usage,
            bool clearsColor,
            bool clearsDepth)
        {
            if (usage == NWRPCameraAttachmentUsage.CameraSetup)
            {
                return new NWRPCameraAttachmentPolicy(
                    clearsColor ? RenderBufferLoadAction.DontCare : RenderBufferLoadAction.Load,
                    RenderBufferStoreAction.Store,
                    clearsDepth ? RenderBufferLoadAction.DontCare : RenderBufferLoadAction.Load,
                    RenderBufferStoreAction.Store);
            }

            return new NWRPCameraAttachmentPolicy(
                RenderBufferLoadAction.Load,
                RenderBufferStoreAction.Store,
                RenderBufferLoadAction.Load,
                RenderBufferStoreAction.Store);
        }

        public bool Equals(NWRPCameraAttachmentPolicy other)
        {
            return colorLoadAction == other.colorLoadAction
                && colorStoreAction == other.colorStoreAction
                && depthLoadAction == other.depthLoadAction
                && depthStoreAction == other.depthStoreAction;
        }
    }

    public struct NWRPCameraAttachmentState
    {
        private bool _cameraTargetBound;
        private NWRPCameraAttachmentPolicy _policy;
        private Rect _viewport;

        public bool CanSkipCameraTargetBind(
            NWRPCameraAttachmentPolicy policy,
            Rect viewport)
        {
            return _cameraTargetBound
                && _policy.Equals(policy)
                && Mathf.Approximately(_viewport.x, viewport.x)
                && Mathf.Approximately(_viewport.y, viewport.y)
                && Mathf.Approximately(_viewport.width, viewport.width)
                && Mathf.Approximately(_viewport.height, viewport.height);
        }

        public void MarkCameraTargetBound(
            NWRPCameraAttachmentPolicy policy,
            Rect viewport)
        {
            _cameraTargetBound = true;
            _policy = policy;
            _viewport = viewport;
        }

        public void Invalidate()
        {
            _cameraTargetBound = false;
            _policy = default;
            _viewport = default;
        }
    }
}
