using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Client.Main.Networking.Debugging
{
    /// <summary>
    /// Captures, analyzes, and logs network packets for protocol debugging.
    /// Provides binary packet dump, handler coverage analysis, and rate-limited
    /// structured logging with a crash-safe ring buffer.
    ///
    /// Enable via appsettings.json:
    ///   "PacketDebugging": {
    ///     "Enabled": true,
    ///     "DumpToFile": true,
    ///     "DumpPath": "/tmp/mu_packets.bin",
    ///     "RingBufferSize": 500,
    ///     "RateLimitMs": 50
    ///   }
    /// </summary>
    public class PacketDebugService : IDisposable
    {
        // ──────────────────────────── Constants ────────────────────────────

        private static readonly byte[] DumpMagic = { 0x4D, 0x55, 0x50, 0x4B, 0x54, 0x01 }; // "MUPKT\x01"

        // ──────────────────────────── Configuration ────────────────────────────

        public bool Enabled { get; set; }
        public bool DumpToFile { get; set; }
        public string DumpPath { get; set; } = Path.Combine(Path.GetTempPath(), "mu_packets.bin");
        public int RingBufferSize { get; set; } = 500;
        public int RateLimitMs { get; set; } = 50;

        // ──────────────────────────── Fields ────────────────────────────

        private readonly ILogger<PacketDebugService> _logger;
        private readonly RingBuffer _ringBuffer;
        private readonly ConcurrentDictionary<(byte Code, byte SubCode), PacketStats> _stats = new();
        private readonly HashSet<(byte Code, byte SubCode)> _registeredHandlers = new();
        private FileStream _dumpStream;
        private readonly object _dumpLock = new();
        private long _totalPacketsReceived;
        private long _totalPacketsSent;
        private long _totalBytesReceived;
        private long _totalBytesSent;
        private readonly Dictionary<byte, long> _lastLogTime = new();

        // ──────────────────────────── Constructor ────────────────────────────

        public PacketDebugService(ILoggerFactory loggerFactory, bool enabled = false)
        {
            _logger = loggerFactory.CreateLogger<PacketDebugService>();
            _ringBuffer = new RingBuffer(RingBufferSize);
            Enabled = enabled;

            if (Enabled)
            {
                _logger.LogInformation("PacketDebugService initialized. DumpToFile={Dump}, Path={Path}",
                    DumpToFile, DumpPath);
            }
        }

        // ──────────────────────────── Public API ────────────────────────────

        /// <summary>
        /// Register known packet handlers so coverage analysis can detect unhandled opcodes.
        /// Call this after PacketRouter initializes its handler dictionary.
        /// </summary>
        public void RegisterHandlers(IEnumerable<(byte MainCode, byte SubCode)> handlerKeys)
        {
            foreach (var key in handlerKeys)
            {
                _registeredHandlers.Add(key);
            }
            _logger.LogDebug("PacketDebugService: {Count} handlers registered for coverage tracking.",
                _registeredHandlers.Count);
        }

        /// <summary>
        /// Record an incoming packet. Called from PacketRouter before dispatch.
        /// </summary>
        public void RecordIncoming(byte mainCode, byte subCode, ReadOnlySpan<byte> rawPacket)
        {
            if (!Enabled) return;

            int packetLength = rawPacket.Length;

            Interlocked.Increment(ref _totalPacketsReceived);
            Interlocked.Add(ref _totalBytesReceived, packetLength);

            var key = (mainCode, subCode);
            _stats.AddOrUpdate(key,
                _ => new PacketStats { Code = mainCode, SubCode = subCode, RecvCount = 1, LastSize = packetLength },
                (_, existing) =>
                {
                    existing.RecvCount++;
                    existing.LastSize = packetLength;
                    existing.LastSeen = DateTimeOffset.UtcNow;
                    return existing;
                });

            _ringBuffer.Push(new RingEntry
            {
                Timestamp = DateTimeOffset.UtcNow,
                Direction = PacketDirection.Incoming,
                Code = mainCode,
                SubCode = subCode,
                Size = rawPacket.Length
            });

            if (DumpToFile)
            {
                WriteDump(PacketDirection.Incoming, rawPacket);
            }

            LogRateLimited(mainCode, subCode, rawPacket.Length, PacketDirection.Incoming);
        }

        /// <summary>
        /// Record an outgoing packet. Called from send path.
        /// </summary>
        public void RecordOutgoing(byte mainCode, byte subCode, ReadOnlySpan<byte> rawPacket)
        {
            if (!Enabled) return;

            int packetLength = rawPacket.Length;

            Interlocked.Increment(ref _totalPacketsSent);
            Interlocked.Add(ref _totalBytesSent, packetLength);

            var key = (mainCode, subCode);
            _stats.AddOrUpdate(key,
                _ => new PacketStats { Code = mainCode, SubCode = subCode, SendCount = 1, LastSize = packetLength },
                (_, existing) =>
                {
                    existing.SendCount++;
                    existing.LastSize = packetLength;
                    existing.LastSeen = DateTimeOffset.UtcNow;
                    return existing;
                });

            _ringBuffer.Push(new RingEntry
            {
                Timestamp = DateTimeOffset.UtcNow,
                Direction = PacketDirection.Outgoing,
                Code = mainCode,
                SubCode = subCode,
                Size = rawPacket.Length
            });

            if (DumpToFile)
            {
                WriteDump(PacketDirection.Outgoing, rawPacket);
            }
        }

        /// <summary>
        /// Returns handler coverage report: which opcodes were seen but have no handler.
        /// </summary>
        public PacketCoverageReport GetCoverageReport()
        {
            var report = new PacketCoverageReport();

            foreach (var kvp in _stats)
            {
                var key = kvp.Key;
                var stats = kvp.Value;
                bool hasHandler = _registeredHandlers.Contains(key)
                    || _registeredHandlers.Contains((key.Code, 0xFF)); // fallback NoSubCode

                if (hasHandler)
                {
                    report.HandledOpcodes.Add(new OpcodeEntry
                    {
                        Code = key.Code,
                        SubCode = key.SubCode,
                        RecvCount = stats.RecvCount,
                        SendCount = stats.SendCount,
                        LastSize = stats.LastSize
                    });
                }
                else
                {
                    report.UnhandledOpcodes.Add(new OpcodeEntry
                    {
                        Code = key.Code,
                        SubCode = key.SubCode,
                        RecvCount = stats.RecvCount,
                        SendCount = stats.SendCount,
                        LastSize = stats.LastSize
                    });
                }
            }

            report.TotalPacketsReceived = _totalPacketsReceived;
            report.TotalPacketsSent = _totalPacketsSent;
            report.TotalBytesReceived = _totalBytesReceived;
            report.TotalBytesSent = _totalBytesSent;
            report.RegisteredHandlerCount = _registeredHandlers.Count;
            report.UniqueOpcodesObserved = _stats.Count;

            return report;
        }

        /// <summary>
        /// Returns the ring buffer contents for crash analysis.
        /// </summary>
        public IReadOnlyList<RingEntry> GetRecentPackets()
        {
            return _ringBuffer.GetAll();
        }

        /// <summary>
        /// Prints a formatted summary to the logger.
        /// </summary>
        public void PrintSummary()
        {
            var report = GetCoverageReport();

            _logger.LogInformation(
                "=== Packet Debug Summary ===\n" +
                "  Received: {Recv} packets ({RecvBytes:N0} bytes)\n" +
                "  Sent: {Sent} packets ({SentBytes:N0} bytes)\n" +
                "  Registered handlers: {Handlers}\n" +
                "  Unique opcodes observed: {Opcodes}\n" +
                "  Handled: {Handled} | Unhandled: {Unhandled}",
                report.TotalPacketsReceived, report.TotalBytesReceived,
                report.TotalPacketsSent, report.TotalBytesSent,
                report.RegisteredHandlerCount,
                report.UniqueOpcodesObserved,
                report.HandledOpcodes.Count,
                report.UnhandledOpcodes.Count);

            if (report.UnhandledOpcodes.Count > 0)
            {
                _logger.LogWarning("=== Unhandled Opcodes ===");
                foreach (var op in report.UnhandledOpcodes.OrderByDescending(o => o.RecvCount))
                {
                    _logger.LogWarning("  0x{Code:X2}-0x{Sub:X2}: recv={Recv}, size={Size}",
                        op.Code, op.SubCode, op.RecvCount, op.LastSize);
                }
            }
        }

        // ──────────────────────────── File Dump ────────────────────────────

        private void WriteDump(PacketDirection direction, ReadOnlySpan<byte> packet)
        {
            lock (_dumpLock)
            {
                try
                {
                    if (_dumpStream == null)
                    {
                        _dumpStream = new FileStream(DumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                        _dumpStream.Write(DumpMagic);
                        _logger.LogInformation("Packet dump file opened: {Path}", DumpPath);
                    }

                    var now = DateTimeOffset.UtcNow;
                    long microseconds = now.ToUnixTimeMilliseconds() * 1000;

                    // Header: timestamp(8) + direction(1) + size(4) = 13 bytes
                    Span<byte> header = stackalloc byte[13];
                    BitConverter.TryWriteBytes(header[..8], microseconds);
                    header[8] = (byte)direction;
                    BitConverter.TryWriteBytes(header[9..13], packet.Length);
                    _dumpStream.Write(header);
                    _dumpStream.Write(packet);
                    _dumpStream.Flush();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to write packet dump");
                    DumpToFile = false;
                }
            }
        }

        // ──────────────────────────── Rate-Limited Logging ────────────────────────────

        private void LogRateLimited(byte mainCode, byte subCode, int size, PacketDirection direction)
        {
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (_lastLogTime.TryGetValue(mainCode, out long lastMs) && (nowMs - lastMs) < RateLimitMs)
                return;

            _lastLogTime[mainCode] = nowMs;
            _logger.LogTrace("[PKT {Dir}] 0x{Code:X2}-0x{Sub:X2} size={Size}",
                direction, mainCode, subCode, size);
        }

        // ──────────────────────────── Dispose ────────────────────────────

        public void Dispose()
        {
            lock (_dumpLock)
            {
                _dumpStream?.Dispose();
                _dumpStream = null;
            }

            if (Enabled)
            {
                PrintSummary();
            }
        }

        // ──────────────────────────── Inner Types ────────────────────────────

        public enum PacketDirection : byte
        {
            Incoming = 0,
            Outgoing = 1
        }

        public class PacketStats
        {
            public byte Code;
            public byte SubCode;
            public long RecvCount;
            public long SendCount;
            public int LastSize;
            public DateTimeOffset LastSeen = DateTimeOffset.UtcNow;
        }

        public class OpcodeEntry
        {
            public byte Code;
            public byte SubCode;
            public long RecvCount;
            public long SendCount;
            public int LastSize;
        }

        public class PacketCoverageReport
        {
            public long TotalPacketsReceived;
            public long TotalPacketsSent;
            public long TotalBytesReceived;
            public long TotalBytesSent;
            public int RegisteredHandlerCount;
            public int UniqueOpcodesObserved;
            public List<OpcodeEntry> HandledOpcodes = new();
            public List<OpcodeEntry> UnhandledOpcodes = new();
        }

        public class RingEntry
        {
            public DateTimeOffset Timestamp;
            public PacketDirection Direction;
            public byte Code;
            public byte SubCode;
            public int Size;

            public override string ToString() =>
                $"[{Timestamp:HH:mm:ss.fff}] {Direction} 0x{Code:X2}-0x{SubCode:X2} ({Size}B)";
        }

        /// <summary>
        /// Lock-free ring buffer for crash-safe packet history.
        /// </summary>
        private class RingBuffer
        {
            private readonly RingEntry[] _buffer;
            private int _head;
            private int _count;
            private readonly object _lock = new();

            public RingBuffer(int capacity)
            {
                _buffer = new RingEntry[capacity];
            }

            public void Push(RingEntry entry)
            {
                lock (_lock)
                {
                    _buffer[_head] = entry;
                    _head = (_head + 1) % _buffer.Length;
                    if (_count < _buffer.Length) _count++;
                }
            }

            public IReadOnlyList<RingEntry> GetAll()
            {
                lock (_lock)
                {
                    var result = new List<RingEntry>(_count);
                    int start = _count < _buffer.Length ? 0 : _head;
                    for (int i = 0; i < _count; i++)
                    {
                        result.Add(_buffer[(start + i) % _buffer.Length]);
                    }
                    return result;
                }
            }
        }
    }
}
