- Keyframe Character Animation
  - Character Kinematics
    - Motion Retargeting
      - Pose types: 
        - T-Pose (aka *Bind pose* *Reference pose* ): the pose with **Zero/identity** rotation,
        - A-Pose: arems are angled downwards
        - Y-pose:  arems are angled upwards
        ![20240726154730](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240726154730.png)
      - Retargeting: Change *reference pose*. 如果想在不同 *reference pose*的角色上实现相同的动作时，由于二者的 动作文件 中的数据是基于各自的 *referece pose*，二者的数据是不相同的。如果我们把适用于 X-pose 的动作数据 经过一定的*处理* 适用于 Y-pose 的角色上， 这个 *处理* 的过程就是 **重定向**（Retargeting）。其数学本质其实就是对各个Joint 作 齐次空间 下的线性映射，以在不同 Pose的角色间实现相同的动作。（相当于每个Pose都是不同的参考系，motion data是local space的data， 而表现的相同的motion data则是保持在world sapce 下相同）
        - 下图中的$R_i^B$表示的是：当B变换到C时，（子）节点*i*自己需要做的旋转量：其本质是$(Q_P^{B \rarr C})^T * Q_i^{B \rarr C}$, 即i的全局旋转，减去其父节点的全局旋转；而在公式中可视为三次变换，从右到左分别是：$Q_P^{A \rarr B} * R_i^{A \rarr C} * (Q_i^{A \rarr B})^T$, 即父节点A->B的全局旋转 加上 子节点 A—>C 的相对父节点的全局旋转 减去 子节点A->B的全局旋转，![20240726162528](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240726162528.png)
    - Full-body IK
  - Keyframe Animation
    - Interpolation and splines ：https://zhuanlan.zhihu.com/p/62860859 P.S.拟合：神似。不要求方程穿过已知点，而是整体趋势一致； 插值：形似。要求每个已知点都没经过。
      - Interpolation: 根据离散点拟合一条函数：![20240726175239](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240726175239.png)
        - Smothness： if continuity (in position, velocity, acceleration)?![20240726175401](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240726175401.png)
        - Polynomial: need n = (N-1)-degree polynomial to fit N data points. ![20240726175635](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240726175635.png)
          - 问题：Runge's phenomenon：High-degree poly 在边缘的区间内会有比大的振动(*Runge phenomenon*)。尽管low-degree polynomials能解决这个问题，但是是因为low-degree本身采样率低。综合来说polynomial 并不适用于动画曲线的拟合。
      - Spline：low-degree piecewise polynomials
        - Cubic Splines(3rd degree):![20240726182656](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240726182656.png)，存在N个区间时，有4N个未知数，需要4N个方程。
          - Req.： 在分段区间$[x_i, x_{i+1}]$上，$S(x) = S_i(x)$是一个三次函数
                   满足插值要求（经过每个已知点）
                   曲线光滑，在$S(x), S^{'}(x), S^{''}(x)$上连续。 
          - 求解：内部的（N-1）个点，需要在一阶上满足前后两个分段函数，在二，三阶上满足连续，得到 4N-4个方程。边界的两个端点，在一阶上满足各自分段上的函数，得到两个。最后需要根据边界得到最后的两个点。
            边界条件：自然边界，固定边界，非节点（/扭结）边界。 
            - 自然边界（Natural Spline）：指定边界点二阶导数为0，$S^{''}(x_0) = 0 = S^{''}_{n}(x_n)$
            - 固定边界(Clampped Spline): 指定边界点一阶导数为固定值（A,B）， $S^{'}_0(x_0) = A, S^{'}_{n}(x_n) = B$
            - 非节点边界（Not-A-Knot Spline）: 第一个插值点的插值点的三阶导值等于第二个点的三阶导值， 最后一个点的三阶导值等于倒二个点的。 $S^{'''}_0(X_0) = S^{'''}_1(X_1), S^{'''}_n(X_n) = S^{'''}_{n-1}(X_{n-1})$
          - cons：
            - No local control: 每个节点都会影响整体曲线。
            - Computationally expensive: 当N（节点数）多时计算量大
        - Cubic Bezier Curves: 三次的Bezier曲线由四个控制点控制。
          - 
        - Cubic basis spline(B-Splines): 基于n次B样条基函数。是bezier曲线的一般化。
          - Pros: 
            - local control： 曲线位于控制多边形的凸包内，控制顶点只影响局部的多项式曲线。 曲线的阶数与控制点的数量解耦。
            - 特征多边形更逼近以及多项式阶次较低
          - Cubic Hermite: 每个全局考虑前后两点的取值，以及他们的一阶导数值。使用这四个点作为控制点。
          - Catmull-Rom spline