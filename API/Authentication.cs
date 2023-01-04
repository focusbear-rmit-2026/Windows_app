using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Auth0.AuthenticationApi;
using Auth0.AuthenticationApi.Models;
using Focus_Bear.Utils;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;

namespace Focus_Bear.API
{
    public class Authentication
    {
       
        private String sClientId;
        private String sClientSecret;
        private String sRealm;
        private String sAudience;
        public string sErrorMessage;

        AppSettings appSettings = AppSettings.GetInstance;

        public Authentication()
        {
            sClientId = "YAYPDa7sAVKuheZy3dYWyzNncOSZq98I";
            sClientSecret = "at1yjtbt6GKH03Pez05Tu5SxljduNMwOf1gkOZqe3WNb46UbnRoxHhvPiIkirlXA";
            sRealm = "Username-Password-Authentication";
            sAudience = "https://auth.focusbear.io/api/v2/";
        }

        //Username = "email@email.com",
        //Password = "Email@email.com123"
        public bool _isEmptyOrInvalid(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return true;
            }

            var jwtToken = new JwtSecurityToken(token);
            if (jwtToken == null)
            {
                return true;
            }
            if(jwtToken.ValidTo < DateTime.UtcNow)
            {
                return true;
            }
            if(jwtToken.ValidFrom > DateTime.UtcNow)
            {
                return true;
            }
            return false ;
        }

        public string getJWTTokenClaim(string token, string claimName)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var securityToken = (JwtSecurityToken)tokenHandler.ReadToken(token); 
                var claimValue = securityToken.Claims.FirstOrDefault(c => c.Type == claimName)?.Value;
                return claimValue;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("getJWTTokenClaim() Exception" + ex.Message);
                return null;
            }
        }
  
        public async Task<bool> Login(String sUsername, String sPassword)
        {
            LogWriter.LogWrite("async Task<bool> Login()");
            try
            {
                //https://github.com/auth0/auth0.net/blob/master/src/Auth0.AuthenticationApi/AuthenticationApiClient.cs
                var authenticationApiClient = new AuthenticationApiClient(new System.Uri($"https://dev-2hidr8ad.us.auth0.com/"));
                              
                var authenticationResponse = await authenticationApiClient.GetTokenAsync(new ResourceOwnerTokenRequest
                {
                    ClientId = sClientId,
                    ClientSecret = sClientSecret,
                    Realm = sRealm,
                    Scope = "openid profile offline_access", // Here you can set the scope
                    Username = sUsername,
                    Password = sPassword,
                    Audience = sAudience
                    
                });

                //Proceed after login
                appSettings.userAccount.email = sUsername;
                appSettings.userAccount.accessToken = authenticationResponse.AccessToken;
                appSettings.userAccount.refreshToken = authenticationResponse.RefreshToken;

                string strTopLevelClaim = getJWTTokenClaim(appSettings.userAccount.accessToken, "https://eyst-backend-prod-8th3.onrender.com/user");

                if (strTopLevelClaim.Length > 0)
                {
                    JObject strTopLevelClaimsParsed = JObject.Parse(strTopLevelClaim);
                    string sUserId = strTopLevelClaimsParsed["id"].ToString();

                    appSettings.userAccount.userID = sUserId;
                    LogWriter.LogWrite("USER ID is:" + sUserId);
                }
                   
                appSettings.SaveTokens();
                appSettings.SaveLoggedInUserInfo();

                return true;

            }
            catch(Exception ex)
            {
                LogWriter.LogWrite("public async Task<bool> Login(String sUsername, String sPassword) EXCEPTION" + ex.Message);

                sErrorMessage = ex.Message;

                Console.WriteLine("\nMessage ---\n{0}", ex.Message);
                return false;
            }
 
        }

        public async Task<bool> RefreshToken(String sRefreshToken)
        {
            try
            {
                //https://github.com/auth0/auth0.net/blob/master/src/Auth0.AuthenticationApi/AuthenticationApiClient.cs
                var authenticationApiClient = new AuthenticationApiClient(new System.Uri($"https://dev-2hidr8ad.us.auth0.com/"));
        
                var authenticationResponse = await authenticationApiClient.GetTokenAsync(new RefreshTokenRequest
                {
                    ClientId = sClientId,
                    ClientSecret = sClientSecret,
                    RefreshToken = sRefreshToken,
                    Scope = "openid profile offline_access", // Here you can set the scope
                    Audience = sAudience
                });

                //Proceed after login
                appSettings.userAccount.accessToken = authenticationResponse.AccessToken;
                appSettings.userAccount.refreshToken = authenticationResponse.RefreshToken;

                appSettings.SaveTokens();

                return true;

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("RefreshToken(String sRefreshToken) EXCEPTION - " + ex.Message);

                sErrorMessage = ex.Message;

                Console.WriteLine("\nMessage ---\n{0}", ex.Message);
                return false;
            }

        }

        public async Task<bool> SignUp(String sUsername, String sPassword)
        {
         
            try
            {
                //https://github.com/auth0/auth0.net/blob/master/src/Auth0.AuthenticationApi/AuthenticationApiClient.cs
                var authenticationApiClient = new AuthenticationApiClient(new System.Uri($"https://dev-2hidr8ad.us.auth0.com/"));

                var req = new SignupUserRequest();
                req.Email = sUsername;
                req.Password = sPassword;
                req.ClientId = sClientId;
                req.Connection = sRealm;

                var t = await authenticationApiClient.SignupUserAsync(req);
                return true;

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("public async Task<bool> SignUp(String sUsername, String sPassword) EXCEPTION - " + ex.Message);

                sErrorMessage = ex.Message;

                Console.WriteLine("\nMessage ---\n{0}", ex.Message);
                return false;

            }          
            
        }
    }
}

