using CryptoDCACalculator.Services;

namespace CryptoDCACalculator;

public partial class AppShell : Shell
{
    private readonly AuthenticationService _authService;

    public AppShell(AuthenticationService authService)
    {
        InitializeComponent();
        _authService = authService;
        
        // Start with login page
        Loaded += OnShellLoaded;
    }

    private async void OnShellLoaded(object? sender, EventArgs e)
    {
        // Navigate to login page initially
        await GoToAsync("//login");
    }

    protected override bool OnBackButtonPressed()
    {
        // Prevent back navigation from main page to login
        if (Current?.CurrentState?.Location?.OriginalString?.Contains("main") == true)
        {
            return true;
        }
        return base.OnBackButtonPressed();
    }
}
