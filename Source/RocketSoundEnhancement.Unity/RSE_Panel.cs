using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RocketSoundEnhancement.Unity
{
    [RequireComponent(typeof(RectTransform))]
    public class RSE_Panel : MonoBehaviour, IDragHandler, IEndDragHandler, IPointerDownHandler
    {
        [SerializeField] private GameObject _basicPanel;
        [SerializeField] private GameObject _advancePanel;
        [SerializeField] private Text _versionLabel;
        [SerializeField] private Toggle _enableAudioEffects;
        [SerializeField] private Toggle _disableStagingSound;
        [SerializeField] private Toggle _enableCustomLimiter;
        [SerializeField] private Toggle _mufflerNormalQuality;
        [SerializeField] private Toggle _mufflerAirSimLiteQuality;
        [SerializeField] private Toggle _mufflerAirSimFullQuality;
        [SerializeField] private Slider _mufflerInternalMode;
        [SerializeField] private Slider _mufflerExternalMode;
        [SerializeField] private Slider _interiorVolume;
        [SerializeField] private Slider _exteriorVolume;
        [SerializeField] private Slider _autoLimiter;
        [SerializeField] private Slider _limiterThreshold;
        [SerializeField] private Slider _limiterGain;
        [SerializeField] private Slider _limiterAttack;
        [SerializeField] private Slider _limiterRelease;

        private RectTransform rectTransform;
        private ISettingsPanel settingsPanel;
        private bool _initialized = false;
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
            _versionLabel.text = settingsPanel.Version;

            _enableAudioEffects.isOn = settingsPanel.EnableAudioEffects;
            _disableStagingSound.isOn = settingsPanel.DisableStagingSound;
            _enableCustomLimiter.isOn = settingsPanel.EnableCustomLimiter;

            _interiorVolume.value = settingsPanel.InteriorVolume;
            _exteriorVolume.value = settingsPanel.ExteriorVolume;

            _autoLimiter.value = settingsPanel.AutoLimiter;
            _limiterThreshold.value = settingsPanel.LimiterThreshold;
            _limiterGain.value = settingsPanel.LimiterGain;
            _limiterAttack.value = settingsPanel.LimiterAttack;
            _limiterRelease.value = settingsPanel.LimiterRelease;

            _mufflerNormalQuality.isOn = settingsPanel.MufflerQuality == AudioMufflerQuality.Normal;
            _mufflerAirSimLiteQuality.isOn = false;
            _mufflerAirSimLiteQuality.interactable = false;
            _mufflerAirSimFullQuality.isOn = settingsPanel.MufflerQuality == AudioMufflerQuality.AirSim;

            _mufflerExternalMode.value = MathHelper.FrequencyToAmount(settingsPanel.MufflerExternalMode);
            _mufflerInternalMode.value = MathHelper.FrequencyToAmount(settingsPanel.MufflerInternalMode);

            _initialized = true;
        }
        public void ToggleAdvanceSettings(bool toggle)
        {
            if (_basicPanel == null || _advancePanel == null) return;

            _basicPanel.SetActive(!toggle);
            _advancePanel.SetActive(toggle);
        }

        public void OnAudioEffecstEnabled(bool isOn)
        {
            if (!_initialized) return;

            settingsPanel.EnableAudioEffects = isOn;
        }

        public void OnDisableStagingSound(bool isOn)
        {
            if (!_initialized) return;

            settingsPanel.DisableStagingSound = isOn;
        }

        public void OnInteriorVolume(float value)
        {
            if (!_initialized) return;

            settingsPanel.InteriorVolume = MathHelper.Round(value, 2);
        }

        public void OnExteriorVolume(float value)
        {
            if (!_initialized) return;

            settingsPanel.ExteriorVolume = MathHelper.Round(value, 2);
        }

        public void OnEnableCustomLimiter(bool isOn)
        {
            if (!_initialized) return;

            settingsPanel.EnableCustomLimiter = isOn;
        }

        public void OnAutoLimiter(float value)
        {
            if (!_initialized) return;

            settingsPanel.AutoLimiter = MathHelper.Round(value, 2);
        }

        public void OnLimiterThreshold(float value)
        {
            if (!_initialized) return;

            settingsPanel.LimiterThreshold = MathHelper.Round(value, 2);
        }

        public void OnLimiterGain(float value)
        {
            if (!_initialized) return;

            settingsPanel.LimiterGain = MathHelper.Round(value, 2);
        }
        public void OnLimiterAttack(float value)
        {
            if (!_initialized) return;

            settingsPanel.LimiterAttack = MathHelper.Round(value, 2);
        }
        public void OnLimiterRelease(float value)
        {
            if (!_initialized) return;

            settingsPanel.LimiterRelease = MathHelper.Round(value, 2);
        }

        public void OnMufflerQuality(int qualityIndex)
        {
            if(!_initialized) return;

            switch (qualityIndex)
            {
                case 0:
                    settingsPanel.MufflerQuality = AudioMufflerQuality.Normal;
                    break;
                case 1:
                    settingsPanel.MufflerQuality = AudioMufflerQuality.AirSim;
                    break;
                default:
                    settingsPanel.MufflerQuality = AudioMufflerQuality.Normal;
                    break;
            }
        }

        public void OnMufflerInternalMode(float value)
        {
            if (!_initialized) return;

            settingsPanel.MufflerInternalMode = MathHelper.AmountToFrequency(value);
        }

        public void OnMufflerExternalMode(float value)
        {
            if (!_initialized) return;

            settingsPanel.MufflerExternalMode = MathHelper.AmountToFrequency(value);
        }

        public void OnReload()
        {
            if (!_initialized) return;

            _initialized = false;
            settingsPanel.LoadSettings();
            Initialize(settingsPanel);
        }

        public void OnSave()
        {
            if (!_initialized) return;
            settingsPanel.SaveSettings();
        }
    }
}
