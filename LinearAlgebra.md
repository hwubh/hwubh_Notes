- matrix中每一列可以视为一个变换后的基坐标轴的表达。![20241217154234](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20241217154234.png)
- 行列式
  - 二维：的值表示基坐标轴围成的四边形的面积变化的比例： =0时，说明变换后面积为0，坐标在一条直线或一个点上。 <0时，说明基坐标轴所在的平面法向量的方向取反了。![20241217161836](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20241217161836.png)
  - 三维：同上，<0时，（如果变换前为右手系）相当于从右手系变为了左手系
  - 行列式计算式的物理含义： ad-bc, a和d代表x，y轴长度的变化率。b，c代表x，y轴的倾斜程度。![20241217161658](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20241217161658.png)
- X * Y matrix表示其为：Y个维度的空间变换为X个维度的空间。![20241217163034](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20241217163034.png)
- 点乘： 可以看作是一个多维降至一维的矩阵？![20241217172859](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20241217172859.png)
- 叉乘： 只有当xyz用基坐标轴代替时，平行多变体的体积与$\vec{v}, \vec{w}$围成的面积相同，即 $\vec{P}$的模长与平行四边形的面积相同？ ![20241217174836](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20241217174836.png)![20241217174411](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20241217174411.png)![20241217173947](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20241217173947.png)
- 基变换：![20241217180001](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20241217180001.png) 将非本地空间下的向量进行变换，需要先转到本地空间下(A)进行变换(M)，再转回去($A^-1$)
- 特征值/特征向量： 特征向量指那些在变换后能留在其张成的空间的向量，（换句话来说就是只有scale上的变化）。 而scale的值即为特征值。![20241217180851](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20241217180851.png)
  - 应用：对应一个旋转来说，特征值为1的特征向量所在的直线即旋转轴
  - 求特征值： 通过行列式。![20241217181417](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20241217181417.png)
  - diagonal matrix： 所有基坐标轴都是特征向量，对角线上的值即为特征值。![20241217181818](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20241217181818.png)
  - 多次幂的矩阵运算可以先将特征向量作为基坐标轴来处理。
- 矩阵向量乘法与求导： 只要满足了向量加法和数乘的规则，就属于向量空间。 ![20241218111700](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20241218111700.png)![20241218112158](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20241218112158.png)