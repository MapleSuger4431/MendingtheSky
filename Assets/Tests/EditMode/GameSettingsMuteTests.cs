using NUnit.Framework;
using UnityEngine;

namespace MendingtheSky.Tests
{
    /// <summary>
    /// 快捷静音：静音是独立开关（不动音量数值），生效值为 0，取消后回到原音量。
    /// 测试会临时改设置，结束后通过属性 setter 恢复原值，同时把缓存写回一致状态。
    /// </summary>
    public class GameSettingsMuteTests
    {
        private const string MusicMutedKey = "settings.musicMuted";
        private const string SfxMutedKey = "settings.sfxMuted";

        private bool oldMusicMuted;
        private float oldMusicVolume;
        private bool oldSfxMuted;
        private float oldSfxVolume;

        [SetUp]
        public void SetUp()
        {
            oldMusicMuted = GameSettings.MusicMuted;
            oldMusicVolume = GameSettings.MusicVolume;
            oldSfxMuted = GameSettings.SfxMuted;
            oldSfxVolume = GameSettings.SfxVolume;
        }

        [TearDown]
        public void TearDown()
        {
            GameSettings.MusicMuted = oldMusicMuted;
            GameSettings.MusicVolume = oldMusicVolume;
            GameSettings.SfxMuted = oldSfxMuted;
            GameSettings.SfxVolume = oldSfxVolume;
            PlayerPrefs.Save();
        }

        [Test]
        public void MutingMusic_KeepsVolumeButSilencesOutput()
        {
            // 先把两路都设成已知值，避免受 PlayerPrefs 残留影响（这条以前会因环境不同而偶发失败）
            GameSettings.MusicVolume = 0.8f;
            GameSettings.SfxVolume = 0.6f;
            GameSettings.SfxMuted = false;
            GameSettings.MusicMuted = true;

            Assert.AreEqual(0.8f, GameSettings.MusicVolume, 0.0001f, "静音不应改动玩家设定的音量");
            Assert.AreEqual(0f, GameSettings.EffectiveMusicVolume, 0.0001f, "静音时实际输出应为 0");
            Assert.AreEqual(0.6f, GameSettings.SfxVolume, 0.0001f, "音乐静音不应影响音效音量");
            Assert.AreEqual(0.6f, GameSettings.EffectiveSfxVolume, 0.0001f, "音效未静音时应保持原音量");
        }

        [Test]
        public void UnmutingMusic_RestoresPreviousVolume()
        {
            GameSettings.MusicVolume = 0.55f;
            GameSettings.MusicMuted = true;
            GameSettings.MusicMuted = false;

            Assert.AreEqual(0.55f, GameSettings.EffectiveMusicVolume, 0.0001f, "取消静音应回到原音量");
        }

        [Test]
        public void MutingSfx_IsIndependentFromMusic()
        {
            GameSettings.MusicVolume = 0.7f;
            GameSettings.SfxVolume = 0.4f;
            GameSettings.MusicMuted = false;
            GameSettings.SfxMuted = true;

            Assert.AreEqual(0.7f, GameSettings.EffectiveMusicVolume, 0.0001f, "音效静音不应影响音乐");
            Assert.AreEqual(0f, GameSettings.EffectiveSfxVolume, 0.0001f, "音效静音时实际输出应为 0");
            Assert.AreEqual(0.4f, GameSettings.SfxVolume, 0.0001f, "音效静音不应改动音量数值");
        }

        [Test]
        public void MuteState_PersistsToPlayerPrefs()
        {
            GameSettings.MusicMuted = true;
            GameSettings.SfxMuted = true;

            Assert.AreEqual(1, PlayerPrefs.GetInt(MusicMutedKey, -1), "音乐静音应写入 PlayerPrefs");
            Assert.AreEqual(1, PlayerPrefs.GetInt(SfxMutedKey, -1), "音效静音应写入 PlayerPrefs");
        }

        [Test]
        public void ToggleMute_ReturnsNewStateAndRaisesOnChanged()
        {
            GameSettings.MusicMuted = false;
            GameSettings.SfxMuted = false;

            int count = 0;
            System.Action handler = () => count++;
            GameSettings.OnChanged += handler;
            try
            {
                Assert.IsTrue(GameSettings.ToggleMusicMuted(), "第一次切换应变为静音");
                Assert.IsFalse(GameSettings.ToggleMusicMuted(), "再次切换应取消静音");
                Assert.IsTrue(GameSettings.ToggleSfxMuted(), "音效切换应变为静音");
                Assert.GreaterOrEqual(count, 3, "每次切换都应触发 OnChanged（音频系统据此更新输出）");
            }
            finally
            {
                GameSettings.OnChanged -= handler;
            }
        }
    }
}
