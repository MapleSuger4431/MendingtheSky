namespace MendingtheSky
{
    /// <summary>
    /// 游戏整体状态机（设计文档第五、十一章）。
    /// Idle    = 未开始 / 主界面
    /// Playing = 关卡进行中（唯一会推进灵气消耗、洞生成、熬煮的状态）
    /// Paused  = 暂停（Time.timeScale = 0，UI 显示暂停面板）
    /// Win     = 单关胜利结算
    /// Lose    = 失败结算（灵气归零）
    /// </summary>
    public enum GameState
    {
        Idle,
        Playing,
        Paused,
        Win,
        Lose
    }
}
