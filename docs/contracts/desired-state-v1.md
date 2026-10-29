# M0 DesiredStateCommand 内部契约

状态：内部 v1 原型，非已冻结的公共 API。实现是本地测试消费者，不提供匿名或远程管理入口。

## 消费流程

CommandCodec 校验输入 → 可信宿主鉴权并建立 AuthorizedScope → OperationService → SQLite 事务接受 → Pending 回执。业务目标由 InstanceId 指定，Actor 不从 JSON 接受。当前没有执行投递器，Pending 不表示游戏启动。

可运行样例：[启动意图](../../examples/desired-state-start.json)、[停止意图](../../examples/desired-state-stop.json)。测试实际读取样例，经 CommandCodec 和 OperationService 写入数据库；停止后重复原启动请求仍返回原回执，不将意图改回 Running。

## 字段与边界

CommandId 为非空 GUID；IdempotencyKey 为非空白且不超过 128 个 .NET 字符的字符串；Target.Value 为非空 GUID；ExpectedVersion.Value 为非负 Int64，必须显式提供；DesiredState 为 Running/Stopped（当前生成式枚举反序列化也接受对应整数 1/0）；SchemaVersion 必须为 1。所有顶层字段必填。额外未知字段可跳过，未知 schema/枚举拒绝。

报文上限 256 KiB，非法 JSON/字段返回 InvalidCommand，超限 PayloadTooLarge，错误 schema UnsupportedSchema。版本要求是 CAS，不可通过空对象让默认值 0 充当调用者提供的 token。

同域、同主体、同命令类别、同目标、同 key：相同 schema/CAS/desired 返回原回执；不同返回 IdempotencyConflict。新的 key 使用旧版本会得到 VersionConflictOrNotFound。当前该错误不区分资源不存在与版本冲突；对外 API 设计时应明确是否允许披露存在性。

Operation 与意图、Outbox 同事务提交。数据库故障向宿主抛出基础设施异常且不返回成功；未来 API 宿主需映射稳定错误并脱敏，不直接对用户暴露异常。

## 故障边界

测试探针可以在意图写入、Operation 写入、Outbox 写完/commit 前、commit 后/回执前暂停。故障回调是仅向测试程序集开放的内部构造参数，不是产品配置、环境变量或网络能力。测试父进程仅终止自己创建的进程句柄。

前三个点被终止后，新连接恢复出旧状态且无 Operation/Outbox；commit 后和回执后被终止，原操作仍在。重试后均只有一条 Operation/Outbox。该证据覆盖进程终止，不覆盖断电、磁盘损坏或 M1 的 OS spawn 原子性。
