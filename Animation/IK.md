- Two Bone IK: compute the postion of joint via the Law of cosines
  - Daniel Holden: First, move to ensure $\lvert ac \rvert = \lvert at \rvert$, then rotate ac to at.
    ![20240608093619](https://raw.githubusercontent.com/hwubh/hwubh_Pictures/main/20240608093619.png)

- Cyclic Coordinate Descent IK: 一次转换一个关节变量来最小化位置和姿态误差。![20240608093805](https://raw.githubusercontent.com/hwubh/hwubh_Pictures/main/20240608093805.png)
  - 方案: 从最末的Joint开始，连接Joint 与 target，旋转该关节使 end-effector 落在连线上。依次对各个关节依次做相同的处理，并进行多次迭代，直到 end-effector 足够接近 target。
  - 优点：计算成本低，只需要点积和叉积。线性复制度。容易实现局部约束。
  - 缺点：运动分布不佳，强调 end-effector 的运动，运动姿势不佳；可能产生大角度旋转，导致不稳定的不连续性和振荡运动；特别是当目标位于接近base时，它会导致链条形成一个环，在到达目标之前滚动和展开自己？？？
  - 扩展：
    - IBK： 分配一个连续的选择范围来控制预定义的全局过度成本阈值，限制非自然姿态；每次迭代引入一个偏置因子，对旋转进行校正；增加一个反馈常数（基于end-effector 与 target的距离）来改善CCD的收敛性，
    - CAA
    - IIK
  - 代码实现：
    - 计算出夹角以及其对应的旋转（这里用class *Rotation* 可以更方便的得到旋转的各种表达形式）：
    - 每次发生旋转时需要更新该节点一下所有受影响的Joints 的 Position
  - 代码分析： https://github.com/Cltsu/GAMES105/blob/main/lab1/Lab2_IK_answers.py
    - *get_joint_rotations*， *get_joint_offsets* 算出各个joint 的 local position/ rotation
    - 使用local variable 记录各个joint 对应的 world/local position/rotation， 其中从root到end 的path记录的local rotation记得取逆，否则记录的是parent相对于child的旋转。
    - IK计算：
      - CCD：
        - 更新当前节点的 world/local rotation， 
          更新其下各个受影响的各个joint的 world rotation/position： world rotation：  world rotation = 新的 parent joint world rot * child local rot； world position = parent joint world rot * child local pos + child world pos
    - 将计算后的IK结果写回*joint_rotation*，主要 root2end的路径上记录的是parent相对于child的旋转，需要取逆
    - 如果*rootjoint*在IK链中，需要更新rootjoint的信息？？
    - 最后计算FK，更新world pos/rot （对于ccd 来说似乎多余了）？？
  - 更好的思路？：https://zhuanlan.zhihu.com/p/608534364
    - 直接计算各个joint 的 world pos/rot， world rot = world rot * rotation； world pos = offset * rotation + 转动关节的pos（这里的offset 是指末端到转动点的位置的offset![20240725175825](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240725175825.png)）

- Gradient Descent：计算函数的梯度，每次根据设置的步长逐步逼近target。https://medium.com/unity3danimation/overview-of-jacobian-ik-a33939639ab2; https://nrsyed.com/2017/12/10/inverse-kinematics-using-the-jacobian-inverse-part-2/ ; https://www.zhihu.com/question/305638940/answer/1639782992
  - Jacobian matrix: ![20240725183438](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240725183438.png) 上图中 \( $p_x, p_y, p_z$ \) 所表示的是 *End_effector*的坐标。而matrix本身则记录 effector 其在各个方向（行） 与 各个joint（纵） 上的变化率。
    - 而在实际计算中，各个偏导则用叉乘来代替：![20240725190637](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240725190637.png). 其中 $a_j$ 表示在世界空间下joint的旋转轴![20240726103958](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240726103958.png)，$r_e \, r_j$ 分别表示end_effector 和 joint的坐标。（世界空间下）。（但用轴角来表示Jacobian会很复杂，一般使用欧拉角将旋转分解为单个自由度的旋转。）
    - ![20240726121429](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240726121429.png) 不是很理解？
  <!-- - Jacobian methods steps：Find the joint configurations: *T*
                            Compute the change in rotations: *dO* 
                            Compute the Jacobian: J
    -  Find Joint Configurations: -->
- 
  -   