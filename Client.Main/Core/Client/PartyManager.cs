using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Client.Main.Core.Client
{
    public class PartyMemberInfo
    {
        public ushort Id { get; set; }
        public byte Index { get; set; }
        public string Name { get; set; }
        public byte MapId { get; set; }
        public byte PositionX { get; set; }
        public byte PositionY { get; set; }
        public uint CurrentHealth { get; set; }
        public uint MaximumHealth { get; set; }
        public float HealthPercentage => MaximumHealth > 0 ? (float)CurrentHealth / MaximumHealth : 0;
    }

    public class PartyManager
    {
        private readonly ILogger<PartyManager> _logger;
        private readonly ConcurrentDictionary<byte, PartyMemberInfo> _partyMembers = new();

        public event Action PartyUpdated;

        public PartyManager(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<PartyManager>();
        }

        public IReadOnlyCollection<PartyMemberInfo> GetPartyMembers()
        {
            return _partyMembers.Values.OrderBy(p => p.Index).ToList();
        }

        public void UpdatePartyList(List<PartyMemberInfo> members)
        {
            _logger.LogDebug("Updating party list with {Count} members.", members.Count);
            _partyMembers.Clear();
            foreach (var member in members)
            {
                if (_partyMembers.TryAdd(member.Index, member))
                {
                    _logger.LogDebug("Added party member '{Name}' at index {Index} with ID {Id:X4}", member.Name, member.Index, member.Id);
                }
                else
                {
                    _logger.LogWarning("Failed to add party member {Name} at index {Index}", member.Name, member.Index);
                }
            }
            OnPartyUpdated();
        }

        public void RemoveMember(byte index)
        {
            if (_partyMembers.TryRemove(index, out var member))
            {
                _logger.LogDebug("Removed party member '{Name}' at index {Index}", member?.Name, index);
                OnPartyUpdated();
            }
        }

        public void UpdateMemberHealth(byte index, float healthPercentage)
        {
            if (_partyMembers.TryGetValue(index, out var member))
            {
                uint newHealth = (uint)(member.MaximumHealth * healthPercentage);
                if (member.CurrentHealth != newHealth)
                {
                    member.CurrentHealth = newHealth;
                    OnPartyUpdated();
                }
            }
        }

        public void UpdateMemberPosition(ushort id, byte x, byte y)
        {
            var member = _partyMembers.Values.FirstOrDefault(m => m.Id == id);
            if (member != null)
            {
                if (member.PositionX != x || member.PositionY != y)
                {
                    member.PositionX = x;
                    member.PositionY = y;
                    OnPartyUpdated();
                }
            }
        }

        public void UpdateMemberMap(ushort id, ushort newMapId)
        {
            var member = _partyMembers.Values.FirstOrDefault(m => m.Id == id);
            if (member != null)
            {
                if (member.MapId != newMapId)
                {
                    member.MapId = (byte)newMapId;
                    OnPartyUpdated();
                }
            }
        }

        public void ClearParty()
        {
            if (_partyMembers.IsEmpty) return;
            _partyMembers.Clear();
            OnPartyUpdated();
        }

        private void OnPartyUpdated()
        {
            _logger.LogTrace("Invoking PartyUpdated event.");
            MuGame.ScheduleOnMainThread(() => PartyUpdated?.Invoke());
        }

        public bool IsPartyActive()
        {
            return _partyMembers.Count > 1;
        }

        public bool IsMember(ushort id)
        {
            return _partyMembers.Values.Any(m => m.Id == id);
        }

        public float GetHealthPercentage(ushort id)
        {
            var member = _partyMembers.Values.FirstOrDefault(m => m.Id == id);
            return member?.HealthPercentage ?? 0f;
        }
    }
}