using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Auth0.AuthenticationApi;
using Auth0.AuthenticationApi.Models;
using Auth0.OidcClient;
using Duende.IdentityModel.OidcClient;
using Duende.IdentityModel.OidcClient.Browser;
using Focus_Bear.Login;
using Focus_Bear.Utils;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json.Linq;


namespace Focus_Bear.API
{
    public class SystemBrowser : IBrowser
    {
        private readonly int _port;
        private HttpListener listener;
        public SystemBrowser(int port = 7890)
        {
            _port = port;
        }

        public async Task<BrowserResult> InvokeAsync(BrowserOptions options, CancellationToken cancellationToken = default)
        {
            listener = new HttpListener();
            var redirectUri = $"http://127.0.0.1:{_port}/";
            listener.Prefixes.Add(redirectUri);
            listener.Start();

            // Ensure cancellation will free the port
            cancellationToken.Register(() =>
            {
                try
                {
                    listener?.Stop();
                    listener?.Close();
                }
                catch { }
            });

            // Launch system browser
            Process.Start(new ProcessStartInfo
            {
                FileName = options.StartUrl,
                UseShellExecute = true,
                Verb = "open"
            });

            // Wait for redirect
            HttpListenerContext context = null;

            try
            {
                context = await listener.GetContextAsync();
            }
            catch (HttpListenerException)
            {
                return new BrowserResult { ResultType = BrowserResultType.UserCancel };
            }

            // Respond with auto-close HTML
            string responseString = @"<html>
          <head>
            <title>Login successful</title>
            <script type='text/javascript'>
              setTimeout(function() {
                try {
                  window.open('', '_self');
                  window.close();
                } catch (e) { }
              }, 1500);
            </script>
          </head>
          <body>
            <p>Login successful! This tab will close automatically.</p>
            <p>If it doesn't, you can close it manually.</p>
          </body>
        </html>";

            var buffer = Encoding.UTF8.GetBytes(responseString);
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            context.Response.OutputStream.Close();
            
            Dispose(); // free the listener

            return new BrowserResult
            {
                ResultType = BrowserResultType.Success,
                Response = context.Request.Url.ToString()
            };
        }

        public void Dispose()
        {
            if (listener != null)
            {
                try
                {
                    listener.Stop();
                    listener.Close();
                    listener = null;
                }
                catch { }
            }
        }
    }

    public class CustomWebView2Browser : IBrowser
    {
        private readonly string _userDataFolder;
        private readonly Window _owner;
        public CustomWebView2Browser(string userDataFolder, Window wndOwner = null)
        {
            _userDataFolder = userDataFolder;
            _owner = wndOwner;
        }

        public async Task<BrowserResult> InvokeAsync(BrowserOptions options, System.Threading.CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<BrowserResult>();

            Application.Current.Dispatcher.Invoke(() =>
            {
                var window = new Window
                {
                    Width = 900,
                    Height = 630,
                    Title = "Focus Bear - Sign in with Google",
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };
                if (_owner != null)
                {
                    window.Owner = _owner;
                }
                var webView = new WebView2();
                window.Content = webView;

                window.Closed += (s, e) =>
                {
                    if (!tcs.Task.IsCompleted)
                    {
                        tcs.TrySetResult(new BrowserResult
                        {
                            ResultType = BrowserResultType.UserCancel
                        });
                    }
                };

                webView.NavigationStarting += (s, e) =>
                {
                    if (e.Uri.StartsWith(options.EndUrl, StringComparison.OrdinalIgnoreCase))
                    {
                        e.Cancel = true;
                        tcs.TrySetResult(new BrowserResult
                        {
                            ResultType = BrowserResultType.Success,
                            Response = e.Uri
                        });
                        window.Close();
                    }
                };

                async void InitWebView()
                {
                    var env = await CoreWebView2Environment.CreateAsync(null, _userDataFolder);
                    await webView.EnsureCoreWebView2Async(env);
                    webView.CoreWebView2.Navigate(options.StartUrl);
                }

                InitWebView();
                window.ShowDialog();
            });

            return await tcs.Task;
        }
    }

    public class Authentication
    {
       
        private String sClientId;
        private String sRealm;
        private String sAudience;
        public string sErrorMessage;
        AppSettings appSettings = AppSettings.GetInstance;

        public Authentication()
        {
            sClientId = "YAYPDa7sAVKuheZy3dYWyzNncOSZq98I";
            //sClientSecret = "at1yjtbt6GKH03Pez05Tu5SxljduNMwOf1gkOZqe3WNb46UbnRoxHhvPiIkirlXA";
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


        public async Task<bool> SendMagicLink(String sUsername)
        {
            try
            {
                //https://github.com/auth0/auth0.net/blob/master/src/Auth0.AuthenticationApi/AuthenticationApiClient.cs
                var authenticationApiClient = new AuthenticationApiClient(new System.Uri($"https://dev-2hidr8ad.us.auth0.com/"));

                var req = new PasswordlessEmailRequest();
                req.Email = sUsername;
                req.ClientId = sClientId;

                var t = await authenticationApiClient.StartPasswordlessEmailFlowAsync(req);
                return true;

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("public async Task<bool> SendMagicLink(String sUsername, String sPassword) EXCEPTION - " + ex.Message);

                sErrorMessage = ex.Message;

                Console.WriteLine("\nMessage ---\n{0}", ex.Message);
                return false;

            }

        }
        private CancellationTokenSource _loginCts;
        public async Task<bool> LoginWithGoogle(FocusBearMainWindow ParentWindow, bool bLoginWithApple = false)
        {
            LogWriter.LogWrite("async Task<bool> LoginWithGoogle()");

            var userDataFolder = System.IO.Path.Combine(AppSettings.AppDataDirPath, "FBCustomWebView2");

            try
            {
                // create CTS for this login attempt
                _loginCts = new CancellationTokenSource();

                var client = new Auth0Client(new Auth0ClientOptions
                {
                    Domain = "dev-2hidr8ad.us.auth0.com",
                    ClientId = sClientId,
                    Scope = "openid profile email offline_access",
                    RedirectUri = "http://127.0.0.1:7890/",
                    LoadProfile = true,
                    Browser = new SystemBrowser(7890)
                });

                
                try
                {
                    string sConnection = "google-oauth2";
                    if (bLoginWithApple)
                    {
                        sConnection = "apple";
                    }
                    var loginResult = await client.LoginAsync(new Dictionary<string, string>
                        {
                            { "connection", sConnection },
                            { "audience", "https://auth.focusbear.io/api/v2/" }
                        },
                        _loginCts.Token
                    );

                    if (!loginResult.IsError)
                    {
                        appSettings.userAccount.email = loginResult.User.FindFirst("email")?.Value;
                        appSettings.userAccount.accessToken = loginResult.AccessToken;
                        appSettings.userAccount.refreshToken = loginResult.RefreshToken;
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

                        LogWriter.LogWrite("LoginWithGoogle() Successful");
                        return true;
                    }
                    else
                    {
                        LogWriter.LogWrite("LoginWithGoogle() FAILED");
                        return false;
                    }
                }
                catch (OperationCanceledException)
                {
                    LogWriter.LogWrite("LoginWithGoogle() was cancelled by user.");
                    return false;
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("Google login error: " + ex);
                    return false;
                }
                finally
                {
                    _loginCts.Dispose();
                    _loginCts = null; // ensure we can start fresh next time
                }
            }
            catch (HttpListenerException ex)
            {
                LogWriter.LogWrite("Port 7890 already in use: " + ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Google login error: " + ex.ToString());
            }
            return false;
        }

        public void CancelGoogleLogin()
        {
            _loginCts?.Cancel();
        }
    }
}

