using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Smart_X_Ecosystem.Services
{
    public class FirebaseAuthService
    {
        private readonly HttpClient _httpClient = new();
        private readonly string _apiKey;

        public bool IsAuthenticated { get; private set; }
        public string CurrentUserEmail { get; private set; } = string.Empty;
        public string IdToken { get; private set; } = string.Empty;

        public event Action? OnAuthStateChanged;

        public FirebaseAuthService(IConfiguration config)
        {
            _apiKey = config["Firebase:WebApiKey"] ?? string.Empty;
        }

        public async Task<(bool Success, string Message)> SignInAsync(string email, string password)
        {
            return await SendFirebaseAuthRequestAsync("signInWithPassword", email, password);
        }

        public async Task<(bool Success, string Message)> SignUpAsync(string email, string password)
        {
            return await SendFirebaseAuthRequestAsync("signUp", email, password);
        }

        private async Task<(bool Success, string Message)> SendFirebaseAuthRequestAsync(string action, string email, string password)
        {
            string cleanEmail = (email ?? string.Empty).Trim();
            string cleanPassword = (password ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(cleanEmail))
            {
                return (false, "Please enter your operator email address.");
            }

            if (!cleanEmail.Contains('@') || !cleanEmail.Contains('.'))
            {
                return (false, "Please enter a valid email address format (for example: operator@smartx.io).");
            }

            if (string.IsNullOrWhiteSpace(cleanPassword))
            {
                return (false, "Please enter your password.");
            }

            if (cleanPassword.Length < 6)
            {
                return (false, "Your password is too short. Passwords must be at least 6 characters long.");
            }

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                return (false, "Authentication service configuration is missing (Firebase WebApiKey).");
            }

            string url = $"https://identitytoolkit.googleapis.com/v1/accounts:{action}?key={_apiKey}";
            var payload = new { email = cleanEmail, password = cleanPassword, returnSecureToken = true };

            try
            {
                var response = await _httpClient.PostAsJsonAsync(url, payload);
                var json = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(json);
                    IdToken = doc.RootElement.GetProperty("idToken").GetString() ?? string.Empty;
                    CurrentUserEmail = doc.RootElement.GetProperty("email").GetString() ?? cleanEmail;
                    IsAuthenticated = true;
                    OnAuthStateChanged?.Invoke();
                    return (true, "Signed in successfully.");
                }
                else
                {
                    using var doc = JsonDocument.Parse(json);
                    string rawError = doc.RootElement.GetProperty("error").GetProperty("message").GetString() ?? "UNKNOWN_ERROR";

                    return (false, FormatFriendlyAuthError(rawError, action));
                }
            }
            catch (Exception)
            {
                return (false, "Unable to reach the authentication server. Please check your internet connection and try again.");
            }
        }

        private static string FormatFriendlyAuthError(string firebaseErrorCode, string action)
        {
            string code = firebaseErrorCode.ToUpperInvariant();

            if (code.Contains("INVALID_LOGIN_CREDENTIALS") || code.Contains("INVALID_PASSWORD"))
            {
                return "Incorrect email or password. Please double-check your password or click 'Register New Operator' to create an account.";
            }

            if (code.Contains("EMAIL_NOT_FOUND"))
            {
                return "No operator account was found with this email address. Click 'Register New Operator' below to create one.";
            }

            if (code.Contains("EMAIL_EXISTS"))
            {
                return "An account with this email address already exists. Please click 'Sign In to Console' instead.";
            }

            if (code.Contains("WEAK_PASSWORD"))
            {
                return "Password is too weak. Please enter a stronger password with at least 6 characters.";
            }

            if (code.Contains("INVALID_EMAIL"))
            {
                return "The email address entered is not valid. Please check for typos (e.g., operator@smartx.io).";
            }

            if (code.Contains("TOO_MANY_ATTEMPTS_TRY_LATER"))
            {
                return "Too many unsuccessful sign-in attempts. Access has been temporarily paused for security—please try again in a few minutes.";
            }

            if (code.Contains("USER_DISABLED"))
            {
                return "This operator account has been disabled by an administrator.";
            }

            return action == "signUp"
                ? "Could not register account. Please verify your email and password (minimum 6 characters)."
                : "Sign-in failed. Please verify your email and password and try again.";
        }

        public void SignOut()
        {
            IsAuthenticated = false;
            CurrentUserEmail = string.Empty;
            IdToken = string.Empty;
            OnAuthStateChanged?.Invoke();
        }
    }
}