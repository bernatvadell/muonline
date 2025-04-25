using System.Collections.Generic;
using System;
using System.Linq;

namespace Client.Main
{
    public interface IChildItem<T> where T : class
    {
        T Parent { get; set; }
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
        private List<T> _controls = new List<T>();
        private readonly object _lock = new object();

        public T Parent { get; private set; }
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _controls.Count;
                }
            }
        }
        public bool IsReadOnly => false;

        public event EventHandler<ChildrenEventArgs<T>> ControlAdded;
        public event EventHandler<ChildrenEventArgs<T>> ControlRemoved;

        internal ChildrenCollection(T parent)
        {
            Parent = parent;
        }

        public T this[int index]
        {
            get
            {
                lock (_lock)
                {
                    return _controls[index];
                }
            }
            set => throw new NotImplementedException("Not implemented set index in ChildrenCollection");
        }

        public int IndexOf(T control)
        {
            lock (_lock)
            {
                return _controls.IndexOf(control);
            }
        }

        public void RemoveAt(int index)
        {
            T control;
            lock (_lock)
            {
                control = _controls[index];
                _controls.RemoveAt(index);
            }

            control.Parent = null;
            ControlRemoved?.Invoke(this, new ChildrenEventArgs<T>(control));
        }

        public void Add(T control)
        {
            lock (_lock)
            {
                control.Parent = Parent;
                _controls.Add(control);
            }

            ControlAdded?.Invoke(this, new ChildrenEventArgs<T>(control));
        }

        public void Insert(int index, T control)
        {
            lock (_lock)
            {
                control.Parent = Parent;
                _controls.Insert(index, control);
            }

            ControlAdded?.Invoke(this, new ChildrenEventArgs<T>(control));
        }

        public T[] ToArray()
        {
            lock (_lock)
            {
                return _controls.ToArray();
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            lock (_lock)
            {
                return _controls.ToArray().AsEnumerable().GetEnumerator();  // Enumerar sobre una copia para evitar problemas de concurrencia
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public void Clear()
        {
            T[] controls;
            lock (_lock)
            {
                controls = _controls.ToArray();
                _controls.Clear();
            }

            foreach (var control in controls)
            {
                control.Parent = null;
                ControlRemoved?.Invoke(this, new ChildrenEventArgs<T>(control));
            }
        }

        public bool Contains(T item)
        {
            lock (_lock)
            {
                return _controls.Contains(item);
            }
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            lock (_lock)
            {
                _controls.CopyTo(array, arrayIndex);
            }
        }

        public bool Remove(T control)
        {
            bool removed;
            lock (_lock)
            {
                removed = _controls.Remove(control);
            }

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
