using System;
using System.Collections.Generic;

namespace Microsoft.Extensions.DependencyInjection
{
    public interface IServiceProvider
    {
        object GetService(Type serviceType);
    }

    public static class ServiceProviderServiceExtensions
    {
        public static T GetService<T>(this IServiceProvider provider)
        {
            return (T)provider.GetService(typeof(T));
        }

        public static T GetRequiredService<T>(this IServiceProvider provider)
        {
            var service = provider.GetService<T>();
            if (service == null)
            {
                throw new InvalidOperationException($"Service of type {typeof(T)} not found");
            }
            return service;
        }
    }

    public class ServiceProvider : IServiceProvider, IDisposable
    {
        private readonly IServiceCollection _services;
        private readonly Dictionary<Type, object> _singletonInstances = new Dictionary<Type, object>();

        public ServiceProvider(IServiceCollection services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            InitializeSingletons();
        }

        private void InitializeSingletons()
        {
            foreach (var descriptor in _services)
            {
                if (descriptor.Lifetime == ServiceLifetime.Singleton && descriptor.ImplementationInstance != null)
                {
                    _singletonInstances[descriptor.ServiceType] = descriptor.ImplementationInstance;
                }
            }
        }

        public object GetService(Type serviceType)
        {
            // Check if already created singleton
            if (_singletonInstances.TryGetValue(serviceType, out var instance))
            {
                return instance;
            }

            // Find the service descriptor
            ServiceDescriptor descriptor = null;
            foreach (var d in _services)
            {
                if (d.ServiceType == serviceType)
                {
                    descriptor = d;
                    break;
                }
            }

            if (descriptor == null)
            {
                return null;
            }

            // Create the instance
            object createdInstance = null;
            if (descriptor.ImplementationInstance != null)
            {
                createdInstance = descriptor.ImplementationInstance;
            }
            else if (descriptor.ImplementationType != null)
            {
                createdInstance = Activator.CreateInstance(descriptor.ImplementationType);
            }

            if (descriptor.Lifetime == ServiceLifetime.Singleton)
            {
                _singletonInstances[serviceType] = createdInstance;
            }

            return createdInstance;
        }

        public void Dispose()
        {
            foreach (var instance in _singletonInstances.Values)
            {
                if (instance is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            _singletonInstances.Clear();
        }
    }

    public static class ServiceCollectionContainerBuilderExtensions
    {
        public static IServiceProvider BuildServiceProvider(this IServiceCollection services)
        {
            return new ServiceProvider(services);
        }
    }
}