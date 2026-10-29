# NexaSM 设计

NexaSM 是 NexaSL 更名。Alpha 优先桌面闭环，兼容 Web、CLI 与无头运行。

- Node（原 EndPoint）：安装、进程、控制台、备份执行、资源观测，仅执行本机任务。
- Controller（原 Host）：节点、网络、工作负载、实例池、部署、调度、权限、审计。
- Standard：组合 Controller、Node 和桌面入口，可启用 Web、无头运行、增加远程 Node。

三者是部署组合，共享业务实现，不是收费等级。仅远程管理不要求运行本地 Controller 或 Node。
关闭管理界面默认不停服；Controller 失联不终止已有 JVM。每个资源每个控制权代次只有一个权威写入者。Standard 加入外部集群需显式转移控制权。

## 目录职责

- src/contracts：资源、命令、状态与事件契约。
- src/services：共享业务服务。
- src/controller：控制中心宿主与编排。
- src/node：本机执行与 Supervisor 集成。
- src/desktop、src/web、src/cli：管理入口。
- distributions/node、controller、standard：发行组合配置，不复制业务代码。
- tests：后续契约、生命周期、故障恢复测试。

## Alpha 验收

完成单机实例创建、安装、启动、控制台、停止及状态恢复；验证命令幂等、UI 退出后 JVM 存活、失联状态、控制权代次与世界卷互斥。具体语言、工具链和 XSR 包版本在实现前确认，当前无可运行程序。
