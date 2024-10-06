using System.Collections.Generic;
using System;
using Client.Main.Controls;

namespace Client.Main
{
    public interface IChildItem<T> where T : class
    {
        T? Parent { get; set; }
    }

    public class ChildrenCollection<T> : ICollection<T> where T : class, IChildItem<T>
    {
        private List<T> _controls = [];

        public T Parent { get; private set; }
        public int Count => _controls.Count;
        public bool IsReadOnly => false;

        internal ChildrenCollection(T parent)
        {
            Parent = parent;
        }

        public T this[int index]
        {
            get => _controls[index];
            set => _controls[index] = value;
        }

        public void Add(T control)
        {
            control.Parent = Parent;
            _controls.Add(control);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _controls.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _controls.GetEnumerator();
        }

        public void Clear()
        {
            var controls = _controls.ToArray();

            foreach (var control in controls)
                control.Parent = null;

            _controls.Clear();
        }

        public bool Contains(T item)
        {
            return _controls.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _controls.CopyTo(array, arrayIndex);
        }

        public bool Remove(T control)
        {
            control.Parent = null;
            return _controls.Remove(control);
        }

        bool ICollection<T>.Remove(T control)
        {
            return this.Remove(control);
        }
    }
}