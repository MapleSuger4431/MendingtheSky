namespace MendingtheSky
{
    /// <summary>
    /// 新手引导的步骤。顺序即游戏主循环：取石 → 入锅 → 熬煮 → 拖到缺口 → 填满 → 愈合。
    /// 只用于第一关的首次游玩，完成后写入 PlayerPrefs 不再出现。
    /// </summary>
    public enum TutorialStep
    {
        /// <summary>未在引导中。</summary>
        Inactive = 0,

        /// <summary>把背篓里的石头拖进锅里。</summary>
        DragToPot = 1,

        /// <summary>等锅里的石头煮熟。</summary>
        WaitCooking = 2,

        /// <summary>把煮好的石头拖到天空的缺口。</summary>
        DragToGrid = 3,

        /// <summary>把整个缺口填满。</summary>
        FillHole = 4,

        /// <summary>洞愈合后的收尾提示。</summary>
        Celebrate = 5,

        /// <summary>引导结束。</summary>
        Done = 6
    }
}
