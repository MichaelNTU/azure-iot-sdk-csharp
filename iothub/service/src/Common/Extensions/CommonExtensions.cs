// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;

#if NET451
using Microsoft.Owin;
#endif

using Microsoft.Azure.Devices.Common.WebApi;

namespace Microsoft.Azure.Devices.Common
{
    public delegate bool TryParse<TInput, TOutput>(TInput input, bool ignoreCase, out TOutput output);

    /// <summary>
    /// Extension method helpers
    /// </summary>
    public static class CommonExtensionMethods
    {
        private const char ValuePairDelimiter = ';';
        private const char ValuePairSeparator = '=';

        /// <summary>
        /// Appends the specified character, if not already there
        /// </summary>
        /// <param name="value">The string value to update</param>
        /// <param name="suffix">The desired suffix</param>
        /// <returns>The fixed up string</returns>
        public static string EnsureEndsWith(this string value, char suffix)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            int length = value.Length;
            if (length == 0)
            {
#pragma warning disable CA1305 // Specify IFormatProvider
                return suffix.ToString();
#pragma warning restore CA1305 // Specify IFormatProvider
            }

            return value[length - 1] == suffix
                    ? value
                    : value + suffix;
        }

        /// <summary>
        /// Prepends the specified character, if not already there
        /// </summary>
        /// <param name="value">The string value to update</param>
        /// <param name="prefix">The desired prefix</param>
        /// <returns>The fixed up string</returns>
        public static string EnsureStartsWith(this string value, char prefix)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (value.Length == 0)
            {
#pragma warning disable CA1305 // Specify IFormatProvider
                return prefix.ToString();
#pragma warning restore CA1305 // Specify IFormatProvider
            }
            else
            {
                return value[0] == prefix
                    ? value
                    : prefix + value;
            }
        }

        /// <summary>
        /// Gets the value of the specified key, if present
        /// </summary>
        /// <param name="map">The dictionary containing the specifed key</param>
        /// <param name="keyName">The key of the desired value</param>
        /// <returns>The value, if present</returns>
        public static string GetValueOrDefault(this IDictionary<string, string> map, string keyName)
        {
            if (!map.TryGetValue(keyName, out string value))
            {
                value = null;
            }

            return value;
        }

        /// <summary>
        /// Takes a string representation of key/value pairs and produces a dictionary
        /// </summary>
        /// <param name="keyValuePair">The string containing key/value pairs</param>
        /// <param name="kvpDelimiter">The delimeter between key/value pairs</param>
        /// <param name="kvpSeparator">The character separating each key and value</param>
        /// <returns>A dictionary of the key/value pairs</returns>
        public static IDictionary<string, string> ToDictionary(this string keyValuePair, char kvpDelimiter, char kvpSeparator)
        {
            if (string.IsNullOrWhiteSpace(keyValuePair))
            {
                throw new ArgumentException("Malformed token");
            }

            IEnumerable<string[]> parts = keyValuePair
                .Split(kvpDelimiter)
                .Select((part) => part.Split(new char[] { kvpSeparator }, 2));

            if (parts.Any((part) => part.Length != 2))
            {
                throw new FormatException("Malformed Token");
            }

            IDictionary<string, string> map = parts.ToDictionary((kvp) => kvp[0], (kvp) => kvp[1], StringComparer.OrdinalIgnoreCase);

            return map;
        }

        /// <summary>
        /// Gets the IoT hub name from the message header
        /// </summary>
        /// <param name="requestMessage">A request message</param>
        /// <param name="iotHubName">The hub name</param>
        /// <returns>True, if found</returns>
        public static bool TryGetIotHubName(this HttpRequestMessage requestMessage, out string iotHubName)
        {
            iotHubName = null;

            // IotHubMessageHandler sets the request message property with IotHubName read from hostname
            if (!requestMessage.Properties.TryGetValue(CustomHeaderConstants.HttpIotHubName, out object iotHubNameObj)
                || !(iotHubNameObj is string))
            {
                return false;
            }

            iotHubName = (string)iotHubNameObj;
            return true;
        }

        /// <summary>
        /// Gets the IoT hub name from the host name
        /// </summary>
        /// <param name="requestMessage">A request message</param>
        /// <returns>The hub name</returns>
        public static string GetIotHubNameFromHostName(this HttpRequestMessage requestMessage)
        {
            // {IotHubname}.[env-specific-sub-domain.]IotHub[-int].net

            string[] hostNameParts = requestMessage.RequestUri.Host.Split('.');
            if (hostNameParts.Length < 3)
            {
                throw new ArgumentException("Invalid request URI");
            }

            return hostNameParts[0];
        }

        /// <summary>
        /// Get the hub name from a request message
        /// </summary>
        /// <param name="requestMessage">A request message</param>
        /// <returns>The hub name</returns>
        public static string GetIotHubName(this HttpRequestMessage requestMessage)
        {
            if (!TryGetIotHubName(requestMessage, out string iotHubName))
            {
                throw new ArgumentException("Invalid request URI");
            }

            return iotHubName;
        }

#if NET451
        public static string GetMaskedClientIpAddress(this HttpRequestMessage requestMessage)
        {
            // note that this only works if we are hosted as an OWIN app
            if (requestMessage.Properties.ContainsKey("MS_OwinContext"))
            {
                OwinContext owinContext = requestMessage.Properties["MS_OwinContext"] as OwinContext;
                if (owinContext != null)
                {
                    string remoteIpAddress = owinContext.Request.RemoteIpAddress;

                    string maskedRemoteIpAddress = string.Empty;

                    IPAddress remoteIp = null;
                    IPAddress.TryParse(remoteIpAddress, out remoteIp);

                    if (null != remoteIp)
                    {
                        byte[] addressBytes = remoteIp.GetAddressBytes();
                        if (remoteIp.AddressFamily == AddressFamily.InterNetwork)
                        {
                            addressBytes[addressBytes.Length - 1] = 0xFF;
                            addressBytes[addressBytes.Length - 2] = 0xFF;
                            maskedRemoteIpAddress = new IPAddress(addressBytes).ToString();
                        }
                        else if (remoteIp.AddressFamily == AddressFamily.InterNetworkV6)
                        {
                            addressBytes[addressBytes.Length - 1] = 0xFF;
                            addressBytes[addressBytes.Length - 2] = 0xFF;
                            addressBytes[addressBytes.Length - 3] = 0xFF;
                            addressBytes[addressBytes.Length - 4] = 0xFF;
                            maskedRemoteIpAddress = new IPAddress(addressBytes).ToString();
                        }
                    }

                    return maskedRemoteIpAddress;
                }
            }

            return null;
        }
#endif

        public static void AppendKeyValuePairIfNotEmpty(this StringBuilder builder, string name, object value)
        {
            if (value != null)
            {
                builder.Append(name);
                builder.Append(ValuePairSeparator);
                builder.Append(value);
                builder.Append(ValuePairDelimiter);
            }
        }

        public static bool IsNullOrWhiteSpace(this string value)
        {
            return string.IsNullOrWhiteSpace(value);
        }

        public static string RemoveWhitespace(this string value)
        {
            return new string(value.Where(c => !char.IsWhiteSpace(c)).ToArray());
        }
    }
}
