# Repository Guidelines

Comprehensive contributor guide for this MuOnline client project.

## Purpose and scope
- MuOnline client clone built on .NET 10 + MonoGame 3.8+.
- Uses Season 6 protocol; reads Season 20 (1.20.61) client data for assets.
- Targets OpenMU or any compatible S6 server; educational/research intent—do not commit proprietary data.

## High-level architecture
- Platform heads (MuWinDX, MuWinGL, MuLinux, MuMac, MuAndroid, MuIos) are thin launchers pointing to shared core.
- `Client.Main` hosts rendering, scenes, UI, networking, scheduling, and game objects.
- `Client.Data` parses MU asset formats and feeds the core.
- Windows heads must pass `MonoGameFramework` to select the correct MonoGame package and shader profile.

## Repository layout
- `Client.Main/`: Core engine and content.
- Scenes: `Client.Main/Scenes/` (`BaseScene`, `LoginScene`, `LoadScene`, `SelectCharacterScene`, `ServerConfigScene`, `GameScene`, tests).
- UI/world controls: `Client.Main/Controls/` (terrain, UI base `GameControl`, world controls).
- Objects: `Client.Main/Objects/` (player, monsters, NPCs, items, effects, particles, worlds).
- Worlds: `Client.Main/Objects/Worlds/<WorldName>` (Lorencia, Noria, Devias, Arena, Icarus, Atlans, Login, SelectWorld).
- Networking: `Client.Main/Networking/` (`PacketRouter`, `PacketBuilder`, handlers, services).
- State/logic: `Client.Main/Core/Client/` (`CharacterState`, `ScopeManager`, `PartyManager`, enums).
- Databases/utilities: `Client.Main/Core/Utilities/` (`ItemDatabase`, `SkillDatabase`, `MapDatabase`, `NpcDatabase`, `CharacterClassDatabase`, `AppearanceConfig`, `PlayerActionMapper`, `WorldInfoAttribute`, `NpcInfoAttribute`, `PacketHandlerAttribute`).
- Models: `Client.Main/Core/Models/` (`ScopeObject`, `ServerInfo`).
- Graphics/effects: `Client.Main/Graphics/`, `Client.Main/Effects/`.
- Content/shaders: `Client.Main/Content/`.
- Data readers: `Client.Data/<FORMAT>` (BMD, ATT, MAP, OZB/OZG, CWS, OBJS, Texture, LANG, CAP, ModulusCryptor).
- Editor: `Client.Editor/` (asset tooling).
- Shared props: `Client.Main.Shared.props`, `Client.Data.Shared.props`; solution: `MuOnline.sln`.

## Key classes and roles
- Core entry/config: `MuGame` singleton (boot, config, DI-like accessors, main-thread marshalling via `ScheduleOnMainThread`).
- Scenes: `BaseScene` lifecycle; `LoginScene`, `LoadScene`, `SelectCharacterScene`, `GameScene`, `ServerConfigScene`, test scenes.
- Scheduling: `TaskScheduler` (priority queue with frame budget).
- Networking: `PacketRouter`, `PacketBuilder`, handlers under `Networking/PacketHandling/Handlers/*`, services under `Networking/Services/*` (`LoginService`, `CharacterService`, `ConnectServerService`).
- State: `CharacterState` (stats, inventory, skills), `ScopeManager` (objects in view), `PartyManager`.
- Rendering/UI: `Controls/GameControl` base; `WorldControl`, `TerrainControl`, `WalkableWorldControl`, `DynamicLight`, `ModelObject`, `MapTileObject`.
- World content: `Objects/Worlds/*` configure terrain/assets per map; vehicles/wings under `Objects/Vehicle`, `Objects/Wings`.
- Data lookup: `ItemDatabase`, `SkillDatabase`, `MapDatabase`, `NpcDatabase`, `CharacterClassDatabase`, `AppearanceConfig`, `PlayerActionMapper`.

## Networking specifics
- Season 6 protocol; handlers registered via `[PacketHandler(main, sub)]`.
- Router switches modes between ConnectServer and GameServer; services assemble outgoing packets.
- Threading: packet handling may occur off the main thread—marshal UI/scene mutations with `MuGame.ScheduleOnMainThread`.
- Flow: ConnectServer list/select → login → character selection → game packets drive scope updates (`ScopeHandler`) and state updates (`CharacterDataHandler`).

## Rendering, UI, and shaders
- MonoGame-based; UI uses virtual resolution (see `Constants` and `appsettings.json`).
- Controls handle coordinate scaling; world controls manage camera, selection, rendering passes.
- Shader sources in `Client.Main/Content` use conditional compilation for DX/GL parity. The `MonoGameFramework` property selects shader profiles; always pass it on Windows builds.
- When touching shaders, test both MuWinDX and MuWinGL; ensure effect parameter bindings align with C# code.
- New content must be referenced so it is copied into head outputs/publish directories.

## Data formats
- Supported: BMD (models/animations), ATT (walkability), MAP (heightmap), OZB/OZG (textures), CWS (camera), OBJS (placements), LANG (localization), CAP (capsule data), Texture atlases.
- Readers live in `Client.Data/<FORMAT>` and feed loaders in `Client.Main`.

## Configuration and environment
- Set `Client.Main/Constants.cs` `DataPath` to your local MU data; debug defaults point to Windows path, release uses relative `Data`.
- `Client.Main/appsettings.json`: host/port, protocol version, client version/serial, graphics (size/fullscreen/UI virtual), logging levels.
- Keep environment-specific paths and credentials local; do not commit proprietary assets.

## Build, run, and publish
- Restore tools once: `dotnet tool restore`.
- Build heads (Windows must pass MonoGame framework):
  - DX11: `dotnet build ./MuWinDX/MuWinDX.csproj -c Debug -p:MonoGameFramework=MonoGame.Framework.WindowsDX`
  - OpenGL: `dotnet build ./MuWinGL/MuWinGL.csproj -c Debug -p:MonoGameFramework=MonoGame.Framework.DesktopGL`
  - Linux/macOS: `dotnet build ./MuLinux/MuLinux.csproj -c Debug` / `dotnet build ./MuMac/MuMac.csproj -c Debug`
- Run examples:
  - DX11: `dotnet run --project ./MuWinDX/MuWinDX.csproj -f net10.0-windows -c Debug -p:MonoGameFramework=MonoGame.Framework.WindowsDX`
  - GL: `dotnet run --project ./MuWinGL/MuWinGL.csproj -f net10.0-windows -c Debug -p:MonoGameFramework=MonoGame.Framework.DesktopGL`
  - Linux: `dotnet run --project ./MuLinux/MuLinux.csproj -f net10.0 -c Debug`
- Publish: `dotnet publish <head>.csproj -c Release -r <rid> [-p:MonoGameFramework=...]`.

## Coding style and patterns
- C# 10, 4-space indentation, Allman braces; `PascalCase` for types/methods, `camelCase` for locals/fields, `Async` suffix for async methods.
- Follow scene and packet-handler patterns; return `Task` from handlers; prefer async/await.
- Marshal UI changes to the main thread with `MuGame.ScheduleOnMainThread`.
- Keep DX/GL references isolated per head; avoid mixing packages.
- Use databases/configs for IDs and metadata instead of hardcoding.

## Threading and scheduling
- MonoGame render/update on main thread; networking async background.
- Use `MuGame.ScheduleOnMainThread` for any UI/scene/state mutations affecting rendering.
- `TaskScheduler` enforces frame budget; enqueue with priorities to avoid hitches.

## State management
- `CharacterState` is the source of truth for player stats, inventory, skills; events propagate updates.
- `ScopeManager` manages in-view objects via masked IDs; use add/update/remove helpers.
- `PartyManager` and `ServerInfo` handle party data and server metadata.

## Scene flow
- `LoadScene` boot/loading → `LoginScene`/`ServerConfigScene` for credentials/server → `SelectCharacterScene` → `GameScene` (in-game orchestration).
- Test scenes (`TestScene`, `TestAnimationScene`) available for experiments.

## Graphics and performance knobs
- `Constants` control render scale, MSAA, dynamic lighting, terrain GPU lighting, buffer pooling, vsync, quality flags.
- Lighting defaults (sun direction/strength) and debug flags vary by build (Debug enables debug panel, disables background music).
- Android defaults lower quality for performance.

## Logging and diagnostics
- Configure logging in `appsettings.json`; set namespaces (e.g., networking) to `Trace` when diagnosing.
- Use task scheduler priorities to avoid frame stalls when adding expensive work.

## Testing and verification
- No automated suite yet; minimum: build and run the head you touched, then smoke login/scene switch/render/movement. Note commands in PRs.
- Prefer deterministic fixtures over live servers when adding tests.

## Contribution guidelines
- Commits: concise, present-tense summaries (see history like “bonfire improvements - sparks and smoke”); group related changes.
- PRs: include intent, affected platforms, commands run, screenshots/clips for visual changes, and note DataPath/appsettings expectations; mention validated backend (DX/GL).
- Respect existing patterns (handlers, scenes, scheduling); keep changes scoped.

## Common pitfalls
- Missing `-p:MonoGameFramework=...` on Windows → wrong MonoGame package and shaders.
- Wrong `DataPath` → missing assets/black screens.
- UI updates from network threads → crashes; always marshal.
- Mixing DX/GL references across projects → restore/build conflicts.
- Queuing heavy work without scheduler priorities → frame hitches.
- Committing proprietary MU data or secrets is forbidden.

## Quick checklist before PR
- Set local `DataPath` and `appsettings.json`.
- Build/run the specific head with correct `MonoGameFramework`.
- Marshal UI/state changes via `MuGame.ScheduleOnMainThread`.
- Add/adjust packet handlers with `[PacketHandler]` returning `Task`.
- Update/look up IDs in databases/configs instead of hardcoding.
- Capture commands run and visuals (if UI) for the PR description.
