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
      - 申请两个的buffer及其对应的array，z方向上分为4096个？ xy方向上为4096 或 10384
      - 创建 ReflectionProbeManager？
  - SetupLights
    - if (m_UseForwardPlus)
      - m_ReflectionProbeManager.UpdateGpuData（传入 cullResults）： visibleReflectionProbes?
      - 设置对应参数在shader侧的命名： urp_ZBinBuffer  urp_TileBuffer _FPParams0 _FPParams1 _FPParams2![20250219174052](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250219174052.png)
  - PreSetup
    - if (m_UseForwardPlus)：
      - 跳过 directional light
      - m_WordsPerTile： 计算每个tile上需要多少words， items的数量为可见光源的数量 + 反射探针的数量 =》 当存在少于31个additional光源时，只需要存1份。



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
