using System;
using System.Collections.Generic;
using UnityEngine;

namespace TacticsGame.Core
{
    /// <summary>
    /// A lightweight Service Locator to manage cross-system dependencies
    /// without relying on tight coupling or Singletons.
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> services = new Dictionary<Type, object>();

        public static void Register<T>(T service)
        {
            var type = typeof(T);
            if (services.ContainsKey(type))
            {
                Debug.LogWarning(
                    $"[ServiceLocator] Service {type} is already registered. Overwriting."
                );
                services[type] = service;
            }
            else
            {
                services.Add(type, service);
                Debug.Log($"[ServiceLocator] Registered service: {type}");
            }
        }

        public static void Unregister<T>()
        {
            var type = typeof(T);
            if (services.ContainsKey(type))
            {
                services.Remove(type);
                Debug.Log($"[ServiceLocator] Unregistered service: {type}");
            }
        }

        public static T Get<T>()
        {
            var type = typeof(T);
            if (services.TryGetValue(type, out var service))
            {
                return (T)service;
            }

            Debug.LogError(
                $"[ServiceLocator] Service {type} not found! Did you forget to register it?"
            );
            return default;
        }

        public static bool TryGet<T>(out T service)
        {
            var type = typeof(T);
            if (services.TryGetValue(type, out var foundService))
            {
                service = (T)foundService;
                return true;
            }
            service = default;
            return false;
        }

        public static void ClearAll()
        {
            services.Clear();
            Debug.Log("[ServiceLocator] Cleared all services.");
        }
    }
}
