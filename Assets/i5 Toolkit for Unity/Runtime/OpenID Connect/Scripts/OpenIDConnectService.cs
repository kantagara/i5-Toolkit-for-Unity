﻿using i5.Toolkit.Core.ServiceCore;
using i5.Toolkit.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace i5.Toolkit.Core.OpenIDConnectClient
{
    public class OpenIDConnectService : IService
    {
        private ClientData clientData;

        public IClientDataLoader ClientDataLoader { get; set; } = new ClientDataResourcesLoader();

        public string[] Scopes { get; set; } = new string[] { "openid", "profile", "email" };

        public string AccessToken { get; private set; }

        public bool IsLoggedIn { get => !string.IsNullOrEmpty(AccessToken); }

        public IOidcProvider OidcProvider { get; set; }

        public IRedirectServerListener ServerListener { get; set; }

        public event EventHandler LoginCompleted;
        public event EventHandler LogoutCompleted;

        public OpenIDConnectService()
        {
            ServerListener = new RedirectServerListener();
        }

        public OpenIDConnectService(ClientData clientData) : this()
        {
            this.clientData = clientData;
        }

        public async void Initialize(BaseServiceManager owner)
        {
            if (clientData == null)
            {
                clientData = await ClientDataLoader.LoadClientDataAsync();
            }

            if (clientData == null)
            {
                i5Debug.LogError("No client data supplied for the OpenID Connect Client.\n" +
                    "Create a JSON file in the resources or reference a OpenID Connect Data file.", this);
            }
        }

        public void Cleanup()
        {
            ServerListener.StopServerImmediately();
            if (IsLoggedIn)
            {
                Logout();
            }
        }

        public void OpenLoginPage()
        {
            if (OidcProvider == null)
            {
                i5Debug.LogError("OIDC provider is not set. Please set the OIDC provider before accessing the OIDC workflow.", this);
                return;
            }
            if (ServerListener == null)
            {
                i5Debug.LogError("Redirect server listener is not set. Please set it before accessing the OIDC workflow.", this);
                return;
            }

            OidcProvider.ClientData = clientData;

            // TODO: support custom Uri schema
            string redirectUri = ServerListener.GenerateRedirectUri();
            ServerListener.RedirectReceived += async (s, e) => await ServerListener_RedirectReceived(s, e);
            ServerListener.StartServer();

            OidcProvider.OpenLoginPage(Scopes, redirectUri);
        }

        private async Task ServerListener_RedirectReceived(object sender, RedirectReceivedEventArgs e)
        {
            if (OidcProvider.ParametersContainError(e.RedirectParameters, out string errorMessage))
            {
                i5Debug.LogError("Error: " + errorMessage, this);
                return;
            }

            if (OidcProvider.AuthorzationFlow == AuthorizationFlow.AUTHORIZATION_CODE)
            {
                string authorizationCode = OidcProvider.GetAuthorizationCode(e.RedirectParameters);
                AccessToken = await OidcProvider.GetAccessTokenFromCodeAsync(authorizationCode, e.RedirectUri);
                Debug.Log("Got access token " + AccessToken);
            }
            else
            {
                AccessToken = OidcProvider.GetAccessToken(e.RedirectParameters);
            }
            LoginCompleted?.Invoke(this, EventArgs.Empty);
        }

        public void Logout()
        {
            AccessToken = "";
            LogoutCompleted?.Invoke(this, EventArgs.Empty);
        }

        public async Task<bool> CheckAccessToken()
        {
            if (!IsLoggedIn)
            {
                i5Debug.LogWarning("Access token not valid because user is not logged in.", this);
                return false;
            }
            IUserInfo userInfo = await GetUserDataAsync();
            return userInfo != null;
        }

        public async Task<IUserInfo> GetUserDataAsync()
        {
            if (!IsLoggedIn)
            {
                i5Debug.LogError("Please log in first before accessing user data", this);
                return null;
            }
            return await OidcProvider.GetUserInfoAsync(AccessToken);
        }
    }
}