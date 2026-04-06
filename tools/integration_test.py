#!/usr/bin/env python3
"""
MU Online Client — Integration Test Framework

Automated end-to-end test that validates the full client flow:
  Connect Server → Login → Server List → Game Server → Character List → Enter World

Runs the client process for a configurable duration, captures stdout/stderr,
and checks for milestone markers in the log output.

Usage:
  python3 integration_test.py                        # Default: 30s timeout
  python3 integration_test.py --timeout 60           # Custom timeout
  python3 integration_test.py --project MuMac        # Specific platform head
  python3 integration_test.py --host 127.0.0.1       # Custom server
  python3 integration_test.py --credentials user:pass # Auto-login
  python3 integration_test.py --ci                   # CI mode: exit code reflects pass/fail
  python3 integration_test.py --build                # Build before running

Designed for CI/CD integration. Exit codes:
  0 = All critical milestones passed
  1 = One or more critical milestones failed
  2 = Client crashed or failed to start

Requires: .NET 10 SDK, appsettings.json configured with server connection info.
"""

import subprocess
import sys
import os
import re
import time
import signal
import argparse
from datetime import datetime
from pathlib import Path


# ═══════════════════════════════════════════════════════════════════════════
# Milestone Definitions
# ═══════════════════════════════════════════════════════════════════════════

class Milestone:
    def __init__(self, name, pattern, critical=True, category="core"):
        self.name = name
        self.pattern = re.compile(pattern, re.IGNORECASE)
        self.critical = critical
        self.category = category
        self.passed = False
        self.timestamp = None
        self.match_line = None


# Core milestones (must pass for a healthy client)
MILESTONES = [
    # ── Boot ──
    Milestone("Configuration loaded", r"Configuration loaded|✅ Configuration", category="boot"),
    Milestone("Network initialized", r"Network Manager initialized|✅ Network", category="boot"),

    # ── Connect Server ──
    Milestone("Connect Server connected",
              r"Socket connected|Connection infrastructure established|Connected to .*:4440",
              category="connect"),
    Milestone("Hello received", r"Hello from Connect Server|Received Hello", category="connect"),
    Milestone("Server list received",
              r"Server List Response|ServerList.*received|Server list received.*servers",
              category="connect"),

    # ── Game Server ──
    Milestone("Game Server connected",
              r"ConnectedToGameServer|connected.*game.*server|Changing state.*ConnectedToGameServer",
              category="gameserver"),
    Milestone("Login sent", r"SendLoginRequest|Logging in as|Login.*sent", category="gameserver"),
    Milestone("Login success",
              r"LoginResult.*Success|LoginResponse.*Okay|Login Result.*Success",
              category="gameserver"),

    # ── Character ──
    Milestone("Character list received",
              r"Character.*received|characters.*received|Received.*character",
              category="character"),
    Milestone("Character selected",
              r"SelectCharacter|Character selected|character.*selected",
              critical=False, category="character"),

    # ── World ──
    Milestone("Map joined",
              r"JoinMapServer|Map joined|joined.*map|MAIN_SCENE|SceneFlag=5|GameScene",
              critical=False, category="world"),

    # ── Gameplay (non-critical) ──
    Milestone("Skills received", r"MagicList|SkillList|skills.*received",
              critical=False, category="gameplay"),
    Milestone("Inventory received", r"Inventory.*received|inventory.*items",
              critical=False, category="gameplay"),
    Milestone("Entities in scope", r"AddToScope|OpenMonster|CreateChar|spawn.*player",
              critical=False, category="gameplay"),
    Milestone("Packets processing", r"Registered.*handlers|PacketHandler|Route.*packet",
              critical=False, category="gameplay"),
]

# ═══════════════════════════════════════════════════════════════════════════
# Error Patterns
# ═══════════════════════════════════════════════════════════════════════════

ERROR_PATTERNS = [
    re.compile(r"Unhandled exception", re.IGNORECASE),
    re.compile(r"FATAL|CRASH|SIGSEGV|SIGBUS|SIGABRT", re.IGNORECASE),
    re.compile(r"StackOverflowException|OutOfMemoryException"),
    re.compile(r"System\..*Exception.*at\s", re.IGNORECASE),
    re.compile(r"Connection refused|Connection reset|ECONNREFUSED"),
]

WARNING_PATTERNS = [
    re.compile(r"could not be loaded", re.IGNORECASE),
    re.compile(r"Duplicate handler", re.IGNORECASE),
    re.compile(r"timeout|timed out", re.IGNORECASE),
]


# ═══════════════════════════════════════════════════════════════════════════
# Test Runner
# ═══════════════════════════════════════════════════════════════════════════

def find_project_root():
    """Walk up from script location to find the repo root (has MuOnline.sln)."""
    current = Path(__file__).resolve().parent
    while current != current.parent:
        if (current / "MuOnline.sln").exists():
            return current
        current = current.parent
    return Path.cwd()


def build_project(project, root):
    """Build the specified platform head."""
    csproj = root / project / f"{project}.csproj"
    if not csproj.exists():
        print(f"  ERROR: {csproj} not found")
        return False

    print(f"  Building {project}...")
    result = subprocess.run(
        ["dotnet", "build", str(csproj), "-c", "Debug", "--no-restore", "-v", "quiet"],
        capture_output=True, text=True, timeout=120,
        cwd=str(root)
    )
    if result.returncode != 0:
        print(f"  BUILD FAILED:\n{result.stderr[-500:]}")
        return False
    print(f"  Build OK")
    return True


def run_client(project, root, timeout_sec, host=None, port=None, credentials=None):
    """Run the client and capture output."""
    csproj = root / project / f"{project}.csproj"

    env = os.environ.copy()
    if credentials:
        env["MU_AUTO_LOGIN"] = credentials

    cmd = ["dotnet", "run", "--project", str(csproj), "-c", "Debug", "--no-build"]

    # Add runtime identifier for platform heads
    rid_map = {
        "MuMac": "osx-arm64",
        "MuLinux": "linux-x64",
        "MuWinDX": "win-x64",
        "MuWinGL": "win-x64",
    }
    rid = rid_map.get(project)
    if rid:
        cmd.extend(["-r", rid])

    print(f"  Running {project} (timeout: {timeout_sec}s)...")

    try:
        proc = subprocess.Popen(
            cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
            env=env, cwd=str(root), text=True
        )

        output_lines = []
        start = time.time()

        while time.time() - start < timeout_sec:
            line = proc.stdout.readline()
            if not line:
                if proc.poll() is not None:
                    break
                time.sleep(0.1)
                continue
            output_lines.append(line.rstrip())

        # Terminate if still running
        if proc.poll() is None:
            proc.send_signal(signal.SIGTERM)
            try:
                proc.wait(timeout=5)
            except subprocess.TimeoutExpired:
                proc.kill()
                proc.wait()

        return proc.returncode, output_lines

    except FileNotFoundError:
        print("  ERROR: dotnet SDK not found in PATH")
        return -1, []
    except Exception as e:
        print(f"  ERROR: {e}")
        return -1, []


# ═══════════════════════════════════════════════════════════════════════════
# Analysis
# ═══════════════════════════════════════════════════════════════════════════

def check_milestones(lines):
    """Check each milestone against output lines."""
    for i, line in enumerate(lines):
        for m in MILESTONES:
            if not m.passed and m.pattern.search(line):
                m.passed = True
                m.match_line = line.strip()
                m.timestamp = i  # line number as proxy
    return MILESTONES


def find_errors(lines):
    """Find error and warning patterns in output."""
    errors = []
    warnings = []
    for line in lines:
        for pat in ERROR_PATTERNS:
            if pat.search(line):
                errors.append(line.strip())
                break
        for pat in WARNING_PATTERNS:
            if pat.search(line):
                warnings.append(line.strip())
                break
    return errors, warnings


def print_report(milestones, errors, warnings, lines, exit_code, ci_mode=False):
    """Print structured test report."""
    print()
    print("=" * 70)
    print("  MU Online Integration Test Report")
    print("=" * 70)
    print(f"  Time: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"  Output lines: {len(lines)}")
    print()

    # Group milestones by category
    categories = {}
    for m in milestones:
        categories.setdefault(m.category, []).append(m)

    total_pass = sum(1 for m in milestones if m.passed)
    total_critical = sum(1 for m in milestones if m.critical)
    critical_pass = sum(1 for m in milestones if m.critical and m.passed)

    for cat, ms in categories.items():
        print(f"  === {cat.upper()} ===")
        for m in ms:
            icon = "PASS" if m.passed else ("FAIL" if m.critical else "SKIP")
            crit = "*" if m.critical else " "
            print(f"    [{icon}]{crit} {m.name}")
            if m.passed and m.match_line and not ci_mode:
                # Truncate long lines
                ml = m.match_line[:80] + "..." if len(m.match_line) > 80 else m.match_line
                print(f"           {ml}")
        print()

    # Errors
    if errors:
        print("  === ERRORS ===")
        for e in errors[:10]:
            print(f"    {e[:100]}")
        if len(errors) > 10:
            print(f"    ... and {len(errors) - 10} more")
        print()

    # Warnings
    if warnings and not ci_mode:
        print("  === WARNINGS ===")
        seen = set()
        for w in warnings:
            short = w[:80]
            if short not in seen:
                print(f"    {short}")
                seen.add(short)
                if len(seen) >= 10:
                    break
        print()

    # Summary
    print("  === SUMMARY ===")
    print(f"    Milestones: {total_pass}/{len(milestones)} passed")
    print(f"    Critical:   {critical_pass}/{total_critical} passed")
    print(f"    Errors:     {len(errors)}")
    print(f"    Warnings:   {len(warnings)}")

    if exit_code is not None and exit_code != 0 and exit_code != -9 and exit_code != -15:
        print(f"    Exit code:  {exit_code} (abnormal)")

    if critical_pass == total_critical and not errors:
        print(f"\n    RESULT: PASS")
    elif errors:
        print(f"\n    RESULT: FAIL (errors detected)")
    else:
        failed = [m.name for m in milestones if m.critical and not m.passed]
        print(f"\n    RESULT: FAIL (critical milestones: {', '.join(failed)})")

    print("=" * 70)

    return critical_pass == total_critical and len(errors) == 0


# ═══════════════════════════════════════════════════════════════════════════
# CLI
# ═══════════════════════════════════════════════════════════════════════════

def main():
    parser = argparse.ArgumentParser(description="MU Online Client Integration Tests")
    parser.add_argument("--timeout", type=int, default=30, help="Test timeout in seconds")
    parser.add_argument("--project", default="MuMac", help="Platform head project (MuMac, MuLinux, etc.)")
    parser.add_argument("--host", help="Server host (overrides appsettings)")
    parser.add_argument("--port", type=int, help="Server port (overrides appsettings)")
    parser.add_argument("--credentials", help="Auto-login as user:pass")
    parser.add_argument("--ci", action="store_true", help="CI mode: minimal output, strict exit codes")
    parser.add_argument("--build", action="store_true", help="Build before running")
    parser.add_argument("--log-file", help="Save full output to file")
    args = parser.parse_args()

    root = find_project_root()
    print(f"  Project root: {root}")
    print(f"  Platform: {args.project}")

    # Build
    if args.build:
        if not build_project(args.project, root):
            sys.exit(2)

    # Run
    exit_code, lines = run_client(
        args.project, root, args.timeout,
        host=args.host, port=args.port,
        credentials=args.credentials
    )

    # Save log
    if args.log_file:
        with open(args.log_file, "w") as f:
            f.write("\n".join(lines))
        print(f"  Log saved: {args.log_file}")

    # Analyze
    milestones = check_milestones(lines)
    errors, warnings = find_errors(lines)
    passed = print_report(milestones, errors, warnings, lines, exit_code, ci_mode=args.ci)

    if args.ci:
        if exit_code not in (0, None, -9, -15):  # -9=SIGKILL, -15=SIGTERM (expected from timeout)
            sys.exit(2)  # Crash
        sys.exit(0 if passed else 1)


if __name__ == "__main__":
    main()
