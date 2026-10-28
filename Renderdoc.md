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

- `renderdoc\driver\gl\wrappers\gl_framebuffer_funcs.cpp` 中: `WrappedOpenGL::FUNC(B)` 为 Wrapper函数的具体实现， 在抓帧时调用。 `WrappedOpenGL::Serialise_FUNC(B)` 为抓帧和回放时负责写入，读取数据的序列化函数。  以 glShadingRateEXT 为例, 具体实现如下
    ```cpp
    template <typename SerialiserType>
    bool WrappedOpenGL::Serialise_glShadingRateEXT(SerialiserType &ser, GLenum rate)
    {
    SERIALISE_ELEMENT(rate);
    SERIALISE_CHECK_READ_ERRORS();

    if(IsReplayingAndReading())
    {
        CheckReplayFunctionPresent(glShadingRateEXT);
        GL.glShadingRateEXT(rate);
        GetCtxData().m_LastShadingRateSource = 1;   // 回放时也要恢复"来源"
    }
    return true;
    }

    void WrappedOpenGL::glShadingRateEXT(GLenum rate)
    {
    SERIALISE_TIME_CALL(GL.glShadingRateEXT(rate)); // 真正调用驱动

    GetCtxData().m_LastShadingRateSource = 1;        // 抓帧时记录来源

        if(IsActiveCapturing(m_State))                   // 只有正在抓帧才写 chunk
        {
            USE_SCRATCH_SERIALISER(); // 全局(driver上唯一)的serialiser
            SCOPED_SERIALISE_CHUNK(gl_CurChunk); // 在 serialiser 上开辟一个 glShadingRateEXT 类型的 chunk
            Serialise_glShadingRateEXT(ser, rate); // 往这个 chunk 里写参数
            GetContextRecord()->AddChunk(scope.Get()); // 把序列化数据封装成一个 Chunk 对象返回
        }
    }
    ```
- `renderdoc\driver\gl\gl_driver.cpp` 中: 在 `WrappedOpenGL::ProcessChunk`中添加VRS相关的分发，用于在回放时调用 `WrappedOpenGL::Serialise_FUNC(B)` 进行Chunk中记录的数据的解析，并根据解析的数据重新调用驱动的函数完成参数填充/绘制。
    ```cpp
    case GLChunk::glFramebufferShadingRateEXT:
      return Serialise_glFramebufferShadingRateEXT(ser, eGL_NONE, eGL_NONE, 0, 0, 0, 0, 0);
    case GLChunk::glShadingRateEXT:
      return Serialise_glShadingRateEXT(ser, eGL_NONE);
    case GLChunk::glShadingRateCombinerOpsEXT:
      return Serialise_glShadingRateCombinerOpsEXT(ser, eGL_NONE, eGL_NONE);
    case GLChunk::glGetFragmentShadingRatesEXT:
      return Serialise_glGetFragmentShadingRatesEXT(ser, 0, 0, NULL, NULL);

    case GLChunk::glShadingRateQCOM:
      return Serialise_glShadingRateQCOM(ser, eGL_NONE);
    ```
    > 对应回放来说数据来源有三种: 记录（chunk + initial state）：
    > 事实来源，记录在 Chunk 中。回答“应用当时做了什么”。
    > 抓帧缓存：记录在 ContextData 中。事实来源的补充，回答“Context当前的状态”。 
    > 回放查询：回放时执行完指令后，直接向驱动进行查询。回答“重放之后这个驱动认为状态是什么”，用于展示和校验。
    这里 ContextData 中缓存了两个变量 `m_LastShadingRateSource` 和 `m_ShadingRateCombiners`。 使用的 ContextData的原因主要考虑几个因素:
    - 抓帧开始前没 Chunk信息，需要 ContextData 用于记录当前context 上的数据。
    - RenderDoc 自己会保存/恢复 GL 状态，但是很多操作（如 MASS resolve， overlay）会改变GL状态，这里就需要依赖 ContextData 记录的中缓存的信息进行恢复。
    - 多Context，每个context 上的渲染状态不同。
    > LastShadingRateSource 用于记录用了哪一种扩展， QCOM 记 2， EXT 记 1。
- `renderdoc\driver\gl\gl_initstate.h`: 添加 FramebufferShadingRateAttachmentData 用于记录抓帧时glFramebufferShadingRateEXT 使用的参数和SRI。记录在 chunk 的 FramebufferInitialData 中。
    ```cpp
    struct FramebufferShadingRateAttachmentData
    {
    bool valid;
    GLResource obj;
    int32_t baseLayer;
    int32_t numLayers;
    int32_t texelWidth;
    int32_t texelHeight;
    };

    DECLARE_REFLECTION_STRUCT(FramebufferShadingRateAttachmentData);

    struct FramebufferInitialData
    {
    bool valid;
    FramebufferAttachmentData Attachments[10];
    GLenum DrawBuffers[8];
    GLenum ReadBuffer;
    FramebufferShadingRateAttachmentData shadingRateAttachment;

    static const GLenum attachmentNames[10];
    };
    ```
- `renderdoc\driver\gl\gl_initstate.cpp`: 添加 FramebufferShadingRateAttachmentData 的序列化方式。
    ```cpp
    template <typename SerialiserType>
    void DoSerialise(SerialiserType &ser, FramebufferShadingRateAttachmentData &el)
    {
    SERIALISE_MEMBER(valid);
    SERIALISE_MEMBER(obj);
    SERIALISE_MEMBER(baseLayer);
    SERIALISE_MEMBER(numLayers);
    SERIALISE_MEMBER(texelWidth);
    SERIALISE_MEMBER(texelHeight);
    }

    template <typename SerialiserType>
    void DoSerialise(SerialiserType &ser, FramebufferInitialData &el)
    {
    SERIALISE_MEMBER(valid);
    SERIALISE_MEMBER(Attachments);
    SERIALISE_MEMBER(DrawBuffers);
    SERIALISE_MEMBER(ReadBuffer);
    if(ser.VersionAtLeast(0x24))
        SERIALISE_MEMBER(shadingRateAttachment);
    else if(ser.IsReading())
        el.shadingRateAttachment = {};
    }
    ```
    ```cpp
    data.shadingRateAttachment = {};
    if(HasExt[EXT_fragment_shading_rate_attachment])
    {
      GLint shadingRateType = eGL_NONE;
      GLuint shadingRateTexture = 0;
      GL.glGetNamedFramebufferAttachmentParameterivEXT(
          res.name, eGL_SHADING_RATE_ATTACHMENT_EXT,
          eGL_FRAMEBUFFER_ATTACHMENT_OBJECT_TYPE, &shadingRateType);
      if(shadingRateType == eGL_TEXTURE)
      {
        GL.glGetNamedFramebufferAttachmentParameterivEXT(
            res.name, eGL_SHADING_RATE_ATTACHMENT_EXT,
            eGL_FRAMEBUFFER_ATTACHMENT_OBJECT_NAME, (GLint *)&shadingRateTexture);

        data.shadingRateAttachment.valid = shadingRateTexture != 0;
        data.shadingRateAttachment.obj = TextureRes(ctx, shadingRateTexture);

        auto it = m_Driver->m_ShadingRateAttachments.find(id);
        if(it != m_Driver->m_ShadingRateAttachments.end())
        {
          data.shadingRateAttachment.baseLayer = it->second.baseLayer;
          data.shadingRateAttachment.numLayers = it->second.numLayers;
          data.shadingRateAttachment.texelWidth = it->second.texelWidth;
          data.shadingRateAttachment.texelHeight = it->second.texelHeight;
        }
      }
    }
    ```
    ```cpp 
    // ContextPrepare_InitialState 抓帧时写入
    data.shadingRateAttachment = {};
    if(HasExt[EXT_fragment_shading_rate_attachment])
    {
      GLint shadingRateType = eGL_NONE;
      GLuint shadingRateTexture = 0;
      GL.glGetNamedFramebufferAttachmentParameterivEXT(
          res.name, eGL_SHADING_RATE_ATTACHMENT_EXT,
          eGL_FRAMEBUFFER_ATTACHMENT_OBJECT_TYPE, &shadingRateType);
      if(shadingRateType == eGL_TEXTURE)
      {
        GL.glGetNamedFramebufferAttachmentParameterivEXT(
            res.name, eGL_SHADING_RATE_ATTACHMENT_EXT,
            eGL_FRAMEBUFFER_ATTACHMENT_OBJECT_NAME, (GLint *)&shadingRateTexture);

        data.shadingRateAttachment.valid = shadingRateTexture != 0;
        data.shadingRateAttachment.obj = TextureRes(ctx, shadingRateTexture);

        auto it = m_Driver->m_ShadingRateAttachments.find(id);
        if(it != m_Driver->m_ShadingRateAttachments.end())
        {
          data.shadingRateAttachment.baseLayer = it->second.baseLayer;
          data.shadingRateAttachment.numLayers = it->second.numLayers;
          data.shadingRateAttachment.texelWidth = it->second.texelWidth;
          data.shadingRateAttachment.texelHeight = it->second.texelHeight;
        }
      }
    }
    ```
    ```cpp 
    // Apply_InitialState 回放时apply
    const ResourceId framebufferId = GetID(live);
    if(data.shadingRateAttachment.valid &&
        HasExt[EXT_fragment_shading_rate_attachment])
    {
    const FramebufferShadingRateAttachmentData &a = data.shadingRateAttachment;
    GL.glFramebufferShadingRateEXT(
        eGL_DRAW_FRAMEBUFFER, eGL_SHADING_RATE_ATTACHMENT_EXT, a.obj.name, a.baseLayer,
        a.numLayers, a.texelWidth, a.texelHeight);
    m_Driver->m_ShadingRateAttachments[framebufferId] = {
        GetID(a.obj), a.baseLayer, a.numLayers, a.texelWidth, a.texelHeight};
    }
    else
    {
    m_Driver->m_ShadingRateAttachments.erase(framebufferId);
    }
    ```
- `renderdoc/driver/gl/gl_renderstate.cpp`: 向 GLRenderState （GL 状态快照） 添加 VRS 相关参数的字段。 抓帧时使用 FetchState记录， 回放时使用ApplyState应用。 这里需要注意的是，在使用EXT_fragment_shading_rate/QCOM_shading_rate 这两个不同的扩展时，记录的参数字段不同。
    ```cpp
    // FetchState
    if(HasExt[EXT_fragment_shading_rate])
    {
        GL.glGetIntegerv(eGL_SHADING_RATE_EXT, (GLint *)&ShadingRate);
        ShadingRateCombinerOp[0] = driver->GetCtxData().m_ShadingRateCombiners[0];
        ShadingRateCombinerOp[1] = driver->GetCtxData().m_ShadingRateCombiners[1];
    }
    else
    {
        ShadingRate = eGL_SHADING_RATE_1X1_PIXELS_EXT;
        ShadingRateCombinerOp[0] = eGL_FRAGMENT_SHADING_RATE_COMBINER_OP_KEEP_EXT;
        ShadingRateCombinerOp[1] = eGL_FRAGMENT_SHADING_RATE_COMBINER_OP_KEEP_EXT;
    }

    if(HasExt[QCOM_shading_rate])
    {
        GL.glGetIntegerv(eGL_SHADING_RATE_QCOM, (GLint *)&ShadingRateQCOM);
        ShadingRatePreserveAspectQCOM =
            GL.glIsEnabled(eGL_SHADING_RATE_PRESERVE_ASPECT_RATIO_QCOM) == GL_TRUE;
    }
    else
    {
        ShadingRateQCOM = eGL_SHADING_RATE_1X1_PIXELS_QCOM;
        ShadingRatePreserveAspectQCOM = false;
    }

    LastShadingRateSource = driver->GetCtxData().m_LastShadingRateSource;
    if(LastShadingRateSource == 0)
    {
        if(HasExt[QCOM_shading_rate] && !HasExt[EXT_fragment_shading_rate])
        LastShadingRateSource = 2;
        else if(HasExt[EXT_fragment_shading_rate])
        LastShadingRateSource = 1;
    }
    ```
    ```cpp
    // ApplyState
    if(HasExt[QCOM_shading_rate] && GL.glShadingRateQCOM && LastShadingRateSource != 2)
        GL.glShadingRateQCOM(ShadingRateQCOM);

    if(HasExt[EXT_fragment_shading_rate])
    {
        if(GL.glShadingRateEXT && !(LastShadingRateSource == 2 && !HasExt[QCOM_shading_rate]))
        GL.glShadingRateEXT(ShadingRate);
        if(GL.glShadingRateCombinerOpsEXT)
        {
        GL.glShadingRateCombinerOpsEXT(ShadingRateCombinerOp[0], ShadingRateCombinerOp[1]);
        driver->GetCtxData().m_ShadingRateCombiners[0] = ShadingRateCombinerOp[0];
        driver->GetCtxData().m_ShadingRateCombiners[1] = ShadingRateCombinerOp[1];
        }
    }

    if(HasExt[QCOM_shading_rate])
    {
        if(GL.glShadingRateQCOM && LastShadingRateSource == 2)
        GL.glShadingRateQCOM(ShadingRateQCOM);
        if(ShadingRatePreserveAspectQCOM)
        GL.glEnable(eGL_SHADING_RATE_PRESERVE_ASPECT_RATIO_QCOM);
        else
        GL.glDisable(eGL_SHADING_RATE_PRESERVE_ASPECT_RATIO_QCOM);
    }

    driver->GetCtxData().m_LastShadingRateSource = LastShadingRateSource;
    ```
    ```cpp
    // Clear
    if(ser.VersionAtLeast(0x24))
    {
        ser.Serialise("GL_SHADING_RATE_EXT"_lit, el.ShadingRate);
        ser.Serialise("GL_SHADING_RATE_COMBINER_OP_EXT"_lit, el.ShadingRateCombinerOp);
    }
    else if(ser.IsReading())
    {
        el.ShadingRate = eGL_SHADING_RATE_1X1_PIXELS_EXT;
        el.ShadingRateCombinerOp[0] = eGL_FRAGMENT_SHADING_RATE_COMBINER_OP_KEEP_EXT;
        el.ShadingRateCombinerOp[1] = eGL_FRAGMENT_SHADING_RATE_COMBINER_OP_KEEP_EXT;
    }

    if(ser.VersionAtLeast(0x25))
    {
        ser.Serialise("GL_SHADING_RATE_QCOM"_lit, el.ShadingRateQCOM);
        ser.Serialise("GL_SHADING_RATE_PRESERVE_ASPECT_RATIO_QCOM"_lit,
                    el.ShadingRatePreserveAspectQCOM);
        ser.Serialise("GL_SHADING_RATE_SOURCE"_lit, el.LastShadingRateSource);
    }
    else if(ser.IsReading())
    {
        el.ShadingRateQCOM = eGL_SHADING_RATE_1X1_PIXELS_QCOM;
        el.ShadingRatePreserveAspectQCOM = false;
        el.LastShadingRateSource = 0;
    }
    ```
- `renderdoc/driver/gl/gl_replay.cpp` : 将抓帧获取的信息汇总，翻译后写入 GLPipe （记录整个管线状态，包含渲染参数，FBO和各种资源的引用） 中，将地址（指针）供 UI 线程进行读取。 GLPipe Renderoc维护的一套GL/GLES统一的结构。 以便同时服务本地 UI、Python、远程 replay 和离线导出等功能。
- `renderdoc/api/replay/gl_pipestate.h`： 修改 GLPipe 的结构（RasterizerState/FBO）
    ```cpp
    // GLPipe::RasterizerState
    rdcpair<uint32_t, uint32_t> shadingRate = {1, 1};
    bool shadingRateQCOM = false;
    bool shadingRatePreserveAspectRatio = false;
    rdcpair<ShadingRateCombiner, ShadingRateCombiner> shadingRateCombiners =
        {ShadingRateCombiner::Keep, ShadingRateCombiner::Keep};

    // GLPipe::FBO
    Descriptor shadingRateAttachment;
    rdcpair<uint32_t, uint32_t> shadingRateTexelSize = {1, 1};
    ```
- `renderdoc/replay/renderdoc_serialise.inl` : 修改序列化的字段和结构体的大小，与 GLPipe 的结构保持一致。 用于从设备端(Android/QNX)返回数据到主机上。
- `qrenderdoc\Windows\PipelineState\GLPipelineStateViewer.ui` : 在 Rasterizer 分组第 4 行加两个 label，`shadingRate` 和 `shadingRateCombiners`. 
- `qrenderdoc\Windows\PipelineState\GLPipelineStateViewer.cpp`: 在 `clearState()`中设置UI界面上的默认值。 `setState()` 上根据使用的扩展，填充返回需要的渲染参数。FBO的attachments队列中添加SRI，显示sri的 类型、尺寸、格式、TileSize。
- `renderdoc\driver\gl\gl_hooks.cpp`, `renderdoc\driver\gl\egl_hooks.cpp`: 对 Hook 的 VRS相关函数的 next_gpa 进行保护，避免引用到hook函数自身。分别在初始化跳过，运行时，事后校验等方式进行保护。

完整数据流总结

```
【抓帧】
App: glShadingRateEXT(4x4)
  → GL.glShadingRateEXT（真实调用）
  → ContextData.m_LastShadingRateSource = 1
  → chunk: Serialise_glShadingRateEXT(rate)

App: glFramebufferShadingRateEXT(FBO, SR_ATTACHMENT, tex, 0, 1, 4, 4)
  → 真实调用
  → m_ShadingRateAttachments[fbo] = {tex, 0, 1, 4, 4}
  → chunk + MarkDirtyResource(tex) + MarkFBOReferenced

每 draw 快照
  → GLRenderState.FetchState()
      ShadingRate / Combiners / QCOM / Source
  → 序列化进帧数据

【回放】
初始状态 → Apply_InitialState → glFramebufferShadingRateEXT + 重建 map
每 draw   → ApplyState → 按 Source 设置 EXT 或 QCOM，恢复 combiner 缓存
chunk     → Serialise_xxx → 再调用真实 GL（保证与抓帧顺序一致）

【展示】
SavePipelineState()
  → 读 GLRenderState → 映射成 (x,y) 和 ShadingRateCombiner
  → 查 FBO attachment + map 补 texel size
  → GLPipe::State
  → GLPipelineStateViewer 文本 + 帧缓冲树节点
  → （远程时）经 ReplayProxy 序列化传到主机
```