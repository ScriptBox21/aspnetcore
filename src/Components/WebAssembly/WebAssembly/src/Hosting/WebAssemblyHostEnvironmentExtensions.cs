using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.AspNetCore.Components.WebAssembly.Hosting
{
    public static class WebAssemblyHostEnvironmentExtensions
    {
        /// <summary>
        /// Checks if the current hosting environment name is <see cref="EnvironmentName.Development"/>.
        /// </summary>
        /// <param name="hostingEnvironment">An instance of <see cref="IWebAssemblyHostEnvironment"/>.</param>
        /// <returns>True if the environment name is <see cref="EnvironmentName.Development"/>, otherwise false.</returns>
        public static bool IsDevelopment(this IWebAssemblyHostEnvironment hostingEnvironment)
        {
            if (hostingEnvironment == null)
            {
                throw new ArgumentNullException(nameof(hostingEnvironment));
            }

            return hostingEnvironment.IsEnvironment("Development");
        }

        /// <summary>
        /// Checks if the current hosting environment name is <see cref="EnvironmentName.Staging"/>.
        /// </summary>
        /// <param name="hostingEnvironment">An instance of <see cref="IWebAssemblyHostEnvironment"/>.</param>
        /// <returns>True if the environment name is <see cref="EnvironmentName.Staging"/>, otherwise false.</returns>
        public static bool IsStaging(this IWebAssemblyHostEnvironment hostingEnvironment)
        {
            if (hostingEnvironment == null)
            {
                throw new ArgumentNullException(nameof(hostingEnvironment));
            }

            return hostingEnvironment.IsEnvironment("Staging");
        }

        /// <summary>
        /// Checks if the current hosting environment name is <see cref="EnvironmentName.Production"/>.
        /// </summary>
        /// <param name="hostingEnvironment">An instance of <see cref="IWebAssemblyHostEnvironment"/>.</param>
        /// <returns>True if the environment name is <see cref="EnvironmentName.Production"/>, otherwise false.</returns>
        public static bool IsProduction(this IWebAssemblyHostEnvironment hostingEnvironment)
        {
            if (hostingEnvironment == null)
            {
                throw new ArgumentNullException(nameof(hostingEnvironment));
            }

            return hostingEnvironment.IsEnvironment("Production");
        }

        /// <summary>
        /// Compares the current hosting environment name against the specified value.
        /// </summary>
        /// <param name="hostingEnvironment">An instance of <see cref="IWebAssemblyHostEnvironment"/>.</param>
        /// <param name="environmentName">Environment name to validate against.</param>
        /// <returns>True if the specified name is the same as the current environment, otherwise false.</returns>
        public static bool IsEnvironment(
            this IWebAssemblyHostEnvironment hostingEnvironment,
            string environmentName)
        {
            if (hostingEnvironment == null)
            {
                throw new ArgumentNullException(nameof(hostingEnvironment));
            }

            return string.Equals(
                hostingEnvironment.Environment,
                environmentName,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
