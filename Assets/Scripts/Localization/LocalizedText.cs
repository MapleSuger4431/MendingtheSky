using TMPro;
using UnityEngine;

namespace MendingtheSky
{
    /// <summary>
    /// 挂到场景里需要翻译的 TMP 文本上，填一个 L10n key；切换语言时自动刷新。
    /// 需要带参数的文本（如「已补缺口 {0} / {1}」）由 UIManager 自己用 L10n.Get(key, args) 格式化，
    /// 不挂本组件（因为参数是动态的）。
    /// </summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class LocalizedText : MonoBehaviour
    {
        [Tooltip("L10n key，例如 ui.paused / ui.resume")]
        [SerializeField] private string key;

        private TextMeshProUGUI label;

        public string Key
        {
            get => key;
            set
            {
                key = value;
                Refresh();
            }
        }

        private void Awake()
        {
            label = GetComponent<TextMeshProUGUI>();
        }

        private void OnEnable()
        {
            L10n.OnLanguageChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            L10n.OnLanguageChanged -= Refresh;
        }

        /// <summary>按当前语言重设文本（key 为空时保持原样，便于场景里先摆占位文字）。</summary>
        public void Refresh()
        {
            if (label == null)
            {
                label = GetComponent<TextMeshProUGUI>();
            }
            if (label != null && !string.IsNullOrEmpty(key))
            {
                label.text = L10n.Get(key);
            }
        }
    }
}
