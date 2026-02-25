using System;
using System.Collections.Generic;
using UnityEngine;

namespace Maptifier.Core
{
    /// <summary>
    /// Single allowed singleton for service registration and resolution.
    /// All services register here during initialization and are resolved
    /// via interface types throughout the application.
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> _services = new(16);
        private static bool _isInitialized;

        public static event Action OnServicesReady;

        public static void Register<T>(T service) where T : class
        {
            var type = typeof(T);
            if (_services.ContainsKey(type))
            {
                Debug.LogWarning($"[ServiceLocator] Overwriting existing service: {type.Name}");
            }
            _services[type] = service;
        }

        public static T Get<T>() where T : class
        {
            var type = typeof(T);
            if (_services.TryGetValue(type, out var service))
            {
                return (T)service;
            }
            Debug.LogError($"[ServiceLocator] Service not found: {type.Name}. Was it registered?");
            return null;
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            var type = typeof(T);
            if (_services.TryGetValue(type, out var obj))
            {
                service = (T)obj;
                return true;
            }
            service = null;
            return false;
        }

        public static bool Has<T>() where T : class
        {
            return _services.ContainsKey(typeof(T));
        }

        public static void MarkReady()
        {
            _isInitialized = true;
            OnServicesReady?.Invoke();
        }

        public static void Reset()
        {
            _services.Clear();
            _isInitialized = false;
            OnServicesReady = null;
        }

        public static bool IsInitialized => _isInitialized;
    }
}
