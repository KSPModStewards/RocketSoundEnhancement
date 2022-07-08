# Rocket Sound Enhancement
Rocket Sound Enhancement (RSE) is an Audio Plugin Framework Mod for [Kerbal Space Program](https://www.kerbalspaceprogram.com/) that offers modders advance sound effects features not available in the base game. 
It features a robust Layering System for use of multiple sounds just like in other games (eg: FMod). 

By itself, RSE only does Sound Limiting and Muffling. You'll need to download a Config Pack Mod in order to have new sounds in your game.
If you release a mod that uses RSE, let me know so I can add it here in a list!

## Config and Sound Mods
- [Rocket Sound Enhancement Default](https://github.com/ensou04/RocketSoundEnhancementDefault) (The Default Sound Library and Config)

## Features
### Audio Muffler
- **Normal**: Mixer based Audio Muffler with Dedicated channels for Exterior Sounds, Focused Vessel and Interior. With Doppler Effect.
- **AirSim** Lite: Simple Mach Effects and Sonic Booms
- **AirSim**: Parts with RSE Modules will simulate realistic sound attenuation over distance, comb-filtering, mach effects, sonic booms (via ShipEffects) and distortion. Stock sound sources has basic mach and distance attenuation (volume or filter based) support.

### Master Audio Limiter/Compressor
Sound Mastering that controls the overall loudness of the game with Adjustable Amount for different dynamics with the "Auto-Limiter" feature or you can tweak your own settings.

### Part Modules
Part Modules dedicated for assigning and controlling sound effects for Parts beyond what the regular stock audio can do. Including parts that didn't have any support for sounds before like Rotors, Wheels and Kerbal Jetpacks.
- RSE_Engines
- RSE_RCS
- RSE_Wheels
- RSE_RotorEngines
- RSE_KerbalEVA (For Jetpacks)
- RSE_Coupler
- RSE_AUDIO (A Simpler non-layer EFFECTS Node version of RSE Modules for drop-in replacement of Stock AUDIO{} with full AirSim Support and a more direct Muffling Support.)

### ShipEffects 
**Physics based Sound Effects System**
- Add Sounds and assign it's Pitch or Volume to any physics event available for each Vessel. (air pressure, jerk, g-forces, etc)
- Add Sonic Boom sound during Mach Events.
- Replace or Disable Staging Sound Effects.
- ShipEffectsCollisions (Part Module) - Add Collision Sound Effects for different impact surfaces to any Part.
