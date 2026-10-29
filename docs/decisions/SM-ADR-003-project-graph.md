# SM-ADR-003 · 最小工程依赖图

状态：Accepted（M0）。
Domain 无项目依赖；Contracts → Domain；Services → Domain/Contracts；Persistence.Sqlite → 三者；M0Probe 作为组合与测试消费者绑定 SQLite。TestProcess 无业务依赖，测试工程消费产品层。
图登记在 eng/project-graph.json，CI 拒绝未登记工程、非法项目/包引用、链接源码和空工程；基础层检查禁止实现命名空间。该静态门禁不等于全面语义/运行时架构证明；引入生成器、XSR 与 Renderer 时须扩展符号检查。
持久化端口由 Services 拥有，SQLite 为向内依赖的适配器。工具最小程序集不提前创建 Desktop/Web/Controller 空壳。
