Stage 模型： 
- Basic Concept：https://docs.openharmony.cn/pages/v4.1/zh-cn/application-dev/application-models/stage-model-development-overview.md 概念图：![20240729105408](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240729105408.png)
  - UIAbility, ExtensionAblility 应用组件：have specific class, object-oriented
    - UIAbility: 提供UI，用于用户交互。生命周期只包含：创建/销毁/前台/后天等状态，通过WindowStage的事件获取
    - ExtensionAbility: 用于特定场景。第三方需要通过ExtensionAbility的派生来为特定创建开发自定义服务。 生命周期：用户触发管理，系统管理生命周期
  - WindowStage: 相当于应用进程的窗口管理器。每个UIAbility实例都与一个WindowStage实例绑定，UIAbility实例通过WindowStage持有一个主窗口，提供给ArkUI进行渲染的区域。
  - Context：Context及其派生类给开发者提供在**运行时**调用各种资源和功能的能力，UIAbility和ExtensionAbility的派生类都有不同的Context派生类，以具备不同的功能。
  - AbilityStage: 每个Entyr，Feature类型的HAP在运行时都有一个Ability类实例，带HAP代码首次加载到进程时的时候，系统会创建一个AbilityStage实例。

- 开发流程：
  - 应用组件开发
    - 应用/组件级配置：应用包名，图标等标识特征的配置。
      - 包名配置：AppScope/app.json5 中的 *bundleName* field，用于标识应用的，唯一的field。反域名命名：e.g.:com.example.demo, 一二三级分别为 域名后缀， 厂商/个人名， 应用名。
      - 图标和标签 （Icons and Labels）： 分为“应用”和“入口”两种。应用：app.json5 的 *icon*，*lable*； 入口： module.json5 的 *icon*，*lable*
        - 应用图标，标签：用于识别整个应用，在识别应用的界面使用。
          - “设置”的“应用管理”界面
          - “隐私管理”界面
          - 状态栏中通知消息时
        - 入口图标，标签：以UIAbility为粒度，支持存在多个入口图标，标签。点击该图标进入对应的UIAbility界面。（类似支付宝小程序那种？？）
          - 桌面上显示该图标时
          - 最近任务列表中显示时
      - ![20240729114656](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240729114656.png)
      - 应用图标/标签的配置： AppScope/app.json5。 *icon*: 图片索引； *label*, 字符串资源的索引
        ``` json5
        {
        "app": {
            "icon": "$media:app_icon",
            "label": "$string:app_name"
            ...
            }
        }
        ```
       - 入口图标/标签的配置：module.json5。对组件配置入口图标和标签。二者将显示在桌面上。*abilities*下的*icon*, *labels*。若想在桌面显示该UIAbility的图标，需要在*Skills*下配置 *entities*, *actions*进行指定。多个上述配置存在可以存在多个图标在桌面显示。
        ``` json5
        {
            "module": {
                ...
                "abilities": [
                {
                    "icon": "$media:icon",
                    "label": "$string:EntryAbility_label",
                    "skills": [
                    {
                        "entities": [
                        "entity.system.home"
                        ],
                        "actions": [
                        "ohos.want.action.home"
                        ]
                    }
                    ],
                }
                ]
            }
            }
        ``` 
         - 入口图标和标签管控规则：无入口图标，标签时默认使用应用图标，标签。如果需要隐藏入口图标的话，需要配置AllowAppDesktopIconHide应用特权
      - 应用版本声明配置： app.json5下的 *versionCode*, *versionName*。 *versionCode*：32位非负整数， 版本号，数值越大版本越新。 *versionName*：版本号的文字描述。
      - Module支持的设备类型配置: module.json5下的*deviceTypes*, 表明支持的设备类型
      - Module权限配置： module.json5下的*requestPermissions*，申请权限的名称、申请权限的原因以及权限使用的场景。
    - UIAbility：包含UI的应用组件，用于和用户交互
      - 设计理念：原生支持应用组件级的跨端迁移和多端协同；支持多设备和多窗口形态。
      - 设计原则：UIAbility组件是系统调度的基本单元，为应用提供绘制界面的窗口。每个实例会在任务列表/视图中对一个任务。一个应用可以包含一个或多个UIAbility组件
      - 声明配置：在module.json5配置文件的*abilities*下中声明UIAbility的名称、入口、标签等相关信息。
      ``` json5
      {
        "module": {
            ...
            "abilities": [
            {
                "name": "EntryAbility", // UIAbility组件的名称
                "srcEntry": "./ets/entryability/EntryAbility.ets", // UIAbility组件的代码路径
                "description": "$string:EntryAbility_desc", // UIAbility组件的描述信息
                "icon": "$media:icon", // UIAbility组件的图标
                "label": "$string:EntryAbility_label", // UIAbility组件的标签
                "startWindowIcon": "$media:icon", // UIAbility组件启动页面图标资源文件的索引
                "startWindowBackground": "$color:start_window_background", // UIAbility组件启动页面背景颜色资源文件的索引
                ...
            }
            ]
        }
      }
      ``` 
      - 生命周期: 当用户打开、切换和返回应用时，UIAbility实例会在不同状态间转换（发生转换时有回调）。有 Create、Foreground，Background、Destroy四种状态。![20240729141036](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240729141036.png)
        - Create状态：UIAbility实例创建时，系统调用onCreate()回调，在回调中进行页面初始化操作。
                      P.S.:  信息传递载体Want: 在应用组件之间传递信息。
          - WindowStageCreate和WindowStageDestroy：![20240729141808](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240729141808.png)
            onWindowStageCreate()回调中通过loadContent()方法设置应用要加载的页面，并根据需要调用on('windowStageEvent')方法订阅WindowStage的事件
            onWindowStageDestroy()回调，可以在该回调中释放UI资源。
        - Foreground和Background状态: Foreground和Background状态分别在UIAbility实例切换至前台和切换至后台时触发，对应于onForeground()回调和onBackground()回调。
            onForeground(): 在UIAbility的UI可见之前触发，申请系统需要的资源，或者重新申请在onBackground()中释放的资源。
            onBackground()：UIAbility的UI完全不可见之后触发，释放UI不可见时无用的资源，或者在此回调中执行较为耗时的操作，例如状态保存等。
        P.S. *singleton*启动模式的UIAbility，调用startAbility()方法启动该UIAbility实例时，只会进入该UIAbility的onNewWant()回调，不会进入其onCreate()和onWindowStageCreate()生命周期回调。
        *specified*下如果只是绑定现有实例，而没有新创建的话，也同上。
        - Destroy：UIAbility实例销毁时触发，在onDestroy()回调中进行系统资源的释放、数据的保存等操作。
      - 启动模式：UIAbility实例在启动时的不同呈现状态。有singleton（单实例模式）multiton（多实例模式）specified（指定实例模式）三种。 在module.json5下的*launchType*字段配置。
        ``` json5
        {
            "module": {
                ...
                "abilities": [
                {
                    "launchType": "singleton",
                    ...
                }
                ]
            }
        }
        ``` 
        - singleton：默认的启动方式。一个应用进程中只存在一个该UIAbility实例，如已存在，则复用。字段配置为"singleton"
        - multion: 每次调用startAbility()方法时，都会在应用进程中创建一个新的该类型UIAbility实例。字段配置为multiton
        - specified: 指定实例。字段配置为specified。在创建UIAbility实例之前，可以为该实例指定唯一的字符串Key，作为识别该实例的判断。使用Key来寻找/创建 指定的UIAbility实例。
      - 基本用法：指定UIAbility的启动页面；获取UIAbility的上下文UIAbilityContext
        - 指定UIAbility的启动页面：UIAbility启动时，需要指定启动界面，否则为默认的白屏。onWindowStageCreate()下通过WindowStage对象的loadContent()中进行设置。
        - 获取UIAbility的上下文UIAbilityContext： UIAbility的上下文为 UIAbilityContext类的实例。其包含abilityInfo、currentHapModuleInfo等属性，UIAbility的相关配置信息和操作UIAbility实例的方法。
           在UIAbility中可以通过this.context
           在页面中，导入依赖资源context模块，然后在组件或本地定义一个context变量
      -  UIAbility组件与UI的数据同步的方式： 使用EventHub进行数据通信； 使用AppStorage/LocalStorage进行数据同步
         - 使用EventHub进行数据通信：EventHub提供了事件机制，有订阅，取消和触发事件等的能力。在基类context中存在EventHub对象，用于UIAbility组件实例内通信。
            UIAbility通过调用eventHub.on()注册事件。
            eventHub.emit()触发事件（传入参数信息）
            注册事件回调中可以得到对应的触发事件结果
            eventHub.off()方法取消该事件的订阅
         - AppStorage/LocalStorage进行数据同步：AppStorage是一个全局的状态管理器，适用于多个UIAbility共享同一状态数据的情况；而LocalStorage则是一个局部的状态管理器，适用于单个UIAbility内部使用的状态数据。
      - UIAbility组件间交互（设备内）：UIAbility是系统调度的最小单元。存在应用内，跨应用的UIAbility之间的跳转与交互。
        - 启动应用内的UIAbility：一个应用内包含多个UIAbility时，存在应用内启动UIAbility的场景。
          X UIAbility 调用startAbility()启动 Y UIAbility，传入*want*作为参数。
          Y UIAbility 在onCreate()或者onNewWant()中接受传入的参数，如*want* 。
          Y UIAbility 在业务结束后，调用terminateSelf() 停止自己？
          调用ApplicationContext的killAllProcesses()方法实现关闭应用所有的进程。
        - 启动应用内的UIAbility并获取返回结果：
           X UIAbility 调用startAbilityForResult()启动Y UIAbility。 
           Y UIAbility 在业务结束后，调用terminateSelfWithResult()，将参数abilityResult 返回给X
           X UIAbility 通过startAbilityForResult()接收
        - 启动其他应用的UIAbility：
          - API 11 以前：显式Want启动： 在want参数中设置该应用bundleName和abilityName。
          - API 12：隐式Want启动： 根据匹配条件由用户选择启动哪一个UIAbility。abilityName参数未设置，在入参Want中指定entities字段和actions字段，由系统判断合适的UIAbility交给用户选择。
            - 判断方式：Want中的entities，action必须包含在待匹配UIAbility的skills配置的entities和actions中。
        - 启动其他应用的UIAbility并获取返回结果： 类似上文，调用startAbilityForResult() 和 terminateSelfWithResult()
        - 启动UIAbility的指定页面：一个UIAbility可以对应多个页面，在启动UIAbility可以指定使用特定的页面。
          - 调用方UIAbility指定启动页面：通过want中的parameters参数增加一个自定义参数传递页面跳转信息。![20240729154701](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240729154701.png)
          - 被调用的UIAbility：UIAbility冷启动和UIAbility热启动
            - 目标UIAbility冷启动：UIAbility实例处于完全关闭状态下被启动，需要完整地加载和初始化UIAbility实例的代码、资源。
              触发onCreate()，在onWindowStageCreate()中解析*want*参数，得到需要加载的页面信息*url*，传入 windowStage.loadContent()
            - 目标UIAbility热启动：UIAbility实例已经启动并在前台运行过，目前在后台，再次启动该UIAbility实例。
              触发onNewWant()。在之前冷启动目标UIAbility时，目标通过getUIContext()保留UIContext对象。当热启动是，解析*want*得到*url*，再从UIContext中得到Router对象。通过Rounter对象并传入*url*进行跳转。
        - 启动UIAbility指定窗口模式（仅对系统应用开放）：使用startAbility()时，在入参中增加StartOptions参数的windowMode属性来配置窗口模式。module.json5下的 *abilities* 的 *supportWindowMode*字段中添加对展示形态的支持。
        P.S.: *windowMode*属性仅适用于**系统应用**，三方应用可以使用*displayId*属性。 系统应用： /system/app/目录下的?
          - 窗口模式： 全屏模式，悬浮窗模式或分屏模式。
        - 通过Call调用实现UIAbility交互（仅对系统应用开放）:Call调用提供一种能够被外部调用并与外部进行通信的能力，且支持前台与后台两种启动方式。通过startAbilityByCall()调用。![20240729162240](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240729162240.png)
        P.S.:  startAbilityByCall() vs startAbility(): startAbilityByCall支持前台与后台两种启动方式，而startAbility()仅支持前台启动; 调用方可使用startAbilityByCall()所返回的Caller对象与被调用方进行通信，而startAbility()不具备通信能力。 -->当需要与CalleeAbility通信，或被CalleeAbility需要在后台运行时，使用Call调用。
        PP.S.: CalleeAbility的启动模式需要为单实例
          - 创建Callee被调用端: 实现指定方法的数据接收回调函数、数据的序列化及反序列化方法.
            设置启动模式为singleton
            导入UIAbility模块
            定义约定的序列化数据
            实现Callee.on监听及Callee.off解除监听
          - 访问Callee被调用端：
            访问Callee被调用端
            通过startAbilityByCall()获取Caller通信接口，注册Caller的onRelease监听
    - ExtensionAbility：基于特定场景提供的应用组件。每一个具体场景对应一个ExtensionAbilityType，开发者只能使用（包括实现和访问）系统已定义的类型。（实现：第三方能否继承；访问：第三方能否访问）
      - 访问指定类型的ExtensionAbility组件：所有类型的ExtensionAbility组件均不能被应用直接启动，而是由相应的系统管理服务拉起，并由系统管理生命周期。![20240729164517](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240729164517.png)
      - 实现指定类型的ExtensionAbility组件：通过派生系统服务提供的基类，实现回调来实现具体的功能。实例依然是由系统服务管理生命周期。
      - ServiceExtensionAbility ：SERVICE类型的ExtensionAbility组件，提供后台服务能力。被启用的ServiceExtensionAbility作为服务端，而发起启动的组件为客户端。系统应用可以启动和连接ServiceExtensionAbility组件，第三方应用只能连接。
      P.S.: 连接：client连接service，二者为强关联，client退出后，service也一起退出（需要等service上不再存在连接时？？）。
            启动：client启动service，二者为弱关联，client退出后，service可以继续存在。
      PP.S.: connect/disconnect 只能在主线程进行。
        - 生命周期：![20240729170511](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240729170511.png)
         - onCreate：创建时触发。
         - onRequest：client调用startServiceExtensionAbility()时触发
         - onConnect：client调用connectServiceExtensionAbility()触发
         - onDisconnect： 最后一个连接断开时触发。
         - onDestroy：不再使用服务且准备将其销毁该实例时触发
        - 实现：系统才能实现ServiceExtensionAbility
         - 准备：替换Full SDK；申请AllowAppUsePrivilegeExtension特权
         - 定义IDL接口：ServiceExtensionAbility作为后台服务，需要向外部提供可调用的接口，开发者可将接口定义在idl文件中，并使用IDL工具生成对应的proxy、stub文件
         - 创建ServiceExtensionAbility：
           工程Module对应的ets目录下新建一个目录并命名为ServiceExtAbility
           ServiceExtAbility目录下新建一个文件并命名为ServiceExtAbility.ets
           ServiceExtAbility.ets文件中导入ServiceExtensionAbility的依赖包，继承ServiceExtensionAbility并实现回调，返回ServiceExtImpl对象。
           module.json5中注册ServiceExtensionAbility
           ``` json5
            {
            "module": {
                ...
                "extensionAbilities": [
                {
                    "name": "ServiceExtAbility",
                    "icon": "$media:icon",
                    "description": "service",
                    "type": "service",
                    "exported": true,
                    "srcEntry": "./ets/ServiceExtAbility/ServiceExtAbility.ets"
                }
                ]
            }
            }
           ```
        - 启动一个后台服务: 
        系统应用通过startServiceExtensionAbility()方法启动一个后台服务，在onRequest()回调中传递*want*。Service业务结束后service调用terminateSelf()或client调用stopServiceExtensionAbility()停止。
        - 连接一个后台服务：通过connectServiceExtensionAbility()连接一个服务，服务的onConnect()接收*want*。client调用disconnectServiceExtensionAbility()断开连接；service在所以连接都断开后被系统销毁。
        - 客户端与服务端通信：client通过在onConnect()中获取到rpc.RemoteObject对象与service通信，有两种方式：使用服务端提供的IDL接口进行通信；直接使用sendMessageRequest接口向服务端发送消息
        - 服务端对客户端身份校验：IDL接口的stub端进行校验: 通过callerUid识别客户端应用; 通过callerUid识别客户端应用
      - EmbeddedUIExtensionAbility: EMBEDDED_UI类型的ExtensionAbility组件，提供了跨进程界面嵌入的能力.UIAbility的页面中通过EmbeddedComponent嵌入本应用的EmbeddedUIExtensionAbility提供的UI,EmbeddedUIExtensionAbility会在独立于UIAbility的进程中运行???
        - 提供方：启动的EmbeddedUIExtensionAbility
          - 生命周期：EmbeddedUIExtensionAbility提供了onCreate、onSessionCreate、onSessionDestroy、onForeground、onBackground和onDestroy生命周期回调![20240729174400](https://raw.githubusercontent.com/hwubh/Temp-Pics/main/20240729174400.png)
          - 开发步骤：类似ServiceExtensionAbility
        - 使用方：启动EmbeddedUIExtensionAbility的EmbeddedComponent组件
          - 开发步骤：在UIAbility的页面中通过EmbeddedComponent容器加载自己应用内的EmbeddedUIExtensionAbility
                     在want.parameters中添加字段ohos.extension.processMode.hostSpecified 或 ohos.extension.processMode.hostInstance
            - ohos.extension.processMode.hostSpecified： 控制非首次启动的EmbeddedUIExtensionAbility是否运行在同UIExtensionAbility的进程中，参数是进程名称。
            - ohos.extension.processMode.hostInstance控制启动的EmbeddedUIExtensionAbility是否按照独立进程启动，入参为true时，一定会新增一个进程。
            - 上述二者同时存在时，hostSpecified优先。
  - 进程模型
  - 线程模型
  - 任务管理
  - 应用配置文件