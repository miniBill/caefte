using System;

namespace Caefte
{
    public struct Span<T>
    {
        public T[] Array { get; }
        public int Offset { get; }
        public int Count { get; }

        public Span(T[] array, int offset, int count)
        {
            Array = array;
            Offset = offset <= 0 ? 0 : offset > array.Length ? array.Length : offset;
            Count = count <= 0 ? 0 : Math.Min(array.Length - Offset, count);
        }

        public T this[int index] { get => Array[index + Offset]; }

        public Span<T> Skip(int amount) => new Span<T>(Array, Offset + amount, Count - amount);
        public Span<T> SkipLast(int amount) => new Span<T>(Array, Offset, Count - amount);
        public Span<T> Take(int count) => new Span<T>(Array, Offset, Math.Min(count, Count));

        public T[] ToArray()
        {
            var result = new T[Count];
            System.Array.Copy(Array, Offset, result, 0, Count);
            return result;
        }
    }
}
