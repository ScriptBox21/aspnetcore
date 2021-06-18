// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Microsoft.AspNetCore.Internal;
using Microsoft.Extensions.Primitives;

namespace Microsoft.AspNetCore.Http.Features
{
    /// <summary>
    /// Default implementation for <see cref="IQueryFeature"/>.
    /// </summary>
    public class QueryFeature : IQueryFeature
    {
        // Lambda hoisted to static readonly field to improve inlining https://github.com/dotnet/roslyn/issues/13624
        private readonly static Func<IFeatureCollection, IHttpRequestFeature?> _nullRequestFeature = f => null;

        private FeatureReferences<IHttpRequestFeature> _features;

        private string? _original;
        private IQueryCollection? _parsedValues;

        /// <summary>
        /// Initializes a new instance of <see cref="QueryFeature"/>.
        /// </summary>
        /// <param name="query">The <see cref="IQueryCollection"/> to use as a backing store.</param>
        public QueryFeature(IQueryCollection query)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            _parsedValues = query;
        }

        /// <summary>
        /// Initializes a new instance of <see cref="QueryFeature"/>.
        /// </summary>
        /// <param name="features">The <see cref="IFeatureCollection"/> to initialize.</param>
        public QueryFeature(IFeatureCollection features)
        {
            if (features == null)
            {
                throw new ArgumentNullException(nameof(features));
            }

            _features.Initalize(features);
        }

        private IHttpRequestFeature HttpRequestFeature =>
            _features.Fetch(ref _features.Cache, _nullRequestFeature)!;

        /// <inheritdoc />
        public IQueryCollection Query
        {
            get
            {
                if (_features.Collection is null)
                {
                    return _parsedValues ?? QueryCollection.Empty;
                }

                var current = HttpRequestFeature.QueryString;
                if (_parsedValues == null || !string.Equals(_original, current, StringComparison.Ordinal))
                {
                    _original = current;

                    var result = ParseNullableQueryInternal(current);

                    _parsedValues = result is not null
                        ? new QueryCollectionInternal(result)
                        : QueryCollection.Empty;
                }
                return _parsedValues;
            }
            set
            {
                _parsedValues = value;
                if (_features.Collection != null)
                {
                    if (value == null)
                    {
                        _original = string.Empty;
                        HttpRequestFeature.QueryString = string.Empty;
                    }
                    else
                    {
                        _original = QueryString.Create(_parsedValues).ToString();
                        HttpRequestFeature.QueryString = _original;
                    }
                }
            }
        }

        /// <summary>
        /// Parse a query string into its component key and value parts.
        /// </summary>
        /// <param name="queryString">The raw query string value, with or without the leading '?'.</param>
        /// <returns>A collection of parsed keys and values, null if there are no entries.</returns>
        [SkipLocalsInit]
        internal static AdaptiveCapacityDictionary<string, StringValues>? ParseNullableQueryInternal(string? queryString)
        {
            if (string.IsNullOrEmpty(queryString) || (queryString.Length == 1 && queryString[0] == '?'))
            {
                return null;
            }

            var accumulator = new KvpAccumulator();
            var query = queryString.AsSpan();

            if (query[0] == '?')
            {
                query = query[1..];
            }

            while (!query.IsEmpty)
            {
                var delimiterIndex = query.IndexOf('&');

                var querySegment = delimiterIndex >= 0
                    ? query.Slice(0, delimiterIndex)
                    : query;

                var equalIndex = querySegment.IndexOf('=');

                if (equalIndex >= 0)
                {
                    var name = SpanHelper.ReplacePlusWithSpace(querySegment.Slice(0, equalIndex));
                    var value = SpanHelper.ReplacePlusWithSpace(querySegment.Slice(equalIndex + 1));

                    accumulator.Append(
                        Uri.UnescapeDataString(name),
                        Uri.UnescapeDataString(value));
                }
                else
                {
                    if (!querySegment.IsEmpty)
                    {
                        accumulator.Append(querySegment);
                    }
                }

                if (delimiterIndex < 0)
                {
                    break;
                }

                query = query.Slice(delimiterIndex + 1);
            }

            return accumulator.HasValues
                ? accumulator.GetResults()
                : null;
        }

        internal struct KvpAccumulator
        {
            /// <summary>
            /// This API supports infrastructure and is not intended to be used
            /// directly from your code. This API may change or be removed in future releases.
            /// </summary>
            private AdaptiveCapacityDictionary<string, StringValues> _accumulator;
            private AdaptiveCapacityDictionary<string, List<string>> _expandingAccumulator;

            public void Append(ReadOnlySpan<char> key, ReadOnlySpan<char> value = default)
                => Append(key.ToString(), value.IsEmpty ? string.Empty : value.ToString());

            /// <summary>
            /// This API supports infrastructure and is not intended to be used
            /// directly from your code. This API may change or be removed in future releases.
            /// </summary>
            public void Append(string key, string value)
            {
                if (_accumulator is null)
                {
                    _accumulator = new AdaptiveCapacityDictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
                }

                if (!_accumulator.TryGetValue(key, out var values))
                {
                    // First value for this key
                    _accumulator[key] = new StringValues(value);
                }
                else
                {
                    AppendToExpandingAccumulator(key, value, values);
                }

                ValueCount++;
            }

            private void AppendToExpandingAccumulator(string key, string value, StringValues values)
            {
                // When there are some values for the same key, so switch to expanding accumulator, and
                // add a zero count marker in the accumulator to indicate that switch.

                if (values.Count != 0)
                {
                    _accumulator[key] = default;

                    if (_expandingAccumulator is null)
                    {
                        _expandingAccumulator = new AdaptiveCapacityDictionary<string, List<string>>(capacity: 5, StringComparer.OrdinalIgnoreCase);
                    }

                    // Already 2 (1 existing + the new one) entries so use List's expansion mechanism for more
                    var list = new List<string>();

                    list.AddRange(values);
                    list.Add(value);

                    _expandingAccumulator[key] = list;
                }
                else
                {
                    // The marker indicates we are in the expanding accumulator, so just append to the list.
                    _expandingAccumulator[key].Add(value);
                }
            }

            /// <summary>
            /// This API supports infrastructure and is not intended to be used
            /// directly from your code. This API may change or be removed in future releases.
            /// </summary>
            public bool HasValues => ValueCount > 0;

            /// <summary>
            /// This API supports infrastructure and is not intended to be used
            /// directly from your code. This API may change or be removed in future releases.
            /// </summary>
            public int KeyCount => _accumulator?.Count ?? 0;

            /// <summary>
            /// This API supports infrastructure and is not intended to be used
            /// directly from your code. This API may change or be removed in future releases.
            /// </summary>
            public int ValueCount { get; private set; }

            /// <summary>
            /// This API supports infrastructure and is not intended to be used
            /// directly from your code. This API may change or be removed in future releases.
            /// </summary>
            public AdaptiveCapacityDictionary<string, StringValues> GetResults()
            {
                if (_expandingAccumulator != null)
                {
                    // Coalesce count 3+ multi-value entries into _accumulator dictionary
                    foreach (var entry in _expandingAccumulator)
                    {
                        _accumulator[entry.Key] = new StringValues(entry.Value.ToArray());
                    }
                }

                return _accumulator ?? new AdaptiveCapacityDictionary<string, StringValues>(0, StringComparer.OrdinalIgnoreCase);
            }
        }

        private static class SpanHelper
        {
            private static readonly SpanAction<char, IntPtr> s_replacePlusWithSpace = ReplacePlusWithSpaceCore;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static unsafe string ReplacePlusWithSpace(ReadOnlySpan<char> span)
            {
                fixed (char* ptr = &MemoryMarshal.GetReference(span))
                {
                    return string.Create(span.Length, (IntPtr)ptr, s_replacePlusWithSpace);
                }
            }

            private static unsafe void ReplacePlusWithSpaceCore(Span<char> buffer, IntPtr state)
            {
                fixed (char* ptr = &MemoryMarshal.GetReference(buffer))
                {
                    var input = (ushort*)state.ToPointer();
                    var output = (ushort*)ptr;

                    var i = (nint)0;
                    var n = (nint)(uint)buffer.Length;

                    if (Sse41.IsSupported && n >= Vector128<ushort>.Count)
                    {
                        var vecPlus = Vector128.Create((ushort)'+');
                        var vecSpace = Vector128.Create((ushort)' ');

                        do
                        {
                            var vec = Sse2.LoadVector128(input + i);
                            var mask = Sse2.CompareEqual(vec, vecPlus);
                            var res = Sse41.BlendVariable(vec, vecSpace, mask);
                            Sse2.Store(output + i, res);
                            i += Vector128<ushort>.Count;
                        } while (i <= n - Vector128<ushort>.Count);
                    }

                    for (; i < n; ++i)
                    {
                        if (input[i] != '+')
                        {
                            output[i] = input[i];
                        }
                        else
                        {
                            output[i] = ' ';
                        }
                    }
                }
            }
        }
    }
}
