# 镜厅雷雨光照

## 人物附近微光

`Level1PalaceStorm` 的“人物附近微光（仅镜厅）”控制随人物移动的小范围柔光，仅在进入镜厅后启用。默认半径 `Player Glow Radius = 1.1` 世界单位，光心相对人物脚底向上 `0.65`；半径之外没有补光。

- `Enable Player Glow`：微光开关。
- `Player Glow Radius / Offset`：半径和相对人物的位置。
- `Player Glow Softness`：从半径的哪个比例开始向外衰减，越小则衰减范围越宽、越柔。
- `Player Glow Tint`：微光颜色。
- `Player Glow Environment`：附近地面、建筑和物品的亮度，默认 `0.085`。
- `Player Glow Character / Rim`：人物表面及轮廓的微光强度，默认 `0.12 / 0.055`。

补光不照亮窗外远景，不改变闪电节奏，也不改变原有管内视野。

场景：`Assets/Scenes/level1.unity`。没有音效、全屏 UI、镜头抖动或新碰撞物。

## 触发

人物完成管道右侧出管动作后，镜厅进入暗场。首次等待默认 3 秒；每次闪电余光结束后固定等待 3 秒，再开始下一次闪电。人物周围微光在等待期间保留。
一次演出包含弱闪（0.06 秒）、短暂回暗（0.12 秒）、主闪（0.15 秒）、余光（0.5 秒）。时长默认乘 0.85～1.15 的随机倍率，强度随机为 0.72～1。
窗外先亮，室内延迟 0.025 秒响应。暂停游戏时间时演出也暂停。R 重置后回到原房间，首次等待重新开始。
编辑状态不自动播放演出，地面光斑、雨和闪电渲染器默认隐藏。

## 调整位置

Hierarchy → `Palace Corridor | glass gallery and cover` → `07 Thunderstorm | lighting and window effects`。
其 `Level1PalaceStorm` 组件包含所有光照节奏与强度参数。

| Inspector 字段 | 用途 |
|---|---|
| Enable Storm | 开关整套演出，关闭后恢复镜厅原亮度 |
| First Flash Delay | 进入镜厅后第一次闪电的等待秒数 |
| Dark Duration | 余光结束到下一次闪电的固定黑暗等待秒数，默认 3 |
| Darkness Fade | 切入暗场的时间 |
| Weak Flash Duration / Strength | 弱闪的时间与强度 |
| Inter Flash Pause | 弱闪与强闪之间的回暗时间 |
| Strong Flash Duration / Afterglow Duration | 强闪时间与余光消退时间 |
| Intensity Range / Duration Variation | 每次闪电强度、节奏的随机范围 |
| Indoor Light Delay | 室内相对窗外亮起的延迟 |
| Lightning Tint | 冷光颜色 |
| Dark Character Exposure / Flash Character Exposure | 人物在暗场、闪光时的亮度 |
| Idle Character Rim / Flash Character Rim | 人物常驻微弱轮廓与闪电轮廓光 |
| Projection Strength | 地面窗格光斑强度 |
| Show Rain / Rain Opacity / Rain Flash Boost | 窗外细雨开关、暗场透明度、闪电时增强 |
| Rain Speed / Rain Slant | 雨速与倾斜方向 |
| Show Lightning Bolt / Bolt Strength / Bolt Width | 窗外闪电线条开关、亮度和宽度 |
| Random Seed | 0 每次随机；其他数值使用固定的随机序列 |

`Layers` 中分别配置天空、远景、花园、建筑和前景遮挡物。每组的 `Dark Exposure` 控制暗场亮度，`Flash Exposure` 控制闪光亮度，`Blue Fill` 控制冷光填充，`Rim` 控制边缘光。
`Window light on floor | no collisions` 下的十个光斑可以独立移动、旋转和缩放。
闪电线条物体位于远景视差层内，名称为 `Lightning bolt | distant parallax`。

## 修改范围

保留已有物体坐标、移动与碰撞逻辑、相机设置、房间光照、管内黑场和漫画布局。
镜厅原有材质通过逐渲染器参数获得闪电响应；关闭效果时恢复原参数。
原代码保存在 `ArtSource/PalaceStorm/Before/`，接入前的场景副本在 `ArtSource/PalaceStorm/Backups/`。
按用户要求，不进行测试、Play 模式演示或截图验证；仅导入、编译、保存场景与构建运行版本。

## ????

???? Dark Exposure?Dark Character Exposure?Dark Pipe Exposure?Idle Character Rim?Rain Opacity ??? 0???????????????????????????????????????????????? Darkness Fade ????????????????????
