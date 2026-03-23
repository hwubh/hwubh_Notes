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
> mt 8678
> mt 8673
> mt 8626
> mtk 8668
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
  > 对于高分辨的画面，Tier 2 VRS的性能改善会比较明显。 
  - Call of Duty: Software VRS 
  - Foveated Rendering： 车机上没有眼球追踪设备，但可以参考赛车游戏的实现？
  - Visible Buffer: Software VRS
  - DOOM: Variable-Rate Compute Shaders (**VCS**) -> 需要把着色逻辑放在Compute Shader中。