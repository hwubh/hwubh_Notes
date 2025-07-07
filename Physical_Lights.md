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