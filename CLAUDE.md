# CLAUDE.md
Guide for Claude Code with the most important, current details about this repo.

## Purpose and scope
- MuOnline client clone built on .NET 10 + MonoGame 3.8+.
- Uses Season 6 protocol; consumes Season 20 (1.20.61) client data for assets.
- Intended to connect to OpenMU (or any Season 6 compatible server).
- Educational/research focus; do not commit proprietary game data.

## High-level architecture
- Platform heads per runtime (Windows DX/GL, Linux, macOS, Android, iOS) call into shared core libraries.
- Core loop lives in `Client.Main` with rendering, scenes, networking, task scheduling, and UI.
- Data readers in `Client.Data` parse MU asset formats and feed rendering/logic.
- Each head passes a `MonoGameFramework` property to pull the correct MonoGame package at restore/build.

## Repository layout (where to look)
- `Client.Main/`: Core engine.
- `Client.Main/Scenes/`: `BaseScene`, `LoginScene`, `LoadScene`, `SelectCharacterScene`, `ServerConfigScene`, `GameScene`, test scenes.
- `Client.Main/Controls/`: UI and world controls (terrain, UI layer, world base classes).
- `Client.Main/Objects/`: Runtime objects (players, monsters, NPCs, effects, items, map tiles, worlds).
- `Client.Main/Objects/Worlds/`: World-specific setups (Lorencia, Noria, Devias, Arena, Icarus, etc.).
- `Client.Main/Networking/`: Packet router, builder, handlers, services.
- `Client.Main/Core/Client/`: `CharacterState`, `PartyManager`, `ScopeManager`, enums.
- `Client.Main/Core/Utilities/`: Databases and attributes (`ItemDatabase`, `MapDatabase`, `NpcDatabase`, `CharacterClassDatabase`, `WorldInfoAttribute`, `NpcInfoAttribute`, `PacketHandlerAttribute`).
- `Client.Main/Core/Models/`: `ScopeObject`, `ServerInfo`.
- `Client.Main/Graphics/` and `Client.Main/Effects/`: Rendering helpers/effects.
- `Client.Main/Content/`: Shaders/content pipeline assets.
- `Client.Data/`: File readers (`BMD`, `ATT`, `MAP`, `OZB/OZG`, `CWS`, `OBJS`, `Texture`, `LANG`, `CAP`, `ModulusCryptor`).
- `Client.Editor/`: Asset tooling (not required for runtime).
- Heads: `MuWinDX/`, `MuWinGL/`, `MuLinux/`, `MuMac/`, `MuAndroid/`, `MuIos/`.
- Shared props: `Client.Main.Shared.props`, `Client.Data.Shared.props` (propagate `MonoGameFramework`).
- Solution: `MuOnline.sln`.

## Key classes (by role)
- Entry/config: `Client.Main/MuGame` singleton (boot, configuration, DI-like accessors, main-thread scheduling).
- Scenes: `Scenes/BaseScene` (lifecycle), `LoginScene`, `LoadScene`, `SelectCharacterScene`, `GameScene`, `ServerConfigScene`.
- Scheduling: `TaskScheduler` (priority queue, frame budget), use `MuGame.ScheduleOnMainThread` for UI/main-thread work.
- Rendering: `Controls/WorldControl`, `Controls/TerrainControl`, `Controls/UI/GameControl` hierarchy; `Objects/ModelObject`, `DynamicLight`, `WalkableWorldControl`.
- World content: `Objects/Worlds/<WorldName>` classes to configure terrain/assets.
- Objects: `PlayerObject`, `MonsterObject`, `NPCObject`, `DroppedItemObject`, `CursorObject`, `MapTileObject`, `Effects/*`, `Particles`.
- Networking: `Networking/PacketRouter`, `PacketBuilder`, handlers under `Networking/PacketHandling/Handlers/*`, services under `Networking/Services/*` (`LoginService`, `CharacterService`, `ConnectServerService`).
- State: `CharacterState` (single source of truth for stats, inventory, etc.), `ScopeManager` (objects in view), `PartyManager`.
- Data lookups: `ItemDatabase`, `SkillDatabase`, `MapDatabase`, `NpcDatabase`, `AppearanceConfig`, `PlayerActionMapper`.
- Attributes: `PacketHandlerAttribute`, `WorldInfoAttribute`, `NpcInfoAttribute`, `SubCodeHolder` helper.
- Configuration: `Constants` (defaults per build flag), `appsettings.json` (host/port, graphics).

## Networking specifics
- Protocol: Season 6, C1/C3 packet structures; attribute-based handler registration (`[PacketHandler(main, sub)]`).
- Router switches modes between ConnectServer and GameServer; services build outgoing packets.
- Threading: packet handling often on background threads; marshal UI/scene updates with `MuGame.ScheduleOnMainThread`.
- Connect flow: `ConnectServerService` (list/select servers) → `LoginService` → `CharacterService` → game packets via `PacketRouter`.
- Scope updates: `ScopeHandler` manages spawn/despawn; `CharacterDataHandler` updates stats/inventory.

## Rendering and UI
- MonoGame-based; uses virtual UI resolution (see `appsettings.json` and `Constants` base UI width/height).
- Terrain/render controls under `Controls/Terrain` and `Controls/UI`; `UiScaler` inside controls manages virtual-to-actual mapping.
- Shader content in `Client.Main/Content`; backends differ by DX/GL but share shader sources with conditional compilation.
- Lighting flags and quality switches configured in `Constants` (dynamic lighting, buffer pooling, render scale, vsync).

## Data formats handled
- BMD (models/animations), ATT (walkability), MAP (heightmap), OZB/OZG (textures), CWS (camera), OBJS (object placements), LANG (localization), CAP (capsule data), Texture atlases.
- Data readers live in `Client.Data/<FORMAT>` folders; consumed by `Client.Main` loaders/managers.

## Configuration and environment
- Set `Client.Main/Constants.cs` `DataPath` to your local MU data folder; defaults differ in Debug (Windows path) vs Release (relative).
- `Client.Main/appsettings.json`: host/port, protocol version, client version/serial, graphics size/fullscreen, logging levels.
- Do not commit proprietary data or credentials; keep environment-specific paths local.

## Build and run commands
- Restore tools: `dotnet tool restore`.
- Build heads (pass MonoGame framework on Windows):
- `dotnet build ./MuWinDX/MuWinDX.csproj -c Debug -p:MonoGameFramework=MonoGame.Framework.WindowsDX`
- `dotnet build ./MuWinGL/MuWinGL.csproj -c Debug -p:MonoGameFramework=MonoGame.Framework.DesktopGL`
- `dotnet build ./MuLinux/MuLinux.csproj -c Debug`
- `dotnet build ./MuMac/MuMac.csproj -c Debug`
- Run examples:
- DX11: `dotnet run --project ./MuWinDX/MuWinDX.csproj -f net10.0-windows -c Debug -p:MonoGameFramework=MonoGame.Framework.WindowsDX`
- GL: `dotnet run --project ./MuWinGL/MuWinGL.csproj -f net10.0-windows -c Debug -p:MonoGameFramework=MonoGame.Framework.DesktopGL`
- Linux: `dotnet run --project ./MuLinux/MuLinux.csproj -f net10.0 -c Debug`
- Publish: `dotnet publish <head>.csproj -c Release -r <rid> [-p:MonoGameFramework=...]`.

## Coding conventions
- C# 10, 4-space indentation, Allman braces.
- `PascalCase` for types/methods, `camelCase` for locals/fields, `Async` suffix for async methods.
- Follow scene/handler patterns; return `Task` from handlers; prefer async/await.
- Marshal UI changes to the main thread via `MuGame.ScheduleOnMainThread`.
- Keep platform-specific references isolated in head projects; do not mix DX/GL packages.
- Avoid large inline data; prefer configuration and databases under `Core/Utilities`.

## Threading model
- MonoGame render/update on main thread; networking async on background threads.
- Use `MuGame.ScheduleOnMainThread` for any UI/scene/state mutation affecting rendering.
- `TaskScheduler` limits per-frame workload; enqueue work with priorities to avoid frame drops.

## State management
- `CharacterState` stores stats, position, inventory, skills; events propagate changes.
- `ScopeManager` tracks in-view objects by masked IDs; use provided methods for add/update/remove.
- `PartyManager` handles party data; `ServerInfo` describes server endpoints.

## Scene flow
- `LoadScene` handles boot/loading.
- `LoginScene` manages credentials/server select; `ServerConfigScene` for config.
- `SelectCharacterScene` for character selection.
- `GameScene` orchestrates in-game world, UI, networking hooks.
- Test scenes (`TestScene`, `TestAnimationScene`) exist for experimentation.

## Controls and UI components
- Base: `Controls/GameControl` (lifecycle, events, children), used across UI/world.
- Terrain: `Controls/TerrainControl`, `WalkableWorldControl`.
- UI layer under `Controls/UI` (HUD, panels) and `Objects/Logo` for branding.
- World controls manage camera, selection, and rendering passes.

## Objects and worlds
- `Objects/PlayerObject`, `MonsterObject`, `NPCObject`, `DroppedItemObject`, `ModelObject`, `CursorObject`, `MapTileObject`.
- Effects/Particles under `Objects/Effects` and `Objects/Particles`.
- `Objects/Worlds/<WorldName>` configure terrain assets and placements for specific maps (Lorencia, Noria, Devias, Arena, Icarus, Atlans, Login, SelectWorld).
- Vehicle/wings under `Objects/Vehicle`, `Objects/Wings`.

## Shaders and content pipeline
- Shader sources reside in `Client.Main/Content` and use conditional compilation for DX/GL parity (e.g., `#if OPENGL` blocks). Keep changes backend-agnostic when possible.
- The `MonoGameFramework` property selected at build drives shader profile selection (DesktopGL vs WindowsDX). Always pass it on Windows heads so dependent projects restore matching content pipeline targets.
- When adding/modifying shaders, test both MuWinDX and MuWinGL builds; ensure effect parameters remain aligned with C# bindings in rendering controls/objects.
- New content must be referenced in the relevant head project so it is copied into the output/publish directories.

## Data utilities and lookup tables
- `ItemDatabase`, `SkillDatabase`, `MapDatabase`, `NpcDatabase`, `CharacterClassDatabase` provide structured info for rendering and logic.
- `AppearanceConfig` maps models/skins; `PlayerActionMapper` links inputs to actions.
- `WorldInfoAttribute` and `NpcInfoAttribute` annotate classes with IDs for discovery.

## Graphics and performance knobs
- `Constants`: render scale, MSAA flag, dynamic lighting, GPU lighting, buffer pooling, vsync, quality toggles.
- Lighting defaults: sun direction/strength, shadow strength, high-quality textures toggle.
- Debug flags: show debug panel, bounding boxes, low-quality switch; set differently for Debug vs Release.
- Android defaults reduce quality (render scale, lighting) for performance.

## Logging and diagnostics
- Logging levels configured in `appsettings.json` (can set specific namespaces to Trace for networking).
- Debug builds enable debug panel and disable background music by default.
- Use task scheduler priorities to prevent stalls when adding expensive work.

## Testing and verification
- No automated test suite yet; run the head you touched and capture commands.
- Smoke checks: build head, connect to server, login, switch scenes, basic movement/rendering, packet send/receive sanity.
- Prefer deterministic data fixtures over live servers when adding tests.

## Contribution guidance
- Keep commits small, present-tense summaries (see git history like “bonfire improvements - sparks and smoke”).
- PRs: include intent, affected platforms, commands executed, screenshots/clips for visual changes, mention DataPath/appsettings expectations.
- Respect existing patterns (packet handlers, scene lifecycle, task scheduling).

## Common pitfalls
- Forgetting `-p:MonoGameFramework=...` on Windows → wrong MonoGame package restored.
- Incorrect `DataPath` → missing assets/black screens.
- Updating UI from network threads → crashes; always marshal to main thread.
- Mixing DX and GL references across projects → package restore conflicts.
- Large proprietary assets or secrets must not be committed.
- Ignoring frame budget when queuing tasks → hitches; use scheduler priorities.

## Quick checklist for new changes
- Set local `DataPath` and `appsettings.json` before running.
- Build the specific head you modify with correct MonoGame property.
- Marshal UI changes through `MuGame.ScheduleOnMainThread`.
- Add/update packet handlers with `[PacketHandler]` and return `Task`.
- Touch databases/configs instead of hardcoding IDs inline.
- Document run/build commands in your PR description.
