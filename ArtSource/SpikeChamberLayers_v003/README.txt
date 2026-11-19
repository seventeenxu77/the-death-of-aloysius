尖刺大厅分层 v003

游戏场景：Assets/Scenes/level1.unity，房间 spike。
运行时用GM命令 2dtrans spike 前往查看。

LayeredHall_WithSerpent.png：通过Unity临时相机导出的合成效果图，带静态主角比例参照。没有移动玩家，也没有进入Play模式。
FarInterior.png：去掉中层柱列后补全的远墙/地板/天花板。
MiddleArcade.png：独立的透明中层拱廊。两张美术由内置图像生成工具，参考原Interior.png拆分。
其余六个前景组件用原Foreground.png的原始像素裁成Sprite资产，位置、比例及楼梯/平台轮廓一致。

从后向前：
01 远墙、远地面、天花板；
02 黑金四头蛇，46骨骼，独立微动，无床背景；
03 中层柱廊，遮挡蛇身；
04 平台、拱洞、楼梯与上层柱拱；
05 玩家行走层；
06 近景柱、右侧墙、地面前沿。

原Interior/Foreground合并渲染对象停用保留。蛇根节点可直接调位置、比例；材质FarSerpent.mat可调Exposure；各视差层PalaceDepthLayer可调Parallax。
既有楼梯碰撞、尖刺、开关、右侧门、房间边界、相机框架未重新摆放。
