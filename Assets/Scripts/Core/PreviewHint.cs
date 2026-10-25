namespace MendingtheSky
{
    /// <summary>
    /// 拖拽预览的三态提示（2026-10-25 用户定稿）：
    /// - Valid：几何可放，且这个形状属于本洞的形状清单 → 绿色；
    ///   冗余补给灵石（sourceHoleId == -1）没有来源洞，可以顶任意位置，也一律绿色；
    ///   注意：绿色只表示「形状属于本洞」，不保证清单数量还够——数量超出会在洞口填满判定时提示；
    /// - ShapeNotInHole：几何可放，但这个形状不在本洞清单里（多半是别的洞切出来的）→ 黄色。
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
