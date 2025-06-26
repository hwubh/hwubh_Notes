# Unity学习笔记--Deferred+(DeferredPlus) Rendering 
## URP中Deferred渲染与Deferred+渲染的对比

## 提要：
Unity6.1版本中在URP中引入了新渲染路径[**Deferred+**]()。 本文主要通过对比URP中**Deferred+**渲染和**Deferred**渲染来理解前者的实现思路。 因为本人才疏学浅，可能存在一些错误的地方，还请各位大佬斧正。

以下是正文：

## 前言
Deferred+，其实就是Clustered Deferred Rendering的一种实现。在URP可以视为Forward+ 和 Deferred的结合。 其在Deferred Rendering的基础上，引入了Forward+ 中的分簇着色（Clustered Shading）。 
下文中主要针对URP中Cluster的构建，以及引入Cluster后Deferred rendering的修改进行介绍。
> 关于Deferred Rendering的原理可以参见这篇文章
<!-- 一方面同Forward+， 对视锥体从XY（Tile） 和 Z （Zbin） 两个“维度”上进行了分割，并计算各个被分割区域受到哪些*非平行（additional）光源*和反射探针的影响。 -->

## Deferred vs Deferred+
> 下文内容暂不考虑XR等多视图渲染的情况。
### ForwardLights 
Deferred+开启时，会在URP管线中会创建[ForwardLights](https://github.com/Unity-Technologies/Graphics/blob/d18dd70ba6e63447b9c1f2225b2a94d56d29a644/Packages/com.unity.render-pipelines.universal/Runtime/ForwardLights.cs)的对象来进行Cluster的构建。
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
  NativeArray<float2> minMaxZs: Local lights在相机空间的深度范围。
  NativeArray<uint> m_ZBins: URP Z-Bin Buffer的数据，记录Zbin收到哪些Local lights的影响。
  int itemsPerTile: Local lights的数量。
  ```
  > 以上为下文会使用的一些变量的含义。
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
    
    > 这里对于Z方向的划分方式，感觉像是一种在均分NDC空间，View空间这两种方案之间的权衡？
  - 对反射探针`reflectionProbes`(`renderingData.cullResults.visibleReflectionProbes`)根据 [importance](https://docs.unity3d.com/6000.1/Documentation/ScriptReference/ReflectionProbe-importance.html) 从大到小重新进行排序。
    ```c#
    // Should probe come after otherProbe?
    static bool IsProbeGreater(VisibleReflectionProbe probe, VisibleReflectionProbe otherProbe)
    {
        return probe.importance < otherProbe.importance ||
            (probe.importance == otherProbe.importance && probe.bounds.extents.sqrMagnitude > otherProbe.bounds.extents.sqrMagnitude);
    }

    for (var i = 1; i < reflectionProbeCount; i++)
    {
        var probe = reflectionProbes[i];
        var j = i - 1;
        while (j >= 0 && IsProbeGreater(reflectionProbes[j], probe))
        {
            reflectionProbes[j + 1] = reflectionProbes[j];
            j--;
        }

        reflectionProbes[j + 1] = probe;
    }
    ```
  - `lightMinMaxZJob`： 计算各个Local lights中各个spot/point light影响的深度范围。
    ``` c#
    var lightMinMaxZJob = new LightMinMaxZJob
    {
        worldToViews = worldToViews,
        lights = visibleLights,
        minMaxZs = minMaxZs.GetSubArray(0, m_LightCount * viewCount)
    };
    // Innerloop batch count of 32 is not special, just a handwavy amount to not have too much scheduling overhead nor too little parallelism.
    var lightMinMaxZHandle = lightMinMaxZJob.ScheduleParallel(m_LightCount * viewCount, 32, new JobHandle());
    ``` 
    先计算光源在View空间中的深度值，然后加上/减去光的范围。
    ``` C#
    var lightIndex = index % lights.Length;
    var light = lights[lightIndex];
    var lightToWorld = (float4x4)light.localToWorldMatrix;
    var originWS = lightToWorld.c3.xyz;
    var viewIndex = index / lights.Length;
    var worldToView = worldToViews[viewIndex];
    var originVS = math.mul(worldToView, math.float4(originWS, 1)).xyz;
    originVS.z *= -1;

    var minMax = math.float2(originVS.z - light.range, originVS.z + light.range);
    ```
    - Point light的深度范围： 光源在View空间中的深度值，加上/减去光的范围。
    - Spot light的深度范围： =圆锥包围盒的范围在Z方向上的投影。 
    ``` c#
    if (light.lightType == LightType.Spot)
    {
        // Based on https://iquilezles.org/www/articles/diskbbox/diskbbox.htm
        var angleA = math.radians(light.spotAngle) * 0.5f; // 锥体的半角
        float cosAngleA = math.cos(angleA);
        float coneHeight = light.range * cosAngleA; // 锥体的高度
        float3 spotDirectionWS = lightToWorld.c2.xyz; // 光源朝向(局部空间+Z方向)在世界空间的表达
        var endPointWS = originWS + spotDirectionWS * coneHeight; // 圆锥底面中心在世界空间的表达
        var endPointVS = math.mul(worldToView, math.float4(endPointWS, 1)).xyz;// 圆锥底面中心在相机空间的表达
        endPointVS.z *= -1;
        var angleB = math.PI * 0.5f - angleA;
        var coneRadius = light.range * cosAngleA * math.sin(angleA) / math.sin(angleB); // math.sin(angleA) / math.sin(angleB) = tan(angleA), 不写math.tan的原因?
        var a = endPointVS - originVS;
        var e = math.sqrt(1.0f - a.z * a.z / math.dot(a, a));

        // `-a.z` and `a.z` is `dot(a, {0, 0, -1}).z` and `dot(a, {0, 0, 1}).z` optimized
        // `cosAngleA` is multiplied by `coneHeight` to avoid normalizing `a`, which we know has length `coneHeight`
        if (-a.z < coneHeight * cosAngleA) minMax.x = math.min(originVS.z, endPointVS.z - e * coneRadius);
        if (a.z < coneHeight * cosAngleA) minMax.y = math.max(originVS.z, endPointVS.z + e * coneRadius);
        //算锥体在Z方向的最大/最小值
    }
    ```
    ![锥体](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250513143954.png)
  - `ReflectionProbeMinMaxZJob`: 计算各个Local lights中各个反射探针影响的深度范围。
    ``` c#
    var reflectionProbeMinMaxZJob = new ReflectionProbeMinMaxZJob
    {
        worldToViews = worldToViews,
        reflectionProbes = reflectionProbes,
        minMaxZs = minMaxZs.GetSubArray(m_LightCount * viewCount, reflectionProbeCount * viewCount)
    };
    var reflectionProbeMinMaxZHandle = reflectionProbeMinMaxZJob.ScheduleParallel(reflectionProbeCount * viewCount, 32, lightMinMaxZHandle);
    ```
    遍历反射探针的包围盒的各个顶点，计算其在相机空间Z方向的投影。
    ``` c#
    var minMax = math.float2(float.MaxValue, float.MinValue);
    var reflectionProbeIndex = index % reflectionProbes.Length;
    var reflectionProbe = reflectionProbes[reflectionProbeIndex];
    var viewIndex = index / reflectionProbes.Length;
    var worldToView = worldToViews[viewIndex];
    var centerWS = (float3)reflectionProbe.bounds.center;
    var extentsWS = (float3)reflectionProbe.bounds.extents;
    for (var i = 0; i < 8; i++)
    {
        // Convert index to x, y, and z in [-1, 1]
        var x = ((i << 1) & 2) - 1;
        var y = (i & 2) - 1;
        var z = ((i >> 1) & 2) - 1;
        var cornerVS = math.mul(worldToView, math.float4(centerWS + extentsWS * math.float3(x, y, z), 1));
        cornerVS.z *= -1;
        minMax.x = math.min(minMax.x, cornerVS.z);
        minMax.y = math.max(minMax.y, cornerVS.z);
    }

    minMaxZs[index] = minMax;
    ```
  - `ZBinningJob`： 每128个Zbin分为一个batch，在不同的worker上计算。 计算Local lights对Zbin的影响，填充m_ZBins。 数据结构参见上文 **URP Z-Bin Buffer** 的内容。
    - 遍历Local lights，从 `minMaxZs`中得到该光源/反射探针影响的深度范围。根据深度范围的最大/最小值计算得到其对应的Zbin，更新这两个Zbin之间所有的Zbin的数据。
    ``` C#
    void FillZBins(int binStart, int binEnd, int itemStart, int itemEnd, int headerIndex, int itemOffset, int binOffset)
    {
        for (var index = itemStart; index < itemEnd; index++) // 遍历Local lights 
        {
            var minMax = minMaxZs[itemOffset + index];
            var minBin = math.max((int)((isOrthographic ? minMax.x : math.log2(minMax.x)) * zBinScale + zBinOffset), binStart);
            var maxBin = math.min((int)((isOrthographic ? minMax.y : math.log2(minMax.y)) * zBinScale + zBinOffset), binEnd); // 分别计算min，max Zbin。

            var wordIndex = index / 32; // 光源序号记录在第（2+wordIndex）个uint上。
            var bitMask = 1u << (index % 32); // 光源序号记录在第bitMask位上。

            for (var binIndex = minBin; binIndex <= maxBin; binIndex++)
            {
                var baseIndex = (binOffset + binIndex) * (headerLength + wordsPerTile);
                var (minIndex, maxIndex) = DecodeHeader(bins[baseIndex + headerIndex]); // headerIndex： 写入第几个uint，取值为0或1，分别代表光源和反射探针。
                minIndex = math.min(minIndex, (uint)index);
                maxIndex = math.max(maxIndex, (uint)index);
                bins[baseIndex + headerIndex] = EncodeHeader(minIndex, maxIndex); // 更新第1或2个uint记录的最大/最小光源，反射探针数据。
                bins[baseIndex + headerLength + wordIndex] |= bitMask;// 更新受影响的local lights数据。
            }
        }
    }
    ``` 
  - `viewToViewportScaleBias`: 传入投影矩阵, 计算投影矩阵的偏移。个人感觉XR用的情况比较多？？
    - 正交时计算非对称正交投影的偏移； 投影计算投影屏幕的偏移参数。
    - viewPlaneBottom0 ： 视口偏移的下界
    - viewPlaneTop0 ： 视口偏移的上界
    - viewToViewportScaleBias0 ： 视口的缩放偏置参数；前两项记录scale，后两项记录offset
    ``` c#
    GetViewParams(camera, viewToClips[0], out float viewPlaneBottom0, out float viewPlaneTop0, out float4 viewToViewportScaleBias0);
    GetViewParams(camera, viewToClips[1], out float viewPlaneBottom1, out float viewPlaneTop1, out float4 viewToViewportScaleBias1);

    // Calculate view planes and viewToViewportScaleBias. This handles projection center in case the projection is off-centered
    void GetViewParams(Camera camera, float4x4 viewToClip, out float viewPlaneBot, out float viewPlaneTop, out float4 viewToViewportScaleBias)
    {
        // We want to calculate `fovHalfHeight = tan(fov / 2)`
        // `projection[1][1]` contains `1 / tan(fov / 2)`
        var viewPlaneHalfSizeInv = math.float2(viewToClip[0][0], viewToClip[1][1]);
        var viewPlaneHalfSize = math.rcp(viewPlaneHalfSizeInv);
        var centerClipSpace = camera.orthographic ? -math.float2(viewToClip[3][0], viewToClip[3][1]): math.float2(viewToClip[2][0], viewToClip[2][1]);

        viewPlaneBot = centerClipSpace.y * viewPlaneHalfSize.y - viewPlaneHalfSize.y;
        viewPlaneTop = centerClipSpace.y * viewPlaneHalfSize.y + viewPlaneHalfSize.y;
        viewToViewportScaleBias = math.float4(
            viewPlaneHalfSizeInv * 0.5f,
            -centerClipSpace * 0.5f + 0.5f
        );
    }
    ``` 
  - `TilingJob`: 计算各个Local lights影响的XY平面上的范围。
    - `rangesPerItem`: 计算每个光源/反射探针在内存中占用的 *InclusiveRange* 结构数量，并确保内存对齐（128 字节为单位），避免伪共享。 
      - *InclusiveRange*: 记录受该local light影响的Tile在一个维度上的范围。 
        每个*InclusiveRange*含两个 *short* 成员变量： `start`, `end`。 分别对应范围边缘的Tile的序号。
      - 每个`rangesPerItem` 有 （1 + m_TileResolution.y）个 *InclusiveRange*。 其中 “1” 的部分记录Y方向上该local light影响的Tile的**行（row）**的范围。 “m_TileResolution.y”的部分，分别代表每一行上，在X方向上该local light影响的Tile的**纵（column）**的范围。 二者重复部分的Tile，则为受该local light影响的Tile。 \
      以下图为例： m_TileResolution = (10, 30), 存在一个Point light在XY平面上投影为红圈。其第“1”的*InclusiveRange*的 `start`, `end`取值为2，29。 第15个*InclusiveRange*代表行14上的范围，`start`, `end`取值为D，H。 ![20250514115345](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250514115345.png)
    - `tileRanges`： *InclusiveRange*数组， `rangesPerItem`的集合，汇总所有Local lights影响Tile的范围。
    ```c#
    // Each light needs 1 range for Y, and a range per row. Align to 128-bytes to avoid false sharing.
    var rangesPerItem = AlignByteCount((1 + m_TileResolution.y) * UnsafeUtility.SizeOf<InclusiveRange>(), 128) / UnsafeUtility.SizeOf<InclusiveRange>();
    var tileRanges = new NativeArray<InclusiveRange>(rangesPerItem * itemsPerTile * viewCount, Allocator.TempJob);
    ```
    - Execute(): 根据光源类型，相机的投影方式调用不同的函数进行处理，得到Local light影响的Tile的范围。 这里存在Spot/Point，正交/透视两两组合，加上反射探针共5种情况。
    ``` C#
    public void Execute(int jobIndex)
    {
        var index = jobIndex % itemsPerTile;
        m_ViewIndex = jobIndex / itemsPerTile;
        m_Offset = jobIndex * rangesPerItem; // 该Local light在 tileRanges上的初始位置。

        m_TileYRange = new InclusiveRange(short.MaxValue, short.MinValue); // tileRanges[m_Offset] = m_TileYRange; “1” 代表的Y方向的取值。

        for (var i = 0; i < rangesPerItem; i++)
        {
            tileRanges[m_Offset + i] = new InclusiveRange(short.MaxValue, short.MinValue);
        }


        if (index < lights.Length)
        {
            if (isOrthographic) { TileLightOrthographic(index); } //正交投影
            else { TileLight(index); } //透视投影
        }
        else { TileReflectionProbe(index); } //反射探针
    }
    ``` 
      - Spot/Point Lights： 以下例子中使用 光源位置为（0，0，0），light.range为10的point light。光源位置为(0,0,0), 光源方向为(1.0, 1.0, 1.0), light.range为10， 圆锥高度为8， 底面圆半径为6的 spot light。 相机朝向为+Z方向。
      - 正交： 
        - 计算圆心在XY平面上的投影，更新在`tileRanges`上的取值范围。
          ``` C#
          void TileLightOrthographic(int lightIndex)
          {
              var light = lights[lightIndex];
              var lightToWorld = (float4x4)light.localToWorldMatrix;
              var lightPosVS = math.mul(worldToViews[m_ViewIndex], math.float4(lightToWorld.c3.xyz, 1)).xyz;
              lightPosVS.z *= -1;
              ExpandOrthographic(lightPosVS);
              //...
          }
          ```
          > ExpandOrthographic(float3 positionVS):相机空间 -> 屏幕空间 -> Tile序号。根据Tile序号更新该光源Y方向的取值范围，和Tile所在行的X方向的取值范围。
            ``` c#
            /// <summary>
            /// Expands the tile Y range and the X range in the row containing the position.
            /// </summary>
            void ExpandOrthographic(float3 positionVS)
            {
                // var positionTS = math.clamp(ViewToTileSpace(positionVS), 0, tileCount - 1);
                var positionTS = ViewToTileSpaceOrthographic(positionVS);
                var tileY = (int)positionTS.y;
                var tileX = (int)positionTS.x;
                m_TileYRange.Expand((short)math.clamp(tileY, 0, tileCount.y - 1));
                if (tileY >= 0 && tileY < tileCount.y && tileX >= 0 && tileX < tileCount.x)
                {
                    var rowXRange = tileRanges[m_Offset + 1 + tileY];
                    rowXRange.Expand((short)tileX);
                    tileRanges[m_Offset + 1 + tileY] = rowXRange;
                }
            }
            ```
        - 计算光源在相机空间的朝向 
          ``` C#
          var lightDirVS = math.mul(worldToViews[m_ViewIndex], math.float4(lightToWorld.c2.xyz, 0)).xyz;
          lightDirVS.z *= -1;
          lightDirVS = math.normalize(lightDirVS);
          ```
        - 计算光源的包围球在XY平面上的圆形投影，根据light.range得到其在XY方向的四个极值点。
          ``` C#
          var range = light.range;
          var sphereBoundY0 = lightPosVS - math.float3(0, range, 0);
          var sphereBoundY1 = lightPosVS + math.float3(0, range, 0);
          var sphereBoundX0 = lightPosVS - math.float3(range, 0, 0);
          var sphereBoundX1 = lightPosVS + math.float3(range, 0, 0);
          ExpandOrthographic(sphereBoundY0);
          ExpandOrthographic(sphereBoundY0);
          ExpandOrthographic(sphereBoundY0);
          ExpandOrthographic(sphereBoundY0);
          ``` 
          **Spot Light**的话，需要额外判断该极值点是否在其**圆锥**的影响范围内，不在的话，抛弃该极值点。
          ``` C#
          var halfAngle = math.radians(light.spotAngle * 0.5f);
          var cosHalfAngle = math.cos(halfAngle);
          //...
          bool SpherePointIsValid(float3 p) => light.lightType == LightType.Point ||
                math.dot(math.normalize(p - lightPosVS), lightDirVS) >= cosHalfAngle; // 比较角度的cos值 -> cos值较大时，角度较小，因而在圆锥内。
          //...
          if (SpherePointIsValid(sphereBoundY0)) ExpandOrthographic(sphereBoundY0);
          if (SpherePointIsValid(sphereBoundY1)) ExpandOrthographic(sphereBoundY1);
          if (SpherePointIsValid(sphereBoundX0)) ExpandOrthographic(sphereBoundX0);
          if (SpherePointIsValid(sphereBoundX1)) ExpandOrthographic(sphereBoundX1);
          ``` 
          ![20250514161608](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250514161608.png)
          ![20250514162044](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250514162044.png)
          > 这个例子中，明显极值点不在Spot light的圆锥内，故会被抛弃。
        - Spot Light还需要考虑底面圆在XY平面上的投影： 先计算底面圆圆心`circleCenter`的位置，然后根据XY坐标轴单位向量在底面上的投影，计算出底面在XY平面上的极值点`circleBoundY0/Y1/X0/X1`。
          ```C#
            var rangeSq = square(range);


            var circleCenter = lightPosVS + lightDirVS * coneHeight;
            var circleRadius = math.sqrt(rangeSq - coneHeightSq);
            var circleRadiusSq = square(circleRadius);
            var circleUp = math.normalize(math.float3(0, 1, 0) - lightDirVS * lightDirVS.y);
            var circleRight = math.normalize(math.float3(1, 0, 0) - lightDirVS * lightDirVS.x);
            var circleBoundY0 = circleCenter - circleUp * circleRadius;
            var circleBoundY1 = circleCenter + circleUp * circleRadius;

            if (light.lightType == LightType.Spot)
            {
                var circleBoundX0 = circleCenter - circleRight * circleRadius;
                var circleBoundX1 = circleCenter + circleRight * circleRadius;
                ExpandOrthographic(circleBoundY0);
                ExpandOrthographic(circleBoundY1);
                ExpandOrthographic(circleBoundX0);
                ExpandOrthographic(circleBoundX1);
            }
          ``` 
          ![20250522110235](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250522110235.png)
        - 逐行计算Tile在X方向上受影响的范围： 逐行计算X方向上的直线 Y = PlaneY 与光源影响范围在XY平面上的交点，交点之间的的Tile被标记为受该光源的影响。
          - Y = PlaneY 的取值为每一行Tile的上边在XY平面的Y值，其结果同时作用于与以其为上边，下边的两行Tile。
            ``` C#
            // Tile plane ranges
            for (var planeIndex = m_TileYRange.start + 1; planeIndex <= m_TileYRange.end; planeIndex++) // 遍历受影响的所有Tile行
            {
              var planeRange = InclusiveRange.empty; // 记录X方向上Tile受影响的范围。
              var planeY = math.lerp(viewPlaneBottoms[m_ViewIndex], viewPlaneTops[m_ViewIndex], planeIndex * tileScaleInv.y); // 使用每一行Tile的上边所在的直线。

              // ...

              var tileIndex = m_Offset + 1 + planeIndex;
              tileRanges[tileIndex] = InclusiveRange.Merge(tileRanges[tileIndex], planeRange); // 结果写入以其为下边的Tile行的受影响范围。
              tileRanges[tileIndex - 1] = InclusiveRange.Merge(tileRanges[tileIndex - 1], planeRange);// 结果写入以其为上边的Tile行的受影响范围。
            }
            ``` 
          - Tile是否在Light 包围球的XY投影内： 计算直线 Y = PlanY 与光源包围球在XY平面投影上的交点`sphereX0/X1`。 计算交点所在的Tile坐标，记录该光源在二者及其之间的Tile上。 
            - 若光源为Spot Light，需要计算交点是否在其**圆锥**的影响范围内，不在的话，抛弃该交点。
            ``` C#
            var sphereX = math.sqrt(rangeSq - square(planeY - lightPosVS.y));
            var sphereX0 = math.float3(lightPosVS.x - sphereX, planeY, lightPosVS.z);
            var sphereX1 = math.float3(lightPosVS.x + sphereX, planeY, lightPosVS.z);
            if (SpherePointIsValid(sphereX0)) { ExpandRangeOrthographic(ref planeRange, sphereX0.x); }
            if (SpherePointIsValid(sphereX1)) { ExpandRangeOrthographic(ref planeRange, sphereX1.x); }
            ```
            ![20250522115002](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250522115002.png)
          - **Spot Light**还需要计算Tile是否在锥体或底面圆的XY平面投影的范围内的情况：
            - 计算Y = PlaneY与锥体投影的交点，即计算Y = PlaneY与到底面圆XY平面投影的两条切线的交点。
              ``` C#
                // Find two lines in screen-space for the cone if the light is a spot.
                float coneDir0X = 0, coneDir0YInv = 0, coneDir1X = 0, coneDir1YInv = 0;
                if (light.lightType == LightType.Spot)
                {
                    // Distance from light position to and radius of sphere fitted to the end of the cone.
                    var sphereDistance = coneHeight + circleRadiusSq * coneHeightInv;
                    var sphereRadius = math.sqrt(square(circleRadiusSq) * coneHeightInvSq + circleRadiusSq); // sphereDistance, sphereRadius, 切点到圆心线段构成一个直角三角形，根据相似三角形可以得到切点到圆心的距离。
                    var directionXYSqInv = math.rcp(math.lengthsq(lightDirVS.xy));
                    var polarIntersection = -circleRadiusSq * coneHeightInv * directionXYSqInv * lightDirVS.xy;
                    var polarDir = math.sqrt((square(sphereRadius) - math.lengthsq(polarIntersection)) * directionXYSqInv) * math.float2(lightDirVS.y, -lightDirVS.x); // 将sphereDistance， sphereRadius投影到XY平面上，利用相似三角形进行计算。
                    var conePBase = lightPosVS.xy + sphereDistance * lightDirVS.xy + polarIntersection;
                    var coneP0 = conePBase - polarDir;
                    var coneP1 = conePBase + polarDir; // 切线与底面圆的交点在XY平面上的投影。

                    coneDir0X = coneP0.x - lightPosVS.x;
                    coneDir0YInv = math.rcp(coneP0.y - lightPosVS.y);
                    coneDir1X = coneP1.x - lightPosVS.x;
                    coneDir1YInv = math.rcp(coneP1.y - lightPosVS.y); // 切线在XY平面上的变化率。
                }

                // Cone
                var deltaY = planeY - lightPosVS.y;
                var coneT0 = deltaY * coneDir0YInv;
                var coneT1 = deltaY * coneDir1YInv;
                if (coneT0 >= 0 && coneT0 <= 1) { ExpandRangeOrthographic(ref planeRange, lightPosVS.x + coneT0 * coneDir0X); }
                if (coneT1 >= 0 && coneT1 <= 1) { ExpandRangeOrthographic(ref planeRange, lightPosVS.x + coneT1 * coneDir1X); } // 根据变化率与Y方向的差值，得到X方向上的差值
              ``` 
              ![20250522151333](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250522151333.png) 
              > 上图的光源朝向为(1f, 1f, 0.4f)
            - 计算Y = PlaneY与底面圆投影的交点。
              ``` c#
              // Circle
              if (planeY >= circleBoundY0.y && planeY <= circleBoundY1.y)
              {
                  var intersectionDistance = (planeY - circleCenter.y) / circleUp.y; //根据Y
                  var closestPointX = circleCenter.x + intersectionDistance * circleUp.x;
                  var intersectionDirX = -lightDirVS.z / math.length(math.float3(-lightDirVS.z, 0, lightDirVS.x));
                  var sideDistance = math.sqrt(square(circleRadius) - square(intersectionDistance));
                  var circleX0 = closestPointX - sideDistance * intersectionDirX;
                  var circleX1 = closestPointX + sideDistance * intersectionDirX;
                  ExpandRangeOrthographic(ref planeRange, circleX0);
                  ExpandRangeOrthographic(ref planeRange, circleX1);
              }
              ``` 
              ![20250522155808](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250522155808.png)
      - 透视：
        - 计算圆心在XY平面上的投影，更新在`tileRanges`上的取值范围。
          ``` c#
          void TileLight(int lightIndex)
          {
            var light = lights[lightIndex];
            if (light.lightType != LightType.Point && light.lightType != LightType.Spot)
            {
                return;
            }

            var lightToWorld = (float4x4)light.localToWorldMatrix;
            var lightPositionVS = math.mul(worldToViews[m_ViewIndex], math.float4(lightToWorld.c3.xyz, 1)).xyz;
            lightPositionVS.z *= -1;
            if (lightPositionVS.z >= near) ExpandY(lightPositionVS);
          }

          /// <summary>
          /// Project onto Z=1, scale and offset into [0, tileCount]
          /// </summary>
          float2 ViewToTileSpaceOrthographic(float3 positionVS)
          {
              return (positionVS.xy * viewToViewportScaleBiases[m_ViewIndex].xy + viewToViewportScaleBiases[m_ViewIndex].zw) * tileScale;
          }
          ``` 
          > 思路与正交投影的方式类似。相机空间 -> 屏幕空间 -> Tile序号。
          ``` C#
          /// <summary>
          /// Expands the tile Y range and the X range in the row containing the position.
          /// </summary>
          void ExpandY(float3 positionVS)
          {
              // var positionTS = math.clamp(ViewToTileSpace(positionVS), 0, tileCount - 1);
              var positionTS = ViewToTileSpace(positionVS);
              var tileY = (int)positionTS.y;
              var tileX = (int)positionTS.x;
              m_TileYRange.Expand((short)math.clamp(tileY, 0, tileCount.y - 1));
              if (tileY >= 0 && tileY < tileCount.y && tileX >= 0 && tileX < tileCount.x)
              {
                  var rowXRange = tileRanges[m_Offset + 1 + tileY];
                  rowXRange.Expand((short)tileX);
                  tileRanges[m_Offset + 1 + tileY] = rowXRange;
              }
          }

          /// <summary>
          /// Project onto Z=1, scale and offset into [0, tileCount]
          /// </summary>
          float2 ViewToTileSpace(float3 positionVS)
          {
              return (positionVS.xy / positionVS.z * viewToViewportScaleBiases[m_ViewIndex].xy + viewToViewportScaleBiases[m_ViewIndex].zw) * tileScale;
          }
          ```
        - 计算光源的包围球在XY平面上的圆形投影。 因为透视投影下，相机X，Y方向上可能因为aspect的取值而存在FOV不同的情况。因此需要将包围球分别投影在XZ, YZ平面上，分别进行计算X，Y方向上的极值点`sphereBoundY0/Y1/X0/X1`。
          ``` C#
          var halfAngle = math.radians(light.spotAngle * 0.5f);
          var range = light.range;
          var rangesq = square(range);
          var cosHalfAngle = math.cos(halfAngle);

          // Radius of circle formed by intersection of sphere and near plane.
          // Found using Pythagoras with a right triangle formed by three points:
          // (a) light position
          // (b) light position projected to near plane
          // (c) a point on the near plane at a distance `range` from the light position
          //     (i.e. lies both on the sphere and the near plane)
          // Thus the hypotenuse is formed by (a) and (c) with length `range`, and the known side is formed
          // by (a) and (b) with length equal to the distance between the near plane and the light position.
          // The remaining unknown side is formed by (b) and (c) with length equal to the radius of the circle.
          // m_ClipCircleRadius = sqrt(sq(light.range) - sq(m_Near - m_LightPosition.z));
          var sphereClipRadius = math.sqrt(rangesq - square(near - lightPositionVS.z));

          // Assumes a point on the sphere, i.e. at distance `range` from the light position.
          // If spot light, we check the angle between the direction vector from the light position and the light direction vector.
          // Note that division by range is to normalize the vector, as we know that the resulting vector will have length `range`.
          bool SpherePointIsValid(float3 p) => light.lightType == LightType.Point ||
              math.dot(math.normalize(p - lightPositionVS), lightDirectionVS) >= cosHalfAngle;

          // Project light sphere onto YZ plane, find the horizon points, and re-construct view space position of found points.
          // CalculateSphereYBounds(lightPositionVS, range, near, sphereClipRadius, out var sphereBoundY0, out var sphereBoundY1);
          GetSphereHorizon(lightPositionVS.yz, range, near, sphereClipRadius, out var sphereBoundYZ0, out var sphereBoundYZ1);
          var sphereBoundY0 = math.float3(lightPositionVS.x, sphereBoundYZ0);
          var sphereBoundY1 = math.float3(lightPositionVS.x, sphereBoundYZ1);
          if (SpherePointIsValid(sphereBoundY0)) ExpandY(sphereBoundY0);
          if (SpherePointIsValid(sphereBoundY1)) ExpandY(sphereBoundY1);

          // Project light sphere onto XZ plane, find the horizon points, and re-construct view space position of found points.
          GetSphereHorizon(lightPositionVS.xz, range, near, sphereClipRadius, out var sphereBoundXZ0, out var sphereBoundXZ1);
          var sphereBoundX0 = math.float3(sphereBoundXZ0.x, lightPositionVS.y, sphereBoundXZ0.y);
          var sphereBoundX1 = math.float3(sphereBoundXZ1.x, lightPositionVS.y, sphereBoundXZ1.y);
          if (SpherePointIsValid(sphereBoundX0)) ExpandY(sphereBoundX0);
          if (SpherePointIsValid(sphereBoundX1)) ExpandY(sphereBoundX1);
          ``` 
          - 这里以投影到YZ平面上为例: 计算相机与投影圆的切线，切点。 需要考虑以下两种情况，
            ``` C#
            /// <summary>
            /// Finds the two horizon points seen from (0, 0) of a sphere projected onto either XZ or YZ. Takes clipping into account.
            /// </summary>
            static void GetSphereHorizon(float2 center, float radius, float near, float clipRadius, out float2 p0, out float2 p1)
            {
                var direction = math.normalize(center);

                // Distance from camera to center of sphere
                var d = math.length(center);

                // Distance from camera to sphere horizon edge
                var l = math.sqrt(d * d - radius * radius);

                // Height of circle horizon
                var h = l * radius / d;

                // Center of circle horizon
                var c = direction * (l * h / radius);

                p0 = math.float2(float.MinValue, 1f);
                p1 = math.float2(float.MaxValue, 1f);

                // Handle clipping
                if (center.y - radius < near)
                {
                    p0 = math.float2(center.x + clipRadius, near);
                    p1 = math.float2(center.x - clipRadius, near);
                }

                // Circle horizon points
                var c0 = c + math.float2(-direction.y, direction.x) * h;
                if (square(d) >= square(radius) && c0.y >= near)
                {
                    if (c0.x > p0.x) { p0 = c0; }
                    if (c0.x < p1.x) { p1 = c0; }
                }

                var c1 = c + math.float2(direction.y, -direction.x) * h; // c0, c1 -> 切点
                if (square(d) >= square(radius) && c1.y >= near)
                {
                    if (c1.x > p0.x) { p0 = c1; }
                    if (c1.x < p1.x) { p1 = c1; }
                }
            }
            ``` 
            ![20250522172445](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250522172445.png)
        - 如果光源为Spot Light：
          - 计算底面圆在YZ屏幕投影：
            ``` C#
            var baseRadius = math.sqrt(range * range - coneHeight * coneHeight);
            var baseCenter = lightPositionVS + lightDirectionVS * coneHeight;
            var baseUY = math.abs(math.abs(lightDirectionVS.x) - 1) < 1e-6f ? math.float3(0, 1, 0) : math.normalize(math.cross(lightDirectionVS, math.float3(1, 0, 0)));
            var baseVY = math.cross(lightDirectionVS, baseUY);
            GetProjectedCircleHorizon(baseCenter.yz, baseRadius, baseUY.yz, baseVY.yz, out var baseY1UV, out var baseY2UV);
            var baseY1 = baseCenter + baseY1UV.x * baseUY + baseY1UV.y * baseVY;
            var baseY2 = baseCenter + baseY2UV.x * baseUY + baseY2UV.y * baseVY;
            if (baseY1.z >= near) ExpandY(baseY1);
            if (baseY2.z >= near) ExpandY(baseY2);
            ``` 
          - 计算底面圆与近平面相交的情况： -> 主要考虑切点被剔除的情况？
            ``` C#
            if (GetCircleClipPoints(baseCenter, lightDirectionVS, baseRadius, near, out var baseClip0, out var baseClip1))
            {
                ExpandY(baseClip0);
                ExpandY(baseClip1);
            }
            ``` 
          - 计算Cone与近平面相交的情况： -> 计算Z方向上最近的点：可能为光源或底面圆Z方向上最近的。 -> 根据光源方向构建一个直角坐标系？？
            ```C#
            // Calculate Z bounds of cone and check if it's overlapping with the near plane.
            // From https://www.iquilezles.org/www/articles/diskbbox/diskbbox.htm
            var baseExtentZ = baseRadius * math.sqrt(1.0f - square(lightDirectionVS.z));
            var coneIsClipping = near >= math.min(baseCenter.z - baseExtentZ, lightPositionVS.z) && near <= math.max(baseCenter.z + baseExtentZ, lightPositionVS.z);
            ``` 
          - 计算Cone的投影
            - 如果与近平面相交. 将底面圆分别投影到XZ, YZ 平面上。构建投影形成的圆锥曲线参数方程。过光源中心做一条直线。计算该直线与圆锥曲线的两个切点。根据切点与光源中心构建直线的参数方程，计算该直线与近平面的交点，得到`p0Y/p1Y/p0X/p1X`
              ```C#
              var coneU = math.cross(lightDirectionVS, lightPositionVS);
              // The cross product will be the 0-vector if the light-direction and camera-to-light-position vectors are parallel.
              // In that case, {1, 0, 0} is orthogonal to the light direction and we use that instead.
              coneU = math.csum(coneU) != 0f ? math.normalize(coneU) : math.float3(1, 0, 0);
              var coneV = math.cross(lightDirectionVS, coneU);

              if (coneIsClipping)
              {
                  var r = baseRadius / coneHeight;

                  // Find the Y bounds of the near-plane cone intersection, i.e. where y' = 0
                  var thetaY = FindNearConicTangentTheta(lightPositionVS.yz, lightDirectionVS.yz, r, coneU.yz, coneV.yz); // 传入底面圆在YZ平面上的投影所对应的圆锥曲线，计算过光源位置的直线在圆锥曲线上的两个切点。 （即圆锥曲线在View空间中的Y方向上的极大，极小值）
                  var p0Y = EvaluateNearConic(near, lightPositionVS, lightDirectionVS, r, coneU, coneV, thetaY.x);
                  var p1Y = EvaluateNearConic(near, lightPositionVS, lightDirectionVS, r, coneU, coneV, thetaY.y);
                  if (ConicPointIsValid(p0Y)) ExpandY(p0Y);
                  if (ConicPointIsValid(p1Y)) ExpandY(p1Y);

                  // Find the X bounds of the near-plane cone intersection, i.e. where x' = 0
                  var thetaX = FindNearConicTangentTheta(lightPositionVS.xz, lightDirectionVS.xz, r, coneU.xz, coneV.xz);
                  var p0X = EvaluateNearConic(near, lightPositionVS, lightDirectionVS, r, coneU, coneV, thetaX.x);
                  var p1X = EvaluateNearConic(near, lightPositionVS, lightDirectionVS, r, coneU, coneV, thetaX.y);
                  if (ConicPointIsValid(p0X)) ExpandY(p0X);
                  if (ConicPointIsValid(p1X)) ExpandY(p1X);
              }

              // o, d, u and v are expected to contain {x or y, z}. I.e. pass in x values to find tangents where x' = 0
              // Returns the two theta values as a float2.
              static float2 FindNearConicTangentTheta(float2 o, float2 d, float r, float2 u, float2 v)
              {
                //这里求的是从光源到底面圆的投影的切线，所以只与圆锥的形状(r决定)，方向(d决定)有关。又因为希望返回的是角度值，所以跟底面的参数（u，v）有关。
                  var sqrt = math.sqrt(square(d.x) * square(u.y) + square(d.x) * square(v.y) - 2f * d.x * d.y * u.x * u.y - 2f * d.x * d.y * v.x * v.y + square(d.y) * square(u.x) + square(d.y) * square(v.x) - square(r) * square(u.x) * square(v.y) + 2f * square(r) * u.x * u.y * v.x * v.y - square(r) * square(u.y) * square(v.x)); // 等于 dXu + dXv - r^2 * (uXv)。 该值大于0时，相机在圆锥外，存在两个切点。
                  var denom = d.x * v.y - d.y * v.x - r * u.x * v.y + r * u.y * v.x;
                  return 2 * math.atan((-d.x * u.y + d.y * u.x + math.float2(1, -1) * sqrt) / denom);
              }

              static float3 EvaluateNearConic(float near, float3 o, float3 d, float r, float3 u, float3 v, float theta)
              {
                  var h = (near - o.z) / (d.z + r * u.z * math.cos(theta) + r * v.z * math.sin(theta)); // 放大的系数。
                  return math.float3(o.xy + h * (d.xy + r * u.xy * math.cos(theta) + r * v.xy * math.sin(theta)), near);
              }
              ```
              ![20250626113952](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250626113952.png)
            -  计算相机到圆锥的两条切线：
              ``` C#
                // Calculate the lines making up the sides of the cone as seen from the camera. `l1` and `l2` form lines
                // from the light position.
                GetConeSideTangentPoints(lightPositionVS, lightDirectionVS, cosHalfAngle, baseRadius, coneHeight, range, coneU, coneV, out var l1, out var l2);

                // Inside the above function there is a check:
                // if (math.dot(math.normalize(-vertex), axis) >= cosHalfAngle)
                //    return;
                // So l1 and l2 can be zero vectors.

                // Check for division by 0
                if ((l1.x != 0.0f) && (l1.y != 0.0f) && (l1.z != 0.0f)) 
                {
                    var planeNormal = math.float3(0, 1, viewPlaneBottoms[m_ViewIndex]);
                    var l1t = math.dot(-lightPositionVS, planeNormal) / math.dot(l1, planeNormal);
                    var l1x = lightPositionVS + l1 * l1t;
                    if (l1t >= 0 && l1t <= 1 && l1x.z >= near) ExpandY(l1x);
                }

                // Check for division by 0
                if ((l2.x != 0.0f) && (l2.y != 0.0f) && (l2.z != 0.0f))
                {
                    var planeNormal = math.float3(0, 1, viewPlaneTops[m_ViewIndex]);
                    var l1t = math.dot(-lightPositionVS, planeNormal) / math.dot(l1, planeNormal);
                    var l1x = lightPositionVS + l1 * l1t;
                    if (l1t >= 0 && l1t <= 1 && l1x.z >= near) ExpandY(l1x);
                }
              ```  
          - 
  - `TileRangeExpansionJob`: 将`TilingJob`中计算的结果写入`m_TileMasks`中。 遍历各个Y方向的Tile分区（遍历Tile行）
    - 遍历各个Local lights，记录该Y方向的Tile分区上，各个Local lights在X方向的Tile分区上的影响的范围（itemRanges）。（剔除在该Y方向Tile分区没影响的光源/反射探针）
      ``` C#
      var rowIndex = jobIndex % tileResolution.y;
      var viewIndex = jobIndex / tileResolution.y;
      var compactCount = 0;
      var itemIndices = new NativeArray<short>(itemsPerTile, Allocator.Temp);
      var itemRanges = new NativeArray<InclusiveRange>(itemsPerTile, Allocator.Temp);

      // Compact the light ranges for the current row.
      for (var itemIndex = 0; itemIndex < itemsPerTile; itemIndex++)
      {
          var range = tileRanges[viewIndex * rangesPerItem * itemsPerTile + itemIndex * rangesPerItem + 1 + rowIndex];
          if (!range.isEmpty)
          {
              itemIndices[compactCount] = (short)itemIndex;
              itemRanges[compactCount] = range;
              compactCount++;
          }
      }
      ``` 
    - 遍历X方向上的各个Tile，提取itemRanges，itemIndices，查看该Tile的X序号是否在range内，如果有，则确认该Tile受该光源的影响。记录在m_TileMasks的一个bit上。
      ```C#
      var rowBaseMaskIndex = viewIndex * wordsPerTile * tileResolution.x * tileResolution.y + rowIndex * wordsPerTile * tileResolution.x;
      for (var tileIndex = 0; tileIndex < tileResolution.x; tileIndex++)
      {
          var tileBaseIndex = rowBaseMaskIndex + tileIndex * wordsPerTile;
          for (var i = 0; i < compactCount; i++)
          {
              var itemIndex = (int)itemIndices[i];
              var wordIndex = itemIndex / 32;
              var itemMask = 1u << (itemIndex % 32);
              var range = itemRanges[i];
              if (range.Contains((short)tileIndex))
              {
                  tileMasks[tileBaseIndex + wordIndex] |= itemMask;
              }
          }
      }
      ```
- SetupLights: 开启Deferred+时，
  - m_ReflectionProbeManager.UpdateGpuData： 更新反射探针的数据。
  - 清理数据。
  - 上传两个CBuffer `urp_ZBinBuffer`, `urp_TileBuffer`。
    ```C#
    using (new ProfilingScope(m_ProfilingSamplerFPUpload))
    {
        m_ZBinsBuffer.SetData(m_ZBins.Reinterpret<float4>(UnsafeUtility.SizeOf<uint>()));
        m_TileMasksBuffer.SetData(m_TileMasks.Reinterpret<float4>(UnsafeUtility.SizeOf<uint>()));
        cmd.SetGlobalConstantBuffer(m_ZBinsBuffer, "urp_ZBinBuffer", 0, UniversalRenderPipeline.maxZBinWords * 4);
        cmd.SetGlobalConstantBuffer(m_TileMasksBuffer, "urp_TileBuffer", 0, UniversalRenderPipeline.maxTileWords * 4);
    }
    ``` 
  - 上传Cluster相关的参数 `_FPParams0`, `_FPParams1`, `_FPParams2`. 
    ```
    _FPParams0：
      x:
      y:
      z:
      w:
    ```

### GBuffer（以 `Lit.shader`为例）: 
- Pass "GBuffer" 使用 keyword `USE_CLUSTER_LIGHT_LOOP` 
  - 计算 `GetMainLight`时，因为光源的可见性已经在计算Cluster时确定。后续着色计算时，不需要依赖引擎内置的 `unity_LightData.z` 判断光源是否被剔除（掩码过滤）。
    ```C#
    #if USE_CLUSTER_LIGHT_LOOP
    #if defined(LIGHTMAP_ON) && defined(LIGHTMAP_SHADOW_MIXING)
        light.distanceAttenuation = _MainLightColor.a;
    #else
        light.distanceAttenuation = 1.0;
    #endif
    #else
        light.distanceAttenuation = unity_LightData.z; // unity_LightData.z is 1 when not culled by the culling mask, otherwise 0.
    #endif
    ``` 
  - 根据反射探针计算IBL时，如果开启`USE_CLUSTER_LIGHT_LOOP`， 会遍历该像素在cluster中记录的各个reflection probe，直到权重已满0.99。 否则，只采样固定的两个默认的探针unity_SpecCube0，unity_SpecCube1。
    ```C#
    half3 CalculateIrradianceFromReflectionProbes(half3 reflectVector, float3 positionWS, half perceptualRoughness, float2 normalizedScreenSpaceUV)
    {
        half3 irradiance = half3(0.0h, 0.0h, 0.0h);
        half mip = PerceptualRoughnessToMipmapLevel(perceptualRoughness);
    #if USE_CLUSTER_LIGHT_LOOP && defined(_REFLECTION_PROBE_ATLAS)
        float totalWeight = 0.0f;
        uint probeIndex;
        ClusterIterator it = ClusterInit(normalizedScreenSpaceUV, positionWS, 1);
        [loop] while (ClusterNext(it, probeIndex) && totalWeight < 0.99f)
        {
          // ...
          float4 scaleOffset0 = urp_ReflProbes_MipScaleOffset[probeIndex * 7 + (uint)mip0];
          float4 scaleOffset1 = urp_ReflProbes_MipScaleOffset[probeIndex * 7 + (uint)mip1];

          half3 irradiance0 = half4(SAMPLE_TEXTURE2D_LOD(urp_ReflProbes_Atlas, sampler_LinearClamp, uv * scaleOffset0.xy + scaleOffset0.zw, 0.0)).rgb;
          half3 irradiance1 = half4(SAMPLE_TEXTURE2D_LOD(urp_ReflProbes_Atlas, sampler_LinearClamp, uv * scaleOffset1.xy + scaleOffset1.zw, 0.0)).rgb;
          // ...
        }
    #else
        // ...

        // Sample the first reflection probe
        if (weightProbe0 > 0.01f)
        {
          // ...
          half4 encodedIrradiance = half4(SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflectVector0, mip));
          // ...
        }

        // Sample the second reflection probe
        if (weightProbe1 > 0.01f)
        {
          // ...
          half4 encodedIrradiance = half4(SAMPLE_TEXTURECUBE_LOD(unity_SpecCube1, samplerunity_SpecCube1, reflectVector1, mip));
          // ...
        }
    #endif
    // ...
    }
    ``` 
    >反射探针的权重由像素到包围盒上所有的面的距离共同决定： 权重 = min( 像素到各面的距离 / Blend Distance, 1.0 - totalWeight或desiredWeightProbe )，当像素到面距离从0~Blend Distance时， 权重从 0~100%之间变化。 而当存在Blend Distance 超过面间距一半时，该probe不存在一个位置可以使权重到达100%。
- DeferredLight：  
  - PrecomputeLights: 不开启Deferred+时， Deferred管线会在CPU侧根据光源类型对additional lights进行排序，并值保留Spot，Light，Directional三种光源。
  - ClusterDeferred和StencilDeferred: 开启Deferred+时，进行光照计算时会使用 **ClusterDeferred.shader**。 而Deferred时，使用**StencilDeferred.shader**.
    - 顶点着色器阶段: 使用**ClusterDeferred.shader**时，使用覆盖全屏的三角形进行处理。 因为后续像素可以根据Cluster中知道有哪些光源参与着色，不用担心做了多余的着色。
      > **StencilDeferred.shader** : 会根据光源类型使用不同的Shader变体，及绘制用的几何体。 保证该光源仅覆盖其影响的像素。 
      Directional light影响全局，mesh使用覆盖全屏的三角形； Point Light为球体； Spot Light为半球体。
      ``` C# 
      internal void ExecuteDeferredPass(RasterCommandBuffer cmd, UniversalCameraData cameraData, UniversalLightData lightData, UniversalShadowData shadowData)
      {
        // ...
        if (m_UseDeferredPlus)
            RenderClusterLights(cmd, shadowData);
        else
            RenderStencilLights(cmd, lightData, shadowData, cameraData.renderer.stripShadowsOffVariants);
        // ...
      }

      void RenderClusterLights(RasterCommandBuffer cmd, UniversalShadowData shadowData)
      {
        // ...
        cmd.DrawMesh(m_FullscreenMesh, Matrix4x4.identity, m_ClusterDeferredMaterial, 0, m_ClusterDeferredPasses[(int)ClusterDeferredPasses.ClusteredLightsLit]);

        // ...
      }

      void RenderStencilLights(RasterCommandBuffer cmd, UniversalLightData lightData, UniversalShadowData shadowData, bool stripShadowsOffVariants)
      {
        // ...
        if (HasStencilLightsOfType(LightType.Directional))
            RenderStencilDirectionalLights(cmd, stripShadowsOffVariants, lightData, shadowData, visibleLights, hasAdditionalLightPass, hasLightCookieManager, lightData.mainLightIndex);

        if (lightData.supportsAdditionalLights)
        {
            if (HasStencilLightsOfType(LightType.Point))
                RenderStencilPointLights(cmd, stripShadowsOffVariants, lightData, shadowData, visibleLights, hasAdditionalLightPass, hasLightCookieManager);

            if (HasStencilLightsOfType(LightType.Spot))
                RenderStencilSpotLights(cmd, stripShadowsOffVariants, lightData, shadowData, visibleLights, hasAdditionalLightPass, hasLightCookieManager);
        }
        // ...
      }
      ```
    - 片元着色器阶段: 
      - 光源计算顺序： 开启Deferred+时，先计算MainLight，然后是additional lights中的Spot/Point Lights，然后是additional lights中的directional light。 而在Deferred中，顺序为MianLight， additional lights中的directional light， additional lights中的Spot/Point Lights。 
        > 注释中提到Deferred+的写法是为了避免FXC编译时的警告。 
        ``` c#
        // Main light
        Light mainLight = GetMainLight();
        mainLight.distanceAttenuation = 1.0;
        bool materialReceiveShadowsOff = (gBufferData.materialFlags & kMaterialFlagReceiveShadowsOff) != 0;
        // ...
        color += DeferredLightContribution(mainLight, inputData, gBufferData);

        // Additional light loop
        // We do additional directional lights last because otherwise FXC complains...
        uint pixelLightCount = GetAdditionalLightsCount();
        LIGHT_LOOP_BEGIN(pixelLightCount)
          // ...
          // Spot/Point Lights
          color += DeferredLightContribution(light, inputData, gBufferData);
        LIGHT_LOOP_END

        UNITY_LOOP for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
        {
          // ...
          // Directional lights
          color += DeferredLightContribution(light, inputData, gBufferData);
        }
        ```  
      - 计算Spot/Point lights: 根据片元的屏幕空间，世界空间位置从Cluster中读取光源信息。
        将 `LIGHT_LOOP_BEGIN`, `URP_FP_DIRECTIONAL_LIGHTS_COUNT`, `CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK`登定义进行转换。
        **ClusterDeferred.shader**中关于Spot/Point lights的计算部分的代码等价于以下代码。
        ``` C
        uint lightIndex;
        ClusterIterator _urp_internal_clusterIterator = ClusterInit(inputData.normalizedScreenSpaceUV, inputData.positionWS, 0);
        [loop] while (ClusterNext(_urp_internal_clusterIterator, lightIndex)) 
        {
          lightIndex += ((uint)_FPParams0.w)；
          if (_AdditionalLightsColor[lightIndex].a > 0.0h) continue;
          Light light = GetAdditionalLight(lightIndex, inputData, gBufferData.shadowMask, aoFactor);

          UNITY_BRANCH if (materialReceiveShadowsOff)
          {
              light.shadowAttenuation = 1.0;
          }

          color += DeferredLightContribution(light, inputData, gBufferData);
        }
        ```
        - ClusterInit： 
          - 根据屏幕UV计算对应的在TileBuffer上的位置
            ``` C
            uint2 tileId = uint2(normalizedScreenSpaceUV * URP_FP_TILE_SCALE);
                state.tileOffset = tileId.y * URP_FP_TILE_COUNT_X + tileId.x;
            #if defined(USING_STEREO_MATRICES)
                state.tileOffset += URP_FP_TILE_COUNT * unity_StereoEyeIndex;
            #endif
                state.tileOffset *= URP_FP_WORDS_PER_TILE;
            ```
          - 计算View空间下的深度，找到对应在ZbinBuffer上的位置。zBinBaseIndex 代表所在的zbin区块的headindex，向后跳过2个element才是开始记录受影响光源的信息
            ``` c
            float viewZ = dot(GetViewForwardDir(), positionWS - GetCameraPositionWS());
            uint zBinBaseIndex = (uint)((IsPerspectiveProjection() ? log2(viewZ) : viewZ) * URP_FP_ZBIN_SCALE + URP_FP_ZBIN_OFFSET);
            // The Zbin buffer is laid out in the following manner:
            //                          ZBin 0                                      ZBin 1
            //  .-------------------------^------------------------. .----------------^-------
            // | header0 | header1 | word 1 | word 2 | ... | word N | header0 | header 1 | ...
            //                     `----------------v--------------'
            //                            URP_FP_WORDS_PER_TILE
            //
            // The total length of this buffer is `4*MAX_ZBIN_VEC4S`. `zBinBaseIndex` should
            // always point to the `header 0` of a ZBin, so we clamp it accordingly, to
            // avoid out-of-bounds indexing of the ZBin buffer.
            zBinBaseIndex = zBinBaseIndex * (2 + URP_FP_WORDS_PER_TILE);
            zBinBaseIndex = min(zBinBaseIndex, 4*MAX_ZBIN_VEC4S - (2 + URP_FP_WORDS_PER_TILE));

            uint zBinHeaderIndex = zBinBaseIndex + headerIndex;
            state.zBinOffset = zBinBaseIndex + 2;
            ``` 
            > zBinHeaderIndex / 4 使用element格式为float4，相当于一个element中存储了四个uint。element数量是c#层申请uint native array的 1/4.
        - ClusterNext: 
          - 当MAX_LIGHTS_PER_TILE > 32，即光源数量大于32个时。 entityIndexNextMax的后16位记录着maxIndex，需要计算的光源的最大数量。 entityIndexNextMax的前16位则记录当前读取的wordIndex，即正在读取wordIndex个 32light。
          - 该32个light结束渲染后，while (ClusterNext(_urp_internal_clusterIterator, lightIndex)) 会判断是否存在下一个32个light. 如果当前 entityIndexNextMax 记录的 wordIndex * 32 不大于 _urp_internal_clusterIterator.entityIndexNextMax 记录的最大light序号的话，会尝试读取下一个word的32个光源。
- ForwardOnly: 
  - 目前Target为ScalableLit 和 Fabric 的shadergraph 不支持Gbuffer的结构，会使用ForwardOnly. 此外Unlit的shader会走Gbuffer的渲染，但不会参与deferredLighting。其也在ForwardOnly阶段渲染。![20250321175443](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250321175443.png) -》 在延迟渲染中，GBuffer 存储了场景的几何信息（如法线、深度、材质属性等）。如果某些物体（如 Unlit 物体）不写入 GBuffer，会导致 GBuffer 中出现“空洞”（即缺失数据区域）。？？
  这里以ComplexLit为例： 走 half4 UniversalFragmentPBR(InputData inputData, SurfaceData surfaceData)： 开启 USE_CLUSTER_LIGHT_LOOP， 先计算mainLight的LightingPhysicallyBased，再算 additional directional light， 最后算其他的additional light