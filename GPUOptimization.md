优化流程:
- 整体GPU平均消耗 -> 判断是计算bound，还是带宽bound
- 截帧分析瓶颈Pass -> 优化Pass消耗
- 分析GPU调度顺序: 有时即使优化了pass本身的消耗，因为硬件调度问题/硬件分配问题(比如Mali旧架构上compute shader 和 vertex shader 共用提交硬件)，可能出现Bubble。
  - 优化策略1: 调整资源关系，调整Pass执行顺序。
  - 优化策略2: 调整（compute/geometry/fragment）stream， 比如调整使用的stream（cs 转 fs），使用async compute。尽可能做到并行。
    ![20260910163223](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20260910163223.png) 优化前，因为compute stream，下方fragment stream 出现了bubble。
    ![20260910164119](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20260910164119.png) 优化后，将compute stream部分移动到了fragment中，防止出现bubble。