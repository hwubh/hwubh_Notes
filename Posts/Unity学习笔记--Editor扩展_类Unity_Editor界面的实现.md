# Unity学习笔记--Editor扩展：类Unity编辑器界面的实现

- 提要：本文主要通过反射来获取集合“EditorWindow”下的类，进而在实现类似Unity 编辑器界面的复刻。
---
- 前言： 这阵子接到一个扩展Unity Editor的需求，希望能实现多个页签（tab/ EditorWindow）集合在一个窗口中，而且这个窗口的最上方有一个标题的样式（大致如下图所示）。 \
![20240826182334](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240826182334.png)

- GUI层级： 考虑到上文的每个“tab”都是一个“<a href="https://docs.unity3d.com/6000.0/Documentation/ScriptReference/EditorWindow.html">EditorWindow”</a>, 那么能容纳其的窗口可能是一个“Docker”，一个“SplitView”, 乃至于一个“ContainerWindow”。（三者的关系可以参照下图）\
![20240828103437](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240828103437.png) (<a href=https://blog.csdn.net/qq_18192161/article/details/107867320>图片出处</a>) <br>
以Unity 编辑器为例，整个编辑器也可以同样划分为从大到小的四个层级。<br>![20240902162648](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240902162648.png)

- 方案选择：
接下来需要要确定的就是我们容纳“tab”的容器应该是“Docker”，“SplitView”，还是“ContainerWindow”。通过对比各个界面的默认样式，可以注意到实际上Unity 编辑器的界面其实是与需求较为符合的。<br>
- “(<a href=https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/GUI/DockArea.cs>DockArea.cs</a>)” 界面