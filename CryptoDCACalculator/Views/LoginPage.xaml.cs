using CryptoDCACalculator.Services;

namespace CryptoDCACalculator.Views;

public partial class LoginPage : ContentPage
{
    private readonly AuthenticationService _authService;
    private readonly DatabaseService _databaseService;

    public LoginPage(AuthenticationService authService, DatabaseService databaseService)
    {
        InitializeComponent();
        _authService = authService;
        _databaseService = databaseService;
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EmailEntry.Text) || string.IsNullOrWhiteSpace(PasswordEntry.Text))
        {
            ShowError("Please enter both email and password");
            return;
        }

        ShowLoading(true);
        
        try
        {
            var success = await _authService.LoginAsync(EmailEntry.Text.Trim(), PasswordEntry.Text);
            
            if (success)
            {
                await Shell.Current.GoToAsync("//main");
            }
            else
            {
                ShowError("Invalid email or password");
            }
        }
        catch (Exception ex)
        {
            ShowError($"Login error: {ex.Message}");
        }
        finally
        {
            ShowLoading(false);
        }
    }

    private async void OnDemoLoginClicked(object sender, EventArgs e)
    {
        EmailEntry.Text = "demo@crypto.com";
        PasswordEntry.Text = "password123";
        
        ShowLoading(true);
        
        try
        {
            var success = await _authService.LoginAsync("demo@crypto.com", "password123");
            if (success)
            {
                await Shell.Current.GoToAsync("//main");
            }
        }
        catch (Exception ex)
        {
            ShowError($"Demo login error: {ex.Message}");
        }
        finally
        {
            ShowLoading(false);
        }
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }

    private void ShowLoading(bool isLoading)
    {
        LoadingIndicator.IsVisible = isLoading;
        LoadingIndicator.IsRunning = isLoading;
        LoginButton.IsEnabled = !isLoading;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        // Clear error message and reset form
        ErrorLabel.IsVisible = false;
        ShowLoading(false);
        
        // Clear previous login if user logged out
        if (!_authService.IsLoggedIn)
        {
            EmailEntry.Text = string.Empty;
            PasswordEntry.Text = string.Empty;
        }
    }
}
