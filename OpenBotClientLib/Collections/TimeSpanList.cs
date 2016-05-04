using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenBot.Collections
{
    public class TimeSpanList<T> : IList<T> where T : IComparable<T>
    {
        public TimeSpan Duration { get; set; }
        private object _lock = new object();
        private TItem[] _items;

        public TimeSpanList(TimeSpan clearDuration)
        {
            Duration = clearDuration;
            _items = new TItem[0];
        }

        internal void UpdateItems()
        {
            List<int> indices = new List<int>();

            for (int i = 0; i < _items.Length; i++)
                if (_items[i].timeAdded + Duration < DateTime.Now)
                    indices.Add(i);

            for (int i = indices.Count - 1; i >= 0; i--)
                RemoveAt(i);
        }

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    UpdateItems();
                    return _items.Length;
                }
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        public T this[int index]
        {
            get
            {
                lock (_lock)
                {
                    UpdateItems();
                    return _items[index].item;
                }
            }

            set
            {
                lock (_lock)
                {
                    _items[index].item = value;
                }
            }
        }

        public int IndexOf(T item)
        {
            for (int i = 0; i < _items.Length; i++)
                if (_items[i].item.Equals(item))
                    return i;

            return -1;
        }

        public void Insert(int index, T item)
        {
            lock (_lock)
            {
                UpdateItems();
                TItem[] newItems = new TItem[_items.Length + 1];

                int u = 0;
                for (int i = 0; i < _items.Length; i++)
                {
                    if (index == u)
                    {
                        newItems[u] = new TItem(item);
                        u++;
                    }


                    newItems[u++] = _items[i];
                }

                _items = newItems;
            }
        }

        public void RemoveAt(int index)
        {
            lock (_lock)
            {
                TItem[] newItems = new TItem[_items.Length - 1];

                int u = 0;
                for (int i = 0; i < newItems.Length; i++)
                {
                    if (index == u)
                        u++;

                    newItems[i] = _items[u++];
                }

                _items = newItems;
            }
        }

        public void Add(T item)
        {
            lock (_lock)
            {
                TItem[] newItems = new TItem[_items.Length + 1];
                Array.Copy(_items, newItems, _items.Length);
                newItems[_items.Length] = new TItem(item);
                _items = newItems;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _items = new TItem[0];
            }
        }

        public bool Contains(T item)
        {
            lock (_lock)
            {
                foreach (TItem i in _items)
                    if (i.item.Equals(item))
                        return true;

                return false;
            }
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            lock (_lock)
            {
                UpdateItems();
                for (int i = arrayIndex; i < arrayIndex + _items.Length; i++)
                    array[i] = _items[i].item;
            }
        }

        public bool Remove(T item)
        {
            lock (_lock)
            {
                UpdateItems();
                for (int i = 0; i < _items.Length; i++)
                    if (_items[i].item.Equals(item))
                    {
                        RemoveAt(i);
                        return true;
                    }

                return false;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            lock (_lock)
            {
                UpdateItems();
                foreach (TItem i in _items)
                    yield return i.item;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private struct TItem
        {
            internal T item;
            internal DateTime timeAdded;

            internal TItem(T item)
            {
                this.item = item;
                this.timeAdded = DateTime.Now;
            }
        }
    }
}
