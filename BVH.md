- def: 包围体层次结构(BVH，Bounding Volume Hierarchy)是一组几何对象上的树结构.形成树的叶节点的所有几何对象都被包裹在包围体（AABB）中。这些节点被分组为小集合，以递归方式分组并封闭在其他较大的包围体中，最终产生在树顶部具有单个包围体的树结构。![v2-b3bc853cc79e627f3f0557e1a2e17b0f_r](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/v2-b3bc853cc79e627f3f0557e1a2e17b0f_r.png)
- BVH Node: 
  - leftNode
  - rightNode
  - parent 
  - aabb
  - name

- k-d tree: 通过寻找方差最大的轴来分割物体。统计数据点在每个维度上的数据方差。挑选出方差中的最大值，对应的维就是分割域的值。 e.g.: 如果X轴的方差大于Y轴，Z轴，就在X轴上进行分割。
  - 是“自上而下”（top-down）的构建方式，需要知道场景所有物体的信息。
    - pros：懒初始化：需要时再构建
    - cons：耗时长
    - https://zhuanlan.zhihu.com/p/697130257

- 动态调整BVH
  - 叶子节点 《-》分支节点
  - 合并： e.g. 合并节点A和节点B -》 复制节点A为A'， ![v2-0d754850de3ce9b7b58003bb52ab3b2a_r](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/v2-0d754850de3ce9b7b58003bb52ab3b2a_r.png)
  - 分离： e.g. 分离节点B， 获取节点B的兄弟节点A，将节点A作为父节点 ![v2-aae53b60de17c60248e95279b4fa58eb_r](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/v2-aae53b60de17c60248e95279b4fa58eb_r.png)

- SAH（Surface Area Heuristic）: 计算最合适的插入的目标节点
  - 前提： 
    - 光线击中AABB的概率和表面积有关-》AABB的表面积最小时是最合适的AABB划分
    - root节点与叶子节点的AABB和任何时候都相等。
  - 结论： 插入节点时，总的变化表面积最小的目标节点是最合适的。
    - 【变化表面积】=【新增的分支节点的表面积】+【插入后所有祖先节点的表面积差】。
    - ![v2-b36f7412f37695de8ab26a73e6bcf494_r](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/v2-b36f7412f37695de8ab26a73e6bcf494_r.png)
    - 考虑到不同插入对象共有的祖先的面积变化是相同的，所以只需要考虑非公有的祖先节点的面积变化？
  - 

- Movement: 物体移动： 先删除物体对应的叶子节点，然后计算该物体的插入。
  - 可以用一个较为宽松的AABB来包裹物体，只有超出一点范围的移动才会触发BVH的更新。

- TLAS(top level acceleration structure) vs BLAS (buttom ~)