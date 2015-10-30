﻿using System;
using System.Web;

//The following libraries were added to this sample.
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Cookies;
using Microsoft.Owin.Security.OpenIdConnect;
using Microsoft.Owin.Security.ActiveDirectory;
using Owin;

//The following libraries were defined and added to this sample.
using Tdlr.Utils;
using System.Globalization;
using Microsoft.Owin.Security.Notifications;
using Microsoft.IdentityModel.Protocols;
using Tdlr.DAL;

namespace Tdlr
{
    public partial class Startup
    {
        public void ConfigureAuth(IAppBuilder app)
        {
            app.SetDefaultSignInAsAuthenticationType(CookieAuthenticationDefaults.AuthenticationType);

            app.UseCookieAuthentication(new CookieAuthenticationOptions());

            //Configure OpenIDConnect, register callbacks for OpenIDConnect Notifications
            app.UseOpenIdConnectAuthentication(
                new OpenIdConnectAuthenticationOptions
                {
                    ClientId = ConfigHelper.ClientId,
                    Authority = String.Format(CultureInfo.InvariantCulture, ConfigHelper.AadInstance, "common"),
                    PostLogoutRedirectUri = ConfigHelper.PostLogoutRedirectUri,
                    RedirectUri = ConfigHelper.PostLogoutRedirectUri,
                    TokenValidationParameters = new System.IdentityModel.Tokens.TokenValidationParameters
                    {
                        ValidateIssuer = false
                    },
                    Notifications = new OpenIdConnectAuthenticationNotifications
                    {
                        AuthorizationCodeReceived = OnAuthorizationCodeReceived,
                        AuthenticationFailed = OnAuthenticationFailed,
                        RedirectToIdentityProvider = OnRedirectToIdentityProvider,
                    }
                });

            app.UseWindowsAzureActiveDirectoryBearerAuthentication(new WindowsAzureActiveDirectoryBearerAuthenticationOptions
            {
                // Replace deprecated parameter with TVP.ValidAudience
                //
                //Audience = "https://strockisdevtwo.onmicrosoft.com/tdlr",
                Tenant = "dev.skwantoso.com",
                TokenValidationParameters = new System.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidAudience = "https://dev.skwantoso.com/tdlr",
                    ValidateIssuer = false,
                },
                AuthenticationType = "AADBearer",
            });
        }

        private Task OnRedirectToIdentityProvider(RedirectToIdentityProviderNotification<OpenIdConnectMessage, OpenIdConnectAuthenticationOptions> notification)
        {
            if (notification.Request.Path.Value.ToLower() == "/account/signup/aad")
            {
                notification.ProtocolMessage.Prompt = "consent";
                string login_hint = notification.OwinContext.Authentication.AuthenticationResponseChallenge.Properties.Dictionary["login_hint"];
                notification.ProtocolMessage.LoginHint = login_hint;
            }

            // Override the post signout redirect URI to be the URL of the app determined on the fly.
            // That way sign out works without config change regardless if running locally or running in the cloud.
            if (notification.Request.Path.Value.ToLower() == "/account/signout")
                notification.ProtocolMessage.PostLogoutRedirectUri = HttpContext.Current.Request.Url.GetLeftPart(UriPartial.Authority);

            // Explicitly add the redirectUri to the request.
            // This allows multiple redirect URIs to be registered in Azure AD, one for running locally and one for when running in the cloud.
            notification.ProtocolMessage.RedirectUri = (new Uri(HttpContext.Current.Request.Url.GetLeftPart(UriPartial.Authority))).ToString();

            return Task.FromResult(0);
        }

        private Task OnAuthorizationCodeReceived(AuthorizationCodeReceivedNotification notification)
        {
            ClientCredential credential = new ClientCredential(ConfigHelper.ClientId, ConfigHelper.AppKey);
            string userObjectId = notification.AuthenticationTicket.Identity.FindFirst(Globals.ObjectIdClaimType).Value;
            string tenantId = notification.AuthenticationTicket.Identity.FindFirst(Globals.TenantIdClaimType).Value;
            AuthenticationContext authContext = new AuthenticationContext(String.Format(CultureInfo.InvariantCulture, ConfigHelper.AadInstance, tenantId), new TokenDbCache(userObjectId));
            AuthenticationResult result = authContext.AcquireTokenByAuthorizationCode(
                notification.Code, new Uri(HttpContext.Current.Request.Url.GetLeftPart(UriPartial.Authority)), credential, ConfigHelper.GraphResourceId);
            return Task.FromResult(0);
        }

        private Task OnAuthenticationFailed(AuthenticationFailedNotification<OpenIdConnectMessage, OpenIdConnectAuthenticationOptions> notification)
        {
            notification.HandleResponse();
            notification.Response.Redirect("/Error/ShowError?signIn=true&errorMessage=" + notification.Exception.Message);
            return Task.FromResult(0);
        }
    }
}