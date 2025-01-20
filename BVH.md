- def: 包围体层次结构(BVH，Bounding Volume Hierarchy)是一组几何对象上的树结构.形成树的叶节点的所有几何对象都被包裹在包围体（AABB）中。这些节点被分组为小集合，以递归方式分组并封闭在其他较大的包围体中，最终产生在树顶部具有单个包围体的树结构。![v2-b3bc853cc79e627f3f0557e1a2e17b0f_r](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/v2-b3bc853cc79e627f3f0557e1a2e17b0f_r.png)
- BVH Node Structure: 
  - AABB 
  - if not a leaf node: left and right child node
  - if a leaf node: primitives -> usually use the indices of triangles in the triangle array.

- k-d tree: 通过寻找方差最大的轴来分割物体。统计数据点在每个维度上的数据方差。挑选出方差中的最大值，对应的维就是分割域的值。 e.g.: 如果X轴的方差大于Y轴，Z轴，就在X轴上进行分割。
  - 是“自上而下”（top-down）的构建方式，需要知道场景所有物体的信息。
    - pros：懒初始化：需要时再构建
    - cons：耗时长
    - https://zhuanlan.zhihu.com/p/697130257

- 动态调整BVH
  - 叶子节点 《-》分支节点
  - 合并： e.g. 合并节点A和节点B -》 复制节点A为A'， ![v2-0d754850de3ce9b7b58003bb52ab3b2a_r](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/v2-0d754850de3ce9b7b58003bb52ab3b2a_r.png)
  - 分离： e.g. 分离节点B， 获取节点B的兄弟节点A，将节点A作为父节点 ![v2-aae53b60de17c60248e95279b4fa58eb_r](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/v2-aae53b60de17c60248e95279b4fa58eb_r.png)

- 一种特殊情况： 当两个三角形构成了一个矩形时，他们的aabbb是完全重合的，也无法进行分割。

- SAH（Surface Area Heuristic）: 计算最合适的插入的目标节点
  - formula: $C_{ASH} = N_{left} * A_{left} +  N_{right} * A_{right}$, C: Cost; N: num of the triangles; A: surface area of the box; (乘以数量是因为要"考虑包围盒被击中时需要进行的后续相交测试"的成本)
    - 分割轴需要从XYZ三个方向上都进行尝试，但为了性能考虑也可以直接选择最长轴进行分割。
  - 前提： 
    - 光线击中AABB的概率和表面积有关-》AABB的表面积最小时是最合适的AABB划分
    - root节点与叶子节点的AABB和任何时候都相等。
  - 结论： 插入节点时，总的变化表面积最小的目标节点是最合适的。
    - 【变化表面积】=【新增的分支节点的表面积】+【插入后所有祖先节点的表面积差】。
    - ![v2-b36f7412f37695de8ab26a73e6bcf494_r](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/v2-b36f7412f37695de8ab26a73e6bcf494_r.png)
    - 考虑到不同插入对象共有的祖先的面积变化是相同的，所以只需要考虑非公有的祖先节点的面积变化？

- Binned BVH : 尝试按照固定的的距离分割aabb，计算该分割后是否cost更低，是的话就分割。可以将构建BVH的复杂度从 $O(n^2)$ 降低到 $O(An)$ (A为分割的份数)。
  ![20250120144300](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250120144300.png)

- Refitting and rebuilding: for animated models
  - refitting: use same bvh tree, update bounds only. -> from leaf to root, in other words, read the nodes from the end of the list of bvhNodes.
    Ensuring never visit an interior node with outdated child nodes, as the index of a child is always greater than the index of its parent
    ![20250120152642](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250120152642.png)
    - application: fast, but only for subtle animation. 
  - Rebuild: regenerate bvh tree.

- TLAS(top level acceleration structure) and BLAS(bottom ~): for fully dynamic scene.  -》 for instancing？
  - transformed BVHs: 当场景中存在多个相同的模型是，可以重复利用其BVH。 通过矩阵每帧计算其bvh在世界空间的aabb盒来重复利用？
  - TLAS: 当场景中有许多的bvh时， 使用一个TLAS Node来包含场景中所有的BVH。 
    - structure： TLAS Node类似于BVHNode，除了triangle array被替换成了BVH array。这里每个bvh 都是一个BLAS。
  -  two-level acceleration structure：
     -  the top-level AS that will contain the object instances, each one with its own transformation matrix.
     -  BLAS then hold the actual vertex data of each object
     -  一个TLAS可以引用任意数量的BLAS，但是TLAS只能引用BLAS，无法引用其他的TLAS，所以不存在TLAS套TLAS的结构。 BLAS中的是真实的顶点数据。通常来说，BLAS越少越好。 TLAS会包含物体的实例，它对每个关联的BLAS都会有一个转换矩阵配套。
        ![20250120173611](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250120173611.png)
  - Cost： 
    Building the Bottom Level Acceleration Structures for dynamically deforming meshes, like skinned meshes and hair.
    Building the Top Level Acceleration Structure for the scene and the Shader Binding Table (SBT).
    Ray traversal for each feature that uses Ray Tracing.
  - BLAS Update： Once for static meshes, every frame for deforming meshes.



- Movement: 物体移动： 先删除物体对应的叶子节点，然后计算该物体的插入。
  - 可以用一个较为宽松的AABB来包裹物体，只有超出一点范围的移动才会触发BVH的更新。

- Light Intersection: 
  - [Möller–Trumbore intersection algorithm](https://en.wikipedia.org/wiki/M%C3%B6ller%E2%80%93Trumbore_intersection_algorithm): Only need the vertices of triangles
  - [slightly faster approaches](http://www.sven-woop.de/papers/2004-Diplom-Sven-Woop.pdf) : faster but need more data.


- reference： 
  - https://dev.epicgames.com/documentation/en-us/unreal-engine/ray-tracing-performance-guide-in-unreal-engine#overviewofraytracingcosts
  - https://jacco.ompf2.com/2022/05/07/how-to-build-a-bvh-part-5-tlas-blas/
  - https://zhuanlan.zhihu.com/p/687720932