# 镜厅守卫

使用本次确认的露面尖冠、镂空叠袖与分片褶裙设计。`ApprovedConcept.png` 为确认图；`Assets/Art/PalaceGuard/GuardParts.png` 为 imagegen 内置工具生成的透明 4×3 分件图集。原图没有被覆盖。

每个守卫有 12 个蒙皮部件和 26 根骨骼，包括双腿、双臂、头部、三组裙片链和挂饰。腿部用两段式反向运动学交替落脚，站立脚的相对位移与角色前进速度对应。手臂反向摆动，身体轻微起伏及前倾，裙片按不同相位摆动。这是骨骼驱动的布料摆动，不是物理布料求解器。

## 场景位置

`level1` → `Palace Corridor | glass gallery and cover` → `08 Guards | three independent patrols`。

三个实例共用 `Assets/Prefabs/Level1/PalaceGuard.prefab`。初始中心按镜厅宽度的 22%、52%、82% 放置，巡逻半径默认 2.3 世界单位。保留现有相机、管道、家具及玩家位置。

## Inspector 调整

选中某个 `Guard`，调整 `Level1PalaceGuard`：

- `Patrol Offsets`：相对于游戏开始位置的左右巡逻边界；选择物体时会画出范围线。
- `Move Speed / Acceleration`：巡逻速度及加减速。
- `Body Width`：整体横向比例，当前为 1.2（加宽 20%），身高不变；转身和步频会适配此比例。
- `Pause Seconds`：到达目标后的随机停留范围，默认 0.8～2.8 秒。
- `Minimum Travel`：每次随机目标的最短距离。
- `Start When Player Arrives`：默认等玩家出管进入镜厅后开始巡逻。
- `Initially Face Left`：初始朝向。
- `Random Seed`：0 为每次随机，非零为固定序列。
- `Stride Length / Foot Lift / Arm Swing`：步幅、抬脚和摆臂。
- `Forward Lean / Body Bob / Cloth Sway`：身体前倾、起伏及布料摆动。
- `Turn Seconds`：转身收窄、翻转、展开的总时长。

移动单个实例可调整初始位置；三个守卫各自选目标和停留，不共享节拍。默认路线互不交叉。当前实现为巡逻与动作，没有添加察觉、追击、攻击或伤害机制，也没有实体阻挡碰撞。

守卫接入镜厅原有闪电、黑场、玩家附近微光和管内视野；守卫不自带发光。守卫位于靠近摄像机的一层：`Visual | 12 skinned parts` 的 `Sorting Group → Order in Layer` 为 50，显示在雕像（42）及遮挡物（43）前方；部件内部顺序保持原配置。

## 素材生成说明

模式：内置 imagegen，参考确认图生成透明分件图集。生成要求：保持黑金剪影、露出侧脸及镂空服饰；四列三行；依次为头、躯干、近臂、远臂、近腿、远腿、内裙、前罩裙、后镂空罩裙、肩饰、带鞘佩剑、腰链垂饰；部件独立且接缝处补足，背景和镂空使用透明通道。

场景修改前备份在 `ArtSource/PalaceGuard/Backups`。按用户要求不运行游戏、自动测试或截图验证。编译及构建结果不能代替用户对比例、装配和行走观感的验收。
