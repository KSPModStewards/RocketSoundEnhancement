using UnityEngine;
using UnityEngine.UI;

namespace RocketSoundEnhancement.Unity
{
    [RequireComponent(typeof(Slider))]
    public class SliderValueDisplay : MonoBehaviour
    {
        public string NumberFormat = string.Empty;
        public float Multiplier = 1.0f;
        [SerializeField] Slider slider;
        [SerializeField] Text label;
        public void Awake()
        {
            if (slider == null) slider = GetComponent<Slider>();

            if (label == null) return;

            label.text = (slider.value * Multiplier).ToString("0") + NumberFormat;
            slider.onValueChanged.AddListener(x =>
            {
                label.text = (x * Multiplier).ToString("0") + NumberFormat;
            });
        }
    }
}
