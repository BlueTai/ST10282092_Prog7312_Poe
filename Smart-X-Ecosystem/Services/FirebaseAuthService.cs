using System.Text.Json;

namespace Smart_X_Ecosystem.Services
{
    public class FirebaseAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public bool IsAuthenticated { get; private set; }
        public string CurrentUserEmail { get; private set; } = string.Empty;
        public string IdToken { get; private set; } = string.Empty;

        public event Action? OnAuthStateChanged;

        public FirebaseAuthService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
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
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                return (false, "Firebase WebApiKey is missing in appsettings.json.");
            }

            string url = $"https://identitytoolkit.googleapis.com/v1/accounts:{action}?key={_apiKey}";
            var payload = new { email, password, returnSecureToken = true };

            try
            {
                var response = await _httpClient.PostAsJsonAsync(url, payload);
                var json = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(json);
                    IdToken = doc.RootElement.GetProperty("idToken").GetString() ?? string.Empty;
                    CurrentUserEmail = doc.RootElement.GetProperty("email").GetString() ?? email;
                    IsAuthenticated = true;
                    OnAuthStateChanged?.Invoke();
                    return (true, "Authenticated successfully via Firebase Identity Toolkit API.");
                }
                else
                {
                    using var doc = JsonDocument.Parse(json);
                    string errorMsg = doc.RootElement.GetProperty("error").GetProperty("message").GetString() ?? "Authentication failed.";
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