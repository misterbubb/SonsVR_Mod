# Sons of the Forest VR Mod (Unofficial)

> **Bring full VR support to *Sons of the Forest* using SteamVR and OpenVR — without official VR mode.**

This mod injects a complete VR pipeline into *Sons of the Forest* (PC version), enabling immersive gameplay using **Valve Index**, **HTC Vive**, **Meta Quest (via Air Link/Virtual Desktop)**, and other **OpenVR-compatible headsets**.

Built with **MelonLoader**, **Il2CppInterop**, and **SteamVR Plugin**, it replaces mouse/keyboard input with natural hand-based interaction and stereo rendering — all while preserving the original game logic.

---

## ✅ Current Features (0.0.2 Alpha)

- **Full Stereo Rendering**  
  Custom left/right eye cameras with OpenVR projection matrices, dynamic resolution scaling, and native texture submission to the compositor.

- **VR Locomotion & Movement**
    - Left thumbstick: walk forward/backward, strafe left/right
    - Right thumbstick **forward**: toggle sprint
    - Smooth body rotation aligned to HMD direction (look where you go)

- **Snap Turning**  
  Rotate your view in fixed increments (default: 30°) using the **right thumbstick (left/right)** — reduces motion sickness.

- **Gesture-Based Actions**
    - **Jump**: Raise both controllers quickly upward
    - **Melee Attack**: Swing right hand fast (velocity-based detection)
    - **Interact/Use**: Squeeze **left or right grip** → mapped to `E`
    - **Inventory**: Press **Y button (left controller)** → mapped to `I`
    - **Crouch**: Push **right thumbstick down** → mapped to `Left Ctrl`


- **Action and Key Mapping**
- FORWARD: W Key              : Left.Thumb-UP 
- BACKWARD: S Key             : Left.Thumb-DoWN
- RIGHT: D Key                : Left.Thumb-RIGHT 
- LEFT: A Key                 : Left.Thumb-LEFT 
- CROUCH: Left Ctrl Key       : Right.Thumb-DOWN
- DROP: G Key                 : Left.GRIP or Right.GRIP  Long grip release 
- INVENTORY: I Key            : Left.Y-BUTTON 
- JUMP: Space Bar Key         : "GESTURE MUVE UP BOTH MC"
- LIGHTER: L Key              : Right.B-BUTTON
- RELOAD: R Key               : Right.A-BUTTON
- RUN: Left Shift Key         : Right.Thumb-UP
- TAKE: E Key                 : Left.GRIP or Right.GRIP
- DISMANTLE: C Key 
- UTILITY: UNBOUND 
- GPS TRACKER: M Key          : Left.GRIP on Right Sholder 
- WALKIE-TALKIE: T Key        : Right.GRIP on Left Sholder 
- BOOK: B Key                 : Right.GRIP on Right Sholder
- ROTATE RIGHT: R Key         : Right.A-BUTTON
- ROTATE LEFT: Q Key 
- PRIMARY ACTION: LMB         : Right.TRIGGER
- SECONDARY ACTION: RMB       : Left.TRIGGER  and "MELE ATTACK GESTURE"
- INTERACT: LMB               : Right.TRIGGER 
- ALTERNATE INTERACT: RMB     : Left.TRIGGER
- PLACE ELEMENT: LMB          : Right.TRIGGER 
- TOGGLE PLACE MODE: RMB      : Left.TRIGGER 
- TERTIARY ACTION: MMB 
- SLEEP: Z Key
- SAVE: E Key                 : Left.GRIP or Right.GRIP 
- SKIP: S Key                 : Left.Thumb-DoWN
- RESET: UNBOUND 
- SELECT: E Key               :Left.GRIP or Right.GRIP 
- BACK: Left Arrow Key 
- CANCEL STRUCTURE: X Key 
- TOGGLE BOOK MODE: X Key 
- BOOK FLIP NEXT PAGE: RMB     : Left.TRIGGER 
- BOOK FLIP PREVIOUS PAGE: LMB : Right.TRIGGER 
- PLAC: C Key 
- CYCLE GRAB BAG CATEGORY: Q Key


- **Automatic Camera & Player Detection**  
  Dynamically finds the game’s `MainCameraFP` and `LocalPlayer` across scenes (main world, caves, interiors).

- **Mouse & Keyboard Emulation**  
  All VR gestures are translated into synthetic keyboard/mouse events the game understands — **no game code changes needed**.

- **Compatibility**  
  Works with existing Sons of the Forest mods and saves. No modification of game files required.

---

## ⚙️ Requirements

- **Game**: *Sons of the Forest* (Steam, PC version)
- **VR Headset**: Any **OpenVR-compatible** device (Index, Vive, Quest via SteamVR, etc.)
- **Mod Loader**: [MelonLoader](https://melonwiki.xyz/) (v0.6.0)
- **Runtime**: .NET 6+ 

---

## 📥 Installation

1. Install **MelonLoader** into your *Sons of the Forest* folder.
2. Copy the `SonsVR_Mod.dll` file into `SonsOfTheForest/Mods/`.
3. Ensure `openvr_api.dll` and `SonsSteamVR_IL2CPP.dll` is present in `SonsOfTheForest/UserLibs/` (included in release).
4. Launch the game in **VR mode** (via SteamVR).

> 💡 **Note**: The game must be launched **through SteamVR**. Do not use desktop mode.

---

## 🛠 Configuration (Future)

All settings (IPD, snap angle, offsets, sensitivity) are currently hardcoded. A `settings.ini` file will be added soon for easy user customization.

---

## 📋 TODO / Roadmap

### Planned Features & Fixes

- ✅ **Add `settings.ini`**  
  Export all hardcoded values (position offset, snap angle, thresholds, etc.) to an external config file.

- 🔧 **Fix Camera & Position Handling**  
  Improve detection and switching logic for dynamic cameras (vehicles, cutscenes, UI scenes).

- 🎮 **User Interface & In-Game Menus**  
  Add VR-native menus for settings, comfort options (snap turn toggle, smooth turn), and inventory navigation.

- 🕹 **Implement Missing Game Actions**  
  Map critical gameplay actions (e.g., building, crafting, weapon switching, context actions) to motion controller inputs.

- 🤖 **Full 6DOF Arm & Hand IK**  
  Implement inverse kinematics for arms/hands to match real-world controller poses (not just camera rotation).

- ⚡ **Optimize Stereo Rendering Pipeline**  
  Improve performance via:
    - Single-pass stereo (if feasible)
    - Dynamic resolution scaling
    - Render texture reuse
    - Async reprojection hints

### Additional Suggested Improvements

- 🧭 **Comfort Options**  
  Add smooth turning, vignette-based tunneling, and height calibration.

- 🗣 **VR Audio Support**  
  Integrate HRTF or SteamVR audio listeners for spatial sound.

- 🧱 **UI in VR**  
  Render game HUD/UI on a virtual screen or world-locked panel (avoid screen-space overlay).

- 🔄 **Pause & Menu Handling**  
  Auto-disable HMD look during pause/inventory to prevent disorientation.

- 📊 **Performance HUD**  
  Optional FPS/MSAA/eye texture stats for debugging.

---

## ⚠️ Known Limitations

- **No official VR support**: Some UI elements may appear on-screen (not in VR space).
- **Weapon/Tool alignment**: Held items are not yet aligned to hand poses (6DOF).
- **Cutscenes & Vehicles**: May break immersion or camera tracking (WIP).
- **Performance**: Render resolution is high by default; may require supersampling adjustment in SteamVR.

---

## 🙌 Credits

- **Mod Author**: Antonio Mauriello (`Anthony`)
- **Based on**: MelonLoader, Il2CppInterop, SteamVR Unity Plugin
- **Special Thanks**: MelonLoader & Sons of the Forest modding communities

---

## 📜 License

Copyright (C) 2025 Antonio Mauriello

This program is free software: you can redistribute it and/or modify  
it under the terms of the **GNU General Public License as published by**  
the Free Software Foundation, **version 2** of the License.

This program is distributed in the hope that it will be useful,  
but WITHOUT ANY WARRANTY; without even the implied warranty of  
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the  
GNU General Public License for more details.

You should have received a copy of the GNU General Public License  
along with this program.  If not, see <https://www.gnu.org/licenses/gpl-2.0.html>.

*Sons of the Forest* is a trademark of Endnight Games Ltd.  
This mod is **not affiliated** with Endnight Games, Steam, or Valve Corporation.
---

> 🌐 **Enjoy VR survival horror — now in full immersion!**  
> *"The forest remembers..." — now in stereo 3D.*