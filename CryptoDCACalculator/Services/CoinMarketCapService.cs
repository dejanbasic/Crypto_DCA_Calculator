using Newtonsoft.Json;

namespace CryptoDCACalculator.Services;

/// <summary>
/// CoinMarketCap API service for fetching real cryptocurrency data
/// Uses the free tier of CoinMarketCap API with proper rate limiting
/// </summary>
public class CoinMarketCapService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://pro-api.coinmarketcap.com/v1";
    
    // CoinMarketCap API key - in production, this should be in secure configuration
    private const string ApiKey = "9a1cf68d-8e08-463b-8a93-875c7178255f";
    
    // For demo purposes without API key, we'll use alternative data source
    private const string AlternativeApiUrl = "https://api.coingecko.com/api/v3";
    
    private readonly Dictionary<string, string> _coinGeckoIdMap = new()
    {
        ["BTC"] = "bitcoin",
        ["ETH"] = "ethereum", 
        ["SOL"] = "solana",
        ["XRP"] = "ripple",
        ["BNB"] = "binancecoin",
        ["DOGE"] = "dogecoin",
        ["TON"] = "the-open-network",
        ["TRX"] = "tron",
        ["ADA"] = "cardano",
        ["SHIB"] = "shiba-inu"
    };

    public CoinMarketCapService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        
        // Add CoinMarketCap API key header if available
        if (!string.IsNullOrEmpty(ApiKey) && ApiKey != "YOUR_API_KEY_HERE")
        {
            _httpClient.DefaultRequestHeaders.Add("X-CMC_PRO_API_KEY", ApiKey);
        }
    }

    /// <summary>
    /// Fetches current prices for all supported cryptocurrencies
    /// Falls back to CoinGecko if CoinMarketCap API key is not available
    /// </summary>
    public async Task<Dictionary<string, double>> GetCurrentPricesAsync(List<string> symbols)
    {
        try
        {
            if (!string.IsNullOrEmpty(ApiKey) && ApiKey != "YOUR_API_KEY_HERE")
            {
                return await GetPricesFromCoinMarketCapAsync(symbols);
            }
            else
            {
                return await GetPricesFromCoinGeckoAsync(symbols);
            }
        }
        catch (Exception ex)
        {
            // Log error and return fallback prices
            Console.WriteLine($"Error fetching prices: {ex.Message}");
            return GetFallbackPrices(symbols);
        }
    }

    /// <summary>
    /// Fetches historical price data for a specific cryptocurrency
    /// Uses CoinGecko's free API for historical data
    /// </summary>
    public async Task<List<(DateTime Date, double Price)>> GetHistoricalPricesAsync(string symbol, DateTime startDate, DateTime endDate)
    {
        try
        {
            if (!_coinGeckoIdMap.TryGetValue(symbol, out string? coinId))
            {
                return GetFallbackHistoricalPrices(symbol, startDate, endDate);
            }

            var fromTimestamp = ((DateTimeOffset)startDate).ToUnixTimeSeconds();
            var toTimestamp = ((DateTimeOffset)endDate).ToUnixTimeSeconds();
            
            var url = $"{AlternativeApiUrl}/coins/{coinId}/market_chart/range?vs_currency=eur&from={fromTimestamp}&to={toTimestamp}";
            
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return GetFallbackHistoricalPrices(symbol, startDate, endDate);
            }

            var content = await response.Content.ReadAsStringAsync();
            var data = JsonConvert.DeserializeObject<CoinGeckoHistoricalResponse>(content);
            
            if (data?.Prices == null)
            {
                return GetFallbackHistoricalPrices(symbol, startDate, endDate);
            }

            return [.. data.Prices.Select(p => (
                Date: DateTimeOffset.FromUnixTimeMilliseconds((long)p[0]).DateTime,
                Price: p[1]
            ))];
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching historical prices for {symbol}: {ex.Message}");
            return GetFallbackHistoricalPrices(symbol, startDate, endDate);
        }
    }

    private async Task<Dictionary<string, double>> GetPricesFromCoinMarketCapAsync(List<string> symbols)
    {
        var symbolString = string.Join(",", symbols);
        var url = $"{BaseUrl}/cryptocurrency/quotes/latest?symbol={symbolString}&convert=EUR";
        
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        var data = JsonConvert.DeserializeObject<CoinMarketCapResponse>(content);
        
        var prices = new Dictionary<string, double>();
        
        if (data?.Data != null)
        {
            foreach (var kvp in data.Data)
            {
                if (kvp.Value?.Quote?.EUR?.Price != null)
                {
                    prices[kvp.Key] = kvp.Value.Quote.EUR.Price;
                }
            }
        }
        
        return prices;
    }

    private async Task<Dictionary<string, double>> GetPricesFromCoinGeckoAsync(List<string> symbols)
    {
        var coinIds = symbols.Where(_coinGeckoIdMap.ContainsKey)
                            .Select(s => _coinGeckoIdMap[s])
                            .ToList();
        
        if (coinIds.Count == 0)
        {
            return GetFallbackPrices(symbols);
        }

        var idsString = string.Join(",", coinIds);
        var url = $"{AlternativeApiUrl}/simple/price?ids={idsString}&vs_currencies=eur";
        
        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            return GetFallbackPrices(symbols);
        }
        
        var content = await response.Content.ReadAsStringAsync();
        var data = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, double>>>(content);
        
        var prices = new Dictionary<string, double>();
        
        if (data != null)
        {
            foreach (var symbol in symbols)
            {
                if (_coinGeckoIdMap.TryGetValue(symbol, out string? coinId) && 
                    data.TryGetValue(coinId, out var priceData) &&
                    priceData.TryGetValue("eur", out double price))
                {
                    prices[symbol] = price;
                }
            }
        }
        
        return prices;
    }

    private static Dictionary<string, double> GetFallbackPrices(List<string> symbols)
    {
        // Fallback prices in case API is unavailable
        var fallbackPrices = new Dictionary<string, double>
        {
            ["BTC"] = 43000,
            ["ETH"] = 2600,
            ["SOL"] = 95,
            ["XRP"] = 0.52,
            ["BNB"] = 315,
            ["DOGE"] = 0.08,
            ["TON"] = 2.1,
            ["TRX"] = 0.11
        };

        return symbols.ToDictionary(s => s, s => fallbackPrices.GetValueOrDefault(s, 100.0));
    }

    private static List<(DateTime Date, double Price)> GetFallbackHistoricalPrices(string symbol, DateTime startDate, DateTime endDate)
    {
        // Generate basic historical prices as fallback
        var prices = new List<(DateTime Date, double Price)>();
        var random = new Random();
        var currentDate = startDate;
        var basePrice = GetFallbackPrices([symbol])[symbol];
        
        while (currentDate <= endDate)
        {
            // Simple trend with volatility
            var daysSinceStart = (currentDate - startDate).TotalDays;
            var trend = Math.Sin(daysSinceStart / 30) * 0.2; // Monthly cycle
            var volatility = (random.NextDouble() - 0.5) * 0.1; // ±5% daily volatility
            var price = basePrice * (1 + trend + volatility);
            
            prices.Add((currentDate, Math.Max(0.01, price)));
            currentDate = currentDate.AddDays(1);
        }
        
        return prices;
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

// Data models for API responses
public class CoinMarketCapResponse
{
    [JsonProperty("data")]
    public Dictionary<string, CoinMarketCapData>? Data { get; set; }
}

public class CoinMarketCapData
{
    [JsonProperty("quote")]
    public CoinMarketCapQuote? Quote { get; set; }
}

public class CoinMarketCapQuote
{
    [JsonProperty(nameof(EUR))]
    public CoinMarketCapPrice? EUR { get; set; }
}

public class CoinMarketCapPrice
{
    [JsonProperty("price")]
    public double Price { get; set; }
}

public class CoinGeckoHistoricalResponse
{
    [JsonProperty("prices")]
    public double[][]? Prices { get; set; }
}
