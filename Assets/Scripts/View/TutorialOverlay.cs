using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MendingtheSky
{
    /// <summary>
    /// 新手引导的表现层：一句短文案 + 一个跟随目标的高亮框 + 跳过按钮。
    ///
    /// 只负责「显示」，不判断什么时候显示——步骤推进由 TutorialManager 决定。
    /// 高亮框用屏幕坐标传入，内部换算到 Canvas 局部坐标，因此对世界物体只需调用方
    /// 先做 Camera.WorldToScreenPoint。
    /// </summary>
    public class TutorialOverlay : MonoBehaviour
    {
        [Tooltip("Canvas 的 RectTransform，用于屏幕坐标转 UI 坐标")]
        [SerializeField] private RectTransform canvasRect;
        [Tooltip("气泡容器（含底板与文字）")]
        [SerializeField] private GameObject bubbleRoot;
        [SerializeField] private TextMeshProUGUI bubbleText;
        [Tooltip("高亮描边框（Sliced 拉伸到目标大小）")]
        [SerializeField] private RectTransform highlight;
        [SerializeField] private Button skipButton;
        [Tooltip("高亮框比目标额外外扩的像素，让描边不压住目标")]
        [SerializeField] private float highlightPadding = 24f;

        /// <summary>玩家点了跳过。</summary>
        public event Action OnSkip;

        /// <summary>气泡当前是否可见（暂停恢复后据此重新显示）。</summary>
        public bool IsVisible => bubbleRoot != null && bubbleRoot.activeSelf;

        private void Awake()
        {
            if (skipButton != null)
            {
                skipButton.onClick.AddListener(() => OnSkip?.Invoke());
            }
            // 注意：这里不能调 Hide()——根节点初始是关闭的，Awake 会在「第一次 ShowText 把它打开」
            // 的那一刻才执行，此时 Hide() 会把刚打开的根节点又关掉（表现为引导永远不显示）。
            if (bubbleRoot != null)
            {
                bubbleRoot.SetActive(false);
            }
            if (highlight != null)
            {
                highlight.gameObject.SetActive(false);
            }
        }

        /// <summary>只显示文案，不画高亮框。text 必须是已本地化好的最终文本。</summary>
        public void ShowText(string text)
        {
            gameObject.SetActive(true);
            SetText(text);
            if (highlight != null)
            {
                highlight.gameObject.SetActive(false);
            }
        }

        /// <summary>显示文案并把高亮框移到屏幕上的一块矩形区域。text 必须是已本地化好的最终文本。</summary>
        public void ShowAt(string text, Vector2 screenCenter, Vector2 screenSize)
        {
            gameObject.SetActive(true);
            SetText(text);
            if (highlight == null || canvasRect == null)
            {
                return;
            }
            highlight.gameObject.SetActive(true);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, screenCenter, null, out Vector2 local))
            {
                highlight.anchoredPosition = local;
                highlight.sizeDelta = screenSize + new Vector2(highlightPadding, highlightPadding);
            }
        }

        public void Hide()
        {
            if (bubbleRoot != null)
            {
                bubbleRoot.SetActive(false);
            }
            if (highlight != null)
            {
                highlight.gameObject.SetActive(false);
            }
            // 整层一起关掉：否则空的白板会继续挡住点击
            gameObject.SetActive(false);
        }

        /// <summary>改右上角按钮的文案：开场页显示「开始」，其余步骤显示「跳过」。</summary>
        public void SetSkipLabel(string key)
        {
            if (skipButton == null)
            {
                return;
            }
            var localized = skipButton.GetComponentInChildren<MendingtheSky.LocalizedText>(true);
            if (localized != null)
            {
                localized.Key = key;
            }
        }

        private void SetText(string text)
        {
            if (bubbleRoot != null)
            {
                bubbleRoot.SetActive(true);
            }
            if (bubbleText != null)
            {
                bubbleText.text = text;
            }
        }
    }
}
