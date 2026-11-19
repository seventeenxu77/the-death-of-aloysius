# 户外出口区：混凝土地面与箱子遮挡

**状态：已撤回，未经用户确认不得接入。** 用户要求先看设计图片。2026-11-12 已从 level1 删除本次新增出口根物体，并将本次新增实现文件移出 Assets，归档到 `ArtSource/OutdoorCorridor/Withdrawn/Implementation-20261112`。其余用户场景调整保留。以下仅为撤回版本的历史说明，不代表当前场景。

场景：`Assets/Scenes/level1.unity`

根物体：`Outdoor Exit | concrete ground and crate cover`

本轮只搭建可编辑的场景和视差层，不接入管口跳下动作、守卫 AI、潜行判定或镜头状态切换。没有运行 Play、测试或截图。进入房间的既有相机行为保持不变；请在 Scene 视图查看出口区域，或选中根物体后按 F。

## 场景结构

- `01 | Concrete ground`：从房间右边界 X=5 开始，长 30 的连续实体混凝土地面，顶部 Y=-2.233852；带 BoxCollider2D。没有桥洞或架空地台。
- `02 | Crates`：三组主遮挡箱，可逐个修改 Transform。都是独立 prefab 实例，带用于后续交互的触发检测框；当前不实现推拉、阻挡或敌人视线判定。
- `03 | Rear yard`：独立围墙、墙沿和后排箱子，Z≈4–5。
- `04 | Middle distance`：独立的普通建筑体块、屋沿、门窗，Z≈12–13。
- `05 | Far distance`：独立建筑体块，Z≈24–25。
- `06 | Sky only`：沿用夜空图片作为纯天空背景，不含地面、建筑或箱子。左边界裁切避免覆盖原房间以外的边缘。
- `07 | Near edge crates`：前景局部箱子，产生略快的近景视差。
- `08 | Suspended outlet and landing markers`：出口、落点的编辑标记，不会在游戏里画标识。

原管道没有移动、复制或重新缩放。当前右端出口参考位置约为 (5.21,0.70)，离混凝土地面约 2.93 单位；下方保留空地，第一组遮挡位于出口右侧约 4.3 单位处。

## 视差方式

背景是可独立选择的真实场景网格，具有宽、高和厚度，不使用整张走廊合成图，不使用模糊或景深后处理。

每组 `Level1OutdoorParallax` 绑定现有场景 Camera：

| 层 | Horizontal Motion | Vertical Motion |
| --- | --- | --- |
| 地面及主遮挡 | 1 | 1 |
| 后院围墙 | 0.82 | 0.9 |
| 中景建筑 | 0.55 | 0.7 |
| 远景建筑 | 0.28 | 0.5 |
| 夜空 | 0.03 | 0.1 |
| 最近箱子 | 1.12 | 1 |

1 表示与地面同速，小于 1 表示较慢的远景，大于 1 表示较快的前景。正交相机仅改变 Z 不会自动产生平移视差，因此脚本基于相机位移分别调整各层。相机缩放仍通过真实投影影响物体，不把背景锁定成屏幕图片。

进入管道后的黑场处理同步使用现有 `Level1Lighting.PipeVisionBlend`，后续镜头跟随时不会泄露外部环境。

## 资产与保存

- 箱子 prefab：`Assets/Art/OutdoorCorridor/Prefabs/PlainWoodCrate.prefab`
- 材质：`Assets/Art/OutdoorCorridor/Materials/`
- 平涂剪影 shader：`Assets/Art/Shaders/Level1/OutdoorSilhouette.shader`
- 场景管理引用：`Level1OutdoorArea`
- 编辑器创建入口：`Aloysius/Level1/Create Outdoor Concrete Exit`（已有出口区时拒绝重复覆盖）
- 创建前的编辑器实时场景备份：`ArtSource/OutdoorCorridor/SceneBackups/level1-before-outdoor-20261112-192654.unity`

地面碰撞体已放置，但角色当前仍受原房间移动范围和管道末端限制。后续接出管动作时，需要同时接通角色落地支撑与移动边界，再添加管内聚焦／户外跟随相机。
