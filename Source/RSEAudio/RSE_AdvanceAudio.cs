using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace RSEAudio
{
	[EffectDefinition("RSE_AUDIO")]
	public class RSE_AdvanceAudio : EffectBehaviour
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
		public FXCurve lowpass = new FXCurve("lowpass", 22000f);
		
		GameObject audioParent;
		AudioSource audioSource;
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
			
			lowpassfilter = audioParent.AddOrGetComponent<AudioLowPassFilter>();
			lowpassfilter.lowpassResonanceQ = lowpassResQ;
			
			if (HighLogic.LoadedScene != GameScenes.LOADING) {
				GameEvents.onGamePause.Add(new EventVoid.OnEvent(OnGamePause));
				GameEvents.onGameUnpause.Add(new EventVoid.OnEvent(OnGameUnpause));
			}
		}
		
		bool playSoundSingle = false;
		bool gamePaused = false;
		public override void OnEvent()
		{
			thrustPow = 1f;
			playSoundSingle = true;
		}
		
		public override void OnEvent(float power)
		{
			thrustPow = power;
		}
		
		void Update()
		{
			try {
				if (audioSource.clip == null)
					return;
			
				if (gamePaused) {
					if (audioSource.isPlaying) {
						audioSource.Stop();
					}
					return;
				}
				
				if (audioSource.loop && !audioSource.isPlaying) {
					audioSource.Play();
				} else if (playSoundSingle) {
					audioSource.Play();
					playSoundSingle = false;
				}

				if (!audioSource.isPlaying)
					return;
				
				audioSource.pitch = pitch.Value(thrustPow);
				AudioFX.SetSourceVolume(audioSource, volume.Value(thrustPow), channel);
			
				var distance = Vector3.Distance(FlightCamera.fetch.mainCamera.transform.position, audioParent.transform.position);
				lowpassfilter.cutoffFrequency = lowpass.Value(distance);
			} catch {
				return;
			}
		}

		void OnGamePause()
		{
			gamePaused = true;
		}

		void OnGameUnpause()
		{
			gamePaused = false;
		}
		
		void OnDestroy()
		{
			GameEvents.onGamePause.Remove(new EventVoid.OnEvent(OnGamePause));
			GameEvents.onGameUnpause.Remove(new EventVoid.OnEvent(OnGameUnpause));
		}
	}
}
