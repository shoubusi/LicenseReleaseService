using System;
using System.Collections.Generic;

namespace Microsoft.Extensions.DependencyInjection
{
    public interface ILoggingBuilder
    {
    }

    public interface IServiceCollection : IList<ServiceDescriptor>
    {
    }

    public class ServiceDescriptor
    {
        public Type ServiceType { get; }
        public Type ImplementationType { get; }
        public object ImplementationInstance { get; }
        public ServiceLifetime Lifetime { get; }

        public ServiceDescriptor(Type serviceType, Type implementationType, ServiceLifetime lifetime)
        {
            ServiceType = serviceType;
            ImplementationType = implementationType;
            Lifetime = lifetime;
        }

        public ServiceDescriptor(Type serviceType, object implementationInstance)
        {
            ServiceType = serviceType;
            ImplementationInstance = implementationInstance;
            Lifetime = ServiceLifetime.Singleton;
        }
    }

    public enum ServiceLifetime
    {
        Singleton,
        Scoped,
        Transient
    }

    public static class ServiceCollectionServiceExtensions
    {
        public static IServiceCollection AddLogging(this IServiceCollection services, Action<ILoggingBuilder> configure)
        {
            // Stub implementation - logging configuration would be handled here
            return services;
        }

        public static IServiceCollection AddSingleton<TService>(this IServiceCollection services)
        {
            return services.AddSingleton(typeof(TService), typeof(TService));
        }

        public static IServiceCollection AddSingleton<TService>(this IServiceCollection services, TService implementationInstance)
        {
            return services.AddSingleton(typeof(TService), implementationInstance);
        }

        public static IServiceCollection AddSingleton<TService>(this IServiceCollection services, Func<IServiceProvider, TService> implementationFactory)
        {
            // Stub implementation - factory would be stored and used to create instances
            return services.AddSingleton(typeof(TService), typeof(TService));
        }

        public static IServiceCollection AddSingleton<TService, TImplementation>(this IServiceCollection services)
            where TImplementation : TService
        {
            return services.AddSingleton(typeof(TService), typeof(TImplementation));
        }

        public static IServiceCollection AddSingleton(this IServiceCollection services, Type serviceType, Type implementationType)
        {
            services.Add(new ServiceDescriptor(serviceType, implementationType, ServiceLifetime.Singleton));
            return services;
        }

        public static IServiceCollection AddSingleton(this IServiceCollection services, Type serviceType, object implementationInstance)
        {
            services.Add(new ServiceDescriptor(serviceType, implementationInstance));
            return services;
        }

        public static IServiceCollection AddTransient<TService>(this IServiceCollection services)
        {
            return services.AddTransient(typeof(TService), typeof(TService));
        }

        public static IServiceCollection AddTransient<TService, TImplementation>(this IServiceCollection services)
            where TImplementation : TService
        {
            return services.AddTransient(typeof(TService), typeof(TImplementation));
        }

        public static IServiceCollection AddTransient(this IServiceCollection services, Type serviceType, Type implementationType)
        {
            services.Add(new ServiceDescriptor(serviceType, implementationType, ServiceLifetime.Transient));
            return services;
        }

        public static IServiceCollection AddScoped<TService>(this IServiceCollection services)
        {
            return services.AddScoped(typeof(TService), typeof(TService));
        }

        public static IServiceCollection AddScoped<TService, TImplementation>(this IServiceCollection services)
            where TImplementation : TService
        {
            return services.AddScoped(typeof(TService), typeof(TImplementation));
        }

        public static IServiceCollection AddScoped(this IServiceCollection services, Type serviceType, Type implementationType)
        {
            services.Add(new ServiceDescriptor(serviceType, implementationType, ServiceLifetime.Scoped));
            return services;
        }
    }
}