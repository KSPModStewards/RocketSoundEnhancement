# Changelog

## [Unreleased]
### Changes
- Fixed OneShot Samples playing with wrong pitch value
- Fixed AirSim Filter not taking effect sometimes.
- Fixed SonicBoom SoundLayer Support
- Removed AirSim Filters from SonicBoom sources
- Removed AudioListener Fix. Install [KSP Community Fixes](https://github.com/KSPModdingLibs/KSPCommunityFixes/releases) instead for the Sound Bug Fix.

## [0.9.4] - 06-08-22
### Changes
- Added MachEffectsAmount config
- Settings Panel now closes when Save button is pressed
- Fixed RSE_AUDIO AirSim Filter being disabled first before the audiosource causing unwanted behaviour
- Fixed Stock Audio Disappearing bug
- Temporary Fix for Sound Stutters when changing scenes

## [0.9.3] - 06-05-22
### Changes
- AirSim Lite muffler quality. mach effects and sonic booms without the airsim filters
- RSE_AUDIO, RSE_AUDIOLOOP, simpler non-SoundLayer version of RSE_Modules for EFFECTS{} nodes.

### Fixes
- Fixed code error with ShipEffects

## [0.9.2] - 06-03-22
### Changes
- New Settings Panel
- Replaced Audio Limiter with Unity's Audio Compressor/Limiter
- Audio Limiter Presets has been replaced with "Limiter Amount" slider in the Settings Panel. 
- Muffling Presets has been replaced with simpler "Muffling Amount" slider in the Settings Panel. (Still saved as a frequency value in the Settings.cfg)
- "ClampActiveVesselMuffling" option for Audio Muffler. Allows the currently active vessel to have separate muffling from regular muffling by clamping the muffling amount to the InternalMode muffling frequency.
- ["CustomMixerRouting" config allows custom routing of existing static sound sources (modded, stock) to the mixer.](https://forum.kerbalspaceprogram.com/index.php?/topic/179579-110x-112x-rocket-sound-enhancement-audio-framework-for-complex-sound-effects-v091-052322-config-pack-v120-052322/&do=findComment&comment=4139319)
- Audio Muffler now considers any sound source that has spatialBlend = 0 or position = 0 as a GUI Source and is ignored from the muffling. Can still be routed back for muffling (for example: wind sounds) via CustomMixerRouting config in Settings.cfg

### Performance Improvements/Fixes
- Fixed denormal numbers from AirSimulationFilter causing high CPU usage
- AudioClips, AudioSources and AirSimulationFilters are prepared ahead of time instead of being created dynamically during update.
- SoundLayers can now share AudioSources with each other if the layer has the same name
- One Shot (eg: Engage, Disengage, Flame-out and Bursts) SoundLayers now share one AudioSource with each other. _Issue: sometimes pitch is incorrectly applied._
- Fixed audio clicking when the sound source is stopped
- Fixed Null Exceptions from ShipEffectsCollisions
- Fixed Loop Randomizer not properlly randomizing causing unwanted Comb-Filtering/Phasing
- Various Refactoring and Optimizations

**Full Changelog**: https://github.com/ensou04/RocketSoundEnhancement/compare/0.9.1...0.9.2

## [0.9.1] - 05-23-22
- Added Support for Internal Space Mods ([RPM](https://github.com/JonnyOThan/RasterPropMonitor/releases), [MAS](https://github.com/MOARdV/AvionicsSystems/releases))
- Fixed Music Getting Muffled at higher Muffler Settings

## [0.9.0] - 05-23-22
### New Audio Muffler Quality Settings
- **Lite:** The Old Basic Muffler
- **Full:** Mixer based Audio Muffler with Dedicated channels for Exterior Sounds, Focused Vessel and Interior. With Doppler Effect
- **AirSim:** Works on top of Full Quality, Parts with RSE Modules will simulate realistic sound attenuation over distance, comb-filtering, mach effects, sonic booms and distortion. Stock sound sources has basic mach and distance attenuation (volume or filter based) support.
### Changes
- **RSE_RotorEngines** Part Module
- **RSE_Propellers**{} Config Node for propeller blades. Works when attached to Rotors with an **RSE_RotorEngines** Part Module
- **Motor{}** Sound Group for **RSE_Wheels**. **Torque** Sound Group has been removed.
- **ShipEffects** now uses Sound Groups for **Physics Controllers** instead of using soundlayer.data
- **DYNAMICPRESSURE{}**, **SONICBOOM{}** and **REENTRYHEAT{}** Sound Group for **ShipEffects**
- Simplified Audio Muffler Settings
- Interior and Exterior Volume Settings
- Support for SoundtrackEditor music sources by @KvaNTy in https://github.com/ensou04/RocketSoundEnhancement/pull/11
- Support for misc Chatterer sound sources by @KvaNTy in https://github.com/ensou04/RocketSoundEnhancement/pull/12

**Full Changelog**: https://github.com/ensou04/RocketSoundEnhancement/compare/0.7.2...0.9.0

0.7.2:
- Potential Fix for Collision Loop Sound Effects Persisting after Impact
- Custom Audio Muffler Settings now react instantly to new settings
- Tweaked Orbit View Muffling
- Removed "MuffleChatterer" from LOWPASSFILTER in Settings.cfg
- Added "AffectChatterer" under RSE_SETTINGS in Settings.cfg
- Code Optimization

0.7.1
- Fixed Music being Muffled

0.7.0
- New In-game Settings Window
- Audio Limiter/Sound Effects Mastering now has Presets Available through In-Game Settings
- Audio Muffler Presets now Available through In-Game Settings

Sound Effects Mastering Presets:
- Balanced (The Default Preset, Sounds are balanced with Reasonable Dynamic Range)
- Cinematic (Wide Dynamic Range, Loud Sounds are Louder and Quiet Sound Are Quieter than Balanced)
- NASA-Reels (Inspired by Real Rocket Launch Footage, This is the Loudest Preset with minimal Dynamic Range)
- Custom (User Customizable Preset)
- Add more Presets in [Settings.cfg](https://github.com/ensou04/RocketSoundEnhancement/blob/0.7.1/GameData/RocketSoundEnhancement/Settings.cfg)

Audio Muffler Presets:
- Interior-Only (Only Muffle sounds when the Camera is in IVA view)
- Muffled-Vacuum
- Silent-Vacuum
- Custom (User Customizable Preset)
- Add more Presets in [Settings.cfg](https://github.com/ensou04/RocketSoundEnhancement/blob/0.7.1/GameData/RocketSoundEnhancement/Settings.cfg)

Other Changes:
- Handling of Configs has been changed in code.
- Code Cleanup

0.6.1
- Setting Muffling to 0 now goes to silent.
- Code Optimization on Audio Limiter

0.6.0
- Compiled on 1.12.3
- Implemented a new Audio Limiter (Fairly Childish Limiter/Compressor)
- Added RCS Part Module
- Volume Settings are now controlled by Stock In-game Settings
- Moved Audio Limiter and Muffler Settings to Settings.cfg
- Increased KSP Real Voices/Sound Limit to 64 (originally was only 15)
- Fixed Engage/Disengage Not Working Together
- Fixed "Volume" Config Node for RSE_Wheels and RSE_Engines not being in code.
- Change Muffling Settings to Internal Muffling only by Default


