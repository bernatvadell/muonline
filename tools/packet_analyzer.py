#!/usr/bin/env python3
"""
MU Online Packet Analyzer — Protocol Debug Tool

Reads binary packet dump files produced by PacketDebugService and provides:
- Packet frequency analysis
- Handler coverage comparison (vs registered handlers in PacketRouter)
- Size distribution and anomaly detection
- Timeline visualization of packet flow
- S5.2 vs S6 struct size comparison for protocol migration debugging

Usage:
  python3 packet_analyzer.py <dump_file>            # Analyze dump
  python3 packet_analyzer.py --scan-handlers <dir>  # Scan C# handlers for coverage
  python3 packet_analyzer.py --compare <dump> <dir>  # Full analysis with coverage

Dump format (from PacketDebugService):
  Header: "MUPKT\x01" (6 bytes)
  Per-packet: timestamp_us(8) + direction(1) + size(4) + raw_data(size)
"""

import struct
import sys
import os
import re
from collections import defaultdict, Counter
from datetime import datetime, timezone


# ═══════════════════════════════════════════════════════════════════════════
# Known S6 packet opcodes and their expected struct names/sizes
# ═══════════════════════════════════════════════════════════════════════════

PBMSG = 3  # C1 header
PWMSG = 4  # C2 header

KNOWN_PACKETS = {
    # (MainCode, SubCode): (name, expected_min_size, notes)
    # Login / Server
    (0xF1, 0x00): ("JoinServer", 12, "Server version + player ID"),
    (0xF1, 0x01): ("LoginResult", 5, "Success/fail code"),
    (0xF1, 0x02): ("LogOut", 4, "Logout response"),
    # Character
    (0xF3, 0x00): ("CharacterList", 8, "Character list header"),
    (0xF3, 0x01): ("CreateCharacter", 19, "Create result + name"),
    (0xF3, 0x03): ("JoinMapServer_S52", 58, "S5.2 map join (stats+position)"),
    (0xF3, 0x0E): ("JoinMapServer_S6", 58, "S6 map join"),
    (0xF3, 0x05): ("LevelUp", 23, "Level + stats"),
    (0xF3, 0x06): ("AddPoint", 8, "Stat point result"),
    (0xF3, 0x10): ("Inventory", None, "Variable: per-item"),
    (0xF3, 0x11): ("MagicList", 6, "Skill list header"),
    (0xF3, 0x13): ("Equipment", 24, "Equipment update"),
    (0xF3, 0x30): ("GameOptions", 34, "Option flags"),
    (0xF3, 0x40): ("ServerCommand", 7, "Server-side command"),
    (0xF3, 0x50): ("MasterLevelExp", None, "Master experience"),
    (0xF3, 0x51): ("MasterLevelUp", None, "Master level up"),
    (0xF3, 0x52): ("MasterSkill", None, "Master skill learn"),
    # Connect Server
    (0xF4, 0x06): ("ServerList", None, "Server list"),
    (0xF4, 0x03): ("ServerConnect", 21, "IP + port"),
    (0xF4, 0x05): ("ServerBusy", 5, "Server busy flag"),
    # Chat
    (0x00, 0xFF): ("Chat", 103, "Chat message"),
    (0x01, 0xFF): ("ChatKey", 95, "Chat by key"),
    (0x02, 0xFF): ("ChatWhisper", None, "Whisper"),
    (0x0D, 0xFF): ("Notice", None, "Server notice"),
    # Combat
    (0x11, 0xFF): ("AttackDamage", 10, "Attack result"),
    (0x16, 0xFF): ("DieExp", 10, "Death + exp"),
    (0x17, 0xFF): ("Die", None, "Death"),
    (0x18, 0xFF): ("Action", 8, "Character action"),
    (0x19, 0xFF): ("Magic", 7, "Skill cast"),
    (0x1B, 0xFF): ("MagicFinish", None, "Skill finish"),
    (0x1E, 0xFF): ("MagicContinue", 8, "Skill continue"),
    (0x69, 0xFF): ("MonsterSkill", 7, "Monster skill"),
    (0x9C, 0xFF): ("DieExpLarge", 12, "Death large exp"),
    # Movement
    (0xD4, 0xFF): ("MoveCharacter", 8, "Movement"),
    (0x15, 0xFF): ("MovePosition", 7, "Position update"),
    # Viewport
    (0x12, 0xFF): ("CreatePlayerViewport", None, "Player spawn"),
    (0x13, 0xFF): ("CreateMonsterViewport", None, "Monster spawn"),
    (0x14, 0xFF): ("DeleteViewport", None, "Entity despawn"),
    (0x1F, 0xFF): ("CreateSummonViewport", None, "Summon spawn"),
    (0x20, 0xFF): ("CreateItemViewport", None, "Item drop"),
    (0x21, 0xFF): ("DeleteItemViewport", None, "Item despawn"),
    (0x45, 0xFF): ("CreateTransformViewport", None, "Transform spawn"),
    # Items
    (0x22, 0xFF): ("GetItem", 16, "Item pickup"),
    (0x23, 0xFF): ("DropItem", None, "Item drop result"),
    (0x24, 0xFF): ("EquipItem", None, "Equip result"),
    (0x25, 0xFF): ("ChangePlayer", None, "Equipment change"),
    # Trade
    (0x36, 0xFF): ("TradeRequest", None, "Trade request"),
    (0x37, 0xFF): ("TradeResponse", None, "Trade response"),
    (0x39, 0xFF): ("TradeMoney", None, "Trade money"),
    (0x3A, 0xFF): ("TradeOk", None, "Trade confirm"),
    (0x3D, 0xFF): ("TradeResult", None, "Trade result"),
    # Party
    (0x40, 0xFF): ("PartyRequest", None, "Party invite"),
    (0x41, 0xFF): ("PartyResult", None, "Party result"),
    (0x42, 0xFF): ("PartyList", None, "Party members"),
    (0x44, 0xFF): ("PartyHP", None, "Party HP update"),
    # Guild
    (0x50, 0xFF): ("GuildJoinRequest", None, "Guild join"),
    (0x5D, 0xFF): ("GuildMemberLeft", None, "Guild leave"),
    (0x65, 0xFF): ("GuildAssign", None, "Guild assign"),
    # Buff
    (0x07, 0xFF): ("SkillStatus", 6, "Buff status"),
    # Events
    (0x0B, 0xFF): ("Event", None, "Event data"),
    (0x0F, 0xFF): ("Weather", None, "Weather change"),
    # Duel
    (0xAA, 0x01): ("DuelRequest", None, "Duel request"),
    (0xAA, 0x02): ("DuelAnswer", None, "Duel answer"),
    (0xAA, 0x03): ("DuelExit", None, "Duel exit"),
    (0xAA, 0x04): ("DuelScore", None, "Duel score"),
    (0xAA, 0x05): ("DuelHP", None, "Duel HP"),
    (0xAA, 0x07): ("DuelSpectator", None, "Duel spectator"),
    # Gens
    (0xF8, 0x02): ("GensRegister", 6, "Gens register result"),
    (0xF8, 0x03): ("GensSecede", 5, "Gens leave"),
    (0xF8, 0x07): ("GensInfo", 21, "Gens info"),
    # Quest
    (0xA0, 0xFF): ("QuestHistory", 54, "Quest states"),
    (0xA1, 0xFF): ("QuestState", 5, "Quest update"),
    (0xA2, 0xFF): ("QuestResult", 6, "Quest result"),
    # Messenger
    (0xC0, 0xFF): ("Messenger", None, "Friend list"),
}


# ═══════════════════════════════════════════════════════════════════════════
# Dump Reader
# ═══════════════════════════════════════════════════════════════════════════

MAGIC = b"MUPKT\x01"


def read_dump(path):
    """Read binary packet dump and yield (timestamp, direction, raw_bytes)."""
    with open(path, "rb") as f:
        header = f.read(6)
        if header != MAGIC:
            print(f"ERROR: Invalid dump file (expected MUPKT magic, got {header!r})")
            sys.exit(1)

        while True:
            entry_header = f.read(13)
            if len(entry_header) < 13:
                break
            ts_us, direction, size = struct.unpack("<qBI", entry_header)
            raw = f.read(size)
            if len(raw) < size:
                break
            ts = datetime.fromtimestamp(ts_us / 1_000_000, tz=timezone.utc)
            yield ts, direction, raw


def parse_packet_header(raw):
    """Extract (main_code, sub_code, payload_size) from C1/C2/C3/C4 packet."""
    if len(raw) < 3:
        return None, None, 0

    ptype = raw[0]

    if ptype in (0xC1, 0xC3):  # 1-byte length
        size = raw[1]
        main_code = raw[2]
        sub_code = raw[3] if len(raw) > 3 else 0xFF
        return main_code, sub_code, size
    elif ptype in (0xC2, 0xC4):  # 2-byte length
        if len(raw) < 4:
            return None, None, 0
        size = (raw[1] << 8) | raw[2]
        main_code = raw[3]
        sub_code = raw[4] if len(raw) > 4 else 0xFF
        return main_code, sub_code, size
    else:
        return None, None, 0


# ═══════════════════════════════════════════════════════════════════════════
# Handler Scanner (reads C# source)
# ═══════════════════════════════════════════════════════════════════════════

HANDLER_RE = re.compile(
    r'\[PacketHandler\(\s*0x([0-9A-Fa-f]+)\s*,\s*(?:0x([0-9A-Fa-f]+)|PacketRouter\.NoSubCode)\s*\)\]'
)


def scan_handlers(source_dir):
    """Scan C# files for [PacketHandler] attributes, return set of (code, subcode)."""
    handlers = set()
    handler_dir = os.path.join(source_dir, "Client.Main", "Networking", "PacketHandling", "Handlers")

    if not os.path.isdir(handler_dir):
        # Try from repo root
        for root, dirs, files in os.walk(source_dir):
            for f in files:
                if f.endswith(".cs"):
                    path = os.path.join(root, f)
                    with open(path, "r", errors="ignore") as fh:
                        for match in HANDLER_RE.finditer(fh.read()):
                            code = int(match.group(1), 16)
                            sub = int(match.group(2), 16) if match.group(2) else 0xFF
                            handlers.add((code, sub))
        return handlers

    for fname in os.listdir(handler_dir):
        if not fname.endswith(".cs"):
            continue
        with open(os.path.join(handler_dir, fname), "r", errors="ignore") as fh:
            for match in HANDLER_RE.finditer(fh.read()):
                code = int(match.group(1), 16)
                sub = int(match.group(2), 16) if match.group(2) else 0xFF
                handlers.add((code, sub))

    return handlers


# ═══════════════════════════════════════════════════════════════════════════
# Analysis
# ═══════════════════════════════════════════════════════════════════════════

def analyze_dump(path, source_dir=None):
    """Full analysis of a packet dump file."""
    packets = list(read_dump(path))

    if not packets:
        print("No packets in dump file.")
        return

    # Basic stats
    incoming = [(ts, raw) for ts, d, raw in packets if d == 0]
    outgoing = [(ts, raw) for ts, d, raw in packets if d == 1]

    print("=" * 70)
    print("  MU Online Packet Analysis Report")
    print("=" * 70)
    print(f"  Dump file: {path}")
    print(f"  Total packets: {len(packets)}")
    print(f"  Incoming (S→C): {len(incoming)}")
    print(f"  Outgoing (C→S): {len(outgoing)}")
    print(f"  Total bytes: {sum(len(r) for _, _, r in packets):,}")

    if len(packets) > 1:
        duration = (packets[-1][0] - packets[0][0]).total_seconds()
        if duration > 0:
            print(f"  Duration: {duration:.1f}s ({len(packets)/duration:.1f} pkt/s)")

    # Frequency by opcode
    freq = Counter()
    sizes = defaultdict(list)
    for ts, direction, raw in packets:
        code, sub, size = parse_packet_header(raw)
        if code is not None:
            key = (code, sub if sub != 0xFF else 0xFF)
            freq[key] += 1
            sizes[key].append(len(raw))

    print(f"\n{'=' * 70}")
    print("  Packet Frequency (top 30)")
    print(f"{'=' * 70}")
    print(f"  {'Code':>10s}  {'Count':>7s}  {'AvgSize':>7s}  {'Name'}")
    print(f"  {'-' * 10}  {'-' * 7}  {'-' * 7}  {'-' * 30}")

    for key, count in freq.most_common(30):
        code, sub = key
        code_str = f"0x{code:02X}-0x{sub:02X}"
        avg_size = sum(sizes[key]) / len(sizes[key])
        name = KNOWN_PACKETS.get(key, KNOWN_PACKETS.get((code, 0xFF), (None,)))[0] or "?"
        print(f"  {code_str:>10s}  {count:>7d}  {avg_size:>7.0f}  {name}")

    # Size anomalies
    print(f"\n{'=' * 70}")
    print("  Size Anomaly Detection")
    print(f"{'=' * 70}")
    anomalies = 0
    for key in sorted(freq.keys()):
        code, sub = key
        known = KNOWN_PACKETS.get(key, KNOWN_PACKETS.get((code, 0xFF)))
        if known and known[1] is not None:
            expected = known[1]
            actual_sizes = sizes[key]
            mismatches = [s for s in actual_sizes if s < expected]
            if mismatches:
                name = known[0]
                print(f"  WARNING: {name} (0x{code:02X}-0x{sub:02X}): "
                      f"expected >= {expected}B, got {min(actual_sizes)}B "
                      f"({len(mismatches)}/{len(actual_sizes)} packets too small)")
                anomalies += 1
    if anomalies == 0:
        print("  No size anomalies detected.")

    # Handler coverage
    if source_dir:
        handlers = scan_handlers(source_dir)
        observed = set(freq.keys())

        # Normalize: check both exact match and NoSubCode fallback
        handled = set()
        unhandled = set()
        for key in observed:
            code, sub = key
            if key in handlers or (code, 0xFF) in handlers:
                handled.add(key)
            else:
                unhandled.add(key)

        print(f"\n{'=' * 70}")
        print("  Handler Coverage Analysis")
        print(f"{'=' * 70}")
        print(f"  Registered handlers: {len(handlers)}")
        print(f"  Opcodes observed: {len(observed)}")
        print(f"  Handled: {len(handled)} ({100*len(handled)/max(len(observed),1):.0f}%)")
        print(f"  UNHANDLED: {len(unhandled)}")

        if unhandled:
            print(f"\n  Unhandled opcodes (packets received but no handler):")
            for key in sorted(unhandled, key=lambda k: freq[k], reverse=True):
                code, sub = key
                count = freq[key]
                name = KNOWN_PACKETS.get(key, KNOWN_PACKETS.get((code, 0xFF), (None,)))[0] or "unknown"
                print(f"    0x{code:02X}-0x{sub:02X}: {count:>5d} packets — {name}")

        # Registered but never seen
        unseen = handlers - {(c, s) for c, s in handlers if
                             (c, s) in observed or any(c == oc for oc, _ in observed)}
        if unseen:
            print(f"\n  Handlers registered but never triggered ({len(unseen)}):")
            for code, sub in sorted(unseen):
                print(f"    0x{code:02X}-0x{sub:02X}")

    print(f"\n{'=' * 70}")
    print("  Analysis complete.")


def scan_only(source_dir):
    """Just scan and report registered handlers."""
    handlers = scan_handlers(source_dir)
    print(f"Found {len(handlers)} registered packet handlers:\n")

    by_code = defaultdict(list)
    for code, sub in sorted(handlers):
        by_code[code].append(sub)

    for code in sorted(by_code.keys()):
        subs = by_code[code]
        sub_str = ", ".join(f"0x{s:02X}" for s in subs)
        known_names = []
        for s in subs:
            k = KNOWN_PACKETS.get((code, s), KNOWN_PACKETS.get((code, 0xFF)))
            if k:
                known_names.append(k[0])
        names = " | ".join(set(known_names)) if known_names else "?"
        print(f"  0x{code:02X} [{sub_str}] — {names}")


# ═══════════════════════════════════════════════════════════════════════════
# CLI
# ═══════════════════════════════════════════════════════════════════════════

def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)

    if sys.argv[1] == "--scan-handlers":
        if len(sys.argv) < 3:
            print("Usage: packet_analyzer.py --scan-handlers <project_dir>")
            sys.exit(1)
        scan_only(sys.argv[2])

    elif sys.argv[1] == "--compare":
        if len(sys.argv) < 4:
            print("Usage: packet_analyzer.py --compare <dump_file> <project_dir>")
            sys.exit(1)
        analyze_dump(sys.argv[2], sys.argv[3])

    else:
        analyze_dump(sys.argv[1])


if __name__ == "__main__":
    main()
