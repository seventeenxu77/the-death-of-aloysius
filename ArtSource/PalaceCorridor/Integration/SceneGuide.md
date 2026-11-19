# level1 宫殿玻璃走廊

接入日期：2026-11-12。使用同一个 `Assets/Scenes/level1.unity`，原房间右侧新增 24 个世界单位的走廊，两段连续窗廊。

## 场景层级与调整

- `Palace Corridor | glass gallery and cover`：走廊总控制，Inspector 可调镜头大小、跟随平滑时间、偏移、整体亮度、角色亮度及移动边界内缩。
- `01 Bounds and ground`：起点、终点和落地地面。地面与原房间同高。
- `02 Architecture | two window gallery sections`：两段窗框、吊灯、红毯建筑精灵及窗外底色。
- `03 Parallax | distant palace and trees`：远处宫殿和树线，`Motion Ratio` 默认 0.26。
- `04 Parallax | garden hedges`：近处花坛，`Motion Ratio` 默认 0.62。数值越接近 1，镜头移动时越接近建筑的移动速度。
- `05 Cover props | move individual objects`：雕像、半身像和基座、屏风、花盆、矮柜。直接调整每个物体的 Transform；它们位于角色前方，可产生视觉遮挡，并沿用接触高亮。
- `06 Wall art`：挂画，位于角色后方，作为墙上装饰。

遮挡物目前是视觉遮挡和触碰检测；本次没有新增守卫巡逻、视线检测或躲藏判定。

## 连接与操作

原管道位置、尺寸和曲线保留，右口新增跟随管道的 `Right mouth | corridor exit` 标记。
管内按 D 向右，抵达出口后原地转身，保持管内 Crawl Speed 倒退到边缘，再反向播放攀入动作，身体下放至双手悬挂，短暂停留后松手落下。落地后继续使用 A/D、Shift、Space。
2026-11-13 修订取消了固定 1.25 秒直接平移出管口的逻辑。
管道参数在玩家的 `Level1TraversalController` 中调整：右口出口开关与标记、`Right Exit Turn Duration`（默认 0.6 秒）、`Right Exit Lower Duration`（2.4 秒）、`Right Exit Hang Pause`（0.55 秒）、`Right Exit Pose Blend`（0.18 秒）、`Release To Fall Duration`（0.3 秒）。倒退速度沿用 `Crawl Speed`，不另行加速。
进入管道时镜头缩小范围聚焦人物；出管后切换为走廊跟随。漫画面板在整个出管动作结束后撤掉，之前的位置和警觉计时保持。
R 沿用原重置操作，回到房间并恢复原房间移动边界与镜头。

## 素材

PNG 位于 `Assets/Art/Textures/Level1/PalaceCorridor/`。
透明素材由 imagegen 从已确认设计拆分生成，提示词记录在 `GenerationNotes.json`。
道具从同一张透明贴图用 Unity 原生 Sprite 矩形切片，元数据在 `PropSlices.json`；没有重新绘制或修改贴图像素。
窗格开口透明，花园与窗框是不同物体，由运行时相机位移驱动实际视差。

## 保存和范围

安装前已通过 Unity 保存当时打开的场景副本，包括未保存的用户布局，备份为同目录下 `level1-before-palace-*.unity`。
修改前的相关代码保存在 `ScriptsBefore/`。
仅执行素材导入、代码编译、场景保存及游戏构建，没有执行测试、Play 模式或游戏截图。视觉和操作效果由用户确认。
