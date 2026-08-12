- https://zhuanlan.zhihu.com/p/689343038
- https://docs.unity3d.com/Manual/low-level-native-plugin-rendering-extensions.html
- https://github.com/Unity-Technologies/NativeRenderingPlugin
- https://zhuanlan.zhihu.com/p/83805113
- https://zhuanlan.zhihu.com/p/664582004
- https://zhuanlan.zhihu.com/p/699133982
- https://zhuanlan.zhihu.com/p/25578376917
- https://zhuanlan.zhihu.com/p/641520190


-------------------------------------------
-----------------------------------------
VRS 指令无法添加到drawcall 的commandlist中

✦ 当前问题总结

  目标
  在 Unity URP (D3D12) 中，让 pipeline shading rate（VRS） 作用到 context.DrawRenderers 绘制的不透明物体上。

  核心问题
  `RSSetShadingRate` 和 draw call 不在同一个 command list，导致 VRS 不生效。

  根因（已通过 RenderDoc 截帧 + 实测确认）

  1. Unity `ScriptableRenderContext` 有两条独立的录制路径：


  ┌──────┬────────────────────────────┬───────────────────────────────────┬───────────────────────────────────┐
  │ 路径 │            API             │             录制到哪              │             提交方式              │
  ├──────┼────────────────────────────┼───────────────────────────────────┼───────────────────────────────────┤
  │ A    │ context.DrawRenderers(...) │ Unity 内部命令列表（SRP batcher） │ context.Submit()                  │
  ├──────┼────────────────────────────┼───────────────────────────────────┼───────────────────────────────────┤
  │ B    │ cmd.IssuePluginEvent(...)  │ cmd CommandBuffer                 │ context.ExecuteCommandBuffer(cmd) │
  └──────┴────────────────────────────┴───────────────────────────────────┴───────────────────────────────────┘

  2. D3D12 pipeline shading rate 是 per-CommandList 状态，只作用于设置它的那个 command list，不跨 list 继承。

  3. RenderDoc 截帧证实：
   - 不透明 draw call 在 command list 1681
   - RSSetShadingRate 在 command list 1696（独立空 list）
   - 两者不同 list → VRS 不生效

  已尝试的方案（都失败）

  方案 1：Phase 3 计划 —— `cmd.IssuePluginEvent` 注入
   - 在 DrawObjectsPass.Execute 里 cmd.IssuePluginEvent 紧挨 context.DrawRenderers
   - ❌ 失败：IssuePluginEvent 进 cmd 的 list，DrawRenderers 进内部 list，不同 list

  方案 2：直接 `CommandRecordingState` + `RSSetShadingRate`（绕过 `IssuePluginEvent`）
   - 加了 SetPipelineShadingRateDirect 原生导出，主线程直接调用
   - 诊断显示 ok=1（调用成功），但画面无 VRS 效果
   - ❌ 失败：CommandRecordingState 在主线程返回的 command list 不是 draw 的 command list（DrawRenderers 延迟到 Submit()
      才录制）


  核心结论
  用公开 API，`RSSetShadingRate` 无法作用到 `context.DrawRenderers` 的 draw call 上。 这是 Unity ScriptableRenderContext
   架构的硬约束。

  唯一可行路径
  不用 `context.DrawRenderers`，改用 `cmd.DrawRenderer` 把物体录制进同一个 `cmd`，让 draw call 和 RSSetShadingRate 共享
  command list。

  代价/障碍：
   - CullingResults 无公开 API 遍历可见 renderer（需反射/内部 API）
   - 失去 SRP Batcher（合批优化），draw call 数暴涨
   - 需自己处理排序、材质、阴影、深度

  待决策
   1. 是否实施 cmd.DrawRenderer 方案（接受性能损失）
   2. 还是改用 image-based VRS / 全屏后处理 pass 演示
   3. 是否先清理诊断代码恢复原状