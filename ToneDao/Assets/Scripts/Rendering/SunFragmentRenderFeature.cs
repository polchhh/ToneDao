using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class SunFragmentRenderFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public LayerMask layerMask = 0;
    }

    public Settings settings = new();
    private SunFragmentPass _pass;

    public override void Create()
    {
        _pass = new SunFragmentPass(settings.layerMask)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer,
                                         ref RenderingData renderingData)
    {
        var camType = renderingData.cameraData.cameraType;
        if (camType != CameraType.Game && camType != CameraType.SceneView) return;
        renderer.EnqueuePass(_pass);
    }

    private class SunFragmentPass : ScriptableRenderPass
    {
        private readonly LayerMask _layerMask;

        private static readonly List<ShaderTagId> ShaderTags = new()
        {
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly"),
            new ShaderTagId("SRPDefaultUnlit"),
        };

        public SunFragmentPass(LayerMask layerMask)
        {
            _layerMask = layerMask;
        }

        private class PassData
        {
            public RendererListHandle RendererList;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph,
                                               ContextContainer frameData)
        {
            var renderingData = frameData.Get<UniversalRenderingData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();

            var sortSettings = new SortingSettings(cameraData.camera)
                { criteria = SortingCriteria.CommonOpaque };
            var drawSettings = new DrawingSettings(ShaderTags[0], sortSettings);
            for (int i = 1; i < ShaderTags.Count; i++)
                drawSettings.SetShaderPassName(i, ShaderTags[i]);

            var filtering = new FilteringSettings(RenderQueueRange.all, _layerMask);

            var rlParams = new RendererListParams(
                renderingData.cullResults, drawSettings, filtering);
            var rendererList = renderGraph.CreateRendererList(rlParams);

            using var builder = renderGraph.AddRasterRenderPass<PassData>(
                "SunFragment After PostFX", out var passData);

            passData.RendererList = rendererList;
            builder.UseRendererList(rendererList);

            builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
            builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture);
            builder.AllowPassCulling(false);

            builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
                ctx.cmd.DrawRendererList(data.RendererList));
        }
    }
}
