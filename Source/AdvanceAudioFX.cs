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
		
		[Persistent]
		public float thumpAmount = 0;
		[Persistent]
		public float thumpRate = 1;
		[Persistent]
		public float thumpSensitivity = 50;
		
		public FXCurve volume = new FXCurve("volume", 1f);
		public FXCurve pitch = new FXCurve("pitch", 1f);
		public FXCurve lowpass = new FXCurve("lowpass", 1f);
		
		AudioSource audioSource;
		
		GameObject audioParent;
		AudioLowPassFilter lowpassFilter;
		
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
				lowpassFilter = audioParent.AddOrGetComponent<AudioLowPassFilter>();
				lowpassFilter.lowpassResonanceQ = lowpassResQ;
				lowpassFilter.customCutoffCurve = lowpass.fCurve;
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
		
		float lastThrust;
		float _thumper;
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
				
				if (thumpAmount > 0) {
					float thrustDelta = (thrustPow - lastThrust) * 60;
					if (thrustDelta > thumpSensitivity) {
						_thumper = thumpAmount;
					}
					float thumpProgress = _thumper / thumpAmount;
					_thumper = Mathf.MoveTowards(_thumper, 0, (thumpRate * thumpProgress) * Time.deltaTime);
				}

	
				audioSource.pitch = pitch.Value(thrustPow) + _thumper;
				AudioFX.SetSourceVolume(audioSource, Mathf.Clamp(volume.Value(thrustPow), 0, 3), channel);
				
				lastThrust = thrustPow;
			
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
