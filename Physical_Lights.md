# Physical Lights

- Units (https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.3/manual/Physical-Light-Units.html ; https://media.contentapi.ea.com/content/dam/eacom/frostbite/files/s2014-pbs-frostbite-slides.pdf ； https://dev.epicgames.com/documentation/en-us/unreal-engine/using-physical-lighting-units-in-unreal-engine )
  - Candela（cd）: base unit of luminous **intensity（I）** in the International System of Units. -> Intensity
  - Lumen（lm）: unit of luminous **flux（F）**. Describes the total amount of visible light that a light source emits in all directions. -> Power 
  - Lux (**lumen per square meter**， lx): unit of **illuminance（E）**.  A light source that emits 1 lumen of luminous flux onto an area of 1 square meter has an illuminance of 1 lux.
  - Nits (**candela per square meter**， cd/$m^2$): unit of **luminance（L）**. Describes the surface power of a visible light source.
  - Exposure value (EV): A value that represents a combination of a camera's shutter speed and f-number.
  - Lighting and exposure diagram: ![LightCheatSheet](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/LightCheatSheet.png)
  - UE中一般使用**Lux**来表示平行光，用**Nits**表示自发光和天空光(Sky Light). Point, spot, area light 可以使用**Lumen** 或 **Candela**
  - ![20250702153050](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250702153050.png)
  - 1 cd = 1 lm/sr; 
    > sr:  steradian or square radian: 大小为$r^2$的球面表面积

- Light type (Analytical Lights): punctual (光源是个点) and area : ![20250702154310](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250702154310.png)
  - punctual: follow inverse square law. ![20250702154602](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250702154602.png) -> use **lumen**/**Candela**
  - Photometric: using IES profile: a file format which describes a light's distribution from a light source using real world measured data. -> use **Candela**
  - Area: separate diffuse and specular -> use **lumen**/**Nits**/**EV**?
    - diffuse: ...
  - Sun: diffuse term is simply approximated with a single direc1on; specular term use an oriented disk approxima1on.  -> use **Lux**
  - emissive: not emit light. -> use **Nits**/**EV**
  - Image-based Light: Distant light probe (HDRI/Procedural Sky)/ Local light probes/ screen-spcae reflection/ planar reflections.
    - Distant light probe (HDRI/Procedural Sky): **Nits**/**EV**

- HDRP Lights
  - Directional Lights: 
    - Shape: Angular Diameter -> 视直径 -> 真实世界的太阳并非完美的点光源，而是具有可见的视直径（约0.53度）。Angular Diameter通过控制光线从不同角度投射，在阴影边缘生成半影区（Penumbra）。 -> impact on the size of specular highlights, and the softness of baked, ray-traced, and PCSS shadows.


-----------
移植light unit可能的问题：
DrawGeneralContentInternal
DrawSpotShapeContent

------------
## Exposure Value(EV): 
- def: numerical camera settings of shutter speed and f/stop
- Physical meaning: Light level. 
  - EV值越高 → 光线越强 → 所需曝光量越少。
  - EV是绝对数值（如EV 12），但需结合ISO解读（ISO 100下的EV 12 ≠ ISO 200下的EV 12）
    > URP中EV默认为EV100， 即 ISO 100的情况。
    > EV100 15 = EV200 15 -> 这里的相对说明二者的进光量相同。

- exposure triangle: three factors affect EV
  - **aperture**: The size of the opening in the lens that allows light to pass through. wider -> shallow depth of field (blurry background).
  - **shutter speed**: The length of time the camera's shutter remains open, exposing the sensor to light. fast ->freezes motion
  - **ISO**: The sensitivity of the camera's sensor to light. lower -> ideal for bright conditions, produces less noise

- Relative EV(compensation?):  is the amount of change from the current exposure, like perhaps +1 EV more.
  - **One EV** is a step of **one stop**(±1 EV) compensation value (could be aperture, shutter speed, or ISO, or some combination)
    - Plus or minus half or double Shutter Speed duration is ± 1 EV
    - Plus or minus half or double ISO value is ± 1 EV
    - Plus or minus $\sqrt{2}$ on f/stop is ± 1 EV: e.g.: f/8 —除以$\sqrt{2}$-> f/5.6 —除以$\sqrt{2}$-> f/4 

- reference: 
  - https://www.scantips.com/lights/evchart.html#chart

-----------
## EV in HDRP：
- Fixed: 在一帧的开始处进行处理（因为不需要依赖上一帧的数据）。 -》 DoFixedExposure -> 调用compute shader kernel `KFixedExposure`， 将计算得到的exposure值写入size 为 1*1 的 Texture `_ExposureTexture`(hdCamera.currentExposureTextures.current``)中。  -> 在物体的着色计算阶段(Forward)/Gbuffer阶段读取。
- dynamic: 在后处理阶段进行处理。 结果用于下一帧的渲染。 同样最后写入size 为 1*1 的 Texture `_ExposureTexture`(hdCamera.currentExposureTextures.current``)中。
  - DoDynamicExposure： 应该三个pass，两个kernel `KPrePass`, `KReduction`
    - 1st pass: `KPrePass`: 在一张10249*1024的贴图上
  - DoHistogramBasedExposure