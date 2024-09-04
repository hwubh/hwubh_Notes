# Unity学习笔记--Editor扩展：类Unity编辑器界面的实现

## 提要：
本文主要通过反射来获取集合“EditorWindow”下的类，进而在实现类似Unity 编辑器界面的复刻。 Unity版本：2022.3.44f1.

## 前言： 
这阵子接到一个扩展Unity Editor的需求，希望能实现多个页签（tab/ EditorWindow）集合在一个窗口中，而且这个窗口的最上方有一个标题的样式（大致如下图所示）。 \
![20240904174706](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240904174706.png)

## GUI层级： 
考虑到上文的每个“tab”都是一个“<a href="https://docs.unity3d.com/6000.0/Documentation/ScriptReference/EditorWindow.html">EditorWindow”</a>, 那么能容纳其的窗口可能是一个“Docker”，一个“SplitView”, 乃至于一个“ContainerWindow”。（三者的关系可以参照下图）\
![20240828103437](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240828103437.png) (<a href=https://blog.csdn.net/qq_18192161/article/details/107867320>图片出处</a>) <br>
以Unity 编辑器为例，整个编辑器也可以同样划分为从大到小的四个层级。<br>![20240904140710](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240904140710.png)

## 方案选择：
接下来需要要确定的就是我们容纳“tab”的容器应该是“Docker”，“SplitView”，还是“ContainerWindow”。这几个类均有在Unity给出的参考源码中(<a href=https://github.com/Unity-Technologies/UnityCsReference/tree/2022.3>C#源码地址</a>)。
- “(<a href=https://github.com/Unity-Technologies/UnityCsReference/blob/2022.3/Editor/Mono/GUI/DockArea.cs>DockArea.cs</a>)” ： 从DockArea的绘制指令来看，其大致将整个界面分为三个区域，分别是<span style="color:red">**Tab的背景**</span>, <span style="color:green">**Tab**</span>, 和<span style="color:blue">**EditorWindowd的界面**</span>。 如果要实现需求中的标题效果，可以考虑修改titleBarRect的高度，但titleBarRect的高度是由 (readonly) 变量 m_BorderSize决定的。故总体而言并不太好在DockerArea的基础上修改。 ![20240904142031](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240904142031.png)
- “(<a href=https://github.com/Unity-Technologies/UnityCsReference/blob/2022.3/Editor/Mono/GUI/SplitView.cs>SplitView.cs</a>)”: SplitView中会根据其*children*的数量n(类：UnityEditor.View)将其自身划分为n个区域，每个区域显示一个*View*。理论上是可以在其上添加新的View来实现需求的。但SplitView的View的子类，本身不能绘制，需要挂载在ContainerWindow上？![20240904144830](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240904144830.png)
- “(<a href=https://github.com/Unity-Technologies/UnityCsReference/blob/2022.3/Editor/Mono/ContainerWindow.cs>ContainerWindow.cs</a>)”： 通过对比各个界面的默认样式，可以注意到实际上。分为上，中，下三个部分的Unity 编辑器界面其实是与上述的需求较为符合的。![20240904151145](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240904151145.png)。 通过向上查找绘制ContainerWindow的绘制函数“Show(ShowMode showMode, bool loadPosition, bool displayImmediately, bool setFocus)”的引用，我们可以找到编辑器界面加载**layout**的函数（位于<a href=https://github.com/Unity-Technologies/UnityCsReference/blob/2022.3/Editor/Mono/GUI/WindowLayout.cs>WindowLayout.cs</a>）![20240904151512](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240904151512.png)。 该函数通过传入的三个struct对象“UnityEditor.WindowLayout.LayoutViewInfo” ，生成一个包含Top，center，buttom三个区域的“ContainerWindow”对象（其实就是Unity编辑器）。换言之，我们只需要自行创建三个“View”对应三个区域创建出类似"Unity编辑器"的窗口。

## 代码实现（从后向前）：
- 1：ContainerWindow： 找到函数中“ContainerWindow”的所有引用，可知我们需要调用ContainerWindow类中的“Show”， “DisplayAllViews” 函数， 设置“rootView”， “rootView.position”这两个属性。 （P.S. Show()函数的第一个传参（<a href=https://github.com/Unity-Technologies/UnityCsReference/blob/2022.3/Editor/Mono/ContainerWindow.bindings.cs>ShowMode</a>）不要填 **4**，否则关闭这个ContainerWindow时，Unity编辑器也会跟着关闭）![20240904152959](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240904152959.png)![20240904153431](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240904153431.png)
- 2：<a href=https://github.com/Unity-Technologies/UnityCsReference/tree/2022.3>MianView.cs</a>: 从图 ![20240904153611](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240904153611.png) 可知“ContainerWindow”对象“mainContainerWindow”给自己的“rootView”属性赋值了一个“mainView”。通过引用，可知“mainView”是一个“MainView”类的对象，其包含了上文提到的“top”， “center”， “buttom”三个"view"对象。![20240904154157](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240904154157.png) 对应的代码如下：![20240904154452](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240904154452.png)
- 3： TopView, CenterView, ButtomView: MainView.AddChild 的传参为View类型，本文中使用View的子类"GUIView"作为TopView，ButtomView， 子类“SplitView”作为CenterView。
  - TopView：当使用UI Toolkit 绘制UI时，可以利用反射从“GUIView”对象中，拿到其类型为“visualElement”的成员变量“visualTree”， 将想绘制的UI Element添加到“visualTree”中。![20240904170612](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240904170612.png)
  - ButtomView： 当使用 IMGUI绘制UI时，可以使用参照TopView方式，不过需要先用IMGUIContainer将 IMGUI指令包装成Visual Element。 （P.S. 另一种方式是：创建“GUIView”的子类，覆写函数 OldOnGUI（）。 OnGUIHandler只有“get”访问器，无法通过反射进行赋值。总体来说不太推荐这个方法，其底层逻辑也是通过IMGUIContainer进行封装。）![20240904171704](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240904171704.png)![20240904172142](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240904172142.png)
  - CenterView： “SplitView”对象包含若干个"DockArea"对象。创建若干个“EditorWindow”对象，添加到“DockArea”中。创建若干个“DockArea”，添加到“SplitView”中。（P.S.： 若使用的是自定义的EditorWindow的子类，子类中不要覆写构造函数, 否则 “ScriptableObject.CreateInstance” 调用的是子类的构造函数，一些继承的基类的成员变量不会被初始化赋值）![20240904172418](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240904172418.png)

## 最后效果：
![20240904174236](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240904174236.png)

## 参考资料：
- https://blog.csdn.net/qq_18192161/article/details/107867320
- https://github.com/Unity-Technologies/UnityCsReference/tree/2022.3
## 项目代码：
- https://github.com/hwubh/hwubh_post_code/tree/main/Unity%E5%AD%A6%E4%B9%A0%E7%AC%94%E8%AE%B0--Editor%E6%89%A9%E5%B1%95_%E7%B1%BBUnity_Editor%E7%95%8C%E9%9D%A2%E7%9A%84%E5%AE%9E%E7%8E%B0/Editor