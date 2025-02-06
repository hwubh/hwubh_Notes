- Swing-Twist Interpolation vs Slerp: https://allenchou.net/2018/05/game-math-swing-twist-interpolation-sterp/
  - Pros and cons: for elongated objects（细长物体）, slerp 插值路径基于四元数的4D单位超球面，它确保两个四元数之间的旋转是沿着4D超球体上的最短路径进行的。然而，四元数空间中的最短路径不直接对应于3D空间中物体的端点（例如，细长物体的“尖端”）在3D球面上的最短路径。Swing-Twist 分解和插值能够更好地处理细长物体的旋转，因为它专注于：  Swing部分：控制物体端点的方向，使其在3D空间中沿最短弧线移动。Twist 部分：控制绕物体主轴的旋转，不影响端点的方向性。
  - Decomposition: 需要将四元数分解为绕轴的Twist和控制端点的Swing。 
    - 已知四元数 $R = [W_R, \vec{V_R}]$ 和 旋转（Twist）轴 $\vec{V_T}$。 将$\vec{V_R}$ 投影到 $\vec{V_R}$得到Twist的四元数 $R = [W_R, proj_{\vec{V_T}}(\vec{V_R})]$. 然后通过T的逆（共轭）求得Swing的四元数 $S = RT^{-1}$。 然后需要对得到的两个四元数做normalized。

- ![20250206170124](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20250206170124.png) 计算出来的swing存在误差，累计后可能造成旋转轴发生偏移。