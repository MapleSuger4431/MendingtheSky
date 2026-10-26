namespace MendingtheSky
{
    /// <summary>
    /// 拖拽预览的三态提示（2026-10-25 用户定稿）：
    /// - Valid：几何可放，且本洞形状清单里还有这个形状的剩余额度 → 绿色。
    ///   额度 = 清单条目数 − 洞内已放的非冗余同形状块数，实时计算：放进去少一个、抠出来恢复一个；
    ///   冗余补给灵石（sourceHoleId == -1）不占额度，一律绿色；
    /// - ShapeNotInHole：几何可放，但清单里没有这个形状、或该形状的额度已经用完 → 黄色。
    ///   仍然允许放下去（自由放置），只是警告：洞口填满时会判定不通过、不愈合；
    /// - Invalid：越界 / 不是缺口格 / 跨洞 / 未煮熟 → 红色。
    /// 未登记形状清单的洞（EditMode 测试、调试键生成的洞）没有可校验的清单，一律按 Valid 显示。
    /// </summary>
    public enum PreviewHint
    {
        Invalid = 0,
        Valid = 1,
        ShapeNotInHole = 2
    }
}
