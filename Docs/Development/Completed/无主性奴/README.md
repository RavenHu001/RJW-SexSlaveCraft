# 第 6 项无主性奴实施与验收

2026-11-21，第 6 项及第 6.1 项合入 `main`（`22bb463d`）。本页保留五批实施计划、分批证据和合并前回归。各批的开发占位和待验收文字按原时点阅读。 [PR #48](https://github.com/RavenHu001/RJW-SexSlaveCraft/pull/48)

[现行设计](../../../Design/Archive/第6项无主性奴设计方案.md) · [四语文案交接底稿](../../Reference/History/第6项无主性奴简中文案交接清单.md) · [合并前整仓回归](#合并前回归修复与验证)

## 文件归属

| 资料 | 入口 |
| --- | --- |
| 已交付规则与数值 | [设计归档](../../../Design/Archive/第6项无主性奴设计方案.md) |
| 运行文案与定稿来源 | [1.7 语言资源](../../../../1.7/Languages/) · [历史交接](../../Reference/History/第6项无主性奴简中文案交接清单.md) |
| 公共敏感度接口 | [现行接入规范](../../Reference/高潮推进与敏感度接入规范.md) |
| 爆发通用规则、计时及保留事项 | [第 7 项完成索引](../奴性爆发/README.md) |
| 2.5.2 版本验证 | [分批结果和发布准备](../../../Releases/2.5.2/2.5.2版本验证.md) |

<a id="ssc-ownerless-implementation-batches"></a>

## 分批实现

| 批次 | 实现范围 | 复用的现有逻辑 |
| --- | --- | --- |
| 1. 阶段、身份与存档 | 建立正式无主 Hediff、共用阶段查询与身份锁。接通恶堕下限、落差心情、阶段经验倍率、概况进度和已有的阶段门槛。移除所有日常调教的自动建链，日常准入使用普通调教规则。 | 复用现行阶段判定、进度公式、身份提交、普通调教准入及日常收益结算。 [阶段判定](../../../../Sexslavecraft/common/TrainingOutcomeUtility.cs) · [进度公式](../../../../Sexslavecraft/common/CorruptionProgressionUtility.cs) · [身份提交](../../../../Sexslavecraft/common/SSCIdentityUtility.cs) · [日常调教](../../../../Sexslavecraft/common/ConditioningUtility.cs) |
| 2. 阶段效果与行为 | 应用四阶属性、持续心情、自动敏感度、行为与浪漫限制。接通合格调教员指派、无主任教禁用、反叛保护与 PNA 成瘾免疫。在目标构建复核三、四阶 RJW 实际判定值。 | 复用现有行为解析、特化和装备覆盖顺序、自我调教分流、浪漫判定及调教管理界面。指派沿用原名单，训导官状态沿用现有生命周期复核。PNA 在成瘾准入和状态清理处接入，药物负荷、吸收和武器判定沿用各自的现有资格。 [行为解析](../../../../Sexslavecraft/common/Restrictions/SSCRestrictionResolver.cs) · [自我调教分流](../../../../Sexslavecraft/common/Restrictions/SSCSelfTrainingAutoJob.cs) · [浪漫判定](../../../../Sexslavecraft/common/Romance/SSCRomancePolicy.cs) · [训导官生命周期](../../../../Sexslavecraft/common/TrainerSpecializationLifecycle.cs) · [PNA 状态](../../../../Sexslavecraft/common/SSCPnaExposureUtility.cs) |
| 3. 爆发成形、绑定与成长 | 接入持续满恶堕必发、同源警报及默认启用。按调教员身份提供成形和等进度绑定分支，保存获批选择，选项使用开发占位文案。去除无链额外 `+50` 分，接通共用转奴开关、结果信和无主进度的人格凝胶往返。 | 复用 `TryStart`、印记优先、冷却去重、发作请求、任务认领、普通调教收益、进度公式和结果发布。绑定复用双向建链与进度交接。转奴复用现有保存键及非仪式概率参数，仅在首次成形和无主转有主成功后执行。 [启动入口](../../../../Sexslavecraft/common/SSCSlaveBreakdownGameComponent.cs) · [任务与结果信](../../../../Sexslavecraft/common/MentalState_SSCSlaveBreakdown.Training.cs) · [转奴公式](../../../../Sexslavecraft/common/WillReductionUtility.cs) · [人格保存](../../../../Sexslavecraft/Comps/ThingsComp_PE_PES.cs) |
| 4. 仪式准入与结算 | 按现有主人和训导官分支补正式无主目标的准入与结算。主人按原始进度绑定，训导官按结果推进无主 Hediff。核对零阶首次建链及共用转奴开关下的原有仪式结算。 | 复用仪式选角、评分、许可、上下文复查，以及与爆发共用的绑定提交和进度交接。训导官分支复用共用进度公式，原版转奴使用仪式参数。 [调教准入](../../../../Sexslavecraft/common/Restrictions/SSCRestrictionTrainingUtility.cs) · [仪式结算](../../../../Sexslavecraft/Ritual/RitualOutcomeEffectWorker_SSCBinding.cs) · [绑定提交](../../../../Sexslavecraft/common/SSCBondUtility.cs) |
| 5. 解绑与生命周期 | 接入主动解绑、主人死亡或确认永久失效、旧档已死亡主人、植入人格时原主人失效。转换保留原进度并统一给予两日后效。爆发和仪式的绑定分支共用再绑定冷却。 | 复用缰绳目标快照、实际锁链归属核对、双方关系与指派清理、读档关系修复和人格恢复。由实际解绑入口在删链前捕获原始进度并认领一次转换。旧身体清理沿用其人格生命周期。 [第五批实施顺序与验收](#ssc-ownerless-release-batch5) |

<a id="ssc-ownerless-first-batch"></a>

### 第一批实现范围

1. **进度与存档。** 在 1.7 新增正式无主 Hediff，以 `Severity` 保存精确进度。有主继续从锁链保存和读取进度。沿用现行四阶阈值判定和推进公式，同时识别手选或印记形成的零阶身份，以及低于首阶阈值的旧锁链。第一批通过测试夹具建立无主状态，玩家的成形入口在第三批接入。
2. **身份与消费者。** 在统一身份查询和提交中加入无主 Hediff 身份锁。共用阶段接入恶堕下限、恶堕落差心情、部位经验和身体开发倍率、「调教管理 → 概况」、二阶 RJW 指派、四阶战衣。SSC 性奴特性仍按正式身份及历史恶堕峰值计算。实际主人、分伤、点名命令与 PNA 阶段维持继续读取实际绑定。 [落差心情](../../../../Sexslavecraft/common/SSCCorruptionStages.cs) · [概况进度](../../../../Sexslavecraft/UI/Dialog_SSCTrainingManagement.Overview.cs) · [RJW 指派](../../../../Sexslavecraft/ThePatches/Harmony_RJWComfortDesignation.cs) · [战衣门槛](../../../../Sexslavecraft/ThePatches/Harmony_PreventWearEFOutfit.cs)
3. **日常调教。** 从新旧两套评分的日常结算中移除自动建链，保留普通调教收益。日常使用普通调教准入及指派校验，「建绑准备」只用于仪式。性奴化进度由爆发及符合结算条件的仪式推进。 [日常结算](../../../../Sexslavecraft/common/ConditioningUtility.cs) · [自动建链](../../../../Sexslavecraft/common/CorruptionProgressionUtility.cs) · [现行调教准入](../../../../Sexslavecraft/common/Restrictions/SSCRestrictionTrainingUtility.cs)

### 第一批验收点

- 让有主和无主角色各保存三阶 `80%`，读档后均原样恢复，概况显示同阶段和进度。两者的恶堕下限均为 `50%`，阶段经验倍率均为 `×1.6`，在同样实际恶堕下得到相同的落差心情。实际绑定查询只对有主角色返回主人。
- 核对无主 Hediff 存在时，身份菜单和实际提交都拒绝切出 SSC 性奴身份。手选零阶、印记零阶和旧锁链仍按各自的现行规则读取。普通旧档载入时仍由现有身份和绑定数据决定状态。
- 让主人分别日常调教零阶和正式无主目标。达到原自动建链门槛时，普通收益照常入账，目标仍保留原进度与归属。覆盖新旧评分，并核对二阶 RJW 指派和四阶战衣的代表性阶段边界。
- 复用现有进度、身份、存读档和概况的测试宿主。第一批完成时，记录专项回归和 1.7 Def 检查结果。 [进度测试](../../../../Tests/RitualProgression/README.md) · [身份测试](../../../../Tests/TrainerIdentity/README.md) · [测试与资源检查入口](../../../../Tests/README.md)

### 第一批实现与自动验证

- 1.7 新增无主进度 Hediff。概况、恶堕下限、落差心情和成长倍率共用阶段读取，身份菜单及提交共用无主 Hediff 身份锁。日常调教在两套评分中均只结算收益，日常准入校验实际指派，未绑定仪式沿用建绑准备。 [无主 Hediff](../../../../Sexslavecraft/Hediff/Hediff_SSCOwnerlessSlave.cs) · [阶段读取](../../../../Sexslavecraft/common/TrainingOutcomeUtility.cs) · [日常结算](../../../../Sexslavecraft/common/ConditioningUtility.cs)
- RimWorld `1.7.5014 rev1195` 隔离候选编译通过，零警告、零错误。原生 `--imprint` **90/90**，涵盖原版健康 Scribe 往返、同阶效果、身份锁、RJW 指派与战衣的阶段门槛，以及日常准入和收益。独立仪式进度 **38/38**、调教员身份 **171/171**、限制系统 **150/150**。 [存读档及阶段回归](../../../../Tests/UterusDevelopmentNative/CorruptionMoodTests.cs) · [日常调教回归](../../../../Tests/UterusDevelopmentNative/SlaveBreakdownTests.cs)
- 无主 Def 的四阶阈值与锁链一致，新增翻译 XML 可解析，Def 与语言镜像检查通过。 [1.7 无主 Def](../../../../1.7/Defs/HediffDefs/SSC_OwnerlessSlave.xml)

#### 第一批实现复核

先确定保存归属，再按用途接入读取，最后拆开日常与仪式的准入、结算。SSC 身份读调教组件，原始进度读当前 Hediff，实际主人读绑定锁链。手选或印记形成的零阶可没有进度载体，低于首阶阈值的旧锁链仍可保留原归属。共用阶段查询为概况、恶堕保底、落差心情和成长倍率提供同一阶段，实际绑定专属的消费者继续校验锁链。 [阶段读取](../../../../Sexslavecraft/common/TrainingOutcomeUtility.cs) · [身份提交](../../../../Sexslavecraft/common/SSCIdentityUtility.cs) · [绑定查询](../../../../Sexslavecraft/common/SSCBondUtility.cs)

日常工作和实际任务都通过共用准入生成器选择用途，两套 1.7 评分只结算普通收益。直接调用旧的非仪式进度入口也不会建链。现行首次建链使用默认 20%，无主转有主接入时须使用来源 Hediff 的原始严重度，并在同一次绑定结算交接载体。 [调教准入](../../../../Sexslavecraft/common/Restrictions/SSCRestrictionTrainingUtility.cs) · [进度结算](../../../../Sexslavecraft/common/CorruptionProgressionUtility.cs)

#### 第一批本机同步与实机验收

**2026-11-20，第一批实机验收通过。** 本机候选来自提交 `df2bc153`，面向 RimWorld `1.7.5014 rev1195`。同步更新 30 份文件，覆盖前备份旧文件，核对 931 份 1.7 运行文件的哈希。维护者反馈实机测试未发现问题。提供的测试清单包括日常调教保留普通收益及归属、无主身份锁及概况、阶段进度存读档。实机反馈为本批整体结论。

<a id="ssc-ownerless-second-batch"></a>

### 第二批实施顺序与验收

1. **阶段属性与心情。** 以无主 Hediff 为唯一阶段来源。原版低于首阈值会使用第一条阶段定义，因此增加中性零阶，正阈值及四阶进度保持原值。概况、头像和刻度只显示四个正阈值，DefInjected 随阶段索引调整。能力偏移和市值、RJW 脆弱度乘数走原版 Hediff 属性。新增独立无主持续思想，身体页只读显示自动档位。 [无主 Def](../../../../1.7/Defs/HediffDefs/SSC_OwnerlessSlave.xml) · [现行敏感度入口](../../../../Sexslavecraft/common/Orgasm/SSCOrgasmSensitivityUtility.cs)
2. **行为许可与界面。** 统一解析阶段基准、特化和装备覆盖。接受调教四阶以允许为基准，仍校验实际指派及最终许可。将读取适用性和编辑资格分开，覆盖初始化、单项修改、恢复默认、批量模板及旧菜单。自我调教复用现有资格和候选分流。自然浪漫策略单独接入无主发起限制，接收和结束关系走原版条件，点名命令继续取实际绑定。 [行为解析](../../../../Sexslavecraft/common/Restrictions/SSCRestrictionResolver.cs) · [单人分流](../../../../Sexslavecraft/common/Restrictions/SSCSelfTrainingAutoJob.cs) · [浪漫策略](../../../../Sexslavecraft/common/Romance/SSCRomancePolicy.cs)
3. **指派与训导官。** 无主可指派任何具备现有调教员资格的人，指派只修改名单。无主角色复用现有训导官持续资格和状态维护，普通进度归档，终极记录以禁用标记保留。 [指派入口](../../../../Sexslavecraft/common/Restrictions/SSCRestrictionTrainerAssignment.cs) · [训导官维护](../../../../Sexslavecraft/common/TrainerSpecializationLifecycle.cs)
4. **共享保护。** 从一阶接入现有越狱、叛乱、狂暴与压制条保护。PNA 成瘾免疫在建依赖、服药、直接添加及遗留依赖清理处统一判定。负荷形态、吸收、武器效果和阶段维持仍使用各自的实际绑定资格。 [反叛保护](../../../../Sexslavecraft/ThePatches/Harmony_HediffPreventRebel.cs) · [PNA 状态维护](../../../../Sexslavecraft/common/SSCPnaExposureUtility.cs)

验收以开发者挂载无主 Hediff 为入口，覆盖 `9.9% → 10%` 的属性、心情和保护切换，二阶升三阶的行为和候选分流，四阶浪漫、覆盖与只读写入防护，以及调教指派、训导官失格和满负荷免瘾。三、四阶分别核对倍率、属性面板值及 RJW 实际读取值，并验证与爆发临时效果叠加。自动回归复用现有原生健康、行为、浪漫、训导官与 PNA 宿主，界面、完整存读档及实际场景留实机确认。

#### 第二批实现与自动验证

- 中性零阶、四阶属性、独立持续心情及自动敏感度已接入。行为许可按阶段、特化和装备解析，个人规则写入保持原有偏好。无主可指派合格训导官，同一角色在失去绑定后停止任教并保留禁用的终极记录。反叛与成瘾免疫从有效一阶生效。 [无主 Hediff](../../../../Sexslavecraft/Hediff/Hediff_SSCOwnerlessSlave.cs) · [行为解析](../../../../Sexslavecraft/common/Restrictions/SSCRestrictionResolver.cs) · [PNA 免瘾](../../../../Sexslavecraft/common/SSCPnaExposureUtility.cs)
- 本批初次构建使用 RimWorld `1.7.5014 rev1195`，零警告、零错误。原生 `--imprint` **95/95**、限制核心 **150/150**、任务守卫 **222/222**、调教员身份 **171/171**、PNA **378/378**。自我调教 **50/50**、主人界面 **124/124**、仪式选角 **144/144**。Def、译文阶段索引和键参数检查通过。 [原生阶段回归](../../../../Tests/UterusDevelopmentNative/CorruptionMoodTests.cs) · [PNA、训导官及脆弱度回归](../../../../Tests/UterusDevelopmentNative/SlaveBreakdownTests.cs)
- **提交前复核。** 补注释说明中性零阶、锁链优先、阶段规则只读、指派与任务许可的分工，以及免瘾与负荷形态各自的资格。修正 PNA 清除最后一份负荷时跨阶段所致的依赖漏清理。新增断言在修改前候选复现，修订后印记分区与上述七组关联回归全部通过。同步修正四语自我调教资格提示，并防护自然浪漫查询遇到尚无健康组件的角色。 [PNA 回归](../../../../Tests/UterusDevelopmentNative/SlaveBreakdownTests.cs) · [浪漫资格回归](../../../../Tests/UterusDevelopmentNative/CorruptionMoodTests.cs)
- 额外复跑 `RomanceControlNative` 得到 **1167/1192**。25 个整场重建用例在原生地图角色收集处遇到空引用，与第一批对照候选的失败清单完全一致。第二批的无主阶段浪漫规则由 `--imprint` 中的原生用例验证。 [整场读档夹具](../../../../Tests/RomanceControlNative/Batch4SceneSaveTests.cs) · [地图角色收集](../../../../Sexslavecraft/common/SSCBondOverviewWorld.cs)

#### 第二批界面修订与实机验收

**2026-11-20，第二批实机验收通过。** 维护者确认最终同步版实机测试未发现问题。同步目标为 RimWorld `1.7.5014 rev1195`，最终候选构建自 `c1683347` 加本轮修订。最后一次同步替换并核对 3 个文件的哈希，候选 DLL 的 SHA-256 为 `2837d8201f3f434c4b6513c6c4cda266d0cfd03dc6934a7f59826ba89b1325e6`。

- **界面。** 无主角色复用原性敏感改写、性行为和浪漫控制的控件布局，并以禁用样式展示当前值。两个行为板块的阶段信息放进标题括号。身体页显示档位，脆弱度数值由健康页展示。 [身体页](../../../../Sexslavecraft/UI/Dialog_SSCTrainingManagement.BodyProduction.cs) · [行为板块](../../../../Sexslavecraft/common/Romance/SSCRomanceUI.cs)
- **敏感度。** 有主和无主共用 `SSC_OrgasmSensitivity` Hediff、描述、属性和持续心情。无主按阶段自动选档，并拒绝手动写入。恢复绑定后重新使用原手动档位。上一测试候选的临时 Hediff 和迁移代码已移除。 [档位实现](../../../../Sexslavecraft/common/Orgasm/SSCOrgasmSensitivityUtility.cs)
- **回归。** 最终同步候选零警告、零错误，原生 `--imprint` **95/95**，含共用描述、自动档位、属性、思想及原手动选择恢复。界面修订候选的界面模型回归 **124/124**，共用效果候选的定向原生运行 **140/140**，其中 49 项为原有敏感度回归。完整原生宿主在上一候选和共用效果候选上均停在 `GrowthFlowTests` 的仪式结算补丁重载歧义。 [原生阶段回归](../../../../Tests/UterusDevelopmentNative/CorruptionMoodTests.cs) · [敏感度回归](../../../../Tests/UterusDevelopmentNative/OrgasmSensitivityTests.cs)

<a id="ssc-ownerless-third-batch"></a>

### 第三批实施顺序与验收

目标为 1.7。先接通满恶堕的通用触发，再完成请求授权和正式结算，最后接无主进度的人格凝胶往返。本批提供的等进度绑定提交供第四批仪式复用。解绑后效和主人失效转换在第五批接入同一绑定准入。

1. **通用启动和警报。** 在 `RefreshBeforeDecay` 读取实际恶堕，95% 至不足 100% 沿用随机判定，实际 100% 则在首次满足准入的定期检查调用同一 `TryStart`。首次跨满值继续由短期待办处理，印记结算和有效待办优先。持续满值检查沿用原版精神状态准入、一天发作冷却和同场去重。`SlaveBreakdownFullTriggerUsed` 继续服务首次跨值路径，持续满值按当前准入判断。警报共用资格查询，查询过程不掷骰或登记待办。将开关字段初值与存档缺省一并设为开启，保留已保存的选择。 [定期检查与警报](../../../../Sexslavecraft/common/SSCSlaveBreakdown.cs) · [满值待办与启动](../../../../Sexslavecraft/common/SSCSlaveBreakdownGameComponent.cs) · [设置](../../../../Sexslavecraft/common/Settings.cs)
2. **入场状态和请求授权。** 进入发作时先记录首次成形候选、正式无主或有主，以及原进度载体和起始阶段，再处理临时身份。开场信统一使用原有正文。有主仍向实际主人发信，无主与首次候选复用附近合格调教员扫描。按[已确认分支表](../../../Design/Archive/第6项无主性奴设计方案.md#ssc-ownerless-breakdown-choices)为同一封信生成选项，保留拒绝和稍后决定。精神状态保存所选结果，沿用原信件 ID、参与者和任务 `loadID` 核对批准、派发及完成。绑定分支额外复核主人资格和再绑定准入。建议对缺少结果字段的旧档无链批准撤销原授权，仍有时间时沿用原流程重新申请。开发选项文案明确标为临时占位。 [发作入场与保存](../../../../Sexslavecraft/common/MentalState_SSCSlaveBreakdown.cs) · [请求、派发与任务认领](../../../../Sexslavecraft/common/MentalState_SSCSlaveBreakdown.Training.cs)
3. **普通收益和正式结果。** 现有训练驱动只取一次本场基础评分，移除无链特批的 `+50`。先走共用调教收益、经验和完成记账，再在原任务最后一次资格检查后提交所选结果，避免新归属干扰仍在运行的原任务校验。替换成功信之后的无链满值预留回调。首次成形完成即建立一阶 20%，无主成长和有主成长共用 `CalculateChainProgress`，读取收益入账后的实际恶堕。同一载体已被外部推进跨阶时保留普通收益，跳过本场额外成长。结果提交、成功记忆和信件发布各认领一次。 [训练完成出口](../../../../Sexslavecraft/JobDriver/JobDriver_Train.cs) · [普通调教结算](../../../../Sexslavecraft/common/ConditioningUtility.cs) · [进度公式](../../../../Sexslavecraft/common/CorruptionProgressionUtility.cs)
4. **等进度绑定与原版转奴。** 扩展双向绑定提交，让新链挂载前接收经核验的原始严重度，三阶 80% 的角色从建链起就保持三阶 80%。确认实际锁链与缰绳写入后移除原无主 Hediff。失败时只清理本次创建的记录，保留仍有效的原进度。首次成形和无主转有主成功后才按共用 `enableRitualEnslavement` 尝试一次原版转奴，沿用本场基础评分、当前意志及实际恶堕，使用非仪式参数。按原版接口返回和最终身份报告转奴结果。既有原版奴隶直接结算 SSC 结果，转奴失败保留已经提交的 SSC 结果。共用现有有主结果信主文，由实际提交结果显示归属和进度。 [双向绑定](../../../../Sexslavecraft/common/SSCBondUtility.cs) · [锁链挂载](../../../../Sexslavecraft/Hediff/Hediff_ChainOfSexSlave.cs) · [转奴公式](../../../../Sexslavecraft/common/WillReductionUtility.cs) · [结果信](../../../../1.7/Languages/ChineseSimplified/Keyed/SSC_BreakdownTraining.xml)
5. **人格凝胶往返。** 在凝胶快照保存可缺省的无主原始严重度，贯通提取、序列化、加工复制和植入。确认凝胶生成成功并仍属于原人格后才移出原身体的无主记录。植入时先清除宿主旧无主记录和身份锁，再恢复来源身份与原严重度，复用第二批的自动敏感度同步。凝胶中有主关系的失效转换随第五批接入。 [凝胶快照](../../../../Sexslavecraft/Comps/ThingsComp_PE_PES.cs) · [提取、清理与植入](../../../../Sexslavecraft/common/ExcretionUtility.cs)

**首个可测节点。** 一阶无主角色维持实际恶堕 100%。冷却到期后的首次合格检查经通用入口发作，警报资格与启动一致，同一轮只启动一次。

**本批验收。** 在 `UterusDevelopmentNative --imprint` 覆盖首次跨满值与印记同场、持续满值跨冷却、两类四阶退出候选、两套调教评分、获批分支读档、换人格或换绑定使旧授权失效、三阶 80% 等进度绑定、同阶及跨阶成长，以及凝胶保存并恢复精确进度。实机再走请求信、完成信与共用转奴开关，核对首次成形和绑定才尝试转奴、转奴失败仍保留 SSC 结果、纯成长只结算一次进度。 [现有爆发原生回归](../../../../Tests/UterusDevelopmentNative/SlaveBreakdownTests.cs)

#### 第三批触发、授权与结算验证

- 通用启动和警报节点在 RimWorld `1.7.5014 rev1195` 通过原生 `--imprint` **96/96**。持续满值跨冷却、警报只读和同场去重通过后，继续实现请求授权与结算。
- 请求授权与结算候选编译零警告、零错误，原生 `--imprint` **99/99**，覆盖获批分支存读档、三阶 `80%` 原进度绑定、首次成形、共用增长上限和转奴失败后保留 SSC 结果。仪式进度 **38/38**、任务守卫 **222/222**、限制核心 **150/150**。 [爆发原生回归](../../../../Tests/UterusDevelopmentNative/SlaveBreakdownTests.cs)
- 请求选项使用开发占位文案。实机反馈与凝胶往返验证记录见下方。

<a id="ssc-ownerless-breakdown-review"></a>

**第三批复核。** 等进度绑定在写入前后同时核对无主记录实例和原始严重度。若挂链回调推进了来源或抛出异常，回滚本次仍持有的链和缰绳条目，保留原无主记录及其当前进度。SSC 进度和归属在原版转奴通知前记录，供本场结果信使用。RimWorld `1.7.5014 rev1195` 复核候选编译零警告、零错误，原生 `--imprint` **100/100**，新增 1 项 Core 复现两种回滚边界。 [绑定交接](../../../../Sexslavecraft/common/SSCBondUtility.cs) · [新增原生回归](../../../../Tests/UterusDevelopmentNative/SlaveBreakdownTests.cs)

**第三批实机反馈。** 已同步提交 `a84b874d` 至本机 RimWorld `1.7.5014 rev1195`。维护者反馈实机测试未发现功能问题，并确认全部角色复用原有奴性爆发开场信正文。已移除已有阶段角色的专用正文及四语翻译键。

<a id="ssc-ownerless-personality-gel"></a>

#### 无主进度的人格凝胶往返

- 凝胶只保存无主 Hediff 的原始严重度，读档和加工复制共用同一字段。缺字段的旧凝胶按无记录处理。普通与场景提取都在放置成功后复核人格、身体和原进度，再移除原身体的记录。放置回调改变进度时撤回该凝胶，保留来源进度供重试。 [凝胶快照](../../../../Sexslavecraft/Comps/ThingsComp_PE_PES.cs) · [普通提取](../../../../Sexslavecraft/common/ExcretionUtility.cs) · [场景提取](../../../../Sexslavecraft/JobDriver/JobDriver_PE.cs)
- 植入先移出宿主旧记录及其身份锁，再按来源身份和原始严重度重建记录。阶段由现有阈值计算，自动敏感度使用该阶段。 [植入](../../../../Sexslavecraft/common/ExcretionUtility.cs)
- RimWorld `1.7.5014 rev1195` 隔离构建零警告、零错误。原生 `--imprint` **102/102**，含新增 2 项 Core。三阶 `0.8123457` 经实际凝胶物品存读档、配方加工、再次存读档后植入，严重度精确相等，并替换宿主的四阶记录。另验证场景放置期间推进后的撤回与重试，以及旧凝胶清除宿主原进度。`--birth` **13/13**，`--fertility` **18/18**。原生宿主隔离地图放置和场景表现。 [原生回归](../../../../Tests/UterusDevelopmentNative/OwnerlessPersonalityTests.cs)
- **提交前复核。** 植入在挂载后和消耗凝胶前核对接收人格、当前记录及原始严重度。若回调改动进度导致植入失败，凝胶保留原快照，撤回本次挂载的进度并恢复原空壳标记，允许重试。新增 1 项 Core 注入实际健康回调验证撤回与重试。复核候选零警告、零错误，`--imprint` **103/103**、`--birth` **13/13**、`--fertility` **18/18**。 [植入结算](../../../../Sexslavecraft/common/ExcretionUtility.cs) · [失败重试回归](../../../../Tests/UterusDevelopmentNative/OwnerlessPersonalityTests.cs)
- **实机验收通过。** 本机 RimWorld `1.7.5014 rev1195` 同步提交 `43380eec` 后，维护者确认无主性奴的人格凝胶往返未发现问题，植入后原阶段和进度均能保留。
- **无主凝胶贴图。** 提取时按实际阶段选用现有人格凝胶定义。一至四阶分别对应普通、初级、高级和完美凝胶，加工使用同档的塑形贴图。三阶进度 80% 仍使用高级凝胶。 [凝胶选档](../../../../Sexslavecraft/common/PersonalityGelUtility.cs) · [四档凝胶定义](../../../../1.7/Defs/ThingsDefs/SSC_PS.xml)
- **贴图实机验收通过。** 在本机 RimWorld `1.7.5014 rev1195`，维护者确认无主性奴一至四阶的人格凝胶加工前后贴图均正常。受测候选的原生印记专项为 **103/103**，分娩专项为 **13/13**。

<a id="ssc-ownerless-ritual-batch4"></a>

### 第四批实施顺序与验收

目标为 1.7。复用现有仪式选角、六阶段任务、评分、普通收益和整场一次性结算。先调整共用准入并验证任务开场，再接主人等进度绑定，最后接训导官成长及两条分支的结算反馈。

1. **共用准入。** 在 `SSCRestrictionTrainingUtility.CreateRequest` 按正式无主状态及主持者 SSC 身份分流。主人走 `BindingPreparation`，复用指定调教员与建绑资格；接入 `CanBindNewWithProgress` 供选角和提交核对，第五批在同一入口补再绑定冷却。合格训导官走 `RitualTraining`，检查当前任职、实际指派和特化、装备覆盖后的最终「接受调教」许可。零阶无链目标走原首次建链路径，已有绑定继续允许实际主人或符合现行条件的指定调教员主持。选角、正式开始和任务守卫共用这套判定。 [共用请求](../../../../Sexslavecraft/common/Restrictions/SSCRestrictionTrainingUtility.cs) · [选角及开场校验](../../../../Sexslavecraft/common/BindingRitualSelectionUtility.cs) · [任务用途适配](../../../../Sexslavecraft/common/Restrictions/SSCRestrictionJobContext.cs)
2. **主人等进度绑定。** 扩展 `RitualChainProgress`，由仪式结果处理器领取本场一次性资格后，在质量、记忆及收益回调前捕获双方人格、组件、原需求、分支和原进度实例。新旧两套评分照常结算恶堕和意志。在写链前核对原无主实例、实际主持、指派和现有的实际恶堕 10% 建链门槛；将当前原始严重度交给第三批 `BindNewWithProgress`。写链后校验本场链实例、实际主人和缰绳，成功完成载体交接后结束本场性奴化结算。一至四阶均可交接。三阶 `80%` 绑定后仍为三阶 `80%`。关系奖励按实际主从关系判定。仪式原版转奴沿用共用开关、现有资格与仪式概率参数。 [结果处理器](../../../../Sexslavecraft/Ritual/RitualOutcomeEffectWorker_SSCBinding.cs) · [仪式收益](../../../../Sexslavecraft/common/ConditioningUtility.cs) · [等进度绑定](../../../../Sexslavecraft/common/SSCBondUtility.cs)
3. **训导官成长与反馈。** 普通收益入账后读取同一 `Need_Corruption` 的实际恶堕，将原无主严重度和下一阶起点传给 `CalculateChainProgress`。沿用恶堕门槛、单场额度和最多跨一阶的限制。只向本场捕获的无主实例写入，复用 Hediff 阶段通知刷新行为与敏感度；同一实例已被外部推进跨阶时，本场只保留普通收益。建议沿用现有委派仪式的原版转奴资格、开关及仪式参数，SSC 归属保持无主。主持者失格、参与者换人格或原进度载体被替换时，停止原分支写入。两类结果都进入现有仪式信，主人分支显示新绑定及保留进度，训导官分支显示实际升阶、同阶进展或进度未变。 [仪式进度与转奴](../../../../Sexslavecraft/common/CorruptionProgressionUtility.cs) · [无主阶段通知](../../../../Sexslavecraft/Hediff/Hediff_SSCOwnerlessSlave.cs)

**自动验收。** 复用仪式选角、仪式生命周期和进度回归，覆盖实际指派、装备或特化覆盖后的许可、开场拒绝、六阶段中断及重复结算。用 1.7 原生候选验证主人把三阶 `80%` 原值写入锁链、四阶可绑定、训导官把二阶 `40%` 在实际恶堕 `60%` 时推进至三阶 `50%` 且保持无主，以及进度或资格失效时拒绝旧写入。回归零阶首次建链 `20%`、有主升阶和转奴开关。 [选角回归](../../../../Tests/RitualSelection/Program.TrainingIdentity.cs) · [仪式生命周期](../../../../Tests/RitualLifecycle/Program.TrainingIdentity.cs) · [进度回归](../../../../Tests/RitualProgression/Program.cs)

**实机验收安排。** 用同一无主存档分别安排主人和训导官主持，核对选角、整场完成、结算信、健康页实际严重度和自动敏感度。再核对主人分支的实际归属与共用转奴开关。

#### 第四批开发与自动验证

- **准入节点。** RimWorld `1.7.5014 rev1195` 原生选角与开场专项 **19/19**，仪式选角 **144/144**、生命周期 **226/226**。先验证正式无主可由两类合格主持者启动，且训导官受最终许可约束、撤销指派后无法开场、零阶仍走原建链准入，再接入两类结算。原生夹具执行 SSC 选角、开场守卫和第一阶段认领，隔离地图与 RJW 身体查询。 [开场回归](../../../../Tests/TrainingDownedNative/TrainerRitualSelectionChecks.cs)
- **结算节点。** 1.7 隔离候选零警告、零错误，原生 `--imprint` **107/107**，新增 4 项 Core。覆盖两套评分下的精确绑定、四阶绑定、训导官按实际恶堕升阶及反馈、原载体或许可变化后的拒绝，以及共用开关中的原版转奴失败仍保留相应 SSC 结果。训导官主持无主目标时，仪式明细按调教员称呼主持者。 [结算回归](../../../../Tests/UterusDevelopmentNative/SlaveBreakdownTests.cs)
- **提交前复核。** 临时挂链至源记录移交之间继续核对主持资格与原指派。健康回调撤销指派时，撤回本次写入，原无主记录及精确严重度保留。新增 1 项 Core，旧候选原生 `--imprint` **107/108** 复现，修订候选 **108/108**。同一 1.7 隔离候选零警告、零错误，原生选角与开场 **19/19**。关联仪式选角 **144/144**、生命周期 **226/226**、进度 **38/38**、互动保护 **222/222**、限制核心 **150/150**。四语主持标题参数与 **728** 份语言镜像通过检查。 [交接实现](../../../../Sexslavecraft/common/SSCBondUtility.cs) · [撤销指派回归](../../../../Tests/UterusDevelopmentNative/SlaveBreakdownTests.cs)

#### 第四批本机同步与实机验收

**2026-11-21，第四批实机验收通过。** 本机 RimWorld `1.7.5014 rev1195` 同步提交 `55d47d4d`，备份并更新 7 份文件，核对 1056 份运行文件的哈希。维护者确认主人主持时绑定并保留原阶段和精确进度，训导官主持时正常升阶并保持无主，两类仪式的结算信显示正常。实机测试未发现问题。

<a id="ssc-ownerless-release-batch5"></a>

### 第五批实施顺序与验收

目标为 1.7。先验证原锁链到无主 Hediff 的交接及后效，再接主动解绑、主人死亡和旧档迁移，随后接入爆发与仪式的绑定冷却，最后补齐人格凝胶往返。

**第五批文案。** 操作按钮和弹窗标题统一使用「解除绑定」。确认框正文及规则提示、失败提示、死亡通知、再绑定冷却提示，以及解绑后效 Hediff 和持续心情的标题与描述均为开发占位文案，正式发布前另行定稿并替换。 [解绑界面文案](../../../../1.7/Languages/ChineseSimplified/Keyed/SSC_BondRelease.xml) · [后效说明](../../../../1.7/Defs/HediffDefs/SSC_BondReleaseAftereffect.xml) · [持续心情文案](../../../../1.7/Defs/ThoughtDefs/SSC_BondReleaseAftereffect.xml)

1. **共用解绑转换和后效。** 新增实际解绑的提交入口，复用 `SSCBondUtility.Unbind` 的关系清理。提交时核对原锁链实例、实际主人、从属者人格和健康载体，读取原锁链的 `Severity`，以精确值挂载无主 Hediff。交接期间统一查询仍优先读取原锁链，确认暂存记录和原上下文有效后再移除原链。阶段按共用阈值重算，三阶的非整百分比和旧零阶锁链的原值均保留。同步清理旧主的对应缰绳引用、指派、关系奖励和依赖原绑定的在途授权，复核其他调教员并刷新无主效果。保留 SSC 身份、原版身份、印记、当前恶堕和历史峰值。原锁链实例用于防止重入和重复提交，回调更换锁链或人格时仅撤回本次暂存写入。人格植入的旧身体清理继续走原有清理路径。 [现有解绑清理](../../../../Sexslavecraft/common/SSCBondUtility.cs) · [共用进度读取](../../../../Sexslavecraft/common/TrainingOutcomeUtility.cs)

   转换成功时应用一个解绑后效 Hediff。原版 `Disappears` 组件保存剩余 tick、显示倒计时，并在到期时移除后效。读入旧开发版角色身上的有效后效时，将原截止 tick 换算成剩余时间。 [原版组件配置](../../../../1.7/Defs/HediffDefs/SSC_BondReleaseAftereffect.xml) · [旧后效加载](../../../../Sexslavecraft/Hediff/Hediff_SSCBondReleaseAftereffect.cs)

2. **接通三个解绑入口。** 三个入口提交同一转换，由转换成功的时刻确定后效起点。已确认的永久销毁或丢弃，在原绑定和严重度仍可核验时也提交同一转换，处理点在锁链自动移除前。 [锁链生命周期](../../../../Sexslavecraft/Hediff/Hediff_ChainOfSexSlave.cs)

   - **主动解绑。** 接入「调教管理 → 关系」的主人卡片。确认框显示主人、保留的阶段和进度、转换后生效的规则及后效。确认时复查管理资格和打开确认框时所对应的原绑定。主人在异图或旅队仍可操作。 [关系页面](../../../../Sexslavecraft/UI/Dialog_SSCTrainingManagement.Relations.cs)
   - **主人死亡。** 在 `Pawn.Kill` 前快照缰绳候选及通知资格，死亡结算后确认实际死亡，逐个核对从属者的原锁链和归属再转换。候选包含异图和旅队。跳过已换主的角色，同次死亡合并通知成功转换的从属者。原主人复活后，从属者依旧无主，冷却结束后可按准入重新绑定。 [缰绳反向索引](../../../../Sexslavecraft/Hediff/Hediff_BridleOfSexSlave.cs) · [死亡转换](../../../../Sexslavecraft/ThePatches/Harmony_BondOwnerDeath.cs)
   - **旧档主人已死亡。** 等跨角色引用恢复后，在 `FinalizeInit` 复用现有角色快照，从从属侧锁链判定死亡主人，覆盖缺少缰绳的旧档。先完成转换，再修复有效的反向关系和恢复在途授权。第一次成功转换开始计时，再次读档沿用原版组件保存的剩余时间。 [读档关系整理](../../../../Sexslavecraft/common/SSCBondRelationGameComponent.cs) · [角色快照及读档任务恢复](../../../../Sexslavecraft/common/Restrictions/SSCRestrictionGameComponent.cs)

3. **接入爆发与仪式的再绑定冷却。** 将后效有效性检查接入 `CanBindNewWithProgress`。爆发的有主成形与无主转有主选项在批准、派发和最终写链时复查。仪式的 `BindingPreparation` 在选角和开场时检查，冷却期间显示剩余时间。正式无主交接和零阶首次建链均在提交前复查，覆盖由旧零阶锁链解绑得到的无主角色。爆发保持无主的成长、训导官主持的无主仪式及有主升阶沿用各自准入。 [共用建绑检查](../../../../Sexslavecraft/common/SSCBondUtility.cs) · [爆发选项](../../../../Sexslavecraft/common/MentalState_SSCSlaveBreakdown.Training.cs) · [仪式准入](../../../../Sexslavecraft/common/Restrictions/SSCRestrictionTrainingUtility.cs) · [仪式写入](../../../../Sexslavecraft/common/CorruptionProgressionUtility.cs)

4. **接入凝胶的进度和后效往返。** 复用第三批的人格快照、存读档、加工复制和植入提交，传递精确进度。成功清源时保存原版 `Disappears` 组件的剩余 tick，并移除原身体的后效。旧凝胶缺省为零。植入时在无主进度恢复后，按保存的正数剩余 tick 重建同一个后效。凝胶内原为有主时，植入时核对主人的当前存活状态和绑定资格。主人存活且合格时按原进度恢复绑定，含植入前已复活的主人。主人死亡、失效或失格时，按原进度恢复为无主，植入确认成功时开始完整两天后效。已提取人格的空壳若残留原链，沿旧身体清理路径移除，避免死亡处理为旧空壳再生成一份进度和后效。 [人格快照及复制](../../../../Sexslavecraft/Comps/ThingsComp_PE_PES.cs) · [人格提取与植入](../../../../Sexslavecraft/common/ExcretionUtility.cs)

#### 第五批验收点

| 范围 | 验收内容 |
| --- | --- |
| 进度交接与撤权 | 用三阶非整百分比和旧零阶锁链验证严重度原值、阶段和身份。重复提交、回调换链或换人格时验证后效只应用一次，旧授权失效，新载体与无关从属关系保留。 |
| 主动解绑、死亡和旧档 | 实机核对确认框、主人在异图或旅队时的主动解绑、一主多从含离图从属者的死亡合并通知，以及原主人复活。用缺缰绳的旧档验证从属侧仍能转换，保存后再次读档时核对阶段、进度及后效剩余时间。 |
| 后效与绑定入口 | 对比原版组件到期前后的工作速度、持续心情与建绑资格。核对爆发与仪式在冷却期间拦截新绑定，包括旧零阶，并验证旧批准或在途仪式在提交时会复查冷却。保留无主的调教仍能结算。 |
| 人格凝胶往返 | 在提取、存读档、加工复制及植入后核对原阶段和精确进度。原身体清除后效，凝胶内剩余 tick 保持不变，植入后继续倒计时。覆盖植入时原主人合格或已复活、原主人死亡或失格时新给两天，以及旧空壳无重复后效。 |

自动验收优先复用绑定关系、仪式选角和结算、原生爆发及人格凝胶往返专项。实机重点检查调教管理、死亡合并通知、健康页倒计时、爆发选项和仪式拒绝提示。 [绑定关系回归](../../../../Tests/BondRelations/Program.cs) · [原生仪式准入](../../../../Tests/TrainingDownedNative/TrainerRitualSelectionChecks.cs) · [原生爆发与仪式结算](../../../../Tests/UterusDevelopmentNative/SlaveBreakdownTests.cs) · [原生凝胶往返](../../../../Tests/UterusDevelopmentNative/OwnerlessPersonalityTests.cs)

#### 第五批开发与自动验证

- **共用转换节点。** 1.7 隔离候选的原生 `--imprint` **110/110**。先覆盖三阶非整百分比、旧零阶的零进度、重复提交只应用一次后效，以及挂载回调换主时撤回暂存记录。 [解绑原生回归](../../../../Tests/UterusDevelopmentNative/BondReleaseTests.cs)
- **接入完成。** 目标构建为 RimWorld `1.7.5014 rev1195`，生产编译零警告、零错误。原生 `--imprint` **114/114**，本批共新增 6 项 Core，包含死亡回调快照、缺失缰绳的旧档迁移、精确到期点及凝胶往返。另通过原生选角与开场 **19/19**、分娩 **13/13**、生育 **18/18**、堕落凝胶 **79/79**。绑定关系及相关仪式、限制和互动回归通过，Def 与四语资源检查通过。 [原生测试记录](../../../../Tests/UterusDevelopmentNative/README.md)
- **提交前复核。** 原锁链离体后，同一人格的健康回调即使继续推进无主进度，也保留已交接的记录和原期限。死亡入口与读档迁移共用失效主人处理，仍有实际锁链的旧从属者可在同次死亡中补齐身份并转换。死亡前记录通知资格，避免旅队及容器归属变化影响合并通知。旧档迁移复用扩展身体快照，覆盖普通角色快照之外的容器和飞船货物。凝胶清源只移除本次核验过的后效实例。新增 1 项 Core 并扩展死亡用例，旧候选 **113/115** 复现两处失败，修订候选 **115/115**。关联原生选角与开场、绑定关系、仪式进度、选角、生命周期、限制核心和互动保护回归通过。 [解绑原生回归](../../../../Tests/UterusDevelopmentNative/BondReleaseTests.cs)
- **自动验证范围。** 额外运行的 `PersonalityTraits` 1.7 链接夹具缺少原版健康类型，在第五批前的提交 `a318bf85` 已复现同一编译错误。本批涉及的人格往返由加载实际游戏程序集的原生专项验证。

#### 第五批界面调整与实机验收

- **解除绑定界面。** 关系页按钮和确认弹窗复用调教管理窗口的深紫底与铜金配色，按钮和弹窗标题统一为「解除绑定」。入口按钮增高至 32 像素，采用深色填充与完整铜色边框，悬停时同步提亮。确认弹窗沿用原主人、阶段、进度及两日后效的预览。 [界面实现](../../../../Sexslavecraft/UI/Dialog_SSCTrainingManagement.Relations.cs)
- **2026-11-21，实机验收通过。** 受测版本为 RimWorld `1.7.5014 rev1195`、第五批提交 `eafab5c1` 及上述界面调整。最后一次同步备份并更新 3 份文件，核对 1068 份运行文件的哈希。维护者确认解除绑定按钮清楚可辨，第五批实机测试未发现问题。

#### 解绑后效计时简化

原版 `Disappears` 接管两日后效的计时、显示与存读档。计时简化候选在 RimWorld `1.7.5014 rev1195` 隔离构建零警告、零错误，原生 `--imprint` **116/116**，选角与开场宿主 **19/19**，Def 镜像检查通过。用例覆盖自然到期、爆发与仪式解锁、原版倒计时读档和旧开发档角色后效迁移。 [解绑计时回归](../../../../Tests/UterusDevelopmentNative/BondReleaseTests.cs)

**凝胶暂停计时。** 续计候选在同一构建下编译零警告、零错误，原生 `--imprint` **116/116**。用例中剩余 **15837 tick** 的人格离体三天，经存读档和加工后按原值植入，下一个健康 tick 正常扣减。植入时原主人失效仍给予 **120000 tick**，无后效的无主人格正常恢复进度。 [凝胶回归](../../../../Tests/UterusDevelopmentNative/OwnerlessPersonalityTests.cs)

**2026-11-21，凝胶续计实机验收通过。** 本机 RimWorld `1.7.5014 rev1195` 同步并核对 3 个候选文件后，维护者确认冷却在凝胶存放期间暂停，植入后按原剩余时间继续计时。植入时原主人失效会给予完整两天后效。实机测试未发现问题。

## 市值与开发绑定复核

2026-11-21，修复时使用 RimWorld `1.7.5014 rev1195` 的原生宿主验证人物市值、殖民地人物财富和交易基价。固定其他估价因素，以 1000 为基准，无主一至四阶依次为 1250、1500、1750、2000，有主为 900、800、700、500。两类零阶均为 1000。四阶等进度绑定后，同一角色的三处读数从 2000 切换到 500。

开发按钮回归从三阶 `0.8123457` 建立绑定，确认锁链保留同一严重度，旧无主记录移除，且可按相同进度解绑。解绑冷却期间再次点击时保留无主记录及原后效。 [开发绑定回归](../../../../Tests/UterusDevelopmentNative/SlaveBreakdownTests.cs)

1.7 隔离构建零警告、零错误。原生 `--imprint` **118/118**，仪式选角与开场 **19/19**，分娩 **13/13**，生育 **18/18**。Def 镜像 **49/49**。

**回归用例精简。** 常驻估价用例保留同一角色四阶绑定前后的原版人物市值与交易基价，以及旧零阶锁链的基础市值。开发绑定用例通过实际按钮验证精确进度交接、解绑和冷却拒绝。复用原修复候选 DLL 重建宿主，`--imprint` **118/118**。 [原生估价回归](../../../../Tests/UterusDevelopmentNative/SexSlaveMarketValueTests.cs)

**2026-11-21，市值与开发绑定实机验收通过。** 本机 RimWorld `1.7.5014 rev1195` 使用提交 `a079cc28` 的开发构建。维护者确认四阶无主角色绑定前后的人物市值倍率分别为 ×2.00 和 ×0.50，开发绑定和解绑均保留原阶段与进度，再绑定冷却正常。

## 文化戒律实机验收

**2026-11-21，实机验收通过。** 在 RimWorld `1.7.5014 rev1195` 同步并校验候选后，维护者确认无主性奴的文化戒律判定测试未发现问题。候选的 1.7 隔离构建零警告、零错误，文化专项 **338/338**，凝胶与淫堕传道专项 **79/79**。 [文化心情、社交和人数回归](../../../../Tests/CultureNative/FeedbackTests.cs) · [淫堕传道回归](../../../../Tests/UterusDevelopmentNative/PartnerCorruptionTests.cs)

## 合并前回归修复与验证

合并前复核以 `8389af78` 及本轮修订为源码基线，使用 RimWorld `1.7.5014 rev1195` 的隔离 1.7 开发构建。阶段查询对顺序正常的 Def 直接扫描，在阈值乱序时沿用规范化排序。敏感度计时热路径的每步零分配断言通过。 [共用阶段查询](../../../../Sexslavecraft/common/TrainingOutcomeUtility.cs) · [计时回归](../../../../Tests/UterusDevelopmentNative/OrgasmAdvanceTests.cs)

| 套件 | 修复及完整套件结果 |
| --- | --- |
| `PersonalityTraits` | 补齐健康追踪、绑定恢复和发作撤权的测试边界，按原主人是否有效核对人格植入后的关系过滤。**167/167**。 |
| `PnaLoad` | 将测试挂链入口接到实际 PNA 补丁使用的等进度挂链签名。**378/378**。 |
| `UterusDevelopmentNative` | 指定仪式结算的实际重载，隔离成长测试的 Harmony 补丁和临时定义，并分别核对首绑撤销旧跨值请求与后续需求刷新触发。**762/762**。 |
| `RomanceControlNative` | 登记无主与解绑 Def，补齐读档的角色枚举边界，提取前捕获原主人供加工及植入场景核对。**1192/1192**。 |

整仓 `All` **52/52 套、6007/6007 项通过**，Def 与语言镜像 **2/2 通过**，失败与未运行均为 0。独立运行的印记与发作 **118/118**、凝胶与淫堕传道 **79/79** 也通过。本次记录为自动验证，分批实机结果见上文。

## 文案定稿与整体验收清单

- 简中字段、显示场景和其它语言资源入口见[第 6 项文案交接清单](../../Reference/History/第6项无主性奴简中文案交接清单.md)。
- 2026-11-21，简中定稿已落实四语，涵盖无主健康状态和长期心情、身份锁定提示、共用敏感度描述、调教请求选项及解绑后效。无主四阶名称与各语言的有主锁链一致。两条绑定选项显示选择信记录的调教员姓名。 [敏感度共用描述](../../../../1.7/Defs/HediffDefs/SSC_OrgasmSensitivity.xml)

下列清单保留原验收范围。实机结论见各批记录，逐项自动结果见合并前回归。

- 核对无主阶段值、特化和装备依次覆盖后的最终许可。
- 核对首次完成直接成形、拒绝与中断，以及符合准入的非性奴、无主和有主角色持续满值跨冷却后均经通用入口发作。覆盖与印记分化同场及两类四阶退出候选。
- 核对爆发中主人和训导官的可选结果、首次两种成形、获批分支存读档、同阶进展和跨阶反馈。覆盖爆发的三阶 `80%` 等进度绑定，以及人格或绑定变化后旧请求失效。
- 核对共用转奴开关采用已保存的选择。开启时，爆发首次成形或无主转有主成功后，对符合资格的目标尝试一次。爆发纯升阶只结算成长。覆盖关闭开关、原版转奴失败后保留 SSC 结果，以及仪式沿用现有转奴结算。
- 解绑转换、再绑定冷却及凝胶往返按[第五批验收点](#第五批验收点)核对。
- 核对二阶升三阶后普通自慰被拒绝、自我调教仍可执行，以及一至四阶的被动强制和自愿接收许可。发作中的求用邀约和人格排泄按各自原有资格验证。
- 核对无主 Hediff 的身份锁、合格调教员指派、无主任教拒绝和训导官失格，以及人格植入时根据主人当时状态恢复归属和原进度。核对调教收益去除无链加分，以及调教管理概况、二阶 RJW 指派和四阶恶堕少女战衣读取共用阶段。 [RJW 指派资格](../../../../Sexslavecraft/ThePatches/Harmony_RJWComfortDesignation.cs) · [战衣阶段门槛](../../../../Sexslavecraft/ThePatches/Harmony_PreventWearEFOutfit.cs)
- 实机观察阶段属性、自动规则与共享保护。重点比较三、四阶 RJW 实际判定、持续心情叠加、经济变化及绑定前后效果。 [测试编写与执行准则](../../Reference/测试编写与执行准则.md)

## 长期心情与文案验收

开发候选的四语标题曾标记开发状态。原生 `--imprint` **116/116**，心情列表回归覆盖一至四阶、零阶和绑定切换。 [长期心情定义](../../../../1.7/Defs/ThoughtDefs/SSC_OwnerlessSlave.xml) · [原生心情列表回归](../../../../Tests/UterusDevelopmentNative/CorruptionMoodTests.cs)

**2026-11-21，长期心情实机验收通过。** 本机 RimWorld `1.7.5014 rev1195` 同步并核对四份文案 XML 后，维护者确认一至四阶的长期心情均出现在心情列表，数值依次为 **−5、−2、+2、+6**。

## 第五批前的主人死亡处理

第五批前，部分功能检查主人是否存活，单纯死亡时锁链仍会保留绑定和严重度。旧档可保留 SSC 主从社交关系和启用的敏感度指令，有主爆发和伤害转移则拒绝死亡主人。第五批把已确认的主人死亡纳入保留进度的自动解绑。 [锁链生命周期](../../../../Sexslavecraft/Hediff/Hediff_ChainOfSexSlave.cs) · [主从关系判定](../../../../Sexslavecraft/common/SSCBondRelationRules.cs) · [死亡转换](../../../../Sexslavecraft/ThePatches/Harmony_BondOwnerDeath.cs)

此前读档按从属侧锁链修复主人缰绳，并保留可解析的死亡主人引用。第五批在 1.7 载入初始化中从从属侧完成迁移，保存转换后的无主进度和后效剩余时间。 [读档绑定修复](../../../../Sexslavecraft/Comps/Comp_Train.cs) · [读档转换入口](../../../../Sexslavecraft/common/Restrictions/SSCRestrictionGameComponent.cs)
