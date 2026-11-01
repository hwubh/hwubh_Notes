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
- 缓存结构
- 缓存的光学量
  - Radiance : W/(m²·sr)， 记录在X位置上，光的方向，光的颜色，光的强度。
    > \[L(x,\omega)=(R,G,B)\] 
  - Irradiance: W/m², 记录在X位置上, 朝向n的表明接受到的，光的颜色，光的强度。 
    > Irradiance 本质为在朝向n的表面上，进行将Radiance(光*入射角余弦)进行半球积分后的结果。
- 计算方式（Final Gather）

Lumen:
