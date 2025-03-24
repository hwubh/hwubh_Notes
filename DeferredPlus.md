- Rendering
  - UniversalRenderPipeline::ProcessRenderRequests -> Render -> RenderCameraStack -> RenderSingleCamera -> RecordAndExecuteRenderGraph -> 
  - Record： 
    - ScriptableRenderer:: RecordRenderGraph 
    - UniversalRenderer::OnRecordRenderGraph -> OnMainRendering
      - ClearTargetsPass -> StencilCrossFadeRenderPass -> customPasses(BeforeRenderingPrePasses) -> Depth(Normal)PrepassRender -> customPasses(AfterRenderingPrePasses) -> if deferred
  - Execute：  
    - RenderGraph::EndRecordingAndExecute -> Execute -> ExecuteRenderGraph -> ExecuteCompiledPass ->
    - RenderGraphPass::Execute

- UniversalRenderer在构建时会确定使用的render path
  - 当render path 是 deferred+ 时
    - GBufferPass + CopyDepthPass + DeferredPass + DrawObjectsPass
  - if deferred -> 
    customPasses(BeforeRenderingGbuffer) -> GBufferPass -> customPasses(BeforeRenderingDeferredLights) -> DeferredPass -> customPasses(BeforeRenderingOpaques) -> DrawObjectsPass(ForwardOnly) -> ...

- 目前非Render Graph时不支持 deferred+ ![20250213170854](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250213170854.png)

- URP17 + DeferredPlus + NORenderGraph : 
  - renderer = cameraData.renderer -> frameData
  - TryGetCullingParameters
  - renderer.OnPreCullRenderPasses : per pass
  - renderer.SetupCullingParameters : per pass
  - SetupPerCameraShaderConstants
  - ProbeReferenceVolume.instance.UpdateCellStreaming + ProbeReferenceVolume.instance.BindAPVRuntimeResources
  - additionalCameraData.motionVectorsPersistentData.Update + UpdateTemporalAATargets
  - context.Cull
  - CreateUniversalResourceData : consisted of all rthandles
  - CreateLightData: reflectionProbeAtlas = settings.reflectionProbeBlending && isDeferredPlus(...)
  - CreateShadowData
  - CreatePostProcessingData
  - CreateRenderingData
  - CreateCullContextData
  - CreateShadowAtlasAndCullShadowCasters
    - InitializeMainLightShadowResolution
    - BuildAdditionalLightsShadowAtlasLayout
    - CullShadowCasters
  - renderer.AddRenderPasses
  - renderer.Setup : UniversalRenderer.Setup
    - IsDepthPrimingEnabled = false
    - if IsOffscreenDepthTexture == true: enqueue ``m_RenderOpaqueForwardPass`` ``m_RenderTransparentForwardPass`` and return 
    - createColorTexture = true : Assign the camera color target early
    - UpdateCameraHistory
    - GetRenderPassInputs
    - RequireRenderingLayers (if opengles, = false, because of MRT )
    - m_DeferredLights.ResolveMixedLightingMode -> CreateGbufferResources: create gbuffer RTHandle
    - if UseFramebufferFetch == true : ??
    - if ssao == true-> depth-normal texture
    - if need depthcopy
    - createColorTexture needed
    - CreateCameraRenderTarget
    - EnqueuePass(m_MainLightShadowCasterPass);
    - EnqueuePass(m_AdditionalLightsShadowCasterPass);
    - RenderingUtils.ReAllocateHandleIfNeeded: Allocate m_DepthTexture if used
    - EnqueuePass(m_DepthNormalPrepass/m_DepthPrepass);
    - if useDepthPriming == true -> EnqueuePass(m_PrimedDepthCopyPass);
    - if generateColorGradingLUT == true -> EnqueuePass(colorGradingLutPass);
    - EnqueueDeferred
      - m_GBufferPass.Configure
      - m_DeferredPass.Configure
      - EnqueuePass(m_GBufferPass);
      - EnqueuePass(m_GBufferCopyDepthPass);
      - EnqueuePass(m_DeferredPass);
      - EnqueuePass(m_RenderOpaqueForwardOnlyPass);
    - EnqueuePass(m_DrawSkyboxPass);
    - if copyColorPass == true -> EnqueuePass(m_CopyColorPass);
    - if requiresMotionVectors == true -> EnqueuePass(m_MotionVectorPass);
    - if needTransparencyPass -> EnqueuePass(m_TransparentSettingsPass);
    - EnqueuePass(m_RenderTransparentForwardPass);
    - EnqueuePass(m_OnRenderObjectCallbackPass);
    - SetupRawColorDepthHistory
      - EnqueuePass(m_HistoryRawDepthCopyPass);
    - EnqueuePass(m_DrawOffscreenUIPass);
    - if last camera in stack: 
      - EnqueuePass(postProcessPass);
      - EnqueuePass(finalPostProcessPass);
      - if cameraData.captureActions -> EnqueuePass(m_CapturePass);
      - EnqueuePass(m_DrawOverlayUIPass);
    - else -> EnqueuePass(postProcessPass)
  - renderer.Execute
    - InternalStartRendering -> OnCameraSetup
    - SortStable: sort passes
    - pass.Configure
    - SetupNativeRenderPassFrameData
      - IF RPEnabled == True -> try to merge passes
    - SetupLights (D+)
    - ExecuteBlock 
LOD_FADE_CROSSFADE

- GBufferPass
  - 输入两张texture？ _CameraNormalsTexture + _CameraRenderingLayersTexture（？）
  - new: 设置shader pass， 设置stencil state
      - StencilState: 记录模板测试中的一系列操作？ 
        - OverwriteStencil： 更改stencilstate， 在GBufferPass 构建中，用于更新stencilReference，取值为StencilUsage.MaterialLit/MaterialSimpleLit/MaterialUnlit
  - Configure：
    - 如果支持framebuffer fetch 和存在 m_DeferredLights.DepthCopyTexture， 将后者作为gbuffer的深度图。
    - gbuffer数量介于4~7之间：分别为
      - 0： Albedo
      - 1： SpecularMetallic
      - 2： NormalSmoothness
      - 3： Lighting -》在 deferredLights.setup中设置，对象为colorattachment
      - 4： Depth
      - 5： RenderingLayers
      - 6： ShadowMask
      - ![20250218172820](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250218172820.png)
    - ConfigureTarget: 设置目标RT为 color： m_DeferredLights.GbufferAttachments； depth： m_DeferredLights.DepthAttachment
    - ConfigureClear： not clear
  - Execute： 
    - InitRendererLists
      - CreateDrawingSettings： 设置渲染相关的配置： perObjectData + mainlightIndex + batching，instancing + lodCrossFadeStencilMask（）
        - lodCrossFadeStencilMask： stencil 判断参与抖动混合的像素的区域。像素通过采样噪音图，结合当前lod的进度（0~1）判断是否舍弃当前层的渲染结果。舍弃发生时，会采样下一层级的lod，从而通过不同像素采样不同层级的lod来实现混合过渡的效果。
      - CreateRendererList ： RendererList： a set of visible GameObjects
      - CreateRendererListObjectsWithError（只有在development）
  - Render：
    - Raster Render pass ？
    - AddRasterRenderPass
      - Initialize: 
    - rendering layers:  lets you configure certain Lights to affect only specific GameObjects
    - 如果采用了 two-pass occlusion culling， 会执行两次 gbufferpass？ 
      - 第一次时： 根据index判断gbuffer slice对应的rt格式，创建相对应的rt（renderinglayers和normalsmoothness如果已经创建的的话则跳过）
    - SetRenderAttachment 设置renderTarget？ 带有index参数，支持MRT？
    - 可以使用dbuffer作为输入的texture
    - 使用 ``cameraDepth`` 作为depth target， 进行写入。
    - InitRendererLists： 
    - UseRendererList
    - SetGlobalTextureAfterPass： 如果有需要``setGlobalTextures``, ``useCameraRenderingLayersTexture`` == true. 在gbufferpass的最后，将cameraNormalsTexture， renderingLayersTexture设置为 global texture.
    - AllowPassCulling(false): 必须执行
    - AllowGlobalStateModification
    - SetRenderFunc： 设置渲染函数 ExecutePass 
  - ExecutePass：
    - 
    - DrawRendererList

- m_RenderOpaqueForwardOnlyPass: 默认有``UniversalForwardOnly``的shader只有complexlit和bakedlit，其中只有complexlit有keyword _CLUSTER_LIGHT_LOOP

- _CLUSTER_LIGHT_LOOP: 
  ``` 
                  #if USE_CLUSTER_LIGHT_LOOP
                  UNITY_LOOP for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                  {
                      Light additionalLight = GetAdditionalLight(lightIndex, inputData.positionWS, half4(1,1,1,1));
                      lighting += MyLightingFunction(inputData.normalWS, additionalLight);
                  }
                  #endif
  ```
  GetAdditionalLight传入lightindex和片元的空间位置查找对应的light。 -》 GetAdditionalPerObjectLight
  float4 lightPositionWS = _AdditionalLightsPosition[perObjectLightIndex];

- ForwardLights： https://zhuanlan.zhihu.com/p/706755603 https://zhuanlan.zhihu.com/p/685781244 
  - constructor
    - 初始化mainlight，additionlight的参数： posiiton，color，occlusionProbesChannel，layerMask，Attenuation，spotDir
    - if (m_UseForwardPlus)： CreateForwardPlusBuffers
      - 申请两个的buffer及其对应的array，
        z方向上**m_ZBins/m_ZBinsBuffer**为4096个 uint/float。 
        xy方向上**m_TileMasks/m_TileMasksBuffer**为4096 或 10384个（additional light数量大于32个时） uint/float
      - 创建 ReflectionProbeManager -> 创建两张 1*1的 RT
  - PreSetup
    - if (m_UseForwardPlus)： ScheduleClusteringJobs![20250324112046](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250324112046.png)
      - ScheduleClusteringJobs 返回的参数
        - lights
        - probes ： 根据Importance从大到小排序的reflection probe 数组
        - zBins
        - tileMasks
        - worldToViews
        - viewToClips
        - m_LightCount ： 非directional 的 additional light （**localLights**）的数量
        - m_DirectionalLightCount： additional light中 directional light的数量
        - out m_BinCount, ： 沿Z方向划分的区块(Zbin)的数量
        - out m_ZBinScale, : ZBinScale, 单位距离上Zbin的数量，用于当前Zbin序号的计算。
        - out m_ZBinOffset, ： ZBinOffset， 0~近平面距离上Zbin数量offset，用于当前Zbin序号的计算。
        - out m_TileResolution, : XY划分的区块(Tile)的数量
        - out m_ActualTileWidth, ： 每个Tile的尺寸（像素单位）
        - out m_WordsPerTile ： 每个tile占据的Uint 数量。
      - 跳过 directional light -》影响全局
      - itemsPerTile: localLights + reflection probes
      - m_WordsPerTile： 计算每个tile上需要多少words， items的数量为 可见光源的数量 + 反射探针的数量 =》 当存在少于31个additional光源时，只需要存1份。 => 每个word(uint)有32位，最多可以表示32个光源/反射探针![20250324115636](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250324115636.png)
      - m_TileResolution： 场景中tile的数量， m_TileResolution.x * m_TileResolution.y * m_WordsPerTile * viewCount > UniversalRenderPipeline.maxTileWords -> 从8个像素的Tile开始划分。如果tile数量超出了m_TileMasksBuffer的尺寸，则扩大一倍，使用16个像素的Tile。 以此类推，tile的数量不大于m_TileMasksBuffer的尺寸。
        - m_TileResolution.x * m_TileResolution.y: tile的数量
        - m_WordsPerTile： 每个tile占据的大小
        - viewCount： 场景中渲染的次数？比如vr的话，需要分别渲染左右眼，共两次
        - ![20250324115636](https://raw.githubusercontent.com/hwubh/Temp-Pics/d563be2c9a2dceceb2f1fb6600e38fd4d8c861f3/20250324115923.png)
      - m_BinCount： m_BinCount = (int)(camera.farClipPlane * m_ZBinScale + m_ZBinOffset); binIndex = z * zBinScale + zBinOffset ： 每个zbin至少占据3个uint ： header：两uint + data： 一uint
        - m_ZBinScale = 1 / scale; z * m_ZBinScale -> 在第几份，相当于 z / scale
        - m_ZBinOffset = -camera.nearClipPlane * m_ZBinScale; -> offset
        - m_BinCount = (int)(camera.farClipPlane * m_ZBinScale + m_ZBinOffset) = (camera.farClipPlane - camera.nearClipPlane) * m_ZBinScale
        - ![20250324144021](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250324144021.png)
          > 透视时使用对数的原因： 沿Z划分时，我们希望做到划分的各个每一块在屏幕占据的像素尽可能接近。 因此需要划分时，越靠近远平面时，每一块占据的深度越多。（因为越远的三角形投影到屏幕上时越小）。
          （图a）如果NDC空间中沿Z方向划分，虽然符合我们的希望，但会使得精度分布过于偏向于近平面。
          （图b）如果View空间中沿Z方向划分，会使每块分配的深度相同，不符合我们的希望。近远平面的块过多。
          （图c）因此URP中选择在View空间中使用对数进行划分，在图二的基础上，给靠近近平面的分配更多的快。
          > [参考链接](https://www.aortiz.me/2018/12/21/CG.html#tiled-shading--forward); [其他延申内容1](https://developer.nvidia.com/content/depth-precision-visualized)；[原论文](https://www.cse.chalmers.se/~uffe/clustered_shading_preprint.pdf)
          > ![20250324154311](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250324154311.png)
          > 公式，图示：![20250324154543](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250324154543.png) 
      - probes： 将probe根据importance 从大到小进行排序。
        > Unity 文档说着色计算是最多只用两个reflection probe，但 cluster时好像不是？？？ 
      - LightMinMaxZJob
        - minMaxZs （local）: 计算各个local光源（Point 和 Spot）影响的深度范围
          - Point： 计算该光源中心点在View空间下的深度值，加上/减去光的范围（range）
          - Spot： 计算圆锥包围盒的范围，再得到其深度值![20250324170557](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250324170557.png) ![20250324171628](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250324171628.png)
      - reflectionProbeMinMaxZJob：
        - minMaxZs（local）： 记录各个反射探针影响的深度范围
          - ![20250324172953](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250324172953.png)
          - 计算包围盒各个顶点的Z值并比较
          - 一种遍历各个序号的方式； i = 0时，为 x=-1, y=-1, z=-1；i = 1时，x=1, y=-1, z=-1 ... i = 7， x=1, y=1, z=1
          ``` c#
            var x = ((i << 1) & 2) - 1; // i 的二进制位操作生成 [-1, 1]
            var y = (i & 2) - 1;
            var z = ((i >> 1) & 2) - 1;
          ```
      - minMaxZs （global）： 记录各个光源+反射探针影响的深度范围 
      - ZBinningJob （一共有*zBinningBatchCount*个job）： 计算各个光源+反射探针对于zbin的影响（是否在范围内）
        - zBinningBatchCount： batch的数量。每个batch包含128个zbin
        - zbin: 每个zbin占据 （2 + m_WordsPerTile）个uint； 2 代表光源和反射探针各自占据的一个uint，每个uint的高16位代表当前 ZBin 所占有的光源的 maxIndex， 低16位为minIndex。 第三个uint则记录具体有哪些光源/反射探针影响该zbin
          - 这里以一个占据了3uint的zbin为例，三个uint分别为
             458752： 0111 0000000000000000 ： 光源的最大序号为7， 最小为0
             589832： 1001 0000000000001000 ： 反射探针的最大序号为9， 最小为8
             899：               1110000011 ： 涉及的光源序号为0， 1， 7， 8， 9
             记录最大，最小序号的意义是？？
      - GetViewParams： 传入投影矩阵 -》 主要用于VR的斜视投影
        - 正交时记录非对称正交投影的偏移； 投影时记录投影屏幕的偏移参数
        - viewPlaneBottom0 ： 视口偏移的下界
        - viewPlaneTop0 ： 视口偏移的上界
        - viewToViewportScaleBias0 ： 视口的缩放偏置参数；前两项记录scale，后两项记录offset
      - TilingJob：
      - TileRangeExpansionJob 
        - m_TileMasks: 包含一个uint，每个位上代表收影响的光源/反射探针的序号
  - SetupLights
    - if (m_UseForwardPlus)
      - m_ReflectionProbeManager.UpdateGpuData（传入 cullResults）： visibleReflectionProbes?
      - 设置对应参数在shader侧的命名： urp_ZBinBuffer  urp_TileBuffer _FPParams0 _FPParams1 _FPParams2![20250219174052](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250219174052.png)


- framebuffer fetch: use the ``SetInputAttachment`` API to set the output of a render pass as the input of the next render pass. keep the framebuffer stays in the on-chip memory, avoid the cost of the bandwidth caused by the acessing it from the video memory.
  - requirements: only works in Vulkan and Mental
  - 
  - https://docs.unity3d.com/6000.0/Documentation/Manual/urp/render-graph-framebuffer-fetch.html

- DeferredLights:
  - new: 
    - 如果是deferredplus： 记录 Shaders/Utils/ClusterDeferred.shader 中 ``Deferred Clustered Lights (Lit)``, ``Deferred Clustered Lights (SimpleLit)``, ``Fog`` 这三个pass， 传入六个参数 _LitStencilRef，_LitStencilReadMask，_LitStencilWriteMask，_SimpleLitStencilRef，_SimpleLitStencilReadMask，_SimpleLitStencilWriteMask （find pass by string to avoid errors caused by stripping happened when find passes by hardcore index）

- SetupLights: 

- RenderClusterLights: -> 渲染了两次？ Deferred Clustered Lights (Lit)、 + Deferred Clustered Lights （SimpleLit）

- DeferredPlus in URP17: 
  - 资源设置：
    - TryGetCullingParameters(cameraData, out var cullingParameters)： 设置视锥剔除的参数
    - 
  - 具体渲染部分：
    - 

- commit: ![20250217102434](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250217102434.png)

- work
  - DBufferCopyDepthPass 是 workaround的，之后需检查，修改.


~~ Test000_ForwardPlus:只能有32个光？ maximumVisibleLights = 32? ~~

- deferred -> unlimited additional lights
  deferred+ -> max 256 lights
  Forwardonlypass in deferred -> ?

- 000 ： tuanjie上颜色不太一样？
- 050： 哪些是deferred，哪些是forwardonly？

-------------
- universalRenderer.useRenderPassEnabled: 作用？ -》 deferred时默认开启native render pass？ -》 减少对Gbuffer的访问？ -> 保留
- RenderingLayerUtils.RequireRenderingLayers： 判断lightmode用？ -》保留
- GBufferFragOutput frag(PackedVaryings packedInput)： 改了个名字？ -》 保留
- PopulateCompatibleDepthFormats： 判断 depth 格式的兼容性， 并显示gui上？ -》 没有相关内容， 不保留
- private static bool HasCorrectLightingModes(UniversalRenderPipelineAsset asset): used for gpu driven drawer, 不保留
- public bool IsGPUResidentDrawerSupportedBySRP(out string message, out LogType severty): 同上
- internal bool IsGBufferValid { get; set; }: 用于Render Graph， 不保留
- if (!UseFramebufferFetch): https://github.com/Unity-Technologies/Graphics/commit/9fe6f5fd1b81800715d60b6b4ba7dcbbdcbfeba9#diff-546c949fd823f6a0267b05267bdedb26259c9c63ae52fce0b60f797b8d9d1a6b ; m_DeferredLights.GbufferAttachments 已经在Gbufferpass中设置成了全局变量，这里不在deferredLights（即deferredpass）的材质上设置应该也是可以的？ -》 不用glabol texture 会省带宽消耗吗？？ -》 先不保留
- private void InitRendererLists(ref PassData passData, ScriptableRenderContext context, RenderGraph renderGraph, UniversalRenderingData renderingData, UniversalCameraData cameraData, UniversalLightData lightData, bool useRenderGraph, uint batchLayerMask = uint.MaxValue): render graph 相关，不保留
- half4 SampleAdditionalLightCookieDeferred(int perObjectLightIndex, float3 samplePositionWS): 采样additional light cookie的整理，之前只支持point 和 spot，这里添加了对additional directional light cookie的支持。： 非deferred的内容，不保留


----------------

Deferred+:
- UniversalRenderer: 
  - UniversalRenderer 初始化时会初始ForwardLight
  - ForwardLight 初始化时会全局设置keyword **_CLUSTER_LIGHT_LOOP**
- Render GBuffer: 
  - Lit.Shader: 的 *GBuffer* pass有 keyword **_CLUSTER_LIGHT_LOOP**。
    - 在计算GetMainLight时，有以下代码
    ``` c#
    #if USE_CLUSTER_LIGHT_LOOP
      #if defined(LIGHTMAP_ON) && defined(LIGHTMAP_SHADOW_MIXING)
          light.distanceAttenuation = _MainLightColor.a;
      #else
          light.distanceAttenuation = 1.0;
      #endif
    #else
        light.distanceAttenuation = unity_LightData.z; // unity_LightData.z is 1 when not culled by the culling mask, otherwise 0.
    #endif
    ``` 
  -> 默认主光源的距离衰减为1（不衰减？？），因为在计算cluster时已经排除了不可见的光源。 -》 计算cluster时如果mianlight不可见是如何配置的？？？
  -> 通过权重值_MainLightColor.a， 混合烘焙与实时阴影； 非cluster的需要后续手动进行混合？？
  - 在计算 GlobalIllumination -> GlossyEnvironmentReflection 时， 如果开启了 _REFLECTION_PROBE_BLENDING 或 cluster lights 会通过反射探针来计算 间接镜面反射IBL. 
    >关于 Reflection probe blending: 只有像素存在于Reflection probe volume中时才计算该probe。
    >通过 Blend Distance 与 像素到probe volume表面的距离来计算贡献度。 当像素到面距离从0~Blend Distance时， 贡献度从 0~100%之间变化。
    >反射探针的贡献度由像素到包围盒 所有面的距离共同决定： 贡献度 = min( 像素到各面的距离 / Blend Distance, 1.0 )。当存在Blend Distance 超过面间距一半时，该probe不存在一个位置可以使贡献度到达100%。
  开启cluster时会遍历cluster中记录的各个reflection probe，直到权重已满0.99
  否则，只采样固定的两个探针unity_SpecCube0，unity_SpecCube1 ？？ 

    ![20250320144253](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250320144253.png)
- Render Deferred Lighting: 使用不同的shader
  - PrecomputeLights
  - ClusterDeferred: ~~没有开启keyword**_CLUSTER_LIGHT_LOOP**？？ 没有做什么特殊处理？先计算mainlight，再算additional的非directional light， 最后算additional directional light。 （没开cluster light，哪里来的URP_FP_DIRECTIONAL_LIGHTS_COUNT？？？）~~ 直接在ClusterDeferred中定义了_CLUSTER_LIGHT_LOOP： ![20250321160707](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250321160707.png)
    - 顶点着色阶段：使用全屏三角形处理。 因为后续可以直接使用光簇中记录的受影响的光源进行着色，不需要担心该像素是否做了多余的着色。
      > 对比StencilDeferred： pass0:Stencil Volume？？
      > pass1: Punctual Light??
      > StencilDeferred的顶点着色阶段会根据光源类型调整几何体的形状，保证使其仅覆盖光源实际影响的地方。（如 directional light影响全部像素的话，则使用全屏三角形。点光源则为球体的投影，spot则为锥体的投影。）
      不同的光源在不同的渲染队列上
      ![20250320163954](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250320163954.png)
    - 片元着色阶段： _SIMPLELIT -> Blinn-phong; _LIT -> PBR
      - 先计算MAIN LIGHT： 
      ``` 
      color += DeferredLightContribution(mainLight, inputData, gBufferData);
      ```

      - 再计算非Directional的 additional light： 不过 GetAdditionalLightsCount() = 0 -> 无所谓 -> 开启 USE_CLUSTER_LIGHT_LOOP 时，下列代码 
      ```
      uint pixelLightCount = GetAdditionalLightsCount();
      LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData, gBufferData.shadowMask, aoFactor);

        UNITY_BRANCH if (materialReceiveShadowsOff)
        {
            light.shadowAttenuation = 1.0;
        }

        color += DeferredLightContribution(light, inputData, gBufferData);
      LIGHT_LOOP_END
      ``` 
      等价于
      ```
      {
        uint lightIndex;
        ClusterIterator _urp_internal_clusterIterator = ClusterInit(inputData.normalizedScreenSpaceUV, inputData.positionWS, 0);
        [loop] while (ClusterNext(_urp_internal_clusterIterator, lightIndex)) 
        {
          lightIndex += URP_FP_DIRECTIONAL_LIGHTS_COUNT; //_AdditionalLightsXXXX数组先放的Directional Light的数据 -》 应该是lightData.visibleLights 里就是这么排的
          if (_AdditionalLightsColor[lightIndex].a > 0.0h) continue;
          Light light = GetAdditionalLight(lightIndex, inputData, gBufferData.shadowMask, aoFactor);

          UNITY_BRANCH if (materialReceiveShadowsOff)
          {
              light.shadowAttenuation = 1.0;
          }

          color += DeferredLightContribution(light, inputData, gBufferData);
        } 
      }
      ```
  - 
- Render Opaques Forward Only: 目前Target为ScalableLit 和 Fabric 的shadergraph 不支持Gbuffer的结构，会使用ForwardOnly. 此外Unlit的shader会走Gbuffer的渲染，但不会参与deferredLighting。其也在ForwardOnly阶段渲染。![20250321175443](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250321175443.png) -》 在延迟渲染中，GBuffer 存储了场景的几何信息（如法线、深度、材质属性等）。如果某些物体（如 Unlit 物体）不写入 GBuffer，会导致 GBuffer 中出现“空洞”（即缺失数据区域）。？？
  这里以ComplexLit为例： 走 half4 UniversalFragmentPBR(InputData inputData, SurfaceData surfaceData)： 开启 USE_CLUSTER_LIGHT_LOOP， 先计算mainLight的LightingPhysicallyBased，再算 additional directional light， 最后算其他的additional light
- 