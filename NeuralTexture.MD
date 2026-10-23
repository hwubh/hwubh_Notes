Neural Texture Compression 
- 类比: 
  - 采样: 输入uv, 输出采样数据；
  - 推理？: 输入UV， 输出参数(颜色、法线、粗糙度等)。
- 步骤:
  - 定义两个的网格 G0, G1. 分辨率是G0精细，G1粗糙, 存入特征值。 每个网格储存 $W\timesH\timesC$ 个元素。
    > 后续就是在训练MLP模型了？ 训练是通过误差反向传播？ 同时调整MIP权重，偏置和特征值来完善。
    > 训练完成以后，系统保存：各级网格中的特征值； 神经网络的权重和偏置。
  - 计算UV在G0, G1上的坐标，找到离该坐标最近的四个“像素”（网格）的特征值
  - 输入以下内容: G0 四个“像素”的特征值；G1 双线性插值四个“像素”的特征值的结果； 位置信息；LOD
    > 采样特征值$D_{\text{in}}=4C_0(G_0 的四个特征值)+C_1(G_1 特征值插值结果)+12(局部位置编码)+1(LOD, Mip层级)$。
    > 12个位置信息: 因为G0 为256\*256， Mip0 为2048\*2048， 同一个 \(G_0\) 局部单元最多需要区分 \(8\times8\) 个目标 texel 位置， 需要8\*8的局部位置信息。 这里使用 $log_{2}(8) = 3 个频率层级$, 又因为频率周期函数，需要确定相位（\*2），纹理为二维，XY方向分别编码(\*2)。 总的来说需要 3\*2\*2 共 12个标量来表示。
    > 为什么需要分为三个频率周期？ 因为提供高频信息才能保证小型MLP能完成高频率的纹理解码
  - 使用训练得到的模型（神经网格） MLP，以第三步的输入，经过两层隐藏层，到输出层，并输出结果。
    > 采样得到的特征值，通过训练好的MLP模型，结合权重和偏置，得到输出结果？
    > 隐藏层: 将输入值计算得到中间值，每层64节点（64个中间结果，用长度为64的向量表达）。
    > 第一层隐藏层负责将输入整合为一个统一的64维内部表达: $h_1=\operatorname{GELU}(W_1x+b_1)$
    > 第二层隐藏层负责将第一层输出64维内部表达进行非线性组合和解码，形成更接近目标纹理通道的64维内部表示
    > 输出层将第二层隐藏层的内部表达转换为实际的纹理通道内容。

MetalML:


训练Texture模型:

FNTC:
| 方面 | FNTC | NVIDIA RTXNTC |
|---|---|---|
| 定位 | UE 项目导向的混合超分/压缩实验 | 完整通用 NTC SDK |
| 低频来源 | 已存在的传统低分辨率 mip | 低分辨率 neural latent |
| 高频来源 | 一个高分辨率 feature texture + MLP residual | 高、低分辨率两组 latent + MLP |
| 默认网络 | 约 `23→16→10` | `48→64→48→32→16` |
| Latent 精度 | 默认 8-bit RGBA | 当前为 BGRA4，4-bit/feature |
| MLP 权重 | 导出代码原样保存 tcnn 参数 | 提供 Generic INT8 和 CoopVec FP8 权重 |
| 通道布局 | 固定 diffuse/normal/roughness/AO/metallic/specular/displacement | manifest 驱动，最多 16 通道 |
| Mip | 默认只训练 Mip0 | 完整 mip 链，2–4 个颜色 mip 共享一对 latent |
| 码率控制 | 通过 grid 分辨率、位宽、ASTC 设置手工控制 | 直接以 BPP/目标 PSNR 控制，支持 adaptive compression |
| 过滤 | 借助低分辨率基底和普通纹理采样，但完整各向异性行为不明确 | 单次解码是未过滤 texel，官方建议配合 STF |
| 部署 | UE 5.5.1 定制插件 | DX12/Vulkan SDK；on-sample、on-load、on-feedback、BCn 转码 |
| 训练 | PyTorch + tiny-cuda-nn | 定制 CUDA kernel + 定制 Adam/量化流程 |

HighRes(u, v) =
    BilinearSample(LowRes, u, v)
    + MLP(FeatureGrid(u, v), PositionEncoding(u, v), LowResSample)
  
FNTC的大致思路: 
Mip 0 外的正常采样。 Mip0 用 Mip1 采样结果(低频) 和 模型推理结果（高频误差） 进行模拟。