# Developer Tools

Debug and testing utilities for MU Online client development.

## Packet Analyzer (`packet_analyzer.py`)

Standalone protocol analysis tool. No compilation needed — just Python 3.

### Scan registered handlers

Finds all `[PacketHandler]` attributes in the C# codebase:

```bash
python3 tools/packet_analyzer.py --scan-handlers .
```

Output:
```
Found 97 registered packet handlers:
  0x00 [0xFF] — Chat
  0xF1 [0x00, 0x01, 0x02] — JoinServer | LoginResult | LogOut
  0xF3 [0x00, 0x01, ...] — CharacterList | CreateCharacter | ...
  0xAA [0x01-0x0D] — Duel system (13 subcodes)
```

### Analyze a packet dump

Reads binary dumps from `PacketDebugService` and reports frequency, size anomalies, and handler coverage:

```bash
python3 tools/packet_analyzer.py /tmp/mu_packets.bin
python3 tools/packet_analyzer.py --compare /tmp/mu_packets.bin .
```

## Integration Tests (`integration_test.py`)

End-to-end test that runs the client, captures output, and validates milestones.

### Quick test

```bash
python3 tools/integration_test.py --timeout 30 --project MuMac
```

### CI mode

Returns exit code 0 (pass) or 1 (fail) or 2 (crash):

```bash
python3 tools/integration_test.py --ci --timeout 45 --project MuLinux --build
```

### Options

| Flag | Default | Description |
|------|---------|-------------|
| `--timeout` | 30 | Seconds to run before terminating |
| `--project` | MuMac | Platform head (MuMac, MuLinux, MuWinDX, MuWinGL) |
| `--credentials` | — | Auto-login as `user:pass` |
| `--build` | off | Build before running |
| `--ci` | off | Strict exit codes, minimal output |
| `--log-file` | — | Save full output to file |

### Milestones checked

15 milestones across 5 categories:

- **Boot**: Configuration loaded, Network initialized
- **Connect**: Connect Server connected, Hello received, Server list received
- **Game Server**: Connected, Login sent, Login success
- **Character**: Character list, Character selected
- **World**: Map joined, Skills, Inventory, Entities, Packets

## PacketDebugService (C#)

Runtime packet capture service. Add to your game initialization:

```csharp
var debugService = new PacketDebugService(loggerFactory, enabled: true);
debugService.DumpToFile = true;
debugService.DumpPath = "/tmp/mu_packets.bin";

// Register handlers for coverage tracking
debugService.RegisterHandlers(packetRouter.GetRegisteredHandlerKeys());

// In packet receive path:
debugService.RecordIncoming(mainCode, subCode, rawPacket);

// On shutdown:
debugService.PrintSummary();
debugService.Dispose();
```

Configure via `appsettings.json`:
```json
{
  "PacketDebugging": {
    "Enabled": true,
    "DumpToFile": true,
    "DumpPath": "/tmp/mu_packets.bin",
    "RingBufferSize": 500,
    "RateLimitMs": 50
  }
}
```
