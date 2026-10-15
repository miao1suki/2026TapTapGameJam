# 标注图物体映射

源图 `map.mvmap (3).json`，范围 X=27～99、Y=−52～0；只实现用户标注部分，其他区域不导入。紫色区域作为房间边界，不生成物体。

| 源 ID | 内容 | 预制体/设置 |
|---|---|---|
| room_rfwrom30jy | 门 | Door，1×2 |
| room_yup68gintw、room_5eic9h9651 | 保持按钮 | DoorButton / Latched，绑定上述门 |
| room_lnhrgmpay6 | 松开恢复按钮 | DoorButton / Held，绑定上述门 |
| room_xipjpj86iu | 单向平台 | OneWayPlatform，6×1 |
| room_smcflrqwv8 + room_5p5k6o4zek 起点 | 转弯水流 | Blue_WaterWay，左/上/左分段 |
| room_lsrckjto6f + room_sffoqtn0x9 起点 | 中间向下水流 | Blue_WaterWay / Down |
| room_w00jx93c1y + room_pn08djxyzj 起点 | 最右向下水流 | Blue_WaterWay / Down，以确认后的方向为准 |
| room_barp4u06n5 + room_yb6c59ouvs 起点 | 上部气泡柱 | Blue_WaterBubble |
| room_226dv2y0ne + room_op8mytvr7l 起点 | 下部气泡柱 | Blue_WaterBubble |
| room_a0gcanoal5、room_1z91em9jvf、room_s1t0ee8lq0、room_eqo9mlzvfp、room_6ogwmzxzvm、room_nu9vd9a33z、room_61iw8to579、room_a0jf4eq7eb、room_omqug6pc8n、room_wlqehrlgd6、room_thx9uiym0p、room_uxe4bh381u | 下层标注水域（源文件未设置颜色） | Blue_WaterSource，按精确几何拆分 |
| room_cqetcbj5xn | 未明确的第二起点标记 | 仅保留规划注释，不生成实体 |
| 范围内其余白灰区域 | 墙体 | 白方块，不猜新增玩法 |

三房间：上层 X27～73/Y−52～−23，下层 X27～62/Y−22～0，右侧 X74～99/Y−52～−8；未划分的截图剩余区域独立管理。深蓝标记转为方向配置，不是白方块。没有根据范围外的图形添加道具或房间。
