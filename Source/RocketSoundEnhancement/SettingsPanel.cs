using KSP.UI.Screens;
using UnityEngine;
using RocketSoundEnhancement.Unity;
using System.Reflection;
using System;
using KSP.UI;

namespace RocketSoundEnhancement
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class SettingsPanel : MonoBehaviour, ISettingsPanel
    {
        private SettingsPanel instance;
        public SettingsPanel Instance { get { return instance; } }
        public float CanvasScale => GameSettings.UI_SCALE;

        public ApplicationLauncherButton AppButton;
        public Texture AppIcon = GameDatabase.Instance.GetTexture("RocketSoundEnhancement/Textures/RSE_Icon", false);

        private RSE_Panel panelController;
        private GameObject rse_PanelPrefab;
        private Vector2 panelPosition = Vector2.zero;
        public GameObject RSE_PanelPrefab
        {
            get
            {
                if (rse_PanelPrefab == null)
                {
                    rse_PanelPrefab = RocketSoundEnhancement.RSE_Bundle.LoadAsset("RSE_Panel") as GameObject;
                }
                return rse_PanelPrefab;
            }
        }

        public string Version => version;
        public string version;
        public bool EnableAudioEffects { get => Settings.EnableAudioEffects; set => Settings.EnableAudioEffects = value; }
        public bool DisableStagingSound { get => Settings.DisableStagingSound; set => Settings.DisableStagingSound = value; }
        public float InteriorVolume { get => Settings.InteriorVolume; set => Settings.InteriorVolume = value; }
        public float ExteriorVolume { get => Settings.ExteriorVolume; set => Settings.ExteriorVolume = value; }
        public AudioMufflerQuality MufflerQuality { get => Settings.MufflerQuality; set => Settings.MufflerQuality = value; }
        public float MufflerExternalMode { get => Settings.MufflerExternalMode; set => Settings.MufflerExternalMode = value; }
        public float MufflerInternalMode { get => Settings.MufflerInternalMode; set => Settings.MufflerInternalMode = value; }
        public float MachEffectsAmount { get => Settings.MachEffectsAmount; set => Settings.MachEffectsAmount = value; }
        public float DopplerFactor { get => Settings.DopplerFactor; set => Settings.DopplerFactor = value; }
        public bool ClampActiveVesselMuffling { get => Settings.ClampActiveVesselMuffling; set => Settings.ClampActiveVesselMuffling = value; }
        public bool EnableCustomLimiter
        {
            get => Settings.EnableCustomLimiter;
            set
            {
                Settings.EnableCustomLimiter = value;
                RocketSoundEnhancement.instance.UpdateLimiter();
            }
        }
        public float AutoLimiter
        {
            get => Settings.AutoLimiter;
            set
            {
                Settings.AutoLimiter = value;
                RocketSoundEnhancement.instance.UpdateLimiter();
            }
        }
        public float LimiterThreshold
        {
            get => Settings.LimiterThreshold;
            set
            {
                Settings.LimiterThreshold = value;
                RocketSoundEnhancement.instance.UpdateLimiter();
            }
        }
        public float LimiterGain
        {
            get => Settings.LimiterGain;
            set
            {
                Settings.LimiterGain = value;
                RocketSoundEnhancement.instance.UpdateLimiter();
            }
        }
        public float LimiterAttack
        {
            get => Settings.LimiterAttack;
            set
            {
                Settings.LimiterAttack = value;
                RocketSoundEnhancement.instance.UpdateLimiter();
            }
        }
        public float LimiterRelease
        {
            get => Settings.LimiterRelease;
            set
            {
                Settings.LimiterRelease = value;
                RocketSoundEnhancement.instance.UpdateLimiter();
            }
        }

        private void Awake()
        {
            instance = this;

            Assembly assembly = AssemblyLoader.loadedAssemblies.GetByAssembly(Assembly.GetExecutingAssembly()).assembly;
            var assemblyInformantion = Attribute.GetCustomAttribute(assembly, typeof(AssemblyInformationalVersionAttribute)) as AssemblyInformationalVersionAttribute;
            version = assemblyInformantion != null ? assemblyInformantion.InformationalVersion : "";
        }

        private void Start()
        {
            if (AppButton == null)
            {
                AppButton = ApplicationLauncher.Instance.AddModApplication(
                    () => OpenSettingsPanel(),
                    () => CloseSettingsPanel(),
                    null, null,
                    null, null,
                    ApplicationLauncher.AppScenes.FLIGHT, AppIcon
                );
            }
        }

        void OpenSettingsPanel()
        {
            if (RSE_PanelPrefab == null) return;

            GameObject panelPrefab = Instantiate(RSE_PanelPrefab, Vector3.zero, Quaternion.identity) as GameObject;
            panelPrefab.transform.SetParent(UIMasterController.Instance.dialogCanvas.transform, false);
            panelPrefab.transform.SetAsFirstSibling();
            panelController = panelPrefab.GetComponent<RSE_Panel>();

            if (panelController == null) return;
            panelController.transform.position = panelPosition;
            panelController.Initialize(Instance);
        }

        void CloseSettingsPanel()
        {
            if (panelController != null)
            {
                panelPosition = panelController.transform.position;
                GameObject.Destroy(panelController.gameObject);
            }
        }

        public void LoadSettings()
        {
            Settings.Load();
            RocketSoundEnhancement.Instance.ApplySettings();
        }

        public void SaveSettings()
        {
            Settings.Save();
            RocketSoundEnhancement.Instance.ApplySettings();
        }
        public void ClampToScreen(RectTransform rect)
        {
           UIMasterController.ClampToScreen(rect, Vector2.zero);
        }

        void OnDestroy()
        {
            if (panelController != null)
                UnityEngine.Object.Destroy(panelController.gameObject);

            if (AppButton != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(AppButton);
                AppButton = null;
            }
        }
    }
}