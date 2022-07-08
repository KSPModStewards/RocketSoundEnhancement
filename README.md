# Rocket Sound Enhancement
Rocket Sound Enhancement (RSE) is an Audio Plugin Framework Mod for [Kerbal Space Program](https://www.kerbalspaceprogram.com/) that offers modders advance sound effects features not available in the base game. 
It features a robust Layering System for use of multiple sounds just like in other games (eg: FMod). 

By itself, RSE only does Sound Limiting and Muffling. You'll need to download a Config Pack Mod in order to have new sounds in your game.
If you release a mod that uses RSE, let me know so I can add it here in a list!

## Config and Sound Mods
- [Rocket Sound Enhancement Default](https://github.com/ensou04/RocketSoundEnhancementDefault) (The Default Sound Library and Config)

## Features
### Audio Muffler - Normal
Mixer based Audio Muffler with Dedicated channels for Exterior Sounds, Focused Vessel and Interior while ignoring UI sounds like Music.

### Audio Muffler - AirSim & AirSim Lite
 An Audio Muffler that works on top of the regular muffler that takes into account the Mach Angle, Velocity and Distance of a Vessel with Sonic Booms Effects (provided by a Config Pack via ShipEffects). 

Parts with RSE Modules will simulate realistic sound attenuation over distance, comb-filtering, and distortion. Game Events like distant explosions will now sound muffled just like in real life.

**AirSim Lite** is a version of **AirSim** that does similar effects without using sound filters.

### Additional Effects
RSE also adds a more robust Doppler Effect.

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

### ShipEffects - Physics based Sound Effects System
- Add Sounds and assign it's Pitch or Volume to any physics event available for each Vessel. (air pressure, jerk, g-forces, etc)
- Add Sonic Boom sound during Mach Events.
- Replace or Disable Staging Sound Effects.
- ShipEffectsCollisions (Part Module) - Add Collision Sound Effects for different impact surfaces to any Part.

### Known Issues
- Sound glitches and stuttering might occur in large part ships and more so when AirSim is enabled or when loading in-between scenes.
- Stock sounds might be un-muffled for a single frame or two. Replacing AUDIO with RSE_AUDIO in Part's EFFECTS node via it's config fixes this issue.
