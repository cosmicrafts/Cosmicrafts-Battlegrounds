using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using EPOOutline;
using System;

public class URPOutlineFeature : ScriptableRendererFeature
{
    private class SRPOutline : ScriptableRenderPass
    {
        private static List<Outlinable> temporaryOutlinables = new List<Outlinable>();

        public ScriptableRenderer Renderer;
        public bool UseColorTargetForDepth;
        public Outliner Outliner;
        public OutlineParameters Parameters = new OutlineParameters();

        private RTHandle colorTarget;
        private RTHandle depthTarget;

        public SRPOutline()
        {
            Parameters.CheckInitialization();
        }

        public void Setup(RTHandle color, RTHandle depth, Outliner outliner, ScriptableRenderer renderer)
        {
            colorTarget = color;
            depthTarget = depth;
            Outliner = outliner;
            Renderer = renderer;
        }

        [Obsolete("This method is obsolete. Override Execute(ScriptableRenderContext, CommandBuffer, ref RenderingData, ref RenderingDataDisplayOnly) instead.")]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (Outliner == null || !Outliner.enabled)
                return;

            var camera = renderingData.cameraData.camera;
            
#if UNITY_EDITOR
            Parameters.Buffer.name = camera.name;
#endif

            Outliner.UpdateSharedParameters(Parameters, camera, renderingData.cameraData.isSceneViewCamera);
            Outlinable.GetAllActiveOutlinables(Parameters.Camera, Parameters.OutlinablesToRender);
            RendererFilteringUtility.Filter(Parameters.Camera, Parameters);

            Parameters.TargetWidth = (int)(camera.scaledPixelWidth * renderingData.cameraData.renderScale);
            Parameters.TargetHeight = (int)(camera.scaledPixelHeight * renderingData.cameraData.renderScale);
            Parameters.Antialiasing = renderingData.cameraData.cameraTargetDescriptor.msaaSamples;

            Parameters.Target = colorTarget;
            Parameters.DepthTarget = UseColorTargetForDepth ? colorTarget : depthTarget;

            Parameters.Buffer.Clear();

            if (Outliner.RenderingStrategy == OutlineRenderingStrategy.Default)
            {
                OutlineEffect.SetupOutline(Parameters);
                Parameters.BlitMesh = null;
                Parameters.MeshPool.ReleaseAllMeshes();
            }
            else
            {
                temporaryOutlinables.Clear();
                temporaryOutlinables.AddRange(Parameters.OutlinablesToRender);
                Parameters.OutlinablesToRender.Clear();
                Parameters.OutlinablesToRender.Add(null);

                foreach (var outlinable in temporaryOutlinables)
                {
                    Parameters.OutlinablesToRender[0] = outlinable;
                    OutlineEffect.SetupOutline(Parameters);
                    Parameters.BlitMesh = null;
                }
                Parameters.MeshPool.ReleaseAllMeshes();
            }

            context.ExecuteCommandBuffer(Parameters.Buffer);
        }
    }

    private class Pool
    {
        private Stack<SRPOutline> outlines = new Stack<SRPOutline>();
        private List<SRPOutline> createdOutlines = new List<SRPOutline>();

        public SRPOutline Get()
        {
            if (outlines.Count == 0)
            {
                var outline = new SRPOutline();
                outlines.Push(outline);
                createdOutlines.Add(outline);
            }
            return outlines.Pop();
        }

        public void ReleaseAll()
        {
            outlines.Clear();
            foreach (var outline in createdOutlines)
                outlines.Push(outline);
        }
    }

    private GameObject lastSelectedCamera;
    private Pool outlinePool = new Pool();
    private List<Outliner> outliners = new List<Outliner>();

    private bool GetOutlinersToRenderWith(RenderingData renderingData, List<Outliner> outliners)
    {
        outliners.Clear();
        var camera = renderingData.cameraData.camera.gameObject;
        camera.GetComponents(outliners);
        if (outliners.Count == 0)
        {
#if UNITY_EDITOR
            if (renderingData.cameraData.isSceneViewCamera)
            {
                var foundObject = Array.Find(
                    Array.ConvertAll(UnityEditor.Selection.gameObjects, x => x.GetComponent<Outliner>()),
                    x => x != null);

                camera = foundObject?.gameObject ?? lastSelectedCamera;
                if (camera == null)
                    return false;
                else
                    camera.GetComponents(outliners);
            }
            else
                return false;
#else
                return false;
#endif
        }
        var hasOutliners = outliners.Count > 0;
        if (hasOutliners)
            lastSelectedCamera = camera;
        return hasOutliners;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!GetOutlinersToRenderWith(renderingData, outliners))
            return;

        // Store the variables with a pragma to acknowledge that we're using obsolete APIs
#pragma warning disable 0618
        var colorTarget = renderer.cameraColorTargetHandle;
        var depthTarget = renderer.cameraDepthTargetHandle;
#pragma warning restore 0618

        var shouldUseDepthTarget = renderingData.cameraData.requiresDepthTexture && 
                                   renderingData.cameraData.cameraTargetDescriptor.msaaSamples <= 1 &&
                                   !renderingData.cameraData.isSceneViewCamera;

        foreach (var outliner in outliners)
        {
            var outline = outlinePool.Get();
            outline.Setup(colorTarget, depthTarget, outliner, renderer);
            outline.UseColorTargetForDepth = !shouldUseDepthTarget;
            outline.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            renderer.EnqueuePass(outline);
        }

        outlinePool.ReleaseAll();
    }

    public override void Create() { }
}
