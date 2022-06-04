using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RocketSoundEnhancement.Unity
{
    public enum AudioMufflerQuality
    {
        Normal = 0,
        AirSimLite = 1,
        AirSim = 2
    }
    public interface ISettingsPanel
    {
        float CanvasScale { get; }
        string Version { get; }

        bool EnableAudioEffects { get; set; }
        bool DisableStagingSound { get; set; }
        float InteriorVolume { get; set; }
        float ExteriorVolume { get; set; }

        AudioMufflerQuality MufflerQuality { get; set; }
        float MufflerInternalMode { get; set; }
        float MufflerExternalMode { get; set; }

        bool EnableCustomLimiter { get; set; }
        float AutoLimiter { get; set; }
        float LimiterThreshold { get; set; }
        float LimiterGain { get; set; }
        float LimiterAttack { get; set; }
        float LimiterRelease { get; set; }

        void LoadSettings();
        void SaveSettings();
        void ClampToScreen(RectTransform rect);
    }
}
