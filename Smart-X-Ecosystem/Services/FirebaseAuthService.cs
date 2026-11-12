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

            if (string.IsNullOrWhiteSpace(cleanEmail) || !cleanEmail.Contains('@'))
            {
                return (false, "Please enter a valid operator email address (e.g., operator@smartx.io).");
            }

            if (string.IsNullOrWhiteSpace(cleanPassword) || cleanPassword.Length < 6)
            {
                return (false, "Firebase requires passwords to be at least 6 characters long.");
            }

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                return (false, "Firebase WebApiKey is missing in appsettings.json.");
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
                    return (true, "Authenticated successfully via Firebase Identity Toolkit API.");
                }
                else
                {
                    using var doc = JsonDocument.Parse(json);
                    string errorMsg = doc.RootElement.GetProperty("error").GetProperty("message").GetString() ?? "Authentication failed.";

                    // If user clicks "Create New Operator Account" for an email that already exists, automatically try signing them in
                    if (action == "signUp" && errorMsg.Contains("EMAIL_EXISTS", StringComparison.OrdinalIgnoreCase))
                    {
                        return await SendFirebaseAuthRequestAsync("signInWithPassword", cleanEmail, cleanPassword);
                    }

                    return (false, $"Firebase API Error: {errorMsg}");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Network error contacting Firebase API: {ex.Message}");
            }
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