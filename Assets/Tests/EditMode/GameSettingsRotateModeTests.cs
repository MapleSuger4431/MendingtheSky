using NUnit.Framework;
using UnityEngine;

namespace MendingtheSky.Tests
{
    /// <summary>
    /// 旋转输入方式（按钮 / 手势 / 两者）：默认按钮、三态循环、持久化，
    /// 以及 DragController 使用的两个派生开关。
    /// 测试会临时改设置，结束恢复原值，避免污染开发机上的 PlayerPrefs。
    /// </summary>
    public class GameSettingsRotateModeTests
    {
        private const string RotateModeKey = "settings.rotateMode";

        private RotateInputMode oldMode;

        [SetUp]
        public void SetUp()
        {
            oldMode = GameSettings.RotateMode;
        }

        [TearDown]
        public void TearDown()
        {
            GameSettings.RotateMode = oldMode;
            PlayerPrefs.Save();
        }

        [Test]
        public void DefaultMode_IsButtons()
        {
            PlayerPrefs.DeleteKey(RotateModeKey);
            // 清掉内存缓存后重新读，模拟全新安装
            typeof(GameSettings)
                .GetField("cachedRotateMode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                ?.SetValue(null, null);

            Assert.AreEqual(RotateInputMode.Buttons, GameSettings.RotateMode, "默认应为屏幕按钮");
            Assert.IsTrue(GameSettings.RotateButtonsEnabled, "按钮模式要显示旋转按钮");
            Assert.IsFalse(GameSettings.RotateGestureEnabled, "按钮模式不接收手势");
        }

        [Test]
        public void Cycle_GoesButtonsGestureBoth_ThenWraps()
        {
            GameSettings.RotateMode = RotateInputMode.Buttons;

            Assert.AreEqual(RotateInputMode.Gesture, GameSettings.CycleRotateMode());
            Assert.AreEqual(RotateInputMode.Both, GameSettings.CycleRotateMode());
            Assert.AreEqual(RotateInputMode.Buttons, GameSettings.CycleRotateMode(), "三种之后应回到按钮");
        }

        [Test]
        public void DerivedSwitches_MatchMode()
        {
            GameSettings.RotateMode = RotateInputMode.Buttons;
            Assert.IsTrue(GameSettings.RotateButtonsEnabled);
            Assert.IsFalse(GameSettings.RotateGestureEnabled);

            GameSettings.RotateMode = RotateInputMode.Gesture;
            Assert.IsFalse(GameSettings.RotateButtonsEnabled, "手势模式不应再画按钮");
            Assert.IsTrue(GameSettings.RotateGestureEnabled);

            GameSettings.RotateMode = RotateInputMode.Both;
            Assert.IsTrue(GameSettings.RotateButtonsEnabled);
            Assert.IsTrue(GameSettings.RotateGestureEnabled);
        }

        [Test]
        public void Mode_PersistsToPlayerPrefs()
        {
            GameSettings.RotateMode = RotateInputMode.Both;
            Assert.AreEqual((int)RotateInputMode.Both, PlayerPrefs.GetInt(RotateModeKey, -1), "旋转方式应写入 PlayerPrefs");
        }

        [Test]
        public void GestureDirection_SwipeUpIsCounterClockwise()
        {
            Assert.IsFalse(MendingtheSky.DragController.IsClockwiseGesture(60f), "上滑（屏幕 y 增大）应为逆时针");
            Assert.IsTrue(MendingtheSky.DragController.IsClockwiseGesture(-60f), "下滑应为顺时针");
            Assert.IsTrue(MendingtheSky.DragController.IsClockwiseGesture(-1f), "轻微下滑也按顺时针判定");
        }

        [Test]
        public void SettingSameMode_DoesNotRaiseOnChanged()
        {
            GameSettings.RotateMode = RotateInputMode.Gesture;

            int count = 0;
            System.Action handler = () => count++;
            GameSettings.OnChanged += handler;
            try
            {
                GameSettings.RotateMode = RotateInputMode.Gesture;
                Assert.AreEqual(0, count, "值未变化时不应广播（否则拖拽控制器会反复刷新）");

                GameSettings.RotateMode = RotateInputMode.Both;
                Assert.AreEqual(1, count, "值变化时应广播一次");
            }
            finally
            {
                GameSettings.OnChanged -= handler;
            }
        }
    }
}
