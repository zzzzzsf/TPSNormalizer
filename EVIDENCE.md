# TPS Normalizer 0.3.2 证据与验证边界

检查对象：本机SPT 4.0.13 / EFT 0.16.9.40087。制作过程只读取原游戏。以下方法名与判断针对这份程序集，不据此推断其他版本。

`Assembly-CSharp.dll` SHA256：`FAEF6F0B9F142F9D047495EC3DCCFD5D6974AC048368DC7045955CF54B117982`。

完整反编译存在19处不相关后端/物品UI错误，因此不是无遗漏审计。原始反编译文件留在工作区，不随本体包分发。

## 0.3.0 菜单与性能验证

十个修复各增加独立参考FPS开关与数值；界面英文存储键保持稳定，显示层支持中文/Eng。数字输入采用草稿并在提交时校验，不在逐位输入期间写回。使用ConfigurationManager官方CustomDrawer标签机制，并在本机18.4程序集上验证标签读取。

新增118项真实BepInEx/ConfigurationManager/Mono集成断言。托管行为检查还覆盖十个独立参考实际修正路径、双语控件、草稿输入/保存次数、环形历史以及FOV增量采样曲线回归。性能检查含预热、7轮中位数与分配记录，详见PERFORMANCE.md与reports/performance.csv。

新安装诊断默认关闭；已有配置保持原选择。运动追踪参考值现在控制平滑时间，固定步长仍取原调用方时间。下述游戏代码证据与残余边界仍适用。

## 代码路径

| 证据位置 | 发现与处理 |
| --- | --- |
| `GraphicsSettingsClass` 构造函数 | 大厅默认60、战局默认120；后者用作参考，不是物理更新率。 |
| `MovementState.ClampRotation` | 三处读取单次 `RotationSpeedClamp` 而无时间换算；补丁将上限乘真实帧时间与参考FPS，不缩放普通鼠标增量。 |
| `PWA.LerpCamera / MountedWeaponCamera` | 每渲染帧调用 `SwaySpring.ApplyVelocity`，而该方法累加 `PVel`；改为支持策略中的固定步输入积分。 |
| `GClass909/911/912.ProcessEffectors` | 原有弹簧已按固定步求解；保留求解器，将线性/限幅ValueBlender输入面积积分到各步。 |
| `MotionEffector.FixedTracking` | 两处四参数SmoothDamp隐式使用渲染时间；改为显式固定步长。同帧多次固定步间分配旋转采样。 |
| `Player.VisualPass` → `PWA.ProcessEffectors` | 有固定更新时传累计固定时间，否则传渲染累计时间；视觉回调每帧执行。按真实时间标定参考FPS的混合时钟响应，固定效应器保留固定时间。 |
| `PWA.method_19` | 四个镜头后坐力Quaternion.Lerp重复使用逐帧权重；改为时间补偿的Slerp，保留动态目标来源。 |
| `PWA.UpdateAimWeight`等、`Player.Look/ManualUpdate`、`MovementContext` | 对明确白名单中的速率型/常量权重时间插值补偿，不统一改写动画姿态权重。 |
| `PlayerBones.ShiftWeaponRoot` | 前两组位置/旋转用于左右持枪平滑；只修正这四处，保留其余八处姿态插值。 |
| `CameraClass.method_5` 协程 | 每帧再次从当前FOV插值，高FPS变焦更快；改为参考递推曲线采样，保留通知、终值及清理。 |
| `GClass2414` → `GClass2407` → `GClass2409.GetValue` | MousePosition方向值按帧产生，上游又用精确±1检测命令；只在检测之后缩放四个转向/观察轴。 |
| `MovementContext.method_15` | 普通侧倾惯性每次调用按常量增减；按参考FPS累积并执行离散步。 |
| `MovementState.UpdateRotationSpeed` → `RollingMedian.get_Avarage` | 最近10次采样的时间窗口随FPS变化，下游用于转向声和灌木声；按传入采样时间构建 `10 / Reference FPS` 秒加权平均，仅影响本地缓冲区读取。 |

## 参考手感

常量权重alpha换算为 `1 - (1 - alpha)^(真实时间 × 参考FPS)`；速率型插值先恢复速率，再按参考帧计算系数。混合时钟另按原游戏参考FPS下两类帧时间的比例计算响应。

例如物理60Hz、参考120FPS时，原视觉路径每真实秒可累计约1.5秒的传入时间。0.2.0选择稳定重现参考响应；若强制每秒都改成1秒，会改变参考120下原开镜速度。

Slerp适合固定目标的时间补偿，但不与原nlerp在所有角度完全相同。摆动输入移至固定步也会改变原渲染步的相位。“参考120”表示标定参照，不保证与未加载插件的120FPS逐帧重合。

## 离线验证

- **417项断言**：数学边界、默认120真实IL常量、生产DLL转换函数在真实游戏IL的匹配、标签保留、形状不匹配拒绝及回调参数元数据；增加藏身处人物继承和诊断字段核对。
- **32次转换 / 49处位置**：包含两个镜头方法的两种转换顺序；不是49个独立功能。
- **13,417项行为断言**：生产源码链接的托管模型，覆盖15/20/30/45/60/90/120/144/240/360FPS、物理50/60/120Hz、阻尼分支、变帧、250ms卡顿、部分开关及被动诊断边界。
- 本轮静态清单读取32个程序集、102343个有方法体的方法，找到985个候选，其中选定核心类110个；另有50个第三方虚方法候选。候选数量不等于缺陷数或修复数。新增的程序集包含已安装的本插件。

本轮发现 `MovementState.SetTilt` 的受阻回退与 `Player.method_23` 的第三人称持枪层回落使用渲染时间驱动的重复插值，已补入白名单。110个核心候选均有处置记录，工作区文件为 `evidence/audit/core-review.csv`。其中仍保留受击的同帧聚合、重复移动保护和特殊输入的20帧回中节奏；本版没有声称消除所有按帧逻辑。

`reports/verification.txt`、`reports/behavior-verification.txt` 是运行输出。`reports/fps-comparison.csv` 为合成模型数据：`ADS_old`/`FOV_old`使用原始对应响应，`Sway_v01`是0.1版模型，不能将所有old列统称为未安装插件的完整游戏。该文件不含游戏现场测量。

验证脚本记录构建与测试输入/报告哈希，打包前再次核对。`manifest.json`标识版本、DLL哈希和未实测状态。

## 未验证内容

没有在原生Unity安装执行钩子、进入战局或检查F12实际显示。SmoothDamp替身仅检查传入步长，没有运行原生求解；碰撞、IK、动画事件、第三方运行时补丁、随机输入和实际帧时间分布仍需实测。没有保证全游戏或所有Mod不再受帧率影响。操作见 [TESTING.md](TESTING.md)。
