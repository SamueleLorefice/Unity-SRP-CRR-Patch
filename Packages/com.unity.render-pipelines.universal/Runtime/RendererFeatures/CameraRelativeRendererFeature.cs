using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.Universal
{
    [SupportedOnRenderer(typeof(UniversalRendererData))]
    [DisallowMultipleRendererFeature("Camera Relative Rendering")]
    public class CameraRelativeRenderingRendererFeature : ScriptableRendererFeature
    {
        private static readonly int CameraRelativeEnabledId = Shader.PropertyToID("_CameraRelativeEnabled");

        class CameraRelativePass : ScriptableRenderPass
        {
            private static readonly int CameraOriginId = Shader.PropertyToID("_CameraOriginWS");

            // Unity 6 uses RecordRenderGraph instead of the old Configure/Execute execution loop
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                // Extract modern camera data from the Frame Data container
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                if (cameraData == null || cameraData.camera.cameraType != CameraType.Game) return;

                // Add a raster pass to inject our global camera position before opaques render
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Camera Relative Setup", out var passData))
                {
                    passData.cameraPosition = cameraData.camera.transform.position;

                    // Prevent Render Graph from optimizing this pass away
                    builder.AllowPassCulling(false);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        // Define the keyword to indicate that camera-relative rendering is active
                        context.cmd.SetGlobalFloat(CameraRelativeEnabledId, 1.0f); // or 0.0f
                        // Cleanly set the global vector inside the current command buffer execution context
                        context.cmd.SetGlobalVector(CameraOriginId, data.cameraPosition);
                    });
                }
            }

            class PassData
            {
                public Vector3 cameraPosition;
            }
        }

        private CameraRelativePass m_ScriptablePass;

        public override void Create()
        {
            m_ScriptablePass = new CameraRelativePass();
            // Execute right before any geometry draws
            m_ScriptablePass.renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(m_ScriptablePass);
        }

        void ResetCameraRelativeFlag()
        {
            // No cmd buffer needed here; this is a global shader property.
            Shader.SetGlobalFloat(CameraRelativeEnabledId, 0.0f);
        }

        void OnDisable()
        {
            ResetCameraRelativeFlag();
        }

        protected override void Dispose(bool disposing)
        {
            ResetCameraRelativeFlag();
            base.Dispose(disposing);
        }
    }
}
