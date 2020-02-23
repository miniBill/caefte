using System.Collections.Generic;

namespace API
{
    public static class QueueExtensions
    {
        public static void EnqueueAll<T>(this Queue<T> queue, IEnumerable<T> items)
        {
            foreach (T item in items)
                queue.Enqueue(item);
        }
    }
}
