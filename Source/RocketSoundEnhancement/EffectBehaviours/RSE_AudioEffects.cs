using RocketSoundEnhancement.AudioFilters;
using RocketSoundEnhancement.Unity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RocketSoundEnhancement.EffectBehaviours
{
    [EffectDefinition("RSE_AUDIO_LOOP")]
    public class RSE_AudioEffectsLoop : RSE_AudioEffects
    {
        public override void OnLoad(ConfigNode node)
        {
            loop = true;
            base.OnLoad(node);
        }
    }

    [EffectDefinition("RSE_AUDIO")]
    public class RSE_AudioEffects : EffectBehaviour
    {
        [Persistent] public AudioFX.AudioChannel channel;
        [Persistent] public string clip = "";
        [Persistent] public bool loop;
        [Persistent] public float spread;

        [Persistent] public bool EnableCombFilter = false;
        [Persistent] public bool EnableLowpassFilter = false;
        [Persistent] public bool EnableWaveShaperFilter = false;
        [Persistent] public float DopplerFactor = 0.5f;
        [Persistent] public AirSimulationUpdate AirSimUpdateMode = AirSimulationUpdate.Full;
        [Persistent] public float FarLowpass = 2500;
        [Persistent] public float AngleHighpass = 0;
        [Persistent] public float MaxCombDelay = 20;
        [Persistent] public float MaxCombMix = 0.25f;
        [Persistent] public float MaxDistortion = 0.5f;

        public FXCurve volume = new FXCurve("volume", 1f);
        public FXCurve pitch = new FXCurve("pitch", 1f);

        public System.Random random = new System.Random();
        public GameObject audioParent;
        public AudioSource audioSource;
        public AirSimulationFilter airSimFilter;
        public AudioClip audioClip;

        bool isActiveVessel;
        bool markForPlay;
        bool playOneShot;
        int slowUpdate;
        float control;
        float distance;
        float lastDistance;
        float doppler = 1;
        float angle = 0;
        float machPass = 1;
        float mach = 0;
        float machAngle = 0;

        public override void OnInitialize()
        {
            audioParent = hostPart != null ? AudioUtility.CreateAudioParent(hostPart, hostPart.name) : this.gameObject;

            audioClip = GameDatabase.Instance.GetAudioClip(clip);
            while (audioClip == null)
            {
                clip = Path.ChangeExtension(clip, null);
                audioClip = GameDatabase.Instance.GetAudioClip(clip);
                if (audioClip == null)
                {
                    Debug.Log($"[RSE]: RSE_AUDIO: {clip} clip cannot be found");
                    break;
                }
            }

            audioSource = AudioUtility.CreateSource(audioParent, volume, pitch, loop, spread);
            audioSource.enabled = false;
            audioSource.clip = audioClip;

            if (hostPart != null && (EnableCombFilter || EnableLowpassFilter || EnableWaveShaperFilter))
            {
                airSimFilter = audioParent.AddComponent<AirSimulationFilter>();
                airSimFilter.enabled = false;

                airSimFilter.EnableCombFilter = EnableCombFilter;
                airSimFilter.EnableLowpassFilter = EnableLowpassFilter;
                airSimFilter.EnableWaveShaperFilter = EnableWaveShaperFilter;
                airSimFilter.SimulationUpdate = hostPart != null ? AirSimUpdateMode : AirSimulationUpdate.Basic;
                airSimFilter.FarLowpass = FarLowpass;
                airSimFilter.AngleHighPass = AngleHighpass;
                airSimFilter.MaxCombDelay = MaxCombDelay;
                airSimFilter.MaxCombMix = MaxCombMix;
                airSimFilter.MaxDistortion = MaxDistortion;
            }

            GameEvents.onGamePause.Add(OnGamePaused);
            GameEvents.onGameUnpause.Add(OnGameUnpaused);
        }

        public override void OnLoad(ConfigNode node)
        {
            ConfigNode.LoadObjectFromConfig(this, node);
            volume.Load("volume", node);
            pitch.Load("pitch", node);
        }

        public override void OnEvent()
        {
            Play(1);
            playOneShot = true;
        }

        public override void OnEvent(float power)
        {
            Play(power);
        }

        public void Play(float power)
        {
            markForPlay = true;
            control = power;
        }

        public virtual void LateUpdate()
        {
            if (audioSource == null || audioClip == null || !HighLogic.LoadedSceneIsFlight) return;

            isActiveVessel = hostPart != null && hostPart.vessel == FlightGlobals.ActiveVessel;

            if (markForPlay)
            {
                float finalVolume = volume.Value(control);

                if (finalVolume < float.Epsilon)
                {
                    if (audioSource.volume == 0 && loop)
                        audioSource.Stop();

                    if (audioSource.isPlaying && loop)
                        audioSource.volume = 0;

                    goto end;
                }

                audioSource.enabled = true;
                if (Settings.MufflerQuality > AudioMufflerQuality.Normal)
                {
                    if (airSimFilter != null && Settings.MufflerQuality == AudioMufflerQuality.AirSim)
                    {
                        airSimFilter.enabled = true;
                        airSimFilter.Distance = distance;
                        airSimFilter.Mach = mach;
                        airSimFilter.Angle = angle;
                        airSimFilter.MachAngle = machAngle;
                        airSimFilter.MachPass = machPass;
                    }
                    else
                    {
                        finalVolume *= Mathf.Log(Mathf.Lerp(1, 10, machPass), 10);
                    }
                }

                AudioFX.SetSourceVolume(audioSource, finalVolume, channel);
                audioSource.pitch = pitch.Value(control) * (loop ? doppler : 1);
                audioSource.outputAudioMixerGroup = AudioUtility.GetMixerGroup(FXChannel.Exterior, isActiveVessel);

                if (!audioSource.isPlaying && loop)
                {
                    if (audioSource.clip == null) audioSource.clip = audioClip;

                    audioSource.time = audioClip.length * (float)random.NextDouble();
                    audioSource.Play();
                }

                if (playOneShot)
                {
                    audioSource.PlayOneShot(audioClip);
                    playOneShot = false;
                }
            }

            end:

            if (airSimFilter != null && airSimFilter.enabled && Settings.MufflerQuality != AudioMufflerQuality.AirSim)
                airSimFilter.enabled = false;

            slowUpdate++;
            if (slowUpdate >= 60)
            {
                slowUpdate = 0;

                if (audioSource.isPlaying || !audioSource.enabled) return;

                audioSource.enabled = false;
                markForPlay = false;

                if (!audioSource.enabled && airSimFilter != null)
                {
                    airSimFilter.enabled = false;
                    airSimFilter.Distance = distance;
                    airSimFilter.Mach = mach;
                    airSimFilter.Angle = angle;
                    airSimFilter.MachAngle = machAngle;
                    airSimFilter.MachPass = machPass;
                }
            }
        }

        public void FixedUpdate()
        {
            if (Settings.EnableAudioEffects && HighLogic.LoadedSceneIsFlight)
            {
                distance = Vector3.Distance(CameraManager.GetCurrentCamera().transform.position, transform.position);
                var relativeSpeed = (lastDistance - distance) / TimeWarp.fixedDeltaTime;
                lastDistance = distance;

                if (hostPart == null) return;

                float speedOfSound = hostPart.vessel.speedOfSound > 0 ? (float)hostPart.vessel.speedOfSound : 340.29f;
                float dopplerRaw = Mathf.Clamp((speedOfSound + ((relativeSpeed) * DopplerFactor)) / speedOfSound, 1 - (DopplerFactor * 0.5f), 1 + DopplerFactor);
                doppler = Mathf.MoveTowards(doppler, dopplerRaw, 0.5f * TimeWarp.fixedDeltaTime);

                angle = (1 + Vector3.Dot(hostPart.vessel.GetComponent<ShipEffects>().MachTipCameraNormal, (transform.up + hostPart.vessel.velocityD).normalized)) * 90;
                machPass = 1f - Mathf.Clamp01(angle / hostPart.vessel.GetComponent<ShipEffects>().MachAngle) * Mathf.Clamp01(hostPart.vessel.GetComponent<ShipEffects>().Mach);

                if (hostPart.vessel.isActiveVessel && (InternalCamera.Instance.isActive || MapView.MapCamera.isActiveAndEnabled))
                {
                    angle = 0;
                    machPass = 1;
                }

                mach = hostPart.vessel.GetComponent<ShipEffects>().Mach;
                machAngle = hostPart.vessel.GetComponent<ShipEffects>().MachAngle;
            }
        }

        public void OnGamePaused()
        {
            if (audioSource == null) return;
            audioSource.Pause();
        }

        public void OnGameUnpaused()
        {
            if (audioSource == null) return;
            audioSource.UnPause();
        }

        public void OnDestroy()
        {
            GameEvents.onGamePause.Remove(OnGamePaused);
            GameEvents.onGameUnpause.Remove(OnGameUnpaused);
        }
    }
}
