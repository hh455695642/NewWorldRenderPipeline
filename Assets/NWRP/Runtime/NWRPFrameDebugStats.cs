namespace NWRP
{
    public enum NWRPFrameTemporaryRTKind
    {
        Color,
        Depth
    }

    public struct NWRPFrameDebugStats
    {
        public int cameraTargetBindCount;
        public int cameraTargetSkipCount;
        public int nonCameraTargetBindCount;
        public int fullscreenBlitCount;
        public int finalBlitCount;
        public int cameraColorCopyCount;
        public int cameraDepthCopyCount;
        public int shadowAtlasCopyCount;
        public int temporaryColorRTCount;
        public int temporaryDepthRTCount;

        public void RecordCameraTargetBind(bool skipped)
        {
            if (skipped)
            {
                cameraTargetSkipCount++;
                return;
            }

            cameraTargetBindCount++;
        }

        public void RecordNonCameraTargetBind()
        {
            nonCameraTargetBindCount++;
        }

        public void RecordFullscreenBlit()
        {
            fullscreenBlitCount++;
        }

        public void RecordFinalBlit()
        {
            finalBlitCount++;
            fullscreenBlitCount++;
        }

        public void RecordCameraColorCopy()
        {
            cameraColorCopyCount++;
            fullscreenBlitCount++;
        }

        public void RecordCameraDepthCopy()
        {
            cameraDepthCopyCount++;
            fullscreenBlitCount++;
        }

        public void RecordShadowAtlasCopy()
        {
            shadowAtlasCopyCount++;
        }

        public void RecordTemporaryRT(NWRPFrameTemporaryRTKind kind)
        {
            if (kind == NWRPFrameTemporaryRTKind.Depth)
            {
                temporaryDepthRTCount++;
                return;
            }

            temporaryColorRTCount++;
        }
    }
}
