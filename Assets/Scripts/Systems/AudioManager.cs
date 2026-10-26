using UnityEngine;

namespace MendingtheSky
{
    /// <summary>
    /// 音频播放入口：一个 BGM（循环）AudioSource + 一个音效（单次）AudioSource。
    /// 当前项目还没有音频资源，先把「音量设置 → AudioSource.volume」这条路接通：
    /// 以后接入 clip 只需调用 PlayMusic / PlaySfx，不用再改设置面板。
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;

        private void Awake()
        {
            if (musicSource != null)
            {
                musicSource.playOnAwake = false;
                musicSource.loop = true;
                musicSource.volume = GameSettings.MusicVolume;
            }
            if (sfxSource != null)
            {
                sfxSource.playOnAwake = false;
                sfxSource.volume = GameSettings.SfxVolume;
            }
            GameSettings.OnChanged += ApplyVolumes;
        }

        private void OnDestroy()
        {
            GameSettings.OnChanged -= ApplyVolumes;
        }

        /// <summary>把当前音量设置应用到两个 AudioSource。</summary>
        private void ApplyVolumes()
        {
            if (musicSource != null)
            {
                musicSource.volume = GameSettings.MusicVolume;
            }
            if (sfxSource != null)
            {
                sfxSource.volume = GameSettings.SfxVolume;
            }
        }

        /// <summary>播放背景音乐（循环）。没有 clip 时什么也不做。</summary>
        public void PlayMusic(AudioClip clip)
        {
            if (musicSource == null || clip == null)
            {
                return;
            }
            musicSource.clip = clip;
            musicSource.volume = GameSettings.MusicVolume;
            musicSource.Play();
        }

        /// <summary>播放一次性音效（受音效音量控制）。</summary>
        public void PlaySfx(AudioClip clip)
        {
            if (sfxSource == null || clip == null)
            {
                return;
            }
            sfxSource.PlayOneShot(clip, GameSettings.SfxVolume);
        }
    }
}
