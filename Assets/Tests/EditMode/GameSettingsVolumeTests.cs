using NUnit.Framework;
using UnityEngine;

namespace MendingtheSky.Tests
{
    /// <summary>
    /// 音量设置：clamp 到 0-1、写入 PlayerPrefs、可通过 OnChanged 通知。
    /// 测试会临时改音量，结束后恢复原值，避免污染玩家设置。
    /// </summary>
    public class GameSettingsVolumeTests
    {
        private const string MusicKey = "settings.musicVolume";
        private const string SfxKey = "settings.sfxVolume";

        private bool hadMusic;
        private float oldMusic;
        private bool hadSfx;
        private float oldSfx;

        [SetUp]
        public void SetUp()
        {
            hadMusic = PlayerPrefs.HasKey(MusicKey);
            oldMusic = PlayerPrefs.GetFloat(MusicKey, 0.8f);
            hadSfx = PlayerPrefs.HasKey(SfxKey);
            oldSfx = PlayerPrefs.GetFloat(SfxKey, 0.8f);
        }

        [TearDown]
        public void TearDown()
        {
            if (hadMusic) PlayerPrefs.SetFloat(MusicKey, oldMusic);
            else PlayerPrefs.DeleteKey(MusicKey);
            if (hadSfx) PlayerPrefs.SetFloat(SfxKey, oldSfx);
            else PlayerPrefs.DeleteKey(SfxKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void MusicVolume_ClampsToUnitRange()
        {
            GameSettings.MusicVolume = 1.7f;
            Assert.AreEqual(1f, GameSettings.MusicVolume, 0.0001f, "超过 1 应夹到 1");

            GameSettings.MusicVolume = -0.5f;
            Assert.AreEqual(0f, GameSettings.MusicVolume, 0.0001f, "低于 0 应夹到 0");
        }

        [Test]
        public void SfxVolume_ClampsToUnitRange()
        {
            GameSettings.SfxVolume = 2f;
            Assert.AreEqual(1f, GameSettings.SfxVolume, 0.0001f);

            GameSettings.SfxVolume = -1f;
            Assert.AreEqual(0f, GameSettings.SfxVolume, 0.0001f);
        }

        [Test]
        public void Volumes_PersistToPlayerPrefs()
        {
            GameSettings.MusicVolume = 0.35f;
            GameSettings.SfxVolume = 0.65f;

            Assert.AreEqual(0.35f, PlayerPrefs.GetFloat(MusicKey, -1f), 0.0001f, "音乐音量应写入 PlayerPrefs");
            Assert.AreEqual(0.65f, PlayerPrefs.GetFloat(SfxKey, -1f), 0.0001f, "音效音量应写入 PlayerPrefs");
        }

        [Test]
        public void ChangingVolume_RaisesOnChanged()
        {
            int count = 0;
            System.Action handler = () => count++;
            GameSettings.OnChanged += handler;
            try
            {
                GameSettings.MusicVolume = 0.42f;
                Assert.GreaterOrEqual(count, 1, "改音量应触发 OnChanged（AudioManager 据此更新 AudioSource）");
            }
            finally
            {
                GameSettings.OnChanged -= handler;
            }
        }
    }
}
