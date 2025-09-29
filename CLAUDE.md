# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a MuOnline clone built with .NET 9.0 and MonoGame framework. It supports multiple platforms: Windows, Android, iOS, Linux, and macOS.

**Protocol Compatibility:** Uses **Season 6 (S6) network protocol** but requires **Season 20 (1.20.61) client data files** for assets.

**Recommended Server:** Designed to work with [OpenMU](https://github.com/MUnique/OpenMU) server.

## Project Structure

- **Client.Data**: Data readers for game files (BMD, ATT, MAP, OZB, etc.)
- **Client.Main**: Core game logic, rendering, UI, networking, and game objects
- **Client.Editor**: Editor tool for game assets
- **MuWin**: Windows platform executable
- **MuAndroid**: Android platform executable
- **MuIos**: iOS platform executable
- **MuLinux**: Linux platform executable
- **MuMac**: macOS platform executable

## Build Commands

### Development Builds
```bash
# Build entire solution
dotnet build

# Run on specific platforms
dotnet run --project ./MuWin/MuWin.csproj -f net9.0-windows -c Debug     # Windows
dotnet run --project ./MuLinux/MuLinux.csproj -f net9.0 -c Debug         # Linux
dotnet run --project ./MuMac/MuMac.csproj -f net9.0 -c Debug             # macOS
dotnet run --project ./MuIos/MuIos.csproj -f net9.0-ios -c Debug         # iOS (macOS only)
```

### Production Builds
```bash
# Windows
dotnet publish ./MuWin/MuWin.csproj -f net9.0-windows -c Release

# Android
dotnet publish ./MuAndroid/MuAndroid.csproj -f net9.0-android -c Release

# Linux
dotnet publish ./MuLinux/MuLinux.csproj -f net9.0 -c Release -r linux-x64

# macOS
dotnet publish ./MuMac/MuMac.csproj -f net9.0 -c Release

# iOS (macOS only)
dotnet publish ./MuIos/MuIos.csproj -f net9.0-ios -c Release
```

### Tool Management
```bash
# Restore .NET tools (required after clone)
dotnet tool restore
```

## Configuration

### Data Path
Configure in `Client.Main/Constants.cs:25`:
```csharp
public static string DataPath = @"C:\Games\MU_Red_1_20_61_Full\Data";
```

### Server Settings
Configure in `Client.Main/appsettings.json`:
```json
{
  "MuOnlineSettings": {
    "ConnectServerHost": "localhost",
    "ConnectServerPort": 44405,
    "ProtocolVersion": "Season6",
    "ClientVersion": "1.04d",
    "ClientSerial": "0123456789ABCDEF"
  }
}
```

## Architecture

### Networking Architecture
**Dual-Mode Packet Routing:** The `PacketRouter` switches between Connect Server and Game Server modes using `SetRoutingMode(bool)`.

**Attribute-Based Handler Registration:** Packet handlers use `[PacketHandler(mainCode, subCode)]` attributes for automatic registration. Handlers are organized by responsibility:
- `ConnectServerHandler`: Server list, server selection
- `MiscGamePacketHandler`: Login, character list, logout, game server handshake
- `CharacterDataHandler`: Stats, level-ups, health/mana updates
- `InventoryHandler`: Item management
- `ScopeHandler`: Object spawn/despawn (players, monsters, NPCs, items)
- `ChatMessageHandler`: Chat messages
- `PartyHandler`: Party management
- `ShopHandler`: NPC shops and trades

**Service Layer:** Networking services encapsulate outgoing packet building:
- `LoginService`: Authentication packets
- `CharacterService`: Character actions (walk, attack, item use, etc.)
- `ConnectServerService`: Server selection packets

**State Management:** `NetworkManager` maintains `ClientConnectionState` (Initial → ConnectingToConnectServer → ConnectedToConnectServer → ConnectingToGameServer → ConnectedToGameServer → Authenticating → InGame).

### Scene Management
**BaseScene Pattern:** All scenes inherit from `BaseScene` which extends `GameControl`. Scenes manage UI controls, world rendering, and input handling.

Key scenes: `LoginScene`, `LoadScene`, `GameScene`

**World System:** Each game map is a `WorldControl` subclass (e.g., `LorenciaWorld`, `DeviasWorld`). Scenes can switch worlds using `ChangeWorldAsync<T>()`.

### Game Object System
**Hierarchical Object Model:**
- `WorldObject` (base): Position, direction, animation, 3D model
  - `PlayerObject`: Player characters
  - `MonsterObject`: NPCs and monsters
  - `NPCObject`: Quest/shop NPCs
  - `DroppedItemObject`: Ground items
  - `EffectObject`: Visual effects

Objects are managed by `ScopeManager` which tracks object IDs and handles spawn/despawn events.

### Threading Model
**Main Thread Scheduling:** MonoGame requires UI and rendering operations on the main thread. Use `MuGame.ScheduleOnMainThread(Action)` to marshal operations from network threads.

**Async Networking:** All network operations are async using `Task`-based patterns. Packet handlers return `Task` and use `async/await`.

### File Format Support
- **BMD**: 3D models and animations (skeletal)
- **ATT**: Terrain walkability and attributes
- **MAP**: Terrain heightmap data
- **OZB/OZG**: Compressed texture formats
- **CWS**: Camera walk/pan scripts
- **OBJS**: Object placement data for maps

## Important Patterns

### Always Prefer Editing Existing Files
NEVER create new files unless explicitly required. Always edit existing files in the codebase.

### Packet Handler Pattern
When adding new packet handlers:
1. Add handler method to appropriate handler class in `Client.Main/Networking/PacketHandling/Handlers/`
2. Use `[PacketHandler(mainCode, subCode)]` attribute
3. Return `Task` (use `Task.CompletedTask` for sync handlers)
4. Packet structure classes are in `MUnique.OpenMU.Network.Packets.ServerToClient`

Example:
```csharp
[PacketHandler(0xF3, 0x01)]
public Task HandleMyPacketAsync(Memory<byte> packet)
{
    var myPacket = new MyPacket(packet);
    // Process packet
    return Task.CompletedTask;
}
```

### Main Thread Operations
Always wrap UI/scene operations from network handlers:
```csharp
MuGame.ScheduleOnMainThread(() => {
    // UI updates here
});
```

### State Management
When modifying `NetworkManager` state:
1. Call `UpdateState(ClientConnectionState.NewState)`
2. State changes trigger `ConnectionStateChanged` event
3. Scenes react to state changes to update UI

## Core Systems Deep Dive

### MuGame Singleton
**Central Game Instance:** `MuGame.Instance` provides global access to:
- `Network`: NetworkManager instance
- `TaskScheduler`: Priority-based task queue for main thread operations
- `AppConfiguration`: IConfiguration from appsettings.json
- `AppSettings`: Parsed MuOnlineSettings
- `ActiveScene`: Currently active BaseScene

**Main Thread Scheduling:** Network threads cannot directly modify UI. Use:
```csharp
MuGame.ScheduleOnMainThread(() => {
    // UI updates, scene changes, control modifications
});
```

**Static Properties:**
- `Random`: Shared Random instance for game logic
- `FrameIndex`: Current frame number (incremented each Update)

### TaskScheduler System
**Priority Queue:** Prevents UI freezing during heavy network activity.

**Priority Levels:**
- `Critical`: Immediate processing (damage, death)
- `High`: Player movements, NPC spawns in view
- `Normal`: UI updates, equipment changes
- `Low`: Background tasks (model loading, texture caching)

**Configuration:**
- Max 10 tasks per frame (configurable)
- Max 16ms processing time per frame (~60 FPS)
- Backpressure control: Drops low-priority tasks when queue exceeds 100

**Usage:**
```csharp
MuGame.TaskScheduler.QueueTask(() => {
    // Task code
}, TaskScheduler.Priority.High);
```

### UI Scaling System (UiScaler)
**Virtual Resolution:** All UI uses 1280x720 virtual coordinates (configurable in appsettings.json).

**Automatic Scaling:** `UiScaler.Configure()` called at startup:
- Calculates scale factor: `Scale = min(ActualWidth/VirtualWidth, ActualHeight/VirtualHeight)`
- Maintains aspect ratio with letterboxing/pillarboxing
- Provides `SpriteTransform` matrix for SpriteBatch rendering

**Coordinate Conversion:**
```csharp
Point virtualPos = UiScaler.ToVirtual(actualMousePos);
Point actualPos = UiScaler.ToActual(virtualPos);
```

**Render Scale:** `Constants.RENDER_SCALE` (default 2.0) enables supersampling for higher quality.

### GameControl Hierarchy
**Base Class for All UI:** Every UI element inherits from `GameControl`.

**Key Properties:**
- `Controls`: ChildrenCollection for hierarchical UI
- `Status`: NonInitialized → Initializing → Ready
- `DisplayPosition`/`DisplayRectangle`: Calculated from parent hierarchy
- `Interactive`: If true, receives mouse/touch events
- `Align`: Auto-positioning (HorizontalCenter, VerticalCenter, etc.)
- `Alpha`: Transparency (0.0-1.0)

**Lifecycle:**
1. Construction
2. `Initialize()` - async initialization, loads resources
3. `Update(GameTime)` - called every frame
4. `Draw(GameTime)` - render phase
5. `Dispose()` - cleanup

**Event System:**
- `Click`: Fired when control is clicked
- `Focus`/`Blur`: Focus management
- `SizeChanged`: When ViewSize changes

### CharacterState
**Single Source of Truth:** Holds all character data received from server.

**Categories:**
- **Basic Info**: Name, ID, Class, Level, Position (X, Y), MapId
- **Stats**: HP, MP, SD, AG, Strength, Agility, Vitality, Energy, Leadership
- **Inventory**: Thread-safe `ConcurrentDictionary<byte, byte[]>` of item data
- **Skills**: Learned skills with levels
- **Shop/Vault**: Separate dictionaries for NPC shop and vault items

**Events:**
- `HealthChanged`: (currentHP, maxHP) - fired on HP update
- `ManaChanged`: (currentMP, maxMP) - fired on MP update
- `InventoryChanged`: Fired when inventory updates
- `MoneyChanged`: Fired when Zen changes

**Thread Safety:** All collections use `ConcurrentDictionary` for safe access from network threads.

### ScopeManager
**Object Visibility Management:** Tracks all objects in player's view range.

**Object Types:**
- `PlayerScopeObject`: Other players
- `NpcScopeObject`: Monsters and NPCs (TypeNumber identifies model)
- `ItemScopeObject`: Dropped items on ground
- `MoneyScopeObject`: Zen drops

**Operations:**
- `AddOrUpdatePlayerInScope(maskedId, ...)`: Adds/updates player
- `RemoveFromScope(maskedId)`: Removes object when out of range
- `TryGetScopeObject(maskedId, out object)`: Retrieves object by ID
- `GetAllPlayers()`: Returns all players in scope

**ID Masking:** Server sends masked IDs (top bit stripped). ScopeManager stores by masked ID, exposes raw ID where needed.

## Configuration Details

### Constants.cs Patterns
**Debug vs Release:** Extensive conditional compilation (`#if DEBUG`).

**Key Debug Settings:**
- `ENTRY_SCENE`: typeof(LoadScene) - starting scene
- `SHOW_DEBUG_PANEL`: true - shows FPS, position, etc.
- `DRAW_BOUNDING_BOXES`: false - visualize collision boxes
- `UNLIMITED_FPS`: true - disables VSync
- `DataPath`: Hardcoded absolute path

**Key Release Settings:**
- `SHOW_DEBUG_PANEL`: false
- `DataPath`: Relative to executable

**Rendering Flags:**
- `ENABLE_DYNAMIC_LIGHTING_SHADER`: GPU-based lighting (vs CPU fallback)
- `OPTIMIZE_FOR_INTEGRATED_GPU`: Reduces max lights for weak GPUs
- `ENABLE_ITEM_MATERIAL_SHADER`: Special effects for high-tier items
- `ENABLE_MONSTER_MATERIAL_SHADER`: Custom monster visual effects
- `RENDER_SCALE`: 2.0 = 2x supersampling
- `MSAA_ENABLED`: false by default (performance)

**Camera Constants:**
- `MIN_CAMERA_DISTANCE`: 800f
- `MAX_CAMERA_DISTANCE`: 1800f
- `DEFAULT_CAMERA_DISTANCE`: 1700f
- `CAMERA_YAW`: -0.7329271f (default view angle)
- `CAMERA_PITCH`: 2.3711946f
- `LOW_QUALITY_DISTANCE`: 3500f (objects beyond render with lower quality)

### appsettings.json Structure
**Logging Configuration:** Per-class log levels with `Trace` for network debugging.

**MuOnlineSettings:**
```json
{
  "ConnectServerHost": "localhost",
  "ConnectServerPort": 44405,
  "ProtocolVersion": "Season6",
  "ClientVersion": "1.04d",
  "ClientSerial": "0123456789ABCDEF",
  "Graphics": {
    "Width": 1280,
    "Height": 720,
    "IsFullScreen": false,
    "UiVirtualWidth": 1280,
    "UiVirtualHeight": 720
  },
  "DirectionMap": {
    "0": 7, "1": 6, "2": 5, "3": 4,
    "4": 3, "5": 2, "6": 1, "7": 0
  },
  "PacketLogging": {
    "ShowWeather": true,
    "ShowDamage": true
  }
}
```

**DirectionMap:** Maps client directions to server directions (may differ by protocol version).

## Advanced Topics

### Performance Considerations
**Main Thread Bottlenecks:**
- Packet processing happens on network threads
- UI updates must be marshalled to main thread via `ScheduleOnMainThread()`
- TaskScheduler prevents frame drops by limiting work per frame

**Render Pipeline:**
1. **3D Pass**: Terrain, objects, effects (depth-enabled)
2. **Transparent Pass**: Blend meshes, particles (depth-read only)
3. **UI Pass**: 2D sprites with `UiScaler.SpriteTransform`

**Asset Loading:**
- Models: Lazy-loaded via `ModelManager`
- Textures: Cached in `TextureLoader`
- All loading async to prevent frame drops

**Memory Management:**
- `ConcurrentDictionary` for thread-safe collections
- Object pooling for frequently created objects (particles, effects)
- Dispose pattern strictly followed

## Common Pitfalls

### Threading Issues
❌ **Wrong:**
```csharp
// In packet handler (network thread)
scene.SomeControl.Text = "Updated";
```

✅ **Correct:**
```csharp
MuGame.ScheduleOnMainThread(() => {
    scene.SomeControl.Text = "Updated";
});
```

### Packet Handler Registration
❌ **Wrong:** Forgetting `[PacketHandler]` attribute
```csharp
public Task HandleMyPacket(Memory<byte> packet) // Won't be called!
```

✅ **Correct:**
```csharp
[PacketHandler(0xF3, 0x01)]
public Task HandleMyPacket(Memory<byte> packet)
```

### State Updates
❌ **Wrong:** Direct state modification
```csharp
_currentState = ClientConnectionState.InGame;
```

✅ **Correct:** Use UpdateState
```csharp
UpdateState(ClientConnectionState.InGame); // Fires events
```

### Coordinate Systems
❌ **Wrong:** Using actual screen coordinates for UI
```csharp
control.X = mouseState.X; // Wrong coordinate space!
```

✅ **Correct:** Convert to virtual coordinates
```csharp
Point virtualPos = UiScaler.ToVirtual(new Point(mouseState.X, mouseState.Y));
control.X = virtualPos.X;
```

### Resource Cleanup
❌ **Wrong:** Not disposing resources
```csharp
var texture = new Texture2D(...);
// Never disposed - memory leak!
```

✅ **Correct:** Implement IDisposable
```csharp
public override void Dispose()
{
    texture?.Dispose();
    base.Dispose();
}
```