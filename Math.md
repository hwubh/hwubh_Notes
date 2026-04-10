- 1： Viewing Transformation:
  - https://zhuanlan.zhihu.com/p/144329075
  - https://zhuanlan.zhihu.com/p/122411512
  - https://zhuanlan.zhihu.com/p/65969162
  - Orthgraphic：Consider scale, then translation (n+f/2（centre） * 2/f-n（scale） = n+f/f-n)
  - Perspective: 
- 2：剔除Culling与剪裁Clipping： https://blog.csdn.net/qq_33744693/article/details/88704309
- 3：透射校正插值(Perspetive-Correct-Interpolation)： 
  - https://paroj.github.io/gltut/Texturing/Tut14%20Interpolation%20Redux.html 
  - https://www.scratchapixel.com/lessons/3d-basic-rendering/rasterization-practical-implementation/visibility-problem-depth-buffer-depth-interpolation.html
  - https://blog.csdn.net/seizeF/article/details/92760068
- 4：Filter： https://www.cnblogs.com/cxrs/archive/2009/10/18/JustAProgramer.html‘
- 5：Reverse-z: ；在View Space下对应的平面尽量均匀分布，移动视野等过度相对平滑
  - 浮点数的分布是不均匀，越靠*0的浮点数分布越密集 + 近平面精度较高 https://developer.nvidia.com/blog/visualizing-depth-precision/
- 6：Early-Z Culling和HiZ
  - https://blog.csdn.net/yinfourever/article/details/109822330
- 7：OIT: https://blog.csdn.net/qq_35312463/article/details/115827894
- 8：PBR: https://zhuanlan.zhihu.com/p/53086060
  - Def: 使用基于物理原理和微平面理论建模的着色/光照模型: ![20260309164409](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20260309164409.png)
  - 光与非光学平坦表面的交互： 如何区分漫反射和次表面散射： 如果像素大小远大于散射距离，则可以把次表面散射近似为漫反射，使用BRDF。 如果像素小于散射距离，为了更真实的着色效果，就需要当作次表面散射现象进行处理， 使用BSDF(BSDF = BRDF + BTDF)?![20260309165303](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20260309165303.png)
  - 物理原理：  基于物理的材质（Material）；基于物理的光照（Lighting）；基于物理适配的摄像机（Camera）
  - 渲染方程的物理基础是能量守恒定律。在一个特定的位置和方向，出射光 Lo 是自发光 Le 与反射光线之和，反射光线本身是各个方向的入射光 Li 之和乘以表面反射率及入射角。 
    - 反射方程(The Reflectance Equation)，则是渲染方程的简化的版本，或者说是一个特例。  -》迪士尼原则的BxDF。
    ![20260309165742](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20260309165742.png) 
  - Diffuse BRDF：常数，一般用lambert？ urp中等于albedo / $\pi$
  - Specular BRDF: 微平面理论（microfacet theory）的Microfacet Cook-Torrance BRDF -> DFG / 4(n·l)(n·v)
    - D：GGX： 描述微表面法线N和半角H同向性的比重，粗糙度越高，物体表面越粗糙，N，H同向性越低（反射越不清晰）：![20240614092732](https://raw.githubusercontent.com/hwubh/hwubh_Pictures/main/20240614092732.png)
    - G：k:NdotL的过去系数：k = pow(1+roughness,2)*0.5, 粗糙度越高，G值越小![20240614092948](https://raw.githubusercontent.com/hwubh/hwubh_Pictures/main/20240614092948.png)
    - F：光线不同角度入射会有不同反射率：非金属的反射率多在：0.02~0.04；金属的反射率多在：0.7~1.0；金属度越高，F值越大（Lerp = （0.04，albedo，metallic））; 入射角越大， F 越大。 ![20240614094220](https://raw.githubusercontent.com/hwubh/hwubh_Pictures/main/20240614094220.png)
  - Physically Based Environment Lighting
    - Diffuse: Irradiance Environment Mapping, 自变量：Normal。 URP中使用球谐函数来计算。 存储1+3+5三阶共9个球谐系数。
    ```
    // 简化版的 SH 计算逻辑
    half3 res = unity_SHAr.www; // L0 常数项
    res += unity_SHAr.xyz * N.x + unity_SHAg.xyz * N.y + unity_SHAb.xyz * N.z; // L1 线性项
    res += unity_SHBr.xyz * (N.x * N.y) + ...; // L2 二次项
    ``` 
    - Specular: 使用Split Sum Approximation将蒙特卡洛积分公式拆为两个部分：将镜面反射函数的求解，通过蒙特卡洛积分转化为有限个样本的求和。然后将该求和分割为两个和式的乘积（split sum approximate），分布求和式（Pre-Filtered Environment Map） 和 和式（Environment BRDF）。
      - Prefiltered Environment Map （LD项，Radiance (L) × Distribution (D)） -》 取决于（自变量）反射方向 (ωr​) 和 粗糙度 (α) -> 储存在反射探针中，每级mip对应一个粗糙度。反射方向对应UV。 
      > D项只是为了确定重要性采样的pdf而引入的？？？
        - Def： 以该方向为中心，基于特定波束宽度（由粗糙度决定）的“加权亮度平均值”。 -> 粗糙度决定lobe的大小。 GGX影响不同反射方向采样结果的权重。
      - Environment BRDF（DFG项，分布 (D)、菲涅尔 (F) 和 几何 (G)）： 存储在LUT图中，记录F的scale和G的bias。通过cosθv​ (N⋅V) 和 粗糙度 (α)进行查找。 
        - URP中使用拟合函数（Karis）来模拟：
        ``` C
        // Computes the specular term for EnvironmentBRDF
        half3 EnvironmentBRDFSpecular(BRDFData brdfData, half fresnelTerm)
        {
            float surfaceReduction = 1.0 / (brdfData.roughness2 + 1.0); // G项的模拟
            return surfaceReduction * lerp(brdfData.specular, brdfData.grazingTerm,  fresnelTerm); // G项的模拟 * F项
        }

        half3 EnvironmentBRDF(BRDFData brdfData, half3 indirectDiffuse, half3 indirectSpecular, half fresnelTerm)
        {
            half3 c = indirectDiffuse * brdfData.diffuse;
            c += indirectSpecular * EnvironmentBRDFSpecular(brdfData, fresnelTerm);
            return c;
        }
        ``` 
- 9：延迟渲染：https://zhuanlan.zhihu.com/p/102134614
- 10: GI: IBL/ PRT/ Light Probe/ Lightmap: https://juejin.cn/post/7026291302547324964#heading-1
- 11: 资源处理：AssetPostProcesser
- 12：法线贴图与切线空间：https://zhuanlan.zhihu.com/p/261667233
  ![20240613200017](https://raw.githubusercontent.com/hwubh/hwubh_Pictures/main/20240613200017.png)
- 13: OBB intersection: https://busyogg.github.io/article/3c9cb66ca768/ https://gamedev.stackexchange.com/questions/44500/how-many-and-which-axes-to-use-for-3d-obb-collision-with-sat
  ``` C++ OBB-OBB
  inline float* GetProjectionLimit(const Vector3f* vertice, const Vector3f axis)
  {
      float* result = new float[2] { std::numeric_limits<float>::max(), std::numeric_limits<float>::min()};
      for (int i = 0; i < 8; i++)
      {
          Vector3f vertext = vertice[i];
          float dot = Dot(vertext, axis);
          result[0] = dot < result[0] ? dot : result[0];
          result[1] = dot > result[1] ? dot : result[1];
      }
      return result;
  }

  bool IntersectOBBOBB(const Vector3f* AABBVertice, const Vector3f* OBBVertice)
  {

      Vector3f b1Sides[3];
      b1Sides[0] = OBBVertice[0] - OBBVertice[1];
      b1Sides[1] = OBBVertice[0] - OBBVertice[2];
      b1Sides[2] = OBBVertice[0] - OBBVertice[4];


      Vector3f seperationAixs[15];
      seperationAixs[0] = Vector3f(1, 0, 0);
      seperationAixs[1] = Vector3f(0, 1, 0);
      seperationAixs[2] = Vector3f(0, 0, 1);
      seperationAixs[3] = Cross(b1Sides[0], b1Sides[1]);
      seperationAixs[4] = Cross(b1Sides[0], b1Sides[2]);
      seperationAixs[5] = Cross(b1Sides[1], b1Sides[2]);
      for(int i = 1; i < 4; i++)
      {
          for(int j = 1; j < 4; j++)
          {
              seperationAixs[2 + i * 3 + j] = Cross(seperationAixs[i - 1], b1Sides[j - 1]);
          }
      }

      for (int i = 0; i < 15; i++)
      {
          float* limit1 = GetProjectionLimit(AABBVertice, seperationAixs[i]);
          float* limit2 = GetProjectionLimit(OBBVertice, seperationAixs[i]);
          float limit1Min = limit1[0];
          float limit1Max = limit1[1];
          float limit2Min = limit2[0];
          float limit2Max = limit2[1];

          if (limit1[0] > limit2[1] || limit2[0] > limit1[1])
          {
              return false;
          }
      }
      return true;
  }
  ``` 

  OBB-Planes
  ``` c++
  bool IntersectOBBPlaneBounds(const AABB& aabb, const Matrix4x4f& rotation, const Plane* p, const int planeCount)
  {
      Vector3f obbCenter = aabb.GetCenter();
      Vector3f obbExtent = aabb.GetExtent();
      Vector3f obbAxes[3] =
      {
          Normalize(rotation.GetAxisX()),
          Normalize(rotation.GetAxisY()),
          Normalize(rotation.GetAxisZ()),
      };

      for (int i = 0; i < planeCount; ++i, ++p)
      {
          const Vector3f& normal = p->GetNormal();
          float dist = p->GetDistanceToPoint(obbCenter);
          Vector3f absNormal = Abs(normal);
          float radius = obbExtent.x * Abs(Dot(obbAxes[0], normal))
                          + obbExtent.y * Abs(Dot(obbAxes[1], normal))
                          + obbExtent.z * Abs(Dot(obbAxes[2], normal));
          if (dist + radius < 0)
              return false; 
      }
      return true;
  }
  ```
  - 计算AABB与面的最近距离时，可以通过Dot(extent, Abs(normal))来得到extent在normal的最长投影。原理可以考虑到extent与abs(normal)在各个分量均为整数，其计算结果可以视为extent各个分量的normal上的投影取abs()后的和。
- 14: 蒙特卡洛积分，重要性采样和GGX： https://zhuanlan.zhihu.com/p/1959722671534223757 https://zhuanlan.zhihu.com/p/361227286 https://zhuanlan.zhihu.com/p/338103692 https://zhuanlan.zhihu.com/p/695130713 https://zhuanlan.zhihu.com/p/41217212  https://zhuanlan.zhihu.com/p/360420413  https://developer.nvidia.com/gpugems/gpugems3/part-iii-rendering/chapter-20-gpu-based-importance-sampling https://www.cnblogs.com/minggoddess/p/14645677.html https://www.cnblogs.com/dydx/p/8635923.html https://patapom.com/blog/BRDF/PreIntegration/#stating-the-problems https://zhuanlan.zhihu.com/p/104422026 https://agraphicsguynotes.com/posts/sample_microfacet_brdf/ https://www.zhihu.com/question/546947425

- 15: 屏占比算法 https://zhuanlan.zhihu.com/p/657320510
  - AABB:
    - 算Centre 和 Extends的方法： 分别算三个面的面积，然后根据角度和Z方向平方，等比缩小为到屏幕上的面积。 -》 存在误差，没考虑FOV和分辨率？ -》 得到的结果是个权重值，而不是实际的像素数量。
    - 算顶点： 将顶点投影到屏幕空间，然后在XY方向上找到Rect进行包裹。然后根据屏幕边界进行Clamp。
  - Sphere:
    - 将AABB简化为球体，然后计算这个球体在投影平面上的“表现直径”。 将圆的半径经过FOV进行比例缩放，返回根据圆心的深度进行缩放。 因为切点和使用的distance是圆心的缘故，会比实际的的包围球到屏幕空间的投影小一些。
  - Unity，相对高度：
    - 计算物体包围球/AABB盒的半长在垂直方向上的占据屏幕的比例（算法参考根据深度缩放），然后根据FOV对应到水平方向。
  > 根据深度缩放的思路是：以视锥体，物体中心，垂直/水平方向的长度构建直角三角形。然后根据相似三角形进行缩放。 