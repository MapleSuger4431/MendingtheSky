namespace MendingtheSky
{
    /// <summary>
    /// 边界方向。约定：网格行向下增大（row 0 为最上一行），
    /// Up = row-1，Down = row+1，Left = col-1，Right = col+1。
    /// </summary>
    public enum EdgeDirection
    {
        Up,
        Down,
        Left,
        Right
    }
}
