using System.Collections.Generic;

namespace LicenseReleaseService.IdleDetection.Collections
{
    public interface IReadOnlyQueue<T> : IEnumerable<T>
    {
        int Count { get; }
        bool Contains(T item);
        T Peek();
        void CopyTo(T[] array, int arrayIndex);
    }
}