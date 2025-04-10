# Making Bonehead
### Unity的程序化动画入门

## 译者前言
本文为[WeaverDev](https://weaverdev.io/)撰写的文章[《Making Bonehead》](https://weaverdev.io/projects/bonehead-procedural-animation/) 中文翻译，另外也擅自加上了一些自己学习本文时的一些理解。 因为本人才疏学浅，翻译后文章相较于原文可能存在一些不恰当的用语乃至于错误的地方，还请各位大佬斧正。所以如果可以的话，**还请务必对照原文进行参考**。 \
[原文工程地址](https://github.com/WeaverDev/Bonehead) 

[译者工程地址](https://github.com/hwubh/hwubh_post_code/tree/main/%E5%8A%A8%E7%94%BB%E5%AD%A6%E4%B9%A0%E7%AC%94%E8%AE%B0_1--Making%20Bonehead) (版本为2022.3 LTS) 

以下是正文：

## 前言
什么是程序化动画（procedural animation）?
> 程序化动画是计算机生成动画的一种，其能实时地自动地生成需要的动画，比起预生成动画能够生成更多样化的动作。 — [Wikipedia](https://en.wikipedia.org/wiki/Procedural_animation)

通俗来讲， 程序化动画是由**代码**驱动的而不是**关键帧**。以角色动画为例，它既可以是简单地将两个动画片段 (animation clip) 跟随角色的速度进行混合得到的，也可以是完全不依赖任何已生成的数据，完全由（代码构成地） 程序化动画系统生成的。

这篇教程中我们将完成后者的简单实现，即交互式（项目）<a href=https://weaverdev.itch.io/bonehead>“Bonehead Sim”</a> 所使用的动画系统，尽管所有这些的概念同样可以应用在传统的关键帧动画。 （这篇文章里）我们聚焦于程序化动画的应用而不是理论。不过如果对其背后的数学原理感兴趣的话，可以参见 <a href=https://www.alanzucconi.com/2017/04/17/procedural-animations>“Alan Zucconi’s”</a>  的教程。
>本文尽管存在许多链接，但仅作为参考，而不是必读项。

## 基础知识
  ### 前向动力学 Forward Kinematic (FK)
  FK通过调整父关节（joint）的旋转(rotation)来调整其子关节的位置(position)与方向(orientation)。骨骼链中的每个子关节都重复此操作，因此（发生了旋转的）骨骼（关节）都会影响层级结构在其下方的所有骨骼（关节）。 总之，此方法只需要控制关节的变换(transform），而这在大多数的引擎中都是默认暴露的。 \
  ![FK](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/FK.gif)
  <center>前向运动学沿着骨骼链向下计算以得到脚的最终位置。</center>

  ### 反向动力学 Inverse Kinematic (IK)
  （与FK）相反，IK要求一个目标位置（target position）与一个杆向量（“肘部”弯曲的方向）作为输入，然后旋转骨骼链上的各个骨骼，使轴的末尾与目标位置重叠。 IK经常用于在身体移动时保持脚部在地面上的固定位置，以及用于手臂以抓取骨架层级(skeleton hierachy)外的物品。 \
  ![IK](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/IK.gif)
  <center>反向运动学从脚的位置开始，计算如何调整骨骼链中的各个骨骼的角度以使脚的位置与目标位置重合。</center>

  > 译注:  这篇[文章： 《逆向运动学（IK）详解》](https://zhuanlan.zhihu.com/p/499405167)比较系统讲述了IK的各种方法。

  ### 质点(Particles) / 韦尔莱积分法(Verlets) / 刚体(Rigidbodies)
  这种方式（刚体 + 韦尔莱积分法）常常用于软体模拟和“布娃娃”物理。 它不通过父子层级结构来，而是通过自由浮动、速度驱动的对象来生成姿态。通过添加约束（如最大的角度和距离）来实时的驱动肢体实现诸如“跌落”等较为复杂的动作。 总之， 这是一种被广泛运动但却不依赖IK, FK的技术。 \
  ![verlet](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/verlet.gif)
  <center>使用[DynamicBone](https://assetstore.unity.com/packages/tools/animation/dynamic-bone-16743)的软体尾巴，DynamicBone是一种基于Verlet积分的软体骨骼（动态骨骼）工具。</center>

> 译注：关于DynamicBone的算法可以参见这篇[文章: ](https://zhuanlan.zhihu.com/p/522873426)

## 准备
首先，你需要某种形式的骨骼结构。 Unity 中默认不区分骨骼和 “transform”， 因此我们可以用一些基本的几何体来构成骨架。 ![hierarchy](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/hierarchy.gif)
<center>骨骼结构的一个案例</center>
如果你使用现成的骨骼结构，你也可以进行尝试和实验添加新的肢体或改变几何形状。
- <a href=https://github.com/WeaverDev/filehost/raw/main/Bonehead%20Tutorial/Bonehead_CapsuleSkeleton.unitypackage>“守宫骨架”</a> - 跟着教程自己写代码.
- <a href=https://github.com/WeaverDev/Bonehead>“完整工程”</a>  - 完整的工程文件（含代码）.
> 译注：个人推荐从“完整工程”中取用“Gecko Simple.prefab”, 因为其包含完整的骨架和Mesh。

为了之后的方便考虑，最好保证骨架上所有的关节在一个方便计算的方向，例如都指向一个方向，或者局部旋转为0。 这显然会让我们后续操作骨骼时更方便。
> 本文假设您的骨架轴向为Z轴朝前，Y轴朝上。如果你使用的模型不是这样的话，需要注意下骨骼的偏移量(局部旋转)，以便使用Unity的内置函数。这可能会很容易变得混乱并难以调试，因此强烈建议您确保骨架设置正确！。 

(译者补充：)
在正式开始前，译者推荐可以在package manager里下载库(package)"[Animation Rigging](https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.2/manual/index.html)". \ ![20241114182937](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20241114182937.png)
在根骨骼“Gecko”处，添加Component"Bone Renderer"。 然后就可以将我们想要可视化的骨骼首尾的两个关节添加到"Transforms"项中。 这里以“Gecko_Neck” 为例，我们可以把“Gecko_Neck”以及它的子节点“Gecko_Jaw”加入“Transforms”便能得到下图中以“Gecko_Neck”为起点，结束在“Gecko_Jaw”的一个蓝色锥体。点击该锥体，便会显示出以“Gecko_Neck”的局部空间坐标轴（记得Scene里要设置成“pivot”）。 
![20241115155752](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20241115155752.png)

## 从“头”开始
为了展示您可以通过不同的方式运用这些概念来使角色栩栩如生，我们将通过实现一个简化的动画系统来驱动Bonehead，使之如同页面顶部视频中表现的那样。这个教程旨在让初学者易于理解，因此为了简明扼要，某些较为复杂的部分将仅作简单概述。 译注：其中的章节“Two-Bone IK”为译者补充的。

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
  > ``SerializeField`` 可以暴露非public的变量到Unity Inspector上。

  然后，将脚本挂载在守宫的根骨骼“Gecko”，然后在场景中添加上一个GameObject "LookTarget"并将其与“Gecko_Neck”一起挂载到Inspector上。![20241114174321](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20241114174321.png)

  我们目标是得到一个**四元数**其使守宫的头部朝向目标对象。 
  > 四元数是旋转的一种表达形式，本文中我们将其当作是“3D方向”处理，具体原理可以参考这篇[文章](https://krasjet.github.io/quaternion/quaternion.pdf)

  首先我们计算得到“头部”到目标对象的相对位移，即从头部对象的位置指向到目标对象位置的一个向量。
  ``` c#
  // 世界坐标向量：从头部指向目标的向量。
  Vector3 towardObjectFromHead = target.position - headBone.position;
  ```
  为了得到指向目标对象的的方向，我们调用 <a href=https://docs.unity3d.com/ScriptReference/Quaternion.LookRotation.html>“Quaternion.LookRotation”</a> 函数。这个(Unity)函数需要我们提供一个“Forward”方向与参考的“Up”方向，输出一个Z轴正方向指向"Forward"方向， y轴正方向与“Up”方向**相似**(二者点乘>0)的四元数。 
  > 这里的 ``up`` 参数只是 ``Quaternion.LookRotation``只是作为参考，实际的 ``up``方向（Y轴）必须是与``forward`` 成90°夹角的。 \
  (译注：) 这里的**相似**(``up``与Y轴的点乘>0)相当于从XZ平面上的两个潜在的Y轴方向中挑选出了一个更靠近 ``up``方向的。
  ``` c#
  headBone.rotation = Quaternion.LookRotation(towardObjectFromHead, transform.up);
  ```
  这里我们使headbone “Gecko_Neck” 的(局部空间)Z轴正方向指向目标对象，Y轴正方向与根骨骼的的Y轴正方向相似。 因为headbone的原先的Z轴正方向与头部朝向是相似的，所以我们便实现了对于头部朝向的控制。
  ![onebone2](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/onebone2.gif)

  （译者补充的内容：）
  通过添加下述调试指令，我们可以看到目前headbone的Z轴正方向已经与目标对象相交。
  ``` c#
  Debug.DrawLine(headBone.position, headBone.position + headBone.forward * 10, Color.red);
  ```
  ![20241115155936](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20241115155936.png)

  为了使头部运动表现更为自然，而不是瞬间移动和穿模，我们还需要对其的添加上阻尼和角度限制。
  - 阻尼： 首先将运动根据速度分帧处理，根据``Mathf.Lerp()``得到当前帧所在的位置。 通过[帧率无关的阻尼函数](https://www.rorydriscoll.com/2016/03/07/frame-rate-independent-damping-using-lerp/) 使用speed * Time.deltaTime 一方面可以模拟出越靠近Target时，位移变化越小的效果。 另一方面也保证了速率不会因为帧率的波动而波动。
    ``` c#
    current = Mathf.Lerp(
    current, 
    target, 
    speed * Time.deltaTime
    );
    ```
    但考虑到speed * Time.deltaTime可能存在大于1的情况，我们这里使用“1 - Mathf.Exp(-speed * Time.deltaTime)”来代替插值项，保证其始终在0~1之间。
    ``` c#
    current = Mathf.Lerp(
    current, 
    target, 
    1 - Mathf.Exp(-speed * Time.deltaTime)
    );
    ```
    > 译注：这里原文作者的思路似乎与[帧率无关的阻尼函数](https://www.rorydriscoll.com/2016/03/07/frame-rate-independent-damping-using-lerp/)一文的思路有所出入。这里采用了后者的思路，个人也认为这比较合理。

    因为我们的关节是通过旋转来改变(子关节的)位移的，我们需要使用四元数来表达。不过因为四元数为旋转，这里使用<a href = "https://discussions.unity.com/t/what-is-the-difference-of-quaternion-slerp-and-lerp/453377/19">Slerp</a>代替Lerp。从而得到我们代码实际使用的内容。
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

  - 角度限制： 我们会使用一个角度值来表示``headbone``所能转动的最大角度。因为是以``headbone``为基准进行判断，我们需要先将目标向量从世界空间下的表达转换到``headbone``的局部空间下再进行判断。
  ![localspace](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/localspace.gif)  
    <center>请注意: 当肩膀和肘部旋转时，手在世界坐标中的位置和旋转会发生变化，但它相对于肘部的局部位置和旋转保持不变！</center>

    局部变换是相对于骨骼的父骨骼的，因此需要通过``headbone``的父骨骼来进行变换，（如果是使用``headbone``的话还需要排除``headbone``自身的局部变换）。首先，通过 ``headBone.parent`` 获取头部父对象的引用，然后调用 [``InverseTransformDirection``](https://docs.unity3d.com/ScriptReference/Transform.InverseTransformDirection.html)。这个方法将一个方向从世界空间转换到局部空间。
    ``` C#
    Vector3 targetLocalLookDir = headBone.parent.InverseTransformDirection(targetWorldLookDir);
    ```
    得到指向目标的向量在``headbone``局部空间的表达后，Unity中提供了函数 <a href = "https://docs.unity3d.com/ScriptReference/Vector3.RotateTowards.html">Vector3.RotateTowards</a> 通过传入的四个参数: 初始/结束指向， 最大弧度，最大长度变化，计算出实际的结束指向。 接着根据得到的指向，使用 ``Quaternion.LookRotation`` 计算出其对应的3D方向（四元数）。
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

    //调试代码(译者补充的)
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
    ![onebone4](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/onebone4.gif)

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
  > ``LateUpadte``中的函数执行顺序是需要注意的。 因为眼部是头部的子骨骼，头部的旋转会影响眼部的位置。所以这里先更新头部再更新眼部。（译注：）越靠近根骨骼的骨骼先更新。

  因为眼球的运动范围是非对称的，这里我们规定眼球绕着各自的Y轴方向移动，并拥有眼球的追踪速度和各自独立的角度限制。
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
  使用类似前文头部追踪的方式使眼球平滑的转向目标方向。
  ``` C#
  // 计算目标旋转在世界空间下的表达。
  // 左右眼均指向“headbone”指向 target的方向。
  // 左右眼的指向平行，避免斗鸡眼的情况出现。
  Quaternion targetEyeRotation = Quaternion.LookRotation(target.position - headBone.position, transform.up);

  // 更新左眼的世界空间旋转
  leftEyeBone.rotation = Quaternion.Slerp(leftEyeBone.rotation, targetEyeRotation, 
      1 - Mathf.Exp(-eyeTrackingSpeed * Time.deltaTime));
  // 更新右眼的世界空间旋转
  rightEyeBone.rotation = Quaternion.Slerp(rightEyeBone.rotation, targetEyeRotation,
      1 - Mathf.Exp(-eyeTrackingSpeed * Time.deltaTime));
  ```
  不同于头部追踪中使用``RotateTowards``进行限制，这里我们采用欧拉角的形式来进行角度限制。欧拉角通过记录物体在其自身的三个坐标轴上的旋转来表达其的旋转，这很适合只绕着一个轴旋转的眼球运动。这将允许我们通过仅操作 <a href = "https://docs.unity3d.com/ScriptReference/Transform-localEulerAngles.html">Transform.localEulerAngles</a> 向量的单个分量，在局部空间中轻松限制一个轴上的旋转。
  >欧拉角是一种相较于四元数更为直观的旋转的表达形式，但其存在许多四元数没有问题。 关于欧拉角与四元数的一篇<a href = "https://web.archive.org/web/20220412171953/https://developerblog.myo.com/quaternions/">文章</a>。
  Unity中的“eulerAngles” 和 “localEulerAngles” 都是将欧拉角表达在0~360度之间，不过我们这里将其映射到-180~180度之间。 为此我们需要对介于180~360度之间的角度减去360度以进行矫正。
  ```C#
  // 以下代码放在上个代码片段（旋转眼球的代码）之后

  // 得到左右眼再局部空间下的绕Y轴的旋转
  float leftEyeCurrentYRotation = leftEyeBone.localEulerAngles.y;
  float rightEyeCurrentYRotation = rightEyeBone.localEulerAngles.y;

  // 映射范围外的角度到-180°~180°之间
  if (leftEyeCurrentYRotation > 180)
  {
      leftEyeCurrentYRotation -= 360;
  }
  if (rightEyeCurrentYRotation > 180)
  {
      rightEyeCurrentYRotation -= 360;
  }

  // 限制左右眼在局部空间中绕Y轴的旋转
  float leftEyeClampedYRotation =
      Mathf.Clamp(
          leftEyeCurrentYRotation,
          leftEyeMinYRotation,
          leftEyeMaxYRotation
      );
  float rightEyeClampedYRotation =
      Mathf.Clamp(
          rightEyeCurrentYRotation,
          rightEyeMinYRotation,
          rightEyeMaxYRotation
      );

  //更新左右眼在局部空间中绕Y轴的旋转
  leftEyeBone.localEulerAngles = new Vector3(
      leftEyeBone.localEulerAngles.x,
      leftEyeClampedYRotation,
      leftEyeBone.localEulerAngles.z
  );
  rightEyeBone.localEulerAngles = new Vector3(
      rightEyeBone.localEulerAngles.x,
      rightEyeClampedYRotation,
      rightEyeBone.localEulerAngles.z
  );
  ```
  ![eyelook](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/eyelook.gif)
  
  ### 两骨骼逆向动力学（Two-Bone IK）
  原文中并没有针对IK进行具体的讲解，而是在<a harf = "https://weaverdev.itch.io/procedural-animation-tutorial">项目源代码</a>中直接提供了一个现成的脚本。这里补充一个简单的IK算法。
  因为只对肢体的肩，肘，腕三个关节进行IK的计算，这里采用通过三角函数就可以得解的Two-Bone IK。 数学原理上可以参考这篇<a href = "https://zhuanlan.zhihu.com/p/447895503">文章</a>。 此外因为肩，肘，腕围成的三角形可能在3维空间存在无数个，所以我们额外引入一个点“Pole”来确定该三角形位于的平面，将解的数量下降为2个，然后再通过规定骨骼在平面的旋转方向得到唯一解。最后在引入点“Effector”作为目标位置，根据三角函数求解肩，肘，腕围成的三角形，得到肩，肘的旋转，使腕与effector尽量贴合，。![20241121162158](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20241121162158.png)
  <center>Pole与肩膀、肘、腕围成的三角形/线段，要保持在同一平面中</center>

  这里我们将pole放置在跟肩关节的父节点下，保证pole会随着骨骼整体的移动而移动，但其相对于IK 链上的各个关节则是相对静止的。![20241127162825](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20241127162825.png) \
  我们将规定骨骼在平面的旋转方向为逆方向（顺时针）， 故pole会像是吸引着肘关节，使肢体整体向着靠近pole位置的方向弯折。
  ![20241129113525](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20241129113525.png)
  具体的算法分为两个步骤： 首先是计算整个手臂的旋转，使肩关节指向target方向。然后计算肩，肘关节的局部旋转。将腕放置在target位置时，“肩-肘-腕”形成的三角形，根据三角形的夹角旋转肩，肘关节。
  - 整个手臂的旋转：
    - 首先移动整个肢体： 使用 LookRotation 将肩的局部空间下的正Z方向指向Effector，并且根据从肩到Pole的向量作为局部空间下的Y方向参考，得到绕Z轴的旋转。
      ``` c#
        // 世界空间向量：从肩到Pole
        var r2pTranslation = poleTransform.position - rootTransform.position;
        // 世界空间向量：从肩到Effector
        var r2eTranslation = effectorTransform.position - rootTransform.position;
        //将肩的局部坐标的正Z方向指向effector, 正y方向与指向pole的位置的方向相似。换言之，保证肩，pole，effector三点共面。
        rootTransform.rotation = Quaternion.LookRotation(r2eTranslation, r2pTranslation);
      ``` 
    - 计算旋转轴Normal： 叉乘向量“肩指向肘”的向量($\vec{T_K}$) , “肩指向effector”的向量($\vec{T_E}$) 得到二者的旋转轴。如果叉乘结果为0的话， 说明这两个向量为同向/相向的，即二者共面，那么就使用 $\vec{T_E}$与 “肩指向pole”的向量($\vec{T_P}$)叉乘的结果作为旋转轴。
      ``` c#
        // 世界空间向量：从肩到肘
        var t2mTranslation = kneeTransform.position - thighTransform.position;
        // 世界空间向量：从肩到肘所绕行的旋转轴
        var normal = Vector3.Cross(t2mTranslation, t2eTranslation).normalized;
        // 如果二者的叉乘为0，则二者同向或相向。
        // 因此二者在同一平面上，使用该平面的法线作为旋转轴
        if (Mathf.Approximately(normal.magnitude, 0f))
        {
            normal = Vector3.Cross(t2pTranslation, t2eTranslation).normalized;
        }
      ``` 
    - 使$\vec{T_K}$ 指向 $\vec{T_E}$方向： 使用 <a href = "https://docs.unity3d.com/ScriptReference/Vector3.SignedAngle.html">Vector3.SignedAngle</a>， 传入初始方向($\vec{T_K}$)，目标方向（$\vec{T_E}$），以及旋转轴Normal，可以得到这两个方向所构成的夹角的角度，以及该夹角对应的旋转方向（顺/逆时针）。 将Normal从世界空间转换到肩的局部空间中，然后使肩关节绕着Normal旋转上面计算得到的角度。      
      ``` c#
        // 计算肩到肘，与肩到effector所构成的夹角
        var thighRotationAngle = Vector3.SignedAngle(t2mTranslation.normalized, t2eTranslation.normalized, normal);
        // 忽略精度导致的计算误差
        if ((t2mTranslation - t2eTranslation).magnitude < 0.01f || Mathf.Abs(thighRotationAngle) < Mathf.PI / 180f)
            thighRotationAngle = 0f;
        // 将旋转轴从世界空间转换到肩的局部空间中
        normal = thighTransform.InverseTransformDirection(normal);
        // 计算旋转的四元数表达
        var thighRotation = Quaternion.AngleAxis(thighRotationAngle, normal);
        // 让肩，肘，effector共线。
        thighTransform.localRotation *= thighRotation;
      ``` 
  - 计算肩，肘关节的局部旋转：
    - 计算三角形的角度： 首先需要确定“肩->肘”，“肘->腕”，“肩->腕”这三个向量的模长，其中“肩->腕”的模长需要根据前二者的模长进行限制，保证其模长在前二者的差与和之间。    
      ```C#
        // 世界空间向量：从肘到腕
        var k2tTranslation = ankleTransform.position - kneeTransform.position;
        // 肩到肘的线段长度
        var t2mLength = t2mTranslation.magnitude;
        // 肘到腕的线段长度
        var k2tLength = k2tTranslation.magnitude;
        // 肩到effector的线段长度需要介于另两边的和与差之间
        var eps = 0.00001f;
        var t2eLength = Mathf.Clamp(t2eTranslation.magnitude, Mathf.Abs(t2mLength - k2tLength) + eps, t2mLength + k2tLength - eps);
      ```
      接着就可以根据余弦定理计算三角形的夹角
      ```C#
        // 计算腕->肩->肘构成的角度，即肩部要旋转的角度
        var a2t2mAngle = Mathf.Acos(Mathf.Clamp((t2mLength * t2mLength + t2eLength * t2eLength - k2tLength * k2tLength)
                                        / (2 * t2mLength * t2eLength), -1, 1));
        // 计算肩->肘->腕构成的角度的补角，即肘部要旋转的角度
        var t2k2tAngle = Mathf.PI - Mathf.Acos(Mathf.Clamp((t2mLength * t2mLength + k2tLength * k2tLength - t2eLength * t2eLength)
                                / (2 * t2mLength * k2tLength), -1, 1));
      ```
    - 计算旋转轴，旋转肩，轴关节： 规定从pole旋转到effector的方向为正方向，所以使用"肩->pole"叉乘"肩->effector"得到旋转轴。 因为希望肘与pole在同一侧，所以这里肩关节逆着旋转轴旋转。
      ![20241129114402](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20241129114402.png)
      将肘关节的局部旋转置零（即“肘到腕的向量”与“肩到肘的向量”同向），然后再逆着刚才肩关节的旋转方向，旋转由肩->肘->腕构成的角度的补角。
      ```C#
      // 规定从pole旋转到effector的方向为角度的正方向
      normal = Vector3.Cross(t2pTranslation, t2eTranslation).normalized;
      // 如果二者的叉乘为0，则二者同向或相向。
      // 因此二者在同一平面上，使用该肩到腕，转到肩到effector的方向为正方向。
      if (Mathf.Approximately(normal.magnitude, 0f))
      {
          var t2tTranslation = ankleTransform.position - thighTransform.position;
          normal = Vector3.Cross(t2tTranslation, t2eTranslation).normalized;
      }

      // 肩到肘的向量绕着旋转轴旋转
      // 因为我们希望肘与pole在同一侧，所以这里顺时针旋转(旋转轴的逆方向: -normal)
      var t2mRotation = Quaternion.AngleAxis(a2t2mAngle * Mathf.Rad2Deg, thighTransform.InverseTransformDirection(-normal));
      // 将肘的局部旋转置零，即肘到腕的向量与肩到肘的向量同向。
      thighTransform.rotation = kneeTransform.rotation = thighTransform.rotation * t2mRotation;
      // 肘到腕的向量绕着旋转轴旋转
      // 逆着肩到肘的向量的旋转方向进行旋转
      var k2tRotation = Quaternion.AngleAxis(t2k2tAngle * Mathf.Rad2Deg, kneeTransform.InverseTransformDirection(normal));
      kneeTransform.rotation = thighTransform.rotation * k2tRotation;
      ```
      ![legik](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/legik.gif)
       <center>Pole的位置（球）， 和 effector的位置（正方体）。 Pole位于根骨骼下，会随着骨骼整体的移动而移动。</center>
  
  ### 迈步(Leg Stepping)
  守宫的步态循环相较于人物的行走是较为简单的，因此我们可以使用一个简单的脚本来完成从A点到B点的运动。而后者往往会使用关键帧（动画）来控制身体的姿态，只有在落脚处采用了程序化动画。 \
  本文中为了确定落脚点，我们为肢体各设置一个“原点(home)位置”。其位于我们落脚点可以触及的范围的中心，与pole一样与肩关节保持相对静止。此外我们还会将其用于脚的旋转，因此需要将其定向为脚在静止状态下应有的朝向。
  ![homepos](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/homepos.gif) \
  我们会根据“原点位置”决定何时何地移动effector，而当肢体超出了我们圈定的范围时，则会触发一步回到“原点位置”的动作。
  首先创建一个叫 ``LegStepper`` 的脚本来处理相关的逻辑，其参数包含 “原点位置” 和其对应的effector的位置。
  ``` c#
  using UnityEngine;

  public class LegStepper : MonoBehaviour
  {
    // "原点" 的位置与旋转
    [SerializeField] Transform homeTransform;
    // 以“原点”为中心圈定的范围的半径
    // 即肢体可以离开“原点”的最大距离
    [SerializeField] float wantStepAtDistance;
    // 每一步需要花费多长时间完成
    [SerializeField] float moveDuration;
    
    // 是否正在移动
    public bool Moving;
  }
  ```
  因为步伐不应该是瞬时的，我们使用协程将这一运动过程进行分帧处理，使之逐步的接近目标。
  以下代码使肢体从现在的位置，旋转经过 ``moveDuration`` 长的时间移动到“原点”的位置，旋转。
  ``` c#
  // 协程需要返回IEnumerator对象
  IEnumerator MoveToHome()
  {
    // 表明正在移动中
    // 肢体正在执行一个协程，避免重复创建与冲突
    Moving = true;

    // 保存初始数据
    Quaternion startRot = transform.rotation;
    Vector3 startPoint = transform.position;

    Quaternion endRot = homeTransform.rotation;
    Vector3 endPoint = homeTransform.position;

    // 步伐开始的时间
    float timeElapsed = 0;

    // 这里使用了do-loop结构，normalizedTime在最后一次循环时会超过1。原文中担心这会导致错误的结果，
    // 但Unity提供的Vector3.Lerp，Quaternion.Slerp会对传入的normalizedTime做clamp01操作，所以不用做额外的操作。
    do
    {
      // 添加上一帧到目前经过了的时间
      timeElapsed += Time.deltaTime;

      float normalizedTime = timeElapsed / moveDuration;

      // 插值得到当前帧所在的位置
      transform.position = Vector3.Lerp(startPoint, endPoint,normalizedTime);
      transform.rotation = Quaternion.Slerp(startRot, endRot, normalizedTime);

      // 等待到下一帧继续执行
      yield return null;
    }
    while (timeElapsed < moveDuration);

    // 结束移动
    Moving = false;
  }
  ```
  为了触发上面的协程，我们在``Update``中对肢体到原点的距离进行检测，当肢体在范围外时触发。
  ``` c#
  void Update()
  {
    // 如果正在移动，不另外开启协程
    if (Moving) return;

    float distFromHome = Vector3.Distance(transform.position, homeTransform.position);

    // 如果肢体超过了范围
    if (distFromHome > wantStepAtDistance)
    {
        // 开始移动（创建协程）
        StartCoroutine(MoveToHome());
    }
  }
  ```
  ![step](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/step.gif) \
  为了使动作更加生动， 可以尝试将肢体稍微抬起，离开地面一些。并且在动作幅度较大时，不是正好落在“原点”上, 而是略微偏离一些。为此我们可以使用嵌套的线性插值实现的二次贝塞尔曲线来模拟运动轨迹。
  ``` c#
  // 新的变量
  // 超出"原点"最大距离的比例。
  [SerializeField] float stepOvershootFraction;

  IEnumerator Move()
  {
    Moving = true;

    Vector3 startPoint = transform.position;
    Quaternion startRot = transform.rotation;

    Quaternion endRot = homeTransform.rotation;

    // 世界空间向量：从effector到“原点”的向量
    Vector3 towardHome = (homeTransform.position - transform.position);
    // 计算过冲向量
    float overshootDistance = wantStepAtDistance * stepOvershootFraction;
    Vector3 overshootVector = towardHome * overshootDistance;
    // 将过冲向量投影在地面上（XZ平面）
    // 计算过冲向量在XZ方向的偏移，
    overshootVector = Vector3.ProjectOnPlane(overshootVector, Vector3.up);

    // 在原点位置上加上过冲带来的偏移
    Vector3 endPoint = homeTransform.position + overshootVector;

    // 计算运动轨迹的中点
    // 在Y方向上加上一些偏移，使步伐抬起移动距离的一半
    Vector3 centerPoint = (startPoint + endPoint) / 2;
    centerPoint += homeTransform.up * Vector3.Distance(startPoint, endPoint) / 2f;

    // 步伐开始的时间 
    float timeElapsed = 0;
    // 这里使用了do-loop结构，normalizedTime在最后一次循环时会超过1。原文中担心这会导致错误的结果，
    // 但Unity提供的Vector3.Lerp，Quaternion.Slerp会对传入的normalizedTime做clamp01操作，所以不用做额外的操作。
    do
    {
      timeElapsed += Time.deltaTime;
      float normalizedTime = timeElapsed / moveDuration;

      // 二次贝塞尔曲线
      transform.position =
        Vector3.Lerp(
          Vector3.Lerp(startPoint, centerPoint, normalizedTime),
          Vector3.Lerp(centerPoint, endPoint, normalizedTime),
          normalizedTime
        );

      transform.rotation = Quaternion.Slerp(startRot, endRot, normalizedTime);

      // 等待到下一帧继续执行
      yield return null;
    }
    while (timeElapsed < moveDuration);

    // 结束移动
    Moving = false;
  }
  ```
  ![step2](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/step2.gif)
    <center>使用二次贝塞尔曲线控制运动轨迹</center> 
  但这动作看起来还是有些平淡。最后让我们为动作添加一些<a herf = "https://easings.net/en">缓动(Easing)</a>效果。 实现缓动的方法有很多，这里提供其中<a href = "https://gist.github.com/Fonserbc/3d31a25e87fdaa541ddf">一种</a>。 确定好方法后，便可将 ``normalizedTime`` 传入缓动函数中从而作用于步伐上。
  ![step3-1](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/step3-1.gif)
  <center>加上缓动之后</center>
  看起来不错，但还是有些问题存在。 因为各个肢体是独自控制其自身的移动状态的，因此可能存在太多肢体同时腾空的情况。

  ![step4](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/step4.gif)   <center>四肢各自控制其运动</center>
  为了解决这个问题，我们可以统一管理这些肢体。 只有处于对角线上的一对肢体(左前-右后， 右前-左后)会同时进行移动。
  目前肢体是否移动是在类LegStepper的 ``Update()`` 中检测的，我们可以转而在 ``GeckoController`` 脚本中实现。 为此，首先将 ``Update()`` 中的代码提取成一个public函数 ``public void TryMove()``。
  ``` c#
  // 之前void Update()中的代码
  public void TryMove()
  {
    if (Moving) return;

    float distFromHome = Vector3.Distance(transform.position, homeTransform.position);

    // 如果肢体超过了圈定的范围
    if (distFromHome > wantStepAtDistance)
    {
      StartCoroutine(Move());
    }
  }
  ```
  回到``GeckoController``脚本， 添加上对四肢的引用。
  ``` c#
  [SerializeField] LegStepper frontLeftLegStepper;
  [SerializeField] LegStepper frontRightLegStepper;
  [SerializeField] LegStepper backLeftLegStepper;
  [SerializeField] LegStepper backRightLegStepper;
  ```
  接着，创建协程来成对的驱动肢体。
   ``` c#
  // 只允许对角线上成对的肢体同时在移动
  IEnumerator LegUpdateCoroutine()
  {
    // 循环运行
    while (true)
    {
      // 尝试移动左前-右后这对肢体
      do
      {
        frontLeftLegStepper.TryMove();
        backRightLegStepper.TryMove();
        // 等待到下一帧继续运行
        yield return null;

      // 在任一条肢体移动时停留在这个循环中。
      // 如果只有一条肢体在移动，调用 TryMove() 会允许另一条腿在合适的情况下开始移动
      } while (backRightLegStepper.Moving || frontLeftLegStepper.Moving);

      // 尝试移动右前-左后这对肢体
      do
      {
        frontRightLegStepper.TryMove();
        backLeftLegStepper.TryMove();
        yield return null;
      } while (backLeftLegStepper.Moving || frontRightLegStepper.Moving);
    }
  }
  ``` 
  > 上述代码在守宫移动过快时可能导致其中一对肢体一直腾空。 

  最后在``GeckoController``的 ``Awake``函数中创建刚刚实现的协程函数。
  ``` C#
  void Awake()
  {
      StartCoroutine(LegUpdateCoroutine());
  }
  ```
  ![step5](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/step5.gif)
  <center>只有对角线上的一对肢体同时在移动</center>

  ### 整体运动(Root Motion)
  因为我们的动画系统以及可以独立地对世界做出反应，我们可以其视为一个单一的个体，而不需要考虑四肢或其他细枝末节，在世界空间中轻松地移动整只守宫。
  接下来我们会通过速度来控制守宫整体的位移与旋转。为此需要确认一个*目标速度*，平滑地将*当前速度*过渡到*目标速度*， 并将其应用在守宫上。
  让我们在``GeckoController``类中实现。首先，设置我们将使用的一些参数，变量
  ``` C#
  // 最大的角速率，移动速率
  [SerializeField] float turnSpeed;
  [SerializeField] float moveSpeed;
  // 最大的角加速度值，加速度值
  [SerializeField] float turnAcceleration;
  [SerializeField] float moveAcceleration;
  // 距离目标的最近/最远距离
  [SerializeField] float minDistToTarget;
  [SerializeField] float maxDistToTarget;
  // 如果与目标的角度差大于这个值则开始旋转
  [SerializeField] float maxAngToTarget;

  // 世界空间下的速度
  Vector3 currentVelocity;
  // 当前的角速度
  // 因为只绕着垂直向上的轴旋转，所以只是一个float。
  float currentAngularVelocity;
  ```
  如同我们在头部追踪上的实现，我们也可以使用平滑函数来控制速度的变化，使之自然地过渡到目标速度。首先，我们先将守宫的身体转向目标。
  ``` C#
  void RootMotionUpdate()
  {
    // 世界空间坐标： 指向目标的方向
    Vector3 towardTarget = target.position - transform.position;
    // 计算towardTarget在局部的XZ平面上的投影
    Vector3 towardTargetProjected = Vector3.ProjectOnPlane(towardTarget, transform.up);
    // 计算守宫的forward方向与目标方向的夹角的角度，已经旋转的方向
    float angToTarget = Vector3.SignedAngle(transform.forward, towardTargetProjected, transform.up);

    float targetAngularVelocity = 0;

    // 如果角度差在容许的范围内，停止旋转
    if (Mathf.Abs(angToTarget) > maxAngToTarget)
    {
      // Unity中顺时针时正方向，即向右旋转的角度为正
      if (angToTarget > 0)
      {
        targetAngularVelocity = turnSpeed;
      }
      // 如果目标在左侧，反转角速度的方向
      else
      {
        targetAngularVelocity = -turnSpeed;
      }
    }

    // 使用平滑函数逐步改变速度
    currentAngularVelocity = Mathf.Lerp(
      currentAngularVelocity,
      targetAngularVelocity,
      1 - Mathf.Exp(-turnAcceleration * Time.deltaTime)
    );

    // 在世界空间中绕着Y轴旋转，角度为delta time(自上一帧后经过的世界) 乘以当前速度。
    transform.Rotate(0, Time.deltaTime * currentAngularVelocity, 0, Space.World);
  }
  ```
  ![turning](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/turning.gif) \
  接下来还需要处理位移，我们根据参数``minDistToTarget``, ``maxDistToTarget``将守宫与目标保持在一定的范围内。当距离过大时靠近，距离过小时则远离。
  ``` C#
  //// 放在RootMotionUpdate函数中， 旋转相关的代码后面 ////
  Vector3 targetVelocity = Vector3.zero;

  // 先旋转再移动。 没有面向目标时不移动。
  if (Mathf.Abs(angToTarget) < 90)
  {
    float distToTarget = Vector3.Distance(transform.position, target.position);

    // 距离目标较远时，靠近目标
    if (distToTarget > maxDistToTarget)
    {
      targetVelocity = moveSpeed * towardTargetProjected.normalized;
    }
    // 距离目标较远时，（反转方向），原理目标
    else if (distToTarget < minDistToTarget)
    {
      targetVelocity = moveSpeed * -towardTargetProjected.normalized;
    }
  }

  currentVelocity = Vector3.Lerp(
    currentVelocity,
    targetVelocity,
    1 - Mathf.Exp(-moveAcceleration * Time.deltaTime)
  );

  // 加上计算这一帧的位移
  transform.position += currentVelocity * Time.deltaTime;
  ```
  最后别忘了在 ``LateUpdate()`` 中加上 ``RootMotionUpdate()``。 因为头部追踪依赖整体的方向，所以要将``RootMotionUpdate()``放在第一项。
  ```C#
  void LateUpdate()
  {
      RootMotionUpdate();
      HeadTrackingUpdate();
      EyeTrackingUpdate();
  }
  ```

## 结语
尽管还有许多可以改进的地方，但这个教程总得有个结束。这只是纯骨骼控制的入门介绍，然而程序化动画的一个重要部分是建立在关键帧动画的基础上，我们可能会在未来的教程中进一步探讨这一点。
![ravenlook](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/ravenlook.gif)
<center>头部追踪加上待机(Idle)动画</center>
更多关于这方面的资料，可以参考David Rosen关于复仇格斗兔2（*Overgrowth*）中的程序化动画系统的[GDC 演讲: 《Animation Bootcamp: An Indie Approach to Procedural Animation》](https://www.youtube.com/watch?v=LNidsMesxSE), 以及Joar Jakobsson和James Therrien的关于雨世界（*Rain World*）使用的程序化动画的[演讲： 《The Rain World Animation Process》](https://www.youtube.com/watch?v=sVntwsrjNe4)

![bonehead](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/bonehead.gif)
<center>谢谢</center>