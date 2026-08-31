# Unity学习笔记--NativePlugin中实现VRS

## 提要：
本文主要通过NativePlugin + RenderFeature的方式尝试在DX12中实现VRS功能。 Unity版本：2022.3.62f3.

## 前言:
最近看了些用NativePlugin来给Unity添加一些渲染特性的文章，加上前阵子给公司项目在Unity2022上做了VRS的功能支持。所以想着说能不能不修改引擎，就靠NativePlguin来实现VRS。 但实际在DX12做下来看的，如果不改源码的话，感觉只能做个技术展示，进不了实际生产中。 这里我就借着这次尝试，简单介绍下VRS和NativePlugin吧。 另外也权当抛砖引玉，看看下面提到的问题，大佬们有什么合适的法子。

## VRS (Variable shading rate, 可变速率着色)
在开始实际代码之前，先简单介绍下VRS。 简单来说，VRS允许开发者控制片元着色器的调用频率，减少不必要的着色计算。在我看来，VRS的特点是解耦了光栅化和着色计算这两个影响了渲染压力的主要因素。 以图为例
![20260817142300](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20260817142300.png)\
通过调整调整着色计算频率，可以在保留三角形的像素边缘的同时，减少着色计算的次数。
> 因此，个人觉得Metal的 RRM (Rasterization Rate Map)这种在光栅化阶段做处理，改变了实际像素分配的技术，可能不太能算VRS？
> Nvapi中的VRS还提供在一个像素中多次进行着色计算的选项。个人觉得可以算是SSAA的上位替代？ 在不提升分辨率的情况下，提升了渲染精度。
> 
常见的着色频率(shading rate)输入包含 **pipelne shading rate**, **attachment shading rate** 和 **primitive shading rate**，三者的控制粒度大致上是从粗到细的。
Pipeline shading rate (Per Drawcall)是最基础的全局设置。其的粒度为Drawcall的，一般通过dynamic state在不同Drawcall之间动态设置。
Attachment shading rate (Per attachment) 通过绑定一张R8_UINT格式的屏幕空间图像附件(Shading Rate Image, **SRI**)进行直到。其粒度为 $8\times8 \to 32\times32$ 的像素块，图像附件上一个像素对应屏幕上一个像素块范围内的着色频率。
> 一般来说SRI通过亮度差或速度来决定像素块的着色频率。前者对画面中颜色相近的区域降低着色频率；后者对移动幅度大的区域降低着色频率。 [1]
> Vulkan中需要注意SRI是在RenderPass Attachments中的，需要在创建RenderPass就确定（特别是subpasses使用不同的SRI时）。

Primitive shading rate (Per primitive) 通过在 vertex shader/ geometry shader/ mesh shader中的着色器语义/变量传入，以光栅化前最后一个输入值为准。 粒度为三角形(注意不是顶点).

这三种输入会通过 **CombinerOps** 进行组合，决定使用的着色频率。
![20260817153207](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20260817153207.png)
> Vulkan/OpenGLES 中没有 “SUM” 组合， 取而代之的是 “MUL =  min(maxRate, AxB)”

## NativePlugin + VRS
### NativePlugin 中实现 Pipeline Shading Rate
首先在项目根目录下创建文件夹 "NativePlguinVRS" 用于放置NativePlguin工程。
创建文件 "GfxPluginVRSPlugin.cpp", 用于初始化插件。 
```cpp
// "IUnityGraphicsD3D12.h" use functions from these 3 following files.
#include <windows.h>
#include <d3d12.h>
#include <dxgi1_6.h> 

#include "IUnityInterface.h"
#include "IUnityGraphics.h"
#include "IUnityGraphicsD3D12.h"
#include "RenderAPI_D3D12.h"            
             

// ---- 全局状态：跨回调共享 ——
static IUnityInterfaces*      s_UnityInterfaces = nullptr;  // 注册表：Get<...>() 取接口
static IUnityGraphics*        s_Graphics        = nullptr;  // 图形接口：注册设备回调/判后端
static IUnityGraphicsD3D12v7* s_D3D12           = nullptr;  // D3D12 接口：ConfigureEvent / 拿 command list
static RenderAPI_D3D12*       s_API             = nullptr;  // VRS 实现类实例

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API
UnityPluginLoad(IUnityInterfaces* unityInterfaces)
{
    s_UnityInterfaces = unityInterfaces;
    s_Graphics = unityInterfaces->Get<IUnityGraphics>();
    s_Graphics->RegisterDeviceEventCallback(OnGraphicsDeviceEvent);
}

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API
UnityPluginUnload()
{
    if (s_Graphics) { s_Graphics->UnregisterDeviceEventCallback(OnGraphicsDeviceEvent); s_Graphics = nullptr; }
}
```
初始化加载Dll时会调用 `UnityPluginLoad`， 获取接口注册表`s_UnityInterfaces` 和 图形接口 `s_Graphics`。 通过`s_Graphics`把 `OnGraphicsDeviceEvent` 注册为图形设备(D3D12 Device)事件的回调，其会在d3d12设备被创建/销毁/重置前后被调用。
卸载Dll会调用UnityPluginUnload，将注销之前注册的回调和接口。

图形设备的创建后，OnGraphicsDeviceEvent 通过Unity DX12的图形接口说明下NativePlugin中的函数事件是影响当前管线状态的。
kUnityD3D12GraphicsQueueAccess_DontCare 说明我们使用Unity 的Commandlist进行提交。这里主要是考虑到ID3D12GraphicsCommandList 设置的作用域限于当前的 Command List。
kUnityD3D12EventConfigFlag_SyncWorkerThreads 用于保证执行顺序。
kUnityD3D12EventConfigFlag_ModifiesCommandBuffersState 因为我们需要改变管线的状态。
kUnityD3D12EventConfigFlag_EnsurePreviousFrameSubmission 
ensureActiveRenderTextureIsBound 执行事件前自动绑定好当前Active的RT

```cpp
#define PipelineShadingRate_EVENT_ID         0 //设置 pipeline shaidng rate 的渲染事件ID

// ---- 设备事件回调（设备创建完毕/销毁前）----
static void OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType)
{
    if (eventType == kUnityGfxDeviceEventInitialize)
    {
        if (s_Graphics->GetRenderer() == kUnityGfxRendererD3D12)
        {
            s_D3D12 = s_UnityInterfaces->Get<IUnityGraphicsD3D12v7>(); //

            UnityD3D12PluginEventConfig vrsCfg = {};
            vrsCfg.graphicsQueueAccess = kUnityD3D12GraphicsQueueAccess_DontCare;
            vrsCfg.flags = kUnityD3D12EventConfigFlag_SyncWorkerThreads
                         | kUnityD3D12EventConfigFlag_ModifiesCommandBuffersState
                         | kUnityD3D12EventConfigFlag_EnsurePreviousFrameSubmission;
            vrsCfg.ensureActiveRenderTextureIsBound = true;
            s_D3D12->ConfigureEvent(PipelineShadingRate_EVENT_ID, &vrsCfg);

            s_API = new RenderAPI_D3D12();
            s_API->OnDeviceInit(s_UnityInterfaces, s_D3D12);
        }
    }
    else if (eventType == kUnityGfxDeviceEventShutdown)
    {
        if (s_API) { s_API->OnDeviceShutdown(); delete s_API; s_API = nullptr; }
    }
}
```

在相同目录下创建文件 "RenderAPI_D3D12.h/.cpp" 负责VRS功能的具体实现，通过指针 s_API 进行调用。
`OnDeviceInit` 在图形设备被创建后调用，通过 "[D3D12_FEATURE_DATA_D3D12_OPTIONS6](https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_feature_data_d3d12_options6)" 检查当前硬件设备是否支持Pipeline Shading Rate。
```cpp
// RenderAPI_D3D12.cpp
#include "RenderAPI_D3D12.h"
#include <d3d12.h>

void RenderAPI_D3D12::OnDeviceInit(IUnityInterfaces*, IUnityGraphicsD3D12v7* d3d12)
{
    m_D3D12 = d3d12;
    InitializeVRSCapabilities();
}

void RenderAPI_D3D12::InitializeVRSCapabilities()
{
    ID3D12Device* device = m_D3D12->GetDevice();
    D3D12_FEATURE_DATA_D3D12_OPTIONS6 options6 = {};
    HRESULT hr = device->CheckFeatureSupport(D3D12_FEATURE_D3D12_OPTIONS6, &options6, sizeof(options6));
    m_PipelineVRSSupported = SUCCEEDED(hr) ? (options6.VariableShadingRateTier != D3D12_VARIABLE_SHADING_RATE_TIER_NOT_SUPPORTED) ： false;
}

void RenderAPI_D3D12::OnDeviceShutdown()
{
    m_D3D12 = nullptr;
}
```

为了让C# 层能调用Native Plugin中的函数实现， 需要使用 **extern "C" UnityRenderingEventAndData UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API** 修饰要导出的函数。
这里导出函数 `IsPipelineVRSSupported` 用于检测当前设备是否支持 pipeline shading rate。 
导出函数 `GetRenderEventFunc` 提供回调函数 `OnRenderEventData` 的地址给 `CommandBuffer.IssuePluginEventAndData`，将Native Plugin 中的渲染相关的函数插入CommandList队列中。后续回调函数 OnRenderEvent 则根据传入的 `eventID` 调用对应的函数。

``` cpp
extern "C" int UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API
IsPipelineVRSSupported()
{
    return (s_API && s_API->IsPipelineVRSSupported()) ? 1 : 0;
}

extern "C" UnityRenderingEventAndData UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API
GetRenderEventFunc()
{
    return OnRenderEventData; 
}
```
> `UnityPluginLoad`, `UnityPluginUnload` 会在

DX12中 [RSSetShadingRate](https://learn.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist5-rssetshadingrate) 使用 D3D12_SHADING_RATE, D3D12_SHADING_RATE_COMBINER* 作为参数输入, 这里使用将参数编码为一个INT值。 
其中 "[D3D12_SHADING_RATE](https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ne-d3d12-d3d12_shading_rate)" 中最大取值为 “D3D12_SHADING_RATE_4X4 = 0xa”, 使用4个bit来表示。 “[D3D12_SHADING_RATE_COMBINER](https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ne-d3d12-d3d12_shading_rate_combiner)” 有DX12中有5种，各占用3个bit。 
> D3D12_SHADING_RATE 只有 $1\times1, 1\times2, 2\times1, 2\times2, 2\times4, 4\times2, 4\times4,$ 7种，理论最少只需要3个bit，但需要多做一次转换。
> 部分设备可能不支持 $2\times4, 4\times2, 4\times4,$，具体可以通过布尔变量 D3D12_FEATURE_DATA_D3D12_OPTIONS6.AdditionalShadingRatesSupported 进行判断。
```cpp
#define VRS_COMB0_SHIFT   4          
#define VRS_COMB1_SHIFT   7          
#define VRS_RATE_MASK     0xF        // rate 位宽（4bit） 0~3
#define VRS_COMB0_MASK    0x7        // combiner0 位宽（3bit） 3~5
#define VRS_COMB1_MASK    0x7        // combiner1 位宽（3bit） 6~8

static void UNITY_INTERFACE_API OnRenderEventData(int eventID, void* data)
{
    // data == nullptr 时，视为 shading rate = 1x1, combinerOps 为 Passthrough，Passthrough。
    if(s_API==nullptr) return;
    int p = (int)(intptr_t)data;
    D3D12_SHADING_RATE rate   = (D3D12_SHADING_RATE)(p & VRS_RATE_MASK);
    D3D12_SHADING_RATE_COMBINER c0 = (D3D12_SHADING_RATE_COMBINER)((p >> VRS_COMB0_SHIFT) & VRS_COMB0_MASK);
    D3D12_SHADING_RATE_COMBINER c1 = (D3D12_SHADING_RATE_COMBINER)((p >> VRS_COMB1_SHIFT) & VRS_COMB1_MASK);
    if(eventID == PipelineShadingRate_EVENT_ID) 
        s_API->SetPipelineShadingRate(rate,c0,c1);
}
```

SetPipelineShadingRate的具体实现。这里需要将命令压入Unity正在使用的Commandlist cmd5 中。
```cpp
bool RenderAPI_D3D12::SetPipelineShadingRate(
    D3D12_SHADING_RATE rate,
    D3D12_SHADING_RATE_COMBINER combiner0,
    D3D12_SHADING_RATE_COMBINER combiner1)
{
    if (!m_PipelineVRSSupported)
        return false;

    UnityGraphicsD3D12RecordingState rec;
    if (!m_D3D12->CommandRecordingState(&rec))
        return false;

    // ID3D12GraphicsCommandList5 为 RSSetShadingRate 要求的最低版本
    ID3D12GraphicsCommandList5* cmd5 = nullptr;
    if (FAILED(rec.commandList->QueryInterface(IID_PPV_ARGS(&cmd5))))
        return false;

    D3D12_SHADING_RATE_COMBINER comb[2] = { combiner0, combiner1 };
    cmd5->RSSetShadingRate(rate, comb);   // 设置 Pipeline Shading Rate 和 CombinerOps
    cmd5->Release();

    return true;
}
```

创建模块定义文件 "GfxPluginVRSPlugin.def" 显示控制导出的函数。
> 不写也行，因为 “UNITY_INTERFACE_EXPORT” 中已经定义了 "__declspec(dllexport)"
``` def
LIBRARY GfxPluginVRSPlugin
EXPORTS
    UnityPluginLoad
    UnityPluginUnload
    GetRenderEventFunc
    IsPipelineVRSSupported
```

将IUnityGraphics.h, IUnityInterface.h, IUnityGraphicsD3D12.h 从引擎安装目录(Unity\Hub\Editor\2022.3.62f3\Editor\Data\PluginAPI) 复制到当前目录的"/Unity"文件夹下。
撰写脚本使用MSVC生成dll到Unity工程的Asset/Plugin 目录下

``` py
import os
import sys
import subprocess
from pathlib import Path

# ─── 路径配置 ───────────────────────────────────────────
SOURCE_DIR = Path(__file__).parent          # NativePluginNative/
OUTPUT_DIR = SOURCE_DIR / "build"
DLL_NAME   = "GfxPluginVRSPlugin.dll"

# 编译的源文件（按需增删）
SOURCES = [
    SOURCE_DIR / "GfxPluginVRSPlugin.cpp",
    SOURCE_DIR / "RenderAPI_D3D12.cpp",
]


# ─── MSVC 工具链自动定位 ────────────────────────────────
VSWHERE = Path(r"C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe")


def find_msvc():
    """用 vswhere.exe 找最新 Visual Studio 的 vcvars64.bat（不手写路径）"""
    if not VSWHERE.exists():
        sys.exit(f"未找到 vswhere.exe（{VSWHERE}），请确认已安装 Visual Studio / Build Tools")

    result = subprocess.run(
        [str(VSWHERE), "-latest", "-property", "installationPath"],
        capture_output=True, text=True)
    vs_path = result.stdout.strip()
    if not vs_path:
        sys.exit("未找到 Visual Studio 安装")

    # 优先选现有版本的实际宏
    vcvars = Path(vs_path) / "VC" / "Auxiliary" / "Build" / "vcvars64.bat"
    if not vcvars.exists():
        # 兼容以前的老布局（x86 安装路径）
        vcvars = Path(r"C:\Program Files (x86)" + vs_path[vs_path.find("\\Microsoft"):]) / \
                 "VC" / "Auxiliary" / "Build" / "vcvars64.bat"
    if not vcvars.exists():
        sys.exit(f"未找到 vcvars64.bat: {vcvars}")
    return str(vcvars)


def run_in_vs_env(vcvars, cmd_list, cwd=None):
    """在 VS 开发者环境中执行命令：cmd /c ""vcvars && <cmd>" """
    cmd_str = " && ".join(cmd_list)
    full_cmd = f'""{vcvars}" && {cmd_str}"'
    return subprocess.run(
        f"cmd /c {full_cmd}",
        cwd=str(cwd) if cwd else None,
        shell=True)


def locate_unity_includes():
    """返回 Unity 头文件 include 目录列表。

    UNITY6 工程当前没有 Unity/ 头文件目录，这里先探测，
    找到就加入 -I，找不到则警告（真实编译还需要补齐头文件）。
    """
    candidates = [
        SOURCE_DIR / "Unity",                  # NativePluginNative/Unity/
        SOURCE_DIR.parent / "Unity",           # NativePluginVRS/Unity/
    ]
    found = [str(c) for c in candidates if c.is_dir()]
    return found


def main():
    print("=" * 60)
    print("编译 DLL...")
    print("=" * 60)

    # 1) 工具链定位
    vcvars = find_msvc()
    print(f"  MSVC: {vcvars}")

    # 2) 源文件存在性
    for src in SOURCES:
        if not src.exists():
            sys.exit(f"源文件不存在: {src}")

    # 3) include 路径（Unity 头）
    includes = locate_unity_includes()
    if not includes:
        print("  ⚠ 未找到 Unity 头文件目录（Unity/），"
              "请补齐 IUnityInterface.h / IUnityGraphics.h / IUnityGraphicsD3D12.h")

    # 4) def 文件（链接用，可选）
    def_file = SOURCE_DIR / "GfxPluginVRSPlugin.def"
    if not def_file.exists():
        print("  ⚠ 未找到 GfxPluginVRSPlugin.def，链接可能失败（导出符号缺失）")

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    # 5) 编译
    obj_files = []
    for src in SOURCES:
        obj = OUTPUT_DIR / (src.stem + ".obj")
        obj_files.append(str(obj))
        inc_args = " ".join(f"/I{inc}" for inc in includes)
        compile_cmd = (
            f"cl /nologo /c /EHsc /std:c++17 /O2 "
            f"{inc_args} /Fo\"{obj}\" \"{src}\""
        )
        ret = run_in_vs_env(vcvars, [compile_cmd])
        if ret.returncode != 0:
            sys.exit(f"编译失败: {src}")

    # 6) 链接
    dll_path = OUTPUT_DIR / DLL_NAME
    def_arg = f"/DEF:\"{def_file}\"" if def_file.exists() else ""
    objs_arg = " ".join(f'"{o}"' for o in obj_files)
    link_cmd = (
        f"link /nologo /DLL /OUT:\"{dll_path}\" {def_arg} "
        f"{objs_arg} d3d12.lib dxgi.lib"
    )
    ret = run_in_vs_env(vcvars, [link_cmd])
    if ret.returncode != 0:
        sys.exit("链接失败")

    print(f"  {dll_path} ({dll_path.stat().st_size} bytes)")
    print()
    print("✅ 构建完成!")


if __name__ == "__main__":
    main()
```

在Asset目录创建文件 NativePluginBridge.cs 获取Native Plugin中导出的函数 GetRenderEventFunc。 

```csharp
using System.Runtime.InteropServices;
using System;

// 与 D3D12_SHADING_RATE 和 D3D12_SHADING_RATE_COMBINER 保持一致
public enum PipelineShadingRate
{
    Rate1x1 = 0x0, Rate1x2 = 0x1, Rate2x1 = 0x4,
    Rate2x2 = 0x5, Rate2x4 = 0x6, Rate4x2 = 0x9, Rate4x4 = 0xA,
}
public enum PipelineShadingRateCombiner
{
    Passthrough = 0, Override = 1, Min = 2, Max = 3, Sum = 4,
}

public static class NativePluginBridge
{
    const string PluginName = "GfxPluginVRSPlugin";

    [DllImport(PluginName)]
    static extern int IsPipelineVRSSupported();
    // 能力查询：设备是否支持 Pipeline VRS
    public static bool IsPipelineVRSSupportedQuery()
        => IsPipelineVRSSupported() != 0;

    // 与 native 的 PipelineShadingRate_EVENT_ID 保持一致
    public const int PipelineShadingRate_EVENT_ID = 0;

    // 位布局与 native 的 SHIFT/MASK 一致
    public const int RATE_SHIFT = 0;
    public const int COMB0_SHIFT = 4;
    public const int COMB1_SHIFT = 7;
    public const int RATE_MASK = 0xF;
    public const int COMB0_MASK = 0x7;
    public const int COMB1_MASK = 0x7;

    [DllImport(PluginName, EntryPoint = "GetRenderEventFunc")]
    static extern IntPtr GetRenderEventDataFunc();

    static IntPtr f;
    public static IntPtr RenderEventAndData =>
        f == IntPtr.Zero ? (f = GetRenderEventDataFunc()) : f;

    // 打包：rate/comb0/comb1 各占一段，位移用宏
    public static int PackRate(int rate, int c0, int c1)
        => ((rate & RATE_MASK) << RATE_SHIFT)
         | ((c0 & COMB0_MASK) << COMB0_SHIFT)
         | ((c1 & COMB1_MASK) << COMB1_SHIFT);
}
```

尝试撰写一个RenderFeature “VRSRenderFeature” 用于验证Pipeline shading rate的效果，通过Scriptabelpass `SetPipelineShadingRatePass` 将设置pipeline shading rate 插入管线中。
```csharp
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class NativePluginVRSFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
    }

    public Settings settings = new Settings();

    private SetPipelineShadingRatePass m_SetPipelineShadingRatePass;

    public override void Create()
    {
        m_SetPipelineShadingRatePass = new SetPipelineShadingRatePass();
        m_SetPipelineShadingRatePass.renderPassEvent = settings.injectPoint;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType != CameraType.Game)
            return;
        if (!settings.enablePipelineVRS)
            return;
        if (!NativePluginBridge.IsPipelineVRSSupportedQuery())
            return;

        // 配置 Pass：把当前 rate/combiner 打包进 data
        int packed = NativePluginBridge.PackRate(
            (int)settings.rate, (int)settings.combiner0, (int)settings.combiner1);
        m_SetPipelineShadingRatePass.Setup(packed);

        renderer.EnqueuePass(m_SetPipelineShadingRatePass);
    }

    protected override void Dispose(bool disposing)
    {
        m_SetPipelineShadingRatePass = null;
    }

    /// <summary>
    /// 设置 pipeline shading rate 的 Pass。
    /// </summary>
    internal class SetPipelineShadingRatePass : ScriptableRenderPass
    {
    }
}
```
Inspector界面允许用户在管线合适的时机，设置指定的Pipeline shading rate 和 combinerOps。
```csharp
[System.Serializable]
public class Settings
{
    [Header("Pipeline Shading Rate")]
    public bool enablePipelineVRS = true;
    public PipelineShadingRate rate = PipelineShadingRate.Rate2x2;
    public PipelineShadingRateCombiner combiner0 = PipelineShadingRateCombiner.Passthrough;
    public PipelineShadingRateCombiner combiner1 = PipelineShadingRateCombiner.Passthrough;

    [Header("Injection Points")]
    [Tooltip("在哪里设置 pipeline shading rate")]
    public RenderPassEvent injectPoint = RenderPassEvent.BeforeRenderingOpaques;
}
```
`SetPipelineShadingRatePass` 中使用`cmd.IssuePluginEventAndData` 通过ID"PipelineShadingRate_EVENT_ID"调用Native函数 SetPipelineShadingRate 设置shading rate.
```csharp
/// <summary>
/// 设置 pipeline shading rate 的 Pass。
/// </summary>
internal class SetPipelineShadingRatePass : ScriptableRenderPass
{
    private int m_Packed;

    public SetPipelineShadingRatePass()
    {
        profilingSampler = new ProfilingSampler(nameof(SetPipelineShadingRatePass));
    }

    public void Setup(int packed)
    {
        m_Packed = packed;
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var ptr = NativePluginBridge.RenderEventAndData;
        if (ptr == System.IntPtr.Zero)
            return;

        var cmd = CommandBufferPool.Get("SetPipelineShadingRate");
        if (cmd == null)
            return;

        cmd.IssuePluginEventAndData(
            ptr, NativePluginBridge.PipelineShadingRate_EVENT_ID, (System.IntPtr)m_Packed);

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }
}
```
在Renderer上插入两个VRSRenderFeature，在绘制Opaque物体之前设置Pipeline Shading Rate 为 $4\times4$, 绘制完Transparent物体后设置Pipeline Shading Rate 为 $1\times1$
![20260824173510](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20260824173510.png)
下图中可以看到场景中的实体物体和天空盒都出现一定程度的变化
![20260824174008](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20260824174008.png)
通过RenderDoc截帧，设置的$4\times4$pipeline shading rate已经生效。
![20260824174328](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20260824174328.png)

但受限于无法修改源码，在URP管线上，我们很难为每一个绘制指令（Drawcall）单独进行Pipeline Shading Rate的设置。 目前URP中提供的接口 `ScriptableRenderContext.DrawRenderers`, `CoammandBuffer.DrawRendererList` 作用更类似于录制一批渲染对象，其后续会通过批处理器生成若干个SRP Batcher（或Drawcall），排序后提交实际的渲染指令到CommandList中。 最理想的情况下，应当是最好能在每个绘制指令提交前设置其对应的pipeline shading rate，但这在不修改源码情况是很难实现的。
### NativePlugin 中实现 Attachment Shading Rate
首先查询当前设备是否支持Attachment Shading Rate。
通常来说，pipeline shading rate 会被归为Tier 1 的VRS能力。 Attachment shading rate 和 Primitive shading rate 被归为Tier 2的VRS 能力。
DX12 中通过 [D3D12_VARIABLE_SHADING_RATE_TIER](https://learn.microsoft.com/en-gb/windows/win32/api/d3d12/ne-d3d12-d3d12_variable_shading_rate_tier) 获取 VRS能力的级别。如果取值为 D3D12_VARIABLE_SHADING_RATE_TIER_1 说明仅支持pipeline shading rate。 而取值为 D3D12_VARIABLE_SHADING_RATE_TIER_2 则说明三种能力都支持。
``` h
// RenderAPI_D3D12.h
public: 
    bool IsImageVRSSupported() const { return m_AttachmentVRSSupported; }

private: 
    bool m_AttachmentVRSSupported = false;
```
``` cpp
// RenderAPI_D3D12.cpp
void RenderAPI_D3D12::InitializeVRSCapabilities()
{
    ID3D12Device* device = m_D3D12 != nullptr ? m_D3D12->GetDevice() : nullptr;
    if (!device)
    {
        m_PipelineVRSSupported = false;
        m_AttachmentVRSSupported = false;
        return;
    }

    D3D12_FEATURE_DATA_D3D12_OPTIONS6 options6 = {};
    HRESULT hr = device->CheckFeatureSupport(D3D12_FEATURE_D3D12_OPTIONS6, &options6, sizeof(options6));
    if (SUCCEEDED(hr))
    {
        D3D12_VARIABLE_SHADING_RATE_TIER m_VRSTier = options6.VariableShadingRateTier;
        m_PipelineVRSSupported = (m_VRSTier >= D3D12_VARIABLE_SHADING_RATE_TIER_1);
        m_AttachmentVRSSupported    = (m_VRSTier >= D3D12_VARIABLE_SHADING_RATE_TIER_2);
    }
    else
    {
        m_PipelineVRSSupported = false;
        m_AttachmentVRSSupported = false;
    }
}
```
<!-- ``` cpp
// GfxPluginVRSPlugin.cpp
extern "C" int UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API
IsAttachmentVRSSupported()
{
    return (s_API && s_API->IsAttachmentVRSSupported()) ? 1 : 0;
}
``` -->
为了生成SRI，还需要知道当前设备使用的“ShadingRateImageTileSize”以确定SRI图的大小。
> DX12 和 DX11(Nvapi) 的SRI的Tile Size多为定值正方形。 Vulkan, OpenGl ES上的实现则是为XY方向分布提供一个[范围](https://registry.khronos.org/VulkanSC/specs/1.0-extensions/man/html/VkPhysicalDeviceFragmentShadingRatePropertiesKHR.html). 
> Tile Size通常来说都是二的倍数。
``` h
// RenderAPI_D3D12.h
public: 
    UINT GetShadingRateImageTileSize() const { return m_ShadingRateImageTileSize; }

private: 
    UINT m_ShadingRateImageTileSize = 16;
```
``` cpp
// RenderAPI_D3D12.cpp
void RenderAPI_D3D12::InitializeVRSCapabilities()
{
    ID3D12Device* device = m_D3D12 != nullptr ? m_D3D12->GetDevice() : nullptr;
    if (!device)
    {
        m_PipelineVRSSupported = false;
        m_AttachmentVRSSupported = false;
        return;
    }

    D3D12_FEATURE_DATA_D3D12_OPTIONS6 options6 = {};
    HRESULT hr = device->CheckFeatureSupport(D3D12_FEATURE_D3D12_OPTIONS6, &options6, sizeof(options6));
    if (SUCCEEDED(hr))
    {
        D3D12_VARIABLE_SHADING_RATE_TIER m_VRSTier = options6.VariableShadingRateTier;
        m_PipelineVRSSupported = (m_VRSTier >= D3D12_VARIABLE_SHADING_RATE_TIER_1);
        m_AttachmentVRSSupported    = (m_VRSTier >= D3D12_VARIABLE_SHADING_RATE_TIER_2);
    }
    else
    {
        m_PipelineVRSSupported = false;
        m_AttachmentVRSSupported = false;
    }
}
```

<!-- DX12 通过 RSSetShadingRateImage 设置SRI 图到渲染状态中。 由于NativePlugin接口不提供接管RT的 D3D12_RESOURCE_BARRIER 的能力， 而SRI在设置前需要将 D3D12_RESOURCE_BARRIER 切换到 D3D12_RESOURCE_STATE_SHADING_RATE_SOURCE。 为避免SRI的 D3D12_RESOURCE_BARRIER (在NativePlugin中被修改后) 与Unity底层维护的状态发生冲突，需要在 NativePlguin中创建SRI图并手动进行管理。 -->

DX12 通过 `RSSetShadingRateImage` 设置SRI 图到渲染状态中。 这里在NativePlugin里创建SRI图并设置在管线中。
> 为什么不在Unity中创建RT然后传到NativePlugin中使用: Unity （DX12上）创建RT默认为Typeless(DXGI_FORMAT_R8_TYPELESS)格式，后续再通过视图描述符(D3D12_*_VIEW_DESC)指定RT的具体格式。 而函数 [RSSetShadingRateImage](https://learn.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-id3d12graphicscommandlist5-rssetshadingrateimage#parameters) 硬性要求SRI的格式为 DXGI_FORMAT_R8_UINT 。 这里没法在调用 `RSSetShadingRateImage` 时指定SRI的格式解释为 DXGI_FORMAT_R8_UINT，所以要求SRI图在NativePlugin创建，管理。

SRI的尺寸是根据Render Target 和 TileSize 共同决定的。 在C#侧获取Render Target的尺寸，通过 `SetRenderTargetSize` 传入Native侧，记录在变量 `m_SriWidth`/`m_SriHeight` 上。 将RenderTarget尺寸除以TileSize后向上取整，得到SRI图的尺寸(Tile 数量)。
``` C#
// NativePluginBridge.cs
public static class NativePluginBridge
{
    [DllImport(PluginName, EntryPoint = "SetRenderTargetSize")]
    private static extern void SetRenderTargetSizeNative(int width, int height);

    public static void SetRenderTargetSizeSize(int width, int height)
    {
        SetRenderTargetSizeNative(width, height);
    }
}
```
``` h
// RenderAPI_D3D12.h
class RenderAPI_D3D12
{
public:
    bool SetShadingRateImageSize(int width, int height);
};
```
``` cpp
bool RenderAPI_D3D12::SetRenderTargetSize(int width, int height)
{
    if (!m_AttachmentVRSSupported)
        return false;

    if (m_SriResource && m_RenderTargetWidth == width && m_RenderTargetHeight == height)
        return true;   // 尺寸未变，复用
               
    int sriWidth  = (width  + (int)m_ShadingRateImageTileSize - 1) / (int)m_ShadingRateImageTileSize;
    int sriHeight = (height + (int)m_ShadingRateImageTileSize - 1) / (int)m_ShadingRateImageTileSize;
    CreateShadingRateImage(sriWidth, sriHeight);
    return m_SriResource != nullptr;
}
```

在NativePlugin侧实现创建SRI的函数`CreateShadingRateImage`，width/height 根据Render Target决定，需要从C#侧经由 `SetShadingRateImageSize` 传入。 SRI的格式可以参考[官方文档](https://learn.microsoft.com/en-us/windows/win32/direct3d12/vrs#format-layout-resource-properties)
``` h
// RenderAPI_D3D12.h
class RenderAPI_D3D12
{
private:

    // SRI related parameters.
    ID3D12Resource* m_SriResource = nullptr; // SRI 图
    D3D12_RESOURCE_STATES m_SRIState = D3D12_RESOURCE_STATE_COMMON;
    int m_SriWidth = 0;
    int m_SriHeight = 0;

    void CreateShadingRateImage(int width, int height);
    void ReleaseShadingRateImage();
};
```
``` cpp
// RenderAPI_D3D12.cpp
void RenderAPI_D3D12::CreateShadingRateImage(int width, int height)
{
    ID3D12Device* device = m_D3D12 ? m_D3D12->GetDevice() : nullptr;
    if (!device)
    {
        VRSLog("[VRS] CreateShadingRateImage: null device\n");
        return;
    }

    ReleaseShadingRateImage();

    // 1) SRI 纹理：R8_UINT + ALLOW_UNORDERED_ACCESS，初始 UNORDERED_ACCESS
    D3D12_HEAP_PROPERTIES heapProps = {};
    heapProps.Type = D3D12_HEAP_TYPE_DEFAULT; // GPU-local

    D3D12_RESOURCE_DESC desc = {};
    desc.Dimension = D3D12_RESOURCE_DIMENSION_TEXTURE2D;
    desc.Width = width;
    desc.Height = height;
    desc.DepthOrArraySize = 1;
    desc.MipLevels = 1;
    desc.Format = DXGI_FORMAT_R8_UINT;
    desc.SampleDesc.Count = 1;

    HRESULT hr = device->CreateCommittedResource(
        &heapProps, D3D12_HEAP_FLAG_NONE, &desc,
        D3D12_RESOURCE_STATE_COMMON, nullptr,
        IID_PPV_ARGS(&m_SriResource));
    if (FAILED(hr) || !m_SriResource)
    {
        VRSLog("[VRS] CreateShadingRateImage failed hr=0x%08X\n", (unsigned)hr);
        return;
    }

    m_SRIState = D3D12_RESOURCE_STATE_COMMON;   // 记录SRI的状态
}

void RenderAPI_D3D12::ReleaseShadingRateImage()
{
    SAFE_RELEASE(m_SriResource);
    m_SriWidth = 0;
    m_SriHeight = 0;
    m_SRIState = D3D12_RESOURCE_STATE_UNORDERED_ACCESS;
}

void RenderAPI_D3D12::OnDeviceShutdown()
{
    ReleaseShadingRateImage();
    m_D3D12 = nullptr;
}
```

`SetShadingRateImage` 中先将SRI的D3D12_RESOURCE_BARRIER 设置为 D3D12_RESOURCE_STATE_SHADING_RATE_SOURCE，然后调用RSSetShadingRateImage进行设置。
> 为了使Attachment Shading Rate能生效，combinersOp1 不能设置为 Passthrough.
``` h
// RenderAPI_D3D12.h
class RenderAPI_D3D12
{
private:
    bool SetShadingRateImage(int width, int height);
    bool ClearShadingRateImage();
};
```
```cpp
// RenderAPI_D3D12.cpp
bool RenderAPI_D3D12::SetShadingRateImage()
{
    if (!m_ImageVRSSupported || !m_SriResource || !m_SriSource)
    {
        VRSLog("[VRS] SetShadingRateImage: not supported or source missing (sri=%p source=%p)\n",
            m_SriResource, m_SriSource);
        return false;
    }

    UnityGraphicsD3D12RecordingState recordingState;
    if (!m_D3D12->CommandRecordingState(&recordingState))
    {
        VRSLog("[VRS] SetShadingRateImage: CommandRecordingState returned false\n");
        return false;
    }

    ID3D12GraphicsCommandList5* cmd5 = nullptr;
    HRESULT hr = recordingState.commandList->QueryInterface(
        IID_PPV_ARGS(&cmd5));
    if (FAILED(hr) || !cmd5)
    {
        OutputDebugStringA("[VRS] SetShadingRateImage: QueryInterface for ID3D12GraphicsCommandList5 failed\n");
        return false;
    }

    // SRI barrier 需要设置为 D3D12_RESOURCE_STATE_SHADING_RATE_SOURCE.
    if (m_SRIState != D3D12_RESOURCE_STATE_SHADING_RATE_SOURCE)
    {
        D3D12_RESOURCE_BARRIER transitionBarrier = {};
        transitionBarrier.Type = D3D12_RESOURCE_BARRIER_TYPE_TRANSITION;
        transitionBarrier.Transition.pResource = m_SriResource;
        transitionBarrier.Transition.Subresource = D3D12_RESOURCE_BARRIER_ALL_SUBRESOURCES;
        transitionBarrier.Transition.StateBefore = m_SRIState;
        transitionBarrier.Transition.StateAfter  = D3D12_RESOURCE_STATE_SHADING_RATE_SOURCE;
        cmd5->ResourceBarrier(1, &transitionBarrier);
        m_SRIState = D3D12_RESOURCE_STATE_SHADING_RATE_SOURCE;
    }

    cmd5->RSSetShadingRateImage(m_SriResource);
    cmd5->Release();

    VRSLog("[VRS] SetShadingRateImage: sri=%p source=%p cmdList=%p copy=(%dx%d)\n",
        m_SriResource, m_SriSource, recordingState.commandList, m_SriWidth, m_SriHeight);
    return true;
}

bool RenderAPI_D3D12::ClearShadingRateImage()
{
    if (!m_ImageVRSSupported || !m_SriResource)
    {
        OutputDebugStringA("[VRS] ClearShadingRateImage: Image VRS not supported or SRI not created\n");
        return false;
    }

    UnityGraphicsD3D12RecordingState recordingState;
    if (!m_D3D12->CommandRecordingState(&recordingState))
    {
        OutputDebugStringA("[VRS] ClearShadingRateImage: CommandRecordingState returned false\n");
        return false;
    }

    // 统一使用 cmd5，所有操作都用它。
    ID3D12GraphicsCommandList5* cmd5 = nullptr;
    HRESULT hr = recordingState.commandList->QueryInterface(
        IID_PPV_ARGS(&cmd5));
    if (FAILED(hr) || !cmd5)
    {
        OutputDebugStringA("[VRS] ClearShadingRateImage: QueryInterface for ID3D12GraphicsCommandList5 failed\n");
        return false;
    }

    // 解除 SRI 绑定
    cmd5->RSSetShadingRateImage(nullptr);

    // 恢复 combiner 为 PASSTHROUGH，避免影响后续渲染
    D3D12_SHADING_RATE_COMBINER combiners[2] = {
        D3D12_SHADING_RATE_COMBINER_PASSTHROUGH,
        D3D12_SHADING_RATE_COMBINER_PASSTHROUGH
    };
    cmd5->RSSetShadingRate(D3D12_SHADING_RATE_1X1, combiners);

    // 解除绑定后，把 SRI 资源状态切回 COPY_DEST，供下次填充。
    if (m_SRIState == D3D12_RESOURCE_STATE_SHADING_RATE_SOURCE)
    {
        D3D12_RESOURCE_BARRIER barrier = {};
        barrier.Type = D3D12_RESOURCE_BARRIER_TYPE_TRANSITION;
        barrier.Transition.pResource = m_SriResource;
        barrier.Transition.Subresource = D3D12_RESOURCE_BARRIER_ALL_SUBRESOURCES;
        barrier.Transition.StateBefore = D3D12_RESOURCE_STATE_SHADING_RATE_SOURCE;
        barrier.Transition.StateAfter  = D3D12_RESOURCE_STATE_COMMON;
        cmd5->ResourceBarrier(1, &barrier);
        m_SRIState = D3D12_RESOURCE_STATE_COMMON;
    }

    cmd5->Release();

    OutputDebugStringA("[VRS] ClearShadingRateImage: RSSet(nullptr) + combiner restore + transition back to COPY_DEST\n");
    return true;
}
```

添加`SetShadingRateImage` 和 `ClearShadingRateImage` 的序号对应的eventID，在 `VRSRenderFeature` 中调用。 
``` cpp
// GfxPluginVRSPlugin.cpp
#define SetAttachmentShadingRate_EVENT_ID         1 //设置 attachment shaidng rate 的渲染事件ID
#define ResetAttachmentShadingRate_EVENT_ID         2 //清楚 attachment shaidng rate 的渲染事件ID

static void OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType)
{
    //...
    s_D3D12->ConfigureEvent(SetAttachmentShadingRate_EVENT_ID, &vrsCfg);
    s_D3D12->ConfigureEvent(ResetAttachmentShadingRate_EVENT_ID, &vrsCfg);
    //...
}

static void UNITY_INTERFACE_API OnRenderEventData(int eventID, void* data)
{
    if (s_API == nullptr) return;

    switch (eventID)
    {
        case PipelineShadingRate_EVENT_ID:
        {
            int p = (int)(intptr_t)data;
            D3D12_SHADING_RATE rate = (D3D12_SHADING_RATE)(p & VRS_RATE_MASK);
            D3D12_SHADING_RATE_COMBINER c0 = (D3D12_SHADING_RATE_COMBINER)((p >> VRS_COMB0_SHIFT) & VRS_COMB0_MASK);
            D3D12_SHADING_RATE_COMBINER c1 = (D3D12_SHADING_RATE_COMBINER)((p >> VRS_COMB1_SHIFT) & VRS_COMB1_MASK);
            s_API->SetPipelineShadingRate(rate, c0, c1);
            break;
        }
        case SetAttachmentShadingRate_EVENT_ID:
        {
            s_API->SetShadingRateImage();
            break;
        }
        case ResetAttachmentShadingRate_EVENT_ID:
        {
            s_API->ClearShadingRateImage();
            break;
        }
    default:
        break;
    }
}
```
``` C#
// NativePluginBridge.cs
public static class NativePluginBridge
{
    //..
    public const int SetAttachmentShadingRate_EVENT_ID = 1;
    public const int ResetAttachmentShadingRate_EVENT_ID = 2;
    //..
}
```
``` C#
// VRSRenderFeature.cs.cs
public static class NativePluginBridge
{
    //..
    public const int SetAttachmentShadingRate_EVENT_ID = 1;
    public const int ResetAttachmentShadingRate_EVENT_ID = 2;
    //..
}
```


使用Renderdoc 截帧，可以看到Attachment Shading Rate已经在管线中生效。

一般来说，实际项目中多根据渲染画面的亮度梯度控制画面着色频率的分布。 大致思路是: 在帧末尾计算画面各个区域(Tile)的亮度梯度，与设置的阈值进行比较, 得到各个区域的着色频率，记录在SRI图上。下一帧绘制场景前，将SRI图进行重投影并设置在管线上。
首先修改`VRSRenderFeature`，在 inspector 上添加用于比较梯度的阈值。

在UberPost阶段计算Quad上的梯度，写入一张R32_UINT纹理中。
> 如果考虑兼容的角度，这里使用一个compute shader来计算梯度可能合适。
> 这里的做法参考了移动版《三角洲行动》的分享中的做法。
>  推测是高通版本的Wave intrinsic 实现？ 我这没找到对应的文档。

因为需要在绘制场景前进行重投影，这里要求开启Predepth 并修改 Motion Vecotor相关的逻辑。
这里参考Predepth中计算Depth的方式，添加 

> 当RenderTarget无法被Tile Size整除时，靠近X/Y = 1的Tile可能出现Shading Point (SV_Position) 大于等于1的情况。 如果使用Texture.load读取SV_Position坐标上的纹理数据数据可能导致问题（返回0）。

## 缺陷:
以上介绍的方法只适合单线程渲染。因为开启多线程渲染后，URP中提交渲染指令的接口 与 commandline 的提交不在一个提交线程中。因而导致渲染线程生效前会重置渲染状态，二者不在一个CommandList中，导致VRS不生效。
此外Primitive Shading Rate 尝试写了下，
