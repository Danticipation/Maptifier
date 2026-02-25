using System;
using System.Collections.Generic;

namespace Maptifier.Core
{
    public static class EventBus
    {
        private static readonly Dictionary<Type, Delegate> Handlers = new();

        public static void Subscribe<T>(Action<T> handler)
        {
            if (handler == null) return;

            var type = typeof(T);
            if (Handlers.TryGetValue(type, out var existing))
            {
                Handlers[type] = Delegate.Combine(existing, handler);
            }
            else
            {
                Handlers[type] = handler;
            }
        }

        public static void Unsubscribe<T>(Action<T> handler)
        {
            if (handler == null) return;

            var type = typeof(T);
            if (Handlers.TryGetValue(type, out var existing))
            {
                var combined = Delegate.Remove(existing, handler);
                if (combined == null)
                {
                    Handlers.Remove(type);
                }
                else
                {
                    Handlers[type] = combined;
                }
            }
        }

        public static void Publish<T>(T eventData)
        {
            var type = typeof(T);
            if (Handlers.TryGetValue(type, out var handler))
            {
                ((Action<T>)handler).Invoke(eventData);
            }
        }
    }
}
