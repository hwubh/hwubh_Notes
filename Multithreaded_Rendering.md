Multithreading Rendering 多线程渲染:
# Mode
- 环形缓冲区（RingBuffer / 双Buffer）： Main Thread + RHI Thread ： RHI Thread 只负责图像API的调用。
- 渲染线程全托管: Main Thread + Rendering Thread / Main Thread + Rendering Thread + RHI Thread: 拷贝主线程的 Entity（实体）状态到渲染线程，所有渲染相关的内容（可见性剔除、数据生成、命令组装、API 提交）从主线程上分离。
- 多线程并发提交 : 在多个线程中同时执行 API 的提交; 将 Rendering Thread 进行拆分，各个线程上各自提交Command buffer并调用图像API。