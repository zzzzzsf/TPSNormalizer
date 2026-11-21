# “感觉没有差别”排查记录 — 0.2.1

用户反馈场景为藏身处靶场。本轮的结论分为已证实、条件推断和仍需运行数据三部分；不把静态检查当作靶场实测。

## 已证实

1. 反馈对应的0.2.0日志包含 `Loading [TPS Normalizer 0.2.0]` 和 `patches installed`；Fika也识别该插件。安装DLL哈希与发布构建一致，配置总开关及十项修复全部开启、参考120。排除了漏装、显式关闭及启动阶段补丁安装失败。
2. `HideoutPlayer.Create`先设 `IsYourPlayer = true`，之后 `Player.Init`调用 `GameWorld.RegisterPlayer`；注册流程为本地玩家设置 `MainPlayer`。藏身处继承LocalPlayer，现有保护条件没有主动排除藏身处。实际运行时归属仍需诊断记录确认，不能仅凭静态流程保证不存在第三方改动。
3. `GClass2404.Update`用 `Input.GetAxisRaw`取得普通鼠标位移。本插件不把这类位移再乘FPS，故普通慢速转向本来就不应因开关发生倍速变化。
4. `ClampRotation`处理单次角度上限，只有输入触及上限时才影响输出。BaseMovementState与PlayerStateContainer默认99，但具体状态可以覆盖；不能据默认值断定用户一定没有触发。
5. `PWA.UpdateWeaponVariables`使用 `InverseLerp(3, 7, 总重量 × (1 - 人体工效/100))`计算默认阈值下的AimSwayStrength。例如4kg、70人体工效时输入1.2，小于3，强度为0。配置和武器可改变实际值；本轮尚未识别用户所持枪械。强度为0时，该瞄准摆动输入修正确实没有效果。它与呼吸、移动拖曳等其他摆动不是同一机制。
6. `Player.FixedUpdate`累计固定步；`GClass909/911/912.ProcessEffectors`按固定步批量执行呼吸、运动、弹簧和后坐力。`Spring.FixedUpdate`和`NewRotationRecoilProcess.CalculateRecoil`中的某些逐调用累加已由调用方固定频率保护，不能再按渲染FPS盲目缩放。
7. 当前图形配置记录 `GameFramerate = 144` 且 `DisableGameFramerateLimit = true`。`GameGraphicsClass.ChangeFramerate`在相关会话路径会使用-1解除上限，设置值144不能证明实际FPS。旧日志没有真实帧时间记录，也没有参考值或总开关的操作历史。

## 本轮发现并处理

- 两处遗漏：`MovementState.SetTilt`受阻时回退侧倾，以及 `Player.method_23`第三人称持枪层回落，均使用 `rate × Time.deltaTime`重复插值。0.2.1纳入原有本地白名单，受ADS and look smoothing开关控制。这两项并不能直接解释用户靶场普通转向的反馈。
- 原版日志只有“安装成功”，缺少实际玩家归属、应用分支及真实FPS。0.2.1加入被动诊断，记录调用数据而不改变帧率或操作角色。ADS记录只反映视觉权重，不宣称覆盖完整开镜动画。
- 重新执行真实游戏IL转换和行为模型检查：417项IL/数学断言、5286项托管行为断言，32次转换、49处位置。没有把这些数值当作已实测项目数量。

## 核心候选处置与残余

本轮清单覆盖32个程序集、102343个有方法体的方法，985个平滑/帧计数候选。所选核心类110处已逐组核对，结果在工作区 `evidence/audit/core-review.csv`：包括已补丁的时间响应、原有固定步、姿态/空间权重、参数映射及明确残余。

残余包括 `Player.ApplyHitDebuff`按同一渲染帧聚合受击效果、`GClass2414.Update`每20渲染帧尝试鼠标回中。`MovementContext.ValidateMotion`是同帧重复移动保护，不能为了“全部固定”直接替换，否则可能重复应用运动。更广清单还发现VehicleBase用目标帧率参与车辆位移；它不在本插件的本地人物/武器修正范围。没有改写这些规则，也没有保证全部第三方代码及所有游戏按帧行为已修复。

## 目前不能下定论的部分

尚无用户靶场实际FPS、参考值对照、武器AimSwayStrength、修正分支命中和最终相机轨迹记录。因此现阶段只能确认“加载成功”及上述机制，不能断言用户感到相同一定是正常、一定是修正无效或一定被其他Mod覆盖。

需要按TESTING.md在诊断版下复测，至少收集总开关开/关、参考30/120/360的短窗口，再做实际30/60/120FPS的对照。取得日志后，先检查 `player`、`strategy`、`actualFPS`、`hits`和补丁归属，再判断效果与遗漏；在此之前，完整运行验证尚未完成。
