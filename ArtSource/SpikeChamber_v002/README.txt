尖刺房 v002 — 按确认构图重做的第一个房间

场景仍是 Assets/Scenes/level1.unity。
仅替换 SerpentCampaign.rooms[1] 的尖刺房，不重建其他房间。
层级：任务板关卡 | Serpent Campaign / 03 尖刺房 | 已确认构图 v002。
场景素材：室内远景、近景柱列与楼梯平台。按最新要求移除了静态蛇图层，巨蛇后续作为独立角色加入。
完整房间宽 26 世界单位；角色保持原尺寸，镜头扩大到整间大厅，让人物显小、巨蛇显大。
近景楼梯持续可见，使用连续高度支撑面，可以直接 A/D 走上去，不需要跳台阶。
尖刺在二层左边，开关在其下方；二层右端的门紧贴右墙，门槛与平台齐平。
沿用原有机关流程：开关激活尖刺、引导巨石命中；之后到右门按 W 继续。

进入方式
项目根目录双击 PlaySpikeRoom.cmd，直接进入这个房间。
Unity Play 模式使用 GM：2dtrans spike。
原大床房和后续关卡未做美术改造。主角两个新动作也仍未接入。

素材来源
ApprovedConcept.png 是用户确认的构图。
Assets/Art/SpikeChamber_v002/Interior.png、Serpent.png、Foreground.png
由内置 imagegen 基于该构图分层生成，前景与蛇图含真实透明通道。
开关和尖刺复用项目已有可交互分件，不烘进背景。
分层图的描绘不保证逐像素等同原概念图；构图、比例、楼梯、右墙出口以原图为目标。Serpent.png 仅保留为历史素材，不再放置在场景内，生成器也不再添加此图层。

修改入口
Aloysius/Level1/Apply Approved Spike Chamber Only：仅重建此房间。
不要运行旧的 Build Task Board Chapter 来覆盖本房间；那是上一版整章生成器。
备份在本目录 Backups。

本次验证（2026-11-16 23:31）
9 项局部检查全部通过：楼梯初始可见、门靠右墙、尖刺在左、门槛对齐、正常步行上楼、顶层高度、通路直达门口、开关可达、下层不可误触上层门。
角色上楼与走到右门使用控制器的连续行走，没有用传送代替这段通路检查。
RoomOverview.png、StairTop.png、RightDoor.png 是独立运行版实际相机渲染。
为了避免与邻近旧场景画层相交，仅将本房间在 Scene 中单独排开；入口采用短距离竖直下落、渐暗、进入新房间后渐亮。
