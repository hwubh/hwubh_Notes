GPU Architecture：
- VRAM与System memory共用物理内存。 
- 每个**GPU Core**(逻辑上也叫 **Shader Core**)上封装了一块**Tile Memory** (Alias **Tile Buffer**, **on-chip framebuffer**), 大小不固定，最小可能就16\*16的像素。 除此之外还有ALU，Regiser File，L1 Cache，Texture Unit。
- 各个GPU Core之间还有共享的L2 cache。
- Apple Silicon和现代的骁龙（Snapdragon）和天玑（Dimensity）芯片也还存在可供CPU, GPU, NPU访问的**SLC (System Level Cache)**介于 L2 cache和System memory之间。
  > Apple 叫 **GPU Last Level Cache**
- Binding Mode: OpenGL 和早期DX上限制了Shader可以访问的贴图数量(对应有几个槽位Slot). CPU 在渲染前，必须显示调用指令"BindTexture(MyTexture, Slot 0)". Shader中写死：layout(binding = 0) sampler2D myTex;。 Slot不够时可以使用VT方案或Texture Atlas, TextureArray来节省Slot.

TBDR
- Procedure: 分为 Tile Phase 和 Render Phase两个阶段
  - Tile Phase: 
    - 将当前ViewPort划分为多个Tile，
    - 执行VertexShader，三角形都变换到屏幕空间 
    - 计算Primitive会影响到哪些Tile，并将结果存储到主存上去(储存到Tiled Vertex Buffer中，alias Intermediate store， Frame Data， parameter buffer， Geometry Work Set)。
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
- Deferred：
  - 1： VS后不立刻进行着色计算，而是分Tile（Binning/Tilling）
  - 2: **HSR** (Hidden Surface Removal)（仅限于**PowerVR**和**Apple Silicon**）: 光栅后，着色前，在单个像素上根据深度判断要使用哪个片元来绘制，只将最近的片元传递去着色。 如果遇到了需要AlphaTest或AlphaBlend，则停止判断逻辑，先着色计算当前片元，完成混合/Test再继续排序(如果Test没被剔除就更新深度缓冲，被剔除了就不更新)。
    > 相较于Early-Z: HSR的颗粒度是逐像素的， Early-Z是颗粒度是逐物体的，如果两个物体出现穿插就无法避免Overdraw。因为Early-Z 如果出现先绘制远的片元，再绘制近的，远的片元的深度无法把近的剔除，因此近的还是会绘制。Early-Z 虽然也是根据深度buffer来判断，但因为依赖要将片元从近到远绘制，但CPU只能逐物体排序，不能逐片元，因此Early-Z的颗粒度是逐物体的。 二者同样都会被Alpha Test打断。
    > HSR的缺点，因为判断片元依赖于所有片元的深度信息，需要等tile上所有的片元都完成了光栅化才能进行处理。
    > Early-Z 和 HSR冲突吗： 本身Early-Z 与HSR不冲突，Early-Z只是用当前的Depth buffer进行比较。可以先Early-Z 再 HSR。 但如果在Eearly-Z之前已经进行了Z-Prepass绘制好了depth buffer，此时Early-Z比较的depth buffer就是已经是最优情况下了的。 （个人认为如果不是场景中要进行Alpha Test的像素很多的话，不太需要用Z-Prepass）
  - **FPK**（Forward Pixel Kill）（**Mali** GPU）： 光栅后，着色前，通过了Early-Z的片元先进一个FIFO队列，如果有相同位置的片元进入队列，则抛弃前一个片元（因为后进入FIFO的，是刚经过EZ test 并且通过的）。
    > FIFO队列的深度是有限的，如果一个Tile中的片元较多的话，可能出现队列满了，不得不把最前的出队进行渲染的问题。
    - 四代以前：Mali GPU的顶点着色也是分两次的，第一次只计算位置相关的，用于分块操作。 第二次在Binning pass阶段会计算所有非位置的顶点属性（如纹理坐标、法线、颜色等）。 最后将这些 VS Output 写入系统内存。
    - 第五代加个开始引入了延迟顶点着色（Deferred Vertex Shading, **DVS**）: Binnings pass中只处理位置相关的顶点着色，然后将分块的图元列表写入系统内存，但不写入VS Output。 Rendering pass 阶段，Tile读取对应的图元列表和原始顶点数据，重新执行 Varying Shading，将计算出的顶点属性直接存储在片上高速缓存中。
  - **LRZ pass**（Low Resolution Z pass pass）（**高通** Adreno）： 在分块阶段（binning pass），会构建一个低分辨率深度缓冲区（Z-buffer），该缓冲区可对 “LRZ 分块（LRZ-tile）” 范围内的渲染贡献进行剔除，从而提升分块阶段的性能。随后在渲染阶段（rendering pass），会先利用这个低分辨率深度缓冲区高效剔除像素，再与全分辨率深度缓冲区进行深度测试。片元着色器直接从片上缓存读取顶点属性进行插值，无需从系统内存读取。
    - 关于VS中LRZ的影响, VS阶段只读取顶点的位置数据进行着色,用于深度(可见性)测试? 完成测试后, 将可见片元分配到相应的图块列表. 每个图块包含哪些图元的索引信息和每个图元的可见性. 后续还需在FS阶段对可见片元正式进行完整的顶点着色计算。 虽然 VS 执行了两次，但宁愿多算一次顶点（计算成本相对较低），也要避免将 VS Output 写入系统内存再读回来（带宽成本极高）。
    > 相较于HSR, FPK, LRZ还多了在Binning阶段的优化，LRZ构建的低分辨率depth buffer（LRZ buffer）在Binning阶段和Rendering阶段的着色前都会进行根据LRZ buffer进行深度测试。
    > 要求着色时不写深度，不含Discrad 操作。
  > UAV/SSBO的写入也会导致HSR,FPK, LRZ操作失效？因为UAV / SSBO不一定在Tile上，而且无法保证需要被剔除片元的着色数据是否需要写回。 但是可以强制开启Early-Z？ https://www.zhihu.com/search?type=content&q=LRZ%20pass
- **Flex Render**（**高通** Adreno）：智能的在某些时候将渲染流程由TBR切换为IMR. （比如render target足够小时？） 可以省下Binning的操作. 

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