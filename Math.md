- 1： Viewing Transformation:
  - https://zhuanlan.zhihu.com/p/144329075
  - https://zhuanlan.zhihu.com/p/122411512
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
- 14: 蒙特卡洛积分，重要性采样和GGX： 
  - 