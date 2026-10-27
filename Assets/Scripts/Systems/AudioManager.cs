using UnityEngine;

namespace MendingtheSky
{
    /// <summary>一次性音效的语义 ID（对应 AudioLibrary 里的字段）。</summary>
    public enum SfxId
    {
        Success,
        Failure,
        Heal,
        Open,
        Spawn,
        Notify,
        Place,
        Invalid,
        Splash,
        Click,
        Lift,
        Put
    }

    /// <summary>
    /// 音频播放入口（单例）：
    /// - 4 条通道：BGM（循环）、一次性 SFX、熬煮循环、告急循环；
    /// - BGM 走「音乐音量」，其余走「音效音量」（GameSettings 变化时实时同步）；
    /// - 其它系统用 AudioManager.Instance?.PlaySfx(SfxId.X) 调用，不需要场景里逐个接线。
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("资源表")]
        [SerializeField] private AudioLibrary library;

        [Header("通道")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;
        [Tooltip("熬煮时循环的咕嘟声")]
        [SerializeField] private AudioSource cookLoopSource;
        [Tooltip("灵气告急时循环的心跳声")]
        [SerializeField] private AudioSource warningLoopSource;

        private void Awake()
        {
            Instance = this;
            Configure(musicSource, true);
            Configure(sfxSource, false);
            Configure(cookLoopSource, true);
            Configure(warningLoopSource, true);
            ApplyVolumes();
            GameSettings.OnChanged += ApplyVolumes;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            GameSettings.OnChanged -= ApplyVolumes;
        }

        private static void Configure(AudioSource source, bool loop)
        {
            if (source == null)
            {
                return;
            }
            source.playOnAwake = false;
            source.loop = loop;
        }

        /// <summary>把音量设置应用到所有通道（音效音量同时控制熬煮/告急循环）。静音由 Effective* 统一折算为 0。</summary>
        private void ApplyVolumes()
        {
            if (musicSource != null)
            {
                musicSource.volume = GameSettings.EffectiveMusicVolume;
            }
            float sfx = GameSettings.EffectiveSfxVolume;
            if (sfxSource != null)
            {
                sfxSource.volume = sfx;
            }
            if (cookLoopSource != null)
            {
                cookLoopSource.volume = sfx;
            }
            if (warningLoopSource != null)
            {
                warningLoopSource.volume = sfx;
            }
        }

        /// <summary>播放背景音乐（循环），重复调用不会重新起播。</summary>
        public void PlayMusic()
        {
            if (musicSource == null || library == null || library.bgmTheme == null)
            {
                return;
            }
            if (musicSource.isPlaying && musicSource.clip == library.bgmTheme)
            {
                return;
            }
            musicSource.clip = library.bgmTheme;
            musicSource.volume = GameSettings.EffectiveMusicVolume;
            musicSource.Play();
        }

        public void StopMusic()
        {
            if (musicSource != null && musicSource.isPlaying)
            {
                musicSource.Stop();
            }
        }

        /// <summary>播放一次性音效。</summary>
        public void PlaySfx(SfxId id)
        {
            AudioClip clip = GetClip(id);
            if (sfxSource == null || clip == null)
            {
                return;
            }
            sfxSource.PlayOneShot(clip);
        }

        /// <summary>灵气告急循环（进入告急状态时调用）。</summary>
        public void PlayWarningLoop()
        {
            PlayLoop(warningLoopSource, library != null ? library.warningLoop : null);
        }

        public void StopWarningLoop()
        {
            StopLoop(warningLoopSource);
        }

        /// <summary>熬煮循环（锅里有 Cooking 状态灵石时调用）。</summary>
        public void PlayCookLoop()
        {
            PlayLoop(cookLoopSource, library != null ? library.bubblingLoop : null);
        }

        public void StopCookLoop()
        {
            StopLoop(cookLoopSource);
        }

        private static void PlayLoop(AudioSource source, AudioClip clip)
        {
            if (source == null || clip == null)
            {
                return;
            }
            if (source.isPlaying && source.clip == clip)
            {
                return;
            }
            source.clip = clip;
            source.volume = GameSettings.EffectiveSfxVolume;
            source.Play();
        }

        private static void StopLoop(AudioSource source)
        {
            if (source != null && source.isPlaying)
            {
                source.Stop();
            }
        }

        private AudioClip GetClip(SfxId id)
        {
            if (library == null)
            {
                return null;
            }
            switch (id)
            {
                case SfxId.Success: return library.success;
                case SfxId.Failure: return library.failure;
                case SfxId.Heal: return library.heal;
                case SfxId.Open: return library.open;
                case SfxId.Spawn: return library.spawn;
                case SfxId.Notify: return library.notify;
                case SfxId.Place: return library.place;
                case SfxId.Invalid: return library.invalid;
                case SfxId.Splash: return library.splash;
                case SfxId.Click: return library.click;
                case SfxId.Lift: return library.lift;
                case SfxId.Put: return library.put;
                default: return null;
            }
        }
    }
}
