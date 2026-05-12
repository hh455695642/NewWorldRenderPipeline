using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP.Editor
{
    [InitializeOnLoad]
    internal static class NWRPCameraDataAutoAdd
    {
        private static bool s_Queued;

        static NWRPCameraDataAutoAdd()
        {
            EditorApplication.delayCall += QueueAutoAdd;
            EditorApplication.hierarchyChanged += QueueAutoAdd;
        }

        private static void QueueAutoAdd()
        {
            if (s_Queued)
            {
                return;
            }

            s_Queued = true;
            EditorApplication.delayCall += AutoAddCameraData;
        }

        private static void AutoAddCameraData()
        {
            s_Queued = false;
            if (Application.isPlaying || !IsNWRPActive())
            {
                return;
            }

            Camera[] cameras = Resources.FindObjectsOfTypeAll<Camera>();
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (!ShouldOwnCamera(camera)
                    || camera.GetComponent<NWRPCameraData>() != null)
                {
                    continue;
                }

                Undo.AddComponent<NWRPCameraData>(camera.gameObject);
                EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
            }
        }

        private static bool IsNWRPActive()
        {
            return GraphicsSettings.currentRenderPipeline is NewWorldRenderPipelineAsset
                || QualitySettings.renderPipeline is NewWorldRenderPipelineAsset;
        }

        private static bool ShouldOwnCamera(Camera camera)
        {
            return camera != null
                && camera.cameraType == CameraType.Game
                && camera.gameObject.scene.IsValid()
                && camera.gameObject.scene.isLoaded
                && !EditorUtility.IsPersistent(camera);
        }
    }
}
