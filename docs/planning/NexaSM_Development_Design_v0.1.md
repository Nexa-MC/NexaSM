# NexaSM 详细开发设计稿

> 文档标识：SM-DESIGN-001 · 修订：0.1 · 状态：开发评审草案  
> 对象：NexaSM Node / Controller / Standard  
> 架构：XSR · 桌面与 Web 双形态 · Alpha 桌面闭环优先  
> 本稿是设计规范，不是实现完成报告、性能承诺或已经冻结的公共 ABI。

## 0. 阅读约定与决策等级

**已确定**：来自本次讨论的产品决策。**本稿建议**：为开始开发而补齐的工程默认值，可以通过 ADR 修改。**需验证**：依赖平台、具体服务端版本或实测的事项。本文的“必须/禁止”指建议采用本稿后需要实施的工程约束，不代表对应代码已经存在。

范围仅包含 NexaSM。NexaAC 作为独立产品通过契约接入；本稿不重新设计其驱动、检测算法或证据验证内部实现。所有示例 ID、资源数量和配置均为示例；性能预算是初始测试门槛，不是测量结果。

参考基线为已读取的 NexaCL `refactor/xsr` 架构锁。通用机制沿用 XSR；NexaSM 的资源和业务语义由本产品拥有。参考资料见文末；仅确认到文件 blob 的内容，不把文档读取当作仓库实现审计。[R01–R05]

---

## 1. 产品基线

### 1.1 已确定的决策

| 编号 | 决策 |
|---|---|
| D-001 | NexaSM 是原 NexaSL 的更名，不另保留一个同职责的 NexaSL 产品。 |
| D-002 | 面向个人、小型社区服和大型 Minecraft 服务器网络，使用相同资源模型。 |
| D-003 | Node 是实际承载 Minecraft 的被管理节点；Controller 是控制中心；Standard 是一体化发行组合。 |
| D-004 | 桌面和 Web 共用业务契约，Alpha 先完成桌面闭环；CLI 用于自动化和恢复。 |
| D-005 | NexaSM 独立产品仓库，使用版本化 Nexa XSR / Platform 包，不引用 NexaCL 应用层源码。 |
| D-006 | X 负责执行与交换；S 负责业务；R 负责语义呈现，状态输入、意图输出。 |
| D-007 | 关闭 UI 默认不停服；管理链路与玩家游戏链路分离。 |
| D-008 | 单机不强制账号、云、Docker、Kubernetes 或外部数据库。 |
| D-009 | NexaAC 可以独立使用；SM 负责部署和管理，不成为每个游戏包的判定路径。 |
| D-010 | 所有容量、平台支持和兼容性声明必须附实测证据。 |

### 1.2 三个发行组合

| 发行形态 | 安装内容 | 默认职责 | 不默认包含 |
|---|---|---|---|
| Node | NodeHost、Supervisor、平台执行后端、CLI | 本机安装、启停、文件、卷、日志、观测 | GUI、全局调度、全网权威库 |
| Controller | 控制服务、管理 API、Web 静态资源、CLI | 配置权威、节点、工作负载、放置、发布、授权 | 本机游戏执行；可额外安装 Node |
| Standard | 本地控制服务、Node、Supervisor、Desktop、CLI；Web 可启用 | 无外部依赖的一体化开服 | 强制登录、强制远程暴露 |

Standard 不是低配版，可以管理远程 Node。只管理远端的电脑可仅安装 Console 组件（Desktop/CLI），不算第四种产品业务版本。部署角色与收费没有绑定。

### 1.3 首期不做的事情

不做通用 IaaS/计费平台；不自行实现 Minecraft 网络代理内核；不提供同一普通生存世界的透明多 JVM 分片；不承诺无停机迁移任意世界；不把 Kubernetes、跨地域多主或自动扩容云账单作为 Alpha 前置；不把客户端来源当作可信证明。

---

## 2. 用户流程与交互契约

### 2.1 个人首次开服

`首次启动 → 选择本地数据目录与后台运行方式 → 新建/导入 → 选择服务端与版本 → 环境和磁盘预检 → 明示接受所需条款 → 安装 → 启动 → 显示真实连接信息`。

系统自动建立默认 ControlDomain、Network 和 Singleton Workload，普通页面只显示“服务器”。用户不必先理解集群。安装和启动都显示独立 Operation，可离开页面、关闭 UI 后继续。

首次启动默认仅开放认证的本地管理端点，不自动开放公网管理端口、不自动映射路由器端口。游戏端口、管理端口、代理回源端口分别解释；“本机可连接”不等于“公网可连接”。

### 2.2 导入既有服务端

默认执行只读扫描：识别候选提供者、版本、启动方式、世界目录、文件风险、已知外部数据库依赖和可能运行中的进程。扫描不运行目录里的脚本或 JAR。

提供两种显式导入方式：

- **CopyManaged**：复制到受管目录，验证后切换为受管实例；原目录不删除。
- **AttachInPlace**：原地接入；授权根目录和可写范围固定，记录基线摘要，说明文件修改影响原实例。

导入目录不等于接管正在运行的 JVM。外部运行进程默认只观察；只有执行后端能验证进程身份和控制通道时才允许声明接管能力。否则安排停服后首次受管启动。

### 2.3 日常操作

实例页显示期望状态、实际状态、就绪条件、数据新鲜度和当前 Run。Start/Stop/Restart、备份、配置编辑均转换为受权命令。进度不得由按钮点击即时伪造；UI 等待权威状态和 OperationReceipt。

停止请求先设置 `DesiredState=Stopped`，再启动关闭流程。用户明确停止后，自动恢复不能把它重新拉起。取消一个停止操作也不能自动恢复为 Running；需要新的显式意图。

### 2.4 大型网络流程

`建立 Network → 注册 Node → 创建模板修订 → 创建 Workload → 生成部署计划 → 检查容量/隔离/依赖 → 审批 → 分批执行 → 验证就绪 → 纳入路由/实例池`。

主入口是 Network 与 Workload 聚合，而不是把几千个实例全部平铺。Node、Fleet、发布、操作、权限和故障使用不同视图。

### 2.5 故障交互

所有错误同时给出：稳定错误码、受影响资源、当前事实、是否可重试、下一步安全动作。不得把网络中断统一显示成校验失败；不得用未知玩家数 0 作为可以停服的证据。

破坏性动作（删除世界、恢复覆盖、强杀、节点接管）必须预览作用范围。确认绑定资源身份、配置版本和计划摘要；确认后若目标已变更，则失效重新预览。

---

## 3. XSR 分层与架构不变量

### 3.1 分层

| 层 | 负责 | 禁止 |
|---|---|---|
| X | 路由、状态、事件、作用域、异步执行、预算、传输适配、诊断 | Minecraft 版本规则、节点选择规则、页面业务 |
| S | 实例、安装、存储、备份、调度、Fleet、发布、权限、Operation | 直接操作 UI、具体渲染后端、全局 service locator |
| R | 本地快照呈现、输入草稿、交互、意图 | 渲染帧内 Query/IPC、启动进程、写业务状态 |

XSR 现有架构采用模块化组合，服务实现与具体 Runtime 路由绑定通过 Composition 分开；不能把 Controller、Node、Desktop 简单重命名为 X、S、R。[R01,R02,R04]

### 3.2 必须自动检查的不变量

| ID | 不变量 |
|---|---|
| INV-01 | 一个 Instance 同时最多拥有一个被本控制域承认的活动 Run；疑似遗留运行必须先核实。 |
| INV-02 | 一个独占可写 Volume 不得授权两个并发写入者；失联不构成旧写入者已停止的证明。 |
| INV-03 | UI 退出不会结束后台 Run 的生命周期作用域。 |
| INV-04 | Node 只执行自身范围内、代次有效且已授权的计划。 |
| INV-05 | 回传成功必须对应已完成的必要持久化，不能用路由接收冒充业务完成。 |
| INV-06 | State 有单一逻辑写入所有者；镜像只能消费、不能反向伪装为权威。 |
| INV-07 | 私密值不进入普通状态、日志、事件或导出清单。 |
| INV-08 | 网络队列、日志缓冲、任务并发与重试均有上限。 |
| INV-09 | 身份、控制代次、配置版本与协议会话编号分别管理。 |
| INV-10 | 未完成适配、未测量、未知、不可用不显示为通过或零值。 |
| INV-11 | 原始控制台、任意安装代码、脚本与插件更新按代码执行风险授权。 |
| INV-12 | 数据回滚与程序回滚分别建模，不能把切回旧 JAR 宣称为世界格式回滚。 |

### 3.3 三种不同的调度

X 的执行调度协调线程、CPU/IO 工作预算；S.Placement 决定 Run 放到哪个 Node；S.Sessions 决定游戏会话占用哪个实例容量。三者禁止合并为通用 Scheduler 大类。

---

## 4. 进程拓扑与生命周期

### 4.1 Standard

```text
Desktop / CLI / 可选 Web
          │ 认证的本地接口
          ▼
Standard Daemon（Controller + Node 的组合根）
          │ 最小执行控制协议
          ▼
Supervisor
          ├─ Minecraft JVM A
          ├─ Minecraft JVM B
          └─ Proxy JVM
```

控制服务和节点服务同进程时通过相同业务契约本地调用，不强制序列化或回环 HTTP。桌面永远是客户端。Web 静态文件可随 Standard 打包，但监听开关独立。

### 4.2 集群

```text
Console / Web / Automation
          ▼
Controller（API + 控制服务 + 权威库）
          │ 认证节点会话
     ┌────┼────┐
     ▼    ▼    ▼
   Node  Node  Node
     │    │    │
 Supervisor ...
     │
 游戏 JVM
```

Alpha 单控制者；多副本是后续能力。HA 控制域依赖明确的权威存储和每分区所有权，不自行发明共识协议，也不把 API 网关多副本当作调度高可用。

### 4.3 Supervisor 的硬边界

Supervisor 保管进程控制句柄、stdin/stdout/stderr、运行回执和有限输出缓冲；执行由 Node 生成的受限计划，核验 PlanDigest、RunId、控制代次和授权。它不选择节点、不下载模组、不做游戏业务决策。

它必须独立于 Node 的进程生命周期：Node 更新或重启时 JVM 和控制台仍可由存活 Supervisor 维护。但 **Supervisor 自身崩溃不默认承诺透明恢复 stdin/stdout**。操作系统不能凭 PID 重新构造已经消失的匿名管道连接；产品必须报告 `ControlChannelLost`，根据执行后端实际能力选择重新连接持久通道、只读观察或经过授权的安全重启。

若要实现 Supervisor 故障后不断服且保留控制台，应单独交付经验证的长期 IO broker 或每 Run 监管后端，并承担额外开销；不能靠一段 PID 扫描代码宣称已实现。

Windows Job 的 kill-on-close 可在最后句柄关闭时终止进程组；Linux systemd 的默认控制组终止行为也会影响子进程。Supervisor/JVM 不得错误地置于随 Node 停止而被清理的组中。必须测试真实安装服务、升级和崩溃路径，而不是只测试从终端启动。[R06,R07]

### 4.4 用户模式与系统服务模式

用户模式不承诺注销账户后继续运行；24/7 保证需要安装对应后台服务、正确的服务账户和系统启动策略。系统关机与 UI 退出是不同事件。配置 `OnMachineBoot`、`OnManagerStart` 和 `RestorePreviousDesiredState` 时必须显示区别。

---

## 5. 技术基线与平台能力

建议采用 .NET 10；Core/Contracts 保持可移植。Node、Supervisor、CLI 以 Native AOT 为目标；Desktop 沿用 Nexa 渲染基础。Controller/Standard 的 Web API 优先使用可验证的 Minimal API 和生成式序列化，按 RID 分别执行 AOT/trim 验收，不承诺任意第三方库都兼容。

ASP.NET Core Native AOT 有明确功能与序列化限制，因此 Web 框架、ORM、身份组件、协议库必须逐一验证；发布成功不等于所有运行分支通过。[R08]

| 平台 | 基础执行目标 | 资源限制和服务能力 |
|---|---|---|
| Windows x64 / ARM64 | 原生 JVM、文件、控制台 | Job/服务账户/系统服务能力按 OS 与权限探测 |
| Linux x64 / ARM64 | 原生 JVM、文件、控制台 | systemd/cgroups 等按实际可用情况启用 |
| macOS ARM64 / x64 | 原生 JVM、文件、控制台 | launchd 与相应平台能力独立验证 |

“目标平台”不是“已验收平台”。Provider、Java、native library、管理服务和打包 RID 必须共同匹配。最小系统版本由所选 .NET 与依赖支持面、实际构建和测试确定，不在本稿凭空宣称。

单机默认原生进程；容器为可选执行后端。原生进程隔离不等于多租户安全沙箱。高强度不可信租户隔离需要明确的 OS/容器/虚拟化部署配置，不通过 GUI 复选框伪装。

---

## 6. 业务模块、所有权和接口

| 模块 | 权威数据/行为 | 核心接口 | 不拥有 |
|---|---|---|---|
| Access | 身份、角色、授权与管理会话 | Authorize、PairNode、Revoke | UI 按钮、游戏玩家认证 |
| Networks | 控制域、Network、策略继承 | CreateNetwork、ResolvePolicy | JVM |
| Workloads | spec、模板引用、期望运行需求 | Create/Update/Scale | OS 进程句柄 |
| Instances | 实例身份、有效配置、Run 关联 | Start/Stop/Restart | 全网调度算法 |
| Provisioning | Provider、解析、安装、导入计划 | Inspect/Plan/Apply | 长期进程监管 |
| NodeLifecycle | 节点执行与就绪判断 | ExecutePlan、ObserveRun | 全局资源权威 |
| Storage | 卷、路径授权、挂载、备份、恢复 | AcquireVolume、Backup、Restore | 安装器规则 |
| Placement | 过滤、评分、资源预留、放置 | PlanPlacement、CommitReservation | 对局匹配规则 |
| Fleets | 实例数与空闲容量协调 | ReconcileFleet、Drain | 直接远程杀进程 |
| Sessions | 对局容量预留、确认、释放 | Reserve/Confirm/Release | 排位和游戏胜负 |
| Rollouts | 模板/策略发布、灰度、回退编排 | Plan/Start/Pause/Rollback | 保证世界格式可逆 |
| Operations | 操作身份、步骤、幂等、恢复 | Submit/Get/Cancel/Reconcile | 取代业务规则 |
| Observability | 指标、日志查询、诊断导出 | Subscribe/Query/Export | 修改实例实际状态 |
| Integrations | NexaAC、NexaCL、外部通知 | CapabilityAdapters | 对其他产品应用层的直接引用 |

模块不必全部独立程序集；必须按变更所有权和可检查边界拆分。Storage 和过程事实位于安装/实例协调之下，禁止双向引用。

每个模块包含 Contracts、领域规则/服务、持久化端口和测试；Composition 负责绑定实现和 Runtime 路由。服务实现不能引用具体 XSR Runtime、Renderer 或 ASP.NET 请求上下文。[R02,R04]

---

## 7. 资源模型与身份

### 7.1 关系图

```text
ControlDomain ── Node
     │
     └─ Network
          └─ Workload ── TemplateRevision / ProtectionBinding
                └─ Instance
                     └─ Run ── Placement ── Node
                          └─ VolumeAttachment ── Volume

Operation 引用资源；SessionReservation 引用游戏容量。
```

Node 可承载多个 Network；Network 不必有代理。Node 初版只属于一个 ControlDomain。TemplateRevision 不可变；InstanceId 跨重启保留；RunId 每次实际启动新建。Instance 名称和显示序号不是身份。

### 7.2 基本资源外形

```text
metadata:
  id, controlDomainId, networkId?, name, labels,
  resourceVersion, specGeneration, createdAt, deletionRequestedAt?
spec:
  用户声明的期望配置
status:
  observedGeneration, ownerEpoch, observedAt,
  availability, conditions, resource-specific facts
```

`ResourceVersion` 是并发控制 token；`SpecGeneration` 只在 spec 变化时递增；`StateRevision` 是某条状态流的修订；`OwnerEpoch` 是控制权代次。不能混用。

ID 使用不透明全局唯一值，语言 SDK 包装成强类型，不依赖 UUID 的时间顺序。分页使用明确的排序和游标。所有绝对时间用 UTC；超时和延时用本机单调时间，跨机器不直接比较单调时钟。

### 7.3 资源字段

| 资源 | 必要字段 |
|---|---|
| ControlDomain | id、trustConfig、storageMode、compatibilityPolicy |
| Network | id、domainId、defaultPolicies、labels |
| Node | id、machineIdentity、capabilities、labels、failureDomain、capacity、observedUsage、availability |
| Workload | mode、templateRevision、desiredState、placementPolicy、resourceProfile、volumePolicy、restartPolicy、admissionPolicy |
| TemplateRevision | provider/version、ArtifactSet、ConfigRevision、runtimeRequirement、launchProfile、contentDigest |
| Instance | workloadId、templateRevision、identityPolicy、effectiveConfigDigest、volumeRefs、currentRunId? |
| Run | instanceId、runId、launchNonce、planDigest、nodeId、ownerEpoch、processIdentity、phase、exitInfo |
| Placement | runId、nodeId、resourceReservation、authorityEpoch、admissionGrant、expiryPolicy |
| Volume | storageBackend、locationRef、accessMode、writerAuthority、backupPolicy、protectionFlags |
| Operation | id、commandId、target、actorRef、idempotencyKey、payloadDigest、state、steps、result |
| Artifact | digest、length、kind、sourceProvenance、verificationStatus、licenseMetadata? |
| SecretRef | id、version、purpose、accessPolicy；不包含明文 |

### 7.4 Workload 的正交维度

- `mode`：Singleton / Fleet。
- `softwareRole`：MinecraftServer / Proxy；未来通用服务通过新 Provider 声明。
- `dataPolicy`：PersistentExclusive / EphemeralClone / ExternalState。
- `executionBackend`：NativeProcess / 可选 Container。
- `sessionModel`：OpenWorld / SingleMatch / MultiMatch。
- `placementPolicy`：Pinned / SelectByLabels / Scheduled。

Fleet 不意味着没有持久数据；一个 JVM 可承载几场对局不能由管理器猜测，必须由声明的 CapacityModel 和可靠适配器提供。

---

## 8. 状态模型与运行状态机

### 8.1 正交状态

```text
DesiredState  = Running | Stopped
RunPhase      = Pending | Preparing | Starting | Running | Stopping | Exited | Failed
Health        = Unknown | Healthy | Degraded | Unhealthy
Admission     = Closed | Open | Draining
Availability  = Available | Stale | Unavailable
ControlState  = Attached | ControlChannelLost | UnmanagedObserved
```

失联是 Availability 的变化，不把最后 `RunPhase=Running` 改为 Exited。`Unknown` 尽量用于观测字段而非掩盖业务阶段。

条件另存：ProcessAlive、GameReady、ProtectionReady（要求 AC 时）、RoutingReady、CapacityReady。`AdmissionReady` 必须由当前玩法要求的条件共同计算，不能只检查 PID 存在。

### 8.2 状态流

```text
Stopped → Pending → Preparing → Starting → Running → Stopping → Exited
                        │          │            │
                        └──────────┴────────────┴→ Failed
```

健康变化、Drain 和可用性是独立维度。Run 退出后保持历史，不改造成新的运行。每次自动重试创建新 Run，并关联同一恢复事件链。

### 8.3 单写入所有权

控制侧拥有 spec；Supervisor 拥有进程事实；NodeLifecycle 拥有游戏就绪判断；Controller 维护节点报告的只读镜像；UI 只拥有草稿、选择和窗口状态。

采用 Snapshot + Delta：每条流标识 `StreamId/Epoch/Revision`；增量只能应用于匹配 base revision。缺口触发重新快照；已关闭会话的数据不能恢复新会话的 Available 状态。快照一致性只覆盖声明的范围，不构成全网事务。[R03]

---

## 9. 命令、查询、事件与 Operation

### 9.1 命令封套

```text
CommandEnvelope:
  CommandId
  IdempotencyKey
  TargetResourceId
  ExpectedResourceVersion?
  CorrelationId
  DeadlineForAdmission?
  PayloadSchemaVersion
  Payload
```

Actor 来自认证会话，不能由客户端任意填充管理员身份。预期版本用于 spec 修改和危险操作；系统内部的重试仍必须重新检查控制代次及操作有效性。

幂等域为 `ControlDomain + Principal + CommandKind + Target + IdempotencyKey`。同 key 同规范化 payload 返回已有 Operation；同 key 不同 payload 拒绝。幂等记录保留期和过期行为是公共契约；高风险操作不能在记录被清理后悄悄重新执行旧请求，要求新请求/确认。

### 9.2 接受与完成

`XSR RouteAccepted` 不等于 `OperationPersisted`。对外只有在必要资源修改、Operation 和待投递记录完成持久化后才返回 `OperationReceipt`。长操作接受后，UI 连接取消只结束等待，不取消业务。

命令通过事务写入业务库与 Outbox。投递可以重复；Node Inbox/执行账本按 operation/step 去重。不能声称跨数据库与 OS 进程创建天然 Exactly Once，必须通过幂等与状态核实闭环。

### 9.3 Operation 状态

`Pending → Running → Succeeded / Failed / Canceled / NeedsAttention`；处理中可进入 WaitingForCapacity、WaitingForNode 或 Paused。步骤记录前置条件、开始、结果摘要、补偿行为和幂等属性。

取消是请求，不是回滚承诺。目录提交、卷切换等进入不可中断临界区后，先完成安全提交/回滚，再响应取消。`CancelRejected`、`OutcomeUnknown` 必须可表达。

### 9.4 原始控制台命令的特殊性

stdin 写入成功不证明游戏执行成功；崩溃时可能无法知道是否已经执行。原始控制台命令默认不自动重放。返回 `Dispatched/ConfirmedByBridge/OutcomeUnknown`，不能冒充事务成功。重放金币发放等命令可能产生重复业务效果。

### 9.5 并发矩阵

| 操作组合 | 默认规则 |
|---|---|
| Start / Start | 同实例串行并去重；已有活动 Run 则返回现有事实或 Conflict |
| Start / Stop | 按资源意图版本排序；Stop 撤销旧启动授权，派生启动必须再检查 |
| Config save / Config save | ResourceVersion CAS，失败者获取差异 |
| Restore / Start | 互斥；恢复期间 MaintenanceIntent 阻止 Reconciler 拉起 |
| Install update / Run | 暂存可并行；修改活动文件必须停服或 Provider 明确支持 |
| Online backup / Config write | 根据一致性组和 Provider 协议协调，不默认安全 |
| Delete / Any mutation | 删除意图持久化后阻止新变更；最终删除仍检查安全条件 |

Event 描述已发生事实，不能代替当前状态，也不能替代持久审计。XSR 有界事件通道与数据库 Outbox 是不同层；不可假设内存事件天然耐久。[R02]

---

## 10. 启动、停止与恢复算法

### 10.1 启动

1. 鉴权、CAS、检查 maintenance/deletion 状态，持久化启动意图和 Operation。
2. 解析固定的 TemplateRevision、ConfigRevision 和 ArtifactSet，形成 ExecutionPlan；禁止到执行时才把 `latest` 重新解析。
3. 选择 Node，原子预留逻辑容量；Node 再做本机实际检查。
4. 申请 Volume 写入权限、端口计划和进程资源；获取有限执行授权。
5. 暂存、验证和提交运行目录；核对计划摘要、权限、Java/架构、磁盘与条款接受记录。
6. Node 生成 RunId/LaunchNonce，Supervisor 先记录本地启动意图，再创建进程并写回执。
7. 若出现“创建成功但回执未持久化”的崩溃窗口，先核实进程身份；无法核实时标记 NeedsAttention，禁止再启动一个副本。
8. 独立推进 ProcessAlive、GameReady、ProtectionReady 和 AdmissionReady，Operation 根据声明的完成条件结束。

启动进度不是固定百分比伪造；步骤可以报告未知剩余量。端口预检查存在竞争窗口，普通 Minecraft JVM 通常不能直接继承管理器预绑定 socket，因此最终 bind 失败仍是可处理错误，不宣称预检查保证端口。

### 10.2 停止

持久化 Stopped 意图 → 关闭新准入 → 根据策略 Drain → 请求优雅停止 → 等待退出 → 到达授权截止条件后才允许升级信号/强制结束 → 确认退出与卷状态 → 释放容量。

没有可靠玩家/对局数量时，Drain 标记 Unknown，不按“0 玩家”处理。人工 ForceStop 权限与普通 Stop 分开。

### 10.3 自动恢复

RestartPolicy 明确原因过滤、稳定运行重置窗口、指数退避、抖动、最大尝试、累计时长。崩溃风暴进入 Failed/NeedsAttention，不无限重启。

人工停止、维护、删除、磁盘不足或卷归属不明不得触发普通重启。离线自动重启权限由断线策略决定：Pinned 本地持久服可在不可迁移、仍持有本地写权限的前提下继续；临时 Fleet 是否允许离线补起必须单独授权，不能自行增加可分配容量。

### 10.4 进程身份

不能仅用 PID。记录 RunId、node boot identity、进程创建时间/系统句柄信息、可执行路径摘要、工作目录和 Supervisor 回执。验证不足就只观察，不按同名 `java` 进程接管或强杀。


---

## 11. Minecraft Provider 与安装系统

### 11.1 Provider 不是一条 `java -jar` 字符串

Provider 根据精确软件版本声明安装、Java、启动、配置、停止、就绪和兼容能力。初期参考切片可采用 Vanilla/Paper，随后适配 Fabric/Forge/NeoForge/Quilt 与 Velocity；具体首发覆盖需通过版本矩阵确定，不因统一接口而宣称四加载器已经兼容。

NeoForge 官方服务器安装流程包含 installer、参数文件和生成的运行脚本，说明单个 JAR 加通用参数不足以表达所有服务端启动方式。[R11]

### 11.2 接口草案

下列名称是拟定业务端口，不是现有 Nexa 包已存在的 API：

```csharp
public interface IServerProvider
{
    ProviderDescriptor Describe();
    ValueTask<InspectionReport> InspectAsync(InspectionRequest request, CancellationToken ct);
    ValueTask<InstallPlan> PlanInstallAsync(InstallRequest request, CancellationToken ct);
    ValueTask<ResolvedLaunchSpec> ResolveLaunchAsync(LaunchRequest request, CancellationToken ct);
    ValueTask<CompatibilityReport> CheckCompatibilityAsync(CompatibilityRequest request, CancellationToken ct);
}

public interface IExecutionBackend
{
    ValueTask<PrepareResult> PrepareAsync(ExecutionPlan plan, CancellationToken ct);
    ValueTask<StartReceipt> StartAsync(StartRequest request, CancellationToken ct);
    ValueTask<RunObservation> ObserveAsync(RunReference run, CancellationToken ct);
    ValueTask<DispatchReceipt> RequestStopAsync(StopRequest request, CancellationToken ct);
}
```

接口只接受类型化契约；CancellationToken 表示本次调用等待/准备取消，不能自动撤销已提交的长期 Run。`StartRequest` 必须携带可恢复身份和授权；类型需要在实际实现时补全并通过编译和契约测试。

### 11.3 安装计划

InstallPlan 固定来源、长度/摘要、需要执行的安装器、预期输出、磁盘预算、条款要求和回滚边界。步骤为下载、验证、提取、执行明确授权的安装器、验证输出、提交。计划不可在执行中静默换源并改变来源信任等级。

下载缓存按内容摘要管理，同时保留来源和信任元数据。摘要只证明内容匹配，不证明来源可信。来源未给可验证摘要时记录 `IntegrityEvidence=Unavailable`，不得伪造“官方已校验”。

安装器和第三方构建都是代码执行，必须以低权限身份运行，有时间/输出/磁盘限制。内置 Provider 不接受远程下发任意 shell；外部脚本作为独立高风险扩展显式授权。

### 11.4 Java 解析

RuntimeRequirement 包含版本范围、架构、供应来源约束及必要能力；解析后记录具体 Java identity。不得全局固定 Java 21，也不能静默使用 PATH 中第一个 Java。自动下载需要用户许可和磁盘预算；离线缓存可用时不强制联网。

JVM 参数以 argv 数组/受管参数文件表达，不通过 shell 拼接。Provider 明确特殊参数顺序；SecretRef 不进入可导出的命令行。确需敏感文件时采用最小 ACL、限时生命周期和清理记录。

### 11.5 内容管理

模组/插件更新生成新的 ArtifactSet 和配置修订，在暂存目录验证，不直接覆盖运行中 JAR。只凭文件名不能自动删除“客户端模组”；先使用可信元数据和 Provider 规则，未知关系显示 Unknown，并生成可审阅差异。

下载镜像、整合包、世界和插件的来源/许可分别记录。用户条款接受留主体、版本引用、时间和适用范围；不替用户静默接受第三方条款。

---

## 12. 配置、文件与实例目录

### 12.1 配置层次

`Provider defaults → TemplateRevision → Workload override → Instance override → per-Run resolved values`。每个字段声明合并策略；标量覆盖、列表替换/按 key 合并不能由 UI 猜测。最终保存 EffectiveConfigDigest 和来源映射。

每个字段声明 ApplyMode：Live、NextRun、Recreate、Immutable。普通文件能保存不等于服务端已应用；显示 PendingChanges。热加载只针对 Provider 确认支持的字段，不提供通用“热重载所有插件”按钮。

配置编辑保存原始文本/未知字段/可保留注释，最小差异修改；不认识的 YAML/TOML 部分不重写。磁盘外部修改通过 hash/mtime/实际内容重新读取检测；冲突时提供三方差异而非覆盖。

### 12.2 文件权限与 IO

所有远程文件 API 接收 VolumeId/WorkspaceId 和相对路径，不接收任意绝对路径。默认拒绝越界 symlink/reparse、归档路径穿越、设备路径和预算超限。只做字符串 `StartsWith(root)` 不是安全路径校验；平台后端尽可能使用基于已授权目录句柄的操作，并防止检查后路径替换。

上传先进入有配额的隔离暂存区，检查长度、摘要、类型和权限，再提交。实例数据、平台服务文件、SecretStore 和安装目录分离。

### 12.3 建议目录

```text
<data-root>/
  control/                 控制侧数据库与受控备份
  node/                    节点执行账本
  supervisor/              监管记录与受控 IPC 元数据
  artifacts/sha256/...      内容缓存；来源记录在数据库
  workspaces/<instance>/   工作目录、配置投影、受管内容
  volumes/<volume>/        持久世界；默认不随实例删除
  backups/<backup>/        清单、索引和数据引用
  staging/<operation>/     有期限的暂存区
  logs/                    分段滚动日志
  secrets/                 仅受限密钥材料/安全存储引用
```

根目录在 OOBE 明确选择，系统服务模式遵循对应 OS 的数据目录和 ACL。不把运行数据写进应用安装目录；导入原地目录用位置引用管理。

### 12.4 删除语义

删除 Instance 默认保留 Volume 和备份；彻底销毁数据是独立动作、权限和确认。使用持久化删除意图/保留策略，先解除路由、停止 Run、确认句柄和写权限，再清理。清理失败显示 pending，不删除数据库记录以假装成功。

---

## 13. Volume、备份、恢复与迁移

### 13.1 一致性等级

| 等级 | 允许声明的保证 | 使用前提 |
|---|---|---|
| StoppedConsistent | 受管进程停止后，其受覆盖文件集合一致 | 确认写入者退出；范围不自动包含外部 DB |
| ProviderQuiesced | Provider/桥接确认的应用静默边界 | 确认回执、写入集合和超时恢复机制 |
| StorageCrashConsistent | 存储快照层面的崩溃一致性 | 快照后端实际支持；应用恢复仍需验证 |
| BestEffort | 不保证应用一致性 | 明示风险；不默认称为“安全恢复点” |

`save-all` 或暂停世界保存不自动涵盖所有插件文件、外部数据库和异步 IO。备份计划列出覆盖项、排除项和所有 ConsistencyGroup，不能一个指令后直接复制全部目录并声称完全一致。

### 13.2 Backup 流程

Plan → 容量/权限预检 → 获取必要协调权 → 进入静默/停服或快照边界 → 捕获数据 → 尽快恢复运行 → 校验清单与内容 → 提交 BackupRecord → 执行保留策略。

所有路径包含失败恢复；即使控制端断线，节点也须在限定时间内退出临时静默状态。保存数据先写临时对象，验证完才发布可恢复清单。备份元数据和数据引用共同决定 GC；部分上传不能显示为完成。

备份可以在同机，灾难恢复要求独立故障域；单份本地副本不宣称可抵御磁盘损坏。敏感备份需要独立加密/密钥恢复策略，密钥不能只存在被备份的同一磁盘上。

### 13.3 Restore 流程

验证 manifest/内容/版本与目标资源 → 持久化 MaintenanceIntent 阻止自动重启 → Drain/Stop → 确认旧写入者停止 → 恢复到新目录或新卷 → 校验 → 准备切换记录 → 切换受管位置引用 → 启动验证 → 提交结果。

跨磁盘移动、Windows 打开的文件、两次目录 rename 不能统一声称一个原子事务。以可恢复日志和明确提交点实现崩溃恢复；同卷单次原子操作只作为平台优化。失败保留原数据，禁止在验证前删除旧卷。

恢复旧世界会回退玩家进度，必须明示；外部数据库若不在同一恢复计划中，不能自动声称数据关系一致。

### 13.4 单机与跨节点迁移

持久世界默认冷迁移：关闭新准入、优雅停止、确认写权限解除、复制/验证、赋予新放置、启动检查、更新路由。增量预复制可降低停机，但最后一致性窗口仍要由 Provider 定义。

控制代次只阻止旧管理命令，不会神奇阻止已有 JVM 写本地文件。对失联节点，必须通过已验证执行隔离、存储访问撤销或机器级隔离证明旧写入者不能继续，才能在其他节点启用独占可写副本。做不到则 RequiresOperator，不“自动修复”为双写。

---

## 14. Controller、Reconciler 与控制权

### 14.1 协调循环

命令写入期望状态；Reconciler 根据 spec 与实际事实推进。使用变更触发的有界工作队列加低频修复扫描，不每毫秒全表扫描。每个目标有退避、公平性和正在处理标记。

不能让命令处理器和协调器分别各启动一次。业务命令可以触发立即协调，但进程效果始终通过同一执行计划和去重路径。

### 14.2 单控制者到 HA

Alpha：单 Controller 写入权威库。后续可多 API 副本，但 Workload/Network 分区在一个 owner epoch 内只有一个协调者。使用持久事务、并发版本与执行端代次检查共同约束。

数据库 advisory lock 是应用约定，不自动约束远端系统；会话级和事务级锁生命周期也不同。不能只持有一个 PostgreSQL lock 就宣称已经解决节点分区或世界 fencing。[R12]

数据库不可写/失去协调权时不发起新的编排。Node 用最后有效授权维持已有运行；长期离线行为由资源策略决定。跨地域初期采用独立控制域或明确单主，不提供未经验证的全球强一致多主。

### 14.3 控制权转移

Standard 接入已有集群的转移流程：冻结被迁移资源的管理写入 → 导出身份、配置、操作和卷引用 → 在目标域检查冲突与范围 → 双方确认转移计划 → 原权威记录 relinquished → 新域接管新的 authority lineage → 本地入口改为客户端。

不是简单比较两个域内的整数 epoch；转移要有显式的跨域授权和接收回执。无法联络旧控制域时，强制接管必须单独授权、核实执行状态与卷隔离，并留下审计。备份恢复出来的旧 Controller 默认只读/恢复模式，不能带着旧权威直接重新连接节点。

---

## 15. Placement、Fleet 与 Sessions

### 15.1 Placement 两阶段

候选过滤：控制域、节点可用性、OS/架构、Provider/Runtime、磁盘、端口范围、卷位置、网络域、保护能力、标签、隔离等级。

候选评分：预留后的容量、故障域分散、数据局部性、碎片、预热成本。结果记录选择理由，不输出不可解释的“最优节点”。

控制侧事务预留逻辑资源后，Node 再次检查本机实际条件并提交接收回执；拒绝则释放/重排。容量视图陈旧时不能仅凭旧状态硬派发。Xmx、堆外开销、进程 RSS、磁盘 IO 和 JVM 总内存分别处理。

### 15.2 Fleet 容量

Fleet 输入不是单纯在线人数，而是 Ready/Reserved/Occupied/Draining/Unhealthy、合规排队需求、启动时间和节点实际资源。原始连接攻击量不作为扩容需求。

建议初期只支持固定实例数和固定 Ready 余量；之后增加比例缓冲、滞回、冷却、最大增减速率、实例启动预算和容量上限。无法取得可靠占用数据时禁用危险自动缩容。

临时实例缩容：关闭新分配 → 等待已确认会话结束 → 停止 → 按数据策略回收。超时强制结束属于独立授权策略。持久 Singleton 不通过 Fleet 自动复制同一写卷扩容。

### 15.3 对局预留

Sessions 的权威容量状态与节点资源不同。预留事务对实例可用容量执行 CAS/条件更新，并创建不可重复 ReservationId。只读镜像可用于筛选，最终占用不能基于缓存独立决定。

`Requested → Reserved → Confirmed → Active → Completed/Released`。Reserved 未确认可按期限释放；Active 不因为普通预留计时器过期就变成空闲。服务端/桥接用会话身份回报，与 RunId 绑定。

分配回执可重复查询；已确认会话不得被重试分配到两个实例。匹配、队伍、经济和胜负由服主游戏业务实现，NexaSM 只提供容量接口。

---

## 16. 代理网络与服务发现

NexaSM 管理 Velocity 等真实代理的进程、配置、后端发现与有序路由变更，不自己在 Controller 转发 Minecraft 数据。

ServiceEndpoint 包含实例/Run 身份、地址范围、端口、适用协议、就绪条件和有效期。区分公网玩家入口、代理回源地址、Node 管理地址。显示 localhost 仅表示同机，不能作为远程节点地址。

路由新增只面向 Ready/AdmissionReady；排空先撤销新分配，再结束已建立会话。代理崩溃时，不承诺已建立 TCP 会话透明迁移。路由更新走版本化计划，部分代理未应用时报告部分生效。

Velocity 官方强调 modern forwarding 不是防火墙替代品。部署向导需要生成后端隔离建议/计划并验证实际可达性；“已写入配置”不等于“隔离已验证”。不同 Minecraft/加载器的转发模式通过兼容矩阵确定。[R13]

---

## 17. 管理 API 与节点协议

### 17.1 边界选择

| 边界 | 设计 |
|---|---|
| 同进程 S | XSR 类型化本地路由，不强制网络编码 |
| Desktop ↔ 本地后台 | 认证的 named pipe/Unix socket 或经评审的 loopback API；不匿名 |
| Web/CLI ↔ Controller | HTTPS 请求 API + 受权状态订阅；外部 JSON 可以使用 |
| Controller ↔ Node | 独立管理协议、双向认证、生成式二进制消息；传输可适配 |
| Node ↔ Supervisor | 最小本机执行协议，固定操作集合、ACL 与会话校验 |
| Host ↔ 插件 Sidecar | 既有 XSR Sidecar 边界，不直接把本地端口暴露公网 |

建议 Node 使用主动出站连接，减少必须开放入站端口的场景。网络层选型在原型中比较 AOT 兼容的实现，冻结 schema 后再锁定库版本；不重新发明 TCP 加密。

### 17.2 公开 API 草案

| 方法/路径 | 行为 |
|---|---|
| `GET /api/v1/capabilities` | 实际支持能力，不返回静态营销清单 |
| `GET /api/v1/networks` | 有权限的 Network 分页 |
| `POST /api/v1/workloads` | 创建，返回资源/Operation |
| `PATCH /api/v1/workloads/{id}` | If-Match/CAS 更新 |
| `POST /api/v1/instances/{id}/actions/start` | 幂等启动 |
| `POST /api/v1/instances/{id}/actions/stop` | 正常停止；强杀另设权限/动作 |
| `POST /api/v1/instances/{id}/actions/restart` | 有序停止后新 Run |
| `POST /api/v1/instances/{id}/backups` | 提交备份计划 |
| `POST /api/v1/instances/{id}/restore-previews` | 预览，不写活动目录 |
| `POST /api/v1/instances/{id}/restores` | 使用绑定目标版本的确认提交 |
| `GET /api/v1/operations/{id}` | 读取持久操作 |
| `POST /api/v1/operations/{id}/cancel` | 请求取消，不承诺回滚 |
| `POST /api/v1/nodes/enrollment-intents` | 有范围/期限的一次性配对意图 |
| `POST /api/v1/subscriptions` | 建立有范围、有期限、有额度的订阅 |

写请求使用 Idempotency-Key；资源编辑使用 If-Match。202 只表示已持久化接受，成功状态以 Operation 为准。401/403/409/412/429/503 等映射稳定业务错误，禁止泄漏内部异常栈。

### 17.3 管理协议封套

```text
ProtocolVersion, SchemaVersion, MessageKind,
SessionId, StreamId, Sequence, CorrelationId,
PayloadLength, Payload
```

新增字段有未知字段跳过和预算；关键未知能力不静默忽略。会话短数字 ID 不持久化成业务身份。版本区分产品、包、协议、数据结构和 Provider；能连上、能读取、能执行某操作分别协商。

控制命令、状态、日志、大文件和指标采用独立预算/优先级，必要时独立连接。原始 TCP 有序流仍会队头阻塞，不能只给消息分类就承诺带宽隔离。大文件尽量使用单独受权、限流的数据通道。

### 17.4 订阅恢复

订阅表达资源集合、过滤器、字段集、游标与最大速率。服务端始终检查作用域，避免跨 Network 状态泄漏。Snapshot 完成后发送有匹配基准的 Delta；缺口/重启切换 epoch 后重新同步。

UI 不每帧轮询 HTTP；查询和拉取发生在呈现适配生命周期中。列表只订阅必要摘要，详情再订阅日志和细粒度指标。撤销权限后终止关联订阅并清理本地敏感镜像。

---

## 18. 身份、安全与权限

### 18.1 本地与远程

本地首启通过 OS 身份/受限 IPC 建立所有者关系，不使用固定默认密码。远程启用要求认证、TLS、Origin/CSRF 策略、速率限制和审计。CORS 不是认证机制；localhost 端口也不能无鉴权接受网页请求。

浏览器不在 URL 中携带长期访问密钥；会话权限按 API 和订阅分别验证。账号可以是本地身份或外部身份提供者，Nexa 账号可选。管理凭据与 Minecraft 玩家凭据完全分开。

Node 通过一次性注册意图 + 管理端确认获得 scoped machine identity；后续轮换凭据，可吊销，记录绑定的控制域。远程自动接受未知节点禁止作为默认值。

### 18.2 初始角色模板

| 角色 | 权限基线 |
|---|---|
| Viewer | 读取指定资源和非敏感状态 |
| Operator | 正常启停、经允许的游戏管理动作 |
| Maintainer | 安装、配置、备份恢复等高风险维护 |
| NetworkAdmin | 网络范围资源/权限管理 |
| DomainOwner | 控制域、节点信任与全局安全设置 |
| AutomationIdentity | 最小资源范围与动作集合、可撤销凭据 |

模板可细分，但不把“可访问控制台”默认授予所有 Operator。ConsoleRaw、InstallExecutable、EditStartup、ReadSecrets、ForceStop、DeleteVolume、TransferAuthority 是单独权限。

插件/JAR 和启动参数可执行任意代码；文件写权限也可能间接取得此能力。策略必须承认这些等价关系，不能声称允许写 plugins 目录却仍能阻止代码执行。

### 18.3 机密管理

存储 SecretRef 与版本，在执行边界按用途解析。API 状态只返回 redacted/present 信息，不回显值；审计不记录原始密码。密钥轮换需要 Provider 支持的应用方式，必要时计划维护重启。

节点 root/admin 或被授权原始 OS 操作者处于本控制域的高信任边界；不能保证对抗已经完全控制机器的管理员。Sidecar 进程隔离也不是完整恶意代码沙箱。

### 18.4 网络访问与下载

来源可访问范围必须受策略限制。下载/URL 导入检查协议、重定向、解析结果和私网目的地址，防止通过远程 URL 功能访问管理端内部服务；合法私服 Maven/对象存储通过显式管理员允许。

归档校验与解压预算、文件权限、执行身份和实际网络出站都要审计。NexaSM 不自动关闭平台安全措施，不自动开放公网 RCON。

---

## 19. 持久化模型与数据库

### 19.1 存储模式

Standard：本机事务数据库（建议 SQLite）承载控制数据；Node 与 Supervisor 分别拥有自己的执行账本，不能让多个服务随意改写对方私有表。逻辑分离可以先按同一进程的模块实现，但远端不能共享打开本地 DB。

Controller 集群：建议 PostgreSQL 存储权威资源、操作、Outbox、容量和分区所有权；大文件/备份不塞进关系表，日志和时序数据采用独立可替换后端。

SQLite WAL 不适合通过网络文件系统提供多主共享；运行中数据库备份应使用受支持的备份流程，不能只复制主 `.db`。官方文档明确说明 WAL 的同机约束和 Online Backup API。[R09,R10]

### 19.2 逻辑表清单

```text
control_domains, networks, nodes, node_credentials,
workloads, template_revisions, config_revisions,
instances, runs, placements, resource_reservations,
volumes, volume_attachments, backups, backup_items,
artifacts, artifact_sources, secret_references,
operations, operation_steps, idempotency_records,
outbox, node_inbox, schedules, schedule_occurrences,
fleet_capacity, session_reservations,
rollouts, rollout_targets, protection_bindings,
role_bindings, audit_records, ownership_partitions
```

关键约束：同实例活动 Run 唯一；独占卷活动写入关联唯一；幂等 key 组合唯一；预留容量条件更新；节点注册凭据唯一且可吊销。数据库约束只保证控制记录正确，不能替代节点实际进程/存储隔离。

不默认采用全量 Event Sourcing。资源表 + 有界操作账本 + Outbox + 审计足以开始；历史事件不能成为恢复当前事实的唯一依赖。

### 19.3 schema 演进

每个模块独立迁移版本，控制总依赖顺序。先支持扩展、再切换、再收缩；混合版本部署明确兼容窗口。升级前备份并检查可回滚性；不可逆数据迁移禁止自动回滚旧二进制。

SQLite → PostgreSQL 使用导出/验证/冻结写入/导入/重新建立信任与所有权的迁移流程；不把复制文件称作数据库迁移。测试恢复数据库后节点与真实 Run 的重新对账。

### 19.4 保留与 GC

Operation/审计/日志/备份/Artifact 的保留策略分别配置。删除引用前完成权限与保留检查。Secret 与证书吊销记录不能因为普通日志清理丢失。安全更新可设最低可接受版本，普通回滚不能绕过吊销。


---

## 20. 日志、指标、计划任务与通知

### 20.1 日志

Supervisor 持续读取 stdout/stderr，按 Run 和序号分段，防止未读取管道反压游戏进程。内存队列、单条长度、磁盘空间和保留期限有界。丢弃可舍弃日志时记录明确 Gap/Truncated 事件与计数，不假装完整。

RawConsole 与结构化运行事实分开。日志中的 `Done` 可作为 Provider 的低保证提示，但不独自构成完整安全/保护就绪。第三方插件日志不作为可信授权消息。控制字符、超长行、格式码和用户内容在展示时转义，禁止日志注入 HTML 或终端命令。

### 20.2 指标

每个指标包含来源、采集时间、单位、采样窗口和 Availability。分别呈现进程 RSS、堆使用、堆上限、系统可用内存、节点预留、磁盘空间和已分配配额。

CPU 显示明确分母：单核等效、整机占比、配额占比分开，不混淆 100%。TPS/MSPT/玩家数只有获得可靠适配器数据才显示精确值；日志推断必须标成估计。监控采样消耗也纳入节点预算。

建议先实现本地指标与导出端口，外部可观测后端作为可选适配。高基数玩家/命令 ID 不默认作为时间序列标签；指标、审计、日志、Trace 采取不同保留策略。

### 20.3 调度任务

计划任务通过相同 Command/Operation 路径执行，不成为绕过权限的第二入口。记录时区、调度表达式、下一次 UTC 时间、DST 重复/缺失处理、错过任务策略、并发策略、执行身份和暂停状态。

每次触发生成 `OccurrenceId=(ScheduleId, plannedOccurrence)` 并去重。明确 Skip/RunOnce/CatchUpBounded，以及 ForbidOverlap/QueueBounded。SM 重启后不得把一个月错过的重启任务全部立即补执行。

### 20.4 通知

Webhook/邮件/其他渠道通过有界投递任务执行，失败不能阻塞游戏。事件做去重、聚合和冷却，通知内容默认脱敏。Webhook 有来源签名/密钥轮换和网络目的限制；重试不保证接收端 Exactly Once，接收端可使用 EventId 去重。

---

## 21. 扩展体系与 Nexa 产品集成

### 21.1 共享包边界

共享 XSR、Platform、Renderer、NXML/NSS、可复用下载/校验/归档与 Java 元数据能力；不把整个 NexaCL 启动协调器、账号流程或桌面项目复制进 SM。

共享包独立版本化与锁定依赖，SM 与 CL 可不同节奏采用。源代码暂时所在仓库不决定消费者的依赖方式；禁止工作站相对路径跨仓库引用、复制共享源码和引入完整产品子模块。

### 21.2 扩展权限

内置 Provider 经发布验证；第三方动态扩展走明确 Sidecar/外部 Provider 边界，不动态加载进 AOT 核心。注册能力需声明作用域和权限，不自动获得状态写入权。

一个扩展可能因插件安装、控制台或文件权限具有代码执行能力，必须向管理员明确。共享 CoreCLR Sidecar 中的程序集隔离不是恶意代码安全边界；高风险扩展可分进程和 OS 账户，仍需要实测与权限控制。

### 21.3 NexaAC 接口

SM 只拥有 `ProtectionBinding`：安装包版本、策略修订引用、要求能力、部署计划和实际就绪回报。AC 拥有检测、证据验证和处罚决策。

SM 显示 `Installed/Compatible/PolicyApplied/ProtectionReady` 四个独立事实。仅安装 JAR 或发出配置命令不能宣布保护已生效。要求 AC 的 Workload 只有得到当前 Run、策略和适配器对应的就绪结果，才满足 AdmissionReady。

SO/SC、ClientPolicy、KernelPolicy 与六级判罚均由 AC profile 定义；SM 可以呈现和编辑被授权的 AC 配置，但不得在自己的调度器复制第二套判罚规则。Ultimate/Extreme/High/Medium/Low/Quiet 作为外部契约值使用，不能影响 SM 基础管理认证或资源预算。

没有安装 AC 时，普通 Workload 仍可运行；要求 AC 的受保护玩法缺少能力时阻止新准入，按声明策略处理已有会话。SM 控制端断线不关闭已有 AC 本地保护。

### 21.4 NexaCL 接口

通过发布的 SM Management.Client/公开协议提供已授权的发现、版本要求、客户端配置建议和连接信息。账号 token、服务器私钥、管理员凭据不随“分享服务器”发送。

玩家客户端兼容清单与服务器完整文件清单分开，避免把插件、世界和私密配置当整合包发布。NexaCL 只是一个管理/玩家入口，不成为 SM 必选安装项或可信玩家证明。

---

## 22. Desktop 与 Web 呈现设计

### 22.1 页面结构

| 页面 | 主要状态 | 主要命令 |
|---|---|---|
| 首次设置 | 本地权限、数据目录、后台模式、网络暴露 | Initialize、EnableService |
| 服务器列表 | 实例摘要、Run、可用性、PendingChanges | Create、Import、Start、Stop |
| 实例概览 | 四级就绪、资源、当前操作、连接信息 | Start/Stop/Restart、打开诊断 |
| 控制台 | 有界日志流、丢失范围、权限 | typed action、受限 RawConsole |
| 配置/内容 | 有效配置、来源、磁盘漂移、ArtifactSet | Preview、Save、PlanUpdate |
| 世界/备份 | 卷、备份一致性、覆盖范围、恢复点 | Backup、RestorePreview、Restore |
| Network | 路由、Workload、聚合故障、发布状态 | Deploy、Drain、Scale |
| Nodes | 实际能力、容量、控制域、节点健康 | Pair、Drain、Rotate、Revoke |
| 操作中心 | 可恢复操作与步骤、等待原因 | Cancel、RetrySafe、Resolve |
| 安全与 AC | 角色、防护引用、实际覆盖、审计 | 授权动作与 AC 策略操作 |

### 22.2 呈现边界

Desktop 采用现有 `Nexa.Xsr.Renderer`、外置 NXML/NSS 与动态片段，产品 Presentation 负责数据投影和意图适配，不创建第二份权威业务状态。[R05]

Web 使用浏览器本地镜像和共享语义契约；页面组件实现可独立。不能假设 NXML 已经是 HTML，也不把实现一个完整跨浏览器渲染引擎作为 Alpha 前置。

输入草稿与已保存配置分开；切换页面时取消会话级 Query，不取消已提交 Operation。异步结果必须检查资源身份、原文版本和页面 generation，防止旧结果写入新实例页面。

### 22.3 性能与可访问性

列表虚拟化、按可见范围建立行和媒体资源，服务端分页；不能订阅全网所有日志。页面显示陈旧标记和最后观测时间。支持键盘、焦点顺序、读屏语义和清晰的破坏性动作确认。

权限导致不可操作时显示授权原因；节点能力不足显示能力原因；等待操作锁显示冲突操作身份。不能统一用一个灰色按钮隐去全部原因。

---

## 23. 安装、更新与发布

### 23.1 产品安装

发行 manifest 明确 Product、Role、Version、RID、依赖包、文件摘要、协议范围和数据 schema 范围。包来源与验证策略固定；缓存内容哈希不代替发布者信任验证。

Standard 安装器提供桌面+后台、无头一体化、仅管理客户端选项。Node/Controller 安装不自动加入不明控制域。系统服务安装和防火墙修改单独展示所需权限。

### 23.2 自更新

Desktop 可独立更新；Node 更新先停止新管理写操作、完成或安全暂停临界步骤、验证 Supervisor 仍持有 Run、切换 Node 二进制、重连核实。不能把关闭 Node 服务顺带杀死所有 JVM 作为正常更新。

Supervisor 更新影响其拥有的控制通道，默认安排维护或仅对新 Run 使用新监管版本；只有经过验证的交接协议才能宣传不中断更新。协议兼容允许新旧版本在限定窗口共存。

Controller schema 升级先检查兼容窗口、备份和迁移锁。健康检查失败可以回退程序，但不能把已不可逆迁移的数据直接交给旧程序。

### 23.3 服务端内容发布

RolloutPlan 固定模板、目标集、maxUnavailable、maxSurge、Drain 条件、健康门槛、回滚边界和保护策略兼容要求。临时 Fleet 可灰度新实例；独占持久世界不能通过 maxSurge 创建第二个写入者。

灰度失败暂停后续目标，保留证据。回滚区分软件/config 版本回退、世界快照恢复以及外部状态恢复；后两者可能损失进度，需显式授权。

### 23.4 发布证据

每个发行版本附依赖锁、平台构建、AOT/trim 实际运行、协议兼容、Provider 矩阵、失败注入、升级/恢复测试、已知缺口与构建摘要。仅 CI 编译绿不代表跨平台 UI、真实开服或生产规模已验收。

---

## 24. 初始可配置预算与性能验证

本节数字是原型阶段建议值，不是 SLA 或性能结果。真实部署须按实例数、硬件和插件负载调优，关键安全上限不得由未授权节点自行扩大。

| 项目 | 初始建议 | 行为 |
|---|---|---|
| 普通管理命令 payload | 256 KiB | 超限拒绝；文件走独立通道 |
| 单个解析后控制帧 | 1 MiB | 先检查长度；快照分块 |
| API 列表 pageSize | 默认 100，最大 500 | 游标分页，返回总数可选 |
| 单节点同时启动 JVM | 默认 2 | 有界排队，允许管理员调节 |
| 单卷并发备份 | 默认 1 | 防止 IO 放大 |
| 日志单条展示 | 默认 64 KiB | 截断并保留计数；原始磁盘策略独立 |
| UI 日志可见缓存 | 每订阅 4 MiB 起 | 丢弃可舍弃记录必须报告 Gap |
| 节点 heartbeat | 初始 5 秒 | 仅活性信号，不证明业务健康 |
| 状态陈旧门槛 | 初始 15 秒 | 标 Stale，不自动判死或迁移世界 |
| 通信失联提示 | 初始 60 秒 | 标记 Unavailable；控制策略另定 |
| 管理队列与限流 | 按主体/资源/动作设有界配额 | 背压返回明确结果，不无限保存 |

每个超时归属于具体步骤；不能统一 30 秒结束所有模组服启动。启动 deadline 由 Provider 与实例配置声明，超时不自动意味着进程已退出。

### 24.1 基准场景

L0：单机 1 实例、无外部依赖；L1：单机约 10 实例验证状态与 IO 竞争；L2：3–5 个真实 Node 验证分区、升级和故障域；L3：模拟 1,000 个 Node 会话与 10,000 个资源状态验证控制平面，再逐步扩大真实规模。

L3 是测试负载，不是承诺可运行 10,000 个 Minecraft JVM。合成状态吞吐与真实游戏性能必须分开报告。

### 24.2 测量项

管理命令持久化接受延迟 p50/p95/p99、状态传播时延、队列长度/拒绝率、进程监管开销、闲置唤醒、稳态内存趋势、日志丢弃、备份吞吐、Restore 时间、控制端恢复时间。

性能结果记录 CPU/内存/OS/存储/网络、数据集、版本和实际采样窗口。没有报告时显示 Unmeasured。优先验证正确性和资源上限，再确定具体毫秒指标。

---

## 25. 故障矩阵与降级

| 故障 | 必须行为 | 禁止行为 |
|---|---|---|
| UI 退出 | Run 继续；下次重建镜像 | 自动取消所有后台操作 |
| Controller 断线 | Node 按有效策略维护本机；暂停未知授权的新编排 | 立即终止全部游戏 |
| Node 退出 | Supervisor 保管 Run 和控制通道；重连对账 | 仅凭进程名重新启动 |
| Supervisor 崩溃 | 明确实际进程和控制能力；报告控制通道丢失 | 宣称管道自动恢复 |
| JVM 退出 | 记录真实退出、按策略退避 | 无限即时重启 |
| 节点失联 | 状态陈旧/不可用；不新分配 | 当作世界写入者已死亡 |
| 数据库不可写 | 拒绝无法持久化的新变更；保持既有运行 | 返回虚假成功 |
| 日志磁盘满 | 有界丢弃/标 Gap、告警、不阻塞游戏管道 | 继续无限缓存 |
| 世界磁盘不足 | 阻止危险新写、告警、按明确策略停服 | 盲目清理未知世界文件 |
| 备份中断 | 清理临时状态、保留未完成标记、解除静默 | 发布完成恢复点 |
| 恢复中断 | 按提交日志选择旧/新位置或 NeedsAttention | 直接删除原目录 |
| 保护组件不足 | 对 required 玩法阻止新准入并说明原因 | 把安装成功当防护完成 |
| 控制域凭据吊销 | 终止受影响管理会话，按安全策略维护游戏 | 默认删除游戏数据 |
| 大量重连 | 有界注册/快照/重试，带抖动 | 所有节点瞬时全量无界同步 |

---

## 26. 测试与验收设计

### 26.1 测试分层

Domain 单元测试验证规则；Contract 测试验证消息兼容与错误语义；Architecture 测试验证引用/类型所有权；Integration 使用真实文件系统、数据库和进程；FaultInjection 在崩溃窗口执行故障；Provider 测试运行真实服务端；UI/AOT/trim 测试实际执行发行物。

使用可控 TestProcess 模拟慢启动、无输出、超长输出、拒绝停止、分叉进程和崩溃，不把全部失败测试压到真实 Minecraft。然后用固定真实版本回归验证 Provider 语义。

### 26.2 强制验收用例

| ID | 输入/故障 | 通过条件 |
|---|---|---|
| T-001 | 同幂等 key 并发提交 Start 100 次 | 一个逻辑 Operation、最多一个活动 Run |
| T-002 | 同 key 不同 payload | 拒绝；不修改原 Operation |
| T-003 | Start 接收后响应丢失 | 重试返回原操作，不新增进程 |
| T-004 | Supervisor spawn 前/后、回执前/后逐点崩溃 | 可核实或 NeedsAttention；不盲目双启 |
| T-005 | 手动 Stop 后出现崩溃恢复事件 | DesiredState 仍 Stopped，不重启 |
| T-006 | UI/Node 分别重启 | UI 不影响 Run；Node 重启由存活 Supervisor 维持 |
| T-007 | Supervisor 重启且通道无法恢复 | 明确 ControlChannelLost，不假报 Attach |
| T-008 | 恢复进行中 Reconciler 收到启动需求 | 互斥成立；不会启动半恢复世界 |
| T-009 | PID 复用或同名 Java | 不接管/不强杀不属于 Run 的进程 |
| T-010 | 端口预检后被占用 | 报启动 bind 冲突；释放本次资源 |
| T-011 | 高速 stdout 超过预算 | 游戏不被未读管道拖住；日志 Gap 可见 |
| T-012 | DB 写满 | 不发假 OperationReceipt；已有游戏按策略继续 |
| T-013 | 节点分区后请求持久迁移 | 没有真实隔离证据则阻止新写入者 |
| T-014 | 多调度者同时预留同一容量 | 条件更新只允许合法容量总和 |
| T-015 | 对局重复确认/释放 | 不重复占用、不释放其他会话 |
| T-016 | 活动会话预留计时器到期 | 不误转空闲 |
| T-017 | StateDelta 基准不匹配 | 重新快照；不猜测补丁 |
| T-018 | 旧 session/epoch 数据晚到 | 不恢复旧权威和旧可用性 |
| T-019 | ACL 撤销时订阅仍在线 | 停止推送并清理敏感镜像 |
| T-020 | ZIP 穿越、符号链接替换、超长文件 | 拒绝越界，原目录不变 |
| T-021 | 导入恶意脚本目录 | 扫描不执行任何代码 |
| T-022 | 配置外部修改与 UI 保存竞争 | 明确冲突，不静默覆盖 |
| T-023 | 停服/静默失败时备份 | 不冒充应用一致备份；解除临时状态 |
| T-024 | 恢复切换各提交点断电 | 旧数据保留，可确定提交结果或 NeedsAttention |
| T-025 | 从备份恢复旧 Controller | 不自动重新取得旧写权限 |
| T-026 | 提升保护策略但部分节点不兼容 | 准入屏障有效，未生效节点不接新玩法 |
| T-027 | 代理回源可公网绕过 | 安全预检失败，不显示隔离已验证 |
| T-028 | 时间回拨、DST 切换、计划任务补执行 | 单调超时正确，Occurrence 去重有效 |
| T-029 | 大量 Node 重连 | 有界握手与快照，停止命令仍可进入预算 |
| T-030 | AOT/trim 发布后执行 | 实际路由、配置、安装、启停、备份路径通过，不只编译 |
| T-031 | 游戏升级写入新世界格式后软件回滚 | 提示不可保证数据回滚，不直接启动旧版写原卷 |
| T-032 | 原始控制台命令发出后断线 | 显示 OutcomeUnknown，不自动重放 |
| T-033 | 凭据轮换/吊销与旧连接 | 作用域和存续策略符合契约 |
| T-034 | 页面切换后旧 Query 完成 | 不写入新实例草稿或页面 |
| T-035 | AC 未安装但普通 workload 启动 | 正常独立运行；required workload 则明确拒绝准入 |

### 26.3 稳定性门槛

先完成短故障循环，再做 24 小时/72 小时等实际长测。每次记录运行环境、工作负载、原始指标和失败，不能用测试计划代替已完成的测试证据。无界增长、死锁、双启动和错误删数据属于阻断问题。

---

## 27. 开发里程碑与垂直切片

阶段名称是任务分组，不自动决定产品版本号。首先完善单机真实闭环，后续模块按需要创建，不提前制造几十个空工程。

| 阶段 | 交付 | 完成条件 |
|---|---|---|
| M0 架构基线 | ADR、Contracts、最小工程、引用门禁、TestProcess | 领域身份、Operation、状态语义可测试；CI 实际运行 |
| M1 执行闭环 | Supervisor + NodeLifecycle + CLI | Start/Stop/Observe/故障恢复；T-001 至 T-010 |
| M2 Standard 桌面 Alpha | 本地控制、NXML 页面、创建/导入、版本解析 | 窗口关闭不停服；真实首组 Provider 安装启动；状态可信 |
| M3 数据安全 | 配置漂移、备份/恢复、内容更新、计划任务 | 恢复演练、数据不丢、步骤可恢复 |
| M4 远程与 Web | 配对、认证 API、Web、节点状态镜像 | 真实 3–5 Node、权限、断线重连、远程升级 |
| M5 网络与 Fleet | Placement、代理发现、容量预留、排空、灰度 | 分区/并发/占用测试和实际小游戏桥接 |
| M6 生产加强 | HA、容量测试、发布与恢复工具、完整兼容矩阵 | 目标规模实测、长期运行、安全评审 |

AC 安装/能力接口可在 M2 预留，实际保护就绪验证随 NexaAC 交付接入；不得等待 AC 驱动完成才发布 SM。四加载器适配可并行，但每个适配必须有独立版本矩阵和真实服务器验证。

### 27.1 首批可直接拆分的任务

| Task | 内容 | 依赖 | 关键证据 |
|---|---|---|---|
| SM-001 | 架构锁和依赖图门禁 | 无 | 禁止引用测试、非空工程检查 |
| SM-002 | 强类型 ID、spec/status、错误模型 | SM-001 | 序列化、CAS、身份测试 |
| SM-003 | Operation、幂等、Outbox/InBox 契约 | SM-002 | 并发重试与故障测试 |
| SM-004 | TestProcess 与 FaultInjection Harness | SM-002 | 可复现崩溃/日志/退出行为 |
| SM-005 | 平台进程与最小 Supervisor | SM-003/004 | 启停、身份验证、管道与账本测试 |
| SM-006 | NodeLifecycle 和 Reconciler | SM-005 | 不双启、手动停止不重启 |
| SM-007 | CLI 与本地认证接口 | SM-006 | 真实命令和稳定退出码 |
| SM-008 | Provider 元数据/Java 解析/执行计划 | SM-002 | 固定版本与 argv/参数文件验证 |
| SM-009 | 安装/导入暂存提交 | SM-008 | 安全扫描、归档预算、失败清理 |
| SM-010 | Standard 组合根和 Desktop 基础页 | SM-007/009 | NXML 意图→Operation→状态闭环 |
| SM-011 | 配置来源/磁盘漂移和内容更新 | SM-009/010 | 不静默覆盖，运行文件保护 |
| SM-012 | Volume 与离线备份/恢复 | SM-006/011 | 崩溃点恢复、原世界保留 |
| SM-013 | 远程配对/API/订阅协议 | SM-003/007 | 作用域、断线、旧 epoch 拒绝 |
| SM-014 | Web 投影与远程 Node 发行 | SM-013 | 一致语义与权限测试 |
| SM-015 | Node 放置/预留与多节点故障 | SM-013 | 真实分区、容量与写卷隔离 |
| SM-016 | AC 管理适配和能力 UI | SM-010/013 | 安装与实际生效分离 |
| SM-017 | Fleet/Sessions/代理路由 | SM-015 | 对局原子预留、排空、过期语义 |
| SM-018 | 发布/HA/容量与长期证据 | SM-017 | 升降级和目标规模测试 |

每个任务需交付：契约、业务、持久化、组合注册、一个真实消费者、失败路径、测试和文档。只提交接口或空按钮不计完成。

---

## 28. 仓库结构与 CI 门禁

```text
NexaSM/
  docs/
    architecture.md
    decisions/
    contracts/
    verification/
  src/
    NexaSM.Domain/
    NexaSM.Contracts/                  初期可共用，稳定后按模块拆分
    NexaSM.Services/                   初期模块化组织，不是全局万能服务
    NexaSM.Services.Composition/
    NexaSM.Management.Protocol/
    NexaSM.Management.Client/
    NexaSM.Platform/
    NexaSM.Presentation/
    Hosts/
      NexaSM.Desktop/
      NexaSM.Daemon/
      NexaSM.Node/
      NexaSM.Controller/              到实际需要独立部署时创建
      NexaSM.Supervisor/
      NexaSM.Cli/
  Ui/                                 NXML/NSS 资产
  web/                                远程 Web 阶段创建
  tests/
    Domain/
    Contracts/
    Architecture/
    Integration/
    Recovery/
    Providers/
    Ui/
    Scale/
  tools/
    TestProcess/
    FaultHarness/
```

只在任务需要时创建项目；工程角色与允许引用清单显式登记。Domain 不依赖 Runtime/Platform 实现；Contracts 不依赖 Services；Services 不依赖 Renderer；Composition 是实现绑定边界；Desktop 业务 UI 不能引用具体服务实现。名称不能作为绕过规则的依据。[R04]

CI 层级：格式/静态检查 → 构建 → 领域/契约 → 依赖图/符号门禁 → TestProcess 集成 → AOT/trim 发行物实际运行 → 各平台 Provider/桌面测试 → 定期长测/容量。

跨仓库共享包引用锁定发布版本或明确预发布版本，不引用开发者磁盘路径。共享包的 API baseline 更新不等于接受破坏性变更；需要兼容性决定和消费者测试。

---

## 29. ADR 清单与待验证问题

建议随开发保存以下 ADR：

| ADR | 内容 | 状态 |
|---|---|---|
| SM-ADR-001 | 独立产品仓库、共享版本化基础包 | 已确定方向 |
| SM-ADR-002 | Node / Controller / Standard 角色 | 已确定方向 |
| SM-ADR-003 | XSR 与组合根依赖边界 | 已确定方向；实施图待锁定 |
| SM-ADR-004 | Supervisor 与 Node 分离 | 本稿建议；每平台生命周期需验证 |
| SM-ADR-005 | Workload/Instance/Run 身份模型 | 本稿建议的细化契约 |
| SM-ADR-006 | 持久 Operation 与去重、非 Exactly Once 承诺 | 本稿建议 |
| SM-ADR-007 | 本地 SQLite / 集群 PostgreSQL | 本稿建议；数据库包与 AOT 待验证 |
| SM-ADR-008 | 持久卷迁移必须真实 fencing | 本稿建议的硬约束 |
| SM-ADR-009 | 管理协议与 Sidecar 协议独立 | 本稿建议 |
| SM-ADR-010 | AC 是独立能力，不进入 SM 热路径 | 已确定方向 |
| SM-ADR-011 | 运行时安装与配置的声明式计划 | 本稿建议 |
| SM-ADR-012 | 不承诺跨平台同等限制和未知就绪 | 本稿建议 |

需要原型验证而非继续空谈的事项：本机监管进程在 Windows/Linux/macOS 安装模式下的真实生存边界；Supervisor 崩溃后的可提供控制级别；Provider 的可靠就绪与静默协议；选定数据库/传输库的 AOT/trim 支持；原地导入与跨卷恢复的可恢复提交；真实模组组合和大规模状态订阅成本。

这些项目可以在当前设计基础上推进，不需要把所有未来算法和库版本一次冻结。

---

## 30. 示例文件与验证边界

配套 `schemas/workload.schema.json` 与 `schemas/command-envelope.schema.json` 展示资源/命令外形；`examples/` 提供 Singleton、Fleet 和启动命令示例。

Schema 校验只能检查声明形状。配套示例验证器补充少量语义条件（例如最小副本不能大于最大副本、内存请求与堆预算关系），不替代服务端授权、资源存在性、卷隔离、Provider 兼容性或进程安全验证。

示例不是已经发布的 API，不应把字段编号或 SDK 签名当作 ABI 冻结。对外发布协议前必须完成版本协商、错误契约、未知字段、大小预算和跨语言测试。

---

## 31. 参考基线与核实资料

以下引用用于区分现有 XSR/平台事实与本稿设计。URL 仅作为资料定位，不代表本稿执行过所述产品操作。

- **R01** NexaCL `docs/xsr/architecture.md`，分支 `refactor/xsr`，读取的 blob `2ee50043fc69e037715506363fda16014695dbac`。定义 X/S/R、模块化组合和所有权；后续 NXML 文档覆盖旧 UI.Next/PXML 命名。
  `https://github.com/Nexa-MC/PCL-N/blob/refactor/xsr/docs/xsr/architecture.md`
- **R02** NexaCL `docs/xsr/service-model.md`，blob `f835b7c01460226186982f9304d9d20fb9f404b5`。命令接受/完成、异步 Query、事件和服务边界。
  `https://github.com/Nexa-MC/PCL-N/blob/refactor/xsr/docs/xsr/service-model.md`
- **R03** NexaCL `docs/xsr/state-model.md`，blob `993d96ee2e56df89f841bda8cc9291bcc57ab95c`。状态所有权、修订、快照与可用性。
  `https://github.com/Nexa-MC/PCL-N/blob/refactor/xsr/docs/xsr/state-model.md`
- **R04** NexaCL `docs/xsr/dependency-rules.md`，blob `e155b9b5562a0bfe8b6f1ad80718eec0c47c434c`。允许引用与架构门禁。
  `https://github.com/Nexa-MC/PCL-N/blob/refactor/xsr/docs/xsr/dependency-rules.md`
- **R05** NexaCL `docs/xsr/nxml-renderer.md`，blob `5c7a74fb09b0d15e58182473833854ee1df2ef86`。Renderer、外部 NXML/NSS 与状态/意图边界。
  `https://github.com/Nexa-MC/PCL-N/blob/refactor/xsr/docs/xsr/nxml-renderer.md`
- **R06** Microsoft Learn, Job Objects。核实 Windows Job 进程组和 kill-on-close 语义。
  `https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects`
- **R07** systemd 官方 `systemd.kill`。核实服务停止与控制组进程终止语义。
  `https://www.freedesktop.org/software/systemd/man/latest/systemd.kill.html`
- **R08** Microsoft Learn, ASP.NET Core support for Native AOT，.NET 10 文档视图。
  `https://learn.microsoft.com/en-us/aspnet/core/fundamentals/native-aot?view=aspnetcore-10.0`
- **R09** SQLite, Write-Ahead Logging。核实 WAL 同机与网络文件系统限制。
  `https://www.sqlite.org/wal.html`
- **R10** SQLite, Online Backup API。核实运行中数据库备份方法。
  `https://www.sqlite.org/backup.html`
- **R11** NeoForged, Installing a NeoForge Server。核实安装器、参数文件及启动流程差异。
  `https://docs.neoforged.net/user/docs/server/`
- **R12** PostgreSQL, Explicit Locking。核实应用 advisory lock 的约定性质与生命周期。
  `https://www.postgresql.org/docs/16/explicit-locking.html`
- **R13** PaperMC, Securing your servers。核实代理转发与后端访问隔离的边界。
  `https://docs.papermc.io/velocity/security/`

---

## 32. 开发启动结论

第一条交付链应为：

**类型化资源与 Operation → TestProcess → Supervisor → NodeLifecycle → CLI → 真实 Provider → Standard Desktop → 备份/恢复。**

先证明“不会重复开服、不会错误接管、关闭 UI 不停服、故障可恢复、世界数据不被破坏”，再把同一条链路扩展到远程、Fleet 和 HA。NexaAC 集成、Web UI 和大规模调度都应成为已验证核心之上的能力，而不是绕过核心的新实现。
