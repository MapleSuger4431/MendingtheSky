

## Codely Structured Memories

### User
- [2026-10-20 14:47:50] 全局要求（2026-10-20 用户明确）：任何阶段遇到信息不足、需求歧义或影响手感的决策，必须及时用 ask_user 拷问确认，不要自行猜测关键决策。此规则适用于后续所有开发工作（设计、编码、美术、发布）。

### Feedback
- [2026-10-19 23:16:40] TJGenerators 的 Unity 自定义工具（generate_sprite / query_local_task / import_image_from_url 等）必须由主 agent 通过 execute_custom_tool 直接调用；general-purpose 子代理没有该工具（只有 MCP 侧生成工具），委托批量生图会直接失败且不消耗积分。编辑器重编译/域重载后本地 sprite 任务的通知可能丢失，用 query_local_task(task_id) 可恢复状态（任务在后端照常完成）。批量 sprite 并发上限 5，批次按参考图依赖排序（同色灵石的未煮/发光状态用已煮版做 image_path 参考）。

### Project
- [2026-10-19 22:18:06] 项目「女娲补天」（MendingtheSky）：2D 拼图游戏，核心循环为 背篓(无限仓)→锅(有限容量熬煮)→工作台(拖拽/旋转/填天空缺口网格)，灵气倒计时制，洞闭合返还灵气。用户首个不依靠教程的自主项目，目标是跑通 设计→编码→测试→发布→维护 全流程。目标平台：Steam + TapTap + 微信/抖音小游戏，游戏完全免费无广告，需同时考虑 PC 与移动端。目标用户：以儿童和女性玩家为主，兼顾男性玩家（保留魔法冒险感，避免全粉少女化）；美术基调为可爱Q版+马卡龙色系（提示词见 Docs/美术资源提示词.md）。多语言：简中/繁中/英语，游戏内切换，自研轻量 L10n（不用官方 Localization 包），繁中用独立翻译表，接口与时机定稿于 Docs/设计评审与定稿.md 第七节，阶段 4 随 UIManager 实装，UI 文案一律走 L10n key。引擎：团结(Tuanjie) 1.8.4。设计文档（含完整类图/状态机/六阶段实现顺序）由用户与网页版 DeepSeek 讨论产出。
- [2026-10-19 23:16:40] 关卡参数标定（2026-10-19 用户确定，覆盖原文档数值）：网格行数 10-15；列数运行时按上半屏宽度自动计算（列数≈可用宽度/格宽，钳制 30-70，实测约 40-60 列）；格高按上半屏高度自适应（视口 0.5~0.95，配置 cellSize 仅作上限 0.6）；单洞 9-30 格，包围盒 3-5 行 × 3-6 列；合并洞不受单洞包围盒限制（maxMergedHoleSize=45）；开局洞数 min(holeCount,totalHoles)。实测 14×52=728 格铺满 x 0.03~0.97 / y 0.50~0.95。Phase1DebugDriver 测试键：空格=生成洞、K=全网格变洞、L=重开关卡。
- [2026-10-20 14:47:18] 缺口视觉架构（2026-10-20 定型，GridVisual）：① 16 变体自动瓦片 hole_tile_{0..15}.png（位掩码 1上/2下/4左/8右=同洞邻居；内部格无缝、暴露侧才画亮边与圆角；程序化生成，1.01 微重叠消缝）；② 逐洞外发光色按 holeId%4 循环（白/蓝/粉/绿），只染发光带不染洞主体；③ 边界用程序化无缝光带 edge_glow_h/v.png（沿长度恒定、截面亮芯+halo），配伪 bloom 层（2.4 倍宽、alpha 0.28）；④ 排序 缺口1/发光2/光带3。可调参数在 GridVisual Inspector：edgeBandThickness、edgeGlowWidthMultiplier、edgeGlowAlpha、holeTints。
- [2026-10-20 14:58:35] 阶段2（灵石系统）已完成（2026-10-20）：LingShiState 枚举、LingShiShape（4旋转预计算，(r,c)→(c,-r)）、ShapeCutter（最左上角锚点+大形状优先，重试30次保证≤8块/洞，单格兜底）、ShapePoolConfig（17形状默认池）、LingShiTypeConfig×3（红3s返2/蓝5s返3/金8s返5）、LingShi（方块拼合视觉，状态→精灵：InBasket=dust/Cooked=glow/其余=normal）、InventoryManager（按洞切割发石+冗余补给+AddCookedLingShi）。场景：背篓区=1/4区0.6倍，前壁basket_front（底部20%）遮挡，篓底18%，生成点在篓口上方；Phase2DebugDriver 已替换 Phase1（空格=新洞、K=全洞、L=重开、I=重发灵石）。实测：3洞37格→11块（6/4/4块每洞）、单格块3个、全部InBasket、物理落篓。

### Reference
- [2026-10-19 21:27:56] 设计评审已完成并落盘：Docs/设计评审与定稿.md（2026-10-19）。含 6 个 P0 bug 修法（合并配额回退、暂存 except、暂存只收 Cooked、OnMouseUp 四段分支、灵气只扣 grid==0、灵石绑定 sourceHoleId）、跨平台决策（横屏先行、IRotateInput 抽象）、P2 实现坑清单、定稿汇总表、待办清单。实现阶段按此文档对照编码。
- [2026-10-19 23:20:46] 美术资产位置索引（2026-10-19 生成）：精灵在 Assets/Art/Sprites/（ling_{red|blue|gold}_{cooked|dust|glow}.png 共9、hole_cell.png、edge_horizontal/vertical.png、pot.png、basket.png、qi_bar.png、qi_icon.png、button_mint/peach.png、bg_sky.png、bg_sky_draft.png、game_icon.png）；prefab 在 Assets/Art/Prefabs/（HoleCell / EdgeHorizontal / EdgeVertical，已接线 GridVisual）；场景 SampleScene 的 Background 物体使用 bg_sky（sortingOrder -10）。原提示词见 Docs/美术资源提示词.md。
