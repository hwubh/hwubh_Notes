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

- FABRIK(Forward and Backward reaching inverse Kinematics): 通过通过计算骨骼节点的空间坐标关系来实现IK。不需要计算旋转，只需要计算线性位置就最终可以求得近似解。（不使用角度旋转，而是将关节沿直线的新位置更新到下一个关节）
  - 迭代步骤： Fabrik 算法一次迭代执行两个方向的遍历，首先从后往前计算到达目标点情况下所有骨骼的空间位置变化（不包括根节点），然后从前往后计算从根节点出发到达目标节点的骨骼节点位置。 
    - 逆向： 最后一个骨骼节点置于目标位置，然后从后向前遍历骨骼节点，前一个节点的位置由后一个节点指向前一个节点的方向向量的单位向量乘以骨骼长度加上后一个节点当前的位置得到。![20250208104405](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250208104405.png)
    - 正向： 从根节点开始从前向后遍历骨骼节点，后一个节点的位置由前一个节点指向后一个节点的方向向量的单位向量乘以骨骼长度加上前一个节点当前的位置得到， ![20250208111127](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250208111127.png)
  - 
    - https://zhuanlan.zhihu.com/p/471910711
    - https://busyogg.github.io/article/5795c3870390/
    - https://blog.csdn.net/zhaishengfu/article/details/88195246
  - 问题： 如何在迭代时，防止出现关节反向折叠(pole target constraint)的问题？？![20250208153719](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250208153719.png)![20250210105026](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250210105026.png)
    - 可能方法：忽略无法达到位置的关节，将该关节与其父关节合并处理？: 通过忽略骨骼，优先计算移动父关节是否可以使target point 落入可解的范围中。

- Gradient Descent：计算函数的梯度，每次根据设置的步长逐步逼近target。https://medium.com/unity3danimation/overview-of-jacobian-ik-a33939639ab2; https://nrsyed.com/2017/12/10/inverse-kinematics-using-the-jacobian-inverse-part-2/ ; https://www.zhihu.com/question/305638940/answer/1639782992
  - Jacobian matrix:  
    ![20240725183438](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240725183438.png) \
    上图中 \( $p_x, p_y, p_z$ \) 所表示的是 *End_effector*的坐标。而matrix本身则记录 effector 其在各个方向（行） 与 各个joint（纵） 上的变化率。 \
    若将其按照各个关节拆分，每次迭代的距离$\Delta r$相当于各个关节在各自的 $\Delta \theta$的转动下，对于effector 在XYZ方向的产生的位移的总和。 \
    ![20250228165607](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250228165607.png)
    ![20250228165556](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250228165556.png)
    这种位移也可以从关节，effector， $\Delta \theta$三者构成的切线上得到。\ ![20250228170709](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250228170709.png)


    - 而在实际计算中，各个偏导则用叉乘来代替：![20240725190637](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240725190637.png). 其中 $a_j$ 表示在世界空间下joint的旋转轴![20240726103958](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240726103958.png)，$r_e \, r_j$ 分别表示end_effector 和 joint的坐标。（世界空间下）。（但用轴角来表示Jacobian会很复杂，一般使用欧拉角将旋转分解为单个自由度的旋转。）
    - ![20240726121429](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240726121429.png) 不是很理解？
  <!-- - Jacobian methods steps：Find the joint configurations: *T*
                            Compute the change in rotations: *dO* 
                            Compute the Jacobian: J
    -  Find Joint Configurations: -->
- 
-   

---

- 写IK遇到一些问题
  - 计算精度：Mathf.Approximately 不太管用
    - Normal方向 ： 从t转到e的话，计算normal时应该时tXe -》 ![20241112155354](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20241112155354.png)
    - 初始位置上时，误差不断累积导致的扰动与错误。
    - target 和 end 的值很小时，也容易造成扰动(计算的角度过大![20241112162434](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20241112162434.png))
  - 万向节死锁？
    - 直接旋转Transform.Rotation好像不太行，先将Normal转换到局部空间内再计算局部的旋转。![20241112154515](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20241112154515.png)
  - 顺序问题： IK计算角度 -> 修正误差（如Hinge上需避免旋转轴发生移动） -》 角度限制（放最后防止插值时出现不希望存在的角度）