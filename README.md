# Rocket Sound Enhancement

Rocket Sound Enhancement (RSE) is an Audio Plugin Framework Mod for [Kerbal Space Program](https://www.kerbalspaceprogram.com/) that offers modders advance sound effects features not available in the base game. 
It features a robust Layering System for use of multiple sounds just like in other games (eg: FMod). 

By itself, RSE only does Sound Limiting and Muffling. You'll need to download a Config Pack Mod in order to have new sounds in your game.
If you release a mod that uses RSE, let me know so I can add it here in a list!

Get the Default Config here:
[Rocket Sound Enhancement Default](https://github.com/ensou04/RocketSoundEnhancementDefault)


## Features
### Settings Window
![Settings Window](https://i.imgur.com/uO4F1d7.png)

### Built-In Basic Audio Muffler
A Lightweight Lowpass Filter.

**Presets:**

    - Interior-Only (Only Muffle sounds when the Camera is in IVA view)
    - Muffled-Vacuum
    - Silent-Vacuum
    - Custom (User Customizable Preset)
    - Add more Presets in the Settings.cfg

### Master Audio Limiter/Compressor
Control the Loudness of the overall game dynamically.

**Presets:**

    - Balanced (The Default Preset, Sounds are balanced with Reasonable Dynamic Range)
    - Cinematic (Wide Dynamic Range, Loud Sounds are Louder and Quiet Sound Are Quieter than Balanced)
    - NASA-Reels (Inspired by Real Rocket Launch Footage, This is the Loudest Preset with minimal Dynamic Range)
    - Custom (User Customizable Preset)
    - Add more Presets in the Settings.cfg

### Part Modules
Apply sounds with Layering Capabilities with these part modules. 
For eg: Using a different Loop sample for low thrust compared to High Thrust on a single Engine.
- RSE_Engines
- RSE_RCS
- RSE_Wheels

Replace and/or Add Sounds to Decouplers, Launch Clamps and Docking Ports.
- RSE_Coupler

### ShipEffects & ShipEffectsCollisions 
**Physics based Sound Effects System**
- Add sounds and assign it's Pitch or Volume to any physics variable available for each Vessel
- Add Collision Sound Effects for different surfaces to any Part
- Replace or Disable Staging Sound Effects

## Dependencies
- [Module Manager](https://github.com/sarbian/ModuleManager)
