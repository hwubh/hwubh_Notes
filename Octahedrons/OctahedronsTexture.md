球面映射方式： Cube mapping， spherical mapping， octahedrons mapping
- Cube mapping: 需要存储六张正方形贴图。
- Spherical mapping: 通过经纬来映射，越靠近两级方向，精度浪费情况越严重。
- Octahedrons mapping: 将一张正方形图片映射到八面体，再映射到球面上。 ->比起球面来说每个像素对应的立体角更加均匀，不会出现太大的扭曲。 -> 比起cube只需要一张图即可。

Octahedrons mapping：
- 映射方式：将一个正方形的四角“对折”，形成两个一样大的正方形。折下去的四个角当作下半球面，剩下的中心部分作为上半球面。然后将这两个正方形映射到八面体的上下两个部分上，然后映射到球面即可。 \
  ![v2-c677e5d7f6bc882a40d8b6e0f0cafb80_r](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/v2-c677e5d7f6bc882a40d8b6e0f0cafb80_r.jpg)
- 公式: $|x| + |y| + |z| = 1$, x,y,z的取值在(-1,1)之间。 当z<0时，需要反转x,y的符号。（Z<0代表下方的四个平面？）
  - 每个像素对应的立体角大小仍不相同: 蓝色的部分为欠采样， 绿色->黄红色的部分为过采样。 \ ![v2-bb49da45b21c03f2b7d9db9f661899a9_r](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/v2-bb49da45b21c03f2b7d9db9f661899a9_r.jpg)
- code： ![v2-df2904bf22b09b898c0d6dc688578de2_r](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/v2-df2904bf22b09b898c0d6dc688578de2_r.jpg)

Concentric Octahedral mapping: Concentric mapping + Octahedral mapping 
- def: 把正方形(上下各四个平面各自形成一个正方形)用 Concentric mapping 映射到同心圆上然后再映射到半球面。 \ ![v2-eb4288937ec3c66d25cf3a2f6dad3602_r](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/v2-eb4288937ec3c66d25cf3a2f6dad3602_r.jpg)
- 公式: ![v2-93833697474604f400792f6f8f136a9c_r](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/v2-93833697474604f400792f6f8f136a9c_r.jpg) https://link.zhihu.com/?target=https%3A//fileadmin.cs.lth.se/graphics/research/papers/2008/simdmapping/clarberg_simdmapping08_preprint.pdf
- 代码: ![v2-42b5b3addd7df9763b7ea089bc47b5d3_r](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/v2-42b5b3addd7df9763b7ea089bc47b5d3_r.jpg)



---------------

- cubeToOctahedral?
- bakeOctahedral
- sampleOctahedral


------------
- reflection probe 集合引擎，baked/custom
- 重要性采样-》 realtime / sky texture
- RGBM

---------
- Skybox的渲染还没支持: ProbeRenderer::Render
- reflectionEditor -> //RRR
- SetReflectionProbeUseOctahedralmap CleanupRenderPipeline 需要吗?
- OnPreSceneGUICallback 要加个mat