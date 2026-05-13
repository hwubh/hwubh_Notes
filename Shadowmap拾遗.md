阴影相机视锥的生成： 
- 确定阴影相机的远近平面时要考虑"潜在投射者", 拉远近平面保证其被囊括在内。 -> 远近平面差距过大时，受限于像素本身的精度（16bit/24bit），容易出现深度冲突（Z-Fighting）和阴影粉刺（Shadow Acne）。 
- Shadow Acne的处理方式：
  - Depth / Normal Bias:
  - 坡度偏移 (Slope-Scaled Bias): 根据表面与光线的夹角动态调整偏移量。表面越斜，偏移量越大。
  - 软阴影：
    - PCF:
    - PCSS
    - VSM
    - AVSM
    - MSM
    - ESM
- Virtual Shadow Maps
  - Sliding Window： 复用上一帧已渲染的，只更新当前新的阴影内容 New Page。
  - Wraparound / Toroidal Addressing： 记录相机矩阵的偏移量。将New Page记录到不需要的Page上，因为采样时New Page的坐标相较于原点来说是大于阴影贴图的覆盖的，可以取余（mod）阴影贴图的尺寸来得到正常的坐标。 -> 由于阴影贴图上是空间不连续的，没法使用硬件插值。![20260417164342](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20260417164342.png)
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
    - shadowmap资源管理：平行光使用Clipmap； 聚光源使用带有Mipmap chain的VSM； 点光源六个面各自对应一张VSM。
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