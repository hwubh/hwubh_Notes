# Unity学习笔记--反射探针扩展：URP中自定义Realtime Reflection Probe

## 提要：
本文主要在URP中自定义了反射探针以取代URP原生的反射探针，实现性能的提升。 Unity版本：2022.3.44f1.

## 前言： 
前阵子项目中在运行时渲染采用了反射探针时，发现有Drawcall数量过多（如生成7层mipmap的Convolution阶段就有120个, 而理论上只需要36个），性能消耗大的问题。 
又因为项目里不太让动引擎源码，所以尝试在C#层重新绘制反射探针。
版本号: 6000.3.2f1. + URP 17.3.0

## 绘制反射探针
参考管线中其他Manager类型，在“”目录下同样使用单例创建类型 `RealtimeReflectionProbeRendererManager`, 用于管理所有类型为*Realtime*的反射探针绘制。
``` C#
public class RealtimeReflectionProbeManager
{
    private static RealtimeReflectionProbeManager s_Instance;
    private static readonly object s_Lock = new object();

    /// <summary>
    /// 获取单例实例
    /// </summary>
    public static RealtimeReflectionProbeManager Instance
    {
        get
        {
            if (s_Instance == null)
            {
                lock (s_Lock)
                {
                    if (s_Instance == null)
                    {
                        s_Instance = new RealtimeReflectionProbeManager();
                    }
                }
            }
            return s_Instance;
        }
    }
}
```

因为在一帧中反射探针的内容只取决于自身的位置与场景，对于同一场景中的各个相机是相同。选择在每一次RenderLoop的开始，相机还没绘制场景之前进行反射探针的绘制。
在`UniversalRenderPipeline::Render` 函数中，调用`RealtimeReflectionProbeManager::Update`遍历所有相机的裁剪结果，收集场景中所有的可见的实时反射探针。
> `RealtimeReflectionProbeManager::Update`使用Time.renderFrameCount来限制同一渲染帧中只渲染一次。如果用的是Time.renderFrameCount则是同一逻辑帧中只渲染一次。 因为Scene相机不会触发Time.renderFrameCount/frameCount。希望Scene场景下也更新相机的话，可以使用自定义Timer。
<!-- > 如果存在同一逻辑帧中有多个渲染帧（如多display），而各个渲染帧对应的场景不同的话，也可以考虑使用后者。具体怎么做看实际项目需求？
> 此外Scene相机会触发Time.renderFrameCount，但不会触发Time.frameCount。 希望Scene场景下也更新相机的话，可以自定义Timer或使用Time.renderFrameCount。 -->
``` c#
protected override void Render(ScriptableRenderContext renderContext, List<Camera> cameras)
{
    \\... offscreenUIRenderedInCurrentFrame = false; ...

    // 处理实时反射探针：遍历所有相机收集可见的探针并渲染
    RealtimeReflectionProbeManager.Instance.Update(renderContext, cameras);

    \\... for (int i = 0; i < cameraCount; ++i) ...

}
```
``` C#
/// <summary>
/// 在渲染循环中更新，遍历所有相机收集可见的反射探针并渲染
/// </summary>
/// <param name="context">渲染上下文</param>
/// <param name="cameras">相机列表</param>
public void Update(ScriptableRenderContext context, List<Camera> cameras)
{
    int currentFrame = Time.frameCount;
    
    // 每帧只更新一次
    if (m_LastProbeUpdateFrame == currentFrame)
        return;

    // 收集所有相机可见的反射探针
    CollectVisibleProbes(context, cameras);

    m_LastProbeUpdateFrame = currentFrame;
}

/// <summary>
/// 从相机列表收集可见的反射探针
/// </summary>
/// <param name="context">渲染上下文</param>
/// <param name="cameras">相机列表</param>
private void CollectVisibleProbes(ScriptableRenderContext context, List<Camera> cameras)
{
    // 遍历所有相机，收集可见的反射探针
    foreach (var camera in cameras)
    {
        if (camera == null)
            continue;

        // 只处理游戏相机
        if (!IsGameCamera(camera) || camera.cameraType != CameraType.Game)
            continue;

        // 为每个相机创建剔除参数以获取可见的反射探针
        UniversalAdditionalCameraData additionalCameraData = null;
        camera.gameObject.TryGetComponent(out additionalCameraData);
        
        var cameraRenderer = UniversalRenderPipeline.GetRenderer(camera, additionalCameraData);
        if (cameraRenderer == null)
            continue;

        if (additionalCameraData != null && additionalCameraData.renderType != CameraRenderType.Base)
        {
            Debug.LogWarning("Only Base cameras can be rendered with standalone RenderSingleCamera. Camera will be skipped.");
            continue;
        }

        if (camera.targetTexture != null && (camera.targetTexture.width == 0 || camera.targetTexture.height == 0))
        {
            Debug.LogWarning($"Camera '{camera.name}' has an invalid render target size (width: {camera.targetTexture.width}, height: {camera.targetTexture.height}). Camera will be skipped.");
            continue;
        }

        if (camera.pixelWidth == 0 || camera.pixelHeight == 0)
        {
            Debug.LogWarning($"Camera '{camera.name}' has invalid pixel dimensions (width: {camera.pixelWidth}, height: {camera.pixelHeight}). Camera will be skipped.");
            continue;
        }

        var frameData = cameraRenderer.frameData;
        var cameraData = UniversalRenderPipeline.CreateCameraData(frameData, camera, additionalCameraData);
        
        if (!UniversalRenderPipeline.TryGetCullingParameters(cameraData, out var cullingParameters))
            continue;

        var cullResults = context.Cull(ref cullingParameters);
        
        // 收集这个相机可见的探针
        var visibleProbes = cullResults.visibleReflectionProbes;
        for (int i = 0; i < visibleProbes.Length; i++)
        {
            var probe = visibleProbes[i].reflectionProbe;
            if (probe != null && probe.mode == ReflectionProbeMode.Realtime)
            {
                processedProbes.Add(probe);
            }
        }
    }
}
```

因为需要各个反射探针都有各自的Realtime Texture。 这里创建组件`UniversalAdditionalReflectionProbeData`附加到各个反射探针上，存放各种附加到反射探针上的自定义数据。 这里先在其中创建一个RTHandle m_CustomRealtimeTexture 作为渲染目标。
``` C#
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// 为反射探针提供额外的渲染数据
    /// 包含每个探针自己管理的实时纹理 RTHandle
    /// </summary>
    [RequireComponent(typeof(ReflectionProbe))]
    [DisallowMultipleComponent]
    public class UniversalAdditionalReflectionProbeData : MonoBehaviour
    {
        [SerializeField]
        private RTHandle m_CustomRealtimeTexture;

        /// <summary>
        /// 获取或设置自定义实时纹理
        /// </summary>
        public RTHandle customRealtimeTexture
        {
            get => m_CustomRealtimeTexture;
            set => m_CustomRealtimeTexture = value;
        }

        /// <summary>
        /// 释放实时纹理
        /// </summary>
        public void ReleaseRealtimeTexture()
        {
            if (m_CustomRealtimeTexture != null)
            {
                // 释放到资源池里
                TextureDesc currentRTDesc = RTHandleResourcePool.CreateTextureDesc(handle.rt.descriptor, TextureSizeMode.Explicit, handle.rt.anisoLevel, handle.rt.mipMapBias, handle.rt.filterMode, handle.rt.wrapMode, handle.name);
                RenderingUtils.AddStaleResourceToPoolOrRelease(currentRTDesc, handle);
                m_CustomRealtimeTexture = null;
            }
        }

        private void OnDestroy()
        {
            ReleaseRealtimeTexture();
        }
    }

    /// <summary>
    /// ReflectionProbe 的扩展方法
    /// </summary>
    public static class ReflectionProbeExtensions
    {
        /// <summary>
        /// 获取或创建反射探针的额外数据组件
        /// </summary>
        /// <param name="probe">反射探针</param>
        /// <returns>额外数据组件</returns>
        public static UniversalAdditionalReflectionProbeData GetUniversalAdditionalReflectionProbeData(this ReflectionProbe probe)
        {
            if (probe == null)
                return null;

            var gameObject = probe.gameObject;
            if (!gameObject.TryGetComponent<UniversalAdditionalReflectionProbeData>(out var probeData))
            {
                probeData = gameObject.AddComponent<UniversalAdditionalReflectionProbeData>();
            }

            return probeData;
        }
    }
}
```
添加反弹探针的扩展函数，未没有组件`UniversalAdditionalReflectionProbeData`自动添加。
``` C#
/// <summary>
/// ReflectionProbe 的扩展方法
/// </summary>
public static class ReflectionProbeExtensions
{
    /// <summary>
    /// 获取或创建反射探针的额外数据组件
    /// </summary>
    /// <param name="probe">反射探针</param>
    /// <returns>额外数据组件</returns>
    public static UniversalAdditionalReflectionProbeData GetUniversalAdditionalReflectionProbeData(this ReflectionProbe probe)
    {
        if (probe == null)
            return null;

        var gameObject = probe.gameObject;
        if (!gameObject.TryGetComponent<UniversalAdditionalReflectionProbeData>(out var probeData))
        {
            probeData = gameObject.AddComponent<UniversalAdditionalReflectionProbeData>();
        }

        return probeData;
    }
}
```
此外别忘了在ModelPostProcessor.cs 加上`UniversalAdditionalReflectionProbeData`，保证Editor中创建反射探针时，同时添加上该组件。
``` C#
class ModelPostprocessor : AssetPostprocessor
{
    void OnPostprocessModel(GameObject go)
    {
        CoreEditorUtils.AddAdditionalData<Camera, UniversalAdditionalCameraData>(go);
        CoreEditorUtils.AddAdditionalData<Light, UniversalAdditionalLightData>(go);
        CoreEditorUtils.AddAdditionalData<ReflectionProbe, UniversalAdditionalReflectionProbeData>(go);
    }
}
```

反射探针的绘制大致分为两步，首先是沿三维坐标的六个方向绘制六个面到Cubemap上。然后是通过镜面卷积生成对应各个粗糙度的Mipmap。 
> Unity中baked的反射探针为GGX重要性采样。 而Realtime时，截帧来看是用的类似高斯模糊做的近似处理。

这里先处理第一步。 
创建类`RealtimeReflectionProbeRenderer`，负责放入渲染反射探针相关的逻辑。
首先声明一个相机，用于绘制反射探针Cubemap的六个面。
``` C#
using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.Universal 
{
    public class RealtimeReflectionProbeRenderer
    {
        Camera renderFaceCamera;
        Camera m_RenderFaceCamera
        {
            get
            {
                if (renderFaceCamera == null)
                {
                    GameObject cameraGo = new GameObject("Reflection Probes Camera");
                    cameraGo.hideFlags = HideFlags.HideAndDontSave;
                    renderFaceCamera = cameraGo.AddComponent<Camera>();
                    cameraGo.AddComponent<UniversalAdditionalCameraData>();
                    renderFaceCamera.enabled = false;
                    renderFaceCamera.cameraType = CameraType.Reflection;
                    renderFaceCamera.allowHDR = true;
                    renderFaceCamera.useOcclusionCulling = true;
                    renderFaceCamera.orthographic = false;
                    renderFaceCamera.allowMSAA = false;
                    renderFaceCamera.fieldOfView = 90f;
                    renderFaceCamera.aspect = 1f;
                }

                return renderFaceCamera;
            }
        }
    }
}
```
遍历收集到的反射探针，逐一进行绘制。
``` C#
/// <summary>
/// 获取或创建反射探针渲染器
/// </summary>
private RealtimeReflectionProbeRenderer renderer
{
    get
    {
        if (m_Renderer == null)
        {
            m_Renderer = new RealtimeReflectionProbeRenderer();
        }
        return m_Renderer;
    }
}

public void Update(ScriptableRenderContext context, List<Camera> cameras)
{
    //... 
    
    // 清空已处理的探针集合
    processedProbes.Clear();

    // 收集所有相机可见的反射探针
    CollectVisibleProbes(context, cameras);

    // 创建副本以避免在枚举期间集合被修改（例如递归渲染调用）
    var probesToRender = new List<ReflectionProbe>(processedProbes);

    // 渲染所有收集到的实时反射探针
    foreach (var probe in processedProbes)
    {
        if (probe != null && probe.mode == ReflectionProbeMode.Realtime)
        {
            renderer.RenderAllCubemapFaces(probe);
        }
    }

    m_LastProbeUpdateFrame = currentFrame;
}
```
根据反射探针上的参数，创建其自定义RT。
```C#
/// <summary>
/// 渲染所有六个立方体面
/// </summary>
/// <param name="probe">反射探针</param>
public void RenderAllCubemapFaces(ReflectionProbe probe)
{
    var probeData = probe.GetUniversalAdditionalReflectionProbeData();
    if (probe == null || probeData == null)
        return;

    // 确保实时纹理已创建
    probeData.EnsureRealtimeTexture(probe);
    var cubemapTexture = probeData.customRealtimeTexture;
    
    if (cubemapTexture == null)
    {
        Debug.LogError($"Failed to create realtime texture for ReflectionProbe '{probe.name}'.");
        return;
    }

    // 使用探针的自定义纹理进行渲染
    RenderRealtimeReflectionProbe(probe, ref cubemapTexture);
}

/// <summary>
/// 确保实时纹理已创建
/// </summary>
/// <param name="probe">反射探针</param>
public void EnsureRealtimeTexture(ReflectionProbe probe)
{
    if (probe == null)
        return;

    if (m_CustomRealtimeTexture != null && m_CustomRealtimeTexture.rt != null)
        return;

    // 创建 Cubemap RenderTexture 描述符
    var descriptor = new RenderTextureDescriptor(probe.resolution, probe.resolution);
    var asset = UniversalRenderPipeline.asset;
    descriptor.graphicsFormat = UniversalRenderPipeline.MakeRenderTextureGraphicsFormat(
        probe.hdr && asset != null && asset.supportsHDR,
        asset != null ? asset.hdrColorBufferPrecision : HDRColorBufferPrecision._32Bits,
        Graphics.preserveFramebufferAlpha);
    descriptor.dimension = TextureDimension.Cube;
    descriptor.volumeDepth = 1;
    descriptor.useMipMap = true;
    descriptor.msaaSamples = 1;
    descriptor.enableRandomWrite = false;
    descriptor.bindMS = false;
    descriptor.useDynamicScale = false;
    descriptor.depthBufferBits = 0;
    descriptor.stencilFormat = GraphicsFormat.None;
    descriptor.sRGB = (QualitySettings.activeColorSpace == ColorSpace.Linear);
    descriptor.depthStencilFormat = GraphicsFormat.None;
    descriptor.autoGenerateMips = true;

    // 使用 RenderingUtils.ReAllocateHandleIfNeeded 创建 RTHandle
    string name = $"ReflectionProbe_{probe.GetInstanceID()}_RealtimeTexture";
    RenderingUtils.ReAllocateHandleIfNeeded(ref m_CustomRealtimeTexture, descriptor, FilterMode.Trilinear, TextureWrapMode.Clamp, 1, 0, name);
}
```
根据反射探针上的参数，调整相机上的参数。
```C#
/// <summary>
/// 渲染实时反射探针到指定的 Cubemap 纹理
/// </summary>
/// <param name="probe">反射探针</param>
/// <param name="cubemapTexture">目标 Cubemap RTHandle</param>
public void RenderRealtimeReflectionProbe(ReflectionProbe probe, ref RTHandle cubemapTexture)
{
    if (probe == null || cubemapTexture == null)
        return;

    // 获取当前渲染管线
    var asset = UniversalRenderPipeline.asset;
    asset.shadowDistance = probe.shadowDistance;

    // 将 ReflectionProbe 中与相机相关的属性赋值到相机上
    m_RenderFaceCamera.nearClipPlane = probe.nearClipPlane;
    m_RenderFaceCamera.farClipPlane = probe.farClipPlane;
    // ReflectionProbeClearFlags 转换为 CameraClearFlags
    m_RenderFaceCamera.clearFlags = (CameraClearFlags)probe.clearFlags;
    m_RenderFaceCamera.backgroundColor = probe.backgroundColor;
    m_RenderFaceCamera.cullingMask = probe.cullingMask;
    m_RenderFaceCamera.allowHDR = probe.hdr;
    m_RenderFaceCamera.gameObject.transform.SetPositionAndRotation(probe.transform.position, Quaternion.identity);
}
```
因为URP不太支持使用Cubemap作为RenderTarget，这里声明一张全局的2D RT `m_FaceRT`作为中转。先渲染到`m_FaceRT`上，再copy到Cubemap对应的面上。
这里`m_FaceRT`用RenderTexture，而不像cubemap那样用RTHandle。是因为`m_FaceRT`是作为RenderTarget创建的， 其需要包含Color 和 Depth的信息。 RenderTexture是同时对应有Color和Depth两个缓冲区的，而RTHandle 是明确只对应单个缓冲区的。 这里cubemap不涉及具体的渲染，不需要Depth，用RTHandle也方便用现成的对象池机制。 详见这篇[讨论](https://discussions.unity.com/t/fixing-rendertexturedescriptor-warning/1562637/7)。
> 其实也不是用不了Cubemap，但我这实测打包后在Vulkan上，深度缓冲好像有点问题。而且如果要用的话，还得把CubemapFace信息一路传到最底下创建RenderTargetIdentifier的地方。
``` C#
public void RenderRealtimeReflectionProbe(ScriptableRenderContext context, ReflectionProbe probe, ref RTHandle cubemapTexture)
{
    // 将 ReflectionProbe 中与相机相关的属性赋值到相机上
    //...

    // 准备用于渲染每个 cubemap 面的 RenderTextureDescriptor
    // 参考 URP 的 ProcessRenderRequests 实现方式
    RenderTextureDescriptor faceRTDesc = cubemapTexture.rt.descriptor;
    faceRTDesc.dimension = TextureDimension.Tex2D;
    faceRTDesc.useMipMap = false;
    faceRTDesc.depthBufferBits = 24;

    // 确保 m_FaceRT 已创建或重新分配（如果需要）
    var fullName = CoreUtils.GetRenderTargetAutoName(faceRTDesc.width, faceRTDesc.height, faceRTDesc.volumeDepth, faceRTDesc.graphicsFormat, faceRTDesc.dimension, "ReflectionProbe_FaceRT", false, false, MSAASamples.None, false, false);

    m_FaceRT = new RenderTexture(faceRTDesc);

    m_FaceRT.name = fullName;

    m_FaceRT.anisoLevel = 1;
    m_FaceRT.mipMapBias = 0;
    m_FaceRT.hideFlags = HideFlags.HideAndDontSave;
    m_FaceRT.filterMode = FilterMode.Trilinear;

    m_FaceRT.wrapModeU = TextureWrapMode.Clamp;
    m_FaceRT.wrapModeV = TextureWrapMode.Clamp;
    m_FaceRT.wrapModeW = TextureWrapMode.Clamp;

    m_FaceRT.Create();
}
```
这里也存在另一种做法是修改 RenderingUtils.ReAllocateHandleIfNeeded，传入一个bool值，使之在创建RT时保留Depth相关的信息。
``` C#
RenderingUtils.ReAllocateHandleIfNeeded(ref m_FaceRT, faceRTDesc, FilterMode.Point, TextureWrapMode.Clamp, 1, 0, "ReflectionProbe_FaceRT", true);

public static bool ReAllocateHandleIfNeeded(
    ref RTHandle handle,
    in RenderTextureDescriptor descriptor,
    FilterMode filterMode = FilterMode.Point,
    TextureWrapMode wrapMode = TextureWrapMode.Repeat,
    int anisoLevel = 1,
    float mipMapBias = 0,
    string name = "",
    bool isRenderTarget = false)
{
    if(!isRenderTarget)
        Assertions.Assert.IsTrue(descriptor.graphicsFormat == GraphicsFormat.None ^ descriptor.depthStencilFormat == GraphicsFormat.None);

    //...
    handle = RTHandles.Alloc(descriptor.width, descriptor.height, allocInfo, isRenderTarget);
    //...
}

public static RTHandle Alloc(int width, int height, RTHandleAllocInfo info, bool isRenderTarget = false)
{
    return s_DefaultInstance.Alloc(width, height, info, isRenderTarget);
}

public RTHandle Alloc(int width, int height, RTHandleAllocInfo info, bool isRenderTarget = false)
{
    var rt = CreateRenderTexture(
            width, height, info.format, info.slices, info.filterMode, info.wrapModeU, info.wrapModeV, info.wrapModeW, info.dimension, info.enableRandomWrite, info.useMipMap
            , info.autoGenerateMips, info.isShadowMap, info.anisoLevel, info.mipMapBias, info.msaaSamples, info.bindTextureMS
            , info.useDynamicScale, info.useDynamicScaleExplicit, info.memoryless, info.vrUsage, info.enableShadingRate, info.name, isRenderTarget);
    //...
}

private RenderTexture CreateRenderTexture(
    int width,
    int height,
    GraphicsFormat format,
    int slices,
    FilterMode filterMode,
    TextureWrapMode wrapModeU,
    TextureWrapMode wrapModeV,
    TextureWrapMode wrapModeW,
    TextureDimension dimension,
    bool enableRandomWrite,
    bool useMipMap,
    bool autoGenerateMips,
    bool isShadowMap,
    int anisoLevel,
    float mipMapBias,
    MSAASamples msaaSamples,
    bool bindTextureMS,
    bool useDynamicScale,
    bool useDynamicScaleExplicit,
    RenderTextureMemoryless memoryless,
    VRTextureUsage vrUsage,
    bool enableShadingRate,
    string name,
    bool isRenderTarget = false)
{
    //
    if(isRenderTarget)
    {
        colorFormat = format;
        depthStencilFormat = GraphicsFormatUtility.GetDepthStencilFormat(24, 0);
        stencilFormat = GraphicsFormat.None;
        shadowSamplingMode = ShadowSamplingMode.None;

        fullName = CoreUtils.GetRenderTargetAutoName(width, height, slices, format, dimension, name, mips: useMipMap, enableMSAA: enableMSAA, msaaSamples: msaaSamples, dynamicRes: useDynamicScale, dynamicResExplicit: useDynamicScaleExplicit);
    }
    else 
    //...if (isShadowMap)
}
```
接下来就是要调整调整相机的VP矩阵，依次渲染六个面进行渲染。 这里需要注意的是，我们渲染面到Cubemap时需要反转Y轴，否则最后的Cubemap结果会是上下颠倒的。 因而我们在定义相机的View矩阵时会手动反转Y轴的正反。 但这个操作又会反转View矩阵的手性，导致物体的正面被错误剔除，因此我们还需要通过`cmd.SetInvertCulling`反转剔除面。
``` C#
public void RenderRealtimeReflectionProbe(ScriptableRenderContext context, ReflectionProbe probe, ref RTHandle cubemapTexture)
{
    // 确保 m_FaceRT 已创建或重新分配（如果需要）
    //...

    for (int face = 0; face < 6; face++)
    {
        m_RenderFaceCamera.worldToCameraMatrix = Matrix4x4.identity.SetViewMatrix(m_RenderFaceCamera.gameObject.transform, face);

        // 渲染时应用 cmd.SetInvertCulling
    }
}

/// <summary>
/// 矩阵扩展方法，用于计算 Cubemap 面的 View 矩阵
/// </summary>
public static class MatrixExtensions
{
    // Cubemap 六个面的方向向量定义（在探针的本地坐标系中）
    // 顺序：PositiveX, NegativeX, PositiveY, NegativeY, PositiveZ, NegativeZ
    private static readonly Vector3[] faceRights = {
        new Vector3( 0, 0,-1),  // Positive X (右侧) - 看向 +X 方向
        new Vector3( 0, 0, 1),  // Negative X (左侧) - 看向 -X 方向
        new Vector3( 1, 0, 0),  // Positive Y (上方) - 看向 +Y 方向
        new Vector3( 1, 0, 0),  // Negative Y (下方) - 看向 -Y 方向
        new Vector3( 1, 0, 0),  // Positive Z (前方) - 看向 +Z 方向
        new Vector3(-1, 0, 0)   // Negative Z (后方) - 看向 -Z 方向
    };

    // 手动反转Y轴，因为反射探针的画面是上下颠倒的。
    private static readonly Vector3[] faceUps = {
        new Vector3(0,-1, 0),   // Positive X - 上方向为 +Y
        new Vector3(0,-1, 0),   // Negative X - 上方向为 +Y
        new Vector3(0, 0, 1),   // Positive Y - 上方向为 -Z
        new Vector3(0, 0,-1),   // Negative Y - 上方向为 +Z
        new Vector3(0,-1, 0),   // Positive Z - 上方向为 +Y
        new Vector3(0,-1, 0)    // Negative Z - 上方向为 +Y
    };

    private static readonly Vector3[] faceForwards  = {
        new Vector3(-1, 0, 0),   // Positive X - 右方向为 -Z
        new Vector3( 1, 0, 0),   // Negative X - 右方向为 +Z
        new Vector3( 0,-1, 0),   // Positive Y - 右方向为 +X
        new Vector3( 0, 1, 0),   // Negative Y - 右方向为 +X
        new Vector3( 0, 0,-1),   // Positive Z - 右方向为 +X
        new Vector3( 0, 0, 1)    // Negative Z - 右方向为 -X
    };

    /// <summary>
    /// 为 Cubemap 的指定面设置 View 矩阵（worldToCameraMatrix）
    /// 此方法会正确处理手性问题，确保正确的面剔除行为
    /// </summary>
    /// <param name="matrix">要设置的矩阵（通常为 worldToCameraMatrix）</param>
    /// <param name="cameraPosition">相机在世界空间中的位置</param>
    /// <param name="face">Cubemap 面的索引 (0-5: PositiveX, NegativeX, PositiveY, NegativeY, PositiveZ, NegativeZ)</param>
    /// <returns>计算好的 worldToCameraMatrix</returns>
    public static Matrix4x4 SetViewMatrix(this Matrix4x4 matrix, Transform cameraTransform, int face)
    {
        if (face < 0 || face >= 6)
        {
            Debug.LogError($"Invalid cubemap face index: {face}. Must be between 0 and 5.");
            return matrix;
        }

        // 获取该面的方向向量（在探针的本地坐标系中，假设探针 transform 为 identity）
        Vector3 right = faceRights[face];
        Vector3 up = faceUps[face];
        Vector3 forward = faceForwards[face];

        Vector3 worldRight = cameraTransform.TransformDirection(right);
        Vector3 worldUp = cameraTransform.TransformDirection(up);
        Vector3 worldForward = cameraTransform.TransformDirection(forward);

        // 构建 View 矩阵
        // View 矩阵的行向量应该是 (right, up, -forward)
        // 因为相机空间是右手坐标系，Z 轴指向相机后方（负 forward 方向）
        Matrix4x4 viewMatrix = new Matrix4x4();
        
        // 设置行向量：第一行 = right, 第二行 = up, 第三行 = -forward
        viewMatrix.SetRow(0, new Vector4(worldRight.x, worldRight.y, worldRight.z, 0));
        viewMatrix.SetRow(1, new Vector4(worldUp.x, worldUp.y, worldUp.z, 0));
        viewMatrix.SetRow(2, new Vector4(worldForward.x, worldForward.y, worldForward.z, 0));
        viewMatrix.SetRow(3, new Vector4(0, 0, 0, 1));

        // 应用平移：将世界坐标转换为相机相对坐标
        Matrix4x4 translateMatrix = Matrix4x4.Translate(-cameraTransform.position);
        Matrix4x4 resultMatrix = viewMatrix * translateMatrix;

        return resultMatrix;
    }
}
```
参照管线中渲染相机的方式，构建CameraData, AdditionalCameraData. 然后参照`UniversalRenderPipeline.RenderSingleCamera`创建函数`UniversalRenderPipeline.RenderSingleCameraForReflectionProbe`, 专门用于绘制反射探针。
``` C#
/// <summary>
/// Renders a single camera for reflection probe cubemap face.
/// This method is similar to RenderSingleCamera, but performs a copy operation to the cubemap face
/// before submitting the context, ensuring the copy happens in the same submit as the rendering.
/// </summary>
/// <param name="context">Render context used to record commands during execution.</param>
/// <param name="cameraData">Camera rendering data.</param>
/// <param name="sourceTexture">Source render texture (the face render target).</param>
/// <param name="cubemapTexture">Target cubemap texture.</param>
/// <param name="cubemapFace">Target cubemap face to copy to.</param>
internal static void RenderSingleCameraForReflectionProbe(ScriptableRenderContext context, UniversalCameraData cameraData, RenderTexture sourceTexture, RenderTexture cubemapTexture, CubemapFace cubemapFace)
{
    //...using (new ProfilingScope(cmdScope, cameraMetadata.sampler))
    {
        //...renderer.AddRenderPasses(ref legacyRenderingData);

        // 手动反转剔除面。 姑且放在这里
        cmd.SetInvertCulling(true);

        //...if (!useRenderGraph) 

        cmd.SetInvertCulling(false);

        // Copy the rendered face to the cubemap before submitting
        // This ensures the copy happens in the same submit as the rendering
        if (sourceTexture != null && cubemapTexture != null)
        {
            if ((SystemInfo.copyTextureSupport & CopyTextureSupport.DifferentTypes) != 0)
            {
                cmd.CopyTexture(sourceTexture, 0, 0, cubemapTexture, (int)cubemapFace, 0);
            }
            else
            {
                Debug.LogError($"CopyTexture to cubemap face is not supported on this platform. ReflectionProbe face {cubemapFace} will not be copied.");
            }
        }
    } // When ProfilingSample goes out of scope, an "EndSample" command is enqueued into CommandBuffer cmd

    //...context.ExecuteCommandBuffer(cmd); // Sends to ScriptableRenderContext all the commands enqueued since cmd.Clear, including the copy command
}
```
点开Frame Debugger，可以看的反射探针已经开始渲染了。
![20260129102729](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20260129102729.png)
为了更好的对比效果，我们需要将绘制好的Cubemap传回反射探针中，使之像下图中baked的反射探针一样可以被预览。
![20260129102946](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20260129102946.png)
为此我们需要重载反射探针的编辑器[ReflectionProbeEditor](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/Inspector/ReflectionProbeEditor.cs). 在"com.unity.render-pipelines.universal\Editor\"目录下创建脚本**ReflectionProbeEditor.cs**, 将从UnityCsReference拿到的代码直接贴入，然后通过反射绕过所有的访问限制。
> 文本量较大，这里就不贴出了。可以参考项目源代码：。
为了使我们自定义Cubemap图可以通过接口 `ReflectionProbe.realtimeTexture`传入，我们需要让Untiy不再自行更新实时反射探针。 具体做法是将在`UniversalAdditionalReflectionProbeData`添加自定义的`RefreshMode`替代反射探针上原有的`RefreshMode`, 然后将后者恒定设置为"ViaScripting"以防止触发更新。 同时，我们也需要修改编辑器，以支持自定义的`RefreshMode`。
``` C#
public class UniversalAdditionalReflectionProbeData : MonoBehaviour
{
    [SerializeField]
    private ReflectionProbeRefreshMode m_CustomRefreshMode = ReflectionProbeRefreshMode.OnAwake;

    /// <summary>
    /// 获取或设置自定义刷新模式
    /// 此模式用于控制反射探针的更新行为，替代 ReflectionProbe 原生的 refreshMode
    /// </summary>
    public ReflectionProbeRefreshMode customRefreshMode
    {
        get => m_CustomRefreshMode;
        set => m_CustomRefreshMode = value;
    }
}

internal class ReflectionProbeEditor : Editor
{
    //... SerializedProperty[] m_NearAndFarProperties;
    // UniversalAdditionalReflectionProbeData 的 SerializedObject，用于访问其所有序列化属性
    SerializedObject m_ProbeDataSerializedObject;
    SerializedProperty m_CustomRefreshMode; // 自定义 RefreshMode（来自 UniversalAdditionalReflectionProbeData）    \

    //... ReflectionProbe p = target as ReflectionProbe;
    // 查找 UniversalAdditionalReflectionProbeData 组件并创建 SerializedObject
    if (p != null)
    {
        var probeData = p.GetUniversalAdditionalReflectionProbeData();
        if (probeData != null)
        {
            // 创建 UniversalAdditionalReflectionProbeData 的 SerializedObject
            m_ProbeDataSerializedObject = new SerializedObject(probeData);
            m_CustomRefreshMode = m_ProbeDataSerializedObject.FindProperty("m_CustomRefreshMode");
        }
        else
        {
            m_ProbeDataSerializedObject = null;
            m_CustomRefreshMode = null;
        }
    }

    //... m_CachedGizmoMaterials.Clear();
    m_ProbeDataSerializedObject = null;

    //.. serializedObject.Update();
    // 更新 UniversalAdditionalReflectionProbeData 的 SerializedObject（如果存在）
    if (m_ProbeDataSerializedObject != null)
    {
        m_ProbeDataSerializedObject.Update();
    }

    //... if (EditorGUILayout.BeginFadeGroup(m_ShowProbeModeRealtimeOptions.faded)){
    // 显示自定义的 RefreshMode（来自 UniversalAdditionalReflectionProbeData）
    // 而不是 ReflectionProbe 原生的 refreshMode（已固定为 ViaScripting）
    ReflectionProbe probe = reflectionProbeTarget;
    if (m_ProbeDataSerializedObject != null && m_CustomRefreshMode != null)
    {
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(m_CustomRefreshMode, Styles.refreshMode);
        if (EditorGUI.EndChangeCheck())
        {                        
            // 确保 ReflectionProbe 的 refreshMode 保持为 ViaScripting
            if (probe.refreshMode != ReflectionProbeRefreshMode.ViaScripting)
            {
                Undo.RecordObject(probe, "Set ReflectionProbe RefreshMode to ViaScripting");
                probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                EditorUtility.SetDirty(probe);
            }
        }
    }
    else
    {
        // Fallback: 如果没有 UniversalAdditionalReflectionProbeData，显示原生的 RefreshMode
        EditorGUILayout.PropertyField(m_RefreshMode, Styles.refreshMode);
    }

    //... serializedObject.ApplyModifiedProperties();
    // 应用 UniversalAdditionalReflectionProbeData 的修改（如果存在）
    if (m_ProbeDataSerializedObject != null)
    {
        m_ProbeDataSerializedObject.ApplyModifiedProperties();
    }
}

public void RenderRealtimeReflectionProbe(ScriptableRenderContext context, ReflectionProbe probe, ref RTHandle cubemapTexture)
{
    //.. 最后面
    // 恢复相机的 targetTexture
    m_RenderFaceCamera.targetTexture = null;
    // 更新反射探针的实时纹理
    probe.realtimeTexture = cubemapTexture;
}

```
对比下使用Unity Baked的效果和我们绘制的结果。
![20260129102610](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20260129102610.png)

处理好Mip0的渲染后，既可以开始进行渲染各级mip。这里主要参考了HDRP中的实现。
首先创建一张Cubemap`m_IntermediumRT` 作为中转，将`m_FaceRT`的渲染结果转而复制到这张图上。 而原先的Cubemap储存卷积后的结果。
```C#
public void RenderRealtimeReflectionProbe(ScriptableRenderContext context, ReflectionProbe probe, ref RTHandle cubemapTexture)
{
    //...            if(!RenderingUtils.ReAllocateHandleIfNeeded(ref m_FaceRT, faceRTDesc, FilterMode.Point, TextureWrapMode.Clamp, 1, 0, "ReflectionProbe_FaceRT", true)) return;
    // 创建中转纹理，用于存储渲染结果并生成 mipmap
    RenderTextureDescriptor intermediumRTDesc = faceRTDesc;
    intermediumRTDesc.useMipMap = true; // 启用 mipmap
    intermediumRTDesc.dimension = TextureDimension.Cube;
    intermediumRTDesc.depthBufferBits = 0;
    if(!RenderingUtils.ReAllocateHandleIfNeeded(ref m_IntermediumRT, intermediumRTDesc, FilterMode.Trilinear, TextureWrapMode.Clamp, 1, 0, "ReflectionProbe_IntermediumRT"))
        return;

    //... cameraData.antialiasing = AntialiasingMode.None;

    // 使用专门为反射探针设计的方法，它会在渲染完成后自动 copy 到 cubemap
    UniversalRenderPipeline.RenderSingleCameraForReflectionProbe(context, cameraData, ref m_FaceRT, ref m_IntermediumRT, (CubemapFace)face);
}
```
从`CommandBufferPool`中获取一个CommandBuffer对象，让`m_IntermediumRT`生成mipmap. 创建函数`FilterCubemapGGX`生成各个粗糙度的预过滤环境贴图，将Cubemap作为渲染目标，`m_IntermediumRT`作为卷积时采样的对象。
``` C#
private Material m_FilterCubemapMaterial;

/// <summary>
/// 获取 FilterCubemap Material，如果不存在则自动创建/更新
/// </summary>
private Material filterCubemapMaterial
{
    get
    {
        // 获取 shader
        Shader filterShader = null;
        var resources = ReflectionProbeResources.instance;
        if (resources != null)
        {
            filterShader = resources.filterCubemapShader;
        }

        if (filterShader == null)
        {
            Debug.LogError("FilterCubemap shader not found! Please ensure ReflectionProbeResources asset exists and has the shader assigned.");
            return null;
        }

        // 检查并创建/更新缓存的 Material
        if (m_FilterCubemapMaterial == null)
        {
            m_FilterCubemapMaterial = new Material(filterShader);
            m_FilterCubemapMaterial.hideFlags = HideFlags.HideAndDontSave;
        }

        return m_FilterCubemapMaterial;
    }
}

// 用于设置 Material 参数的 MaterialPropertyBlock
private MaterialPropertyBlock m_FilterCubemapPropertyBlock = new MaterialPropertyBlock();

public void RenderRealtimeReflectionProbe(ScriptableRenderContext context, ReflectionProbe probe, ref RTHandle cubemapTexture)
{
    //... 最后面
    // 完成 mip0 六个面的渲染后，生成 mipmap
    CommandBuffer cmd = CommandBufferPool.Get();
    cmd.name = "GenerateMipmaps_IntermediumRT";

    // 生成 mipmap（CommandBuffer.GenerateMips 支持 RenderTexture）
    cmd.GenerateMips(m_IntermediumRT.rt);

    // 进行卷积操作
    FilterCubemapGGX(cmd, m_IntermediumRT, cubemapTexture);

    context.ExecuteCommandBuffer(cmd);
    CommandBufferPool.Release(cmd);
    context.Submit();

    probe.realtimeTexture = cubemapTexture;
}

/// <summary>
/// 使用 GGX 过滤生成各个粗糙度的预过滤环境贴图
/// </summary>
/// <param name="cmd">命令缓冲区</param>
/// <param name="sourceCubemap">源 cubemap (m_IntermediumRT)</param>
/// <param name="targetCubemap">目标 cubemap (cubemapTexture)</param>
private void FilterCubemapGGX(CommandBuffer cmd, RTHandle sourceCubemap, RTHandle targetCubemap)
{
    if (sourceCubemap == null || sourceCubemap.rt == null || targetCubemap == null || targetCubemap.rt == null)
        return;
    
    // 首先复制 mip0：将 sourceCubemap 的 mip0 复制到 targetCubemap 的 mip0
    for (int face = 0; face < 6; face++)
    {
        cmd.CopyTexture(
            sourceCubemap.rt, face, 0, // 源：sourceCubemap，面索引，mip0
            targetCubemap.rt, face, 0  // 目标：targetCubemap，面索引，mip0
        );
    }
    
    // 获取 Material（会自动检查并创建/更新）
    Material filterMaterial = filterCubemapMaterial;
    if (filterMaterial == null)
        return;
    
    
    // 使用 MaterialPropertyBlock 设置源 cubemap
    m_FilterCubemapPropertyBlock.SetTexture("_SourceCubemap", sourceCubemap.rt);
    
    // 计算 invOmegaP
    // invOmegaP = 1 / omegaP, where omegaP = FOUR_PI / (6.0 * cubemapWidth * cubemapWidth)
    int cubemapWidth = sourceCubemap.rt.width;
    float omegaP = (4.0f * Mathf.PI) / (6.0f * cubemapWidth * cubemapWidth);
    float invOmegaP = 1.0f / omegaP;
    m_FilterCubemapPropertyBlock.SetFloat("_InvOmegaP", invOmegaP);
    
    // 遍历 mip1 到 mip6，为每个 mip 级别生成预过滤贴图
    for (int mipLevel = 1; mipLevel <= 6; mipLevel++)
    {
        m_FilterCubemapPropertyBlock.SetFloat("_MipLevel", mipLevel);
        
        // 遍历六个面
        for (int face = 0; face < 6; face++)
        {
            m_FilterCubemapPropertyBlock.SetFloat("_FaceIndex", face);
            
            // 设置渲染目标为 cubemap 的特定 mip 级别和面
            CoreUtils.SetRenderTarget(cmd, targetCubemap, ClearFlag.None, mipLevel, (CubemapFace)face);
            
            // 使用 MaterialPropertyBlock 绘制全屏三角形
            CoreUtils.DrawFullScreen(cmd, filterMaterial, m_FilterCubemapPropertyBlock);
        }
    }
}
```
> 这里需要使用MaterialPropertyBlock来传递每次循环中filterMaterial参数，否则后续循环通过Material.SetXXX接口设置的参数可能会污染前面几次CoreUtils.DrawFullScreen中提交的filterMaterial参数。
具体卷积方法参考URP.Core中 [ImageBasedLighting.hlsl](https://github.com/advancedfx/afx-unity-srp/blob/advancedfx/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl)的函数`IntegrateLD`。 
不过这里考虑到每级mip固定只采样34次，一共只存在204个采样点。我这直接将采样点对应的入射方向和立体角定义在hlsl中，而不是类似原方法使用LUT图或实时计算。 
创建ShaderLab文件`FilterCube.Shader`, `FilterCubemap.hlsl`, 以及存储Shader的ScriptObject文件`ReflectionProbeResources`.
``` C#
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// ScriptableObject for storing reflection probe related shader resources
    /// </summary>
    [CreateAssetMenu(fileName = "ReflectionProbeResources", menuName = "Rendering/Universal/Reflection Probe Resources", order = 100)]
    public class ReflectionProbeResources : ScriptableObject
    {
        [SerializeField]
        [ResourcePath("Shaders/Utils/FilterCubemap.shader")]
        private Shader m_FilterCubemapShader;

        /// <summary>
        /// FilterCubemap shader for GGX prefiltering
        /// </summary>
        public Shader filterCubemapShader
        {
            get => m_FilterCubemapShader;
            set => m_FilterCubemapShader = value;
        }

        private static ReflectionProbeResources s_Instance;

        /// <summary>
        /// Get the instance of ReflectionProbeResources
        /// </summary>
        public static ReflectionProbeResources instance
        {
            get
            {
                if (s_Instance == null)
                {
                    // Try to find the asset in the project
                    #if UNITY_EDITOR
                    string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ReflectionProbeResources");
                    if (guids.Length > 0)
                    {
                        string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                        s_Instance = UnityEditor.AssetDatabase.LoadAssetAtPath<ReflectionProbeResources>(path);
                    }
                    #endif

                    // Fallback: try to find in Resources folder
                    if (s_Instance == null)
                    {
                        s_Instance = Resources.Load<ReflectionProbeResources>("ReflectionProbeResources");
                    }
                }
                return s_Instance;
            }
        }
    }
}
```
``` ShaderLab
Shader "Hidden/Universal Render Pipeline/FilterCubemap"
{
    Properties
    {
        _SourceCubemap ("Source Cubemap", Cube) = "" {}
        _InvOmegaP ("Inv Omega P", Float) = 1.0
    }
    
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        
        Pass
        {
            Name "FilterCubemapGGX"
            ZTest Always
            ZWrite Off
            Cull Off
            
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/FilterCubemap.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Sampling/Sampling.hlsl"
            
            TEXTURECUBE(_SourceCubemap);
            SAMPLER(s_trilinear_clamp_sampler);
            
            float _InvOmegaP;
            float _MipLevel; // 当前要生成的 mip 级别 (1-6)
            float _FaceIndex; // 当前渲染的 cubemap 面索引 (0-5)
            
            struct Attributes
            {
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                // 使用全屏三角形
                float4 pos = GetFullScreenTriangleVertexPosition(input.vertexID);
                float2 uv = GetFullScreenTriangleTexCoord(input.vertexID);
                
                output.positionCS = pos;
                output.uv = uv;
                
                return output;
            }
            
            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
                // 从 UV 坐标重建 cubemap 方向
                float2 positionNVC = input.uv * 2.0 - 1.0;
                
                // 使用面索引和 UV 坐标重建方向
                uint faceId = (uint)_FaceIndex;
                float3 N = CubemapTexelToDirection(positionNVC, faceId);
                
                // 计算粗糙度（从 mip 级别映射到粗糙度）
                // mipLevel 1-6 对应不同的粗糙度级别
                float perceptualRoughness = MipmapLevelToPerceptualRoughness(_MipLevel);
                float roughness = PerceptualRoughnessToRoughness(perceptualRoughness);
                
                // 对于 cubemap 过滤，我们假设 V == N（视角方向等于法线方向）
                float3 V = N;
                
                // 使用静态采样版本的 IntegrateLD
                // mipLevelIndex = _MipLevel - 1 (因为 mipLevel 1-6 对应 index 0-5)
                // 固定使用 34 个样本
                uint mipLevelIndex = (uint)_MipLevel - 1;
                float4 result = IntegrateLD_StaticSamples(
                    TEXTURECUBE_ARGS(_SourceCubemap, s_trilinear_clamp_sampler),
                    V,
                    N,
                    roughness,
                    _InvOmegaP,
                    mipLevelIndex
                );

                return result;
            }
            
            ENDHLSL
        }
    }
}
```
``` hlsl
#ifndef UNITY_FILTER_CUBEMAP_HLSL_INCLUDED
#define UNITY_FILTER_CUBEMAP_HLSL_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Sampling/Fibonacci.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/BSDF.hlsl"

// 预计算的 GGX IBL 样本数据（替代 ggxIblSamples 贴图）
// 每个元素是 float4(localL.x, localL.y, localL.z, omegaS)
// 总共 6 个 mip 级别，每个级别 34 个样本
// 使用统一的数组：6 * 34 = 204 个元素

// 统一的静态数组，包含所有 mip 级别的样本数据
// 数组索引计算：arrayIndex = mipLevelIndex * 34 + sampleIndex
static const float4 k_GGXIblSamples[204] = {
    // 6 mip levels * 34 samples = 204 elements
    // 格式：float4(localL.x, localL.y, localL.z, omegaS)
    // Generated by C# Script
    // Format: float4(localL.x, localL.y, localL.z, omegaS)

    // 具体参考源码
}


// 不使用 ggxIblSamples 贴图的 IntegrateLD 实现
// 使用静态数组存储预计算的样本数据，不使用 USE_KARIS_APPROXIMATION
float4 IntegrateLD_StaticSamples(TEXTURECUBE_PARAM(tex, sampl),
                                 real3 V,
                                 real3 N,
                                 real roughness,
                                 real invOmegaP,
                                 uint mipLevelIndex) // mipLevel - 1 (0-5)
{
    real3x3 localToWorld = GetLocalFrame(N);
    
    // 不使用 USE_KARIS_APPROXIMATION，使用精确的 F * G 权重
    real NdotV = 1; // N == V
    real partLambdaV = GetSmithJointGGXPartLambdaV(NdotV, roughness);
    
    float3 lightInt = float3(0.0, 0.0, 0.0);
    float  cbsdfInt = 0.0;
    
    // 固定使用 34 个样本
    [unroll]
    for (uint i = 0; i < 34; ++i)
    {
        real3 L;
        real  NdotL, NdotH, LdotH;
        
        // 从静态数组获取预计算的样本数据（使用采样 UV 计算）
        float4 sampleData = k_GGXIblSamples[mipLevelIndex * 34 + i];
        real3 localL = sampleData.xyz;
        real omegaS = sampleData.w;
        
        // 转换到世界空间
        L = mul(localL, localToWorld);
        NdotL = localL.z;
        LdotH = sqrt(0.5 + 0.5 * NdotL);
        
        if (NdotL <= 0) continue; // 注意：某些样本的贡献为 0
        
        // 预过滤的 BRDF 重要性采样
        // 使用较低的 MIP-map 级别来获取低概率样本，以减少方差
        // Ref: http://http.developer.nvidia.com/GPUGems3/gpugems3_ch20.html
        
        // 'invOmegaP' 在 CPU 上预计算并作为参数提供
        // real omegaP = FOUR_PI / (6.0 * cubemapWidth * cubemapWidth);
        const real mipBias = roughness;
        real mipLevel = 0.5 * log2(omegaS * invOmegaP) + mipBias;
        
        // 从 cubemap 采样
        real3 val = SAMPLE_TEXTURECUBE_LOD(tex, sampl, L, mipLevel).rgb;
        
        // 不使用 USE_KARIS_APPROXIMATION，使用精确的 F * G 权重
        // The choice of the Fresnel factor does not appear to affect the result.
        real F = 1; // F_Schlick(F0, LdotH);
        real G = V_SmithJointGGX(NdotL, NdotV, roughness, partLambdaV) * NdotL * NdotV; // 4 cancels out
        
        lightInt += F * G * val;
        cbsdfInt += F * G;
    }
    
    // 防止 0/0 导致的 NaN
    cbsdfInt = max(cbsdfInt, REAL_EPS);

    return float4(lightInt / cbsdfInt, 1.0);
}

#endif // UNITY_FILTER_CUBEMAP_HLSL_INCLUDED

```
> 不过如果反射贴图的尺寸较大，如512*512。34次采样可能不太够。
静态数组打成LUT，大概是这样。
![20260130184409](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20260130184409.png)
> 生成静态数组和对应的LUT的[脚本]（）
对比Baked和我们自定义的效果
