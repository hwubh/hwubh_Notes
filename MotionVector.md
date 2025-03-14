MotionVectorRenderPass
- use depth texture : // Motion vectors depend on the (copy) depth texture. Depth is reprojected to calculate motion vectors.
- m_CameraMotionVecMaterial: CoreUtils.CreateEngineMaterial(data.shaders.cameraMotionVector);
- RT格式： 
  - Color: RG16_sFloat: _MotionVectorTexture
  - Depth: _MotionVectorDepthTexture : 复用的场景的？ -》 shader中没看到画depth的地方，可能复用？
- MotionVectorsPersistentData： 跨帧数据：包含
  - m_ViewProjection： 当前帧视锥投影矩阵 ： _NonJitteredViewProjMatrix
  - m_PreviousViewProjection： 上一帧视锥投影矩阵 ： _PrevViewProjMatrix
  - m_LastFrameIndex： 上一帧ID？
  - m_PrevAspectRatio： 上一帧相机视锥比例？
- 开启TAA或object motion blur时会自动调用MotionVectorRenderPass

- Setup: 传Color，depth RTHandle
- Configure： 传color，depth，设置target
- Execute： 设置passData参数
  - m_FilteringSettings: opaque(LayerMask)
- ExecutePass: 
  - DrawCameraMotionVectors: depends on camera depth to reconstruct static geometry positions
    - draw fullscreen using cameraMaterial
  - DrawObjectMotionVectors: context.DrawRenderers -> based on "LightMode = MotionVectors"

Motion Blur
- MotionBlurMode: CameraOnly, CameraAndObjects
- MotionBlurQuality: High, medium, low
- intensity: multiplier for velocities : _Intensity
- clamp: maximum length for the velocity. as a fraction of the screen's full resolution? ： _Clamp

PostProcess::DoMotionBlur
- m_Materials.cameraMotionBlur : CameraMotionBlur.shader
- UpdateMotionBlurMatrices: 
  - viewProjectionStereo[0] 当前帧视锥矩阵 ： _ViewProjM
  - previousViewProjectionStereo[0]:  上一帧视锥矩阵 ： _PrevViewProjM
- if mode == MotionBlurMode.CameraAndObjects -》 传入_MotionVectorTexture
- BlitCameraTexture： 根据mode，和是否object motion blur 选定对应的pass

CameraMotionVector.shader: 
- 计算屏幕空间位置，采样_CameraDepthTexture得到深度
- 重建世界空间位置（根据视锥投影矩阵）
- 计算当前帧及上一帧的NDC空间坐标
- 根据二者的插值（posNDC - prevPosNDC）计算位移（速度），并换算到屏幕空间下（velocity.xy *= 0.5）。
- 输出该位移（速度）至 _MotionVectorTexture

ObjectMotionVectors.hlsl
- unity_MotionVectorsParams: 
  - X : Use last frame positions (skinned meshes)
  - Y : Force No Motion
  - Z : Z bias value
  - W : Camera only
- Unity默认传入的projectionMatrix是由jitter（抖动）过![GetProjectionMatrix](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/（）.png)。 
  其中m_JitterMatrix当开启TAA时会对投影矩阵进行偏移
  NoJitter的投影矩阵在MotionVector中单独从GL侧获取并传入。
- 分别计算当前帧和上一帧的是否jitter过的投影空间中位置positionCS， positionCSNoJitter。
  - 其中上一帧的模型空间位置需要考虑 蒙皮骨骼带来的位移偏移 和 mesh中预先计算好的MotionVector属性（alembic文件中模型可能存在）
- 如果unity_MotionVectorsParams.y == 0； 返回velocity为0
- 计算当前帧及上一帧的NDC空间坐标
- 根据二者的插值（posNDC - prevPosNDC）计算位移（速度），并换算到屏幕空间下（velocity.xy *= 0.5）。
- 输出该位移（速度）至 _MotionVectorTexture

CameraMotionBlur.shader:
- 根据屏幕空间坐标计算 projPos（从positionCS [-w, w] 映射到 projPos[0， w]）
- half4 DoMotionBlur(VaryingsCMB input, int iterations, int useMotionVectors): 低中高质量分别迭代2~4次，useMotionVectors == 1 时直接采样 _MotionVectorTexture。 否则直接重新计算camera motion vector。
- 沿velocity的反向，沿途多次采样混合。