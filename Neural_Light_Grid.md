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
  - 缓存的光学量
    - 光传输结果 irradiance?
    - 光传输过程 radiance?
  - 缓存结构
    - 表面空间上的烘焙 ： 记录物体几何表面对应的烘培结果？
    - 世界空间布点的烘焙 : 记录空间位置对应的烘培结果？ 一般对应 radiance？
- 方案:
  - lightmap：
  - light probe:
  - probe volume （LPPV）
  - Precomputed Radiance Transfer(PRT)


GI 方案可以按照三个维度进行划分:
- 缓存结构
- 缓存的光学量
  - Radiance : W/(m²·sr)， 记录在X位置上，光的方向，光的颜色，光的强度。
    > \[L(x,\omega)=(R,G,B)\] 
  - Irradiance: W/m², 记录在X位置上, 朝向n的表明接受到的，光的颜色，光的强度。 
    > Irradiance 本质为在朝向n的表面上，进行将Radiance(光*入射角余弦)进行半球积分后的结果。
- 计算方式（Final Gather）