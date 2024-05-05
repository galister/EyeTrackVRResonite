# EyeTrackVRResonite Fork for OscAvMgr support

This fork is made specifically to work with [OscAvMgr](https://github.com/galister/oscavmgr). It supports both Face and Eye tracking.

# Original Readme

A [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader) mod for [Resonite](https://resonite.com/) that enables the use of [EyeTrackVR](https://github.com/EyeTrackVR/EyeTrackVR).

## Usage
1. Install [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader).
2. Place [EyeTrackVRResonite.dll](https://github.com/Meister1593/EyeTrackVRResonite/releases) into your `rml_mods` folder. This folder should be at `C:\Program Files (x86)\Steam\steamapps\common\Resonite\rml_mods` on windows or `$HOME/.steam/steam/steamapps/common/Resonite/rml_mods` on linux for a default installation. You can create it if it's missing, or if you launch the game once with ResoniteModLoader installed it will create the folder for you.
3. Place [OscCore.dll](https://github.com/Meister1593/EyeTrackVRResonite/releases) into your Resonite base folder, one above your 'rml_mods' folder. This folder should be at `C:\Program Files (x86)\Steam\steamapps\common\Resonite` on windows or `$HOME/.steam/steam/steamapps/common/Resonite` on linux for a default installation.
4. Start EyeTrackVR software before running the game, otherwise it won't reconnect in-game.
5. Check if it's working by looking at eyes in the mirror.

# Libraries used:
- [OscCore](https://github.com/tilde-love/osc-core) - MIT License
