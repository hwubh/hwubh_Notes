https://zhuanlan.zhihu.com/p/2068657071654974329
https://unity.com/blog/engine-platform/new-ways-of-applying-global-illumination-in-unity-6
https://user.guancha.cn/main/content?id=1699844
https://zhuanlan.zhihu.com/p/210674643
https://advances.realtimerendering.com/s2024/content/Iwanicki/Advances_SIGGRAPH_2024_Neural_LightGrid.pdf
https://www.activision.com/cdn/research/Neural_Light_Grid.pdf
https://zhuanlan.zhihu.com/p/1910499429288513612
https://zhuanlan.zhihu.com/p/2013638441640941008
https://zhuanlan.zhihu.com/p/2012909669794988193
https://www.zhihu.com/question/2043732532387124859
https://zhuanlan.zhihu.com/p/1992568844586329068

预计算/烘焙型GI:
- 维度：
  - 缓存的光学量: \[光照结果=光的传输关系 *流明输入 \] 
    - 光传输结果: 光照结果.(可以是表面 irradiance，也可以是出射 radiance 或它的系数?)
    - 光传输过程: 光传输关系。(运行时计算“流明输入”，得到光照结果)$t_k(x)=\int_{S^2} V(x,\omega)\max(0,n(x)\cdot\omega)Y_k(\omega)\,d\omega$。包含遮挡性V，余弦权重max，球谐基函数K 
  - 缓存结构
    - 表面空间上的烘焙 ： 记录物体几何表面对应的烘培结果？
    - 世界空间布点的烘焙 : 	场景中的三维采样点. 根据被照亮位置，读取附近探针并插值
- 方案:
  - lightmap：通过网格自身记录的 Lightmap UV 找到烘培的光照结果。 如果是lightmap atlas， 需要考虑 scale/offset。 记录irradiance？
    - 方向性lightmap: 还需要记录方向信息.额外保存主光方向、多个方向上的光照分量，或其他方向编码。 例如烘培光照时表面的法线为n0,但运行时法线贴图改用了法线N1. 就需要根据主光方向等对烘培结果进行调整。让法线贴图的凹凸细节也能对烘焙光照产生明暗响应。
    > 为什么不烘培时就用法线贴图的法线呢？  低分辨率地保存光照的方向信息，运行时再结合高分辨率的法线贴图计算明暗。
  - Lightmass Volumetric Lightmap: 
    - 在需要预计算光照的区域内建立三维采样结构： Lightmass Importance Volume
    - 在静态几何附近提高采样密度: 光照往往变化快的地方，需要更密的采样
    - 求解各采样位置的光照:3 个频带、9 个系数/颜色通道的 SH 表示 + 天空遮蔽方向、静态生成的方向光阴影信息等辅助数据
    - 插值SH时，可以根据位置或者法线
  - light probe proxy volume （LPPV）：已有 Light Probe 数据 -> 在代理体积的网格位置插值 -> 存入可采样的三维纹理 -> 着色时按表面位置读取
  - Precomputed Radiance Transfer(PRT): 烘培的球谐函数系数，可以放在纹理，buffer，顶点属性中。
    - 表面PRT: 存储生成irradiance所需的传输系数。 多为 SH Transfer Vector
    - 空间PRT: SH Transfer Matrix
    > 漫反射表面通常能提前把方向积分掉，所以存向量；需要保留输出方向分布时，就存矩阵。? 
- 应用: 室外为了支持TOD，动态天气等效果，多烘培“光传输过程”，使用PRT方案。室内因为存在点光源/聚光灯等，多单独烘焙Lightmap或者Volumetric Lightmap。


屏幕空间类GI:
在depth buffer上做Linear Trace 或 Hi-Z/HZB Trace.
SSGI 流程:
- 像素根据深度重建表面位置:
- 在法线朝向的半球内选择几个方向：Sobol“随机化低差异序列”：保留样本均匀覆盖的组织方式，同时让不同像素或不同次运行得到不同的样本。
- 沿这些方向追踪，与屏幕深度图比较: 比较视空间深度。
- 命中其他表面后，读取其光照颜色
- 累积各方向的贡献，得到间接光，再进行降噪：因为只追踪少量光线(欠采样)，所以需要做降噪。具体来说是在时域/空域上做过滤，其中空间滤波不能简单混合，而是会根据相邻像素的离屏幕的距离(越远越小)，深度(深度差越大，权重越小)，法线角度（朝向越不一致，权重越小）调整混合的权重。
Linear Trace：沿射线逐步检查
Hi-Z / HZB Trace：用分层深度快速跳过区域
- 问题： 容易出现漏光、闪烁和视角依赖
> 判断是否相交时，可能要求通过“薄层测试”，即将深度图视为有厚度，完整跨过厚度才能确定相交了？

动态空间光照缓存类GI: 可以按照三个维度进行划分:
- 如何在空间上存储缓存: World-Space Probe、Screen-Space Probe、Surfel、Surface Atlas、Voxel
  - Surfel： 附带光照数据的小面元. 位于物体表面，带方向性和面积。 计算面元对另一个点的贡献时，面积、朝向和距离构成的立体角决定其权重值。
  - Surface atlas: 把场景物体分别展开投影成二维小图，再把这些小图拼进一张大纹理. 每个 texel 可以保存材质、法线，以及某种光照结果。
    - UV映射: 先查找命中点最适合的Card，然后将命中点转换到 Card 的局部空间，投影得到二维坐标，再映射到 Atlas 中的矩形区域
    > 动态lightmap一般指存储的是光照结果的？
    > surface atlas： 一般记录GI采样时需要的表面信息？ 材质、法线、深度？
  - Voxel 体素化: 记录“这个空间格子中的几何、遮挡和表面光照”。表面是嵌入三维空间中的二维曲面。体素化做的是:
在三维网格里，标记哪些格子被这个曲面穿过。 
    - 稀疏体素层次: 只为有需要的区域分配详细数据。如使用 Sparse Voxel Octree 进行细化。 -> 减少存储，加快查询。
      > 最小Voxel的尺寸为工程配置，存在场景表面的Voxel会继续细分到该配置的大小。  
    - 锥追踪: 锥追踪近似查询的是一个有角度宽度的方向区域。每次前进一步后，计算锥体直径，找到与直径匹配的体素LOD，使用轴线上的焦点进行采样，过滤后加入现有的光照结果中？
    - 将**锥追踪**和**稀疏体素层次**结合: 采样时根据截面大小选择体素层级：
      - 近处截面小，查高分辨率数据；
      - 远处截面大，查过滤后的低分辨率数据；
      - 沿途累积光照与遮挡，前面的不透明区域会衰减后面的贡献。
        >  不透明度：沿查询方向穿过该区域时，被挡住的程度。即使材质完全不透明，粗体素中的有效不透明度也可能小于 1，因为表面只覆盖了这个粗区域的一部分。
        > VCT发射的锥体方向一般有9个？： 从表面法线构建两个正交方向，然后1 个锥沿法线；4 个沿侧向 $(\pm0.707,0,\pm0.707), (0,\pm0.707,\pm0.707)$；4 个沿角向 $(\pm0.408,\pm0.408,0.816)$；
    > Volumetric lightmap 记录收到了什么光照。 Voxel Cone Tracing 强调向外采样，查询墙、桌子等区域的光照和遮挡，来估计“这里受到怎样的照明”。
- 缓存的光学量
  - Radiance : W/(m²·sr)， 记录在X位置上，光的方向，光的颜色，光的强度。
    > \[L(x,\omega)=(R,G,B)\] 
  - Irradiance: W/m², 记录在X位置上, 朝向n的表明接受到的，光的颜色，光的强度。 
    > Irradiance 本质为在朝向n的表面上，进行将Radiance(光*入射角余弦)进行半球积分后的结果。
- 计算方式（Final Gather）: 从空间缓存到像素之间的一个映射
  - World-Space Probe Gather：像素重建世界空间坐标，用周围的Probe插值。
  - Surfel Neighborhood Gather：像素重建世界空间坐标，用周围的Surfel插值。
  - Screen-Probe Gather：屏幕上放置Screen-Probe，Probe向多个方向发出射线采样，采样完的结果插值回像素。
  - Per-Pixel Raytrace Gather（Full/Half/Quater Res）：逐像素发射光线，命中Surface Cache，读取Surface Cache上存的光照。
  - 混合Final Gather方案。
- 从接受点出发的采样/查询光线，其方向可以由随机数、低差异序列等提供。 Lumen中则是结合 BRDF 和历史入射光照估计的 Product Importance Sampling，减少无用方向，把更多查询放到预期贡献大的方向。
- 
| 烘焙内容 ↓ / 空间载体 → | 表面空间：Mesh 顶点或 UV texel | 世界空间：三维采样点或探针 |
|---|---|---|
| **光照结果：固定照明下的 irradiance** | **Irradiance Lightmap / 顶点光照**。每个表面样本存该处法线接收到的 RGB 辐照度 \(E(x,n_x)\)。若不含材质反照率，着色时再乘 \(\rho/\pi\)。[UE 对 Surface Lightmap 的说明](https://dev.epicgames.com/documentation/en-us/unreal-engine/volumetric-lightmaps-in-unreal-engine) | **Irradiance Volume / Irradiance Probes**。探针表示“在这个位置，朝向 \(n\) 的表面能收到多少光” \(E(p,n)\)。动态角色按位置插值附近探针，再按自身法线求值。也可以存入射光的 SH，在求值时完成余弦卷积；UE Volumetric Lightmap 是这类工程实例。[UE Volumetric Lightmaps](https://dev.epicgames.com/documentation/en-us/unreal-engine/volumetric-lightmaps-in-unreal-engine) |
| **光照结果：固定照明下的 radiance** | **表面出射辐射度缓存**。存表面向外反射的 \(L_o(x,\omega_o)\)。理想漫反射表面可以只存一个 RGB；若出射光随方向变化，则需要方向系数或多方向样本。这是对数据含义的描述，各引擎未必给它同一个算法名。 | **Radiance Probes / 预计算 Light Field Probes**。存从探针位置沿各方向看到的入射辐射度 \(L_i(p,\omega)\)，例如方向纹理或 SH 系数。McGuire 等人的 Light Field Probes 还存可见性信息，支持更可靠的空间查询。[Precomputed Light Field Probes](https://research.nvidia.com/publication/2017-02_real-time-global-illumination-using-precomputed-light-field-probes) |
| **光传输关系：给定光源输入后才得到结果** | **表面 PRT（Precomputed Radiance Transfer）**。每个顶点或 texel 存传输向量 \(t_k(x)\)。运行时与当前环境光系数 \(\ell_k\) 点乘，得到该点的漫反射照明。若还要保留出射方向变化，可以使用传输矩阵。[Sloan 等人的 PRT 论文](https://www.microsoft.com/en-us/research/wp-content/uploads/2017/01/prt.pdf) | **PRT Volume / Transfer Probes**。每个空间探针存光源输入到该处局部光照的映射。一般形式是 \(\mathbf c_p=T_p\boldsymbol\ell\)，其中 \(\mathbf c_p\) 可以是当地入射光的方向系数。实际例子是《对马岛之魂》的世界空间探针，存天空可见性及其反弹响应，运行时结合天空和方向光求出环境漫反射。[原始 PRT 的空间传输矩阵](https://cseweb.ucsd.edu/~ravir/291/2015/papers/p527-sloan.pdf) · [Sucker Punch 工程说明](https://media.gdcvault.com/gdc2026/Slides/Wohllaib_Eric_Ambient_Diffuse_Lighting_in_Ghost_of_Yotei.pdf) |

Voxel/SDF/Sparse Voxel类GI:
Voxel类GI会将场景的几何，材质等信息离散到三维体素网格或者稀疏体素结构中，再把光照注入为Radiance，运行时执行Voxel Ray/Cone Tracing。
SDF主要提供Visibility和Hit Query，本身不一定存储光照，利用好SDF可以跳过过空的空间。

硬件Raytracing与采样复用型GI:
硬件Raytracing首先只是一种光线求交的Backend.

和“动态空间光照缓存类 GI”有一个实质区别：两者都做复用，但复用的对象不同。 光照缓存复用的是“某处已经算出的光照”，ReSTIR 复用的是“哪些光照样本值得拿来估计当前像素”。硬件光追又是另一层，它可以给这两种方案都提供求交能力。


# Reflective Shadow Map https://zhuanlan.zhihu.com/p/147895507
- 思路: 把“被灯光照亮的表面”当成一批小光源，再用这些小光源照亮其他物体，近似一次反弹的间接光。
- 做法：
  - 扩展Shadowmap，记录表面和光照信息： 表面位置，表面法线，反射光通量。每个有效 RSM texel 都代表一小块受光表面，可以转成一个 VPL（Virtual Point Light，虚拟点光源）。
  - 渲染接收点时，累加这些“小光源”的贡献。 不考虑第二段光路（反射点 -> 接受点） ，且只抽样一些texel，然后根据采样分布加权。


----------------------------------------------------
https://zhuanlan.zhihu.com/p/265463655
lightmap:
- UE4 lightmap: 方向性lightmap， 每个像素记录8个float系数，包含 3个RGB分量+Log光强度值(压缩用)+3个一阶球谐系数 + 一个亮度残差。
  > 为什么这里一阶球谐系数只需要3个float -> 因为RGB颜色被分离出来了, 即每个方向通道上颜色是一样的，球谐系数只表达方向性。此外0阶各个像素是相同的，作为常数。
  > 完整 RGB SH： 光照(N) = C0 + Cx·nx + Cy·ny + Cz·nz, C0、Cx、Cy、Cz 各是一个 float3.
  > 引文中的近似： 光照(N) = RGB × (a + b·nx + c·ny + d·nz). 、b、c、d 各是一个 float.
  Log的作用： 把亮度转换到对数空间再量化。 增加暗部的精度。
  残差：取对数改善了刻度的分布，但主通道仍然只能落在离散刻度上。 用残差来补充精度？
----------------------------------------------------
https://zhuanlan.zhihu.com/p/23410011

Irradiance Volume： 缓存内容: 光照结果（irradiance）; 缓存结果： 世界空间布点
- 空间分割--把空间分成规则的有限份，好以少量的数据表达出世界的样子.
  > 粗了的话，容易漏光；细了，数据量太大。 
  - editor自动在合适的地方分配probe
    - Unity APV?
  - 提供人为干预机制:
- 光照信息记录--记录每个空间分割中光照是什么样子的
  - 存储内容:
    - rgb颜色(6个方向的ambient颜色，一般叫ambient cube)，6个float3
    - SH 系数，二阶的球谐系数，9个float3（零阶部分为常数？）. 也可以使用压缩后的二阶球谐系数，也是通过提取颜色的方式。
  - 烘培方式: 
    - 每个probe做capture cubemap，然后downsample。
    - 每个probe做raytracing
    - reproject cube: 在场景的少量位置捕获 cubemap （要带深度信息），再用它们烘焙大量 irradiance probe。 第一次还是需要probe发射射线的，但是后续(光照变化的话)只需要更新cubemap即可。因为probe到cubemap的映射关系是固定的。
      - 从Probe发出射线，找到交点。
      - 获取交点在cubemap上的位置。
      - 用深度检测 cubemap 能不能看到交点。
  - 空间中的数据结构： 多使用Sparse Irradiance Octree (稀疏八叉树)
    - 第一级别的Probe信息，保存到一个3D纹理中
    - 对于每个被细分的节点Brick光照信息，并整排列放置到Atlas贴图中.
    - 需要一张查找表信息纹理，保存每个Brick的位置信息。查找表信息纹理，和世界坐标成线性映射的。如果当前的坐标在某个Brick中，就将其指向相应Brick的位置，如果不在某个Brick中，就指向均匀排布的最高级别的Probe信息块中。
- relighting--向场景中打光 
  - deferred lighting: 在 lighting pass 用深度重建像素的世界位置 P，从 G-buffer 读法线和材质，再用 P 采 3D texture、用 N 求值。
  - forward lighting: 在cpu端把对应物件周围的irradiance信息抽取出来，针对这个物体（比如一个玩家），作为一个ambient lighting，以几个shader constant的方式传到shader中去计算. shader 用各像素自己的 N 求值。 
    > 这样的话一个物体共用一个irradiance？
- 处理漏光: https://zhuanlan.zhihu.com/p/2021899719325021160
  - 虚拟偏移: 偏移Probe 的采光位置(交点)。保证采光位置不会在墙体内，或跨过墙体。 通过SDF来判断？
  - 调整权重: 根据采用光照的方向，计算周围八个点的权重。根据**表面朝向** 和 **Probe 有效性**
    - 表面朝向: 即使从probe -> 交点的向量，其方向是否与交点表面的法线同向。
    - Probe 有效性： 烘焙时，会统计 probe 的采样射线有多大比例命中了几何体的背面。背面命中比例达到配置的 Validity Threshold，就可以将这个 probe 判为无效。
    - 屏幕深度遮挡
    - Chebyshev 距离检验： 在多个方向（通常是 6 个轴向或球面调和函数）记录到周围几何体的距离r，对应的均值E[r]，二阶矩e[r]。运行时，插值得到该方向上均值E[r]，二阶矩e[r]，运算该处方差，然后使用切比雪夫不等式，计算出该方向上未被遮挡的可能性。
      > 此外当 d 小于 均值E[r]，默认不遮挡。d为该方向上的距离，随着d的增大，不等式得到的未被遮挡的可能性越小。
- Probe权重
  - probe规则排布: 包住查询位置 8 个probe，使用三线性插值。
  - probe 非规则排布: 由重心坐标决定,包含查询位置的那个四面体(以probe为顶点)
----------------------------------------------------



----------------------------------------------------
球谐函数的Deringing

使用球谐函数计算 light probe 光照，有时会遇到 ring /振铃效应的问题，当接近一个punctual light进行光照烘焙时，就有可能出现这个问题。
因为峰值过高，要用球谐系数拟合这个尖峰，重建结果可能在其他方向产生多余的亮瓣、暗瓣，甚至负值
----------------------------------------------------
Lumen:
将三角形几何简化为轴对齐 Surfel。
用类似 K-means 的过程聚类 Surfel。
将聚类转换为轴对齐 Cards。
按覆盖程度选取给定数量的 Cards；失败时可回退到类似 Cubemap 的六面投影。

MeshIndex
    → 找到 Card Grid

Card Grid + 命中位置
    → 查所在单元，得到 6 个候选 Cards

命中法线
    → 从中选 3 个 Cards

对每个 Card：
    → 投影命中位置
    → Gather4 深度，手动处理双线性过滤
    → 排除无效 texel
    → 按深度差、Card 法线与命中法线的点积、
      双线性系数加权

混合各 Card 样本
    → 得到命中位置的 Surface Cache 数据

Lumen不是每个像素都会发射光线的，而是以 1/16 的分辨率放置probe。实际计算，会选取里像素最近的几个probe接受到的光线。 而且各个probe的权重会受到以下因素的影响: probe 离像素的相对距离，probe 与像素所在的切屏幕的距离（离切平面越远，说明二者在几何上共面的可能性越小？）。