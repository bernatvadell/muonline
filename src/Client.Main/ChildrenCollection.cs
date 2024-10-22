using System.Collections.Generic;
using System;
using Client.Main.Controls;

namespace Client.Main
{
    public interface IChildItem<T> where T : class
    {
        T? Parent { get; set; }
    }

    public class ChildrenEventArgs<T> where T : class, IChildItem<T>
    {
        public T Control { get; }

        public ChildrenEventArgs(T control)
        {
            Control = control;
        }
    }

    public class ChildrenCollection<T> : ICollection<T> where T : class, IChildItem<T>
    {
        private List<T> _controls = [];

        public T Parent { get; private set; }
        public int Count => _controls.Count;
        public bool IsReadOnly => false;

        public event EventHandler<ChildrenEventArgs<T>> ControlAdded;
        public event EventHandler<ChildrenEventArgs<T>> ControlRemoved;

        internal ChildrenCollection(T parent)
        {
            Parent = parent;
        }

        public T this[int index]
        {
            get => _controls[index];
            set => throw new NotImplementedException();
        }

        public void Add(T control)
        {
            control.Parent = Parent;
            _controls.Add(control);
            ControlAdded?.Invoke(this, new ChildrenEventArgs<T>(control));
        }

        public void Insert(int index, T control)
        {
            control.Parent = Parent;
            _controls.Insert(index, control);
            ControlAdded?.Invoke(this, new ChildrenEventArgs<T>(control));
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
            {
                control.Parent = null;
                ControlRemoved?.Invoke(this, new ChildrenEventArgs<T>(control));
            }

            _controls.Clear();
        }

        public bool Contains(T item)
        {
            return _controls.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public bool Remove(T control)
        {
            var removed = _controls.Remove(control);

            if (removed)
            {
                control.Parent = null;
                ControlRemoved?.Invoke(this, new ChildrenEventArgs<T>(control));
            }

            return removed;
        }

        bool ICollection<T>.Remove(T control)
        {
            return this.Remove(control);
        }

        internal void Add(object value)
        {
            throw new NotImplementedException();
        }
    }
}