# 分支与协作

- main：可审查的稳定基线；初始骨架不代表可发布产品。
- develop：下一版本集成。
- feature/node：node 方向开发，完成切片后合入 develop。
- feature/controller：controller 方向开发，完成切片后合入 develop。
- feature/standard：standard 方向开发，完成切片后合入 develop。

功能分支不是永久产品分叉；公共契约与业务修复回归共享主线。同步 develop，避免复制另一发行形态的业务代码。后续按 feature/<topic>、fix/<topic> 创建短期分支。develop 经验证后通过 PR 合入 main，发布使用 v<major>.<minor>.<patch> 标签。

PR 说明目标、边界、验证结果和限制。涉及契约需说明版本兼容；涉及状态与控制权需覆盖幂等和恢复；涉及反作弊需区分基础保护、准入与行为处罚。未执行测试必须明确标注。

当前未设置远程分支保护或 CI，也未选定构建工具。实现阶段引入实际构建与测试后再设置必需检查。每个云任务已隔离，默认使用现有 checkout，不额外创建 worktree。
