# TPS Normalizer 0.3.2

作者：Zenice · MIT 许可证 · [English](README.en.md) · [源码](https://github.com/zzzzzsf/TPSNormalizer)

0.3.2 仅更新版本元数据与启动日志；运行包仅含 DLL。保留原 GUID、配置路径和修正行为，作者的游戏内实测确认针对 0.3.1。

SPT 客户端实验版插件：减少本地玩家视角、武器动画及部分姿势逻辑随实际渲染帧率变化的差异。默认参考 **120 FPS**，可以在 **F12 → TPS Normalizer → 全局基准帧率 / Global reference FPS** 中改为 30–360。

参考值控制已覆盖逻辑的响应尺度，不限制实际FPS。120来自本次检查的游戏版本中战局帧率设置的默认值。

**发布状态：已编译并通过离线验证；作者已确认在 SPT 4.0.13 完成游戏内测试，主要功能正常。该实测由作者报告，未提供逐项测量、录像或干净安装证明；详见 [实测记录](MAINTAINER-TEST-REPORT.md)。不能保证消除游戏及第三方Mod的全部帧率依赖。**

## 适用版本与依赖

- 针对 SPT **4.0.13**、EFT **0.16.9.40087** 制作；其他版本没有验证。
- 需要正常工作的SPT客户端及其BepInEx。本机编译依赖为 BepInEx **5.4.23.2** 和游戏自带的Harmony。
- F12界面由ConfigurationManager提供。本机位于 `BepInEx/plugins/spt/ConfigurationManager`；本包不重复分发它。
- 运行无需.NET SDK、服务器Mod、联网下载或额外前置插件。
- Fika 2.2.3、SAIN 4.4.0 仅做过部分静态检查，没有完成同时运行的兼容性测试。

## 安装与卸载

1. 退出游戏，移除旧版StableGame.dll，避免重复加载。
2. 将包内 **BepInEx** 文件夹合并到你选择的SPT游戏目录，形成：

   ```text
   <SPT游戏目录>/BepInEx/plugins/StableGame/StableGame.dll
   ```

3. 启动SPT，按F12，找到 **TPS Normalizer**。
4. 初次测试使用 `Enabled = true`、`Reference FPS = 120`，十个修复开关全部开启。
5. 按 [TESTING.md](TESTING.md) 完成五分钟检查和跨FPS对比。

首次加载后，BepInEx创建 `BepInEx/config/local.stablegame.fpsnormalizer.cfg`。设置即时生效并保存。切换后等当前动作结束，再重新开始动作比较。

卸载：退出游戏，删除 `BepInEx/plugins/StableGame`；配置文件可自行保留或删除。总开关可临时关闭修正；比较完全未加载状态时，应退出游戏移走DLL后重启。

本包没有安装脚本。制作和打包仅在工作区进行，没有自动安装或修改原游戏。以上复制步骤由使用者在选定的SPT安装中手动执行。

## 0.3.0 菜单与数字输入

- 第一栏最上方选择 **中文 / Eng**；本插件的分组、选项、说明、开关、应用与重置按钮随语言即时切换。F12窗口自身的搜索和全局按钮由ConfigurationManager管理。
- 第二栏十项修复各自提供 **独立基准帧率 / Use separate reference FPS**。默认关闭并跟随全局；打开后显示该项30–360的FPS输入，独立值默认120。每项之间保留40像素，约两行的空白。
- 数字可以先清空、逐位输入。按回车、点击“应用”或移开输入焦点才提交并限制到30–360；Esc取消。非法整数保留旧值并显示提示。仅输入中间的“1”“12”不会改变设置或触发保存。
- “重置本项”恢复该修复开启、独立调节关闭、独立值120。语言切换保留已有配置键名、开关和数值。
- 运动追踪的参考FPS从本版开始调整滤波平滑时间；真实物理时间步保持不变。各功能对参考值的响应机制不同，数值越大不代表效果越好。

## F12设置与覆盖

| 选项 | 作用与注意事项 |
| --- | --- |
| Enabled | 修正总开关。 |
| Reference FPS | 30–360，默认120。改变参考手感，不锁帧；数值越高不代表越好。 |
| Rotation speed limit | 转向单次角度上限按时间换算；不缩放普通鼠标位移。只有触及上限时容易看出变化。 |
| Aim sway impulse | 普通与架枪时的瞄准摆动输入移至固定步积分。未知自定义动画策略保留原路径。 |
| Camera recoil blend | 补偿镜头后坐力重复插值，不修改枪械属性或弹道。 |
| Motion tracking timestep | 两处SmoothDamp使用调用方固定步长，并分配同批旋转采样；参考FPS控制平滑时间：原值 × 120 / 参考FPS。默认120保持原响应。 |
| Visual update timestep | 按真实时间重现参考FPS下原游戏视觉混合时钟响应。建议与下一项成对开关。 |
| ADS and look smoothing | 开镜、镜头对齐、自由观察回正、俯仰限制、部分姿势/趴姿及左右持枪切换平滑。 |
| FOV transition | 按参考FPS原始曲线采样，减少高FPS重复插值引起的变焦加速。 |
| Alternate mouse axis rate | 仅处理MousePosition方向型转向/观察轴。默认鼠标输入下可能没有可见变化。 |
| Lean inertia reference ticks | 普通侧倾惯性按参考频率更新；高FPS下可能出现参考频率的阶梯变化。 |
| Rotation sound averaging | 转向速度平均窗口改为最近 `10 / Reference FPS` 秒，影响转向声和灌木声响应。 |

修复限于当前本地玩家的已定位路径。没有调整全局 `Time.timeScale`、`Time.fixedDeltaTime` 或实际画面帧率上限。

## 验证与局限

通过 **417项IL/数学断言**及 **13,417项托管行为断言**。真实游戏IL上执行32次转换、检查49处位置，另核对七个补丁类的回调参数；重叠镜头方法检查了两种转换顺序。

0.2.1曾补入受阻侧倾与第三人称持枪层回落，并加入诊断。0.3.0新增安装默认关闭 `03 诊断 → 诊断日志 / Diagnostic logging`，已有配置的选择会保留。每10秒记录实际FPS、玩家/动画策略和修正命中情况，也记录观察到的ADS响应。没有自动锁帧或操作角色；排查结束可关闭诊断。旧版无差别反馈的排查见 [REVIEW.md](REVIEW.md)。菜单与性能更新见下文和 [PERFORMANCE.md](PERFORMANCE.md)。

行为模型覆盖实际15–360FPS、物理50/60/120Hz、变帧、250ms卡顿及弹簧阻尼分支。测试使用托管Unity边界替身，没有运行原生Unity、实际战局或原生SmoothDamp求解。结果见 `reports`，证据见 [EVIDENCE.md](EVIDENCE.md)。

动态目标、低FPS丢失的输入采样、随机摇晃、碰撞、IK、动画事件和其他Mod仍可能产生差异。部分时间四元数平滑改用Slerp，大角度时会改变参考FPS下原Lerp轨迹；不能保证逐帧姿态完全一致。

方法形状不匹配或补丁安装失败时，插件记录错误并撤销自身已装补丁。这不能代替版本和运行时兼容性检查；更新SPT/EFT后应重新验证。

## 性能检查

已消除每帧设置字符串构造、转向历史链表节点分配以及热路径反射取值；FOV曲线改为增量采样，初始化设置合并为一次保存。菜单翻译只在切换时刷新。

Release托管模型中，正常关闭诊断时的模拟帧工作约0.001–0.003ms，稳定阶段0 B/帧分配。这不是运行中游戏的帧耗时，也不包含原生渲染、真实SmoothDamp、Harmony调用和菜单绘制。实际游戏内开销需按测试指南A/B比较；详见 [PERFORMANCE.md](PERFORMANCE.md) 及reports性能原始数据。

另通过118项真实BepInEx/ConfigurationManager程序集与本机Mono的集成检查，覆盖标签解析、语言、范围、存储键及字段读取；没有据此宣称游戏内测试通过。

## 包内文件与源码构建

唯一运行组件为 `BepInEx/plugins/StableGame/StableGame.dll`。文档、`reports`、`manifest.json` 和 `SHA256SUMS.txt` 无需复制进游戏。本包不含游戏程序集、BepInEx/Harmony副本、测试探针、存档、日志或用户配置。

公开源码的独立编译方法见 [BUILDING.md](BUILDING.md)。完整工作区具备.NET 10 SDK、当前游戏依赖及现有测试夹具时：

```powershell
.\build.ps1 -GamePath 'D:\Game\EFT'
.\verify.ps1 -GamePath 'D:\Game\EFT'
.\package.ps1
```

脚本只读取游戏依赖，在工作区输出。安装包运行不依赖工作区路径。行为测试所需的本地BetterSpring反编译夹具不随本体包分发。
