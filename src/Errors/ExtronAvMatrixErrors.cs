using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace PepperDash.Essentials.Plugin.Errors
{
    /// <summary>
    /// Extron SIS API Error Codes and Messages
    /// Based on Extron Simple Instruction Set (SIS) Protocol
    /// </summary>
    public enum ExtronSisErrorCode
    {
        /// <summary>
        /// Invalid input number (out of range)
        /// </summary>
        [Description("Invalid input number")]
        E01 = 1,

        /// <summary>
        /// Invalid command
        /// </summary>
        [Description("Invalid command")]
        E10 = 10,

        /// <summary>
        /// Invalid preset number (out of range)
        /// </summary>
        [Description("Invalid preset number")]
        E11 = 11,

        /// <summary>
        /// Invalid output number (out of range)
        /// </summary>
        [Description("Invalid output number")]
        E12 = 12,

        /// <summary>
        /// Invalid value (out of range)
        /// </summary>
        [Description("Invalid value")]
        E13 = 13,

        /// <summary>
        /// Invalid command for this configuration
        /// </summary>
        [Description("Invalid command for this configuration")]
        E14 = 14,

        /// <summary>
        /// Timeout
        /// </summary>
        [Description("Timeout")]
        E17 = 17,

        /// <summary>
        /// Busy
        /// </summary>
        [Description("Busy")]
        E22 = 22,

        /// <summary>
        /// Privileges violation (password access)
        /// </summary>
        [Description("Privileges violation")]
        E24 = 24,

        /// <summary>
        /// Device not present
        /// </summary>
        [Description("Device not present")]
        E25 = 25,

        /// <summary>
        /// Maximum number of connections exceeded
        /// </summary>
        [Description("Maximum number of connections exceeded")]
        E26 = 26,

        /// <summary>
        /// Invalid event number
        /// </summary>
        [Description("Invalid event number")]
        E27 = 27,

        /// <summary>
        /// Bad filename or file not found
        /// </summary>
        [Description("Bad filename or file not found")]
        E28 = 28,

        /// <summary>
        /// Bad file type or size (for logo assignment)
        /// </summary>
        [Description("Bad file type or size")]
        E33 = 33,

        /// <summary>
        /// Unknown error
        /// </summary>
        [Description("Unknown error")]
        Unknown = 99
    }

    /// <summary>
    /// Helper class for working with Extron SIS error codes
    /// </summary>
    public static class ExtronSisErrors
    {
        /// <summary>
        /// Dictionary mapping error codes to their descriptions
        /// </summary>
        private static readonly Dictionary<ExtronSisErrorCode, string> ErrorMessages = new Dictionary<ExtronSisErrorCode, string>
        {
            { ExtronSisErrorCode.E01, "Invalid input number" },
            { ExtronSisErrorCode.E10, "Invlaid command" },
            { ExtronSisErrorCode.E11, "Invalid preset number" },
            { ExtronSisErrorCode.E12, "Invalid output number" },
            { ExtronSisErrorCode.E13, "Invalid value" },
            { ExtronSisErrorCode.E14, "Invalid command for this configuration" },
            { ExtronSisErrorCode.E17, "Timeout" },
            { ExtronSisErrorCode.E22, "Busy" },
            { ExtronSisErrorCode.E24, "Privileges violation" },
            { ExtronSisErrorCode.E25, "Device not present" },
            { ExtronSisErrorCode.E26, "Maximum number of connections exceeded" },
            { ExtronSisErrorCode.E27, "Invalid event number" },
            { ExtronSisErrorCode.E28, "Bad filename or file not found" },
            { ExtronSisErrorCode.E33, "Bad file type or size" },
            { ExtronSisErrorCode.Unknown, "Unknown error" }
        };

        /// <summary>
        /// Gets the error message for the specified error code
        /// </summary>
        /// <param name="errorCode">The error code</param>
        /// <returns>The error message</returns>
        public static string GetErrorMessage(ExtronSisErrorCode errorCode)
        {
            return ErrorMessages.TryGetValue(errorCode, out string message) ? message : "Unknown error";
        }

        /// <summary>
        /// Gets the error message for the specified error code number
        /// </summary>
        /// <param name="errorCodeNumber">The error code number</param>
        /// <returns>The error message</returns>
        public static string GetErrorMessage(int errorCodeNumber)
        {
            if (Enum.IsDefined(typeof(ExtronSisErrorCode), errorCodeNumber))
            {
                return GetErrorMessage((ExtronSisErrorCode)errorCodeNumber);
            }
            return GetErrorMessage(ExtronSisErrorCode.Unknown);
        }

        /// <summary>
        /// Parses an error response from the device (e.g., "E01")
        /// </summary>
        /// <param name="errorResponse">The error response string</param>
        /// <returns>The corresponding error code</returns>
        public static ExtronSisErrorCode ParseErrorResponse(string errorResponse)
        {
            if (string.IsNullOrEmpty(errorResponse))
                return ExtronSisErrorCode.Unknown;

            // Remove whitespace and convert to uppercase
            errorResponse = errorResponse.Trim().ToUpper();

            // Handle "E##" format
            if (errorResponse.StartsWith("E") && errorResponse.Length >= 2)
            {
                string numberPart = errorResponse.Substring(1);
                if (int.TryParse(numberPart, out int errorNumber))
                {
                    if (Enum.IsDefined(typeof(ExtronSisErrorCode), errorNumber))
                    {
                        return (ExtronSisErrorCode)errorNumber;
                    }
                }
            }

            // Try to parse as enum name
            if (Enum.TryParse<ExtronSisErrorCode>(errorResponse, true, out ExtronSisErrorCode result))
            {
                return result;
            }

            return ExtronSisErrorCode.Unknown;
        }

        /// <summary>
        /// Checks if the response indicates an error
        /// </summary>
        /// <param name="response">The response from the device</param>
        /// <returns>True if the response indicates an error</returns>
        public static bool IsErrorResponse(string response)
        {
            if (string.IsNullOrEmpty(response))
                return false;

            return response.Trim().ToUpper().StartsWith("E");
        }

        /// <summary>
        /// Creates a formatted error message including the error code
        /// </summary>
        /// <param name="errorCode">The error code</param>
        /// <returns>Formatted error message</returns>
        public static string FormatErrorMessage(ExtronSisErrorCode errorCode)
        {
            return $"{errorCode}: {GetErrorMessage(errorCode)}";
        }

        /// <summary>
        /// Creates a formatted error message from an error response string
        /// </summary>
        /// <param name="errorResponse">The error response string</param>
        /// <returns>Formatted error message</returns>
        public static string FormatErrorMessage(string errorResponse)
        {
            var errorCode = ParseErrorResponse(errorResponse);
            return FormatErrorMessage(errorCode);
        }
    }
}
