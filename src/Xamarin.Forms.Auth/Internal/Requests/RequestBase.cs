﻿// Copyright (c) 2019 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Xamarin.Forms.Auth
{
    /// <summary>
    /// Base class for all flows. Use by implementing <see cref="ExecuteAsync(CancellationToken)"/>
    /// and optionally calling protected helper methods such as SendTokenRequestAsync, which know
    /// how to use all params when making the request.
    /// </summary>
    internal abstract class RequestBase
    {
        protected RequestBase(
            IServiceBundle serviceBundle,
            AuthenticationRequestParameters authenticationRequestParameters,
            IAcquireTokenParameters acquireTokenParameters)
        {
            ServiceBundle = serviceBundle;
            AuthenticationRequestParameters = authenticationRequestParameters;
            if (authenticationRequestParameters.Scope == null || authenticationRequestParameters.Scope.Count == 0)
            {
                throw new ArgumentNullException(nameof(authenticationRequestParameters), nameof(authenticationRequestParameters.Scope));
            }

            ValidateScopeInput(authenticationRequestParameters.Scope);

            acquireTokenParameters.LogParameters(AuthenticationRequestParameters.RequestContext.Logger);
        }

        internal AuthenticationRequestParameters AuthenticationRequestParameters { get; }

        protected IServiceBundle ServiceBundle { get; }

        protected ITokenCache TokenCache => ServiceBundle?.PlatformProxy?.TokenCache;

        public async Task<AuthenticationResult> RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                await PreRunAsync().ConfigureAwait(false);
                AuthenticationRequestParameters.LogParameters(AuthenticationRequestParameters.RequestContext.Logger);
                LogRequestStarted(AuthenticationRequestParameters);

                AuthenticationResult authenticationResult = await ExecuteAsync(cancellationToken).ConfigureAwait(false);
                LogReturnedToken(authenticationResult);

                return authenticationResult;
            }
            catch (AuthException ex)
            {
                AuthenticationRequestParameters.RequestContext.Logger.ErrorPii(ex);
                throw;
            }
            catch (Exception ex)
            {
                AuthenticationRequestParameters.RequestContext.Logger.ErrorPii(ex);
                throw;
            }
        }

        internal abstract Task<AuthenticationResult> ExecuteAsync(CancellationToken cancellationToken);

        internal virtual Task PreRunAsync()
        {
            return Task.FromResult(0);
        }

        protected static SortedSet<string> GetDecoratedScope(SortedSet<string> inputScope)
        {
            SortedSet<string> set = new SortedSet<string>(inputScope.ToArray());
            set.UnionWith(ScopeHelper.CreateSortedSetFromEnumerable(OAuth2Value.ReservedScopes));
            return set;
        }

        protected AuthenticationResult CacheTokenResponseAndCreateAuthenticationResult(OAuth2TokenResponse tokenResponse)
        {
            // developer passed in user object.
            AuthenticationRequestParameters.RequestContext.Logger.Info("Checking client info returned from the server..");

            var authenticationResults = new AuthenticationResult(tokenResponse, ServiceBundle.Config.IsExtendedTokenLifetimeEnabled);

            TokenCache?.SaveAccessAndRefreshToken(AuthenticationRequestParameters.Authority, AuthenticationRequestParameters.ClientId, tokenResponse);

            return authenticationResults;
        }

        protected void ValidateScopeInput(SortedSet<string> scopesToValidate)
        {
            // Check if scope or additional scope contains client ID.
            // TODO: instead of failing in the validation, could we simply just remove what the user sets and log that we did so instead?
            if (scopesToValidate.Intersect(ScopeHelper.CreateSortedSetFromEnumerable(OAuth2Value.ReservedScopes)).Any())
            {
                throw new ArgumentException("MSAL always sends the scopes 'openid profile offline_access'. " +
                                            "They cannot be suppressed as they are required for the " +
                                            "library to function. Do not include any of these scopes in the scope parameter.");
            }

            if (scopesToValidate.Contains(AuthenticationRequestParameters.ClientId))
            {
                throw new ArgumentException("API does not accept client id as a user-provided scope");
            }
        }

        protected Task<OAuth2TokenResponse> SendTokenRequestAsync(
            IDictionary<string, string> additionalBodyParameters,
            CancellationToken cancellationToken)
        {
            return SendTokenRequestAsync(
                additionalBodyParameters,
                cancellationToken);
        }

        protected async Task<OAuth2TokenResponse> SendTokenRequestAsync(
            string tokenEndpoint,
            IDictionary<string, string> additionalBodyParameters,
            CancellationToken cancellationToken)
        {
            OAuth2Client client = new OAuth2Client(ServiceBundle.HttpManager);
            client.AddBodyParameter(OAuth2Parameter.ClientId, AuthenticationRequestParameters.ClientId);
            client.AddBodyParameter(OAuth2Parameter.ClientInfo, "1");

            client.AddBodyParameter(OAuth2Parameter.Scope, GetDecoratedScope(AuthenticationRequestParameters.Scope).AsSingleString());

            client.AddQueryParameter(OAuth2Parameter.Claims, AuthenticationRequestParameters.Claims);

            foreach (var kvp in additionalBodyParameters)
            {
                client.AddBodyParameter(kvp.Key, kvp.Value);
            }

            return await SendHttpMessageAsync(client, tokenEndpoint).ConfigureAwait(false);
        }

        private void LogRequestStarted(AuthenticationRequestParameters authenticationRequestParameters)
        {
            string messageWithPii = string.Format(
                CultureInfo.InvariantCulture,
                "=== Token Acquisition ({3}) started:\n\tAuthority: {0}\n\tScope: {1}\n\tClientId: {2}",
                authenticationRequestParameters.Authority,
                authenticationRequestParameters.Scope.AsSingleString(),
                authenticationRequestParameters.ClientId,
                GetType().Name);

            string messageWithoutPii = string.Format(
                CultureInfo.InvariantCulture,
                "=== Token Acquisition ({0}) started.",
                GetType().Name);

            if (authenticationRequestParameters.Authority != null)
            {
                messageWithoutPii += string.Format(
                    CultureInfo.CurrentCulture,
                    "\n\tAuthority Host: {0}",
                    authenticationRequestParameters.Authority?.Host);
            }

            authenticationRequestParameters.RequestContext.Logger.InfoPii(messageWithPii, messageWithoutPii);
        }

        private async Task<OAuth2TokenResponse> SendHttpMessageAsync(OAuth2Client client, string tokenEndpoint)
        {
            UriBuilder builder = new UriBuilder(tokenEndpoint);
            builder.AppendQueryParameters(AuthenticationRequestParameters.ExtraQueryParameters);
            OAuth2TokenResponse msalTokenResponse =
                await client
                    .GetTokenAsync(
                        builder.Uri,
                        AuthenticationRequestParameters.RequestContext)
                    .ConfigureAwait(false);

            if (string.IsNullOrEmpty(msalTokenResponse.Scope))
            {
                msalTokenResponse.Scope = AuthenticationRequestParameters.Scope.AsSingleString();
                AuthenticationRequestParameters.RequestContext.Logger.Info("ScopeSet was missing from the token response, so using developer provided scopes in the result");
            }

            return msalTokenResponse;
        }

        private void LogReturnedToken(AuthenticationResult result)
        {
            if (result.AccessToken != null)
            {
                AuthenticationRequestParameters.RequestContext.Logger.Info(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "=== Token Acquisition finished successfully. An access token was returned with Expiration Time: {0} ===",
                        result.ExpiresOn));
            }
        }
    }
}