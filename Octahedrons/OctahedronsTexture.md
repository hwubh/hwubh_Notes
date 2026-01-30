球面映射方式： Cube mapping， spherical mapping， octahedrons mapping
- Cube mapping: 需要存储六张正方形贴图。
- Spherical mapping: 通过经纬来映射，越靠近两级方向，精度浪费情况越严重。
- Octahedrons mapping: 将一张正方形图片映射到八面体，再映射到球面上。 ->比起球面来说每个像素对应的立体角更加均匀，不会出现太大的扭曲。 -> 比起cube只需要一张图即可。

Octahedrons mapping：
- 映射方式：将一个正方形的四角“对折”，形成两个一样大的正方形。折下去的四个角当作下半球面，剩下的中心部分作为上半球面。然后将这两个正方形映射到八面体的上下两个部分上，然后映射到球面即可。 \
  ![v2-c677e5d7f6bc882a40d8b6e0f0cafb80_r](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/v2-c677e5d7f6bc882a40d8b6e0f0cafb80_r.jpg)
- 公式: $|x| + |y| + |z| = 1$, x,y,z的取值在(-1,1)之间。 当z<0时，需要反转x,y的符号。（Z<0代表下方的四个平面？）
  - 每个像素对应的立体角大小仍不相同: 蓝色的部分为欠采样， 绿色->黄红色的部分为过采样。 \ ![v2-bb49da45b21c03f2b7d9db9f661899a9_r](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/v2-bb49da45b21c03f2b7d9db9f661899a9_r.jpg)
- code： ![v2-df2904bf22b09b898c0d6dc688578de2_r](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/v2-df2904bf22b09b898c0d6dc688578de2_r.jpg)

Concentric Octahedral mapping: Concentric mapping + Octahedral mapping 
- def: 把正方形(上下各四个平面各自形成一个正方形)用 Concentric mapping 映射到同心圆上然后再映射到半球面。 \ ![v2-eb4288937ec3c66d25cf3a2f6dad3602_r](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/v2-eb4288937ec3c66d25cf3a2f6dad3602_r.jpg)
- 公式: ![v2-93833697474604f400792f6f8f136a9c_r](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/v2-93833697474604f400792f6f8f136a9c_r.jpg) https://link.zhihu.com/?target=https%3A//fileadmin.cs.lth.se/graphics/research/papers/2008/simdmapping/clarberg_simdmapping08_preprint.pdf
- 代码: ![v2-42b5b3addd7df9763b7ea089bc47b5d3_r](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/v2-42b5b3addd7df9763b7ea089bc47b5d3_r.jpg)



---------------

- cubeToOctahedral?
- bakeOctahedral
- sampleOctahedral


------------
- reflection probe 集合引擎，baked/custom
- 重要性采样-》 realtime / sky texture
- RGBM

---------
- Skybox的渲染还没支持: ProbeRenderer::Render
- reflectionEditor -> //RRR
- SetReflectionProbeUseOctahedralmap CleanupRenderPipeline 需要吗?
- OnPreSceneGUICallback 要加个mat
- GetTextureImporterSettingsForOctahedralmap 可能要需要个temp的
- const float BurleyRoughness = ((float)m) / (float)(context.nr_mips_convolved - 1); -> mipmap每一层级代表的粗糙度受mipmap的层数影响， 好像不太合理？？
- 不完全根据下一级mipmap进行生成？
- 改了Transfer，需要加宏来控制版本？

-----------
![20251010144529](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20251010144529.png)
这里有两个地方可能产生缝的问题，一个是从Mipmap0往下生成高级别的mipmap时，这个可以通过修改自行生成mipmap的方式解决。 另一个球体在采样octahedralmap的时候。 二者本质上都是因为在贴图边缘采样时，如果开启了硬件插值，如果UV不在0~1之间，就会导致采样的结果不合预期。 当UV不在0~1时，应该是根据X=0, X=1, Y=0, Y=1这四条进行映射才对，简单的Repeat/Mirror不能满足需求。 
![20251010145203](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20251010145203.png)（映射的方式。）
前者生成mipmap可以通过手动烘培mipmap时，主动映射UV来处理。 （也可以通过padding来处理，但存在较多的精度浪费，因为需要生成的mipmap层级越高，mipmap0需要的padding size越大）
后者采样octahedralmap时，因为是硬件插值，只能通过添加padding来处理。（应该每层都加个2的padding就行?）
padding: https://www.shadertoy.com/view/43G3Dd . 一般来说512*512的贴图需要32个像素的padding.

- octahderalMap dir to UV， 需注意得到的UV是不是-1~1的，如果是的话，还得要对UV缩放到0~1间。

式子： 
```
ext : 纹理的尺寸
border ： padding的尺寸
px ： 是否开启纹理对齐

vec2 border_expand(vec2 uv, float ext, float border, float px) {
    float I = ext - 2. * border;
    uv = (uv - border/ext) * ext/I;
    if (px > .5) uv = (floor(uv * I) + .5) / (I);
    return uv;
}

vec2 oct_border(vec2 uv, float ext, float border, float px) {
    // scale uv to account for borders
    uv = border_expand(uv, ext, border, px);
    // flip borders
    vec2 st = uv;
    st = (uv.x < 0. || uv.x > 1.) ? vec2(1. - fract(st.x), 1. - st.y) : st;
    st = (uv.y < 0. || uv.y > 1.) ? vec2(1. - st.x, 1. - fract(st.y)) : st;
    return st;
}
```

        ImgPtrsExt[mip] = (float*)UNITY_MALLOC_NULL(kMemDefault, channels * (mipWidth) * (mipHeight) * sizeof(float));

        if (mipWidth >= 32)
        {
            for (int y = 0; y < mipWidth; y++)
            {
                for (int x = 0; x < mipHeight; x++)
                {
                    float v = y + 0.5f;
                    float u = x + 0.5f;

                    float realSize = mipWidth - 4.0f;
                    u = (u - 2.0f) / realSize;
                    v = (v - 2.0f) / realSize;

                    if (u < 0.0f || u > 1.0f)
                    {
                        v = 1.0f - v;

                        if (u < 0.0f)
                            u = -u;
                        if (u > 1.0f)
                            u = 2.0f - u;
                    }

                    if (v < 0.0f || v > 1.0f)
                    {
                        u = 1.0f - u;

                        if (v < 0.0f)
                            v = -v;
                        if (v > 1.0f)
                            v = 2.0f - v;
                    }

                    int u0 = std::min(mipWidth - 1.0f, std::max(0.0f, u * mipWidth));
                    int v0 = std::min(mipHeight - 1.0f, std::max(0.0f, v * mipHeight));

                    const int idx_src = v0 * mipWidth + u0;
                    const int idx_dst = y * mipWidth + x;

                    for (int c = 0; c < channels; c++)
                    {
                        if (mip == 0)
                            (ImgPtrsExt[mip])[channels * idx_dst + c] = (ImgPtrsSrc[0])[channels * idx_src + c];
                        else
                            (ImgPtrsExt[mip])[channels * idx_dst + c] = (ImgPtrsDst[0][mip])[channels * idx_src + c];
                    }
                }
            }
        }


---------------

static void DoSinglePixConvolveSIMDOctahedralMap(ConvBrdfContext& context, float* ResPtr, const unsigned short PermTableAptr[], const unsigned short PermTableBptr[], const int NrRaysIn,
const int offs, const float fMipOffs, const float mipLim, const float fN, const float RealRoughness,
const float vX[3], const float vY[3], const float dir[3], const float PaddingSize)
{
using namespace math;
float4 vResult[] = { 0.0f, 0.0f, 0.0f, 0.0f };
const float RealRoughnessSquared = RealRoughness * RealRoughness;
const float4 scale = 1.0f / ((float)(CONV_RAND_MAX + 1));
const int NrRays = NrRaysIn & (~0x3);     // force to multiple of 4 (though this should already be the case)
const int NrIts = NrRays / 4;

// 预计算八面体映射校正因子
const float octahedralNormalization = 4.0f / (float)M_PI; // 八面体映射的归一化常数

for (int Q = 0; Q < NrIts; Q++)
{
int i[4], J[4];
for (int q = 0; q < 4; q++)
{
// Use N-rooks to distribute N samples across a 2D space
int I0 = 4 * Q + q;
i[q] = PermTableBptr[I0];       // think of PermTableBptr[] as an inverse permutation table going from I --> i
DebugAssertMsg(IsPowerOfTwo(NrRays) && NrRays > 0, "Convolve: ray count should be power of two");
J[q] = PermTableAptr[(offs + i[q]) & (NrRays - 1)];
}
int jitteri[4], jitterj[4];
for (int q = 0; q < 4; q++)
{
const int jit_idx = (i[q] + offs) & (LENGTH_RAND_TABLE - 1);
jitteri[q] = context.RandAptr[jit_idx];
jitterj[q] = context.RandBptr[jit_idx];
}
int4 tmpA = int4(jitteri[0], jitteri[1], jitteri[2], jitteri[3]);
int4 tmpB = int4(jitterj[0], jitterj[1], jitterj[2], jitterj[3]);
float4 vIfloat = convert_float4(int4(4 * Q) + int4(0, 1, 2, 3));
float4 vJfloat = convert_float4(int4(J[0], J[1], J[2], J[3]));
float4 vTheta = (2.0f * ((float)M_PI) / NrRays) * (vIfloat + (scale * convert_float4(tmpA)));
float4 vProb = (1.0f / NrRays) * (vJfloat + (scale * convert_float4(tmpB)));

// 使用更稳定的采样方法
const float K = RealRoughnessSquared;
const float4 si = sqrt(vProb / (K + (1.0f - K) * vProb));
const float4 co = sqrt(max(0.0f, 1.0f - si * si));
const float4 cotheta = cos(vTheta);
const float4 sitheta = sin(vTheta);
const float4 vx = cotheta * co;
const float4 vy = sitheta * co;
const float4 vz = si;

float4 vDir[3];
for (int r = 0; r < 3; r++)
    vDir[r] = vx * vX[r] + vy * vY[r] + vz * dir[r];

// 计算PDF并应用八面体映射校正
float4 tmp = (si * si) * (RealRoughnessSquared - 1.0f) + 1.0f;
float4 pdf = (si * RealRoughnessSquared) / (((float)M_PI) * tmp * tmp);

// 八面体映射特有的校正
float4 octahedralWeight = CalculateOctahedralWeight(vDir);
pdf *= octahedralWeight;

const float4 adx = abs(vDir[0]);
const float4 ady = abs(vDir[1]);
const float4 adz = abs(vDir[2]);

// 改进的LOD计算，考虑八面体映射特性
const float4 maxabscomp = max(max(adx, ady), adz);
// 添加八面体映射缩放因子
float4 octahedralScale = 1.0f + 0.2f * (maxabscomp - min(min(adx, ady), adz)); // 简化的拉伸校正
float4 Lod = fMipOffs - 0.5f * log2e(max(float4(FLT_EPSILON), pdf * maxabscomp * maxabscomp * maxabscomp * octahedralScale));
Lod = max(float4(mipLim), Lod);

float4 vSample[4];
OctahedralMapSampleSIMD(vSample, context.ImgPtrsExt[0], context.dimSrc, context.numChannels, context.numSrcMips, vDir, Lod, PaddingSize);

// 应用额外的权重校正
float4 finalWeight = octahedralWeight / (float)NrRays;
for (int c = 0; c < context.numChannels; c++)
    vResult[c] += vSample[c] * finalWeight;
}

// 归一化结果
for (int c = 0; c < context.numChannels; c++)
    ResPtr[c] = dot(1.0f, vResult[c]);
}

// 添加辅助函数用于计算八面体映射权重
float4 CalculateOctahedralWeight(float4 vDir[3])
{
    // 计算八面体映射的雅可比行列式校正
    const float4 absX = abs(vDir[0]);
    const float4 absY = abs(vDir[1]);
    const float4 absZ = abs(vDir[2]);
    
    // 八面体映射的拉伸校正因子
    const float4 sum = absX + absY + absZ;
    const float4 weight = sum * sum * sum; // 立方体到八面体映射的雅可比校正
    
    return weight;
}


-----------
Shader "Hidden/OctahedralConversionWithPadding"
{
    Properties
    {
        _MainTex ("Cubemap", Cube) = "" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        
        Pass
        {
            Name "OctahedralConversionWithPadding"
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            TEXTURECUBE(_MainTex);
            SAMPLER(sampler_MainTex);
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            uniform float _MipLevel;
            uniform float _TextureSize;
            uniform uint _PaddingSize;
            SamplerState sampler_LinearRepeat;

            Varyings vert(Attributes input)
            {
                Varyings output;

                output.positionHCS = TransformObjectToHClip(input.positionOS);

                // float scalePadding = (_TextureSize + _PaddingSize) / _TextureSize;
                // float offsetPadding = (_PaddingSize / 2.0) / (_TextureSize + _PaddingSize);
                // float2 octUV = (input.uv - offsetPadding) * scalePadding;

                // if (octUV.x < 0.0f || octUV.x > 1.0f)
                // {
                //     octUV.y = 1.0f - octUV.y;

                //     if (octUV.x < 0.0f)
                //         octUV.x = -octUV.x;
                //     if (octUV.x > 1.0f)
                //         octUV.x = 2.0f - octUV.x;
                // }

                // if (octUV.y < 0.0f || octUV.y > 1.0f)
                // {
                //     octUV.x = 1.0f - octUV.x;

                //     if (octUV.y < 0.0f)
                //         octUV.y = -octUV.y;
                //     if (octUV.y > 1.0f)
                //         octUV.y = 2.0f - octUV.y;
                // }

                output.uv = input.uv;

                return output;
            }
            
            float4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                //float2 uv = RepeatOctahedralUV(input.uv.x, input.uv.y);
                // if (input.uv.x < 0.0f || input.uv.x > 1.0f)
                //     return half4(1,0,0,1);
                float scalePadding = (_TextureSize + _PaddingSize) / _TextureSize;
                float offsetPadding = (_PaddingSize / 2.0) / (_TextureSize + _PaddingSize);
                float2 octUV = (input.uv - offsetPadding) * scalePadding;
                if (octUV.x < 0.0f || octUV.x > 1.0f)
                {
                    octUV.y = 1.0f - octUV.y;

                    if (octUV.x < 0.0f)
                        octUV.x = -octUV.x;
                    if (octUV.x > 1.0f)
                        octUV.x = 2.0f - octUV.x;
                }

                if (octUV.y < 0.0f || octUV.y > 1.0f)
                {
                    octUV.x = 1.0f - octUV.x;

                    if (octUV.y < 0.0f)
                        octUV.y = -octUV.y;
                    if (octUV.y > 1.0f)
                        octUV.y = 2.0f - octUV.y;
                }
                float3 dir = UnpackNormalOctQuadEncode(2.0f * octUV - 1.0f);
                return SAMPLE_TEXTURECUBE_LOD(_MainTex, sampler_MainTex, dir, _MipLevel);
            }
            ENDHLSL
        }
    }
    
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
在顶点着色器中做UV变换好像不太行，得放在片元才是正确的。

--------
float srcU = U * (curDim - 1);
float srcV = V * (curDim - 1);

int x0 = (int)floorf(srcU);
int y0 = (int)floorf(srcV);
int x1 = x0 + 1;
int y1 = y0 + 1;

x0 = std::max(0, std::min(x0, curDim - 1));
x1 = std::max(0, std::min(x1, curDim - 1));
y0 = std::max(0, std::min(y0, curDim - 1));
y1 = std::max(0, std::min(y1, curDim - 1));

float fx = srcU - x0;
float fy = srcV - y0;

for (int c = 0; c < nr_channels; c++)
{
    float p00 = (ImgPtrs_mid[faceIndex][m])[nr_channels * GenImgIdx(x0, y0, curDim) + c];
    float p01 = (ImgPtrs_mid[faceIndex][m])[nr_channels * GenImgIdx(x0, y1, curDim) + c];
    float p10 = (ImgPtrs_mid[faceIndex][m])[nr_channels * GenImgIdx(x1, y0, curDim) + c];
    float p11 = (ImgPtrs_mid[faceIndex][m])[nr_channels * GenImgIdx(x1, y1, curDim) + c];

    float interpolated =
        p00 * (1.0f - fx) * (1.0f - fy) +
        p10 * fx * (1.0f - fy) +
        p01 * (1.0f - fx) * fy +
        p11 * fx * fy;

    (ImgPtrs_dst[m])[nr_channels * idx_dst + c] = interpolated;
}

--------------

cube to octa 时图像上下颠倒的问题： 
- 使用OpenGLES: 在生成每一级octahedralmap的 mipmap时，会触发 Yflip, imageFilter::Blit()中src R的YFIP计算为false， dst Tex的UVYTop2Botton = false， ReadbackImage / CopyTexture 不参与翻转Y坐标， shader中不满足 UNITY_UV_STARTS_AT_TOP， 不翻转Y坐标, RT::SetActive中SetInvertProjectionMatrix = false, RT::SetActive中dst Tex的SetUVYIsFromTopToBottom = true, UpdateYFlipState 中 dst Tex的SetUVYIsFromTopToBottom = false;
- 在生成cubemap的每个面时，会触发Yflip。 
> imageFilter::Blit 发生Y反转的条件： 源纹理本身倒置且满足基本条件（VR未启用，不是RT，不是Y坐标从上到下的坐标系（DX11,12;Vulkan；Metal；PS4/5））； 或渲染到Game View且源纹理UV的Y方向为从底部到顶部； 或源纹理UV的Y方向为顶部到底部

- 使用DX11时， 在生成每一级octahedralmap的 mipmap时，不会触发 Yflip， imageFilter::Blit()中未发生Y反转，dst tex的 UVYTop2Botton = false， shader中满足 UNITY_UV_STARTS_AT_TOP，会翻转Y坐标。

这里无论是DX 还是 OpenGLES，ImageFilters::Blit都会发生翻转？

-------
可以使用compute shader 来处理Cube2Octahedron。但是SSBO数量>7时可以把各个mipmap放在一个Drawcall里？

-----
球体上的均匀分布： 斐波那契螺旋分布

-----

urp 改默认keyword:
-  UniversalRenderPipelineAsset：：#if UNITY_EDITOR // multi_compile_fragment _ _REFLECTION_PROBE_USE_OCTAHEDRALMAP
        [ShaderKeywordFilter.SelectOrRemove(
- ShaderScriptableStripper：：if (stripTool.StripMultiCompile(
- ShaderBuildPreprocessor：： enum ShaderFeatures : long

-------
如何从外向内画skytexture： （因为是从外向内画， 所有只改transform没用）
计算六个面对应的viewmatrix： 
``` c#
        private static readonly Vector3[] faceForwards = {
            new Vector3(0, 0, -1),  // Positive X (右侧)
            new Vector3(0, 0, 1),   // Negative X (左侧)
            new Vector3(1, 0, 0),   // Positive Y (上方)
            new Vector3(1, 0, 0),   // Negative Y (下方)
            new Vector3(1, 0, 0),   // Positive Z (前方)
            new Vector3(-1, 0, 0)   // Negative Z (后方)
        };

        private static readonly Vector3[] faceUps = {
            new Vector3(0, -1, 0),  // Positive X
            new Vector3(0, -1, 0),  // Negative X
            new Vector3(0, 0, 1),   // Positive Y
            new Vector3(0, 0, -1),  // Negative Y
            new Vector3(0, -1, 0),  // Positive Z
            new Vector3(0, -1, 0)   // Negative Z
        };

        private static readonly Vector3[] faceRights = {
            new Vector3(-1, 0, 0),  // Positive X
            new Vector3(1, 0, 0),   // Negative X
            new Vector3(0, -1, 0),  // Positive Y
            new Vector3(0, 1, 0),   // Negative Y
            new Vector3(0, 0, -1),  // Positive Z
            new Vector3(0, 0, 1)    // Negative Z
        };

        public static Matrix4x4 SetBasisTransposed(this Matrix4x4 matrix, Vector3 forward, Vector3 up, Vector3 right)
        {
            // 创建一个矩阵，列向量分别为forward, up, right
            matrix[0, 0] = forward.x; matrix[0, 1] = up.x; matrix[0, 2] = right.x; matrix[0, 3] = 0;
            matrix[1, 0] = forward.y; matrix[1, 1] = up.y; matrix[1, 2] = right.y; matrix[1, 3] = 0;
            matrix[2, 0] = forward.z; matrix[2, 1] = up.z; matrix[2, 2] = right.z; matrix[2, 3] = 0;
            matrix[3, 0] = 0; matrix[3, 1] = 0; matrix[3, 2] = 0; matrix[3, 3] = 1;

            // 返回转置后的矩阵
            return matrix.transpose;
        }

        private static Matrix4x4 CalculateViewMatrixWithSetBasisTransposed(Transform cameraTransform, int face)
        {
            Vector3 position = cameraTransform.position;

            Vector3 forward = faceForwards[face];
            Vector3 up = faceUps[face];
            Vector3 right = faceRights[face];

            Vector3 worldForward = cameraTransform.TransformDirection(forward);
            Vector3 worldUp = cameraTransform.TransformDirection(up);
            Vector3 worldRight = cameraTransform.TransformDirection(right);

            Matrix4x4 viewMatrix = new Matrix4x4();
            viewMatrix = viewMatrix.SetBasisTransposed(worldForward, worldUp, worldRight);

            Matrix4x4 translateMatrix = Matrix4x4.Translate(-position);

            return viewMatrix * translateMatrix;
        }
```
然后将计算得到的view matrix 设置到相机的worldToCameraMatrix 上。
此外，因为是外向内画，会导致相机中片元的winding order与正常情况相反，通过设置cmd.SetInvertCulling(true);来反转winding order。


----------------
环境镜面反射的实现: 将镜面反射函数的求解，通过蒙特卡洛积分转化为有限个样本的求和。然后将该求和分割为两个和式的乘积（split sum approximate），分布求和式（Pre-Filtered Environment Map） 和 和式（Environment BRDF）。
- Prefiltered Environment Map （LD项，Radiance (L) × Distribution (D)） -》 取决于反射方向 (ωr​) 和 粗糙度 (α) -> 储存在反射探针中，每级mip对应一个粗糙度。反射方向对应UV。 
> D项只是为了确定重要性采样的pdf而引入的？？？
- Environment BRDF（DFG项，分布 (D)、菲涅尔 (F) 和 几何 (G)）： 存储在LUT图中，记录F的scale和G的bias。通过cosθv​ (N⋅V) 和 粗糙度 (α)进行查找。
---------------------------

Unity中如何写Vertex Shader：
以CoreUtils.DrawFullScreen为例： 使用一个三角形来进行绘制。![20260130155833](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20260130155833.png)
首先通过 
struct Attributes
{
    uint vertexID : SV_VertexID;
};
获取到三角形的顶点0， 1， 2.
然后通过式子
Varyings Vert(Attributes input)
{
    Varyings output;
    output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
    return output;
}

float4 GetFullScreenTriangleVertexPosition(uint vertexID, float z = UNITY_NEAR_CLIP_VALUE)
{
    // note: the triangle vertex position coordinates are x2 so the returned UV coordinates are in range -1, 1 on the screen.
    float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
    float4 pos = float4(uv * 2.0 - 1.0, z, 1.0);
#ifdef UNITY_PRETRANSFORM_TO_DISPLAY_ORIENTATION
    pos = ApplyPretransformRotation(pos);
#endif
    return pos;
}
得到三角形顶点在屏幕空间的坐标pos的xy项)（-1， -1）， （-1， 3）， （3， -1）
也可通过
GetFullScreenTriangleTexCoord 获得0~2空间下的uv坐标。
然后在片元着色器中使用 CubemapTexelToDirection将uv坐标转为面上uv的世界空间坐标 （屏幕空间 -> 世界空间）