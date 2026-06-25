问题:
- 未开启ImageVRS时，设置SRI图会导致Editor Crash
- colorFlags |= kSurfaceCreateShadingRate; 多写了一个
- Planar 用 Pipeline shading rate
- RRM 使用Inverse Scale造成的 Encode（A） --> Encode(C) 之间，origin Texture存在的add synchronization 问题。
- RRM resolve 默认使用 store action
- RRM 中间只要有setRenderTarget前没有设置RRM，就会导致flush
- Vulkan demo 中给出的公式好像有问题，关于1*2的UINT值，经测试，应该是1，但是demo中给出的公式(rate_x>>1)|(rate_y<<1)，得到的结果是3. 正确的公式应该是((rate_x >> 1) << 2) | (rate_y >> 1)？ （Foveated Rendering还用的错误的公式）
- Renderdoc 加载如editor时， DX11 不支持vrs。可能需要改Renderdoc。
- Render Scale 不等于1 时crash。
- 申请过大的 R8_UINT 图作为SRI图在DX11上会crash
- Vulkan 上Foveated Rendering 会覆盖 ImageBasedVRS的设置
- rasterizationRateMapCaps 可能有重复
- Editor/Mono/EditorGUI.cs: 支持VT？
- GFX_SUPPORTS_SHADINGRATE_PERDRAW
- GL 的跨帧行为正确吗？
- GL SetCombinerOps/setPipeShadingRate 与 per draw VRS的冲突问题
- HLSL 中 RW R8_UINT的问题
- rrm independent stencil texture 
- VRS in XBOX
- Rendering Feature Index
- rasterization rate map caps 可能不准/
- FrameDebugger tileSize没显示
- ShadingRateTileSize 改为写明 = 8， = 116， = 32；
- VK_DYNAMIC_STATE_FRAGMENT_SHADING_RATE_KHR 没开启
------------------
- 为什么shading rate 和 combinerops的设置分开写？ ： OpenGLES中是分开的。
------------------
移动端VRS支持：
Adreno GPU

    A range of Adreno GPUs — 642L, 660+, 650+, 7xx GPUs 支持 VRS
        使用GL_QCOM_shading_rate 扩展
    骁龙888（Adreno 660）往后的GPU 是支持VRS的
    有关机型：
        OnePlus 9 and 9 Pro 之后或者更高配的机型
        Mi 11 有关之后或者更高配的机型
        Vivo X60 Pro+ 和 iQOO 7 的有关机型之后或者更高配的机型
> 8155P -> Adreno 640 GPU 不支持VRS
> 8255P -> Adreno 663， 基于660 应该支持？
> 8195P -> Adreno 680 不支持VRS
> 8295P -> Adreno 695 支持VRS？
> 8775P ->  Adreno 663， 基于660 应该支持？
> 8650 ->  Adreno 663， 基于660 应该支持？
> 8397 ->  Elite 架构，应该支持VRS?
> 8387P ->  Adreno 700+, 应该支持VRS?
> 8797P ->  Elite 架构，应该支持VRS?
> 8787P？
OpenGLES上不支持Tier 2 VRS


Xclipse GPU

    以AMD RDNA 2架构为主体，支持光线追踪和可变速率着色（VRS）
        使用GL_QCOM_shading_rate 扩展
    Exynos 2200 SoCs 支持VRS（理论上支持，实际上应该是有问题）
    有关机型：Galaxy S22、Galaxy S22 Plus和Galaxy S22 Ultra中的部分

MaliGPU

    Mali-G715/G615 GPUs 往后的支持 VRS （2022Q2）
    有关SoCs: 暂无， 预计在23年早期
> mt8678 -> ARM Immortalis-G925 MC12 GPU
> mt8673 -> Mali-G78?
> mt8626 -> Mali-G615 MC6
OpenGLES 上不支持。


- Vulkan: 需要扩展 "VK_KHR_fragment_shading_rate" -> 需要调用vkCreateRenderPass2 才能在pNext 中挂载VkFragmentShadingRateAttachmentInfoKHR结构体。
- OpenGLES: 需要扩展“GL_EXT_fragment_shading_rate_attachment”. 在云真机平台上测试得，该扩展从Adreno 740，即骁龙8Gen2上开始出现？

PowerVR GPUs 不支持

    jumping the VRS bandw

--------------------------
Keyword:
- AMD FidelityFX
----------------
实现VRS的方法: Drawcall (一级)，逐模型 -> Mask / Image (二级)，逐tile -> Primitive (三级)，逐三角形 ： 颗粒度从粗到细。 都需要硬件支持。 
> 软件模拟（Variable Resolution Rendering (可变分辨率渲染)， Checkerboard Rendering (棋盘格渲染)）会导致改变Zbuffer的尺寸，导致渲染精度问题？

Per Draw Call

FullScreen Mask

Primitive attribute

----------------------

在FS中使用R8_UNIT 作为RWTexture的问题：没法8位8位的写入。 使用Uint读取时，会读取00~30四个像素。而Texture[0,0]其实代表了[00]到[30]，Texture[1，0]则是从[4,0]开始的。

--------------------------

vulkan support vrs attachment:
- create Pipeline 要声明VK_DYNAMIC_STATE_FRAGMENT_SHADING_RATE_KHR，不然改不了shadingrate 和 combinerops

- create VKImage with flag VK_IMAGE_USAGE_FRAGMENT_SHADING_RATE_ATTACHMENT_BIT_KHR：RenderTexture：：Create（） -> device.CreateRenderColorSurface -> VK CreateColorRenderSurfacePlatform -> VK CreateColorSurfaceImpl, CHECK FLAG(RenderTexture的serialized 属性，要修改transfer版本) VK_IMAGE_USAGE_FRAGMENT_SHADING_RATE_ATTACHMENT_BIT_KHR

UpdateShadingRate -》 CreateRenderPass2 -》 CreateRenderPass -》 GetPackedRenderPass -》SetRenderPassSetup（ GetRenderPass？）， 根据 MakeCompatibilityRenderPassDescription进行判断 -》 SetRenderTargetsImpl？ 

MakeCompatibilityRenderPassDescription -》 

m_ShadingRateAttachment -》 SetShadingRateAttachment -》 ConfigureFoveatedRendering -》 

- set attachment:  Framebuffers::BeginUseFramebuffer ->MakeFramebufferSetup -> RenderSurface::UseAttachment -> Texture::UseAttachment -> Attachment::Attachment -> VkFramebuffer Framebuffers::CreateFramebuffer
RenderSurface (Unity层)
       ↓
Texture / Renderbuffer
       ↓
Image (VkImage + VkImageView)
       ↓
Attachment
       ↓
FramebufferSetup
       ↓
VkFramebuffer

m_IsConfigureFragmentShadingRateImageEvent 的作用？

Vk中设置VRS SRI 图要注意两点，一个是要确保SRI图被附加到了**Fragment Shading Rate Map**（RenderDoc的FB阶段可见）上，一个是确保**Shading Rate Combiners**（Renderdoc的Rasterizer阶段可见）上不是KEEP（会导致被忽略）
![20260403153550](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20260403153550.png)

VulkanCapsViewer ： 可用于查看Vulkan扩展

--------------------------
DX12 上可以使用PIX来查看 Shading Rate情况，不过需要使用DXIL(HLSLPROGRAM 中添加 *#pragma use_dxc* 来强制使用)

DX12: 打包后kRenderCommand_DrawRendererList 指令在 kRenderCommand_SetShadingRateImageEnable 之后 -》 怀疑是context.DrawRenderers 不遵从commandbuffer的队列。 -》 实际上DrawRenderers有自己的一个队列，与context.ExecuteCommandBuffer的队列进行区分。 Unity在上层有抽象了一个m_Commands来进行管理。（比如每次context.ExecuteCommandBuffer或 context.DrawRenderers都会往m_Commands队列中添加个对象。）

放在Before里设置Image 好吗？
--------------------------
OpenGLES上： 
GfxDeviceGLES::SetRenderTargetsImpl()
    └── GfxFramebufferGLES::Activate()          // 设置 pending framebuffer -> CreateFramebuffer 绑定 Depth/Stencil Attachments
        └── GfxFramebufferGLES::Prepare()       // 准备 framebuffer
            └── GfxFramebufferGLES::MakeCurrentFramebuffer()  // 真正绑定 FBO
                └── m_Api.BindFramebuffer()     // GL 调用


--------------------------
FrameDebugger： 加一个CombinerOp显示？

--------------------------
VRS 的设置： FinalShadingRate在图元装配后，光栅化之前/开始时根据Combiner进行组合得到。
- DX11:  依赖NVAPI
  - 使用CreateShadingRateResourceView将传入的SRI图包装为 ID3D11NvShadingRateResourceView（向GPU声明该贴图用于决定Shading Rate）
  > Resource View： Resource View（资源视图） 是一种轻量的包装对象，它的作用是告诉 GPU 这块内存数据应该被当成什么、怎么去访问。
  - 创建 _NV_D3D11_VIEWPORT_SHADING_RATE_DESC_V1 实例，其包含一个bool成员enableVariablePixelShadingRate 表示是否开启VRS。 一个数组成员 shadingRateTable 作为GPU读取SRI取值时映射表（不同于其他API，NVAPI的映射表是由用户决定的，而且NVAPI提供的选项有很多，甚至可以Discard，超采样 tile）
  - 创建 NV_D3D11_VIEWPORTS_SHADING_RATE_DESC 实例，其包含一个_NV_D3D11_VIEWPORT_SHADING_RATE_DESC_V1实例，当前的版本version，和绑定的视口，裁剪矩阵数量（DX11理论最多16个？）
  - 使用NvAPI_D3D11_RSSetViewportsPixelShadingRates将 NV_D3D11_VIEWPORTS_SHADING_RATE_DESC （主要时映射表） 绑定在设备上下文上。
  - 使用RSSetShadingRateResourceView 将 ID3D11NvShadingRateResourceView （SRI图） 绑定在设备上下文。
  > TileSize 是硬件/驱动写死的，从 NV_VARIABLE_PIXEL_SHADING_TILE_WIDTH  NV_VARIABLE_PIXEL_SHADING_TILE_HEIGHT 获取（写死了16）

- DX12: 
  - RSSetShadingRate: 设置Pipeline shading rate 和 combinerOps
  - 使用D3D12_RESOURCE_BARRIER將SRI的资源状态设置为D3D12_RESOURCE_STATE_SHADING_RATE_SOURCE
  > TileSize 是硬件/驱动写死的，从 D3D12_FEATURE_DATA_D3D12_OPTIONS6 获取
  - 使用RSSetShadingRateImage将SRI绑定到图形命令列表的特定槽位。

OpenGLES: 要求扩展 
  - 通过glFramebufferShadingRateEXT (GLenum target, GLenum attachment, GLuint texture, GLint baseLayer, GLsizei numLayers, GLsizei texelWidth, GLsizei texelHeight) 将 SRI图，TileSize绑定到具体的FBO上。 baseLayer：使用“texture” （SRI）的第几层开始；numLayers：使用“texture” 几层作为SRI进行绑定。 比如如果时VR的话，因为存在两个视图，就可以绑定两层（数量受GL_MAX_FRAGMENT_SHADING_RATE_ATTACHMENT_LAYERS_EXT控制）。 target为绑定目标”（Binding Target）枚举： 多为 GL_FRAMEBUFFER 或 GL_DRAW_FRAMEBUFFER。
  >  glBindFramebuffer 可以把 FBO对象绑定到绑定目标”（Binding Target）枚举上。
  - 通过glShadingRateCombinerOpsEXT设置当前CombinerOps

Vulkan: Vulkan 1.2以上
  - 通过vkCmdSetFragmentShadingRateKHR 设置Pipeline shading rate 和 combinerOps
  - 创建VkFragmentShadingRateAttachmentInfoKHR(sType 为VK_STRUCTURE_TYPE_FRAGMENT_SHADING_RATE_ATTACHMENT_INFO_KHR)，其成员包含 SRI图， TileSize和一个用于未来扩展的pNext指针。
  - 在vkCreateRenderPass2 renderpass的subpass 的pNext 上挂载VkFragmentShadingRateAttachmentInfoKHR结构体。
  > Vulkan 1.4后不需要绑定在RenderPass：：subpass，直接挂载在 VkRenderingInfo::pNext 上？

--------------------------

- 上一帧得到的亮度图在传入前要做什么处理？： 目前上一帧传入的亮度图会通过Motion Vector进行重投影。但这依赖于绘制一个Depth-prepass。这其实消耗是比较大的。
  - Gears Tactics的优化方式：异步计算-> 同时进行上一帧的SRI图生成和当前帧的Depth-prepass
  - AMD FidelityFX： 会生成Motion Vector，但是同时会将Motion Vector上移动快的像素组降低shading rate。
  ![20260403164213](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20260403164213.png) 

- 如何集合在Tonemapping中（FS）中： 目前的问题的在于，RWTexture只能以UInt/Int的形式，32位的进行读写。放在以R8_UInt的SRI图上时，相当于每次都会读写四个像素。而且RWTexture[0，0]相当于[0，0]~[3，0]. 目前想到的方法是每个像素读写时都会后后续四个Tile上相同位置的像素同时进行计算，结果分别写入32位的4个8位上。但这样相当于计算量乘以4了。
  - 替代方案： 使用compute shader 计算。
  - Unity6： 单独有个pass来画。
-----------------------------
Windows 编Renderdoc 的坑：
- 编译android： https://qiankanglai.me/2020/12/26/renderdoc-compile-android-on-windows/index.html 
  - include-bin缺失： 要先用g++ -O2 -o renderdoc/3rdparty/include-bin/include-bin.exe renderdoc/3rdparty/include-bin/main.cpp 编译出include-bin.exe， 然后将include-bin.exe复制到 build-android/bin/的Bin目录下, 命名为include-bin
  - git version不匹配 --> 过不了版本检测。
  - 64位，32位都得编译，不然过不了版本检测。 -》 32位的放在: build-android-arm32; 64位的放在： build-android

--------------------------

Metal rasterization rate map (RRM): 更类似density map的思路。 目前metal RRM是从X/Y两个方向分别记录Tile的缩放比例，每个取值代表一整行/列的Tile在X/Y方向的目标缩放值。 因此这里需要将SRI图做个保守转换？
------------------------------------

关于Rendering Debugger中的显示。
-----------------

Image-based VRS (Tier 2 VRS):
> Tier 2 VRS 还包含 Per Primitive （attribute）的。
- Shading Rate Image (**SRI**) 图的生成: 
  - Gears Tactics: Hardware VRS
    - 使用computer shader 在 final color buffer上跑 sobel 算子进行边缘检测，计算相邻像素的亮度差（感知亮度）。 通过人为设定的阈值来判断高频（边缘），低频区域，对于低频区域使用较大的Quad。 此外如果画面正在运动的话，还需要通过Motion Vectors进行重投影，得到上一帧对应的SRI UV。
    - Aggressive + Conservative: 生成SRI 图时，根据不同的阈值生成两种不同的Mask。 对于低频信息较多的Pass可以使用Aggressive的那张。反之，则用Conservative的那张。
      > 因为VRS值是在处理Tile前通过硬件读入的，无法再Shader运行时进行判断并选择。因此阈值的判断需要再生成SRI图时就完成。
    - Skip edge detection on the borders of a VRS tile： 因为每个VRS tile只能统一使用一种Shading Rate。 在使用Sobel 算子时，可以直接忽略Tile最外面两层像素，只计算Tile中间区域内的像素来决定Shading Rate。
    - Merge the VRS texture generation to be part of tonemapping： 将SRI 图的生成放在Tonemapping的最后一步中。 避免作为独立的pass，还需要另外读入framebuffer，造成带宽消耗。（不过就没法用compute shader了？）
    - Running the VRS texture generation on the Async Compute Queue: 异步生成SRI 图，因为在下一帧的开始阶段，最先执行的时Z-Prepass，不需要进行着色计算，可以在这个阶段，计算执行SRI图的生成。
        > Mobile 上支持这种异步吗？
    - 对于使用Compute Shader进行计算的pass，因为无法依赖硬件进行Shading Rate的读取。需要将整张SRI图读取进来，根据读取到的Shading Rate来决定哪些Thread直接return。用剩下的Thread扩大采样范围，填补被杀掉的线程留下的空袭。
    - 对于Denoiser pass： VRS会导致噪点增多，Denoiser需要读取SRI图, 根据当前区域的Shading Rate来调整模糊的权重。Shading Rate越大，权重值越大，用更强的平滑度来弥补欠采样的问题。
    - 如果开启了DRS导致SRI图与color attachment的tile数量不符，需要通过Down/up scale SRI图进行匹配。
    ![20260330114858](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20260330114858.png) 
  > 对于高分辨的画面，Tier 2 VRS的性能改善会比较明显。 
  - Call of Duty: Software VRS:![20260326102337](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20260326102337.png)
    - 申请一张4x MSAA图: 通过坐标系的重映射来将4x采样点对应到四个像素上。 
    - 允许QUAD的颗粒度为2\*2， 而不是硬件VRS QUAD的8\*8或16\*16.
    - 会根据时间，动态选择像素使用的采样点。 着色计算时要保证采样点在所对应的像素的中心。
    - 创建一张Stencil mask来决定 shading Rate。 使用一个2bit标识符来决定四种shading Rate 1\*1, 2\*2, 2\*1, 1\*2.
    - 创建VRS Image Mask：根据Weber-Fechner JND k calculation计算背景亮度，作为阈值。如果XY方向上的梯度没有超过该阈值，则认为该方向上的像素可以合并。 将结果记录到VRS Image Mask。其会记录过去四帧的VRS判断情况。
    - 生成VRS RENDER MASK：
      - 将VRS Image Mask根据Motion Vector 重投影到当前帧的位置。如果发现新出现的像素，则回退为全分辨率渲染。
      - 读取VRS Image Mask上过去四帧的VRS 判断情况，只有过去四帧存在未通过VRS判断的情况（画面的梯度变化保持稳定时（角色/物体没在快速运动）），则使用全分辨率。
      - FMask显示要合并的QUAD上的采样点属于不同的三角形，会使用全分辨率。
      - 如果FMask和VRS Image Mask的结果发生冲突，优先采用FMask的判断。（比如VRS Image Mask认为应该全分辨率，但FMask认为可以开启2\*2时，主要针对植被等细碎的，经常发生运动的物件）
      - 最后结果写入Stencil Mask中。并创建一张全分辨率的Wavefront Packing Buffer来快速查找采样点？
    - 需要在Pre-pass中使用4xMSAA，但只着色计算一次采样点。得到全分辨率的深度和透明掩码（可见性）以及Fmask。 如果Fmask == 1， 则后续resolve时只需要计算一次？
    - 正式渲染时，通过Stencil mask排除掉一部分的采样，使用Depth == Equal，剔除被遮挡的片元。 因为把 4 个像素的信息压缩进 1 个像素的 4 个采样点，采样纹理时，需要手动向下修正一级（-1）。各项异性也要乘以0.5进行矫正。
    - 开启VRS 2\*2 时，为了避免使用一个像素填充4个像素造成的Block Artifacts，可以通过Fmask上记录的像素覆盖率，调整实际颜色。 也可以参考相邻Quad，进行插值。
    - Computer Shader 如何使用Stencil mask进行优化：
      - Plane Iteration：
        - 先在16\*16的Tile（对应256\*256）统计有哪几种采样点可能会用到，记录在wave中。
        - 该Tile上有几种采样点活跃就发送对应的Wave。每个Wave的所有线程都只计算一种采样点（s0，s1，s2，s3）以保证内存对齐。
      - Compaction：
        - 先在16\*16的Tile（对应256\*256）统计所有的需要计算的采样点数量tileSubSampleSum， 每64个采样点分配一个Wave进行处理。
        - 如果Wave的数量很少（2~3个），则认为采样点较为稀疏。不使用Plane Iteration策略，而是开启Packing。
        - BallotMask 配合 i = 1 to 4 遍历 S1​∼S3​（第一个采样点跳过），将采样的XY坐标存入itemQueue种。
        - 将itemQueue根据原像素的位置存入查找表outVRSTilePacking中。
        - 运行时，将线程重定位到需要计算的采样点上。
  - Foveated Rendering： 车机上没有眼球追踪设备，但可以参考赛车游戏的实现？
  - Visible Buffer: Software VRS
    > Visibility Buffer: 本意是用于解决臃肿的Gbuffer带来的开销的方案。 运行时使用一张全屏RT，将InstanceID, TriangleID，MaterialID压缩在一个32位uint中，写入RT中。 着色时通过TriID得到三角形的顶点属性，InsID获取MVP矩阵，MatID获取材质信息。
    > 可能还需要Barycentric Coord（重心位置）：用于插值顶点属性； Depth Buffer：记录当前像素的深度。
        本质对于一个2\*2 Quad，其四个像素如果被四个不同片元占据的话， 四个片元都会被执行一次（包含辅助像素）。 用V-Buffer可以提前知道像素应该使用哪个片元，避免浪费。 -> 因此对于越小的三角形，其VRS改善的效果越小？
    - 采用Iterative Interpolation的着色思路，以4\*4的像素块为例，分为5个阶段（16 = 2^4; 需要4+1个阶段）分开进行着色。 -> 无法使用片元着色器，只能由computer shader.
      - Pass 0： 选择一个固定的位置的像素进行着色计算。 此外对于周围四个像素的Drawcall ID均不同的像素也需要进行着色计算，保证几何正确性。
      - Pass1： 利用 Pass 0 已有的点，通过“十字交叉”或“对角线”采样，插值出中间点。
      - Pass 2-4：重复此过程，利用已有的（包括插值得出的）像素作为参考，进一步细分网格。
  - DOOM: Variable-Rate Compute Shaders (**VCS**) -> 需要把着色逻辑放在Compute Shader中。

--------------------
- https://docs.vulkan.org/features/latest/features/proposals/VK_KHR_fragment_shading_rate.html#_per_draw_state


---------------------
RRM: 
- https://zhuanlan.zhihu.com/p/586856514
- https://zhuanlan.zhihu.com/p/587988966
- https://zhuanlan.zhihu.com/p/585309726
- https://developer.apple.com/documentation/metal/scaling-variable-rasterization-rate-content?language=objc
- https://developer.apple.com/metal/Metal-Shading-Language-Specification.pdf
- https://docs.google.com/document/d/1ZC2JaRN21v9nMm-73492wRdk4ybvTJ8Li983DLgeiAY/edit?pli=1&tab=t.0

VRS: 
- https://zhuanlan.zhihu.com/p/628429688
- https://zhuanlan.zhihu.com/p/585309726
- https://discussions.unity.com/t/optimize-renderer-features-with-variable-rate-shading-in-unity-6-1/1605893
- https://docs.vulkan.org/features/latest/features/proposals/VK_KHR_fragment_shading_rate.html
- https://docs.vulkan.org/samples/latest/samples/extensions/fragment_shading_rate_dynamic/README.html#_shading_rate_image
- https://zhuanlan.zhihu.com/p/16771987518
- https://docs.nvidia.com/nvapi/group__nvapitypes.html#ga56d116a83e80cdf3777e7fa1b5d200dc
- https://docs.nvidia.com/nvapi/group__dx.html#ga03d79649623a7f5d76d5dfbdbc2dad31
- https://developer.nvidia.com/blog/turing-variable-rate-shading-vrworks/
- https://docs.nvidia.com/nvapi/globals_eval_n.html
- https://learn.microsoft.com/zh-cn/windows/win32/api/d3d12/ne-d3d12-d3d12_shading_rate      
- https://docs.vulkan.org/refpages/latest/refpages/source/vkGetPhysicalDeviceFragmentShadingRatesKHR.html
- https://registry.khronos.org/OpenGL/extensions/NV/NV_shading_rate_image.txt
- https://registry.khronos.org/OpenGL/extensions/EXT/EXT_fragment_shading_rate.txt
- https://github.com/NVIDIA/nvapi
- https://devblogs.microsoft.com/directx/gears-vrs-tier2/#vrs-texture-generation
- https://zhuanlan.zhihu.com/p/525804724
- https://gpuopen.com/learn/integrating-vrs-in-the-riftbreaker/
- https://zhuanlan.zhihu.com/p/2017375769647669935
- https://static.graphicsprogrammingconference.com/public/2025/slides/variable-rate-compute-shaders-in-doom/Fuller-Hammer-variable-rate-compute-shaders-in-doom-the-dark-ages.pdf
- https://zhuanlan.zhihu.com/p/138024983
- https://zhuanlan.zhihu.com/p/639479470
- https://registry.khronos.org/OpenGL/extensions/QCOM/QCOM_shading_rate.txt
- https://zhuanlan.zhihu.com/p/713593072
- Sig20_VRS_final_final_DROBOT
- https://filmicworlds.com/blog/software-vrs-with-visibility-buffer-rendering/
- https://zhuanlan.zhihu.com/p/683761529
- https://zhuanlan.zhihu.com/p/560714968
- https://github.com/GameTechDev/VRS-DoF
- https://www.zhihu.com/question/340549627
- https://zhuanlan.zhihu.com/p/626821650
- https://zhuanlan.zhihu.com/p/628429688
- https://www.blurredcode.com/2025/06/491d0591/#%E7%A1%AC%E4%BB%B6%E5%85%BC%E5%AE%B9%E6%80%A7%E9%97%AE%E9%A2%98
- https://developer.arm.com/Additional%20Resources/Video%20Tutorials/DevHub/Enhancing%20mobile%20game%20performance%20with%20Variable%20Rate%20Shading
- https://microsoft.github.io/DirectX-Specs/d3d/VariableRateShading.html
- https://forums.unrealengine.com/t/implementing-image-based-variable-rate-shading/1900726/6
- https://docs.unity3d.com/6000.1/Documentation/ScriptReference/Rendering.CommandBuffer.SetShadingRateImage.html?ampDeviceId=ddda3701-d887-48ef-8642-581b5c771bec&ampSessionId=1775016286272&ampTimestamp=1775211777505
- https://www.intel.com/content/www/us/en/developer/articles/technical/velocity-luminance-adaptive-rasterization.html
- https://filmicworlds.com/blog/decoupled-visibility-multisampling/
- https://filmicworlds.com/blog/software-vrs-with-visibility-buffer-rendering/
- https://developer.samsung.com/galaxy-gamedev/blog/en/2022/11/22/using-variable-rate-shading-to-improve-performance-on-mobile-games
- https://registry.khronos.org/OpenGL/api/GL/glext.h
- https://github.com/Microsoft/DirectX-Graphics-Samples/tree/develop/Samples/Desktop/D3D12VariableRateShading
- https://developer.arm.com/community/arm-community-blogs/b/mobile-graphics-and-gaming-blog/posts/using-asynchronous-compute-on-arm-mali-gpus
- https://learn.microsoft.com/en-us/windows/win32/direct3d12/vrs
- https://d29g4g2dyqv443.cloudfront.net/sites/default/files/akamai/gameworks%2Fevents%2Fcgdc16%2Fcem_vrworks_cgdc_cn.pdf
- https://github.com/jp7677/dxvk-nvapi
- https://discussions.unity.com/t/multi-res-shading-nvapi/614100/9
- https://github.com/KhronosGroup/GLSL/blob/main/extensions/ext/GLSL_EXT_fragment_shading_rate.txt
- https://developer.mediatek.com/VariableRateShading/6554775458a7392014e51c52.html
- https://developer.download.nvidia.com/video/gputechconf/gtc/2019/presentation/s9209-advances-in-real-time-automotive-visualisation.pdf