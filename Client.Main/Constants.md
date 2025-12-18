Constants quick reference
=========================

Where the values live
---------------------
- File: `Client.Main/Constants.cs`
- Defaults are applied in three passes:
  1) Base defaults (all platforms)
  2) Debug overrides (`#if DEBUG`) – enables debug panel, turns off music, sets Windows data path
  3) Android overrides (`#if ANDROID`) – trims effects for performance and lowers render scale

Key performance-related switches
--------------------------------
- Render scale: `RENDER_SCALE` (base 1.0, Android 0.75). Lower = fewer pixels rendered (biggest FPS lever). Not loaded from config; change via code or pause menu.
- Texture filtering: `HIGH_QUALITY_TEXTURES` (base true, Android false). Turning off removes anisotropic filtering, saves GPU time.
- Grass: `DRAW_GRASS` (base true, Android false). Disable to reduce overdraw.
- Dynamic lights: `ENABLE_DYNAMIC_LIGHTS` (base true, Android false). Disabling removes dynamic lights from terrain/objects.
- Lighting shaders: `ENABLE_DYNAMIC_LIGHTING_SHADER`, `ENABLE_TERRAIN_GPU_LIGHTING` (base true, Android false). Disabling swaps to simpler lighting.
- Material shaders: `ENABLE_ITEM_MATERIAL_SHADER`, `ENABLE_MONSTER_MATERIAL_SHADER` (base true, Android false). Disabling removes special item/monster effects.
- Trails: `ENABLE_WEAPON_TRAIL` (base true, Android false). Cosmetic; disable to save fill-rate.
- Integrated GPU mode: `OPTIMIZE_FOR_INTEGRATED_GPU` (base false, Android true). Uses fewer lights.
- MSAA: `MSAA_ENABLED` (base false). Leave off on mobile; raising it costs a lot.
- VSync/Framerate: `DISABLE_VSYNC` (base true) and `UNLIMITED_FPS` (base true). Turn VSync on if you want capped FPS, but leave off for max performance.
- Batch optimizations: `ENABLE_BATCH_OPTIMIZED_SORTING` (base true). Keep enabled for draw-call savings.

Other notable settings
----------------------
- Audio: `BACKGROUND_MUSIC`, `SOUND_EFFECTS`, and their volumes.
- UI: `SHOW_DEBUG_PANEL`, `DRAW_BOUNDING_BOXES`, `DRAW_BOUNDING_BOXES_INTERACTIVES`.
- Paths: `DataPath` (base: app base dir; Windows Debug: `C:\Games\MU_Red_1_20_61_Full\Data`), plus `DataPathUrl`/`DefaultDataPathUrl`.
- Camera: yaw/pitch, zoom, and limits constants live here (rarely changed).

How to change
-------------
- Code defaults: edit `Constants.cs`.
- Per-build overrides: use compilation symbols (`DEBUG`, `ANDROID`, `WINDOWS`).
- Runtime tweaks: render scale and many toggles are exposed in the pause menu; others can be changed and hot-reloaded only via code.
