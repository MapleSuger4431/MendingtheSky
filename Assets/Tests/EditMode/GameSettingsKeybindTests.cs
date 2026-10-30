using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace MendingtheSky.Tests
{
    /// <summary>
    /// 旋转键位：默认 D / A、可持久化、拒绝非法键（None 与鼠标键）、恢复默认。
    /// 测试结束恢复原值，避免污染开发机上的 PlayerPrefs。
    /// </summary>
    public class GameSettingsKeybindTests
    {
        private const string CwKey = "settings.key.rotateCW";
        private const string CcwKey = "settings.key.rotateCCW";

        private KeyCode oldCw;
        private KeyCode oldCcw;

        [SetUp]
        public void SetUp()
        {
            oldCw = GameSettings.RotateClockwiseKey;
            oldCcw = GameSettings.RotateCounterClockwiseKey;
        }

        [TearDown]
        public void TearDown()
        {
            GameSettings.RotateClockwiseKey = oldCw;
            GameSettings.RotateCounterClockwiseKey = oldCcw;
            PlayerPrefs.Save();
        }

        [Test]
        public void Defaults_AreDAndA()
        {
            GameSettings.ResetRotateKeys();
            Assert.AreEqual(KeyCode.D, GameSettings.RotateClockwiseKey, "顺时针默认应为 D");
            Assert.AreEqual(KeyCode.A, GameSettings.RotateCounterClockwiseKey, "逆时针默认应为 A");
        }

        [Test]
        public void BoundKey_PersistsToPlayerPrefs()
        {
            GameSettings.RotateClockwiseKey = KeyCode.F;
            Assert.AreEqual((int)KeyCode.F, PlayerPrefs.GetInt(CwKey, -1), "顺时针键应写入 PlayerPrefs");

            GameSettings.RotateCounterClockwiseKey = KeyCode.S;
            Assert.AreEqual((int)KeyCode.S, PlayerPrefs.GetInt(CcwKey, -1), "逆时针键应写入 PlayerPrefs");
        }

        [Test]
        public void MouseAndNoneKeys_AreRejected()
        {
            GameSettings.RotateClockwiseKey = KeyCode.D;
            GameSettings.RotateClockwiseKey = KeyCode.Mouse0;
            Assert.AreEqual(KeyCode.D, GameSettings.RotateClockwiseKey, "鼠标键不应被接受");

            GameSettings.RotateClockwiseKey = KeyCode.None;
            Assert.AreEqual(KeyCode.D, GameSettings.RotateClockwiseKey, "None 不应被接受");

            Assert.IsTrue(GameSettings.IsBindableKey(KeyCode.K), "字母键应该可绑定");
            Assert.IsFalse(GameSettings.IsBindableKey(KeyCode.Mouse3), "鼠标中键不应可绑定");
        }

        [Test]
        public void Reset_RestoresDefaultsAndRaisesOnChanged()
        {
            GameSettings.RotateClockwiseKey = KeyCode.F;
            GameSettings.RotateCounterClockwiseKey = KeyCode.S;

            int count = 0;
            System.Action handler = () => count++;
            GameSettings.OnChanged += handler;
            try
            {
                GameSettings.ResetRotateKeys();
            }
            finally
            {
                GameSettings.OnChanged -= handler;
            }

            Assert.AreEqual(KeyCode.D, GameSettings.RotateClockwiseKey);
            Assert.AreEqual(KeyCode.A, GameSettings.RotateCounterClockwiseKey);
            Assert.AreEqual(1, count, "恢复默认应广播一次，让拖拽控制器立刻用新键");
        }

        [Test]
        public void DirtySavedValue_FallsBackToDefault()
        {
            // 模拟存档被手改成鼠标键，读回来应回退到出厂值
            PlayerPrefs.SetInt(CwKey, (int)KeyCode.Mouse2);
            typeof(GameSettings)
                .GetField("cachedRotateClockwise", BindingFlags.NonPublic | BindingFlags.Static)
                .SetValue(null, null);

            Assert.AreEqual(KeyCode.D, GameSettings.RotateClockwiseKey, "非法存档值应回退到默认 D");
        }
    }
}
