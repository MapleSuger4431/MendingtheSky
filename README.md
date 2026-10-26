# 女娲补天（MendingtheSky）

2D 拼图游戏：背篓取灵石 → 下锅熬煮 → 拖到天空缺口里拼满 → 洞口愈合返还灵气。
灵气持续消耗，归零失败。目标平台 Steam / TapTap / 微信 / 抖音小游戏，完全免费无广告。

- 引擎：团结引擎 1.8.4（Unity 2022.3 LTS 兼容）
- 语言：简体中文 / 繁體中文 / English（游戏内可切换）

---

## 快速开始

1. 用团结引擎打开项目根目录（`ProjectSettings/ProjectVersion.txt` 会锁定版本）；
2. 打开场景 `Assets/Scenes/SampleScene.scene`；
3. 点 Play。开局的背篓里会有本关各洞口切出的灵石，拖进锅里煮熟，再拖到上半屏缺口里拼满。

运行所需的配置资产都在 `Assets/ScriptableObjects/`，由场景里的 `GameManager` 等组件在 Inspector 中引用。

---

## 目录速览

| 路径 | 内容 |
|---|---|
| `Assets/Scripts/Data/` | 关卡、形状池、灵石类型等 ScriptableObject 定义 |
| `Assets/Scripts/Gameplay/` | 规则与数据模型（`Hole` / `LingShi` / `ShapeCutter` / `MatchChecker`） |
| `Assets/Scripts/Systems/` | 管理器（`GameManager` / `GridManager` / `InventoryManager` / `PotManager` / `QiManager`） |
| `Assets/Scripts/View/` | 表现层（`GridVisual` / `UIManager`） |
| `Assets/Scripts/Input/` | 输入（`DragController`） |
| `Assets/Scripts/Localization/` | 轻量多语言（`L10n` + 词表 + `LocalizedText`） |
| `Assets/Scripts/Debug/` | 调试快捷键 `DebugCheats` |
| `Assets/ScriptableObjects/` | 关卡 `Level01`、形状池、灵石类型、本地化词表 |
| `Assets/Art/Prefabs/` | 缺口格、边界光条、灵石、灵石方块 |
| `Assets/Tests/EditMode/` | EditMode 单测（64 条） |
| `Docs/` | 设计评审、架构说明、美术资源提示词 |

---

## 调试快捷键

仅测试用（`DebugCheats`，正式版可移除该组件）：

| 键 | 作用 |
|---|---|
| Q | 灵气 +20 |
| T | 开关灵气消耗 |
| 空格 | 生成一个新洞并给背篓发对应的灵石 |
| K | 整个网格变成一个大洞（渲染/性能测试） |
| L | 重开关卡 |
| I | 重发背篓初始灵石 |
| H | 取下鼠标所指洞口内已放置的灵石 |
| J | 打印每个洞的轮廓、形状清单、可解性 |

---

## 测试

Unity 菜单 `Window → General → Test Runner`，选 **EditMode**，Run All。
命令行（可选）：

```
<Tuanjie 安装目录>/Editor/Tuanjie.exe -batchmode -projectPath <项目路径> -runTests -testPlatform EditMode -testResults results.xml
```

---

## 文档

| 文档 | 内容 |
|---|---|
| `Docs/架构说明.md` | **先看这个**：架构、启动流程、主调用链、数据模型、扩展点、技术债 |
| `Docs/设计评审与定稿.md` | 规则决策记录：6 个 P0 问题修法、跨平台决策、多语言方案、发布合规 |
| `Docs/美术资源提示词.md` | 已生成美术资产的提示词与风格约束 |

---

## 当前进度

已完成：网格与洞口、灵石系统、拖拽/旋转/放置、熬煮与灵气、洞口闭合与胜利、美术资源、中文字体、多语言（简/繁/英）。

待做：无尽模式、关卡数据库接入与多关卡、手感/视觉打磨、存档、发布准备。
