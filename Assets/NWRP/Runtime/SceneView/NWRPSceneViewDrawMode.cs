#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NWRP
{
    internal static class NWRPSceneViewDrawMode
    {
        private static readonly HashSet<SceneView> s_RegisteredSceneViews =
            new HashSet<SceneView>();

        public static void SetupDrawMode()
        {
            EditorApplication.update -= UpdateSceneViewStates;
            EditorApplication.update += UpdateSceneViewStates;
            UpdateSceneViewStates();
        }

        public static void ResetDrawMode()
        {
            EditorApplication.update -= UpdateSceneViewStates;

            foreach (SceneView sceneView in s_RegisteredSceneViews)
            {
                if (sceneView != null)
                {
                    sceneView.onValidateCameraMode -= ValidateCameraMode;
                }
            }

            s_RegisteredSceneViews.Clear();
        }

        internal static bool ValidateCameraMode(SceneView.CameraMode cameraMode)
        {
            switch (cameraMode.drawMode)
            {
                case DrawCameraMode.ShadowCascades:
                case DrawCameraMode.RenderPaths:
                case DrawCameraMode.AlphaChannel:
                case DrawCameraMode.Overdraw:
                case DrawCameraMode.Mipmaps:
                case DrawCameraMode.SpriteMask:
                case DrawCameraMode.DeferredDiffuse:
                case DrawCameraMode.DeferredSpecular:
                case DrawCameraMode.DeferredSmoothness:
                case DrawCameraMode.DeferredNormal:
                case DrawCameraMode.ValidateAlbedo:
                case DrawCameraMode.ValidateMetalSpecular:
                case DrawCameraMode.TextureStreaming:
                    return false;
                default:
                    return true;
            }
        }

        internal static bool IsWireOverlayMode(DrawCameraMode drawMode)
        {
            return drawMode == DrawCameraMode.Wireframe
                || drawMode == DrawCameraMode.TexturedWire;
        }

        internal static bool TryGetDrawMode(Camera camera, out DrawCameraMode drawMode)
        {
            drawMode = DrawCameraMode.Textured;

            if (camera == null || camera.cameraType != CameraType.SceneView)
            {
                return false;
            }

            SceneView currentSceneView = SceneView.currentDrawingSceneView;
            if (currentSceneView != null
                && (currentSceneView.camera == camera || currentSceneView.camera == null))
            {
                drawMode = currentSceneView.cameraMode.drawMode;
                return true;
            }

            foreach (SceneView sceneView in SceneView.sceneViews)
            {
                if (sceneView == null || sceneView.camera != camera)
                {
                    continue;
                }

                drawMode = sceneView.cameraMode.drawMode;
                return true;
            }

            // SceneView rendering can hand SRP a temporary camera reference.
            if (currentSceneView != null)
            {
                drawMode = currentSceneView.cameraMode.drawMode;
                return true;
            }

            return false;
        }

        private static void UpdateSceneViewStates()
        {
            foreach (SceneView sceneView in SceneView.sceneViews)
            {
                if (sceneView == null || s_RegisteredSceneViews.Contains(sceneView))
                {
                    continue;
                }

                sceneView.onValidateCameraMode += ValidateCameraMode;
                s_RegisteredSceneViews.Add(sceneView);
            }
        }
    }
}
#endif
