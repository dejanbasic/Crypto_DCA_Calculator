using CryptoDCACalculator.Models;
using CryptoDCACalculator.Services;
using Syncfusion.Maui.Charts;

namespace CryptoDCACalculator.Views;

public partial class DCACalculatorPage : ContentPage
{
    private readonly AuthenticationService _authService;
    private readonly DatabaseService _databaseService;
    private readonly CryptoPriceService _priceService;
    private readonly DCACalculatorService _calculatorService;
    private readonly CoinMarketCapService _coinMarketCapService;
    private readonly CryptoWebSocketService _webSocketService;
    
    private List<Cryptocurrency> _cryptocurrencies = [];
    private List<DCAInvestment> _investments = [];
    private int _investmentCounter = 1;
    private readonly Dictionary<string, double> _previousPrices = [];

    public DCACalculatorPage(
        AuthenticationService authService,
        DatabaseService databaseService,
        CryptoPriceService priceService,
        DCACalculatorService calculatorService,
        CoinMarketCapService coinMarketCapService,
        CryptoWebSocketService webSocketService)
    {
        InitializeComponent();
        
        _authService = authService;
        _databaseService = databaseService;
        _priceService = priceService;
        _calculatorService = calculatorService;
        _coinMarketCapService = coinMarketCapService;
        _webSocketService = webSocketService;        
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        InitializeUI();
    }

    private async void InitializeUI()
    {
        UserLabel.Text = $"Welcome, {_authService.CurrentUser}!";

        // Load cryptocurrencies and refresh current prices
        _cryptocurrencies = await _databaseService.GetCryptocurrenciesAsync();

        // Initialize cryptocurrency price ticker
        InitializeCryptoPriceTicker();

        // Refresh current prices from API (don't block UI if this fails)
        _ = Task.Run(async () =>
        {
            try
            {
                await RefreshCurrentPricesAsync();
                // Update ticker after refreshing prices
                MainThread.BeginInvokeOnMainThread(() => InitializeCryptoPriceTicker());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to refresh current prices: {ex.Message}");
            }
        });

        // Initialize WebSocket for real-time price updates
        await InitializeWebSocketAsync();

        _investments = await _databaseService.GetDCAInvestmentsAsync();
        foreach (var investment in _investments)
        {
            // Ensure unique IDs for UI management
            investment.Id = _investmentCounter++;
            CreateInvestmentUI(investment);
        }

        if (_investments.Count == 0)
        {
            // Initialize with first investment
            AddInvestmentToUI();
        }
    }

    private async void OnCalculateClicked(object sender, EventArgs e)
    {
        if (!ValidateMultipleInvestments())
            return;

        ShowLoading(true);
        HideError();
        
        try
        {
            // Define Top 10 cryptocurrencies for competing analysis (excluding user's chosen ones)
            var top10Coins = new List<string> { "BTC", "ETH", "SOL", "XRP", "BNB", "DOGE", "TON", "TRX", "ADA", "SHIB" };
            var userChosenCoins = _investments.Select(i => i.CryptoSymbol).Distinct().ToList();
            var competingCoins = top10Coins.Where(coin => !userChosenCoins.Contains(coin)).ToList();

            if (_investments.Count == 1)
            {
                // Single investment - use detailed portfolio calculation with competing analysis
                var detailedPortfolio = await _calculatorService.CalculateDetailedPortfolioAsync(_investments, competingCoins);
                DisplayDetailedPortfolioResults(detailedPortfolio);
            }
            else
            {
                // Multiple investments - use detailed portfolio calculation with competing analysis
                var detailedPortfolio = await _calculatorService.CalculateDetailedPortfolioAsync(_investments, competingCoins);
                DisplayDetailedPortfolioResults(detailedPortfolio);
            }
        }
        catch (Exception ex)
        {
            ShowError($"Calculation error: {ex.Message}");
        }
        finally
        {
            ShowLoading(false);
        }
    }

    private async void OnSaveInvestmentClicked(object sender, EventArgs e)
    {
        if (!ValidateMultipleInvestments())
            return;

        try
        {
            var investments = await _databaseService.GetDCAInvestmentsAsync();
            foreach (var investment in investments)
            {
                await _databaseService.DeleteDCAInvestmentAsync(investment);
            }

            foreach (var investment in _investments)
            {
                await _databaseService.SaveDCAInvestmentAsync(investment);
            }
            
            await DisplayAlert("Success", "Investment configurations saved!", "OK");
        }
        catch (Exception ex)
        {
            ShowError($"Save error: {ex.Message}");
        }
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        var confirm = await DisplayAlert("Logout", "Are you sure you want to logout?", "Yes", "No");
        if (confirm)
        {
            _investmentCounter = 1;
            InvestmentsContainer.Children.Clear();

            SummaryFrame.IsVisible = false;
            HoldingsFrame.IsVisible = false;
            HoldingsContainer.Children.Clear();
            ResultsFrame.IsVisible = false;
            ResultsTableContainer.Children.Clear();
            CompetingChartFrame.IsVisible = false;
            ErrorLabel.IsVisible = false;

            if (ResultsContainer.Children.Count > 6)
                ResultsContainer.RemoveAt(5); // Remove previous breakdown if exists

            _authService.Logout();
            await Shell.Current.GoToAsync("//login");
        }
    }

    private bool ValidateMultipleInvestments()
    {
        if (_investments.Count == 0)
        {
            ShowError("Please add at least one investment configuration.");
            return false;
        }

        foreach (var investment in _investments)
        {
            if (string.IsNullOrEmpty(investment.CryptoSymbol))
            {
                ShowError("Please select a cryptocurrency for all investments.");
                return false;
            }

            if (investment.MonthlyAmount <= 0)
            {
                ShowError("Monthly investment amount must be greater than 0.");
                return false;
            }

            if (investment.InvestmentDay < 1 || investment.InvestmentDay > 31)
            {
                ShowError("Investment day must be between 1 and 31.");
                return false;
            }
        }

        return true;
    }

    private void DisplayResults(PortfolioSummary portfolio)
    {
        // Show summary
        SummaryFrame.IsVisible = true;
        TotalInvestedLabel.Text = $"€{portfolio.TotalInvested:N2}";
        CurrentValueLabel.Text = $"€{portfolio.TotalCurrentValue:N2}";
        ROILabel.Text = $"{portfolio.TotalROI:N2}%";
        ROILabel.TextColor = portfolio.TotalROI >= 0 ? Colors.Green : Colors.Red;

        // Show holdings
        DisplayHoldings(portfolio);

        // Show monthly results
        DisplayMonthlyResults(portfolio.MonthlyResults);
    }

    private void DisplayDetailedPortfolioResults(DetailedPortfolioSummary detailedPortfolio)
    {
        // Convert to regular portfolio summary for display
        var portfolio = new PortfolioSummary
        {
            TotalInvested = detailedPortfolio.TotalInvested,
            TotalCurrentValue = detailedPortfolio.TotalCurrentValue,
            MonthlyResults = []
        };

        // Aggregate holdings and values from all crypto results
        foreach (var cryptoResult in detailedPortfolio.CryptoResults)
        {
            portfolio.CryptoHoldings[cryptoResult.Symbol] = cryptoResult.TotalHoldings;
            portfolio.CryptoValues[cryptoResult.Symbol] = cryptoResult.CurrentValue;
            
            // Merge monthly results
            foreach (var monthlyResult in cryptoResult.MonthlyResults)
            {
                var existingResult = portfolio.MonthlyResults
                    .FirstOrDefault(r => r.Date.Year == monthlyResult.Date.Year && r.Date.Month == monthlyResult.Date.Month);
                
                if (existingResult != null)
                {
                    existingResult.InvestedAmount += monthlyResult.InvestedAmount;
                    existingResult.CurrentValue += monthlyResult.CurrentValue;
                }
                else
                {
                    portfolio.MonthlyResults.Add(new DCACalculationResult
                    {
                        Date = monthlyResult.Date,
                        InvestedAmount = monthlyResult.InvestedAmount,
                        CurrentValue = monthlyResult.CurrentValue,
                        CryptoAmount = monthlyResult.CryptoAmount,
                        PriceAtPurchase = 0 // Not applicable for multi-crypto
                    });
                }
            }
        }

        portfolio.MonthlyResults = [.. portfolio.MonthlyResults.OrderBy(r => r.Date)];
        DisplayResults(portfolio);

        // Add detailed breakdown
        DisplayDetailedBreakdown(detailedPortfolio.CryptoResults);
        
        // Display competing performance chart
        DisplayCompetingPerformanceChart(detailedPortfolio);
    }

    private void DisplayDetailedBreakdown(List<CryptoInvestmentResult> cryptoResults)
    {
        var breakdownFrame = new Frame
        {
            BackgroundColor = Color.FromArgb("#333333"),
            BorderColor = Color.FromArgb("#555555"),
            CornerRadius = 10,
            Padding = new Thickness(20),
            Margin = new Thickness(0, 15, 0, 0)
        };

        var layout = new VerticalStackLayout { Spacing = 10 };

        var titleLabel = new Label
        {
            Text = "Investment Breakdown",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        };
        layout.Add(titleLabel);

        foreach (var crypto in cryptoResults)
        {
            var cryptoColor = crypto.ROI >= 0 ? Color.FromArgb("#4CAF50") : Color.FromArgb("#FF6B6B");
            var cryptoLabel = new Label
            {
                Text = $"{crypto.Symbol}: {crypto.TotalHoldings:N6} coins | €{crypto.CurrentValue:N2} | ROI: {crypto.ROI:N2}%",
                FontSize = 14,
                TextColor = cryptoColor
            };
            layout.Add(cryptoLabel);
        }

        breakdownFrame.Content = layout;

        if (ResultsContainer.Children.Count > 6)
            ResultsContainer.RemoveAt(5); // Remove previous breakdown if exists
        ResultsContainer.Insert(5, breakdownFrame);
    }

    private void DisplayCompetingPerformanceChart(DetailedPortfolioSummary detailedPortfolio)
    {
        if (detailedPortfolio.CompetingResults.Count == 0)
            return;

        CompetingChartFrame.IsVisible = true;
        CompetingChart.Series.Clear();
        PerformanceSummary.Children.Clear();

        // Create performance ranking with your portfolio included
        var allPerformances = new List<(string Name, double ROI, bool IsYourPortfolio)>();
        
        // Add your portfolio
        var yourPortfolioROI = detailedPortfolio.TotalROI;
        allPerformances.Add(("Your Portfolio", yourPortfolioROI, true));
        
        // Add competing coins
        foreach (var competitor in detailedPortfolio.CompetingResults)
        {
            allPerformances.Add((competitor.Symbol, competitor.ROI, false));
        }
        
        // Sort by ROI descending
        allPerformances = [.. allPerformances.OrderByDescending(p => p.ROI)];
        
        // Find your portfolio's rank
        var yourRank = allPerformances.FindIndex(p => p.IsYourPortfolio) + 1;
        var totalCompetitors = allPerformances.Count;
        
        // Add performance ranking summary
        var rankingLabel = new Label
        {
            Text = $"Your Portfolio Ranking: #{yourRank} out of {totalCompetitors} investments",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = yourRank <= totalCompetitors / 3 ? Color.FromArgb("#4CAF50") : 
                       yourRank <= totalCompetitors * 2 / 3 ? Color.FromArgb("#FFB74D") : Color.FromArgb("#FF6B6B")
        };
        PerformanceSummary.Add(rankingLabel);
        
        // Show top 3 and your portfolio if not in top 3
        var summaryText = "Top Performers: ";
        var topThree = allPerformances.Take(3).ToList();
        for (int i = 0; i < topThree.Count; i++)
        {
            var perf = topThree[i];
            summaryText += $"#{i + 1} {perf.Name} ({perf.ROI:N1}%)";
            if (i < topThree.Count - 1) summaryText += ", ";
        }
        
        var summaryLabel = new Label
        {
            Text = summaryText,
            FontSize = 12,
            TextColor = Color.FromArgb("#B0B0B0")
        };
        PerformanceSummary.Add(summaryLabel);

        // Define colors for different series
        var colors = new[]
        {
            Color.FromArgb("#4CAF50"), // Your portfolio - green (prominent)
            Color.FromArgb("#80FF6B6B"), // Competing coins - semi-transparent red
            Color.FromArgb("#80FFB74D"), // Semi-transparent orange
            Color.FromArgb("#8064B5F6"), // Semi-transparent blue
            Color.FromArgb("#80AB47BC"), // Semi-transparent purple
            Color.FromArgb("#8026A69A"), // Semi-transparent teal
            Color.FromArgb("#80FF8A65"), // Semi-transparent deep orange
            Color.FromArgb("#8078909C"), // Semi-transparent blue grey
            Color.FromArgb("#80A5D6A7"), // Semi-transparent light green
            Color.FromArgb("#80BCAAA4"), // Semi-transparent brown
            Color.FromArgb("#80CE93D8")  // Semi-transparent light purple
        };

        // Add your portfolio performance as the main series (fully opaque)
        var portfolioSeries = new AreaSeries
        {
            Label = "Your Portfolio",
            Fill = colors[0],
            Stroke = colors[0],
            StrokeWidth = 3,
            ShowDataLabels = false,
            EnableTooltip = true
        };

        var portfolioData = new List<ChartDataPoint>();
        foreach (var (Date, PortfolioValue) in detailedPortfolio.ValueOverTime)
        {
            portfolioData.Add(new ChartDataPoint(Date, PortfolioValue));
        }
        portfolioSeries.ItemsSource = portfolioData;
        portfolioSeries.XBindingPath = "XValue";
        portfolioSeries.YBindingPath = "YValue";
        CompetingChart.Series.Add(portfolioSeries);

        // Add competing coins as semi-transparent area charts
        int colorIndex = 1;
        foreach (var competingCoin in detailedPortfolio.CompetingResults.Take(9)) // Limit to top 9 (plus your portfolio = 10 total)
        {
            var competingSeries = new AreaSeries
            {
                Label = $"{competingCoin.Symbol} (ROI: {competingCoin.ROI:N1}%)",
                Fill = colors[colorIndex % colors.Length],
                Stroke = colors[colorIndex % colors.Length],
                StrokeWidth = 1,
                ShowDataLabels = false,
                Opacity = 0.5, // Semi-transparent
                EnableTooltip = true
            };

            var competingData = new List<ChartDataPoint>();
            foreach (var (Date, Value) in competingCoin.ValueOverTime)
            {
                competingData.Add(new ChartDataPoint(Date, Value));
            }
            competingSeries.ItemsSource = competingData;
            competingSeries.XBindingPath = "XValue";
            competingSeries.YBindingPath = "YValue";
            CompetingChart.Series.Add(competingSeries);
            
            colorIndex++;
        }

        // Configure chart axes
        ChartXAxis.LabelStyle = new ChartAxisLabelStyle
        {
            TextColor = Color.FromArgb("#B0B0B0"),
            LabelFormat = "MMM yy"
        };

        ChartYAxis.LabelStyle = new ChartAxisLabelStyle
        {
            TextColor = Color.FromArgb("#B0B0B0"),
            LabelFormat = "€0.##"
        };
    }

    private void DisplayHoldings(PortfolioSummary portfolio)
    {
        HoldingsFrame.IsVisible = true;
        HoldingsContainer.Children.Clear();

        foreach (var holding in portfolio.CryptoHoldings)
        {
            var value = portfolio.CryptoValues.GetValueOrDefault(holding.Key, 0);
            var btcValue = _cryptocurrencies.FirstOrDefault(c => c.Symbol == "BTC")?.CurrentPrice ?? 1;

            var holdingFrame = new Frame
            {
                BackgroundColor = Colors.Transparent,
                BorderColor = Color.FromArgb("#404040"),
                CornerRadius = 8,
                Padding = 15,
                Margin = new Thickness(0, 0, 0, 10)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

            var symbolLabel = new Label
            {
                Text = holding.Key,
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White,
                VerticalOptions = LayoutOptions.Center
            };
            Grid.SetColumn(symbolLabel, 0);
            
            var amountLabel = new Label
            {
                Text = holding.Key == "BTC" ? $"{holding.Value:N6} {holding.Key}" : $"{value/btcValue:N6} BTC",
                FontSize = 14,
                TextColor = Color.FromArgb("#B0B0B0"),
                VerticalOptions = LayoutOptions.Center
            };
            Grid.SetColumn(amountLabel, 1);
            
            var valueLabel = new Label
            {
                Text = $"€{value:N2}",
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#4CAF50"),
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Center
            };
            Grid.SetColumn(valueLabel, 2);
            
            grid.Children.Add(symbolLabel);
            grid.Children.Add(amountLabel);
            grid.Children.Add(valueLabel);
            holdingFrame.Content = grid;
            
            HoldingsContainer.Children.Add(holdingFrame);
        }
    }

    private void DisplayMonthlyResults(List<DCACalculationResult> results)
    {
        ResultsFrame.IsVisible = true;
        ResultsTableContainer.Children.Clear();

        if (_investments.Count > 1)
        {
            CryptoAmountLabel.Text = "Crypto amount";
        }
        else
        {
            CryptoAmountLabel.Text = $"{_investments[0].CryptoSymbol} amount";
        }
        
        var displayResults = results.ToList();
        double cumulativeInvested = 0;
        double cumulativeAmount = 0;
        
        foreach (var result in displayResults)
        {
            cumulativeInvested += result.InvestedAmount;
            cumulativeAmount += result.CryptoAmount;
            double cumulativeValue = result.CurrentValue;
            var grid = new Grid
            {
                Padding = 8,
                BackgroundColor = ResultsTableContainer.Children.Count % 2 == 0
                    ? Colors.Transparent
                    : Color.FromArgb("#333333")
            };

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

            var dateLabel = new Label
            {
                Text = result.Date.ToString("MMM yyyy"),
                FontSize = 12,
                TextColor = Colors.White
            };
            Grid.SetColumn(dateLabel, 0);

            var investedLabel = new Label
            {
                Text = $"€{cumulativeInvested:N0}",
                FontSize = 12,
                TextColor = Colors.White
            };
            Grid.SetColumn(investedLabel, 1);

            var amountLabel = new Label
            {
                Text = _investments.Count > 1 ? "Mixed" : $"{cumulativeAmount:N6}",
                FontSize = 12,
                TextColor = Colors.White
            };
            Grid.SetColumn(amountLabel, 2);

            var valueLabel = new Label
            {
                Text = $"€{cumulativeValue:N0}",
                FontSize = 12,
                TextColor = Color.FromArgb("#4CAF50")
            };
            Grid.SetColumn(valueLabel, 3);

            var roi = cumulativeValue > 0 && cumulativeInvested > 0 ? (cumulativeValue - cumulativeInvested) / cumulativeInvested * 100 : 0;
            var roiLabel = new Label
            {
                Text = $"{roi:N1}%",
                FontSize = 12,
                TextColor = roi >= 0 ? Colors.Green : Colors.Red
            };
            Grid.SetColumn(roiLabel, 4);

            grid.Children.Add(dateLabel);
            grid.Children.Add(investedLabel);
            grid.Children.Add(amountLabel);
            grid.Children.Add(valueLabel);
            grid.Children.Add(roiLabel);

            ResultsTableContainer.Children.Add(grid);
        }
    }

    private void ShowLoading(bool isLoading)
    {
        LoadingIndicator.IsVisible = isLoading;
        LoadingIndicator.IsRunning = isLoading;
        CalculateButton.IsEnabled = !isLoading;
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }

    private void HideError()
    {
        ErrorLabel.IsVisible = false;
    }

    /// <summary>
    /// Refreshes current cryptocurrency prices from API and updates the database
    /// Only updates if prices haven't been updated in the last hour to respect rate limits
    /// </summary>
    private async Task RefreshCurrentPricesAsync()
    {
        try
        {
            var symbols = _cryptocurrencies.Select(c => c.Symbol).ToList();
            var currentPrices = await _coinMarketCapService.GetCurrentPricesAsync(symbols);

            if (currentPrices.Count != 0)
            {
                await _databaseService.UpdateCurrentPricesAsync(currentPrices);
            }
        }
        catch (Exception ex)
        {
            // Log error but don't fail the UI initialization
            Console.WriteLine($"Failed to refresh prices: {ex.Message}");
        }
    }

    #region WebSocket Real-time Updates

    /// <summary>
    /// Initializes WebSocket connection for real-time price updates
    /// </summary>
    private async Task InitializeWebSocketAsync()
    {
        try
        {
            // Subscribe to price updates
            _webSocketService.PriceUpdated += OnPriceUpdated;

            // Get symbols to subscribe to (limit to main cryptocurrencies for better performance)
            var symbols = _cryptocurrencies.Select(c => c.Symbol).ToList();
            await _webSocketService.ConnectAsync(symbols);

            // Update UI to show connected status
            MainThread.BeginInvokeOnMainThread(() =>
            {
                RealTimeIndicator.Fill = Color.FromArgb("#4CAF50");
                RealTimeLabel.Text = "Live prices connected";
                RealTimeLabel.TextColor = Color.FromArgb("#4CAF50");
            });

            Console.WriteLine($"WebSocket connected for {symbols.Count} cryptocurrencies");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize WebSocket: {ex.Message}");
            
            // Update UI to show disconnected status
            MainThread.BeginInvokeOnMainThread(() =>
            {
                RealTimeIndicator.Fill = Color.FromArgb("#FF6B6B");
                RealTimeLabel.Text = "Using cached prices";
                RealTimeLabel.TextColor = Color.FromArgb("#FF6B6B");
            });
        }
    }

    /// <summary>
    /// Handles real-time price updates from WebSocket
    /// </summary>
    private void OnPriceUpdated(object? sender, PriceUpdateEventArgs e)
    {
        // Update the UI on the main thread
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                // Update the cryptocurrency price in memory
                var crypto = _cryptocurrencies.FirstOrDefault(c => c.Symbol == e.Symbol);
                if (crypto != null)
                {
                    crypto.CurrentPrice = e.Price;
                    crypto.LastUpdated = e.Timestamp;
                    
                    // Update the ticker display
                    UpdateCryptoTickerItem(e.Symbol, e.Price);
                }

                // Flash the real-time indicator to show activity
                await FlashRealTimeIndicator();

                Console.WriteLine($"Real-time update: {e.Symbol} = ${e.Price:N2}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling price update: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Cleanup WebSocket connection when page is disposed
    /// </summary>
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
        try
        {
            _webSocketService.PriceUpdated -= OnPriceUpdated;
            _ = Task.Run(_webSocketService.DisconnectAsync);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error disconnecting WebSocket: {ex.Message}");
        }
    }

    /// <summary>
    /// Flashes the real-time indicator to show price update activity
    /// </summary>
    private async Task FlashRealTimeIndicator()
    {
        // Briefly change to a brighter color
        RealTimeIndicator.Fill = Color.FromArgb("#66BB6A");
        await Task.Delay(200);
        RealTimeIndicator.Fill = Color.FromArgb("#4CAF50");
    }

    #endregion

    #region Multiple Investments Management

    private void OnAddInvestmentClicked(object sender, EventArgs e)
    {
        AddInvestmentToUI();
    }

    private void AddInvestmentToUI()
    {        
        var investment = new DCAInvestment
        {
            Id = _investmentCounter++,
            CryptoSymbol = _cryptocurrencies.FirstOrDefault()?.Symbol ?? "BTC",
            StartDate = new DateTime(DateTime.Now.Year, 1, 1),
            MonthlyAmount = 200,
            InvestmentDay = 15,
            AllocationPercentage = 100,
            IsActive = true
        };

        _investments.Add(investment);
        CreateInvestmentUI(investment);
    }

    private void CreateInvestmentUI(DCAInvestment investment)
    {
        var investmentFrame = new Frame
        {
            BackgroundColor = Color.FromArgb("#2d2d2d"),
            BorderColor = Color.FromArgb("#404040"),
            CornerRadius = 8,
            Padding = new Thickness(15),
            Margin = new Thickness(0, 5, 0, 5)
        };

        var mainLayout = new VerticalStackLayout { Spacing = 10 };

        // Header with remove button
        var headerGrid = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            ]
        };

        var titleLabel = new Label
        {
            Text = $"Investment #{investment.Id}",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            VerticalOptions = LayoutOptions.Center
        };

        var removeButton = new Button
        {
            Text = "x",
            BackgroundColor = Color.FromArgb("#FF6B6B"),
            TextColor = Colors.White,
            Padding = new Thickness(0),
            CornerRadius = 5,
            WidthRequest = 30,
            HeightRequest = 30,
            FontAttributes = FontAttributes.Bold,
            FontSize = 12
        };
        removeButton.Clicked += (s, e) => RemoveInvestment(investment, investmentFrame);

        headerGrid.Add(titleLabel, 0, 0);
        headerGrid.Add(removeButton, 1, 0);
        mainLayout.Add(headerGrid);

        // Investment configuration grid
        var configGrid = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            ],
            RowDefinitions =
            [
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            ],
            RowSpacing = 10,
            ColumnSpacing = 10
        };

        // Cryptocurrency picker
        var cryptoLabel = new Label { Text = "Cryptocurrency", TextColor = Color.FromArgb("#B0B0B0"), FontSize = 12 };
        var cryptoPicker = new Picker
        {
            Title = "Select Crypto",
            TextColor = Colors.White,
            BackgroundColor = Color.FromArgb("#404040"),
            TitleColor = Color.FromArgb("#B0B0B0"),
            ItemsSource = _cryptocurrencies.Select(c => $"{c.Symbol} - {c.Name}").ToList(),
            SelectedIndex = GetCryptoIndex(investment.CryptoSymbol)
        };
        cryptoPicker.SelectedIndexChanged += (s, e) => {
            if (cryptoPicker.SelectedIndex >= 0)
                investment.CryptoSymbol = _cryptocurrencies[cryptoPicker.SelectedIndex].Symbol;
        };

        // Monthly amount
        var amountLabel = new Label { Text = "Monthly Amount (EUR)", TextColor = Color.FromArgb("#B0B0B0"), FontSize = 12 };
        var amountEntry = new Entry
        {
            Text = investment.MonthlyAmount.ToString(),
            TextColor = Colors.White,
            BackgroundColor = Color.FromArgb("#404040"),
            Keyboard = Keyboard.Numeric
        };
        amountEntry.TextChanged += (s, e) => {
            if (double.TryParse(amountEntry.Text, out double amount))
                investment.MonthlyAmount = amount;
        };

        // Start date
        var startLabel = new Label { Text = "Start Date", TextColor = Color.FromArgb("#B0B0B0"), FontSize = 12 };
        var startPicker = new DatePicker
        {
            Date = investment.StartDate,
            TextColor = Colors.White,
            BackgroundColor = Color.FromArgb("#404040")
        };
        startPicker.DateSelected += (s, e) => investment.StartDate = startPicker.Date;

        // Investment day
        var dayLabel = new Label { Text = "Investment Day", TextColor = Color.FromArgb("#B0B0B0"), FontSize = 12 };
        var dayPicker = new Picker
        {
            Title = "Day of Month",
            TextColor = Colors.White,
            BackgroundColor = Color.FromArgb("#404040"),
            TitleColor = Color.FromArgb("#B0B0B0"),
            ItemsSource = Enumerable.Range(1, 31).Select(d => d.ToString()).ToList(),
            SelectedIndex = investment.InvestmentDay - 1
        };
        dayPicker.SelectedIndexChanged += (s, e) => {
            if (dayPicker.SelectedIndex >= 0)
                investment.InvestmentDay = dayPicker.SelectedIndex + 1;
        };

        // Add controls to grid
        configGrid.Add(new VerticalStackLayout { Children = { cryptoLabel, cryptoPicker } }, 0, 0);
        configGrid.Add(new VerticalStackLayout { Children = { amountLabel, amountEntry } }, 1, 0);
        configGrid.Add(new VerticalStackLayout { Children = { startLabel, startPicker } }, 0, 1);
        configGrid.Add(new VerticalStackLayout { Children = { dayLabel, dayPicker } }, 1, 1);

        mainLayout.Add(configGrid);
        investmentFrame.Content = mainLayout;
        InvestmentsContainer.Add(investmentFrame);
    }

    private void RemoveInvestment(DCAInvestment investment, Frame investmentFrame)
    {
        _investments.Remove(investment);
        InvestmentsContainer.Remove(investmentFrame);
    }

    private int GetCryptoIndex(string symbol)
    {
        for (int i = 0; i < _cryptocurrencies.Count; i++)
        {
            if (_cryptocurrencies[i].Symbol == symbol)
                return i;
        }
        return 0;
    }

    #region Crypto Price Ticker

    /// <summary>
    /// Initializes the cryptocurrency price ticker in the header
    /// </summary>
    private void InitializeCryptoPriceTicker()
    {
        if (_cryptocurrencies?.Count > 0)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                CryptoPriceTicker.Children.Clear();
                                
                foreach (var crypto in _cryptocurrencies)
                {
                    // Store initial price for change calculation
                    _previousPrices[crypto.Symbol] = crypto.CurrentPrice;
                    
                    var tickerItem = CreateCryptoTickerItem(crypto);
                    CryptoPriceTicker.Children.Add(tickerItem);
                }
            });
        }
    }

    /// <summary>
    /// Creates a single cryptocurrency ticker item
    /// </summary>
    private Frame CreateCryptoTickerItem(Cryptocurrency crypto)
    {
        var frame = new Frame
        {
            BackgroundColor = Color.FromArgb("#3a3a3a"),
            BorderColor = Color.FromArgb("#505050"),
            CornerRadius = 10,
            Padding = new Thickness(5,10),
            HasShadow = false,
            MinimumWidthRequest = 80
        };

        var stackLayout = new VerticalStackLayout
        {
            Spacing = 5
        };

        // Symbol
        var symbolLabel = new Label
        {
            Text = crypto.Symbol,
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Center
        };

        string decimals = crypto.CurrentPrice >= 1 ? "F2" : crypto.CurrentPrice >= 0.01 ? "F4" : "F8";

        // Price
        var priceLabel = new Label
        {
            Text = crypto.CurrentPrice > 0 ? $"€{crypto.CurrentPrice.ToString(decimals)}" : "€0.00",
            FontSize = 11,
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Center
        };

        stackLayout.Children.Add(symbolLabel);
        stackLayout.Children.Add(priceLabel);

        frame.Content = stackLayout;
        
        // Store crypto data for updates
        frame.BindingContext = crypto;

        return frame;
    }

    /// <summary>
    /// Updates a specific cryptocurrency in the ticker
    /// </summary>
    private void UpdateCryptoTickerItem(string symbol, double price)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var tickerItem = CryptoPriceTicker.Children
                .OfType<Frame>()
                .FirstOrDefault(f => f.BindingContext is Cryptocurrency crypto && crypto.Symbol == symbol);

            if (tickerItem?.Content is VerticalStackLayout stackLayout && stackLayout.Children.Count == 2)
            {
                // Calculate change percent based on previous price
                var changePercent = 0.0;
                if (_previousPrices.TryGetValue(symbol, out var previousPrice) && previousPrice > 0)
                {
                    changePercent = (price - previousPrice) / previousPrice * 100;
                }
                
                var changeColor = changePercent >= 0 ? Color.FromArgb("#4CAF50") : Color.FromArgb("#F44336");

                // Update the price label
                if (stackLayout.Children[1] is Label priceLabel)
                {
                    string decimals = price >= 1 ? "F2" : price >= 0.01 ? "F4" : "F8";
                    priceLabel.Text = $"€{price.ToString(decimals)}";
                    priceLabel.TextColor = changeColor;
                }
                
                // Store the current price as previous for next update
                _previousPrices[symbol] = price;
            }
        });
    }

    #endregion

    #endregion
}
