# Unity学习笔记--Editor扩展：类Unity编辑器界面的实现

## 提要：
本文主要在探讨在Unity中实现反射探针旋转的思路，以及反射探针上一些可以优化的点。 Unity版本：6000.3.1f1.

## 前言：
Unity在 URP17.3中对反射探针的旋转添加了支持，看了下大致思路与笔者之前写的差不多。 这里就简练讲下实现思路，涉及源码的地方就用伪代码大致讲下思路。 

## 思路：

- 在PlayerSettings/Graphics 面板内通过开关 *Use Reflection Probe Rotation* 全局控制反射探针旋转功能是否启用。 该接*reflectionProbeSettings.UseReflectionProbeRotation*口会影响以下几个地方：
  - 全局Keyword **
  - s_StripReflectionProbeRotationVariants，用于判断打包是否剔除反射探针旋转的变体。
  - ScheduleClusteringJobs中影响构建光蔟时的求交判断。

- 视锥剔除阶段：
  - 遍历所有反射探针，将锥体的各个面分别与探针做相交测试。 大致思路是先将单个面根据探针的Transform信息（不考虑Scale）变换到探针的Local 坐标系中，计算该探针是否完全位于面的负空间（法线反向的空间）。如果是的话，才认为没有相交。 