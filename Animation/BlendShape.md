## BlendShape
- def: Morgh动画的一种: **顶点动画**： 直接指定动画每一帧的顶点位置，其动画关键帧中存储的是Mesh所有顶点在关键帧对应时刻的位置。 通常通过在中性形状（Default）到目标形状(Target)的mesh之间进行残值来产生动画，以节省存储vertex的内存消耗。
- 为什么使用BlendShape？
  - 面部的肌肉数量众多，使用纯骨骼蒙皮的话，要保证效果的话，过多的骨骼数量会带来性能问题。
  - 最终的面部姿势作为多个面部表情的线性组合，即“BlendShape Target”。每个“Target”可以是一个完整的表情，也可以是一个“delta”微表情，比如说抬起一边的眉毛。通过面部动作编码系统（FACS），阐明表情与情绪之间的相对应关系.![Facial-Expression-Morph-Model](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/Facial-Expression-Morph-Model.png)
    - Pros：
      - 可以对形状产生直接的控制。
      - 语义参数化的控制方式(每个target都有具体的含义)，更直观，权重即各种面部表情的强度。
      - 混合结果被限制在各个BlendShape基所张成的空间中。（限制，有利有弊）
- Math： Blendshapes 是线性面部模型，其中各个基本向量代表各个面部表情，但各个基并不是**正交**的，即各个*基本表情*会相互影响。![20241104115208](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20241104115208.png)
  - Global BlendeShape： 将基础混合目标形状与其他混合形状目标相混合。
    - w为各个表情的权重，b为各个表情的vertex，其中w的和为 1 。![20241104115443](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20241104115443.png) 竖直方向代表每个表情有 *pz*个顶点. \
    ![20241104115640](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20241104115640.png)
  - Delta BlendShape： 指定一个表情(通常为自然表情)为中性的基础表情，其余表情的表达替换为二者之间的差值。![20241104120137](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20241104120137.png)
  - Math expression: $f : R^{n} -> R^{3p}$, 其中*n*代表存在n个动画控制参数($\approx$ 表情数量)， *p*代码每个表情上的顶点数。
  - Interpolation： 因为简单地线性插值中性与目标表情往往不能得到符合质量表情。实践中常额外对其进行修正。其中主要分为
    - 中间形状（分段线性插值）: 在中性与target之间插入一个表情，将线程插值分段处理。
      - e.g.: 如在中性表情$b_0$，与target表情$b_1$之间0.5权重处，插入表情$b_2$， 则当权重介于0~0.5时，$b_0$ 与 $b_2$混合； 当权重介于0.5~1.0时， $b_0$ 与 （$b_2$, $b_1$ 混合后的结果）混合。
    - 组合形状(Combination Blendshapes):  对特定的表情(组合)添加额外的 “矫正表情”， 如下式的二，三行。![20241104164025](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20241104164025.png) \
      以 $b_{1,5}w_{1}w_{5}$其物理含义为只有当target表情 $b_1 , b_5$ 都参与了插值时($w_{1}, w_{5 \ne 0}$)才会添加“矫正表情” $b_{1,5}$ 参与插值。 \
      ![20241104170707](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20241104170707.png)
      - 当前专业的表情模型中，绝大多数的表情都是“矫正表情”
      - cons： 会造成插值曲线的不平整？随着次数(“矫正函数”的元数)的增加而恶化？？--》 因为多元 ($w_1 w_2$) 时，插值不是线性的，导致权重值越高时，斜率越大。![20241104173523](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20241104173523.png)
    - 基于径向基函数（Radial Basis Function，RBF）：

### 参考资料
- https://zhuanlan.zhihu.com/p/657434885
- https://zhuanlan.zhihu.com/p/659837005
- https://zhuanlan.zhihu.com/p/391409060
- https://diglib.eg.org/server/api/core/bitstreams/263adf15-3e7f-481a-a3cc-62b359a6d295/content