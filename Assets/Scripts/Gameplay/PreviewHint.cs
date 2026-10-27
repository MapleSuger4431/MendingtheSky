namespace MendingtheSky
{
    /// <summary>
    /// 拖拽预览的两态提示（2026-10-27 方案 D 定稿）：
    /// - Valid：几何可放 → 绿色；
    /// - Invalid：越界 / 不是缺口格 / 跨洞 / 未煮熟 → 红色。
    ///
    /// 背景：形状清单已整体移除，愈合不再校验形状组合（填满即愈合），
    /// 所以原来表示「清单里没有这个形状」的黄色提示不再有意义。
    /// </summary>
    public enum PreviewHint
    {
        Invalid = 0,
        Valid = 1
    }
}
