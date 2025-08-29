namespace CryptoDCACalculator.Services;

/// <summary>
/// Simple mock authentication service
/// In a real app, this would integrate with proper authentication providers
/// </summary>
public class AuthenticationService
{
    private bool _isLoggedIn = false;
    private string _currentUser = string.Empty;

    // Mock user credentials - in production, this would use proper authentication
    private readonly Dictionary<string, string> _mockUsers = new()
    {
        { "demo@crypto.com", "password123" },
        { "investor@dca.com", "invest123" },
        { "user@test.com", "test123" }
    };

    public bool IsLoggedIn => _isLoggedIn;
    public string CurrentUser => _currentUser;

    /// <summary>
    /// Mock login method - validates against hardcoded credentials
    /// Returns true if login successful, false otherwise
    /// </summary>
    public async Task<bool> LoginAsync(string email, string password)
    {
        // Simulate network delay
        await Task.Delay(1000);

        if (_mockUsers.TryGetValue(email, out string? value) && value == password)
        {
            _isLoggedIn = true;
            _currentUser = email;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Logs out the current user
    /// </summary>
    public void Logout()
    {
        _isLoggedIn = false;
        _currentUser = string.Empty;
    }
}
