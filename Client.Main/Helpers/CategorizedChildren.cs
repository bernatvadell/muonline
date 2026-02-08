#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using static MUnique.OpenMU.Network.Packets.ClientToServer.LahapJewelMixRequest;

namespace Client.Main.Helpers
{
    public sealed class ActionDisposable : IDisposable
    {
        private Action? _dispose;
        public ActionDisposable(Action dispose) => _dispose = dispose;
        public void Dispose() { _dispose?.Invoke(); _dispose = null; }
    }

    public sealed class CategoryRule<TItem>
    {
        public required Func<TItem, bool> Predicate { get; init; }
        public Func<TItem, Action, IDisposable>? Watch { get; init; }
    }


    public sealed class CategorizedChildren<TItem, TCategory>
    where TItem : class, IChildItem<TItem>
    where TCategory : struct, Enum
    {
        private readonly ChildrenCollection<TItem> _children;
        private readonly IReadOnlyDictionary<TCategory, CategoryRule<TItem>> _rules;

        private readonly Dictionary<TCategory, List<TItem>> _cache = new();
        private readonly Dictionary<TItem, List<IDisposable>> _subscriptions = new();
        private bool _dirty = true;

        public CategorizedChildren(
            ChildrenCollection<TItem> children,
            IReadOnlyDictionary<TCategory, CategoryRule<TItem>> rules)
        {
            _children = children ?? throw new ArgumentNullException(nameof(children));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));

            foreach (var cat in _rules.Keys)
                _cache.TryAdd(cat, new List<TItem>(16));

            _children.ControlAdded += (_, e) => { Hook(e.Control); _dirty = true; };
            _children.ControlRemoved += (_, e) => { Unhook(e.Control); _dirty = true; };

            for (int i = 0; i < _children.Count; i++)
                Hook(_children[i]);
        }

        public IReadOnlyList<TItem> Get(TCategory category)
        {
            Ensure();
            return _cache.TryGetValue(category, out var list) ? list : Array.Empty<TItem>();
        }

        private void Invalidate() => _dirty = true;

        private void Hook(TItem item)
        {
            if (_subscriptions.ContainsKey(item)) return;

            var list = new List<IDisposable>(capacity: _rules.Count);
            foreach (var r in _rules.Values)
            {
                var watch = r.Watch;
                if (watch == null) continue;
                list.Add(watch(item, Invalidate));
            }

            if (list.Count > 0)
                _subscriptions[item] = list;
        }

        private void Unhook(TItem item)
        {
            if (!_subscriptions.TryGetValue(item, out var list)) return;
            for (int i = 0; i < list.Count; i++) list[i].Dispose();
            _subscriptions.Remove(item);
        }

        private void Ensure()
        {
            if (!_dirty) return;

            foreach (var kv in _cache) kv.Value.Clear();

            for (int i = 0; i < _children.Count; i++)
            {
                var item = _children[i];
                foreach (var kv in _rules)
                    if (kv.Value.Predicate(item))
                        _cache[kv.Key].Add(item);
            }

            _dirty = false;
        }
    }

}
