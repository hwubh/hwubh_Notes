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
    > 我自己试下来 quad中 lane 是不是 active是GPU决定的？ DX12上用hlsl写，fxc编译，ddx_fine ddy_fine 很多时候是没有输出的。 https://microsoft.github.io/DirectX-Specs/d3d/HLSL_SM_6_7_QuadAny_QuadAll.html

Wave Intrincis (DX12)
- def: 内置函数，专用于控制 warp 的 lane 间共享和同步数据. 
  > 支持检查: ID3D12Device::CheckFeatureSupport
- Func：
  - 查询： 查询 Lane 状态: lane 数量，索引，最小索引active lane
  - 投票（WaveActiveAny*）: 比较 Active lane 之间的取值: 判断表达式在active lane 是否为true, AnyTrue,AllTrue,Ballot(返回最多表达128个lane结果的mask bit : uint4)
  - 广播（WaveReadLane*）: Active Lane 间的数据通信： 获取表达式在指定/最小索引lane上的取值。
  - 归约: 归约 warp 中所有 Active Lane 的结果，这个结果适用于所有的active lane，为uniform值： Equal、Add、Mul、Min、Max、And、Or、Xor
  - 前缀计算（WavePrefix*）: 局部归约？ 归约所有小于当前 Lane Index 的 Active Lanes （不包含当前lane）的结果。 有 sum, product, CountBits（bool返回值为True的数量）
  - 前缀计算(WaveMultiPrefix*): 在前缀计算（WavePrefix*）基础上，可以通过bit mask 过滤前缀中一些不希望参与的lane。
  - Quad 数据交换（Shuffle）: Quad 范围内的相邻的 Lane 之间读取和交换数据： 可以读取指定位置上lane，和 水平/垂直/对象方向的lane的表达式取值。
  - WaveMatch: 将当前lane的取值与Wave 中其它的 Active Lane 进行比较，返回最多表达128个lane结果的mask bit : uint4。
  - IsHelperLane： 判断当前active lane是否式halper lane
  - wavesize： 可以指定compute shader的 wave size。
  - QuadAny/QuadAll： quad版本的投票
  - Helper Lanes 支持 Wave Ops:  Pixel Shader 入口函数加上 [ WaveOpsIncludeHelperLanes] 属性，显式要求是否在 Helper Lane中执行所有 Wave Ops。
  >  Wave Ops： 查询，投票，广播，归约，前缀计算

Vulkan Subgroup(Vulkan 1.1 开始)

Metal SIMD-group

https://zhuanlan.zhihu.com/p/469436345

## ddx/ddy 在全屏 Blit Pass 中失效的实测分析

### 背景
在 URP UberPost fragment shader 中使用 `ddx(L)`/`ddy(L)` 计算 luma 梯度，写入 R32_UINT UAV。发现所有像素的导数返回值接近 0，且改用 `ddx_fine` 也不生效。

### 现象
`x&0` 恒为 0 即条件恒 true，用于单独控制某一轴的筛选。2×2 quad 中像素布局（Direct3D）：
- P0=(偶x, 偶y) 左上, P1=(奇x, 偶y) 右上, P2=(偶x, 奇y) 左下, P3=(奇x, 奇y) 右下

单个条件测试 `ddx_fine` 的返回值：

| 写入条件 | 筛选轴 | 写入的像素 | 数量 | dX |
|----------|--------|-----------|------|-----|
| `x&0==0 && y&0==0` | 无筛选（全写） | P0,P1,P2,P3 | 4 | **非零** |
| `x&1==0 && y&1==0` | 偶x 且 偶y | P0 | 1 | 0 |
| `x&0==0 && y&1==0` | 仅偶y（上排） | P0,P1 | 2 | 0 |
| `x&1==0 && y&0==0` | 仅偶x（左列） | P0,P2 | 2 | 0 |

三个条件（排除全写）任意两两组合后 `ddx_fine` 恢复非零：

| 组合 | 覆盖像素 | 数量 | dX |
|------|----------|------|-----|
| 条件2 + 条件3 | P0,P1 | 2 | **非零** |
| 条件2 + 条件4 | P0,P2 | 2 | **非零** |
| 条件3 + 条件4 | P0,P1,P2 | 3 | **非零** |

**规律：单个条件只写 1-2 个像素 → 导数为 0；两个条件组合（即使仍只覆盖 2 个像素）→ 导数恢复非零。** 说明问题不只是写入像素的数量，而是编译器对单条件 if 的优化导致部分 lane 被跳过，多个 UAV 写入路径阻止了这种优化。

### 原因

GPU 对不产生任何输出（framebuffer + UAV）的像素会跳过 shader 执行（标记为 inactive lane）。当只有部分像素写 UAV 时：

1. 不写 UAV 的像素被 GPU 判定为无输出 → 标记为 inactive → 不执行 fragment shader
2. 2×2 quad 被打散 → `ddx`/`ddy`（以及 `ddx_fine`/`ddy_fine`）没有完整的 4 个邻居可比较 → 返回 0
3. 只有当 quad 内所有 4 个像素都参与执行时，导数才能正常工作

这与 Ben Golus 文章（<https://bgolus.medium.com/distinctive-derivative-differences-cce38d36797b>）中描述的 in-quad communication 技术一致：导数依赖 2×2 quad 中 4 个像素的寄存器值差，必须有完整的 quad 参与。

### 解决方案

**让所有 4 个像素都写 UAV**，确保 GPU 不会跳过任何 pixel，quad 保持完整。

由于 in-quad communication 使 4 个像素计算出相同的梯度值（取 quad 内最大差值），它们写同一个 `screenPos >> 1` 位置时值相同，无写入竞争问题。

### ddx (coarse) vs ddx_fine

根据文章，`ddx`（coarse）对整个 quad 返回同一个值（P1-P0），忽略 P3；`ddx_fine` 给出逐行/逐列导数，能获取 quad 内全部 4 个像素的信息。进行 in-quad communication 重建 4 个 luma 值时需要使用 `ddx_fine`/`ddy_fine`。