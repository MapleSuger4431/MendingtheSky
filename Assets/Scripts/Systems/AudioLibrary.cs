using UnityEngine;

namespace MendingtheSky
{
    /// <summary>
    /// 音频资源表：按「声音类别」索引，由 AudioManager 引用。
    /// 文件名与字段一一对应（Assets/Audio/Sfx、Assets/Audio/Bgm）。
    /// </summary>
    [CreateAssetMenu(fileName = "AudioLibrary", menuName = "MendingtheSky/Audio Library")]
    public class AudioLibrary : ScriptableObject
    {
        [Header("BGM")]
        public AudioClip bgmTheme;

        [Header("一次性音效")]
        public AudioClip success;
        public AudioClip failure;
        public AudioClip heal;
        public AudioClip open;
        public AudioClip spawn;
        public AudioClip notify;
        public AudioClip place;
        public AudioClip invalid;
        public AudioClip splash;
        public AudioClip click;
        public AudioClip lift;
        public AudioClip put;

        [Header("循环音效")]
        public AudioClip warningLoop;
        public AudioClip bubblingLoop;
    }
}
