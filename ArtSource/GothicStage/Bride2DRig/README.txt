哥特剧场：新娘二维骨骼美术源文件

Bride_Parts_Source.png、Parts/：原始分件图与透明部件贴图。
parts_metadata.json：部件位置数据。
rig.json：骨骼层级、网格、UV、蒙皮权重。
walk_motion.json：适配角色后的行走动作曲线。
MotionSource/：原始动作数据及来源说明。

Unity 使用资源：Assets/Art 下的 Textures/GothicStage、Meshes/GothicStage、Materials/GothicStage。
动作数据：Assets/Data/GothicStage/ApprovedWalk.asset。
疾跑动作：Assets/Data/GothicStage/SprintRun.asset，由行走数据的 sprint 字段引用。
交互脚本：Assets/Scripts/GothicStage。
场景：Assets/Scenes/Gothic_Theatre_Bride_Walk.unity。

动作数据来源：CMU Graphics Lab Motion Capture Database，subject 07，trial 01。
https://mocap.cs.cmu.edu/search.php?subjectnumber=7
The data used in this project was obtained from mocap.cs.cmu.edu.
The database was created with funding from NSF EIA-0196217.

2026-11-11：Shift 疾跑
按住左或右 Shift + A/D（或方向键）疾跑；松开 Shift 回到行走。
Shift 单独按住保持站立。Tab/滚轮拉近观察，R 复位，Esc 退出独立游戏。
跑步使用独立的 128 帧骨骼动作：完整左右腿周期 0.72 秒，步幅 600 像素。
速度约 2.451 Unity 单位/秒（行走约 0.913），走跑切换保持同一落脚相位。
run_motion.json：适配后的跑步骨骼采样与落脚诊断数据。
原始跑步来源：CMU subject 09, trial 01；采样帧 35–122，120 Hz。
https://mocap.cs.cmu.edu/search.php?subjectnumber=9
MotionSource/09.asf、09_01.amc 保留原始数据。
沿行进方向投影，周期曲线拟合，固定腿长，支撑期脚底约束及双骨骼 IK；
加入服装、面纱和头发的延迟摆动，并沿用现有收窄展开转身。
验证记录保存在 Logs/SprintMotionValidation.json；记录的是控制器逻辑输入检查。

2026-11-12：空格跳跃
jump_poses.json：在现有 35 骨骼上手工设计的六个姿势，不是动作捕捉数据。
包含蓄力、蹬地、上升、顶点收腿、着地准备和落地压缩，腿段保持 130 像素。
Unity 数据：Assets/Data/GothicStage/Jump.asset，ApprovedWalk.jump 引用。
控制器进行姿势插值、重力轨迹与接地 IK；默认起跳高度 1，重力 14。
支持原地/行走/疾跑跳跃，运行检查见 Logs/Jump/Validation.json。
