# Making Bonehead
### Unity的程序化动画入门

## 前言
什么是程序化动画（procedural animation）?
> 程序化动画是计算机生成动画的一种，其不依赖与预先生成的动画（片段），而是实时的自动地生成需要的动画，以实现不同的形态。
通俗来讲， 程序化动画是由**代码**驱动而不是**关键帧**。以角色动画为例，它既可以是将两个动画片段 (animation clip) 跟随角色的速度进行混合这样简单的，也可以是完全不依赖任何已生成的数据，完全由（代码） 程序化动画系统（生成的）。

这篇教程中我们将完成后者的简单实现，即交互式（项目）<a href=https://weaverdev.itch.io/bonehead>“Bonehead Sim”</a> 所使用的动画系统，尽管所有相同的概念都可以在传统关键帧动画之上使用。 （这篇文章里）我们聚焦于程序化动画的应用而不理论。不过如果对其背后的数学原理感兴趣的话，可以参见 <a href=https://www.alanzucconi.com/2017/04/17/procedural-animations>“Alan Zucconi’s”</a>  的教程。

## 基础知识
### 前向动力学 Forward Kinematic (FK)
FK通过调整父关节（joint）的旋转(rotation)来得到其子关节的位置(position)与方向(orientation). 对(骨骼)链中的每个子关节都重复此操作，因此（发生了旋转的）骨骼（关节）都会影响层次结构在其下方的所有骨骼（关节）。 总之，FK需要控制关节的变换(transform)而这在大多数的引擎中都是默认暴露的。
> P.S.: 

### 反向动力学 Inverse Kinematic (IK)
（与FK）相反，IK 要求目标位置（target position） 与一个极向量（即旋转轴）作为输入，然后旋转链条上的各个骨骼，使轴的末尾与目标位置重叠。 IK经常用于在身体移动时保持脚部在地面上的位置，以及在抓取骨架层次(skeleton hierachy)外的物品的手臂。
> P.S.:  

### 质点(Particles) / 韦尔莱积分法(Verlets) / 刚体(Rigidbodies)
这种方式（刚体 + 韦尔莱积分法）常常用于软体模拟和“布娃娃”物理。 它不通过父/子结构来，而是通过自由浮动、速度驱动的对象来生成姿态。通过添加约束（如最大的*角度*， *距离*）来实时的驱动肢体实现诸如“跌落”等较为复杂的动作。 总之， 这是一种被广泛运动但却不依赖IK, FK的技术。

## 准备
首先，你需要某种形式的骨骼结构。 Unity 中默认不区分骨骼和“transform”， 因此我们可以用一些基本的集合体来构成骨架。 如果你使用现成的骨骼结构，你也可以通过添加新的肢体或改变几何形状来进行尝试和实验。？？
- <a href=https://github.com/WeaverDev/filehost/raw/main/Bonehead%20Tutorial/Bonehead_CapsuleSkeleton.unitypackage>“守宫骨架”</a> - 跟着教程自己写代码.
- <a href=https://github.com/WeaverDev/Bonehead>“完整工程”</a>  - 完整的工程文件（含代码）.

为了之后的方便考虑，最好保证骨架上所有的关节在一个方便计算的位置，例如都指向一个方向，或者局部旋转为0。 这显然会让我们操作骨骼时更方便