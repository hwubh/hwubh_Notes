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


--------------------------

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
    - 对于使用Compute Shader进行计算的pass，因为无法依赖硬件进行Shading Rate的读取。需要将整张SRI图读取进来，根据读取道德Shading Rate来决定哪些Thread直接return。用剩下的Thread扩大采样范围，填补被杀掉的线程留下的空袭。
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
        本质对于一个2\*2 Quad，其四个像素如果被四个不同片元占据的话， 四个片元都会被执行一次（包含辅助像素）。 用V-Buffer可以提前知道像素应该使用哪个片元，避免浪费。 -> 因此对于越小的三角形，其VRS改善的效果越小？
    - 采用Iterative Interpolation的着色思路，以4\*4的像素块为例，分为5个阶段（16 = 2^4; 需要4+1个阶段）分开进行着色。 -> 无法使用片元着色器，只能由computer shader.
      - Pass 0： 选择一个固定的位置的像素进行着色计算。 此外对于周围四个像素的Drawcall ID均不同的像素也需要进行着色计算，保证几何正确性。
      - Pass1： 利用 Pass 0 已有的点，通过“十字交叉”或“对角线”采样，插值出中间点。
      - Pass 2-4：重复此过程，利用已有的（包括插值得出的）像素作为参考，进一步细分网格。
  - DOOM: Variable-Rate Compute Shaders (**VCS**) -> 需要把着色逻辑放在Compute Shader中。