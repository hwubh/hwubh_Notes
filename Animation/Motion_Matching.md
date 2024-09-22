- idea: 根据**当前姿势**计算出的数据（Current Pose）， 和根据用户输入**预测**出的未来的**运动轨迹**（Trajectory），在动画数据库中**实时搜索**最合适的姿势作为下一帧的目标姿势，进行得到下一帧的姿势。
  - Pose：记录角色的姿势的特征数据（关节的局部位置/速度/加速度, 角色的朝向等）
  - Trajectory: 运动轨迹的特征数据（*一般不止一个*），包含位置/速度等
  - Goal： 预期轨迹，Pose + Trajectory
  - Dataset： 仅保留了特征的动画数据？？ 可以KD-Tree来优化搜索。
  - Compute_Cost: 计算得分，找出最合适（得分最小）的target pose。 

- Steps：
  - 预测（Stepping）: 根据当前的昨天，逐步推导出下一个Pose的Feature
  - 查找(Projection/Motion Matching Match)：构建合适的特征空间，然后在这个特征空间中根据输入信号找到最近邻
  - 解压缩(Decompression / Pose Lookup)：要把查找到的目标feature / Frame Index重新映射回动画数据库中的目标动作
![v2-086a351efc76c4790d5c215de26ed270_720w](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/v2-086a351efc76c4790d5c215de26ed270_720w.webp)

- Trajectory Matching: 
  - 轨迹预测：

- Pros and Cons
  - Pros: 结构简单
    - 质量高？，响应及时
    - Dance Cards 对动补有很好的指导，工业流程化动补 ??(P.S.“Dance Cards”可以理解为一种预先设计好的动作列表或指导手册，类似于舞蹈卡片，列出了需要捕捉的特定动作或动作序列。用于给动补提供指引。)
    - 所有数据都在同一个文件
  - Cons： Data Heavy， 需要很多数据。
    - 大多数数据适用于locomotion
    - 滑步


- Code：
  - Init: 读取“long_walk” 和 “idle”动作。
    - 使用 position，rotation，velocity，avelocity作为feature项
    - 使用 
    - 
  - 第一帧：初始化rootnode的XZ项。 
  - 记录"transition"的dst/src pos/rot项
  - 计算特征值（共27项）：
    - 根节点预测轨迹的3个时刻所对应的世界坐标下的 pos的XZ坐标以及 Z轴指向的XZ坐标，共12项--》归一化？
    - 记录左右脚踝的pos，以及左右脚踝，根节点的速度，共15项
    - 寻找最合适的下一帧。
  - 计算特征值得分：


- ref:https://zhuanlan.zhihu.com/p/662604249 https://github.com/Cltsu/GAMES105/blob/main/lab2/Viewer/controller.py https://www.theorangeduck.com/page/code-vs-data-driven-displacement  https://zhuanlan.zhihu.com/p/136971426 https://zhuanlan.zhihu.com/p/646264881  