# NexaCL 主线 XSR 复用检查

日期：2026-10-29。来源：Nexa-MC/NexaCL，main，提交 `2ecadffb74f40c07a674a7f72cefaa50d0dd1c0b`。
用户确认尚未提供共享包，本轮改为核实源码可复用边界。NexaMC/NexaCL 返回 Repository not found；组织名以实际可访问的 Nexa-MC 为准。旧 PCL-N/refactor/xsr 仅为历史基线。

## 结论

XSR 有可复用实现，核心可以通过版本化包被独立产品消费。无需等待一个尚不存在的公共包源，也无需将 NexaCL 的应用源码复制到 NexaSM。但从本地实验包到正式共享包还需完成构建资产、元数据、版本和发布流程。

本轮仅只读检查上游源码及执行构建/测试，在 /tmp 做本地打包实验；NexaCL git status 干净。没有发布包、修改 NexaCL main 或替换 NexaSM 的生产依赖。

## 工程边界（以 csproj 与登记图交叉核对）

| 边界 | 静态项目闭包 | 建议 |
|---|---|---|
| Nexa.Xsr.Runtime | 8：Core、Contracts、Sidecar.Protocol、Sidecar.Transport、Xsr.Abstractions、Diagnostics、State、Runtime | 首批共享核心候选；闭包中无 Nexa.Services 具体业务实现 |
| Nexa.Xsr.Renderer.Nxml.Runtime | 7：Core、Contracts、Abstractions、State、Renderer、Nxml.Compiler、Nxml.Runtime | M2 桌面前共享；还要处理 NXML/NSS 资产与 Renderer 后端 |
| Nexa.Xsr.Renderer.Backend.Avalonia | 直接依赖 Renderer；外部 Avalonia.Desktop/Themes.Fluent 12.1.0 | 只给桌面组合根消费，不能进入 Node/Domain/Services |
| Nexa.Platform.Abstractions | Core + 端口定义 | 评审进程身份、控制和资源采样等通用端口；部分命名空间仍在 Services 下 |
| Nexa.Platform.Runtime | 20 个项目，含 Java/Process/Capabilities 契约、Minecraft.Metadata、Services.Common/Recovery 等 | 不能整包视为纯通用平台层；先提取 SM 所需通用实现与最小依赖 |

以上数字是静态产品项目引用闭包，不含框架/外部 NuGet 和 Directory.Build.targets 注入的构建工具。完整机器可读列表见 [检查记录](nexacl-xsr-inspection.json)。

## 已执行验证

环境：Debian 13 x64、.NET SDK 10.0.403；上游 global.json 要求 10.0.100 并允许 latestFeature，当前 SDK 符合。

1. `dotnet run --project tests/Nexa.Xsr.Runtime.Tests/Nexa.Xsr.Runtime.Tests.csproj -c Release`：183 passed，0 skipped；没有使用 --skip-os-ipc。保留默认函数插桩构建。
2. 对上述 8 个核心项目运行 `dotnet pack -c Release --no-build -p:IsPackable=true -p:PackageVersion=2.0.0-smprobe.2ecadffb -o /tmp/nexasm-xsr-local-feed`，得到临时实验包。
3. 独立 Probe 仅有 `PackageReference Nexa.Xsr.Runtime`，无指向上游源码的 ProjectReference。测试类型化路由、Acceptance 与 Completion 分离、状态初始不可用、发布、标记 Stale 后保留旧值；正常运行通过。
4. 同一包消费者 Linux x64 Native AOT 发布后实际运行通过；没有将此结果外推为 Renderer、插件热补丁、跨平台或全部 Runtime 功能均通过。

原始日志：`/workspace/nexacl-xsr-runtime-inspection.log`、`/workspace/nexacl-xsr-pack-inspection.log`、`/workspace/nexacl-xsr-consumer-inspection.log`。临时消费者 `/tmp/nexasm-xsr-consumer`，临时源 `/tmp/nexasm-xsr-local-feed`。其寿命仅当前环境，不是持久发布源；包 SHA-256 已记录。

## 打包实验揭示的具体缺口

- 上游 Directory.Build.props 默认为 IsPackable=false；没有成熟的独立共享包发布入口。
- Directory.Build.targets 自动引入 Nexa.Xsr.Patch.Compiler，eng/xsr/Xsr.AutomaticFunctionPatches.targets 在编译后插桩并生成每程序集 .nfp。
- 上游输出目录有 .nfp，但本次 8 个 nupkg 均没有 .nfp。即使普通路由可运行，也不能宣称插件自动补丁能力随包完整交付。
- 消费者的构建不会自动继承仓库根 Directory.Build.targets。需决定并实现 build/buildTransitive 工具包或明确的构建 SDK，保证生成目录和编译器以正确角色传播；不能把 Mono.Cecil/编译器作为产品运行依赖。
- Nxml.Compiler 的 csproj 使用相对 Content 指向 Renderer/Styles/*.nss 和 Components/*.c.nxml，正式 NuGet 需要显式声明资产路径、复制/publish 行为与内容版本。
- 仓库 LICENSE 为 Apache-2.0；正式包需声明许可证、来源 commit、README、支持范围及所需第三方通知。现有产品版本属性不等于已经发布了独立包版本。

## 对 NexaSM 的接入约束

- Composition 负责把 SM 服务注册到 XsrCommandRouterBuilder/QueryRouter；服务实现继续只依赖 SM 契约与持久化端口。
- XsrCommandDispatch.Acceptance 是路由接受，不能映射为 SM 已持久化 OperationReceipt。要在服务持久提交后投影回执，长操作按自身 Operation 状态跟踪。
- XsrStateStore 是内存状态投影机制，不替代 SQLite 权威资源、幂等记录、Outbox 或执行账本。状态陈旧不变成游戏退出。
- XsrRuntimeId/SessionId 与 SM InstanceId/RunId/OwnerEpoch 保持独立，不因都叫 ID 而相互替代。
- 上游 SidecarSupervisor 管理插件进程和会话；shutdown 会结束被监管的子进程。不能拿它直接当作关闭 UI/Node 后仍必须保管 JVM 的 SM Supervisor。
- Platform 的进程身份接口可借鉴 Unknown/Running/Exited 语义，但不等于 SM 已实现 RunId、boot identity、LaunchNonce、计划摘要与卷写入权核验。

## 下一步可执行顺序

1. 在 NexaCL 增加独立共享包构建入口和元数据，保持现有产品构建不变；先实现 8 个核心包的完整依赖、.nfp 和工具包传播。共享包版本策略通过 ADR 单独确定。
2. 独立消费者同时验证普通/AOT 路由状态和所承诺的自动补丁能力，并检查 nupkg 内容与许可。实验 prerelease 不能作为正式稳定版。
3. 发布到组织可访问的包源；也可先生成带 manifest/摘要的本地开发 feed，由可复现命令构建，不把 /tmp 或个人目录写进 SM 项目依赖。
4. NexaSM 增加 Composition 适配与真实 PackageReference、依赖锁和消费者测试，保持 Domain/Services 边界。到此才能关闭 M0 正式共享包消费出口。
5. Platform 通用能力拆分与 Renderer/NXML 包化分别执行，不用引入整个 NexaCL Services 层来绕过边界。

这将原先“等待用户提供包信息”的缺口转为具体的上游工程任务。本次检查并未实施该上游产品变更。

## 实施后续

上述内容保留为初次源码检查记录。共享包资产与 SM Composition 现已实施，固定版本/提交/包哈希见 `eng/xsr-source.json`，当前验证与限制以 [M0 记录](m0.md) 为准。默认本地调用不等待 SidecarReady，性能预算优先。
