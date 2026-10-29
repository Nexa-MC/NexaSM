# SM-ADR-001 · 独立产品和版本化依赖

状态：Accepted（方向）；核心本地包消费实验通过，正式共享包及完整构建资产仍待交付。

SM 与 AC 是独立产品，SM 不引用 NexaCL 应用工程、共享源码副本或开发者磁盘路径。共享包必须有可消费版本、来源、许可证和验证证据。

## 当前来源与检查

2026-10-29 用户确认 XSR 尚未共享，指定检查 NexaCL。实际仓库为 Nexa-MC/NexaCL main，提交 2ecadffb74f40c07a674a7f72cefaa50d0dd1c0b。不再以旧 PCL-N/refactor/xsr 作为当前来源，也不继续等待用户提供不存在的包源。

核心 Runtime 的 8 项目闭包不依赖具体业务服务；上游 Runtime 183 项测试通过。隔离目录生成本地实验包后，纯 PackageReference 消费者的路由/状态普通与 Linux AOT 测试通过。

正式提取仍需 .nfp、自动插桩工具传播、许可证与版本元数据、可复现包构建和发布。Platform.Runtime 依赖 20 个项目，应拆通用能力；Renderer/NXML 资产另做包化验收。详见 [上游检查与实施顺序](../verification/nexacl-xsr-inspection.md)。

## 历史记录

之前检查 PCL-N/refactor/xsr 1b69f8c3a8553d8c7970e6659024242f68b35d09 及 NuGet 公共 flat-container，未找到可消费发布包。该结论只描述当时的来源和发布面，不是“XSR 没有实现”。
