# SM-ADR-006 · 持久接受和幂等

状态：Accepted（M0 切片）。
SetDesiredState 的规范化 payload 为 schema、expected version、desired state 的固定字段表示；CommandId 是关联身份，不影响相同请求的语义摘要。幂等域为数据库绑定 ControlDomain + 已授权 Principal + CommandKind + Target + Key。它不是任意 JSON 的通用规范化器。
同事务 CAS 修改实例、插入 Operation、Outbox，commit 后才返回 Pending receipt；重复 key 同摘要返回原记录，不同摘要拒绝。实现不启动进程，不宣称 Exactly Once 或已有单 Run 保护。
M0 不清理幂等记录，避免过期后无声重放；M1 必须补容量与保留期限、过期确认、步骤账本、Inbox 投递确认和有界重试。Outbox 当前只有有界读，没有生产投递器。ExecutionStepKey/Receipt 只是 Node Inbox 契约草案。
OperationService 接受可信宿主建立的 AuthorizedScope；这不是已实现认证/鉴权的公网 API。状态转移规则有测试，数据库当前只持久 Pending，完成/取消/补偿留给执行切片。

验证补充：五个内部检查点实际 kill/重启已在普通和 Linux AOT 探针执行，涵盖提交前事务回滚以及提交后回执丢失。ExpectedVersion.Value 不能省略。JSON 样例由真实消费者测试，见 docs/contracts/desired-state-v1.md。
