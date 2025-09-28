using System;
using System.Collections;
using System.Collections.Generic;

namespace LicenseReleaseService.IdleDetection.Collections
{
    public class ReadOnlyQueue<T> : IReadOnlyQueue<T>
    {
        private readonly Queue<T> _queue;

        public ReadOnlyQueue(Queue<T> queue)
        {
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        }

        public int Count => _queue.Count;

        public bool Contains(T item)
        {
            return _queue.Contains(item);
        }

        public T Peek()
        {
            return _queue.Peek();
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _queue.CopyTo(array, arrayIndex);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _queue.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _queue.GetEnumerator();
        }
    }
}