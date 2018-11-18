using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace RSEAudio
{
	[EffectDefinition("RSE_AUDIO")]
	public class AdvanceAudioFX : EffectBehaviour
	{
		[Persistent]
		public AudioFX.AudioChannel channel;
		
		[Persistent]
		public bool loop;
		
		[Persistent]
		public string clip = string.Empty;

		[Persistent]
		public float doppler = 1;
		
		[Persistent]
		public float lowpassResQ = 1;
		
		[Persistent]
		public float maxDistance = 500;
		
		[Persistent]
		public AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;
		
		public FXCurve volume = new FXCurve("volume", 1f);
		public FXCurve pitch = new FXCurve("pitch", 1f);
		public FXCurve lowpass = new FXCurve("lowpass", 1f);
		
		AudioSource audioSource;
		
		GameObject audioParent;
		AudioLowPassFilter lowpassfilter;
		float thrustPow;
		
		public override void OnLoad(ConfigNode node)
		{
			ConfigNode.LoadObjectFromConfig(this, node);
			volume.Load("volume", node);
			pitch.Load("pitch", node);
			lowpass.Load("lowpass", node);
			
		}
	
		public override void OnSave(ConfigNode node)
		{
			ConfigNode.CreateConfigFromObject(this, node);
			volume.Save(node);
			pitch.Save(node);
			lowpass.Save(node);
		}
		
		public override void OnInitialize()
		{
			var audioClip = GameDatabase.Instance.GetAudioClip(clip);
			if (audioClip == null)
				return;
			
			audioParent = new GameObject();
			audioParent.transform.parent = gameObject.transform;
			audioParent.transform.position = gameObject.transform.position;
			audioParent.transform.rotation = gameObject.transform.rotation;
			audioParent.layer = gameObject.layer;
			
			audioSource = audioParent.AddComponent<AudioSource>();
			audioSource.clip = audioClip;
			audioSource.volume = volume;
			audioSource.pitch = pitch;
			audioSource.spatialBlend = 1;
			audioSource.rolloffMode = rolloffMode;
			audioSource.maxDistance = maxDistance;
			audioSource.dopplerLevel = doppler;
			audioSource.loop = loop;
			
			if (lowpass.keyFrames.Count > 0) {
				lowpassfilter = audioParent.AddOrGetComponent<AudioLowPassFilter>();
				lowpassfilter.lowpassResonanceQ = lowpassResQ;
				lowpassfilter.customCutoffCurve = lowpass.fCurve;
			}
			
			if (HighLogic.LoadedScene != GameScenes.LOADING) {
				GameEvents.onGamePause.Add(OnGamePause);
				GameEvents.onGameUnpause.Add(OnGameUnpause);
			}
		}
		
		bool _playSoundSingle = false;
		bool _gamePaused = false;
		int _fixedUpdateCount;
		
		public override void OnEvent()
		{
			thrustPow = 1f;
			_playSoundSingle = true;
		}
		
		public override void OnEvent(float power)
		{
			thrustPow = power;
		}
		
		void FixedUpdate()
		{
			if (_fixedUpdateCount < 2)
				_fixedUpdateCount++;
		}
		
		void Update()
		{
			if (_fixedUpdateCount < 2)
				return;
			
			try {
				if (audioSource.clip == null)
					return;
			
				if (_gamePaused) {
					if (audioSource.isPlaying) {
						audioSource.Stop();
					}
					return;
				}
				
				if (audioSource.loop && !audioSource.isPlaying) {
					audioSource.time = UnityEngine.Random.Range(0, audioSource.clip.length);
					audioSource.Play();
				} else if (_playSoundSingle) {
					audioSource.Play();
					_playSoundSingle = false;
				}

				if (!audioSource.isPlaying)
					return;
				
				audioSource.pitch = pitch.Value(thrustPow);
				AudioFX.SetSourceVolume(audioSource, volume.Value(thrustPow), channel);
			
			} catch {
				return;
			}
		}

		void OnGamePause()
		{
			_gamePaused = true;
		}

		void OnGameUnpause()
		{
			_gamePaused = false;
		}
		
		void OnDestroy()
		{
			GameEvents.onGamePause.Remove(OnGamePause);
			GameEvents.onGameUnpause.Remove(OnGameUnpause);
		}
	}
}
