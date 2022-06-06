using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RocketSoundEnhancement.Unity
{
    [RequireComponent(typeof(RectTransform))]
    public class RSE_Panel : MonoBehaviour, IDragHandler, IEndDragHandler, IPointerDownHandler
    {
        [SerializeField] private GameObject basicPanel;
        [SerializeField] private GameObject advancePanel;
        [SerializeField] private Text versionLabel;

        [SerializeField] private Toggle enableAudioEffects;
        [SerializeField] private Toggle disableStagingSound;

        [SerializeField] private Slider interiorVolume;
        [SerializeField] private Slider exteriorVolume;

        [SerializeField] private Toggle enableCustomLimiter;
        [SerializeField] private Slider autoLimiter;
        [SerializeField] private Slider limiterThreshold;
        [SerializeField] private Slider limiterGain;
        [SerializeField] private Slider limiterAttack;
        [SerializeField] private Slider limiterRelease;

        [SerializeField] private Toggle mufflerNormalQuality;
        [SerializeField] private Toggle mufflerAirSimLiteQuality;
        [SerializeField] private Toggle mufflerAirSimFullQuality;
        [SerializeField] private Toggle clampActiveVesselMuffling;
        [SerializeField] private Slider mufflerInternalMode;
        [SerializeField] private Slider mufflerExternalMode;
        [SerializeField] private Slider machEffectsAmount;
        [SerializeField] private Slider dopplerFactor;

        private RectTransform rectTransform;
        private ISettingsPanel settingsPanel;
        private bool initialized = false;
        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
        }

        public void OnDrag(PointerEventData eventData)
        {
            rectTransform.anchoredPosition += eventData.delta / settingsPanel.CanvasScale;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (settingsPanel == null) return;

            settingsPanel.ClampToScreen(rectTransform);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            transform.SetAsLastSibling();
        }

        public void Initialize(ISettingsPanel _settingsPanel)
        {
            settingsPanel = _settingsPanel;
            versionLabel.text = settingsPanel.Version;

            enableAudioEffects.isOn = settingsPanel.EnableAudioEffects;
            disableStagingSound.isOn = settingsPanel.DisableStagingSound;
            enableCustomLimiter.isOn = settingsPanel.EnableCustomLimiter;

            interiorVolume.value = settingsPanel.InteriorVolume;
            exteriorVolume.value = settingsPanel.ExteriorVolume;

            autoLimiter.value = settingsPanel.AutoLimiter;
            limiterThreshold.value = settingsPanel.LimiterThreshold;
            limiterGain.value = settingsPanel.LimiterGain;
            limiterAttack.value = settingsPanel.LimiterAttack;
            limiterRelease.value = settingsPanel.LimiterRelease;

            mufflerNormalQuality.isOn = settingsPanel.MufflerQuality == AudioMufflerQuality.Normal;
            mufflerAirSimLiteQuality.isOn = settingsPanel.MufflerQuality == AudioMufflerQuality.AirSimLite;
            mufflerAirSimFullQuality.isOn = settingsPanel.MufflerQuality == AudioMufflerQuality.AirSim;
            clampActiveVesselMuffling.isOn = settingsPanel.ClampActiveVesselMuffling;

            mufflerExternalMode.value = MathHelper.FrequencyToAmount(settingsPanel.MufflerExternalMode);
            mufflerInternalMode.value = MathHelper.FrequencyToAmount(settingsPanel.MufflerInternalMode);
            machEffectsAmount.value = settingsPanel.MachEffectsAmount;
            dopplerFactor.value = settingsPanel.DopplerFactor;

            initialized = true;
        }
        public void ToggleAdvanceSettings(bool toggle)
        {
            if (basicPanel == null || advancePanel == null) return;

            basicPanel.SetActive(!toggle);
            advancePanel.SetActive(toggle);
        }
        public void OnAudioEffecstEnabled(bool isOn)
        {
            if (!initialized) return;

            settingsPanel.EnableAudioEffects = isOn;
        }
        public void OnDisableStagingSound(bool isOn)
        {
            if (!initialized) return;
            settingsPanel.DisableStagingSound = isOn;
        }
        public void OnInteriorVolume(float value)
        {
            if (!initialized) return;
            settingsPanel.InteriorVolume = MathHelper.Round(value, 2);
        }
        public void OnExteriorVolume(float value)
        {
            if (!initialized) return;
            settingsPanel.ExteriorVolume = MathHelper.Round(value, 2);
        }
        public void OnEnableCustomLimiter(bool isOn)
        {
            if (!initialized) return;
            settingsPanel.EnableCustomLimiter = isOn;
        }
        public void OnAutoLimiter(float value)
        {
            if (!initialized) return;
            settingsPanel.AutoLimiter = MathHelper.Round(value, 2);
        }
        public void OnLimiterThreshold(float value)
        {
            if (!initialized) return;
            settingsPanel.LimiterThreshold = MathHelper.Round(value, 2);
        }
        public void OnLimiterGain(float value)
        {
            if (!initialized) return;
            settingsPanel.LimiterGain = MathHelper.Round(value, 2);
        }
        public void OnLimiterAttack(float value)
        {
            if (!initialized) return;
            settingsPanel.LimiterAttack = MathHelper.Round(value, 2);
        }
        public void OnLimiterRelease(float value)
        {
            if (!initialized) return;
            settingsPanel.LimiterRelease = MathHelper.Round(value, 2);
        }
        public void OnMufflerQuality(int qualityIndex)
        {
            if(!initialized) return;
            switch (qualityIndex)
            {
                case 0:
                    settingsPanel.MufflerQuality = AudioMufflerQuality.Normal;
                    break;
                case 1:
                    settingsPanel.MufflerQuality = AudioMufflerQuality.AirSimLite;
                    break;
                case 2:
                    settingsPanel.MufflerQuality = AudioMufflerQuality.AirSim;
                    break;
                default:
                    settingsPanel.MufflerQuality = AudioMufflerQuality.Normal;
                    break;
            }
        }
        public void OnClampActiveVesselMuffling(bool isOn)
        {
            if (!initialized) return;
            settingsPanel.ClampActiveVesselMuffling = isOn;
        }
        public void OnMufflerInternalMode(float value)
        {
            if (!initialized) return;
            settingsPanel.MufflerInternalMode = MathHelper.AmountToFrequency(value);
        }
        public void OnMufflerExternalMode(float value)
        {
            if (!initialized) return;
            settingsPanel.MufflerExternalMode = MathHelper.AmountToFrequency(value);
        }
        public void OnMachEffectsAmount(float value)
        {
            if (!initialized) return;
            settingsPanel.MachEffectsAmount = MathHelper.Round(value, 2);
        }
        public void OnDopplerFactor(float value)
        {
            if (!initialized) return;
            settingsPanel.DopplerFactor = MathHelper.Round(value, 2);
        }
        public void OnReload()
        {
            if (!initialized) return;

            initialized = false;
            settingsPanel.LoadSettings();
            Initialize(settingsPanel);
        }
        public void OnSave()
        {
            if (!initialized) return;
            settingsPanel.SaveSettings();
        }
    }
}
