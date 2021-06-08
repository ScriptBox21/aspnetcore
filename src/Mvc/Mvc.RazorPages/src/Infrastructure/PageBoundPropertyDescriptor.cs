// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Reflection;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure
{
    /// <summary>
    /// Describes a page bound property.
    /// </summary>
    public class PageBoundPropertyDescriptor : ParameterDescriptor, IPropertyInfoParameterDescriptor
    {
        /// <summary>
        /// Gets or sets the <see cref="System.Reflection.PropertyInfo"/> for this property.
        /// </summary>
        public PropertyInfo Property { get; set; } = default!;

        PropertyInfo IPropertyInfoParameterDescriptor.PropertyInfo => Property;
    }
}
