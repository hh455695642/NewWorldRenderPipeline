using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP
{
    /// <summary>
    /// NewWorld 渲染管线主类。
    /// 继承 Unity SRP 框架的 RenderPipeline，实现自定义渲染循环。
    ///
    /// 渲染流程概览：
    ///   foreach (camera)
    ///     CameraRenderer.Render(context, camera)
    ///       ├─ Cull      → 裁剪不可见物体
    ///       ├─ Setup     → 设置相机属性 &amp; 清屏
    ///       ├─ Draw      → 绘制不透明 → 天空盒 → 透明物体
    ///       └─ Submit    → 提交命令到 GPU
    /// </summary>
    public class NewWorldRenderPipeline : RenderPipeline
    {
        private readonly NWRPRenderer _renderer = new NWRPRenderer();
        private readonly NewWorldRenderPipelineAsset _asset;

        public NewWorldRenderPipeline(NewWorldRenderPipelineAsset asset)
        {
            _asset = asset;
            GraphicsSettings.useScriptableRenderPipelineBatching = asset.useSRPBatcher;
            GraphicsSettings.lightsUseLinearIntensity = true;
        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            foreach (Camera camera in cameras)
            {
                _renderer.Render(context, camera, _asset);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _renderer.Dispose();
                _asset.DisposeRuntimeFeatures();
            }

            base.Dispose(disposing);
        }
    }
}
