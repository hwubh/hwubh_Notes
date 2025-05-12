# Unity学习笔记--Deferred+(DeferredPlus) Rendering 
## URP中Deferred渲染与Deferred+渲染的对比

## 提要：
Unity6.1版本中在URP中引入了新渲染路径[**Deferred+**]()。 本文主要通过对比URP中**Deferred+**渲染和**Deferred**渲染来理解前者的实现思路。 因为本人才疏学浅，可能存在一些错误的地方，还请各位大佬斧正。

以下是正文：

## 前言
Deferred+，其实就是Clustered Deferred Rendering的一种实现。在URP可以视为Forward+ 和 Deferred的结合。 在Deferred Rendering的基础上，引入了Forward+ 中的分簇着色（Clustered Shading）。 
下文中主要针对URP中Cluster的构建，以及引入Cluster后Deferred rendering的修改进行介绍。
> 关于Deferred Rendering的原理可以参见这篇文章
<!-- 一方面同Forward+， 对视锥体从XY（Tile） 和 Z （Zbin） 两个“维度”上进行了分割，并计算各个被分割区域受到哪些*非平行（additional）光源*和反射探针的影响。 -->

## Deferred vs Deferred+
> 下文内容暂不考虑XR等多视图渲染的情况。
### ForwardLights 
Deferred+开启时，会在URP管线中会创建[ForwardLights](https://github.com/Unity-Technologies/Graphics/blob/d18dd70ba6e63447b9c1f2225b2a94d56d29a644/Packages/com.unity.render-pipelines.universal/Runtime/ForwardLights.cs)的对象进行Cluster的构建。
- Constructor： 
  - CreateForwardPlusBuffers(): 创建两个[GraphicsBuffer](https://docs.unity3d.com/6000.1/Documentation/ScriptReference/GraphicsBuffer.html) “URP Z-Bin Buffer”和 “URP Tile Buffer”，分别用于储存Z方向， XY平面上的非平行(additional)光源和反射探针的信息，并以Cbuffer的形式上传到GPU侧。 
    > 下文中使用 **local lights** 一词表示非平行光源和反射探针。 
    - URP Z-Bin Buffer： 记录从相机近平面到远平面间划分成的各个区块(*Zbin*)的非平行光源，反射探针信息，大小固定为4096个 uint。
      - URP Z-Bin Buffer 可以被进一步划分为N个*Zbin*，每个*Zbin*占据 （2+k）个uint。 其中k的数量取决与 Local Lights的数量。 因为每个uint至多可以记录32个Local lights的信息，即uint的每个位(bit)上记录一个。 <br> $k = \frac{Local lights的数量}{32} + 1$ 
      - Zbin的(2+k)个uint
        - 第一个uint（**header0**）: 记录local lights中非平行光源的序号的最大值和最小值。 e.g. 458752 = 0000 000000000111 0000000000000000， 高16位为最大值，序号为7. 低16位为最小值，序号为0.
        - 第二个uint（**header1**）: 记录local lights中反射探针的序号的最大值和最小值。e.g. 589832 = 0000000000001001 0000000000001000,  高16位为最大值，序号为9. 低16位为最小值，序号为8.
        - 第三个及之后的uint（**word X**）: 记录local lights的序号。 e.g. 899 = 1110000011， 从右向左的各个位代表该位上的光源是否影响该区块。 这里第0，1，7，8，9位上取值为1，代表序号为0，1，7，8，9的local lights参与该Zibn中的片元的着色计算。
    - URP Tile Buffer： 记录屏幕空间中XY平面上屏幕划分成的各个区块(*Tile*)的非平行光源，反射探针信息，大小固定为4096个 uint, 或 (local lights的数量大于32个时) 10384个uint。
      - URP Tile Buffer 可以被进一步划分为N个*Tile*，每个*Tile*占据k个uint。k的数量取决与 Local Lights的数量。每个uint的位上记录一个local lights信息。 <br> $k = \frac{Local lights的数量}{32} + 1$
  - ReflectionProbeManager.Create()： 建两张 1*1的反射探针的RT。
- PreSetup： 因为考虑兼容性问题（不用Compute Shader）， URP在CPU侧使用Job system构建Cluster。
  ```
  int m_LightCount: Local lights的数量。
  int m_DirectionalLightCount： 非主光源的平行光源的数量。
  int m_WordsPerTile： 每个Tile/Zbin占据的用于记录 Local Lights的uint的数量。 
  int2 m_TileResolution： XY平面上Tile数量。x,y分量分别记录X，Y方向上的数量。
  float m_ZBinScale: Z方向单位距离上Zbin的数量。
  float m_ZBinOffset: 0~近平面间距离计算的Zbin数量，作为offset用于当前Zbin序号的计算。
  int m_BinCount: Z方向上Zbin的数量。
  ```
  > 以上为下文会使用的一些全局变量的含义。
  - 计算 `m_LightCount`, `m_DirectionalLightCount`, `m_WordsPerTile`: 因为平行光源为全局影响，所以不参与Cluster的构建。
    ``` c#
    m_LightCount = lightData.visibleLights.Length;//光源的数量
    var lightOffset = 0;
    while (lightOffset < m_LightCount && lightData.visibleLights[lightOffset].lightType == LightType.Directional) //排除光源中的平行光源
    {
        lightOffset++; //平行光源的数量
    }
    m_LightCount -= lightOffset; 

    // If there's 1 or more directional lights, one of them must be the main light
    m_DirectionalLightCount = lightOffset > 0 ? lightOffset - 1 : 0; // 减去主光源，得到非主光的平行光源的数量

    var visibleLights = lightData.visibleLights.GetSubArray(lightOffset, m_LightCount);
    var reflectionProbes = renderingData.cullResults.visibleReflectionProbes;
    var reflectionProbeCount = math.min(reflectionProbes.Length, UniversalRenderPipeline.maxVisibleReflectionProbes);
    var itemsPerTile = visibleLights.Length + reflectionProbeCount; // Local lights的数量
    m_WordsPerTile = (itemsPerTile + 31) / 32; // 每个Tile需要多少uint来记录Local lights。 （每个uint 32位记录32个）
    ``` 
  - 计算 `m_TileResolution`： 在XY平面上，从**8个像素宽**的Tile开始划分。如果所以Tile需要的uint数量超过了 `UniversalRenderPipeline.maxTileWords`(URP Tile Buffer拥有的uint的数量)，则将Tile的尺寸扩大一倍，使用16个像素宽的Tile继续划分，以减少所需的uint数量。依次类推，直到Tile所需的uint数量少于`UniversalRenderPipeline.maxTileWords`
    ``` c#
      m_ActualTileWidth = 8 >> 1;
    do
    {
        m_ActualTileWidth <<= 1; 
        m_TileResolution = (screenResolution + m_ActualTileWidth - 1) / m_ActualTileWidth;
    }
    while ((m_TileResolution.x * m_TileResolution.y * m_WordsPerTile * viewCount) > UniversalRenderPipeline.maxTileWords); 
    // m_TileResolution.x * m_TileResolution.y： Tile的数量
    // m_WordsPerTile： 每个Tile使用的uint数量
    // viewCount：视口的数量，比如立体渲染时是2（分别渲染左右眼）
    ``` 
  - 计算 `m_ZBinScale`, `m_ZBinOffset`, `m_BinCount`： 根据URP Tile Buffer拥有的uint的数量，计算Z方向上Zbin的数量，以及后续反过来使用深度Z计算Zbin序号时所需的参数。  
    - 序号的计算公式为： （正交时）$Zbin序号 = \frac{Z}{Z_{far} - Z_{near}} - \frac{Z_{near}}{Z_{far} - Z_{near}}$ 
      或 （透视时）$Zbin序号 = \frac{log_{2}(Z)}{log_{2}(Z_{far} / Z_{near})} - \frac{log_{2}(Z_{near})}{log_{2}(Z_{far} / Z_{near})}$
      其中透视投影时的公式应该是出自[**id tech 6**的分享](https://advances.realtimerendering.com/s2016/Siggraph2016_idTech6.pdf)的Page 5。 
    ``` c#
    if (!camera.orthographic)
    {
        // Use to calculate binIndex = log2(z) * zBinScale + zBinOffset
        m_ZBinScale = (UniversalRenderPipeline.maxZBinWords / viewCount) / ((math.log2(camera.farClipPlane) - math.log2(camera.nearClipPlane)) * (2 + m_WordsPerTile));
        m_ZBinOffset = -math.log2(camera.nearClipPlane) * m_ZBinScale;
        m_BinCount = (int)(math.log2(camera.farClipPlane) * m_ZBinScale + m_ZBinOffset);
    }
    else
    {
        // Use to calculate binIndex = z * zBinScale + zBinOffset
        m_ZBinScale = (UniversalRenderPipeline.maxZBinWords / viewCount) / ((camera.farClipPlane - camera.nearClipPlane) * (2 + m_WordsPerTile));
        m_ZBinOffset = -camera.nearClipPlane * m_ZBinScale;
        m_BinCount = (int)(camera.farClipPlane * m_ZBinScale + m_ZBinOffset);
    }
    ``` 
    > 透视时使用对数的原因： 沿Z划分时，我们希望做到每个Zbin在屏幕占据的像素尽可能接近。 因此需要划分时，越靠近远平面时，每一块占据的深度越多。（因为越远的三角形投影到屏幕上时越小）。 \
    ![20250324154311](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250324154311.png) \
    （图a）: 如果NDC空间中沿Z方向划分，虽然符合我们的希望，但会使得精度分布过于偏向于近平面。 \
    （图b）如果View空间中沿Z方向划分，会使每块分配的深度相同，不符合我们的希望。靠近 远平面的Cluster过多。 \
    （图c）因此URP中选择在View空间中使用对数进行划分，使Cluster更接近为一个矩形。在图二的基础上，给靠近近平面的分配更多的Cluster，对近处的物体进行更细的划分，剔除尽可能多的光源？ \
    Z方向上Zbin的划分。  \
    ![20250324154543](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250324154543.png) \
    参考资料: [A Primer On Efficient Rendering Algorithms & Clustered Shading](https://www.aortiz.me/2018/12/21/CG.html#tiled-shading--forward)； [Clustered Deferred and Forward Shading(论文)](https://www.cse.chalmers.se/~uffe/clustered_shading_preprint.pdf); 
  > 这里对于Z方向的划分方式，感觉像是一种在均分NDC空间，View空间之间的权衡？
  - 