CURRENT:
- Potental Fix for Collision Loop Sound Effects Persisting after Impact
- Tweak Orbit View Muffling
- Removed "MuffleChatterer" from LOWPASSFILTER in Settings.cfg
- Added "AffectChatterer" under RSE_SETTINGS in Settings.cfg

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


