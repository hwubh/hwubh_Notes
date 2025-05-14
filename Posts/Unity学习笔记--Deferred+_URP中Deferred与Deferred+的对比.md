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
        - Spot Light还需要考虑底面圆在XY平面上的投影： 先计算底面圆圆心的位置，然后根据XY坐标轴单位向量在底面上的投影，计算出底面在XY平面上的极值点。
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
      - 