# SM-ADR-005 · 身份、版本与状态

状态：Accepted（M0 内部原型，非公共 ABI）。
InstanceId、RunId、OperationId 为不同类型；ResourceVersion、SpecGeneration、StateRevision、OwnerEpoch 分开。版本控制接受 CAS；spec 实际变化才增加 generation。Availability 变化不推导 Run 退出。
当前只落实实例期望状态的最小数据库形状，不是完整资源 schema。强类型 struct 可具有默认值，必须在命令入口拒绝空身份；持久数据外部输入不能绕过校验。传输生成式 JSON 支持添加未知字段，但未知 schema 与非法枚举拒绝。
