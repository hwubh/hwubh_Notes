Construct
- Default Constructor： 
- Parameterized Constructor： 用于实现多态性的初始化，常配合初始化列表 (Initialization List) 使用
- Copy Constructor： 默认为浅拷贝，如果类中含有指针成员，通常需要手动实现以进行深拷贝，防止多个对象指向同一块内存。
- Move Constructor: 不复制数据，而是将源对象的指针转移给新对象，并将源对象的指针置空. -> 只夺取引用
- Conversion Constructor： 
- Conversion Constructor： 使用隐式类型转换

智能指针：利用 RAII (资源获取即初始化) 机制来自动管理内存
- unique_ptr： 同一时间只有一个指针拥有该资源； 禁用拷贝构造，支持移动构造。
- shared_ptr： 允许多个指针指向同一个对象；支持拷贝构造，每拷贝一个 shared_ptr，内部的内部的引用计数就会加 1； 移动构造不会增加计数，而是直接转移控制权。 会多占据一块内存用于计数器。
- weak_ptr: 弱引用，指向shared_ptr所管理的对象, 但不会改变 shared_ptr的引用计数。不论是否有weak_ptr指向，一旦最后一个指向对象的shared_ptr被销毁，对象就会被释放。 用于缓存对象， 因为不受生命周期的影响？ 
  - 可用于避免**循环引用**： 两个对象 A 和 B 互相持有对方的 shared_ptr。当外部作用域结束时，A 等待 B 释放，B 等待 A 释放，导致引用计数永远无法归零

数据结构：
- vecotr: 动态数组。分配一块连续的内存空间。 O(1)查找； 尾部插入/删除通常是 O(1)，但在中间插入/删除是 O(n)
- deque： 分段连续数组。 由一段段定长的连续空间（缓冲区）组成，通过一个“控制映射表”（Map）来管理这些段。O(1)查找，但需要调整两次； 头尾插入/删除都是 O(1)（中间插入/删除是 O(n)？）
- list: 双向链表。每个节点包含数据及指向前后节点的指针。不支持随机访问，查找效率为 O(n)； 已知位置时，插入和删除是 O(1)。
- std::set / std::map / std::multimap: 红黑树 (Red-Black Tree)。查找、插入、删除的时间复杂度均为 O(logn)。
- std::unordered_map / std::unordered_set: 哈希表 (Hash Table)。采用开链法（Bucket 数组 + 单向链表/红黑树）解决冲突。平均查找复杂度为 O(1)，最坏情况（哈希冲突严重时）为 O(n)
- std::stack：默认基于 std::deque（后进先出）。
- std::queue：默认基于 std::deque（先进先出）。
- std::priority_queue：底层是堆 (Heap)，通常基于 std::vector 实现，通过算法维护大顶堆或小顶堆。
