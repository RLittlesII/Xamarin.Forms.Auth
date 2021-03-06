﻿// Copyright (c) 2019 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace Xamarin.Forms.Auth
{
    /// <summary>
    /// This exception class is to inform developers that UI interaction is required for authentication to
    /// succeed. It's thrown when calling <see cref="ClientApplicationBase.AcquireTokenSilent(System.Collections.Generic.IEnumerable{string}, string)"/> or one
    /// of its overrides, and when the token does not exists in the cache, or the user needs to provide more content, or perform multiple factor authentication based
    /// on Azure AD policies, etc..
    /// </summary>
    [Serializable]
    public class AuthUiRequiredException : AuthServiceException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AuthUiRequiredException"/> class with a specified.
        /// error code and error message.
        /// </summary>
        /// <param name="errorCode">
        /// The error code returned by the service or generated by the client. This is the code you can rely on
        /// for exception handling.
        /// </param>
        /// <param name="errorMessage">The error message that explains the reason for the exception.</param>
        public AuthUiRequiredException(string errorCode, string errorMessage)
            : this(errorCode, errorMessage, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthUiRequiredException"/> class with a specified.
        /// error code, error message and inner exception indicating the root cause.
        /// </summary>
        /// <param name="errorCode">
        /// The error code returned by the service or generated by the client. This is the code you can rely on
        /// for exception handling.
        /// </param>
        /// <param name="errorMessage">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">Represents the root cause of the exception.</param>
        public AuthUiRequiredException(string errorCode, string errorMessage, Exception innerException)
            : base(errorCode, errorMessage, innerException)
        {
        }
    }
}
