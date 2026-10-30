namespace MendingtheSky
{
    /// <summary>
    /// 新手引导的步骤。顺序即游戏主循环：看目标 → 取石 → 入锅 → 熬煮 → 拖到缺口 → 填满 → 愈合。
    /// 只用于第一关的首次游玩，完成后写入 PlayerPrefs 不再出现。
    /// </summary>
    public enum TutorialStep
    {
        /// <summary>未在引导中。</summary>
        Inactive = 0,

        /// <summary>开场说明：目标是什么、大概怎么操作，点「开始」进入下一步。</summary>
        Welcome = 1,

        /// <summary>把背篓里的石头拖进锅里。</summary>
        DragToPot = 2,

        /// <summary>等锅里的石头煮熟。</summary>
        WaitCooking = 3,

        /// <summary>把煮好的石头拖到天空的缺口，并顺手说明怎么旋转。</summary>
        DragToGrid = 4,

        /// <summary>把整个缺口填满。</summary>
        FillHole = 5,

        /// <summary>洞愈合后的收尾提示。</summary>
        Celebrate = 6,

        /// <summary>引导结束。</summary>
        Done = 7
    }
}
