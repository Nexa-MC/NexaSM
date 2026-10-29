# NexaSM

服务器管理与编排平台；Node、Controller、Standard 共用业务核心。

当前：M0 工程与持久接受原型。尚无真实 Minecraft 启停、生产 CLI 或桌面宿主。

- [开发计划](docs/planning/development-plan.md)
- [M0 验证与限制](docs/verification/m0.md)
- [详细设计输入](docs/planning/NexaSM_Development_Design_v0.1.md)
- [架构基线](docs/architecture.md)
- [分支与协作](CONTRIBUTING.md)

## 开发验证

安装 global.json 指定 SDK，准备 Python 3，然后运行：

```sh
python eng/verify.py
# Linux x64，需 C/C++ 编译链接器与 zlib 开发文件：
python eng/verify.py --aot
```

受限云环境本次工具链在 /workspace/.tools/dotnet；运行前 `source /workspace/nexasm-env.sh`。通用开发机使用正常安装的 SDK，无须这些绝对路径。普通与 AOT 依赖锁分别维护，正式检查使用 locked mode。

M0Probe 仅在新建的测试数据库上验证提交、重开与重试，不连接真实游戏数据。`tools/FaultHarness/recovery.py` 会终止自己创建的探针进程。当前 src 下小写目录是早期设计占位；实际项目使用 NexaSM.*，未创建未来模块空工程。

性能为最高优先级：[响应预算](docs/performance/response-budgets.md)规定简单操作 ≤10ms、其余操作首次有效响应 ≤100ms。默认进程内 XSR 组合，不等待 SidecarReady；长任务返回真实状态，持久成功必须在事务提交之后。

验证脚本从 `eng/xsr-source.json` 固定的 NexaCL 源码构建共享包到忽略的本地 feed，核对包哈希后 locked restore；需要 Git 和网络。CI 在 Linux 构建一次相同 feed，再供 Linux/Windows 消费。当前尚未发布至公共 NuGet registry。
