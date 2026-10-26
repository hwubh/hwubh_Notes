- 通过 "LastShadingRateSource" 决定走EXT_fragment_shading_rate 还是 QCOM_shading_rate


添加扩展:
- `gl_common.h` 的 `EXTENSION_CHECKS()` 增加：
    ```c
    EXT_TO_CHECK(99, 99, EXT_fragment_shading_rate)
    EXT_TO_CHECK(99, 99, EXT_fragment_shading_rate_primitive)
    EXT_TO_CHECK(99, 99, EXT_fragment_shading_rate_attachment)
    EXT_TO_CHECK(99, 99, QCOM_shading_rate)
    ```
    > EXT_TO_CHECK 前两个取值代表 OpenGL 和 OpenGLES的版本取值. 如果取值为 3.0, 3.2, 则说明从  GL 3.0，GLES 3.2 起改扩展已经纳入了版本规划中, 不需要通过扩展的方式进行支持, 默认已经支持该扩展的能力. 如果取值为 99, 则说明必须依赖扩展. 
- `gl_driver.cpp` 的 `BuildGLExtensions()` / `BuildGLESExtensions()` 把扩展名加入 RenderDoc 对外暴露的扩展列表: 
    ```c
    m_GLExtensions.push_back("GL_EXT_fragment_shading_rate");
    m_GLExtensions.push_back("GL_EXT_fragment_shading_rate_primitive");
    m_GLExtensions.push_back("GL_EXT_fragment_shading_rate_attachment");

    m_GLESExtensions.push_back("GL_EXT_fragment_shading_rate");
    m_GLESExtensions.push_back("GL_EXT_fragment_shading_rate_primitive");
    m_GLESExtensions.push_back("GL_EXT_fragment_shading_rate_attachment");
    m_GLESExtensions.push_back("GL_QCOM_shading_rate");
    ```
    > 硬件查询时返回硬件支持情况的是 真实硬件支持的扩展列表(implExts) 和 RenderDoc支持的硬件列表(m_GLESExtensions/m_GLExtensions) 的交集.  这里 RenderDoc Hook了查询扩展的接口()

添加常量:
- `gl_enum.h` 增加VRS相关的参数常量: 包含Fragment Shading Rate的取值。 CombinerOps的取值。TileSize的长宽范围。 Aspect等。
    ```c
    eGL_SHADING_RATE_1X1_PIXELS_EXT = 0x96A6,
    eGL_SHADING_RATE_1X2_PIXELS_EXT = 0x96A7,
    eGL_SHADING_RATE_2X1_PIXELS_EXT = 0x96A8,
    eGL_SHADING_RATE_2X2_PIXELS_EXT = 0x96A9,
    eGL_SHADING_RATE_1X4_PIXELS_EXT = 0x96AA,
    eGL_SHADING_RATE_4X1_PIXELS_EXT = 0x96AB,
    eGL_SHADING_RATE_4X2_PIXELS_EXT = 0x96AC,
    eGL_SHADING_RATE_2X4_PIXELS_EXT = 0x96AD,
    eGL_SHADING_RATE_4X4_PIXELS_EXT = 0x96AE,
    eGL_SHADING_RATE_EXT = 0x96D0,
    eGL_SHADING_RATE_ATTACHMENT_EXT = 0x96D1,
    eGL_FRAGMENT_SHADING_RATE_COMBINER_OP_KEEP_EXT = 0x96D2,
    eGL_FRAGMENT_SHADING_RATE_COMBINER_OP_REPLACE_EXT = 0x96D3,
    eGL_FRAGMENT_SHADING_RATE_COMBINER_OP_MIN_EXT = 0x96D4,
    eGL_FRAGMENT_SHADING_RATE_COMBINER_OP_MAX_EXT = 0x96D5,
    eGL_FRAGMENT_SHADING_RATE_COMBINER_OP_MUL_EXT = 0x96D6,
    eGL_MIN_FRAGMENT_SHADING_RATE_ATTACHMENT_TEXEL_WIDTH_EXT = 0x96D7,
    eGL_MAX_FRAGMENT_SHADING_RATE_ATTACHMENT_TEXEL_WIDTH_EXT = 0x96D8,
    eGL_MIN_FRAGMENT_SHADING_RATE_ATTACHMENT_TEXEL_HEIGHT_EXT = 0x96D9,
    eGL_MAX_FRAGMENT_SHADING_RATE_ATTACHMENT_TEXEL_HEIGHT_EXT = 0x96DA,
    eGL_MAX_FRAGMENT_SHADING_RATE_ATTACHMENT_TEXEL_ASPECT_RATIO_EXT = 0x96DB,
    eGL_MAX_FRAGMENT_SHADING_RATE_ATTACHMENT_LAYERS_EXT = 0x96DC,
    eGL_FRAGMENT_SHADING_RATE_WITH_SHADER_DEPTH_STENCIL_WRITES_SUPPORTED_EXT = 0x96DD,
    eGL_FRAGMENT_SHADING_RATE_WITH_SAMPLE_MASK_SUPPORTED_EXT = 0x96DE,
    eGL_FRAGMENT_SHADING_RATE_ATTACHMENT_WITH_DEFAULT_FRAMEBUFFER_SUPPORTED_EXT = 0x96DF,
    eGL_FRAGMENT_SHADING_RATE_NON_TRIVIAL_COMBINERS_SUPPORTED_EXT = 0x8F6F,
    eGL_FRAGMENT_SHADING_RATE_PRIMITIVE_RATE_WITH_MULTI_VIEWPORT_SUPPORTED_EXT = 0x9780,

    eGL_SHADING_RATE_QCOM = 0x96A4,
    eGL_SHADING_RATE_PRESERVE_ASPECT_RATIO_QCOM = 0x96A5,
    eGL_SHADING_RATE_1X1_PIXELS_QCOM = 0x96A6,
    eGL_SHADING_RATE_1X2_PIXELS_QCOM = 0x96A7,
    eGL_SHADING_RATE_2X1_PIXELS_QCOM = 0x96A8,
    eGL_SHADING_RATE_2X2_PIXELS_QCOM = 0x96A9,
    eGL_SHADING_RATE_4X2_PIXELS_QCOM = 0x96AC,
    eGL_SHADING_RATE_4X4_PIXELS_QCOM = 0x96AE,
    ```
- `gl_stringise.cpp` 增加VRS相关的参数常量：`TOSTR_CASE_STRINGIZE_GLENUM` 负责将 enum取值，表示为 enum名称。用于在 UI 和 日志中表示。
    ```c
    TOSTR_CASE_STRINGIZE_GLENUM(GL_SHADING_RATE_1X1_PIXELS_EXT)
    TOSTR_CASE_STRINGIZE_GLENUM(GL_SHADING_RATE_1X2_PIXELS_EXT)
    TOSTR_CASE_STRINGIZE_GLENUM(GL_SHADING_RATE_2X1_PIXELS_EXT)
    TOSTR_CASE_STRINGIZE_GLENUM(GL_SHADING_RATE_2X2_PIXELS_EXT)
    TOSTR_CASE_STRINGIZE_GLENUM(GL_SHADING_RATE_1X4_PIXELS_EXT)
    TOSTR_CASE_STRINGIZE_GLENUM(GL_SHADING_RATE_4X1_PIXELS_EXT)
    TOSTR_CASE_STRINGIZE_GLENUM(GL_SHADING_RATE_4X2_PIXELS_EXT)
    TOSTR_CASE_STRINGIZE_GLENUM(GL_SHADING_RATE_2X4_PIXELS_EXT)
    TOSTR_CASE_STRINGIZE_GLENUM(GL_SHADING_RATE_4X4_PIXELS_EXT)
    TOSTR_CASE_STRINGIZE_GLENUM(GL_SHADING_RATE_EXT)
    TOSTR_CASE_STRINGIZE_GLENUM(GL_SHADING_RATE_ATTACHMENT_EXT)
    TOSTR_CASE_STRINGIZE_GLENUM(GL_FRAGMENT_SHADING_RATE_COMBINER_OP_KEEP_EXT)
    TOSTR_CASE_STRINGIZE_GLENUM(GL_FRAGMENT_SHADING_RATE_COMBINER_OP_REPLACE_EXT)
    TOSTR_CASE_STRINGIZE_GLENUM(GL_FRAGMENT_SHADING_RATE_COMBINER_OP_MIN_EXT)
    TOSTR_CASE_STRINGIZE_GLENUM(GL_FRAGMENT_SHADING_RATE_COMBINER_OP_MAX_EXT)
    TOSTR_CASE_STRINGIZE_GLENUM(GL_FRAGMENT_SHADING_RATE_COMBINER_OP_MUL_EXT)
    TOSTR_CASE_STRINGIZE_GLENUM(GL_MIN_FRAGMENT_SHADING_RATE_ATTACHMENT_TEXEL_WIDTH_EXT)
    TOSTR_CASE_STRINGIZE_GLENUM(GL_MAX_FRAGMENT_SHADING_RATE_ATTACHMENT_TEXEL_WIDTH_EXT)
    TOSTR_CASE_STRINGIZE_GLENUM(GL_MIN_FRAGMENT_SHADING_RATE_ATTACHMENT_TEXEL_HEIGHT_EXT)
    TOSTR_CASE_STRINGIZE_GLENUM(GL_MAX_FRAGMENT_SHADING_RATE_ATTACHMENT_TEXEL_HEIGHT_EXT)
    TOSTR_CASE_STRINGIZE_GLENUM(GL_MAX_FRAGMENT_SHADING_RATE_ATTACHMENT_TEXEL_ASPECT_RATIO_EXT)
    TOSTR_CASE_STRINGIZE_GLENUM(GL_MAX_FRAGMENT_SHADING_RATE_ATTACHMENT_LAYERS_EXT)
    TOSTR_CASE_STRINGIZE_GLENUM(GL_FRAGMENT_SHADING_RATE_WITH_SHADER_DEPTH_STENCIL_WRITES_SUPPORTED_EXT)
    TOSTR_CASE_STRINGIZE_GLENUM(GL_FRAGMENT_SHADING_RATE_WITH_SAMPLE_MASK_SUPPORTED_EXT)
    TOSTR_CASE_STRINGIZE_GLENUM(GL_FRAGMENT_SHADING_RATE_ATTACHMENT_WITH_DEFAULT_FRAMEBUFFER_SUPPORTED_EXT)
    TOSTR_CASE_STRINGIZE_GLENUM(GL_FRAGMENT_SHADING_RATE_NON_TRIVIAL_COMBINERS_SUPPORTED_EXT)
    ```
- `official/glext.h`、`official/glesext.h`、`official/glcorearb.h`：在RenderDoc的GL头文件中添加VRS相关的函数和常量定义
    ```c
    #ifndef GL_EXT_fragment_shading_rate
    #define GL_EXT_fragment_shading_rate 1
    #define GL_SHADING_RATE_1X1_PIXELS_EXT    0x96A6
    #define GL_SHADING_RATE_1X2_PIXELS_EXT    0x96A7
    #define GL_SHADING_RATE_2X1_PIXELS_EXT    0x96A8
    #define GL_SHADING_RATE_2X2_PIXELS_EXT    0x96A9
    #define GL_SHADING_RATE_1X4_PIXELS_EXT    0x96AA
    #define GL_SHADING_RATE_4X1_PIXELS_EXT    0x96AB
    #define GL_SHADING_RATE_4X2_PIXELS_EXT    0x96AC
    #define GL_SHADING_RATE_2X4_PIXELS_EXT    0x96AD
    #define GL_SHADING_RATE_4X4_PIXELS_EXT    0x96AE
    #define GL_SHADING_RATE_EXT               0x96D0
    #define GL_SHADING_RATE_ATTACHMENT_EXT    0x96D1
    #define GL_FRAGMENT_SHADING_RATE_COMBINER_OP_KEEP_EXT 0x96D2
    #define GL_FRAGMENT_SHADING_RATE_COMBINER_OP_REPLACE_EXT 0x96D3
    #define GL_FRAGMENT_SHADING_RATE_COMBINER_OP_MIN_EXT 0x96D4
    #define GL_FRAGMENT_SHADING_RATE_COMBINER_OP_MAX_EXT 0x96D5
    #define GL_FRAGMENT_SHADING_RATE_COMBINER_OP_MUL_EXT 0x96D6
    #define GL_MIN_FRAGMENT_SHADING_RATE_ATTACHMENT_TEXEL_WIDTH_EXT 0x96D7
    #define GL_MAX_FRAGMENT_SHADING_RATE_ATTACHMENT_TEXEL_WIDTH_EXT 0x96D8
    #define GL_MIN_FRAGMENT_SHADING_RATE_ATTACHMENT_TEXEL_HEIGHT_EXT 0x96D9
    #define GL_MAX_FRAGMENT_SHADING_RATE_ATTACHMENT_TEXEL_HEIGHT_EXT 0x96DA
    #define GL_MAX_FRAGMENT_SHADING_RATE_ATTACHMENT_TEXEL_ASPECT_RATIO_EXT 0x96DB
    #define GL_MAX_FRAGMENT_SHADING_RATE_ATTACHMENT_LAYERS_EXT 0x96DC
    #define GL_FRAGMENT_SHADING_RATE_WITH_SHADER_DEPTH_STENCIL_WRITES_SUPPORTED_EXT 0x96DD
    #define GL_FRAGMENT_SHADING_RATE_WITH_SAMPLE_MASK_SUPPORTED_EXT 0x96DE
    #define GL_FRAGMENT_SHADING_RATE_ATTACHMENT_WITH_DEFAULT_FRAMEBUFFER_SUPPORTED_EXT 0x96DF
    #define GL_FRAGMENT_SHADING_RATE_NON_TRIVIAL_COMBINERS_SUPPORTED_EXT 0x8F6F
    typedef void (APIENTRYP PFNGLGETFRAGMENTSHADINGRATESEXTPROC) (GLsizei samples, GLsizei maxCount, GLsizei *count, GLenum *shadingRates);
    typedef void (APIENTRYP PFNGLSHADINGRATEEXTPROC) (GLenum rate);
    typedef void (APIENTRYP PFNGLSHADINGRATECOMBINEROPSEXTPROC) (GLenum combinerOp0, GLenum combinerOp1);
    typedef void (APIENTRYP PFNGLFRAMEBUFFERSHADINGRATEEXTPROC) (GLenum target, GLenum attachment, GLuint texture, GLint baseLayer, GLsizei numLayers, GLsizei texelWidth, GLsizei texelHeight);
    #ifdef GL_GLEXT_PROTOTYPES
    GLAPI void APIENTRY glGetFragmentShadingRatesEXT (GLsizei samples, GLsizei maxCount, GLsizei *count, GLenum *shadingRates);
    GLAPI void APIENTRY glShadingRateEXT (GLenum rate);
    GLAPI void APIENTRY glShadingRateCombinerOpsEXT (GLenum combinerOp0, GLenum combinerOp1);
    GLAPI void APIENTRY glFramebufferShadingRateEXT (GLenum target, GLenum attachment, GLuint texture, GLint baseLayer, GLsizei numLayers, GLsizei texelWidth, GLsizei texelHeight);
    #endif
    #endif /* GL_EXT_fragment_shading_rate */
    ```

添加函数HOOK:
    > 所以完整的调用链是四段：应用调用 glShadingRateEXT → 落到导出的符号 glShadingRateEXT（由 FuncWrapper 生成，内部转调 glShadingRateEXT_renderdoc_hooked） → glShadingRateEXT_renderdoc_hooked 调 glhook.driver->glShadingRateEXT(...) → WrappedOpenGL::glShadingRateEXT 记录 chunk，然后调 GL.glShadingRateEXT(...)（真实驱动）
- `renderdoc/driver/gl/gl_dispatch_table_defs.h` 中: `ForEachSupported(FUNC)`声明"是驱动上的函数指针"名字, `DefineSupportedHooks()`声明 hook 的函数签名（`FuncWrapperN`）
    ```c
    FUNC(glShadingRateEXT, glShadingRateEXT); \
    FUNC(glShadingRateCombinerOpsEXT, glShadingRateCombinerOpsEXT); \
    FUNC(glGetFragmentShadingRatesEXT, glGetFragmentShadingRatesEXT); \
    FUNC(glShadingRateQCOM, glShadingRateQCOM); \

    FuncWrapper7(void, glFramebufferShadingRateEXT, GLenum, target, GLenum, attachment, GLuint, texture, GLint, baseLayer, GLsizei, numLayers, GLsizei, texelWidth, GLsizei, texelHeight); \
    FuncWrapper1(void, glShadingRateEXT, GLenum, rate); \
    FuncWrapper2(void, glShadingRateCombinerOpsEXT, GLenum, combinerOp0, GLenum, combinerOp1); \
    FuncWrapper4(void, glGetFragmentShadingRatesEXT, GLsizei, samples, GLsizei, maxCount, GLsizei *, count, GLenum *, shadingRates); \
    FuncWrapper1(void, glShadingRateQCOM, GLenum, rate); \
    ```
    > FuncWrapperN 的 ‘N’ 表示参数的数量。
    > ForEachSupported的 FUNC(A,B) 两个参数的含义: 第一个函数是RenderDoc实现的，存入 GLDispatchTable 的字段，用于决定hook最终调用的WrappedOpenGL方法(glhook.driver-><A>); 第二个函数表示从驱动中获取函数指针使用的名称(B).
    以 glShadingRateEXT 为例，其根据宏'FuncWrapper1' 展开后，实现如下: 
    ```c
    void glShadingRateEXT_renderdoc_hooked(GLenum rate)
    {
        SCOPED_GLCALL(glShadingRateEXT); // 负责将调用记录在Chunk上。 
        UNINIT_CALL(glShadingRateEXT, rate); // 是驱动上的函数指针不存在时的兜底默认值。
        return glhook.driver->glShadingRateEXT(rate);
    }
    ```
    > SCOPED_GLCALL 中写入Chunk时可能出现多线程竞态。 这里使用RAII 对象 （Threading::ScopedLock） hook函数体作用域中加锁（RAII 对象：构造时 Lock()，析构时 Unlock()）。
    > 因为通过加锁来保证Chunk的写入顺序，RenderDoc中hook的GL调用时串行的，所以提交效率会比真机情况慢？
- `gl_dispatch_table.h` 增加对应字段： 每个条目记录其对应的是驱动上的函数指针。
    ```c
    // EXT_fragment_shading_rate
    PFNGLFRAMEBUFFERSHADINGRATEEXTPROC glFramebufferShadingRateEXT;
    PFNGLSHADINGRATEEXTPROC glShadingRateEXT;
    PFNGLSHADINGRATECOMBINEROPSEXTPROC glShadingRateCombinerOpsEXT;
    PFNGLGETFRAGMENTSHADINGRATESEXTPROC glGetFragmentShadingRatesEXT;

    // QCOM_shading_rate
    PFNGLSHADINGRATEQCOMPROC glShadingRateQCOM;
    ```

- `renderdoc\driver\gl\wrappers\gl_framebuffer_funcs.cpp` 中: `WrappedOpenGL::FUNC(B)` 为 Wrapper函数的具体实现。 `WrappedOpenGL::Serialise_FUNC(B)` 为 Wrapper函数的具体实现。  以 glShadingRateEXT 为例,