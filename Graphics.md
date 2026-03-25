- 1：#Normal #transform #Object_Space #World_Space Stop Using Normal Matrix: https://lxjk.github.io/2017/10/01/Stop-Using-Normal-Matrix.html
    *Tips* :![20240213203944](https://raw.githubusercontent.com/hwubh/hwubh_Pictures/main/20240213203944.png) The "M" should be "M<sup>'</sup>"

- 2：Texture Compression： https://zhuanlan.zhihu.com/p/634020434; https://zhuanlan.zhihu.com/p/237940807 https://zhuanlan.zhihu.com/p/158740249 https://zhuanlan.zhihu.com/p/1923192571934537263 
  - ETC: 人眼对亮度而不是色度更敏感这一事实。 因此，每个子块中仅存储一种基色 (ETC1/ETC2 由两个子块组成) ，但亮度信息是按每个纹素存储的。子块由1个基本颜色值和4个修饰值可以确定出4种新的颜色值。
    - 4\*4的像素块分为两个2\*4分块，共占据64bit: 每个分块存储1个RGB基色（12bit）, 1bit “diff”， 3bit 修饰位； 剩下的32位bit包含16个2位选择器，每个像素的颜色根据其二位选择器从四个颜色中选出一个。
    - RGB ETC1 4 bit ：4 bits/pixel，对RGB压缩比6:1，不支持Alpha，绝大部分安卓设备都支持。
    - RGB ETC2 4 bit ：4 bits/pixel，对RGB压缩比6:1。不支持Alpha，ETC2兼容ETC1，压缩质量可能更高，但对于色度变化大的块误差也更大，需要在OpenGL ES 3.0和OpenGL 4.3以上版本。 ->利用了 ETC1 中一些“逻辑上不可能出现”的位组合，开辟了三种新模式：
      - T-Mode & H-Mode：专门处理对比度较高的区域，减少边缘锯齿。
      - Planar Mode（平面模式）：这是 ETC2 的杀手锏，专门处理平滑渐变（如天空、皮肤），彻底解决了 ETC1 在渐变处的色阶断层问题。
    - RGBA ETC2 8bit ：8 bits/pixel，对RGBA压缩比4:1。支持完全的透明通道，版本要求同上。 -> ETC2 的基础上多64位来专门储存Alpha通道（EAC）
    - RGB +1bit Alpha ETC2 4bit ：4 bits/pixel。支持1bit的Alpha通道，也就是只支持镂空图，图片只有透明和不透明部分，没有中间的透明度。 -> 使用1bit 来记录Alpha值，只有0或1，不支持半透。
    - EAC 只存一共通道： 8 bit 基础值 + 4bit 乘法 + 4bit 修饰表索引(只有三位实际使用，所有只用8种派生值) + 16个3bit选择器。 每个像素有8种派生值选择。
  - DXTC / BC：https://en.wikipedia.org/wiki/S3_Texture_Compression
    - DXT1 / BC1: 用于RGB或只有1bit Alpha的贴图 
      - 4\*4个像素共64bit 为一个单位，前32bit存贮颜色的两个极端值(c0,c1)，后32bit分为4*4的lookup page，每个page对应一个pixel和2bit状态符（0:c0; 1: c1; 2:c2(插值的颜色$\frac{2}{3}c_0 + \frac{1}{3}c_1$)；3：c3（插值的颜色$\frac{1}{3}c_0 + \frac{2}{3}c_1$或transparent(RGBA均为0), if c0 <= c1）） -> 1:6
    - DXT2/3：在DXT1的基础上多出64bit来描述alpha信息，每个pixel的alpha使用4bit存储， 0-15，共16种选择值。二者的区别只有DXT2计算的颜色值预乘了Alpha值
      - DXT2：color: Premultiplied by alpha
      - DXT3 / BC2：独立
    - DXT4/5: 在DXT1的基础上多出64bit来描述alpha信息，alpha 以类似color的方式存贮，64bit 包含2个8bit 极端值，16个3bit 状态符，每个像素有8种插值选项。二者的区别只有DXT4计算的颜色值预乘了Alpha值
      - DXT4：color: Premultiplied by alpha
      - DXT5 / BC3：独立
      - if c0> c1, c2~7 插值； if c0 <= c1, c2~5插值，c6=0, c7=255
    - BC 4/5 : 
      - BC 4: 使用64位处理单个通道（同处理Alpha的方式）
      - BC 5: 使用128位处理两个通道（同处理Alpha的方式）
    - BC6H： 针对RGB16的HDR图。4\*4个像素共128bit. 包含两个48bit的RGB值，和16个2bit选择器。
    - BC7: 4\*4个像素共128bit. 动态分配位数，有8种模式。
  - PVRTC:
    - 不同于DXT和ETC这类基于块的算法，而将整张纹理分为了高频信号和低频信号，低频信号由两张低分辨率的图像A和B表示，这两张图在两个维度上都缩小了4倍，高频信号则是全分辨率但低精度的调制图像M，M记录了每个像素混合的权重。要解码时，A和B图像经过双线性插值（bilinearly）宽高放大4倍，然后与M图上的权重进行混合。
    - RGB PVRTC 4 bit ：4 bits/pixel，对RGB压缩比6:1
    - RGBA PVRTC 4 bit ：4 bits/pixel，对RGBA压缩比8:1
    - RGB PVRTC 2 bit ：2 bits/pixel，对RGB压缩比6:1
    - RGBA PVRTC 2 bit ：2 bits/pixel
  - ASTC: https://zhuanlan.zhihu.com/p/158740249
    - 每块固定使用128bit，块size：4\*4~12\*12， 压缩率从3：1 ~ 27：1

- 3：Color Space：https://zhuanlan.zhihu.com/p/548826041 ; https://zhuanlan.zhihu.com/p/66558476 ; https://zhuanlan.zhihu.com/p/609569101
  ![20240610133007](https://raw.githubusercontent.com/hwubh/hwubh_Pictures/main/20240610133007.png)
  - Gamma：2.2，屏幕输出时会将颜色变换到Gamma2.2空间中：$l = u^2.2, (l,u \in (0,1))$
  - Gamma矫正：$\frac 1 {2.2}$, 在屏幕输出前转到Gamma0.45，使屏幕最终输出转为Gamma1.0：$u_0 = u_i^{\frac{1}{2.2}}$
  - sRBG: Gamma0.45 Color Space
    - Why: <!-- 1: 存储时进行Gamma矫正；2： -->人眼对暗部更敏感，用更大的数据范围来存暗色，用较小的数据范围来存亮色。（下注）
           - Physically Linear（物理）: 以物理光子数量描述的线性数值空间 
             Perceptually Linear(感知): 以光子进入人眼产生的感知亮度描述的线性数值空间![v2-c3b18b218328d622be8a647b41b9c523_r](https://raw.githubusercontent.com/hwubh/hwubh_Pictures/main/v2-c3b18b218328d622be8a647b41b9c523_r.png)
             二者为幂律关系：Vphysically = （Vperceptual）^ gamma
             如果（相机/贴图）用**物理亮度**来记录**感知亮度**，则会有**精度**问题：当感知亮度为0.5时，对应的物理亮度只占据$\frac{1}{4}$的记录空间。用物理空间的亮度值来做为图像texel值的话，会使得保存或描述暗部颜色的bit位数不足，而人眼恰好善长分辨暗的颜色，这会让很多暗的颜色丢失。![v2-943fd8197c308e924e4cbc954f260741_720w](https://raw.githubusercontent.com/hwubh/hwubh_Pictures/main/v2-943fd8197c308e924e4cbc954f260741_720w.webp)
             因此，通常（相机/贴图）实际记录的是感知亮度。将接收的的物理亮度转化为感知亮度的过程称为**gamma encode**或**Image File gamma**。 将记录的感知亮度转化为物理亮度并发射称为**gamma decode**或**display gamma**。将前两者的乘积（多为*1*），称为**System gamma**。
    - Shader中的处理： 实际运算需现将**感知亮度**（sRGB贴图中的texel值）通过^2.2(**Remove Gamma Correction**)转换成**物理亮度**再实际进行。计算结束后需将结果再通过^0.45(**Gamma Correction**)转化为**感知亮度**。
    - 贴图：一般Diffuse（albedo）为sRBG, 而specular maps、normal maps，light maps，一些HDR格式的图片为线形物理空间（物理亮度）的贴图，以节省转换。
  - Unity：如果选择了Gamma，那Unity不会对输入和输出做任何处理，换句话说，Remove Gamma Correction 、Gamma Correction都不会发生，除非你自己手动实现；而Linear则对Shaderlab中的*颜色*输入，有[Gamma]前缀的Property变量（如*金属度*）以及在*sRGB Texture*采样时进行Remove Gamma Correction。
  - Gamma空间：使用非sRGB diffuse图时可以节省一步Remove Gamma Correction运算。 -> (8-bit通道里，暗部精度应该会有问题？)
  - Linear空间：使用sRGB diffuse时美术查看效果方便，shader中可以不用写Remove Gamma Correction。但Gamma Correction必不可少。

- 4：卷积：两个函数（输入函数：f(x), 权值函数：g(x)）的卷积，本质上就是先将一个函数翻转，然后进行滑动叠加。
     卷积的本质就是加权积分, 对于（输入函数：f(x), 权值函数：g(x)）来说，g是f的权值函数，表示输入f各个点对输出结果的影响大小。数学定义∑f(x)g(n-x)中的n-x表示x的权值和什么相关，也可以理解为一种约束。![20240616182256](https://raw.githubusercontent.com/hwubh/hwubh_Pictures/main/20240616182256.png)
  - 卷：函数的翻转：为“积”施加约束，指定参考（如信号分享中的在特定的时间段的前后进行“积”）
  - 积：积分/加权求和：是**全局**概念，把两个函数在时间或者空间上进行混合
  - ![v2-847a8d7c444508862868fa27f2b4c129_r](https://raw.githubusercontent.com/hwubh/hwubh_Pictures/main/v2-847a8d7c444508862868fa27f2b4c129_r.jpg)

- 5: 采样定理，频谱混叠和傅里叶变换：https://zhuanlan.zhihu.com/p/74736706 https://zhuanlan.zhihu.com/p/627793196
  - 采样：把模拟信号转换为计算机可以处理的数字信号的过程；采样定理：只有当采样频率fs.max > 最高频率fmax的2倍时，才能比较好的保留原始信号的信息。（实践中倍率多为介于2.56~4）
  - 狄拉克函数：在时域和频域都是脉冲状的；
    时域：周期为$T_s$![v2-2b3c294a40466b50571b0d905b34cb63_r](https://raw.githubusercontent.com/hwubh/hwubh_Pictures/main/v2-2b3c294a40466b50571b0d905b34cb63_r.jpg)
    频域：周期为$\frac{2\pi}{T_s}$![20240616200427](https://raw.githubusercontent.com/hwubh/hwubh_Pictures/main/20240616200427.png)
  - 时域的乘积等于频域的卷积（反之亦然）：采样相当于在频域在冲激函数的各频率处重复目标信号的频谱![v2-057fdc41813a61dae12dae44dcd49cd9_r](https://raw.githubusercontent.com/hwubh/hwubh_Pictures/main/v2-057fdc41813a61dae12dae44dcd49cd9_r.jpg)
  - 频域与时域：![20190513004552862](https://raw.githubusercontent.com/hwubh/hwubh_Pictures/main/20190513004552862.gif)
  - 频谱的混叠：在时域上采样如果不够快，也就是采样函数的频率过低，那么频域上频率重复的就会变得过快，最终会造成频谱的混叠![v2-914d0b3b671270340c5c872663f89b09_r](https://raw.githubusercontent.com/hwubh/hwubh_Pictures/main/v2-914d0b3b671270340c5c872663f89b09_r.jpg)
    当混叠发生时，可用使用*低通滤波*过滤到低频信息（图像上表现为模糊）![v2-12a2df5bc1b0d2fa13163f340f5a28ee_r](https://raw.githubusercontent.com/hwubh/hwubh_Pictures/main/v2-12a2df5bc1b0d2fa13163f340f5a28ee_r.jpg)

- FXAA与Sharpening：https://zhuanlan.zhihu.com/p/431384101 https://catlikecoding.com/unity/tutorials/custom-srp/fxaa/#3.7 https://wingstone.github.io/posts/2021-03-01-fxaa/
  - FXAA：Quality：
    - 边缘判断：梯度计算，采样5次，![20240616232605](https://raw.githubusercontent.com/hwubh/hwubh_Pictures/main/20240616232605.png)
    - 基于亮度的混合系数计算：采样9次，通过计算目标像素和周围像素点的平均亮度的差值，我们来确定将来进行颜色混合时的权重；![20240616232920](https://raw.githubusercontent.com/hwubh/hwubh_Pictures/main/20240616232920.png)
    - 计算混合方向：取梯度最大的方向，向上为正，向下为负，向右为正，向左为负：![20240616233142](https://raw.githubusercontent.com/hwubh/hwubh_Pictures/main/20240616233142.png)
    - 混合：将当前像素点的 uv ，沿着偏移的方向，按照偏移权重偏移；![20240616233334](https://raw.githubusercontent.com/hwubh/hwubh_Pictures/main/20240616233334.png)
    - 边界混合系数：针对斜边，要得到得到正确的混合系数，就需要扩大采样范围。判断边界的方式是计算两侧的亮度值的差，是否和当前的亮度变化梯度值符合。![20240616233628](https://raw.githubusercontent.com/hwubh/hwubh_Pictures/main/20240616233628.png)![20240616233702](https://raw.githubusercontent.com/hwubh/hwubh_Pictures/main/20240616233702.png)
  - FXAA: Console：简化版本
    -  边缘判断：梯度计算，采样5次，同Quality
    -  方向判断：计算当前亮度变化的梯度值，即亮度变化最快的方向，就是锯齿边界的法线方向。![20240616234222](https://raw.githubusercontent.com/hwubh/hwubh_Pictures/main/20240616234222.png)
    -  混合：沿着切线方向分别向正负方向偏移 UV ，进行两次采样，再平均后作为抗锯齿的结果。![20240616234250](https://raw.githubusercontent.com/hwubh/hwubh_Pictures/main/20240616234250.png)
    -  边界混合：因为对水平和垂直方向的锯齿不友好，故将偏移距离延伸至更远处。做法是用Dir 向量分量的最小值的倒数，将 Dir1 进行缩放。![20240616234723](https://raw.githubusercontent.com/hwubh/hwubh_Pictures/main/20240616234723.png)![20240616234752](https://raw.githubusercontent.com/hwubh/hwubh_Pictures/main/20240616234752.png)
    > 这一步相当于是对“混合”中的Dir1的重定义，与“混合”是二合一的一步。一步来说，计算Dir1 后就得进行Dir2的计算。 然后再用Dir2去做uv偏移。
  - 缺点：在光照高频(颜色变化很快)的地方不稳定（blend anything that has high enough contrast, including isolated pixels），移动摄影机时，会导致一些闪烁。
  - 可用于几何抗拒齿也可用于shading抗拒齿；使用一个pass即可实现FXAA，非常易于集成；与MSAA相比能节省大量内存；可用于延迟渲染；
  - 如何缓解FXAA带来模糊感？：https://gamedev.stackexchange.com/questions/104339/how-do-i-counteract-fxaa-blur ; 
    - Contrast Adaptive Sharpening: 也是使用拉普拉斯算子，只有边缘的黑白图，将该图加会FXAA后的图像。 局部对比度自适应来控制锐化的幅度，对比度越大，锐化程度越大； 此外通过数值钳位（Clamping），确保锐化后的像素依然落在周围像素的取值范围内（经过锐化计算后的新像素值，不能超过周围像素中的最大亮度 (Max) 和 最小亮度 (Min)。）。
    - sharpending？/edge detection： 先用edge detection 计算出高频部分，然后乘以一个sharpness系数，加上FXAA处理后的图片。

- TAA：历史帧的数据来实现抗锯齿，每个像素点有多个采样点，但均摊到多个帧中。
  - 静态：只保留上一帧计算的结果与当前帧两帧。
    - 次采样点：就是在每帧采样时，将采样的点进行偏移，实现**抖动** (jitter)。 采样点的偏移与次序使用 *Halton* 序列![20240624194119](https://raw.githubusercontent.com/hwubh/hwubh_Pictures/main/20240624194119.png)
             **抖动**：通过修改投影矩阵的$m_20 , m_21$项来偏移XY分量。![v2-143d0f5393f5c7b9d9b18eeba2ce66eb_r](https://raw.githubusercontent.com/hwubh/hwubh_Pictures/main/v2-143d0f5393f5c7b9d9b18eeba2ce66eb_r.png)
    - 混合：因为在HDR空间下作TAA效果抗锯齿效果不佳；在postprocessing后做TAA会影响需要在HDR中计算的bloom等效果；所以开启TAA时需要两次Tone mapping：（下图方案一）![v2-c4ccc37c5541f7a7fe166bc7fafc36b8_720w](https://raw.githubusercontent.com/hwubh/hwubh_Pictures/main/v2-c4ccc37c5541f7a7fe166bc7fafc36b8_720w.webp)
          最后将历史帧数据，和当前帧数据进行 lerp 混合。
          > 涉及物理（能量）的操作需要在HDR中做。不涉及的美术效果（如FXAA, 锐化，LUT）在LDR中做。但TAA又需要在后处理前做，所有只能进行两次Tone mapping.
          > 历史帧 是未经过后处理的画面。
          > [Survey of Temporal Antialiasing Techniques](http://behindthepixels.io/assets/files/TemporalAA.pdf) 的 <3.3.1. Sample accumulation in HDR color space> 
  - 动态（相机移动，物体静止）： 
    - 重投影：当相机移动后，使用当前帧的深度信息，反算出世界坐标，使用上一帧的投影矩阵，在混合计算时做一次重投影。![v2-b68e86d6db5205544484fe1a6b910da0_r](https://raw.githubusercontent.com/hwubh/hwubh_Pictures/main/v2-b68e86d6db5205544484fe1a6b910da0_r.png)
      - Reverse Reprojection（重投影）：记录上一帧的MVP矩阵，当前帧渲染时会使用上一帧的MVP矩阵对像素进行反向投影，看是否可以在上一帧的帧缓冲里面找到(此处判断是否找到：根据物体ID、深度等信息)。若找到则复用，未找到则标记为“遮蔽”。（如果是蒙皮mesh，还需要记录骨骼位置）
  - 动态（物体移动）：
    - Motion Vector/Velocity：像素在历史帧与当前帧在屏幕空间下的位移。存储在 Motion Vector/Velocity 贴图（RG16格式，对精度要求高）上。
      <!-- P.S.: UE中为了节省Velocity Buffer 的带宽，只计算运动物体的Motion Vector，  -->
    - 使用 Motion Vector：使用 Motion Vector 算出上一帧在屏幕空间的坐标（使用双线性模式进行采样，因为不一定在像素中心位置。可以对历史帧进行*锐化*处理）。
                          因为 Velocity buffer本身也有锯齿，采样几何体边缘可能引入新的锯齿。所以可以比较该像素周边3x3像素的深度，选用深度最小的那一个的velocity。
    - Ghosting（鬼影）：当新的像素出现时，如果前后帧采样的颜色差过大的情况下进行混合。解决方式：对比当前帧和历史帧（以及相邻的像素），将历史帧的颜色截断（clamp/clip）在合理的范围内。
      Flickering（闪烁）：抖动导致的不收敛，子采样点存在部分高频信息，混合后造成的闪烁。本质上是高频信息被离散的光栅化方法限制的问题。着色走样：假如历史帧存在高光，而当前帧却因为子采样点的抖动没有采样到高光信息，历史帧的高光信息就会被截断，就会导致这一高光“忽隐忽现”。 集合走样：当一个在屏幕空间极其细小的三角形经过光栅化时，谁都不能在看到显示结果时得知其是否被光栅化到了某一个像素上，这就是“薛定谔的光栅化”。
    - 解决方式：对采样的历史帧和当前帧数据进行对比，将历史帧数据 clamp/截断 在合理的范围内：读取当前帧数据目标像素周围 5 个或 9 个像素点的Max，Min值作为范围。然后：clamp或clip；在TAA之后进行一次滤波（低通），虽然可以有效减少闪烁，但是会让画面比较模糊。
    - 混合：使用一个可变化的混合系数值来平衡抖动和模糊的效果，当物体的 Motion Vector 值比较大时，就增大 blendFactor 的值，反之则减小![20240625015835](https://raw.githubusercontent.com/hwubh/hwubh_Pictures/main/20240625015835.png)
  - https://zhuanlan.zhihu.com/p/479530563；https://zhuanlan.zhihu.com/p/425233743；https://zhuanlan.zhihu.com/p/366494818
- FSR: 1.0是空间域的。 2.0 是时间域的。 3.0是时间域+补帧。

- 光栅化
  - 如果是按线框来光栅化的话，是根据斜率，当斜率较大时，每步进一个单位的x/y，对应的y/x会步进若干个单位，因为锯齿明显。
  - https://zhiruili.github.io/posts/rasterization/

- FrameBufferFetch 和 Renderpass/subrenderpass：
  - FrameBufferFetch 允许在一个pass在fragment shader阶段对当前缓冲区的像素进行访问- 》 读取当前像素位置的 原始值，根据读取的原始值计算新值，再写回同一位置。 （通过硬件）
    - 在Tile-Based GPU： Framebuffer 数据保留在Tile Memory中，不需要经过显存，减少带宽消耗。
    - 数据同步问题：
      - 单像素顺序性：同一像素位置的片段着色器调用会按提交顺序执行（如深度测试通过顺序）。
      - 多像素并行性：不同像素位置的读写可以并行，硬件自动管理依赖。
      - 不能跨像素读取数据 -> 未定义操作
    - 本质： 同时读写RT? 节省传统 Load/Store RT的消耗。
  - Renderpass/subrenderpass: 允许在一个Renderpass的多个subpass （相当于上文的pass）之间复用输入输出。 - 》 即当前subpass的输出之间作为下一个subpass的输入使用，避免通过显存造成的带宽消耗。
    - 在Tile-Based GPU： Framebuffer 数据保留在Tile Memory中，不需要经过显存，减少带宽消耗。
    - 本质： 类似memoryless？ 不需要跨帧使用的，即写即用即抛的RT不要写回显存中。
    - Tile-based：Vertex阶段逐subpass的，所有的subpass的vertex阶段后（结果写入一块内存中）才有Tile的划分。  ![20250313172611](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250313172611.png)
                  Fragment阶段是先逐Tile，即该tile所有subpass的fragment都依次执行完毕后，才执行下一个Tile。
  - 注意事项： Tile Memory 容量有限！ 
  - https://www.zhihu.com/question/469595919 https://zeux.io/data/gdc2020_arm.pdf https://zhuanlan.zhihu.com/p/744643395 https://zhuanlan.zhihu.com/p/574540329 https://zhuanlan.zhihu.com/p/640672385 

- Texture Streaming:  https://zhuanlan.zhihu.com/p/600257663
  - def: 根据摄像机的位置只加载对应Mipmap Level的纹理到显存中
  - 加载的对象： 计算得到的Mip级别及比其更高Mipmap级别
  - 加载逻辑： 
    - 当渲染一个使用Mipmap纹理的GO时，CPU将最低Mipmap等级（人为设置）的Mip异步加载到显存中。
    - GPU先使用这些低级Mipmap渲染GO。
    - cpu计算出该GO必不可能用到mip等级，比如计算出x意味着只可能会用到x+1 ~ n级Mip，将x+1 ~ n级Mip加载到显存中。
    - 当GPU对纹理进行采样时，它会根据当前像素的纹理坐标和GPU计算的两个内部值DDX和DDY来确定要使用的Mipmap等级，也就是根据像素在场景中覆盖的实际大小找到与之匹配的Texel大小，根据该Texel大小决定要使用的Mip等级。补充说明，DDX和DDY提供有关当前像素旁边和上方像素的UV的信息，包括距离和角度，GPU使用这些值来确定有多少纹理细节对相机可见。
  - 纹理异步加载 AUP: 
  - 纹理支持Mipmap Streaming： 勾选Streaming Mipmaps； （Android上）需要开启LZ4 或LZ4HC 压缩格式 -》 实现异步纹理加载； 调整Mip Map Priority， 设置优先级
  - Max Level Reduction（初始Mip级别） 与 Memory Budget： Max Level Reduction在Mipmap Streaming System中优先级比Memory Budget高，意味着即使会超出Budget，纹理依旧会加载Max Level Reduction级别的Mip到显存中。
  - Texture.streamingTextureDiscardUnusedMips： 是否主动卸载无用Mip？不开启时，不用的Mip进入缓存池，等待budget不够用时才卸载。 反之关闭时，不用的直接卸载，节省内存。
  > 不激活Texture.streamingTextureDiscardUnusedMips的情况下，Mip被丢弃的时机（指已加载的Mip从显存中卸载）应该只有一个，即当前纹理串流预算不足，且需要加载新的Mip Streaming Texture.
  - 管理策略： 
    - Non Streamed Texture被加载时，完全加载。
    - 加载Scene时，如果Budget未满时：Texture会完全加载。 不足时，按Max Level Reduction加载
    - 动态加载GO Texture在Load和Instantiate时， 首先加载Max Level Reduction级的Mipmap。
    - 实际渲染GO时， 按照当前空闲的纹理串流预算和摄像机和物体之间的距离等等因素去计算当前需要加载的Mipmap等级。如果Budget足够，则加载计算出的Mipmap等级；如果Budget不足，则依然加载Max Level Reduction级别的Mipmap。 
    - 运行时，如果新的texture加载时会超预算。以距离摄像机从远到近的顺序重新计算Scene中的所有GO，卸载掉使用了过高级别的Mipmap级别。 如果卸载后空间足够，加载该新texture计算的mipmap级别；如果不够，则加载Max Level Reduction级别的Mipmap。
  
- IBL: - 15： IBL: https://zhuanlan.zhihu.com/p/66518450 https://zhuanlan.zhihu.com/p/69380665 https://zhuanlan.zhihu.com/p/56063836  https://zhuanlan.zhihu.com/p/563676455 https://zhuanlan.zhihu.com/p/144438588 https://www.pauldebevec.com/ReflectionMapping/IlluMAP84.html https://research.nvidia.com/sites/default/files/pubs/2017-02_Real-Time-Global-Illumination/light-field-probes-final.pdf https://www.cnblogs.com/KillerAery/p/16828304.html#reflection-probe https://www.zhihu.com/question/63086916 https://zhuanlan.zhihu.com/p/404520592

- Occlusion Culling: https://zhuanlan.zhihu.com/p/363277669 https://zhuanlan.zhihu.com/p/701883987 https://zhuanlan.zhihu.com/p/842429737 https://zhuanlan.zhihu.com/p/565197985
  - 预计算可见性/Precomputed Visibility: 适用于静态物体。 将场景划分为一个个Cell, 每个Cell会将可见的静态物体的ID记录在其对应的Bit Array中。 运行时，根据相机在哪个Cell中，则取出对应的Bit Array，可以知道有哪些静态物体是可见的。
    > 如果要存在动态物体，则需要实时计算该物体所在的cell。然后根据Cell之间的可见性信息，判断可见性。
  - Portal-Culling： 适用于静态物体。 将场景划分为一个个Cell，每个cell记录它直接相邻的Portal，和通过Portal能连接到哪些cell。 因为Cell的大小不一定时固定的，可以通过八叉树来储存Cell的index，用于后续的查找。 运行时，通过相机位置找到所在的Cell。先计算portal的AABB是否与视锥体相机，如果相交的话，则根据该portal与相机构建一个新的视锥体，用于下一次递归中的视锥体。 
    每个Cell会维护一个静态物体的列表和一个动态问题的列表。 对于动态物体，会根据其位置，实时更新其到其对应的Cell上。运行时，如果所在的Cell联通了，还需要用Portal构架的新视锥体与该Cell上记录的动态物体进行求交测试。 
    如果要计算动态物体的遮挡的话，可以将其渲染到低分辨率的深度图上。在判断Cell或物体的相连情况时，参考该深度图。（Software Occlusion的思路）
    > 可以通过调整Portal的开关来模拟门的开关。
  - 硬件遮挡查询： 使用一个depth-only pass将深度写入Z-buffer中，然后传入物体到GPU进行遮挡剔除。  在GPU内三角形会被光栅化，其结果与z-buffer比较但不会写入深度，标记其可见像素数量n，如果n=0代表物体会被完全遮挡需要被剔除掉。 最后再把信息传回CPU进行剔除。
    - 用于depth-only pass渲染的物体使用包围盒或proxy mesh代替。
    - depth only pass 可以使用一个材质来统一绘制。
    - 粗糙深度测试： 利用硬件层面的 Tile 优化，如果一个区域的深度值远小于物体，直接判定为遮挡，连像素级别的比对都省了。
    - 查询逻辑优化 (Binary Query)： 如果有一个像素通过了剔除，则省略后续该物体上其他像素的测试。
    - 解决回读延迟： 下一帧再用 或 对于支持断言/条件（predicated/conditional）的遮挡查询的API（如DX, OpenGL）, GPU可以记录遮挡插混后可见的物体的ID。后续提交的物体如果不在可见列表，也不会渲染。
      > Coherent Occlusion Culling中：将渲染队列分为上一帧可见的，和上一帧不可见的。 上一帧可见的直接绘制，并给不可见的发送查询请求。 查询请求则在CPU 处理好Drawcall提交，GPU正在绘制时的时间，由CPU完成查询，并补上可见物体的绘制请求。
  - Hierarchical Z Buffer:
    准备 Hierachical Z-Buffer: 获取一张遮挡体的深度信息的深度图。通过将采用声明Mipmap。Mipmap存储的是深度的最大值而不是平均值。
    > 只渲染遮挡体可能导致深度图上的一些像素缺少深度信息（出现所谓的“**洞**”）。《大革命》中选择默认值为0，这可能导致出现错误剔除。 但填入1的话，会导致高级别Mipmap的取值皆为1.
    遮挡查询：被遮挡体的包围盒投影到屏幕空间，然后找到一个mipmap层级使其包围盒只覆盖2x2个像素。 首先将被遮挡体的包围盒的顶点，变换到屏幕空间，得到屏幕空间中uv方向的包围盒。 根据该包围盒在Mip0的UV方向覆盖了多少个像素（覆盖像素较多的那个方向），计算并选择合适的Mipmap层级进行深度比较。 使用包围的顶点作为uv坐标，采用Mipmap层的深度图进行深度比较，如果四个深度值都比顶点的深度小，则说明该被遮挡体被遮挡了，需要被剔除。
    - 大革命： 挑选最近300个遮挡提做全屏渲染深度，用于Hi-z和 Early-Z。 同时这张图可以与经过了重映射的上一帧深度图之间进行互相补“**洞**”的操作。
      - Shadowmap相机进行遮挡剔除： 将主相机的深度图均分为等大的Tile（日16*16），选择深度最大的Z值。结合近平面的Z值，深度最大的Z，与Tile在相机空间的XY值构成一个长方体。使用该长方体作为阴影贴图相机的遮挡体。
    - Two-Phase Occlusion Culling就是：第一阶段用上一帧的depth pyramid先剔除一遍物体， 渲染可见物体，第二阶段先生成当帧的depth pyramid，再用新的depth pyramid重新剔除第一阶段已经被剔除掉的物体，渲染上一帧不可见，但是这一帧可见的物体。
    > 快速移动相机时的处理方法:
      重投影
      预先遮挡体
      Two-Phase
      检测相机的旋转角速度：如果速度未超过阈值，则使用重投影的hiz图进行剔除。如果超过了阈值，调整depth bias? 帮运行离相机近的物体作为Occluder。
  - Software Occlusion Culling: 
    将遮挡体经过视锥，背面剔除后的结果光栅后记录全分辨率的深度。然后以Tile为单位进行将分辨率，每个Tile的最大深度对应降分辨率Depth Buffer的一个像素的深度。
    对被遮挡体包围盒进行视锥剔除，剔除结果变换到屏幕空间，用包围盒的最小深度与包围盒覆盖的Tile的最大深度进行比较。如果包围盒的最小深度比Tile的最大深度还大，说明这遮挡，可以剔除。
    - Masked Software Occlusion Culling （MSOC）: 
    将划分为矩形Tile（32*8），充分利用CPU的SIMD特性（这里为8路SIMD,每路32通道），一次性输出一整个Tile的结果。
    每个Tile用两个float Z0max和Z1max 来表示深度，和32位的mask来表示这个Tile上各个像素是用Z0max还是Z1max。 因为Tile存储的是深度是一个范围，不需要保留全分辨率的深度图也能得到比较好的裁剪精度。
      - 光栅化： 
        会计算顶点的数值确定三角形覆盖的Tile，每个Tile只需要计算一次边缘方程，通过递增递减的形式快速求出后续的覆盖情况。 
        通过scanline对三角形进行扫描并光栅化。
        每个SIMD单元负责32×1或者8×4个像素的处理逻辑。!   [20260324153112](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20260324153112.png)
        将三角形未覆盖的区域的Mask设置未0。反之为1？
      - 计算Hierarchical Depth Buffer和遮挡剔除：
         每个Tile只存3个数据，两个浮点数Z0max，Z1max和32位的mask（uint）。 Z1max叫做Working Layer，记录的时近处物体的最大深度。 Z0max为Reference Layer，记录的是整个Tile的最大深度。mask用于标记当前像素使用哪个Zmax。 ![20260324154158](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20260324154158.png)
         深度大于Z0max的三角形会被直接剔除，处于Z0max和Z1max之间的三角形会进行光栅化且更新Z1max的深度。 ![20260324154300](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20260324154300.png)
         如果三角形的深度要远小于Z1max和Z0max，这意味着该三角形可能属于近景的物体，大概率整个物体都不会被剔除掉，这时需要把mask的值设置为全部取0，且Z1max移动到近裁剪面 ![20260324154424](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20260324154424.png)
         当Z1max1的值铺满整个屏幕（mask全部取同一值），这个时候就会用Z1max覆盖Z0max的值。
         > 如果不使用 Mask，软件遮挡剔除（SOC）每画一个三角形都要去更新全局深度图，CPU 的同步开销很大。
         **将光栅化和遮挡剔除合并在一个步骤中**。 通过Z0max对物体包围盒进行剔除，
  - GPU-Driven Pipeline: 在GPU中做遮挡剔除的计算。
    - CPU 粗糙视锥剔除: 做视锥体剔除和用软光栅在CPU做粗糙的遮挡剔除, 减少上传大GPU的Buffer尺寸。
    - CPU 合并Instance： 将不同mesh合并为为一个Instance。（Merge Instancing： 可以是所有参与合并的Mesh，在Vertex Buffer中占据相同大小的空间。 用于后续通过Vertex Buffer计算处Instance ID）
    - CPU 通过哈希合并Drawcall: 将不同材质参数，PSO数据（每个PSO对应一个shader变体）通过哈希的形式压缩一个Buffer中， 通过实例的Instance_id查找对应的数据。 合并到一个Drawcall中进行提交。
    - GPU Instance Culling: 在GPU中进行Instace颗粒的视锥剔除和遮挡剔除。遮挡剔除可以使用Hi-z.
    - GPU 拆分为Chunk（可选）： 当Instance 直接拆分位Cluster时数量过多时，可以先将Instacne拆分为Chunk作为中间层。 每个Chunk中包含64个Cluster。 以平衡不同Warp上的工作量（如果每个Warp分配一个Instance的话）
    - GPU 拆分为Cluster: 64个三角形组成一个Cluster， 每个线程处理一个cluster。 极限技术Cluster的包围盒在屏幕上的投影， 与Hi-z进行深度测试。 另外可以对通过了测试的Clusetr进行排序，尽可能保证从近到远的顺序，方便后续的early-z。 
    - GPU 三角形剔除：对于三角形可以采用多种剔除方法，下图中包含背面剔除，细节剔除，深度提出，小图元剔除，视锥体剔除等，这些更细粒度的剔除需要考虑场景来组合使用。![20260324171054](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20260324171054.png)
    - GPU 合并Index: 将通过了剔除的三角形或Cluster重新打包。 将所有通过测试的三角形索引（每 3 个索引为一个单位）依次写入一个新的、连续的 RWStructuredBuffer 中。 在写入紧凑 Buffer 的同时，系统通常会按照**材质（PSO/Instance）**进行分组，作为不同的Drawcall，调用DrawIndirect提交绘制。
    - 交错式顶点buffer： 将instance 中不同的顶点属性放在不同的buffer中储存。提升hit rate。

- 全局光照 Global Illuminance (**GI**): GI = 直接光照 + 间接光照 + 环境光照
  - 环境光的漫反射部分：
    - irradiance map: 通过蒙特卡洛积分，积分整个半球上的，方向分布遵从余弦加权分布采样 (Cosine-weighted Sampling)。 
    - 球谐函数： 将一个球面函数，进行投影/projection后，得到球谐系数。然后又可以从球谐系数重构/reconstrcution出原函数的近似。
      - 一般分为三阶，九个系数：
        - 第一阶： 一个系数：无方向性的环境光均值。
        - 第二阶： 3个系数，对应可见中的XYZ轴： 表示主光源的方向偏好，可以模拟处明暗面
        - 第三节： 5个系数： 细节修正	产生平滑的颜色过渡和光影对比
      - 消除振铃效应：导致弱光的一侧出现负值的黑边或光照不连续 -> 相当于为了拟合波峰（高光过亮），将高阶系数调大，导致了下冲（Under shoot）的情况。
        - Windowing： 给高阶因子乘以衰减因子。
        - Clamp: UE 取最强方向对面光照的光强度作为最小光强度，将最小光强度 clamp 到至少最强光的 5%。
  - 环境光的高光部分： IBL的环境高光： Radiance map ？ -> 反射探针？ split-sum来算不同粗糙度的？
    - 误差： 高光技术拆分为两个可以预积分部分你的误差。（左边是表示各个方向光照的辐照度汇总, 右边是BRDF的汇总）， 和假设 N = R = V的误差。
    - BRDF项的积分结果可以使用近似函数代替。(模拟G*F)
  - 环境光遮蔽/AO: 
    - Baking AO： 烘焙到贴图或顶点上。
      - 烘培进lightmap： （技术镜面反射时）可以通过当前采样点的lightmap亮度/环境平均亮度 算出遮挡比例，用于把采样反射探针得到的高光信息也按比例遮挡。
      - 烘培为单独贴图：兼顾动态环境-> 最终环境光=实时环境光颜色×AO贴图采样值。
      - AO 贴图 + Bent Normal： 允许离特定方向（Bent Normal）较近的光线进入。
    - SSAO: 必须有 depth-buffer ， 选有normal-buffer
      - depth： 使用球形分布（有normal时只生成半球？）的采样点，在目标像素周围，视图空间内，在一定半径内的球行内部进行 N 几个采样点的采样，然后将采样点映射到屏幕空间，和 depth-buffer 中的深度进行比较，记深度测试通过的点个数为 M，得到AO系数为$\frac{M}{N}$。
      - Normal: 在切空间的正半球布置随机点。 计算采样点与球心和normal的cos值，作为权重。
    - Volumetric obscurance： 在目标周围点确定一个球体，将球体中的被占据的体积比例作为Volumetric obscurance 系数![20260325172957](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20260325172957.png)
      - 如果考虑normal：则取目标点法线上方切空间中一个球形的占据比例![20260325173029](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20260325173029.png)
    - HBAO: RAY-MARCHING 发射射线找离距离水平地面最高的点
      ![20260325173925](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20260325173925.png)  
    - GTAO: 在屏幕空间方向上寻找“视界线”（Horizon Angles）。 
      - 扫描方向： 在屏幕像素周围旋转采样（比如取 4~8 个方向 ϕ）。
      - 寻找切线角： 在每一个方向上，步进采样深度缓冲，找到左右两个方向上遮挡最严重的“地平线角度” h1​ 和 h2​。
      - 物理积分： 得到了这两个角度，就意味着你知道了从这个点看出去，有多少“天空”是露出来的。
      ![20260325174145](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20260325174145.png) 
      - Multi-bounce（多重弹跳） 模拟： 将原本单一的遮蔽值（AO），根据材质的 反照率（Albedo/BaseColor） 进行了一次非线性的映射。
  - lightmap（irradiance map）： 针对静态物体，离线烘培其光照信息，包含了直接光+间接光+阴影？ -> 只存了漫反射信息？
  - light probe: 用于环境光照的漫反射部分。 储存3阶9个float3。 把分散的探针连接成一个个四面体。 
    - 逐物体的：根据角色中心点P在四面体中的中心坐标计算插值得到的球谐系数。 每个Mesh共用一个球谐戏数？
    - LPPV / Volume： 逐像素的： 在物体周围生成一个隐形的网格。每个网格点中都有一份插值好的球谐系数。 着色计算时，根据片元的世界坐标去网格中做3D 线性插值。
    - 漏光： 室内外分开烘焙，门口做过度处理。
            设置室内外标记，记录权重值。 计算重心坐标时卡奥率权重值。 
  - Irradiance Volume / Volumetric Lightmap： 使用3D 纹理，思路类似Light probe/ LPPV： 不过使用二阶球谐系数来储存光照信息。
  - Precomputed Radiance Transfer(PRT):离线环境从每个Probe发射大量的射线，以预计算整个场景的Albedo Emission等色彩信息，同时再发出少量射线，将碰撞到的采样点的位置和法线储存下来，在运行时计算这些采样点的受光情况（天光，太阳光，局部光等），将这些光照信息乘到提前烘焙好的albedo，并叠加emission，最后得到一个看起来像那么回事的plausible的结果
    - 拆分色彩部分的计算和灯光可见度的计算： 
      对于可见性（几何与材质），因为场景的物体位置，颜色，自发光，遮挡关系等是固定的，可以将这部分烘培进probe里。 得到预计算的传输系数。
      对于颜色部分（光源）:如主光源方向，skybox等颜色等颜色相关的，则是动态计算的。实时把当前的太阳光、天光也转化为一组 SH 系数。
      最终光照=预计算的传输系数⋅实时灯光系数
  - Dynamic Diffuse Global Illumination: 通过硬件加速计算传输系数。
- 