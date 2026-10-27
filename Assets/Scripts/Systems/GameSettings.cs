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

        private const float DefaultVolume = 0.8f;

        /// <summary>任意设置变化事件：设置面板与预览/音频系统监听后立即生效。</summary>
        public static event Action OnChanged;

        /// <summary>缓存，避免每帧读 PlayerPrefs。</summary>
        private static bool? cachedShowPlacementPreview;
        private static float? cachedMusicVolume;
        private static float? cachedSfxVolume;
        private static bool? cachedMusicMuted;
        private static bool? cachedSfxMuted;

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
        public static float EffectiveMusicVolume => MusicMuted ? 0f : MusicVolume;

        /// <summary>实际输出的音效音量：静音时为 0，否则等于 SfxVolume。</summary>
        public static float EffectiveSfxVolume => SfxMuted ? 0f : SfxVolume;

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
