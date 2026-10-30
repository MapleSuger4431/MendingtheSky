using System;
using UnityEngine;

namespace MendingtheSky
{
    /// <summary>
    /// 玩家可修改的设置（持久化到 PlayerPrefs）。
    ///
    /// 当前设置项：
    /// - ShowPlacementPreview（放置预览高亮）：默认开启。
    ///   关闭后拖拽灵石时不再显示绿/橙/红预览格，
    ///   但放置校验与死锁防护仍然生效（只是不给视觉提示），
    ///   供想挑战更高难度的玩家使用。
    ///
    /// 扩展方式：后续新增设置项时沿用同一模式——
    /// 私有 PlayerPrefs key + 带缓存的静态属性 + OnChanged 事件，
    /// UI（UIManager 设置面板）监听 OnChanged 刷新显示。
    /// </summary>
    public static class GameSettings
    {
        /// <summary>「显示放置预览高亮」的 PlayerPrefs 键。</summary>
        private const string KeyShowPlacementPreview = "settings.showPlacementPreview";

        /// <summary>音乐音量（0-1）的 PlayerPrefs 键。</summary>
        private const string KeyMusicVolume = "settings.musicVolume";

        /// <summary>音效音量（0-1）的 PlayerPrefs 键。</summary>
        private const string KeySfxVolume = "settings.sfxVolume";

        /// <summary>音乐静音开关的 PlayerPrefs 键。</summary>
        private const string KeyMusicMuted = "settings.musicMuted";

        /// <summary>音效静音开关的 PlayerPrefs 键。</summary>
        private const string KeySfxMuted = "settings.sfxMuted";

        /// <summary>旋转输入方式（按钮 / 手势 / 两者）的 PlayerPrefs 键。</summary>
        private const string KeyRotateMode = "settings.rotateMode";

        /// <summary>顺时针旋转键的 PlayerPrefs 键。</summary>
        private const string KeyRotateClockwise = "settings.key.rotateCW";

        /// <summary>逆时针旋转键的 PlayerPrefs 键。</summary>
        private const string KeyRotateCounterClockwise = "settings.key.rotateCCW";

        /// <summary>顺时针旋转的出厂键位。</summary>
        public const KeyCode DefaultRotateClockwiseKey = KeyCode.D;

        /// <summary>逆时针旋转的出厂键位。</summary>
        public const KeyCode DefaultRotateCounterClockwiseKey = KeyCode.A;

        private const float DefaultVolume = 0.8f;

        /// <summary>任意设置变化事件：设置面板与预览/音频系统监听后立即生效。</summary>
        public static event Action OnChanged;

        /// <summary>缓存，避免每帧读 PlayerPrefs。</summary>
        private static bool? cachedShowPlacementPreview;
        private static float? cachedMusicVolume;
        private static float? cachedSfxVolume;
        private static bool? cachedMusicMuted;
        private static bool? cachedSfxMuted;
        private static RotateInputMode? cachedRotateMode;
        private static KeyCode? cachedRotateClockwise;
        private static KeyCode? cachedRotateCounterClockwise;

        /// <summary>
        /// 是否显示放置预览高亮（默认 true）。
        /// set 时会写入 PlayerPrefs 并触发 OnChanged。
        /// </summary>
        public static bool ShowPlacementPreview
        {
            get
            {
                if (!cachedShowPlacementPreview.HasValue)
                {
                    cachedShowPlacementPreview = PlayerPrefs.GetInt(KeyShowPlacementPreview, 1) == 1;
                }
                return cachedShowPlacementPreview.Value;
            }
            set
            {
                if (ShowPlacementPreview == value)
                {
                    return;
                }
                cachedShowPlacementPreview = value;
                PlayerPrefs.SetInt(KeyShowPlacementPreview, value ? 1 : 0);
                PlayerPrefs.Save();
                OnChanged?.Invoke();
            }
        }

        /// <summary>
        /// 切换「放置预览高亮」并返回新值（供设置按钮直接使用）。
        /// </summary>
        public static bool TogglePlacementPreview()
        {
            ShowPlacementPreview = !ShowPlacementPreview;
            return ShowPlacementPreview;
        }

        /// <summary>音乐音量（0-1，默认 0.8）。写入时立即持久化并广播 OnChanged。</summary>
        public static float MusicVolume
        {
            get
            {
                if (!cachedMusicVolume.HasValue)
                {
                    cachedMusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyMusicVolume, DefaultVolume));
                }
                return cachedMusicVolume.Value;
            }
            set
            {
                float clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(MusicVolume, clamped))
                {
                    return;
                }
                cachedMusicVolume = clamped;
                PlayerPrefs.SetFloat(KeyMusicVolume, clamped);
                PlayerPrefs.Save();
                OnChanged?.Invoke();
            }
        }

        /// <summary>音效音量（0-1，默认 0.8）。</summary>
        public static float SfxVolume
        {
            get
            {
                if (!cachedSfxVolume.HasValue)
                {
                    cachedSfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeySfxVolume, DefaultVolume));
                }
                return cachedSfxVolume.Value;
            }
            set
            {
                float clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(SfxVolume, clamped))
                {
                    return;
                }
                cachedSfxVolume = clamped;
                PlayerPrefs.SetFloat(KeySfxVolume, clamped);
                PlayerPrefs.Save();
                OnChanged?.Invoke();
            }
        }

        /// <summary>
        /// 音乐是否被静音（默认 false）。
        /// 静音是独立开关而不是把音量写成 0：这样滑块位置与音量值不会丢，
        /// 取消静音时自动回到原来的音量。
        /// </summary>
        public static bool MusicMuted
        {
            get
            {
                if (!cachedMusicMuted.HasValue)
                {
                    cachedMusicMuted = PlayerPrefs.GetInt(KeyMusicMuted, 0) == 1;
                }
                return cachedMusicMuted.Value;
            }
            set
            {
                if (MusicMuted == value)
                {
                    return;
                }
                cachedMusicMuted = value;
                PlayerPrefs.SetInt(KeyMusicMuted, value ? 1 : 0);
                PlayerPrefs.Save();
                OnChanged?.Invoke();
            }
        }

        /// <summary>音效是否被静音（默认 false），语义同 MusicMuted。</summary>
        public static bool SfxMuted
        {
            get
            {
                if (!cachedSfxMuted.HasValue)
                {
                    cachedSfxMuted = PlayerPrefs.GetInt(KeySfxMuted, 0) == 1;
                }
                return cachedSfxMuted.Value;
            }
            set
            {
                if (SfxMuted == value)
                {
                    return;
                }
                cachedSfxMuted = value;
                PlayerPrefs.SetInt(KeySfxMuted, value ? 1 : 0);
                PlayerPrefs.Save();
                OnChanged?.Invoke();
            }
        }

        /// <summary>实际输出的音乐音量：静音时为 0，否则等于 MusicVolume。音频系统只读这个值。</summary>
        public static float EffectiveMusicVolume => MusicMuted ? 0f : MusicVolume;        /// <summary>实际输出的音效音量：静音时为 0，否则等于 SfxVolume。</summary>
        public static float EffectiveSfxVolume => SfxMuted ? 0f : SfxVolume;

        /// <summary>
        /// 旋转输入方式（默认 Buttons）。写入时持久化并广播 OnChanged，
        /// 拖拽控制器据此决定是否显示屏幕按钮、是否接收双指手势。
        /// </summary>
        public static RotateInputMode RotateMode
        {
            get
            {
                if (!cachedRotateMode.HasValue)
                {
                    int raw = PlayerPrefs.GetInt(KeyRotateMode, (int)RotateInputMode.Buttons);
                    // 夹紧到合法区间，避免改过枚举后读到历史脏值
                    if (raw < (int)RotateInputMode.Buttons || raw > (int)RotateInputMode.Both)
                    {
                        raw = (int)RotateInputMode.Buttons;
                    }
                    cachedRotateMode = (RotateInputMode)raw;
                }
                return cachedRotateMode.Value;
            }
            set
            {
                if (RotateMode == value)
                {
                    return;
                }
                cachedRotateMode = value;
                PlayerPrefs.SetInt(KeyRotateMode, (int)value);
                PlayerPrefs.Save();
                OnChanged?.Invoke();
            }
        }

        /// <summary>设置面板「旋转方式」按钮用：按钮 → 手势 → 两者 → 按钮 循环。</summary>
        public static RotateInputMode CycleRotateMode()
        {
            switch (RotateMode)
            {
                case RotateInputMode.Buttons:
                    RotateMode = RotateInputMode.Gesture;
                    break;
                case RotateInputMode.Gesture:
                    RotateMode = RotateInputMode.Both;
                    break;
                default:
                    RotateMode = RotateInputMode.Buttons;
                    break;
            }
            return RotateMode;
        }

        /// <summary>是否显示屏幕上的顺时针/逆时针按钮。</summary>
        public static bool RotateButtonsEnabled => RotateMode != RotateInputMode.Gesture;

        /// <summary>是否接收「第二根手指上下滑」的旋转手势。</summary>
        public static bool RotateGestureEnabled => RotateMode != RotateInputMode.Buttons;

        /// <summary>
        /// 顺时针旋转键（默认 D）。写入时持久化并广播 OnChanged，拖拽控制器立刻用新键。
        /// None 与鼠标键会被拒绝——鼠标键和拖拽 / 右键取下会抢同一套输入。
        /// </summary>
        public static KeyCode RotateClockwiseKey
        {
            get
            {
                if (!cachedRotateClockwise.HasValue)
                {
                    cachedRotateClockwise = ReadKey(KeyRotateClockwise, DefaultRotateClockwiseKey);
                }
                return cachedRotateClockwise.Value;
            }
            set
            {
                if (!IsBindableKey(value) || RotateClockwiseKey == value)
                {
                    return;
                }
                cachedRotateClockwise = value;
                PlayerPrefs.SetInt(KeyRotateClockwise, (int)value);
                PlayerPrefs.Save();
                OnChanged?.Invoke();
            }
        }

        /// <summary>逆时针旋转键（默认 A）。</summary>
        public static KeyCode RotateCounterClockwiseKey
        {
            get
            {
                if (!cachedRotateCounterClockwise.HasValue)
                {
                    cachedRotateCounterClockwise = ReadKey(KeyRotateCounterClockwise, DefaultRotateCounterClockwiseKey);
                }
                return cachedRotateCounterClockwise.Value;
            }
            set
            {
                if (!IsBindableKey(value) || RotateCounterClockwiseKey == value)
                {
                    return;
                }
                cachedRotateCounterClockwise = value;
                PlayerPrefs.SetInt(KeyRotateCounterClockwise, (int)value);
                PlayerPrefs.Save();
                OnChanged?.Invoke();
            }
        }

        /// <summary>把两个旋转键恢复出厂值（D / A）。</summary>
        public static void ResetRotateKeys()
        {
            cachedRotateClockwise = DefaultRotateClockwiseKey;
            cachedRotateCounterClockwise = DefaultRotateCounterClockwiseKey;
            PlayerPrefs.SetInt(KeyRotateClockwise, (int)DefaultRotateClockwiseKey);
            PlayerPrefs.SetInt(KeyRotateCounterClockwise, (int)DefaultRotateCounterClockwiseKey);
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }

        /// <summary>该键位能否绑定：排除 None 与鼠标键。</summary>
        public static bool IsBindableKey(KeyCode key)
        {
            if (key == KeyCode.None)
            {
                return false;
            }
            int value = (int)key;
            return value < (int)KeyCode.Mouse0 || value > (int)KeyCode.Mouse6;
        }

        /// <summary>读键位并夹掉历史脏值（改过枚举或手改存档时）。</summary>
        private static KeyCode ReadKey(string prefKey, KeyCode fallback)
        {
            int raw = PlayerPrefs.GetInt(prefKey, (int)fallback);
            KeyCode key = (KeyCode)raw;
            return IsBindableKey(key) ? key : fallback;
        }

        /// <summary>切换音乐静音并返回新状态（设置面板的「静音」按钮直接用）。</summary>
        public static bool ToggleMusicMuted()
        {
            MusicMuted = !MusicMuted;
            return MusicMuted;
        }

        /// <summary>切换音效静音并返回新状态。</summary>
        public static bool ToggleSfxMuted()
        {
            SfxMuted = !SfxMuted;
            return SfxMuted;
        }
    }
}
