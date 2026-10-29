# SM-ADR-007 · 工具链与本地事务原型

状态：Accepted（Linux x64 M0 原型）；其他平台待验证。
使用 global.json 固定 .NET SDK，NuGet.Config 只声明 nuget.org，各项目分别保存普通与 AOT linux-x64 依赖锁。SQLite 适配采用 Microsoft.Data.Sqlite 10.0.14；当前使用本地 rollback journal、FULL synchronous 与即时写事务，不启用共享网络文件系统，也未实现迁移/备份功能。
选择依据：并发重试、事务失败回滚、重开库与 Native AOT 消费能够直接验证。SQLite 默认 journal 足以开始，WAL 不是正确性前提；后续可按竞争实测决定。数据库仅一个控制域且连接无池化，便于原型释放与崩溃演练。
PostgreSQL、WAL 优化、schema 升级和恢复备份另行决策。测试框架 xUnit 2.9.3、测试 SDK 17.14.1，精确传递依赖见 lock。
