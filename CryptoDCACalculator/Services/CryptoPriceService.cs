using CryptoDCACalculator.Models;

namespace CryptoDCACalculator.Services;

/// <summary>
/// Cryptocurrency price service that fetches real data from CoinMarketCap/CoinGecko
/// Falls back to mock data if API is unavailable for development/demo purposes
/// </summary>
public class CryptoPriceService(DatabaseService databaseService, CoinMarketCapService coinMarketCapService)
{
    private readonly DatabaseService _databaseService = databaseService;
    private readonly CoinMarketCapService _coinMarketCapService = coinMarketCapService;
    private readonly Random _random = new();

    // Keep fallback prices for when API is unavailable
    private readonly Dictionary<string, List<(DateTime Date, double Price)>> _fallbackPrices = new()
    {
        ["BTC"] =
        [
            (new DateTime(2023, 1, 1), 16500),
            (new DateTime(2023, 6, 1), 27000),
            (new DateTime(2023, 12, 1), 42000),
            (new DateTime(2024, 1, 1), 43000),
            (new DateTime(2024, 6, 1), 62000),
            (new DateTime(2024, 12, 1), 95000),
            (new DateTime(2025, 8, 1), 43000)
        ],
        ["ETH"] =
        [
            (new DateTime(2023, 1, 1), 1200),
            (new DateTime(2023, 6, 1), 1900),
            (new DateTime(2023, 12, 1), 2400),
            (new DateTime(2024, 1, 1), 2600),
            (new DateTime(2024, 6, 1), 3500),
            (new DateTime(2024, 12, 1), 4000),
            (new DateTime(2025, 8, 1), 2600)
        ],
        ["SOL"] =
        [
            (new DateTime(2023, 1, 1), 10),
            (new DateTime(2023, 6, 1), 20),
            (new DateTime(2023, 12, 1), 95),
            (new DateTime(2024, 1, 1), 100),
            (new DateTime(2024, 6, 1), 140),
            (new DateTime(2024, 12, 1), 190),
            (new DateTime(2025, 8, 1), 95)
        ],
        ["XRP"] =
        [
            (new DateTime(2023, 1, 1), 0.35),
            (new DateTime(2023, 6, 1), 0.48),
            (new DateTime(2023, 12, 1), 0.62),
            (new DateTime(2024, 1, 1), 0.52),
            (new DateTime(2024, 6, 1), 0.58),
            (new DateTime(2024, 12, 1), 2.30),
            (new DateTime(2025, 8, 1), 0.52)
        ],
        ["BNB"] =
        [
            (new DateTime(2023, 1, 1), 250),
            (new DateTime(2023, 6, 1), 300),
            (new DateTime(2023, 12, 1), 350),
            (new DateTime(2024, 1, 1), 315),
            (new DateTime(2024, 6, 1), 400),
            (new DateTime(2024, 12, 1), 450),
            (new DateTime(2025, 8, 1), 260)
        ],
        ["DOGE"] =
        [
            (new DateTime(2023, 1, 1), 0.06),
            (new DateTime(2023, 6, 1), 0.07),
            (new DateTime(2023, 12, 1), 0.08),
            (new DateTime(2024, 1, 1), 0.08),
            (new DateTime(2024, 6, 1), 0.09),
            (new DateTime(2024, 12, 1), 0.10),
            (new DateTime(2025, 8, 1), 0.08)
        ],
        ["TON"] =
        [
            (new DateTime(2023, 1, 1), 0.0001),
            (new DateTime(2023, 6, 1), 0.0002),
            (new DateTime(2023, 12, 1), 0.0003),
            (new DateTime(2024, 1, 1), 0.0004),
            (new DateTime(2024, 6, 1), 0.0005),
            (new DateTime(2024, 12, 1), 0.0006),
            (new DateTime(2025, 8, 1), 0.0004)
        ],
        ["TRX"] =
        [
            (new DateTime(2023, 1, 1), 0.06),
            (new DateTime(2023, 6, 1), 0.07),
            (new DateTime(2023, 12, 1), 0.08),
            (new DateTime(2024, 1, 1), 0.08),
            (new DateTime(2024, 6, 1), 0.09),
            (new DateTime(2024, 12, 1), 0.10),
            (new DateTime(2025, 8, 1), 0.08)
        ],
        ["ADA"] =
        [
            (new DateTime(2023, 1, 1), 0.25),
            (new DateTime(2023, 6, 1), 0.30),
            (new DateTime(2023, 12, 1), 0.40),
            (new DateTime(2024, 1, 1), 0.42),
            (new DateTime(2024, 6, 1), 0.55),
            (new DateTime(2024, 12, 1), 0.60),
            (new DateTime(2025, 8, 1), 0.40)
        ],
        ["SHIB"] =
        [
            (new DateTime(2023, 1, 1), 0.00001),
            (new DateTime(2023, 6, 1), 0.000015),
            (new DateTime(2023, 12, 1), 0.00002),
            (new DateTime(2024, 1, 1), 0.00002),
            (new DateTime(2024, 6, 1), 0.000025),
            (new DateTime(2024, 12, 1), 0.00003),
            (new DateTime(2025, 8, 1), 0.00002)
        ]
    };

    /// <summary>
    /// Gets the price for a specific cryptocurrency on a specific date
    /// First tries to get real data from API, then checks local cache, finally falls back to generated prices
    /// </summary>
    public async Task<double> GetPriceAsync(string symbol, DateTime date)
    {
        // First check if we have it in our local database
        var existingPrice = await GetStoredPriceAsync(symbol, date);
        if (existingPrice.HasValue)
            return existingPrice.Value;

        double price;
        
        // If requesting current/recent price (within last week), try to get real data from API
        if (date >= DateTime.Now.AddDays(-7))
        {
            try
            {
                var currentPrices = await _coinMarketCapService.GetCurrentPricesAsync([symbol]);
                if (currentPrices.TryGetValue(symbol, out double apiPrice))
                {
                    price = apiPrice;
                }
                else
                {
                    price = GenerateFallbackPrice(symbol, date);
                }
            }
            catch
            {
                price = GenerateFallbackPrice(symbol, date);
            }
        }
        else
        {
            // For historical dates, try to get historical data or generate fallback
            price = await GetHistoricalPriceAsync(symbol, date);
        }
        
        // Store the price for future use
        await StorePriceAsync(symbol, date, price);
        return price;
    }

    /// <summary>
    /// Gets historical price for a specific date, using real API data when possible
    /// </summary>
    private async Task<double> GetHistoricalPriceAsync(string symbol, DateTime date)
    {
        try
        {
            // Try to get real historical data from CoinGecko (free API)
            var historicalData = await _coinMarketCapService.GetHistoricalPricesAsync(
                symbol, 
                date.AddDays(-1), 
                date.AddDays(1)
            );
            
            // Find the closest date
            var (Date, Price) = historicalData
                .OrderBy(p => Math.Abs((p.Date.Date - date.Date).TotalDays))
                .FirstOrDefault();
            
            if (Date != default)
            {
                return Price;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting historical price for {symbol} on {date:yyyy-MM-dd}: {ex.Message}");
        }
        
        // Fall back to generated price
        return GenerateFallbackPrice(symbol, date);
    }

    private async Task<double?> GetStoredPriceAsync(string symbol, DateTime date)
    {
        var priceHistory = await _databaseService.GetPriceHistoryAsync(symbol, date.Date);
        return priceHistory.FirstOrDefault(p => p.Date.Date == date.Date)?.Price;
    }

    private async Task StorePriceAsync(string symbol, DateTime date, double price)
    {
        var priceEntry = new CryptoPriceHistory
        {
            Symbol = symbol,
            Date = date.Date,
            Price = price
        };
        
        await _databaseService.SavePriceHistoryAsync([priceEntry]);
    }

    private double GenerateFallbackPrice(string symbol, DateTime date)
    {
        if (!_fallbackPrices.TryGetValue(symbol, out List<(DateTime Date, double Price)>? value))
        {
            // For cryptocurrencies not in our base data, generate a simple pattern
            return 1.0 + _random.NextDouble() * 10;
        }

        var basePrices = value;
        
        // Find the two closest base prices to interpolate between
        var beforePrice = basePrices.LastOrDefault(bp => bp.Date <= date);
        var (Date, Price) = basePrices.FirstOrDefault(bp => bp.Date > date);

        double basePrice;
        
        if (beforePrice.Date == default && Date != default)
        {
            basePrice = Price;
        }
        else if (beforePrice.Date != default && Date == default)
        {
            basePrice = beforePrice.Price;
        }
        else if (beforePrice.Date != default && Date != default)
        {
            // Interpolate between the two prices
            var totalDays = (Date - beforePrice.Date).TotalDays;
            var daysFromStart = (date - beforePrice.Date).TotalDays;
            var ratio = totalDays > 0 ? daysFromStart / totalDays : 0;
            
            basePrice = beforePrice.Price + (Price - beforePrice.Price) * ratio;
        }
        else
        {
            basePrice = 100; // Default fallback
        }

        // Add some realistic daily volatility (±5%)
        var volatility = 1 + ((_random.NextDouble() - 0.5) * 0.1);
        return Math.Max(0.01, basePrice * volatility);
    }

    /// <summary>
    /// Pre-generates and stores price history for a cryptocurrency over a date range
    /// This optimizes performance by batch-generating commonly needed price data
    /// </summary>
    public async Task GeneratePriceHistoryAsync(string symbol, DateTime startDate, DateTime endDate, List<int> investmentDays)
    {
        var pricesToGenerate = new List<CryptoPriceHistory>();
        var currentDate = new DateTime(startDate.Year, startDate.Month, 1);

        while (currentDate <= endDate)
        {
            foreach (var day in investmentDays)
            {
                if (day <= DateTime.DaysInMonth(currentDate.Year, currentDate.Month))
                {
                    var investmentDate = new DateTime(currentDate.Year, currentDate.Month, day);
                    if (investmentDate <= endDate && investmentDate >= startDate)
                    {
                        var existingPrice = await GetStoredPriceAsync(symbol, investmentDate);
                        if (!existingPrice.HasValue)
                        {
                            // For batch generation, use fallback prices for performance
                            pricesToGenerate.Add(new CryptoPriceHistory
                            {
                                Symbol = symbol,
                                Date = investmentDate,
                                Price = GenerateFallbackPrice(symbol, investmentDate)
                            });
                        }
                    }
                }
            }
            currentDate = currentDate.AddMonths(1);
        }

        if (pricesToGenerate.Count != 0)
        {
            await _databaseService.SavePriceHistoryAsync(pricesToGenerate);
        }
    }
}
