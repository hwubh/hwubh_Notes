阴影相机视锥的生成： 
- 确定阴影相机的远近平面时要考虑"潜在投射者", 拉远近平面保证其被囊括在内。 -> 远近平面差距过大时，受限于像素本身的精度（16bit/24bit），容易出现深度冲突（Z-Fighting）和阴影粉刺（Shadow Acne）。 
- Shadow Acne的处理方式：
  - Depth / Normal Bias:
  - 坡度偏移 (Slope-Scaled Bias): 根据表面与光线的夹角动态调整偏移量。表面越斜，偏移量越大。
  - 软阴影：
    - PCF:每个采样点先做深度比较，再对比较结果加权平均。
    - PCSS：用平均遮挡深度和接收点深度估算半影宽度，再用这个宽度决定 PCF 核的大小。
    - VSM: 分别过滤 z 和 z²，得到一阶矩 E[z] 和二阶矩 E[z²]。
    - AVSM： 	每个 Shadow Map 像素存一条压缩的深度–透射率曲线 T(z)
    - MSM： 	经典四矩 MSM 分别过滤 z、`z²`、`z³`、`z⁴`。
    - ESM: 	过滤指数变换后的深度
- Virtual Shadow Maps
  - Sliding Window： 复用上一帧已渲染的，只更新当前新的阴影内容 New Page。
  - Wraparound / Toroidal Addressing： https://developer.nvidia.com/gpugems/gpugems2/part-i-geometric-complexity/chapter-2-terrain-rendering-using-gpu-based-geometry
  记录相机矩阵的偏移量。将New Page记录到不需要的Page上，因为采样时New Page的坐标相较于原点来说是大于阴影贴图的覆盖的，可以取余（mod）阴影贴图的尺寸来得到正常的坐标。 -> 由于阴影贴图上是空间不连续的，没法使用硬件插值。![20260417164342](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20260417164342.png)
    - 独立级联：对于不同精度要求可以通过划分不同的级联来改善。 不同的级联使用各自的Pagemap，其大小相同，对应到各自的Physical Texture上（或Texture Atlas上相同大小的块）。
    - Pagemap Mipmap: 
      - ![20260512165138](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20260512165138.png)
      - 虚拟贴图被分割成了大小相等的Page。虚拟贴图和索引贴图都是按照mipmap组织的。物理缓存没有mipmap层级，它会在不同的page中存储不同级别的mipmap贴图。
    - HPT(Hierarchical Page Table)： 
      - 独立级联和Page Mipmap都有一个问题，如果VT要表示的范围很大，Mip0的Pagemap的尺寸会过大。
      - 思路类似四叉树： 父Page被四个低层级的子Page指着。不同层级的Page使用的Slot大小是一致的。
      - Pagemap上像素存的信息：
        - 物理页地址（Physical Page Address） -> PageOffset，记录的对应physical texture上Slot的索引。
        - 层级标记位（Flags / Page State）：
          - Mapped/Valid Bit：该页是否已经在物理内存中渲染完成。
          - Is-Leaf Bit：标记这是树的叶子节点（最高精度），还是可以进一步细分的父节点。 -> 如果存在子page，还存有Child Table Offset（去哪找下一级页表条目）。
          - Dirty/Invalid Bit：该页是否需要被重新渲染（比如因为物体移动）。
          - Mipmap层级 ?
        - 回退索引（Fallback / Parent Link）： 如果当前页还没渲染好，它会包含一个指向其父级（更粗糙层级）物理页的索引，确保采样时有“保底”阴影。 -> 不是存在像素里的，而是shader中的临时变量？
      - 如何得到实际采样UV：
        - Page 索引 (Pindex​)：floor(Virtual_UV / SlotSize)。这决定了你去 Page Table 的哪个位置查表。  -> 在Pagemap上采样的UV。 ->采样后得到对应的Slot在Physical Texture的位置。
        - 页内偏移 (Ointernal​)：fract(Virtual_UV / SlotSize)（范围 0.0∼1.0）。这决定了你在那一页里面的相对位置。
        - 如果出现了回退 -> 将子级计算的 Ointernal​ 缩放一半，然后根据子级在父级的位置，决定是否在XY方向加0.5
        - 将（Ointernal + SlotID）​ * SlotSize * $\frac{1}{PhysicalTexSize}$ 才能得到实际采样Physical Texture的坐标。
      - 寻址查询：
        - 从下至上的： 先根据Z值或ddx，ddy确定要采样的层级。 -> 因此需要维护一张 层级元数据表 (Level Metadata Table)。 记录各级pagemap在四叉树buffer的初始位置和SlotSize的大小。 根据Page 索引 (Pindex​)计算Hash得到Hash表上的在这一层的Offset。
        - 从上至下的： 根据Child Table Offset。
    - HPT 相较于 独立级联的好处：
      - 不会用重复绘制的区域，独立级联需要保持覆盖底层级级联已经绘制的区域。
      - HPT不要钱不同精度要求的区域是空间连续的。
      - 可以支持的层级数量多。
      - 自带fallback -> 优先加载或者缓存？ 
        > 其实如果子page都加载好了，父page对应slot可以被优先释放？
    - 缺点：
      - 逻辑较为复杂，Physical Texture之间不是空间连续的，不能使用硬件双线性插值？ -》 可以各个Slot单独加个Padding？
      - Pagemap 使用 Texture2DArray 或者一个巨大的 Linked List / Buffer 保留。 需要进行寻址查询。
  - UE VSM:
    - shadowmap资源管理：
      - 平行光使用Clipmap； 
      - 聚光源使用带有Mipmap chain的VSM； 
      - 点光源六个面各自对应一张VSM。
    - 流程：
      - Page收集：
        - 根据像素与相机之间的距离来计算合适的Clipmap层级。
        - 根据像素的光源空间位置，计算在VT上的UV，所在的Page。
      - 阴影绘制：走Nanite的流程，尽可能减少渲染的片元数量。
        - Intance cull： 每一级VT都需要进行剔除判断，然后记录该instance 在哪几个视锥体上存在。
        - Cluster cull: 根据instance所在的clipmap级别，选取对应级别的ClusterLOD。然后进行cluster剔除。
        - 光栅化（后）计算各个三角形读取 indirection table（页表）找到physical texture位置，更新深度。
      - Page处理: 每帧在收集到屏幕上像素所需的所有Page之后，这些Page大致分以下几种情况：
        - 新出现的Page，需要为其分配物理空间并绘制
        - 已经缓存了的Page，但其上物体发生变化，需要重新绘制
        - 已经缓存了的Page且其上物体没有变化，不需要绘制
        - 与instance/cluster与需要更新的page进行覆盖测试时，使用Page的HIZ优化计算效率，HIZ只存Flags信息（标注是否需要更新）。
      - LRU Page： 分配新page，先取空的，如果没有空的，取LRU Page。 RequestPage不能用（正被使用的page）。
      - Clipmap更新： 相机移动时，Clipmap也会跟着移。Clipmap移动后，page需要更新其在PageTable上的位置，以保持PageTable的空间连续性。
      - 动静分离： 静态物体阴影和动态物体阴影分开绘制，阴影绘制完成后将静态物体阴影叠加到动态物体阴影图上。优势是更新阴影时只需要
    - 平行光： 
      - 最多分为22级Mipmap。
      - 投影矩阵： 覆盖直径作为平行投影的 width、height，半径的一半作为矩阵的 z near，z far 则固定为 0.5。
      - 对齐ViewTarget： ViewTarget移动了一个Clipmap的半径才进行移动阴影相机。 以ViewTarget为参考确定Clipmap的中心点，但只以能以一个Clipmap半径调整中心点。 这样保证了每个page分配的阴影像素数量是恒定的，避免抖动？ 最重要的是为了方便计算，通过世界点减去中心点，然后除以固定的Page尺寸就得得到相对的逻辑PageOffset。
      - 所以VSM共用一个Page Table: 总数为21845；（最细的一级为128*128）
        - Page Table：
          记录Physical Texture上的Page Offset； 
          当前指向的阴影的LOD级别与需求的差别等级（0为当前级，X>0 为来自上X级）;
          bAnyLODValid数据是否有效。
      - Nanite Shadow:
        - Instance Culling: 
          - 对Instance的包围进行视锥剔除； 
          - 投影是否覆盖到阴影贴图的任何像素中心；
          - 如果覆盖的Page 都已经Cache，跳过后续。
        - Cluster Culling： 
          - 当前Node是否能经过剔除；
          - 当前Node覆盖的Page是否已经Cache；
        - 结果写入VSM专用的Visible buffer，因为需要储存Page索引，会比一般的Visible buffer多使用32位。
      - Virtual Address Translation: 从Vuv 到 Puv的调整方式。
        - 软光栅：
          - Per Pixel:  没有 vPage 字段; Rasterization 最终写入像素时（WritePixel 函数）通过 VirtualToPhysicalTexel 函数将虚拟地址转换为物理地址，再将光栅化的 Depth 写入到 Physical Page 中
          - Per Page: 有 vPage 字段; 用vPage从 Pagemap上查表。
        - 硬光栅: 一次渲染16个Page，为了并行考虑； 可能导致一个Cluster被多个窗体提交，光栅化，导致浪费。
多层纹理混合地面使用 ID Tex控制地貌，因为ID Tex本身精度问题导致的Block Artifacts怎么处理：
- 添加细节： 顶点偏移，高度图，贴图
- 噪声，抖动，多线性插值
- ID tex也做成VT.

悬崖（XY方向的投影地形）: 
- FryCry5： tri plane projection，如有必要的话做blend来处理。 使用随机混合来降低使用不同tiling系数的sample次数。"stochastic cliff shading": 使用有噪点的alpha test来替代alpha blend，本质上是一个screen door effect

抖动：
- 考虑用时域的方式来压低？

# Screen Space shadowmap: https://zhuanlan.zhihu.com/p/45679584
- 使用屏幕空间下的shadowmap替代光源空间下的，多用于延迟渲染。
- 屏幕空间做一次阴影收集计算（Shadows Collector）: 得到一张屏幕空间阴影纹理（只记录了比较结果）. 通过间深度图中深度图变换到光源空间中，采样shadowmap进行比较，将结果写入屏幕空间阴影贴图中。
- pros: 
  - 不需要用cascade shadow的？不用binding太多shadowmap 纹理？
  - PCF这些可以每个像素只跑一次？

# Contact Shadow:  https://zhuanlan.zhihu.com/p/357280024
采样深度图，往光线反方向做ray marching，判断是否有接触。
- 问题： 步长是一个固定值，它会跳过一些刚好在屏幕空间上很细的薄物体，导致产生锯齿、片状等问题
  - 引入抖动，改变步长 -> 产生噪音。
    - 使用TAA， 改善噪音。
    - 基于深度差的联合双边滤波方案: 
- 处理漏光
- 增加细节阴影。其实有点像是SSAO了？

# 原神中优化阴影的一些技巧： https://zhuanlan.zhihu.com/p/56317663
- 使用Poisson disc（泊松分布算法）的soft shadow（软阴影）
- 分为8级cascade，后面四级不每帧更新
- 1/4 * 1/4 大小的Mask标注半影区域，只有半影区域做软阴影。 通过在4*4 coarse pixel中选取几个点判断是否有阴影。Mask会有模糊以弥补判断半阴影时欠采样的问题。
- Local light shadow:
  - offline 生成 全场景的静态local light shadowmap，并进行压缩
  - 运行时使用compute shader进行解压
  - 压缩方式: 1024*1024 shadowmap分为32\*32个tile，每个tile分为16\*16个2\*2的coarse pxiel。 
    - 每个2\*2块如果是落在同一平面上，则可以使用深度平面方程\[z(x,y)=ax+by+c\]表示。
    - 每个2\*2块如果被不同平面分割，则使用压缩浮点数的方式压缩四个深度值。
    - 每个Tile使用Quad tile，合并同一平面上的coarse pixel（可以共用深度平面方程？）
    - https://zhuanlan.zhihu.com/p/388563845 中还将空白tile剔除掉了，后续解压的shadowmap打成了图集。

# Distance Field Shadow https://zhuanlan.zhihu.com/p/683779588
先生成一张光空间深度图，而是从当前像素出发，沿光线方向做一段追踪，靠距离场数据判断中途是否撞上遮挡物。
- 根据SDF确定步进长度
  > SDF(P) 表示点P离最近的三角形的距离。
- 沿向光源方向进行步进，到$P_0$. $SDF(P_0)$ 得到 $P_0$ 的**安全距离**。根据安全距离，原点P 计算得到**安全角度** $arcsin(\frac{SDF(P_0)}{(P_0) - (P)})$.
  >  $arcsin(\frac{SDF(P_0)}{(P_0) - (P)})$ 可以约分为 $min(\frac{k*SDF(P_0)}{(P_0) - (P)}, 1.0)$ k为手动调整的系数
- 根据步进路上最小的安全角度判断阴影可见度，实现软阴影。
- pros:
  - DFSS的速度和效果是要好于多级的级联阴影贴图（如果只想用1级，那还是级联阴影贴图速度快）
  - 完全没有shadow acne和peter panning的问题
- cons: 
  - SDF的数据需要占据一定的内存
  - 如果动态物体也使用SDF的话，需要一定开销来组织数据（动态添加和剔除）
- UE优化:
  - 不会对整个场景生成SDF，而只是为单个模型生成SDF(纹理大小控制在128 x 128 x 128)。全局距离场使用多个 clipmap 体积纹理。
  - 渲染时将像素点的世界位置与灯光连线，找到和哪些物体发生碰撞，然后再依次读取这些物体的SDF
  - 静态物体和动态物体的SDF分开管理（但最终还是整合到一个3D纹理中），以减少动态物体的开销. 只更新动态物体自身的SDF 和 全局SDF中动态物体移动前后位置的SDF。

# Dual Paraboloid Shadow Maps(DPSM): https://zhuanlan.zhihu.com/p/55218272
- 双抛物面：把每个半球的方向，通过非线性映射压到一个圆盘，两个圆盘分别存进两张 2D shadow map。
- 使用时在shader中映射回来。
> 有点像是画地图？ 
---------------
- https://zhuanlan.zhihu.com/p/1986214133645984827
- https://zhuanlan.zhihu.com/p/2030698173631554239
- https://zhuanlan.zhihu.com/p/15560088727
- https://zhuanlan.zhihu.com/p/388563845
- https://zhuanlan.zhihu.com/p/630697111
- https://zhuanlan.zhihu.com/p/147895507