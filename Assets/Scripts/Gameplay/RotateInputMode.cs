namespace MendingtheSky
{
    /// <summary>
    /// 灵石旋转的输入方式（设置面板「旋转方式」三态循环）。
    ///
    /// 默认 Buttons：移动端显示屏幕上的顺时针/逆时针按钮，桌面端用 R / Shift+R。
    /// 提供 Gesture 是因为单手拖拽时另一只手还要够右侧按钮，儿童玩家容易够不到；
    /// Both 让两种方式并存，玩家按习惯自选。
    /// </summary>
    public enum RotateInputMode
    {
        /// <summary>屏幕按钮（移动端）/ 键盘 R、Shift+R（桌面端）。默认值。</summary>
        Buttons = 0,

        /// <summary>只有手势：拖拽时第二根手指上滑逆时针、下滑顺时针。</summary>
        Gesture = 1,

        /// <summary>按钮与手势同时可用。</summary>
        Both = 2
    }
}
