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

    if (m_CustomRealtimeTexture != null)
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
接下来就是要调整调整相机的VP矩阵，以此渲染六个面进行渲染。
``` C#
public void RenderRealtimeReflectionProbe(ScriptableRenderContext context, ReflectionProbe probe, ref RTHandle cubemapTexture)
{
    // 确保 m_FaceRT 已创建或重新分配（如果需要）
    //...

    for (int i = 0; i < 6; i++)
    {
        CubemapFace face = (CubemapFace)i;
        SetupCameraForCubemapFace(face);

        // 使用成员变量 m_FaceRT 作为渲染目标
    }
}

/// <summary>
/// 设置相机朝向立方体贴图的六个方向之一
/// </summary>
/// <param name="position">相机位置（通常是反射探针的位置）</param>
/// <param name="face">立方体面的索引 (0-5)</param>
void SetupCameraForCubemapFace(CubemapFace face)
{
    // 定义六个方向的前向向量和上向量
    Vector3 forward = Vector3.forward;
    Vector3 up = Vector3.up;

    switch (face)
    {
        case CubemapFace.PositiveX: // Right (+X)
            forward = Vector3.right;
            up = Vector3.up;
            break;
        case CubemapFace.NegativeX: // Left (-X)
            forward = Vector3.left;
            up = Vector3.up;
            break;
        case CubemapFace.PositiveY: // Up (+Y)
            forward = Vector3.up;
            up = Vector3.back;
            break;
        case CubemapFace.NegativeY: // Down (-Y)
            forward = Vector3.down;
            up = Vector3.forward;
            break;
        case CubemapFace.PositiveZ: // Forward (+Z)
            forward = Vector3.forward;
            up = Vector3.up;
            break;
        case CubemapFace.NegativeZ: // Back (-Z)
            forward = Vector3.back;
            up = Vector3.up;
            break;
    }

    // 使用 TransformDirection 计算世界空间的基向量
    // 这些向量定义了相机的局部坐标系在世界空间中的方向
    Vector3 right = m_RenderFaceCamera.transform.TransformDirection(Vector3.right);
    Vector3 cameraUp = m_RenderFaceCamera.transform.TransformDirection(Vector3.up);
    Vector3 cameraForward = m_RenderFaceCamera.transform.TransformDirection(Vector3.forward);

    // 构建 worldToCameraMatrix - 将世界坐标转换到相机坐标
    // 矩阵的行向量是相机坐标系的基向量（以转置形式）
    Matrix4x4 worldToCameraMatrix = new Matrix4x4();
    
    // 设置基向量（行向量）和平移部分
    // 平移部分使用 TransformDirection 计算得到的基向量和 position 的点积
    worldToCameraMatrix.m00 = right.x;
    worldToCameraMatrix.m01 = right.y;
    worldToCameraMatrix.m02 = right.z;
    worldToCameraMatrix.m03 = 0;
    
    worldToCameraMatrix.m10 = cameraUp.x;
    worldToCameraMatrix.m11 = cameraUp.y;
    worldToCameraMatrix.m12 = cameraUp.z;
    worldToCameraMatrix.m13 = 0;
    
    worldToCameraMatrix.m20 = cameraForward.x;
    worldToCameraMatrix.m21 = cameraForward.y;
    worldToCameraMatrix.m22 = cameraForward.z;
    worldToCameraMatrix.m23 = 0;
    
    worldToCameraMatrix.m30 = 0;
    worldToCameraMatrix.m31 = 0;
    worldToCameraMatrix.m32 = 0;
    worldToCameraMatrix.m33 = 1;

    // 应用到相机
    m_RenderFaceCamera.worldToCameraMatrix = worldToCameraMatrix;
}
```
接下来就是要调整调整相机的VP矩阵，以此渲染六个面进行渲染。
