GPU Thread（lane？）间同步/共享的方式
- 全局同步： 数据储存在GPU内存中，通过原子操作保持通过。（？）
- 局部同步: 数据储存在GPU处理器内核的局部Cache （Shared Memory） （LSD?）中, 通过Group Memory Barrier实现组内线程同步。
    > 处理器核心: Nvidia:Streaming Multiprocessor（SM）; AMD: Compute Unit（CU）; Mali: Execution Engines
- 看下文

Warp/Wavefront/SIMD-group : 
- 最小调度单元，一个SM中存在多个（？）, 以实现同步处理多个数据（？）。 Warp 由lane(近似于thread？)组成，同一个Wrap中的lane执行相同的指令，但可以处理不同的数据。（类似SIMD/SIMT）。 
- Warp size/ Warp width: 一个Warp可以容纳的lane数量。
- lane状态: Active lane/ inactive lane, 以下几种情况会导致 inactive lane（其实就是Shader Divergency？）
  - （non-uniform）Branching
  - 未填满 warp 大小的 Thread Group，当 Thread Group 大小不是 Warp 大小的整数倍时，没有分配任务的 lane 在整个 Shader 的执行过程中一直是 Inactive 状态而不会发生改变。
  - Quad Overdraw: Quad 是另一种 lane 的组织形式，有固定 4 个 lane，一个 Warp 可以划分多个 Quad。在 Pixel Shader 中是 2x2 的像素排列，布局顺序如下：![20260727154332](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20260727154332.png)
    Quad 中的 lane都是active的，但是对最终渲染结果没贡献的成为Help Lane （help pixel？），同一Wrap能可能存在整个Quad没跟三角形相交导致的Inactive？![20260727154641](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20260727154641.png)
    > Quad 在Compute shader中时线性布局。