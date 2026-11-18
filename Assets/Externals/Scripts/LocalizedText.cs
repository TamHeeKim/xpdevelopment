using UnityEngine;
using UnityEngine.UI;
namespace FishingPrototype
{
    [RequireComponent(typeof(Text))]
    public sealed class LocalizedText : MonoBehaviour
    {
        [Header("CSV 텍스트")]
        public string textKey;
        void OnEnable() { GetComponent<Text>().text = CombatText.Get(textKey); }
    }
}
