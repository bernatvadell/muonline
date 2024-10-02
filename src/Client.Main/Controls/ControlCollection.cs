using System.Collections.Generic;
using System;

namespace Client.Main.Controls
{
    public class ControlCollection : ICollection<GameControl>
    {
        private List<GameControl> _controls = new List<GameControl>();

        public GameControl Parent { get; private set; }
        public int Count => _controls.Count;
        public bool IsReadOnly => false;

        internal ControlCollection(GameControl parent)
        {
            Parent = parent;
        }

        public GameControl this[int index]
        {
            get => _controls[index];
            set => _controls[index] = value;
        }

        public void Add(GameControl control)
        {
            control.Parent = Parent;
            _controls.Add(control);
        }

        public IEnumerator<GameControl> GetEnumerator()
        {
            return _controls.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _controls.GetEnumerator();
        }

        public void Clear()
        {
            foreach (var control in _controls)
                control.Parent = null;

            _controls.Clear();
        }

        public bool Contains(GameControl item)
        {
            return _controls.Contains(item);
        }

        public void CopyTo(GameControl[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        bool ICollection<GameControl>.Remove(GameControl control)
        {
            control.Parent = null;
            return _controls.Remove(control);
        }

        internal void Add(object value)
        {
            throw new NotImplementedException();
        }
    }
}