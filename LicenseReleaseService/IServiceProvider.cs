using System;

namespace LicenseReleaseService
{
    /// <summary>
    /// Simple service provider interface for dependency injection
    /// </summary>
    public interface IServiceProvider
    {
        /// <summary>
        /// Gets the service object of the specified type.
        /// </summary>
        /// <param name="serviceType">An object that specifies the type of service object to get.</param>
        /// <returns>A service object of type serviceType, or null if there is no service object of type serviceType.</returns>
        object GetService(Type serviceType);
    }

    /// <summary>
    /// Extension methods for IServiceProvider
    /// </summary>
    public static class ServiceProviderExtensions
    {
        /// <summary>
        /// Gets service of type T from the IServiceProvider.
        /// </summary>
        /// <typeparam name="T">The type of service object to get.</typeparam>
        /// <param name="provider">The IServiceProvider to retrieve the service object from.</param>
        /// <returns>A service object of type T or null if there is no such service.</returns>
        public static T GetService<T>(this IServiceProvider provider)
        {
            return (T)provider.GetService(typeof(T));
        }
    }
}