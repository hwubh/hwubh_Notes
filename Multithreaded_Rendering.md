Multithreading Rendering 多线程渲染:
# Mode
- 环形缓冲区（RingBuffer / 双Buffer）： Main Thread + RHI Thread ： RHI Thread 只负责图像API的调用。
- 渲染线程全托管: Main Thread + Rendering Thread / Main Thread + Rendering Thread + RHI Thread: 拷贝主线程的 Entity（实体）状态到渲染线程，所有渲染相关的内容（可见性剔除、数据生成、命令组装、API 提交）从主线程上分离。
- 多线程并发提交 : 在多个线程中同时执行 API 的提交; 将 Rendering Thread 进行拆分，各个线程上各自提交Command buffer并调用图像API。

# Mode：https://zhuanlan.zhihu.com/p/44116722 如果只有 主线程 和 渲染线程
-  双队列+帧同步方案: 主线程/渲染线程 使用不同的队列，主线程填充队列A时， 渲染消费队列B。完成渲染后交换队列。线程之间的数据传递使用智能指针，即主线程和渲染线程共享一份资源，因此智能指针必须是线程安全的. ![20261028164817](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20261028164817.png)
   -  如何保证线程安全： 
      -  禁止在资源析构函数中调用主线程的函数
      -  使用资源GC，即资源的分配和释放完全在主线程中完成，分配资源时，将资源放到主线程的资源管理器中，引用计数为1，在每一帧结束时，将资源管理器中引用计数为1的资源删除
-  管道方案: 参考Unity Multithreading 
-  环形队列方案: 类似管道方案，但是渲染指令加入队列时不序列化。与双队列类似，主线程与渲染线程采用智能指针共享资源。 ![20261028164800](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20261028164800.png)

Unity Multithreading采用管道方案：
![20261028101853](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20261028101853.png)
- 管道可以采用环形buffer来实现，主线程将渲染指令系列化为二进制数据，不断的往buffer里写入，当buffer被填满时，主线程被阻塞；渲染线程不断的从buffer中读取数据，反系列化为渲染指令，当buffer中无数据时，渲染线程被阻塞.
- 采用环形buffer的优点是读写buffer只需要移动读写点的位置，在只有一个生产者和消费者前提下，环形buffer的读写可以做到无锁（读写点pos(read，write各一个)用int32表示，如果32位的int读写是原子操作，那么只需要采用内存屏障来保证在读写点变更时，相应的内存是可用的即可）（此外还有用于通知对方线程要进行等到的 shouldSignal 是原子变量）
- 采用管道的优点是，不用考虑资源分配与释放的线程安全，因为同一份资源在主线程和渲染线程分别有自己不同的实例，两个线程分别维护自己资源的生命周期。 (Unity中，大部分数据通过序列化直接写入环形buffer，本质为主线程，渲染线程各自维护。 除了类似Texture这种，是通过传递裸指针+查询全局指针表的形式？)
- 采用管道的缺点是面向数据，它要求所有渲染指令必须能被系列化，因此不能采用抽象度过高的实现方式。比如一个抽象度比较高的渲染指令是传入一个相机、场景和renderTarget,这种级别的抽象使用管道来实现就会比较麻烦。因为它要求相机、场景和renderTarget必须是可系列化的。
采用管道的另一个缺点是为了能读写大容量的资源，需要预先分配一块比较大的内存，而且相对于直接传递资源的指针来说，读取耗时会相对高一些
- Unity中四种发生同步的情况:
  - 传递大数据（> 半个Buffer）
  - 指针传递
  - Worker反写主线程管理的内存: RenderSurface。 
  - 显式栅栏： 如进行Present时有barrier阻止其他Present进行。完成渲染之后 和 显示调用生成barrier。
- 当开启 Graphics Jobs 时，存在多个 job 线程同时提交渲染指令。 当创建Job线程时，主线程在ring buffer 插入“分发了Job线程”的指令，并记录该Job的指针。 后续渲染线程执行到该指令时，就能找到该Job管理的队列，进行执行。 （这里如果该Job的写入还未完成，就会造成渲染线程堵塞。）
  - 开启 Native Graphics Jobs时，Graphics Job可以直接向驱动的Deferred context Commandlist填充API调用。 Jobs直接通过主线程控制边界来保证串行有序
  ![20261028164011](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20261028164011.png) 

- 如何避免GPU有一帧耗时变长造成的帧率波动？:
  ![20261028105736](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20261028105736.png)
  主线程改为提前两帧的策略，只有在主线程第3帧Render时，如果发现渲染线程第1帧的渲染指令未处理完成时才会去等待。
  ![20261028105934](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20261028105934.png)

# 原子操作
原子操作是指在执行过程中不会被中断的操作。可以看作是不可分割的单元， 运行期间不会有任何的上下文切换。
- 单核处理器： 原子操作可以通过禁止中断的方式来保证不被中断。
- 多核处理器: 使用硬件原子指令/使用锁和同步原语. 如CAS（Compare-And-Swap）这种硬件原子指令，可以会比较内存中的值与期望值，如果相等则执行写入操作，否则不执行。