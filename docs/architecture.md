# 协作架构基线

NexaSM 管理部署与运行；NexaAC 负责反作弊与入口安全；NexaCL 提供玩家侧集成。大型网络是设计目标，尚无容量验证。

## XSR 与边界

- X（Execution and Exchange Runtime）：路由、状态、事件、生命周期、通信、资源预算、诊断，不拥有 Minecraft 业务规则。
- S（Business Services）：实例、备份、部署、调度；以及准入、披露、检测、处置。
- R（Semantic Renderer）：读取本地状态、呈现页面、产生意图，不直接操作 Node、JVM 或处罚。
- 共享能力通过版本化 Nexa XSR／Platform 包引入，不直接引用 NexaCL 应用实现。
- 桌面沿用 Nexa.Xsr.Renderer、NXML／NSS；Web 共享契约与操作语义，不要求首发复用全部绘制代码。

## 运行链路与资源

管理：Desktop / Web / CLI → Controller → Node → Supervisor → Minecraft / Proxy。
游戏：玩家 → NexaAC.Edge → Minecraft + NexaAC.Server。
SC：玩家组件 → 证据验证 → 服务端准入与处置。
游戏热路径不等待远程管理服务；包处理、信息裁剪、即时规则保护留在代理或游戏 JVM，管理侧接收有界批量数据。

资源层次：ControlDomain → Network → Workload → Instance → Run。
Instance 是持久身份，Run 是一次执行。模板修订、放置、世界卷、操作、对局容量预留独立建模。
小服界面隐藏层级，底层不另造模型。持久世界与临时对局采用不同恢复策略；复制 JVM 不能安全扩容同一世界。

## 实施顺序与验收

1. Standard 单机桌面闭环。
2. SO 入口、登录洪泛与协议预算。
3. SO 披露、规则检测与证据。
4. SC 能力协商、会话绑定、用户态协作。
5. 多节点、Fleet、灰度与高可用。
6. 经验证的平台证明和必要的最小内核辅助。

首要验收：重复命令不重复启动、关闭界面不停服、失联不误报、持久世界不双写、攻击下资源有界、裁剪不破坏同步、误报误罚可追溯。
这些是待实现的验收目标，不是已经通过的测试。
