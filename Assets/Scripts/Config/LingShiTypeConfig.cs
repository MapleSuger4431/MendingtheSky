using UnityEngine;

namespace MendingtheSky
{
    /// <summary>
    /// 灵石种类配置（红=快煮 / 蓝=中速 / 金=慢煮）。
    /// 创建：右键 → Create → MendingtheSky → LingShi Type。
    /// 视觉映射：InBasket→dustSprite；Cooking/Placed/Locked→normalSprite；Cooked→glowSprite。
    /// </summary>
    [CreateAssetMenu(fileName = "LingShiTypeConfig", menuName = "MendingtheSky/LingShi Type")]
    public class LingShiTypeConfig : ScriptableObject
    {
        [Header("基础")]
        public string typeId;
        public string displayName;

        [Header("熬煮与灵气")]
        public float cookTime = 3f;
        public float qiValue = 2f;

        [Header("视觉")]
        public Sprite normalSprite;
        public Sprite dustSprite;
        public Sprite glowSprite;
        public Color tintColor = Color.white;
        public GameObject dustEffectPrefab;
        public GameObject glowEffectPrefab;

        public float GetCookTime() { return cookTime; }
        public float GetQiValue() { return qiValue; }
        public Sprite GetNormalSprite() { return normalSprite; }
        public Sprite GetDustSprite() { return dustSprite; }
        public Sprite GetGlowSprite() { return glowSprite; }
        public Color GetTintColor() { return tintColor; }
        public GameObject GetDustEffectPrefab() { return dustEffectPrefab; }
        public GameObject GetGlowEffectPrefab() { return glowEffectPrefab; }
    }
}
