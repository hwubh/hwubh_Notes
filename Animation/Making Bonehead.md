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

为了之后的方便考虑，最好保证骨架上所有的关节在一个方便计算的位置，例如都指向一个方向，或者局部旋转为0。 这显然会让我们操作骨骼时更方便。
> **_NOTE:_**: 本文提供的“守宫”上的骨骼大多都是以 Z forward 和 Y up为轴的。 如果你使用的模型不是同样的话，直接使用原文提供的代码的话，需要注意下骨骼局部的旋转。 

在正式开始前，个人推荐可以在package manager里下载库(package)"Animation Rigging".![20241114182937](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20241114182937.png)
在根骨“Gecko”处，添加Component"Bone Renderer"。 然后就可以将我们想要可视化的骨骼首尾的两个关节添加到"Transforms"项中。 这里以“Gecko_Neck” 为例，我们可以把“Gecko_Neck”以及它的子节点“Gecko_Jaw”加入“Transforms”便能得到下图中以“Gecko_Neck”为起点，结束在“Gecko_Jaw”的一个蓝色锥体。点击该锥体，便会显示出以“Gecko_Neck”的位置为原点的坐标轴（记得Scene里要设置成“Pivot”）。 
![20241115155752](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20241115155752.png)

## 从“头”开始
接下来会简单介绍如何实现本文开始所展现的“守宫”的运动效果。
### 单骨骼追踪(Single Bone Tracking)
首先让我们创建一个名为"GeckoController"的 MonoBehaviour脚本，其包含之后所有的运动逻辑。 为此，我们先在脚本中声明目标对象和守宫颈部骨骼的索引，在 Unity inspector界面暴露并联接上场景中的这两个对象。

```c#
using UnityEngine;

public class GeckoController : MonoBehaviour 
{
  // 被追踪的目标
  [SerializeField] Transform target;
  // 守宫颈部骨骼
  [SerializeField] Transform headBone;
  
  //调用 LateUpdate 来更新我们所有的动画逻辑
  //其次序在游戏逻辑(Update())与渲染流程之间
  //前者保证动画使用正确的数据
  //后者保证动画与渲染结果相符合
  //关于Unity事件函数的执行循序：https://docs.unity3d.com/6000.0/Documentation/Manual/execution-order.html
  void LateUpdate()
  {
    // 具体控制骨骼的代码
  }
}
```
>Note: <span style="color:red"> [*SerializeField*] </span>可以暴露非public的变量到Unity Inspector上。

然后，将脚本挂载在守宫的根骨骼“Gecko”，然后在场景中添加上一个GameObject "LookTarget"并将其与“Gecko_Neck”一起挂载到Inspector上。![20241114174321](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20241114174321.png)

我们目标是得到一个**四元数**其使守宫的头部朝向目标对象。 
> Note: 四元数是旋转的一种表达形式，本文中我们将其当作是“3D方向”处理，具体原理可以参考这篇文章： https://krasjet.github.io/quaternion/quaternion.pdf

首先我们计算得到“头部”到目标对象的相对位移，即从头部对象的位置指向到目标对象位置的一个向量。
``` c#
// 这里的向量表达是在世界坐标下的。
Vector3 towardObjectFromHead = target.position - headBone.position;
```
为了得到指向目标对象的的方向，我们调用 <a href=https://docs.unity3d.com/ScriptReference/Quaternion.LookRotation.html>“Quaternion.LookRotation”</a> 函数。这个(Unity)函数需要我们提供一个“Forward”方向与参考的“Up”方向，输出一个Z轴正方向指向"Forward"方向， y轴正方向与“Up”方向**相似**(二者点乘>0)的四元数。 
``` c#
headBone.rotation = Quaternion.LookRotation(towardObjectFromHead, transform.up);
```
这里我们使headbone “Gecko_Neck” 的(局部空间)Z轴正方向指向目标对象，Y轴正方向与根骨骼的的Y轴正方向相似。 因为headbone的原先的Z轴正方向与头部朝向是相似的，所以我们便实现了对于头部朝向的控制。
![onebone2](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/onebone2.gif)

通过添加下述调试指令，我们可以看到目前headbone的Z轴正方向已经与目标对象相交。
``` c#
Debug.DrawLine(headBone.position, headBone.position + headBone.forward * 10, Color.red);
```
![20241115155936](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20241115155936.png)

为了使头部运动表现更为自然，而不是瞬间移动和穿模，我们还需要对其的添加上阻尼和角度限制。
- 阻尼： 首先将运动根据速度分帧处理，根据Lerp()得到当前帧所在的位置。 使用speed * Time.deltaTime 一方面可以模拟出越靠近Target时，位移变化越小的效果。 另一方面也保证了速率不会因为帧率的波动而波动。
    ``` c#
    current = Mathf.Lerp(
    current, 
    target, 
    speed * Time.deltaTime
    );
    ```
    但考虑到speed * Time.deltaTime可能存在大于1的情况，我们这里使用“1 - Mathf.Pow(smoothing, dt)”来代替插值项。
    ``` c#
    current = Mathf.Lerp(
    current, 
    target, 
    1 - Mathf.Exp(-speed * Time.deltaTime)
    );
    ```
    >Note:  参考资料：<a href = "https://www.rorydriscoll.com/2016/03/07/frame-rate-independent-damping-using-lerp/">frame rate independent damping function </a>

    因为我们的关节是通过旋转来改变(子关节的)位移的，这里使用<a href = "https://discussions.unity.com/t/what-is-the-difference-of-quaternion-slerp-and-lerp/453377/19">Slerp</a>代替Lerp。从而得到我们代码实际使用的内容。
    ``` c#
    Quaternion targetRotation = Quaternion.LookRotation(
    towardObjectFromHead, 
    transform.up
    );
    headBone.rotation = Quaternion.Slerp(
    headBone.rotation, 
    targetRotation, 
    1 - Mathf.Exp(-speed * Time.deltaTime)
    );
    ```
    ![onebone3](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/onebone3.gif)
  - 角度限制： 我们会使用一个角度值来表示headbone所能转动的最大角度。因为是以headbone为基准进行判断，我们需要先将目标向量从世界空间下的表达转换到headbone的局部空间下再进行判断。 Unity中提供了函数 <a href = "https://docs.unity3d.com/ScriptReference/Transform.InverseTransformDirection.html">Vector3.RotateTowards</a> 通过传入的四个参数: 初始/结束指向， 最大弧度，最大长度变化，计算出实际的结束指向。 接着根据得到的指向，使用 Quaternion.LookRotation 计算出其对应的3D方向（四元数）。
  ![onebone4](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/onebone4.gif)

  对应的代码：
    ``` c#
    // 这里的向量表达是在世界坐标下的。
    Vector3 towardObjectFromHead = target.position - headBone.position;
    //记录headbone当前的局部旋转
    Quaternion currentLcoalRotation = headBone.localRotation;
    //当headBone的局部旋转被置空后，headbone 和 headboned的父节点相对于世界空间的变换相同。
    headBone.localRotation = Quaternion.identity;
    var targetLocalLookDir = headBone.InverseTransformDirection(towardObjectFromHead);
    // 相当于一个Clamp操作，将角度限制在0到headMaxTurnAngle之间。
    targetLocalLookDir = Vector3.RotateTowards(Vector3.forward, targetLocalLookDir, Mathf.Deg2Rad * headMaxTurnAngle, 0);
    //计算目标旋转在局部空间下的表达。
    Quaternion targetLocalRotation = Quaternion.LookRotation(targetLocalLookDir, Vector3.up);

    headBone.localRotation = Quaternion.Slerp(
        currentLcoalRotation, targetLocalRotation,
        1 - Mathf.Exp(-speed * Time.deltaTime));

    //调试代码
    {
        Debug.DrawLine(headBone.position, headBone.position + headBone.forward * 10, Color.red);
        //显示头部的旋转范围
        var length = Mathf.Tan(headMaxTurnAngle * Mathf.Deg2Rad) * 3;
        var jointPosPP = headBone.position + headBone.parent.TransformDirection(new Vector3(length, length, 3));
        var jointPosNP = headBone.position + headBone.parent.TransformDirection(new Vector3(-length, length, 3));
        var jointPosPN = headBone.position + headBone.parent.TransformDirection(new Vector3(length, -length, 3));
        var jointPosNN = headBone.position + headBone.parent.TransformDirection(new Vector3(-length, -length, 3));
        Debug.DrawLine(headBone.position, jointPosPP, Color.blue);
        Debug.DrawLine(headBone.position, jointPosNP, Color.blue);
        Debug.DrawLine(headBone.position, jointPosPN, Color.blue);
        Debug.DrawLine(headBone.position, jointPosNN, Color.blue);
        Debug.DrawLine(jointPosPP, jointPosNP, Color.blue);
        Debug.DrawLine(jointPosNP, jointPosNN, Color.blue);
        Debug.DrawLine(jointPosNN, jointPosPN, Color.blue);
        Debug.DrawLine(jointPosPN, jointPosPP, Color.blue);
    }
    ```
    ### 眼球追踪(Eye Tracking)
    接下来我们开始添加眼球追踪的效果，不过先让我们将单骨骼追踪的部分整理好，将其与眼球追踪的部分分离开。
    ``` c#
    public class GeckoController : MonoBehaviour
    {
        // 被追踪的目标
        [SerializeField] Transform target;
        // 守宫颈部骨骼
        [SerializeField] Transform headBone;
        // 头部运动速度
        [SerializeField] float headTrackingSpeed;
        // 头部最大旋转角度
        [SerializeField] float headMaxTurnAngle;

        void LateUpdate()
        {
            //从靠近根节点的骨骼开始更新
            HeadTrackingUpdate();
            EyeTrackingUpdate();
        }

        void HeadTrackingUpdate() 
        {
          ///头部追踪的代码
        }

        void EyeTrackingUpdate() 
        {
          //眼球追踪的代码
        }
    }
    ```
    这里我们规定眼球绕着各自的Y轴方向移动，并拥有眼球的追踪速度和各自独立的角度限制。
    ``` c#
    //左右眼骨骼位置
    [SerializeField] Transform leftEyeBone;
    [SerializeField] Transform rightEyeBone;

    //左右眼的运动速度和各自的角度限制。
    [SerializeField] float eyeTrackingSpeed;
    [SerializeField] float leftEyeMaxYRotation;
    [SerializeField] float leftEyeMinYRotation;
    [SerializeField] float rightEyeMaxYRotation;
    [SerializeField] float rightEyeMinYRotation;
    ```
    使用类似前文头部追踪的方式完成眼球的追踪。，但这里我么希望采用欧拉角的形式来进行角度限制。欧拉角通过记录物体在其自身的三个坐标轴上的旋转来表达其的旋转，这很适合只绕着一个轴旋转的眼球运动。这将允许我们通过仅操作 <a href = "https://docs.unity3d.com/ScriptReference/Transform-localEulerAngles.html">Transform.localEulerAngles</a> 向量的单个分量，在局部空间中轻松限制一个轴上的旋转。
    >Note: (原文中分享的)关于欧拉角与四元数的一篇<a href = "https://web.archive.org/web/20220412171953/https://developerblog.myo.com/quaternions/">文章</a>。
    Unity中的“eulerAngles” 和 “localEulerAngles” 都是将欧拉角表达在0~360度之间，不过我们这里将其映射到-180~180度之间。 为此我们需要对介于180~360度之间的角度减去360度以进行矫正。
    ```C#

    ```
    ### Two-Bone IK
    原文中省略了