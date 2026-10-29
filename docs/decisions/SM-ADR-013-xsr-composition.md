# SM-ADR-013 · XSR 本地组合与性能边界

状态：Accepted，首批核心包接入。
用户要求不依赖 SidecarReady、性能优先。SM 只建立类型化本地路由，不创建 SidecarSupervisor、插件会话、扫描器或就绪屏障，不启用宿主自动补丁插桩。Runtime 包当前仍包含 Sidecar 类型，这是共享包静态闭包，不代表 SM 启动了这些能力。

Composition 是唯一引用 Xsr.Runtime 的 SM 产品层；Services/Domain 继续保持独立。路由接受不等于持久接受，SubmitAsync 必须等待服务提交回执。SQLite 同步 IO 在有并发上限的后台任务执行，满额立即返回 AdmissionBusy，不无限排队。

取消令牌取消调用者等待；一旦提交路径开始，不撤销已经接受的业务。丢失回执后使用相同幂等 key 查询/重试。这里的路由是管理路径，不进入游戏包处理热路径。

共享包来自锁定 NexaCL 提交的可复现本地 feed，不引用其应用源码。先运行 eng/prepare_xsr.py，随后 locked restore。远程发布源可在后续采用相同包版本和锁后替换。完整性能数据尚未测量，不宣称延迟 SLA。

SQLite 写入由一个可复用专用线程串行执行，默认最多 4 个在途请求（执行与排队合计）。使用有界队列，不占用公共线程池等待存储；组合根由后台宿主管理，释放时排空已接受的写入。
