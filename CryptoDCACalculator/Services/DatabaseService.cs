using SQLite;
using CryptoDCACalculator.Models;

namespace CryptoDCACalculator.Services;

/// <summary>
/// Local database service for storing cryptocurrency prices and user investments
/// Uses SQLite for fast local storage with minimal setup
/// </summary>
public class DatabaseService
{
    private readonly SQLiteAsyncConnection _database;

    public DatabaseService()
    {
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "CryptoDCA.db");
        _database = new SQLiteAsyncConnection(dbPath);
        InitializeDatabaseAsync();
    }

    private async void InitializeDatabaseAsync()
    {
        await _database.CreateTableAsync<Cryptocurrency>();
        await _database.CreateTableAsync<CryptoPriceHistory>();
        await _database.CreateTableAsync<DCAInvestment>();
        
        // Seed with popular cryptocurrencies
        await SeedCryptocurrenciesAsync();
    }

    private async Task SeedCryptocurrenciesAsync()
    {
        var existingCryptos = await _database.Table<Cryptocurrency>().CountAsync();
        if (existingCryptos > 0) return;

        var cryptos = new List<Cryptocurrency>
        {
            new() { Symbol = "BTC", Name = "Bitcoin", CurrentPrice = 43000, LastUpdated = DateTime.UtcNow },
            new() { Symbol = "ETH", Name = "Ethereum", CurrentPrice = 2600, LastUpdated = DateTime.UtcNow },
            new() { Symbol = "SOL", Name = "Solana", CurrentPrice = 95, LastUpdated = DateTime.UtcNow },
            new() { Symbol = "XRP", Name = "Ripple", CurrentPrice = 0.52, LastUpdated = DateTime.UtcNow },
            new() { Symbol = "BNB", Name = "Binance Coin", CurrentPrice = 315, LastUpdated = DateTime.UtcNow },
            new() { Symbol = "DOGE", Name = "Dogecoin", CurrentPrice = 0.08, LastUpdated = DateTime.UtcNow },
            new() { Symbol = "TON", Name = "Toncoin", CurrentPrice = 0.0001, LastUpdated = DateTime.UtcNow },
            new() { Symbol = "TRX", Name = "Tron", CurrentPrice = 0.06, LastUpdated = DateTime.UtcNow },
            new() { Symbol = "ADA", Name = "Cardano", CurrentPrice = 1.5, LastUpdated = DateTime.UtcNow },
            new() { Symbol = "SHIB", Name = "Shiba Inu", CurrentPrice = 0.00002, LastUpdated = DateTime.UtcNow }
        };

        await _database.InsertAllAsync(cryptos);
    }

    public async Task<List<Cryptocurrency>> GetCryptocurrenciesAsync()
    {
        return await _database.Table<Cryptocurrency>().ToListAsync();
    }

    public async Task SavePriceHistoryAsync(List<CryptoPriceHistory> priceHistory)
    {
        await _database.InsertAllAsync(priceHistory);
    }

    public async Task<List<CryptoPriceHistory>> GetPriceHistoryAsync(string symbol, DateTime startDate)
    {
        return await _database.Table<CryptoPriceHistory>()
            .Where(p => p.Symbol == symbol && p.Date >= startDate)
            .OrderBy(p => p.Date)
            .ToListAsync();
    }

    public async Task SaveDCAInvestmentAsync(DCAInvestment investment)
    {
        await _database.InsertAsync(investment);
    }

    public async Task<List<DCAInvestment>> GetDCAInvestmentsAsync()
    {
        return await _database.Table<DCAInvestment>()
            .Where(i => i.IsActive)
            .ToListAsync();
    }

    public async Task DeleteDCAInvestmentAsync(DCAInvestment investment)
    {
        await _database.DeleteAsync(investment);
    }

    /// <summary>
    /// Updates current prices for all cryptocurrencies
    /// This method can be called periodically to refresh prices from API
    /// </summary>
    public async Task UpdateCurrentPricesAsync(Dictionary<string, double> currentPrices)
    {
        var cryptos = await _database.Table<Cryptocurrency>().ToListAsync();
        var updated = false;

        foreach (var crypto in cryptos)
        {
            if (currentPrices.TryGetValue(crypto.Symbol, out double newPrice))
            {
                crypto.CurrentPrice = newPrice;
                crypto.LastUpdated = DateTime.UtcNow;
                await _database.UpdateAsync(crypto);
                updated = true;
            }
        }

        if (updated)
        {
            Console.WriteLine($"Updated current prices for cryptocurrencies at {DateTime.UtcNow}");
        }
    }
}
