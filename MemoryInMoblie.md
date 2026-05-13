GPU Architecture：
- VRAM与System memory共用物理内存。 
- 每个**GPU Core**(逻辑上也叫 **Shader Core**)上封装了一块**Tile Memory** (Alias **Tile Buffer**, **on-chip framebuffer**), 大小不固定，最小可能就16\*16的像素。 除此之外还有ALU，Regiser File，L1 Cache，Texture Unit。
- 各个GPU Core之间还有共享的L2 cache。
- Apple Silicon和现代的骁龙（Snapdragon）和天玑（Dimensity）芯片也还存在可供CPU, GPU, NPU访问的**SLC (System Level Cache)**介于 L2 cache和System memory之间。
  > Apple 叫 **GPU Last Level Cache**

- Batching（合批）: 合批可以从三个角度进行考虑，1：减少管线状态切换； 2：资源绑定； 3：渲染指令的调用。 一次Drawcall含有以下操作: 
    设置/切换 PSO（管线状态对象）（Shader程序；混合、深度等渲染状态； 顶点布局描述）;
    绑定 Shader 资源（常量缓冲区、纹理、采样器）
    更新各类常量（世界矩阵、材质参数等）
    绑定 顶点(Vertex)缓冲， 索引（Index）缓冲（定义哪些顶点形成三角形）
  - 管线状态切换： 设置/切换 PSO
  - 资源绑定: 绑定 Shader 资源（常量缓冲区、纹理、采样器） + 更新各类常量（世界矩阵、材质参数等） + 绑定 顶点(Vertex)缓冲， 索引（Index）缓冲（定义哪些顶点形成三角形）
  - 渲染指令的调用: drawcall减少，比如DrawInstance 合并了Input Assembly阶段中 VertexBuffer 和 IndexBuffer 的绑定，以及将多次DrawIndexed绘制指令合并成一条DrawIndexedInstanced。
  - SRP Batching: 针对前两点 -> 合并了PSO的设置以及材质对应的PerMaterialBuffer的绑定；
  - Static/Dynamic Batching: 通过合并相同材质的Mesh以减少Drawcall。
  - Bindless: 优化资源绑定开销和绘制指令调用开销. 允许绑定固定/不定长度的描述符到GPU，将绑定纹理的步骤从CPU转移到了GPU。 因此通过PerInstance 数据里增加一个纹理 ID，指向 Bindless 堆，从而实现“一个 DrawCall 画不同纹理的物体”。 
  - PSO缓存？
- Binding Mode: OpenGL 和早期DX上限制了Shader可以访问的贴图数量(对应有几个槽位Slot). CPU 在渲染前，必须显示调用指令"BindTexture(MyTexture, Slot 0)". Shader中写死：layout(binding = 0) sampler2D myTex;。 Slot不够时可以使用VT方案或Texture Atlas, TextureArray来节省Slot.
  - Bnindless(Unbounded)： 将 Buffer\Texture 的 GPU 虚拟地址存储在 Bindless Buffer 中，在 Shader 中通过索引 Bindless 而直接访问 Texture\Buffer 数据的技术。
  - Pros：
    - 减少Drawcall（切纹理造成的， 但不能减少SRP Batcher开启时的SetShaderPass）
    - 不收Slot数量影响
    - 不需要再频繁地管理 PropertyBlock 或切换 Descriptor Set。材质数据可以精简为一个结构体，里面存着几个 uint 索引，内存布局非常紧凑。（？） -》减少了绑定资源的消耗？
  - Cons： 会占用Register存储Resource Descriptor。
  - 假 Bindless： 通过Texture Array来避免Slot数量闲置。对Array中的纹理有限制（相同维度、相同大小、相同格式） -》 更新Slice中的数据时需要更新整个内容数据。Bindless只需要更新指向的句柄（Descriptor）。
  - 有限 Bindless：  D3D12 和 Vulkan 中， 可以使用Descriptor 数组（Array of Descriptor， **AoD**）。 每个element对应一个单独的资源。
    - 有限： 需要在Shader中指名AOD的大小，无法动态调整。
  - 真Bindless: shader中定义AOD时不指定大小。
  - Procedure: 
    - CPU侧更新： 更新描述符来实现动态绑定不同的资源
    - GPU侧绑定： 根据索引去找查找对应的VRAM地址
  > 描述符集布局（Descriptor Set Layout） 是一个模板，它预先定义了一个描述符集由哪些资源绑定点组成，每个绑定点是什么类型、能被哪个着色器阶段访问.  其可以由若干个VkDescriptorSetLayoutBinding 组成，其中如果VkDescriptorSetLayoutBinding 的descriptorCount 大于一，则说明其绑定的描述符数量不止一个，如果绑定的shader，则可以视为一个AoD。

Nanite:
- Nanite Mesh:
  - 将Mesh切分为Cluster
    > cluster的优势: 更新粒度的剔除，提升Cache命中率 
    - cluster: 静态构建，使用 HLOD组织。
  - 在Cluster上用BVH都将HLOD
  - 压缩顶点属性，Index
- Procedure
  - Streaming: 从上一帧回读的 Cluster Page Request 数据异步上传 Cluster 渲染数据
  - InitContext： 初始化
  - CullRasterize： 执行剔除与光栅化
    - InstanceCull
    - PersistentCull ： 使用上一帧的HZB
    - 硬件光栅化/软件光栅化：根据Cluster的屏幕空间大小选择不同的光栅化方式。 输出Visible Buffer
    - 构建 HZB
    - Post PersistentCull： 补漏，对被遮挡的 BVH Node 和 Cluster 进行剔除； 使用当前帧的BVH。
    - Post 硬件光栅化/软件光栅化
  - EmitDepthTargets: 生成Depth 相关的 Scene Depth、Stencil、Velocity、Material Depth 等 Buffer
  - BasePass: G-Buffer
  - Shadows: VSM需要的Detph
  - Readback： 回读在 PersistentCull pass 中产生的 Cluster Page Request 数据。
- Visible Buffer： RG32格式，R通道7bit存InstanceID，25bit存VertexID。 G通道存depth。 Material信息存在另外的 Material depth buffer中。
  - InstanceCull: 以Mesh为单位的culling。 
  - PersistentCull: 以cluster为单位的culling。 先通过Mesh的BVH进行层次性剔除，然后使用生产者-消费者模式剔除队列中的Node。 线程从FIFO任务队列总取Node进行剔除，通过剔除的节点的四个子节点加入到队列的末尾。 线程即作为生成，也作为消费者。 最后的叶节点如果也通过了的话，加入到Cluster List中。
  - 光栅化:
    - 硬件光栅化: 大三角形和非Nanite Mesh。
      > 小三角形容易造成Quad Overdraw？
    - 软件光栅化： 小三角形使用Compute Shader写成的软光栅化； 每个Cluster对应一个线程组， 先算出所有顶点的剪裁空间坐标存入Shared Memory中。 然后每个线程读取对应三角形的Index Buffer和变换后的Vertex Position，根据Vertex Position计算出三角形的边，执行背面剔除和小三角形（小于一个像素）剔除，然后利用原子操作完成Z-Test，并将数据写进Visibility Buffer。
      > Nanite Mesh 不支持顶点位置会发生变化，带有Mask的Mesh
  - EmitDepthTargets: 
    - Nanite Mask: 计算当前像素是否是Nanite Mesh
    - Scene Depth Buffer: 根据Nanite Mask将Visible buffer 写入场景的 depth buffer中
    - Stencil Buffer： Nanite MESH是否接受贴花？
    - Emit Material Depth: 本质是Material ID Buffer， 但不是UINT整数型贴图，而是使用D32S8的深度图格式。 为了方便后续使用Z Test Equal来筛选材质， Stencil Test 来筛选Nanite Mesh。
      - Deferred Material (Deferred Texture): 延迟渲染时，因为只能渲染一次，使用统一的Shader 程序。 一些复杂的效果只能单独渲染该Mesh或重复调用该材质的shader进行全屏渲染。  Deferred Material 将材质分类，找出每个材质对应的像素进行着色计算。
        - Material Culling： 
          - 将屏幕划分为8*8的Tile。 ~~统计每个Tile包含的MaterialID， 将ID的最大最小值作为Material ID Range存入一张R32G32UINT图中。~~ 记录各个材质对应的Tile 列表。 （避免出现大量空转的Wrap）
          - 逐材质绘制其Tile列表的各个Tile，并使用 Material Depth剔除无效像素，只对有效像素进行着色。
  - 使用Visibility Buffer记录当前像素实际使用的顶点，深度，材质数据，避免G-Buffer的带宽消耗。
    - 维护一个全局的顶点数据和材质贴图表：着色计算时，从顶点数据中根据InstanceID和VertexID获取顶点属性。使用Barycentric Coord 插值顶点属性。 使用materialID获取材质信息。

Mesh Shader: 在硬件层面实现Visible Buffer的计算？ 替代了“Input Assembler → Vertex Shader → Hull Shader → Tessellator → Domain Shader → Geometry Shader ”
- 输入: 自定义结构，不一定是顶点缓冲； 
- 处理: 一次处理一个小网格块（Meshlet）
- 输出: 直接输出最终图元

TBDR
- Procedure: 分为 Tile Phase 和 Render Phase两个阶段
  - Tile Phase: 
    - 将当前ViewPort划分为多个Tile，
    - 执行VertexShader，三角形都变换到屏幕空间 
    - 计算Primitive会影响到哪些Tile，并将结果存储到主存上去(储存到Tiled Vertex Buffer中，alias Intermediate store， Frame Data， parameter buffer， Geometry Work Set)。 
    > PowerVR中, parameter buffer中好像分两块， Primitive List（Tile List / Display List） 和 Vertex Data。 Primitive List 只存Index，不存实际的Position，以节省带宽，需要后续FS中再算。
  - Render Phase:
    - Load Action: 初始化Tile memory，决定是否从VRAM加载Frame Buffer数据到Tile Memory上。 
      - Load： 从VRAM加载 frame buffer的数据。 适用于只绘制部分像素时。
      - Clear: 不加载VRAM，初始化Tile memory时，适用Clear value处理所有的像素。
        > **fast clear**： 使用硬件预设的清楚值，比一般的LoadAction.Clear快？
      - Dont Care： 不加载VRAM， 初始化Tile memory时不做任何操作。 适用于绘制全屏像素时。
        > Tile memory 可能还存在 Depth/Stencil buffer，以及用于HSR测试的“可见性列表”
    - Rasetrize: 
      - 光栅化（开始时硬件会从Tiled Vertex Buffer加载对应的顶点数据？）
    - Shader: 着色计算
    - Store：决定是否将frame buffer数据从Tile写回VRAM中。
      - Dont care： 不写回VRAM
      - Store： 写回VRAM. 如果时MSAA图，写回的是未经过Resolve的MSAA图
      - Resolve（MultisampleResolve）: 进行Resolve操作，写回Resolve后的图到VRAM.
      - StoreAndResolve（storeAndMultisampleResolve）: 写回未经过Resolve的MSAA图, 和写回Resolve后的非MSAA图到VRAM
        > 就两种操作，需要指定一张“普通的、非 MSAA 的纹理”来接收Resolve后的结果。
        > IOS上好像针对MSAA, Depth, Stencil有优化? 设置为MTLStorageMode.Memoryless时,不存在未经过Resolve的MSAA图. 采样点在Tile中确定, resolve后写回非MSAA图到VRAM. Depth/Stencil也可只存在于tile上?
      > - **transaction elimination** （ARM）: 比较该Tile前一次和本次的渲染结果的 **循环冗余校验（CRC值）**, 判断Tile是否发生变化。如果相同，则认为二者没变化，不执行Stroe操作，将framebuffer写回VRAM。
      
![20260319155219](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20260319155219.png)
- Resource Storage Mode： 内存中的对象的被CPU和GPU的访问模式，通常有Shared，Private，Memoryless三种
  - Shared - CPU 和GPU都可以访问，这类资源通常由CPU创建并更新。
  - Private - 存在SystemMemory上，只有GPU可以访问，通常用于绘制 render targets,Compute Shader存储中间结果或者 texture streaming.
  - Memotyless - 存在TileMemory， 只有当前Tile可以访问，用完就会被刷新掉，比如Depth/Stencil Buffer 在 iOS 上对于所有的不需要 resolve 的 rt（或 store action 设置为 don’t care）都应该设置为 memoryless，比如上面说到的Depth和Stencil。
- Deferred： 隐面消除 (Hidden Surface Removal)
  - 1： VS后不立刻进行着色计算，而是分Tile（Binning/Tilling）
  - 2: **HSR** (Hidden Surface Removal)（仅限于**PowerVR**和**Apple Silicon**）: 光栅后，着色前，在单个像素上根据深度判断要使用哪个片元来绘制，只将最近的片元传递去着色。 如果遇到了需要AlphaTest或AlphaBlend，则停止判断逻辑，先着色计算当前片元，完成混合/Test再继续排序(如果Test没被剔除就更新深度缓冲，被剔除了就不更新)。
    > 相较于Early-Z: HSR的颗粒度是逐像素的， Early-Z是颗粒度是逐物体的，如果两个物体出现穿插就无法避免Overdraw。因为Early-Z 如果出现先绘制远的片元，再绘制近的，远的片元的深度无法把近的剔除，因此近的还是会绘制。Early-Z 虽然也是根据深度buffer来判断，但因为依赖要将片元从近到远绘制，但CPU只能逐物体排序，不能逐片元，因此Early-Z的颗粒度是逐物体的。 二者同样都会被Alpha Test打断。
    > HSR的缺点，因为判断片元依赖于所有片元的深度信息，需要等tile上所有的片元都完成了光栅化才能进行处理。
    > Early-Z 和 HSR冲突吗： 本身Early-Z 与HSR不冲突，Early-Z只是用当前的Depth buffer进行比较。可以先Early-Z 再 HSR。 但如果在Eearly-Z之前已经进行了Z-Prepass绘制好了depth buffer，此时Early-Z比较的depth buffer就是已经是最优情况下了的。 （个人认为如果不是场景中要进行Alpha Test的像素很多的话，不太需要用Z-Prepass）
  - **FPK**（Forward Pixel Kill）（**Mali** GPU）： 光栅后，着色前，通过了Early-Z的片元先进一个FIFO队列，如果有相同位置的片元进入队列，则抛弃前一个片元（因为后进入FIFO的，是刚经过EZ test 并且通过的）。
    > FIFO队列的深度是有限的，如果一个Tile中的片元较多的话，可能出现队列满了，不得不把最前的出队进行渲染的问题。 
    - 四代以前：Mali GPU的顶点着色也是分两次的，第一次只计算位置相关的，用于分块操作。 第二次在Binning pass阶段会计算所有非位置的顶点属性（如纹理坐标、法线、颜色等）。 最后将这些 VS Output 写入系统内存。
    - 第五代加个开始引入了延迟顶点着色（Deferred Vertex Shading, **DVS**）: Binnings pass中只处理位置相关的顶点着色，然后将分块的图元列表写入系统内存，但不写入VS Output。 Rendering pass 阶段，Tile读取对应的图元列表和原始顶点数据，重新执行 Varying Shading，将计算出的顶点属性直接存储在片上高速缓存中。![20260320104702](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20260320104702.png)
  - **LRZ pass**（Low Resolution Z pass pass）（**高通** Adreno）： 在分块阶段（binning pass），会构建一个低分辨率深度缓冲区（Z-buffer），该缓冲区可对 “LRZ 分块（LRZ-tile）” 范围内的渲染贡献进行剔除，从而提升分块阶段的性能。随后在渲染阶段（rendering pass），会先利用这个低分辨率深度缓冲区高效剔除像素，再与全分辨率深度缓冲区进行深度测试。片元着色器直接从片上缓存读取顶点属性进行插值，无需从系统内存读取。![20260327153616](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20260327153616.png)
    - 关于VS中LRZ的影响, VS阶段只读取顶点的位置数据进行着色,用于深度(可见性)测试（将三角形的最小深度与LRZ图进行判断）? 完成测试后, 将可见三角形分配到相应的图块列表（Visibility table）. 每个图块包含哪些图元的索引信息和每个图元的可见性. 后续还需在FS阶段对可见片元正式进行完整的顶点着色计算。 虽然 VS 执行了两次，但宁愿多算一次顶点（计算成本相对较低），也要避免将 VS Output 写入系统内存再读回来（带宽成本极高）。 https://blogs.igalia.com/siglesias/2021/04/19/low-resolution-z-buffer-support-on-turnip/
    > 相较于HSR, FPK, LRZ还多了在Binning阶段的优化，LRZ构建的低分辨率depth buffer（LRZ buffer）在Binning阶段和Rendering阶段的着色前都会进行根据LRZ buffer进行深度测试。
    > 要求着色时不写深度，不含Discrad 操作。
    > 切换深度写入的方向，手动写入深度，混合或逻辑操作，向SSBO，Image写入数据，和Discard都会使LRZ 失效或暂时失效。 https://blogs.igalia.com/dpiliaiev/adreno-lrz/
  > UAV/SSBO的写入也会导致HSR,FPK, LRZ操作失效？因为UAV / SSBO不一定在Tile上，而且无法保证需要被剔除片元的着色数据是否需要写回。 但是可以强制开启Early-Z？ https://www.zhihu.com/search?type=content&q=LRZ%20pass
  - Nvidia的硬件光栅化(Rasterization): 一个三角形通常会经历两个阶段的光栅化：Coarse Raster和Fine Raster
    - Coarse Raster: 以单个三角形作为输入，分为若干个8*8像素的块。然后使用低分辨率的Z-Buffer进行遮挡剔除（Z-Cull）。
    - Fine Raster： 进行Early-Z, 输出2*2像素的Quad。
- **Flex Render**（**高通** Adreno）：智能的在某些时候将渲染流程由TBR切换为IMR. （比如render target足够小时？） 可以省下Binning的操作. 
- VS Output：
  - Adreno 架构下，Binning Pass 之后只产出两种数据并会将其写到 system memory：Primitive List 和 Primitive Visibility。在 Rendering Pass 会重新执行一遍 VS，产出 VS Output。这些数据不回写回 system memory，而是存在 On-Chip Memory (LocalBuffer)，PS 阶段直接可以从 Local Buffer 读取。
  - Mali：
    - 四代以前：Mali GPU的顶点着色也是分两次的，第一次只计算位置相关的，用于分块操作。 第二次在Binning pass阶段会计算所有非位置的顶点属性（如纹理坐标、法线、颜色等）。 最后将这些 VS Output 写入系统内存。
    - 第五代加个开始引入了延迟顶点着色（Deferred Vertex Shading, **DVS**）: Binnings pass中只处理位置相关的顶点着色，然后将分块的图元列表写入系统内存，但不写入VS Output。 Rendering pass 阶段，Tile读取对应的图元列表和原始顶点数据，重新执行 Varying Shading，将计算出的顶点属性直接存储在片上高速缓存中。![20260320104702](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20260320104702.png)
  - Unity: 在VertexData中将数据分为两份Stream，一份只有Position通道数据。一份为剩下的。 如果是SkinnedMesh，前一份数据还需要Normal 和 tangent？
  - MSAA: TBR 架构中，跨 tile 的三角形在每个 tile 中都会被执行。如果开启了 MSAA，那么 tile size 就会变小，那么跨 tile 的三角形数量就会变多，vs 压力会变得更大。
- Shader Divergency:
  - Branching:
    - 常量条件：这种在编译时就会被优化掉，是不会产生分支的。
    - Uniform 作为条件：如果同个 wave 中的所有 fiber 都是走同一个条件分支，理应也是不会产生分支的。
    - 运行时的变量决定: 需要分别跑不同的分支。 跑分支1时，分支2的fiber会处于闲置的状态。
  - Quad Overdraw: SP（streaming processors） 在PS进行着色计算时，是以Quad (2\*2的像素块)为单位的。![20260327160556](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20260327160556.png)。 如果在一个Quad中当前三角形不能占据全部的像素，就会出现Quad Overdraw。 这部分没被三角形覆盖的像素一般叫做辅助通道（Helper Lanes / Helper Pixels）， 虽然不会写入color buffer，但还是会进行着色计算。
    - Adreno上一个Wave（Wavefront/Warp）上虽然有128个fiber（Thread/Lane），但因为一个Wave上最多加载四个三角形对应的像素，最极端的情况下，四个三角形只对应四个有效像素，那么128个FIber就只画4个像素，闲置了124个。
  - Z-test Divergency： 片元经过着色计算后没有通过深度测试，未写入Color buffer。
  > 检测Divergency 的方法： Divergency 比例 = % Time ALUs Working - % Shader ALU Capacity Utilized;
      % Time ALUs Working： 当着色器忙碌时，ALU 工作的时间百分比。
      % Shader ALU Capacity Utilized： 在 ALU 工作的那些周期里， 有效工作的比例。
      其中前者统计的是被Wrap分配走的Fiber，而后者只考虑有有效产出的Fiber。 如果出现前者高，后者低，说明出现了Divergency，Wrap中很多Fiber在闲置或做无用功。
- Shader Complexity:
  - instruction count: 
    - 静态指令数（static instruction count）: 编译后的 shader 程序里的指令数量；
    - 动态指令数（dynamic instruction count）: 被执行的指令数量。（如循环产生的指令）
    > Mali Offline Compiler统计的是静态指令数。 
    > 统计指令过多的指标： % Instruction Cache Miss （印象里Branching也会导致指令变多？）
  - Register： 
    - 延迟隐藏 (Latency Hiding): 当前一个shader任务（Wave）在等待数据时，SP可以通过**上下文切换（Context Switch）**立即转去执行另一个 Shader任务（Wave） 的计算任务。 但如果Register的数量不够同时容纳两个的Shader的上下文，会导致切换失败。SP只能等待。
    - 寄存器溢出（Register Spilling）: 当占用寄存器数量继续增大，大于 on-chip memory 的尺寸时, 只能把上下文数据存入System Memory。
    > 检测Register的方法： 
        Mali Offline Compiler中register footprint per shader instance 来看 shader 寄存器的使用数量。
        在 Snapdragon Profiler 中可以通过 % Shaders Stalled 来判断 shader 的执行效率。当 SP 无法切换到其他 shader 去执行时，就会出现 stall.
        查看IPC（instruction per cycle）是否过低。
    - 如何优化寄存器:
      - 优化指令，减少临时寄存器的使用： MAD
      - 控制变量的作用域: 要用才定义，使用逻辑分段。
      - 向量的拆分与重组： 将变量尽量合到一个向量中。 多用向量运算。
      - 多用内置函数。
      - 减少分支； 慎用unroll
      - 使用half代替float
      - 延迟采样，不用一口气采样所有贴图。
      - 只用一次的量可以当作Uniform，避免占用通用寄存器。
  - Wave优化：
    - 消除 Wave 发散 (Divergence) -> 
      - Branching
      - Overdraw
    - 提升 Occupancy（占用率）: GPU 能够同时并发运行的 Wave 数量
      - 减少寄存器使用
      - 优化Group Size
      - 优化Shared memory
    - Wave Intrinsics： 允许 Wave 内部的线程直接交换数据，而无需经过内存或 Shared Memory，这极大地降低了延迟和寄存器压力。 -> 避免造成读写冲突的原子操作？
    - 避免全局同步和等待：
      - 渲染状态同步
      - Texture Bounds
      - Instruction Bounds
      - Register Bounds

- MAD / FMA 指令:
  - 可以在一个clock cycle完成以下计算：res=a×b+c
  - 优点：
    - 优化指令数： MUL + ADD -> MAD
    - 减少寄存器占用： 减少了使用临时寄存器记录加法结果的结果。
  - 操作： 
    - 显式改写代数式
    - 善用单分量缩放与偏移 (Scale & Bias)
    - 避免过度使用括号， 常量合并。


- RT Compression: 
  - UBWC: 

- 参考资料:
  https://zhuanlan.zhihu.com/p/407976368
  https://www.zhihu.com/search?type=content&q=LRZ%20pass
  https://zhuanlan.zhihu.com/p/112120206
  https://www.zhihu.com/question/425740956
  https://zhuanlan.zhihu.com/p/363027882
  https://developer.apple.com/documentation/Metal/improving-edge-rendering-quality-with-multisample-antialiasing-msaa
  https://zhuanlan.zhihu.com/p/1928114739189375482
  https://www.zhihu.com/question/427803115
  https://zhuanlan.zhihu.com/p/1923685725662081189
  https://zhuanlan.zhihu.com/p/2759747438