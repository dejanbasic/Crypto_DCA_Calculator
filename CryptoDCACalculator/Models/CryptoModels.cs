using SQLite;

namespace CryptoDCACalculator.Models;

/// <summary>
/// Represents a cryptocurrency with basic information
/// </summary>
public class Cryptocurrency
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double CurrentPrice { get; set; }
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Historical price data for cryptocurrencies stored in local database
/// </summary>
public class CryptoPriceHistory
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    public string Symbol { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public double Price { get; set; }
}

/// <summary>
/// Represents a user's DCA investment configuration
/// </summary>
public class DCAInvestment
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string CryptoSymbol { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public double MonthlyAmount { get; set; }
    public int InvestmentDay { get; set; } // Day of month to invest (e.g., 15th, 20th, 25th)
    public double AllocationPercentage { get; set; } = 100; // For portfolio allocation
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Calculated result for a single month's DCA investment
/// </summary>
public class DCACalculationResult
{
    public DateTime Date { get; set; }
    public double InvestedAmount { get; set; }
    public double CryptoAmount { get; set; }
    public double PriceAtPurchase { get; set; }
    public double CurrentValue { get; set; }
    public double ROI => CurrentValue > 0 && InvestedAmount > 0 ? (CurrentValue - InvestedAmount) / InvestedAmount * 100 : 0;
}

/// <summary>
/// Portfolio summary containing all investments and calculations
/// </summary>
public class PortfolioSummary
{
    public double TotalInvested { get; set; }
    public double TotalCurrentValue { get; set; }
    public double TotalROI => TotalCurrentValue > 0 && TotalInvested > 0 ? (TotalCurrentValue - TotalInvested) / TotalInvested * 100 : 0;
    public List<DCACalculationResult> MonthlyResults { get; set; } = [];
    public Dictionary<string, double> CryptoHoldings { get; set; } = []; // Symbol -> Amount
    public Dictionary<string, double> CryptoValues { get; set; } = []; // Symbol -> Current Value
}

/// <summary>
/// Enhanced calculation result for single crypto investment over time
/// </summary>
public class CryptoInvestmentResult
{
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double TotalInvested { get; set; }
    public double TotalHoldings { get; set; } // Amount of crypto owned
    public double CurrentValue { get; set; }
    public double ROI => CurrentValue > 0 && TotalInvested > 0 ? (CurrentValue - TotalInvested) / TotalInvested * 100 : 0;
    public List<DCACalculationResult> MonthlyResults { get; set; } = [];
}

/// <summary>
/// Competing coin performance data for comparison charts
/// </summary>
public class CompetingCoinResult
{
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double TotalInvested { get; set; }
    public double CurrentValue { get; set; }
    public double ROI => CurrentValue > 0 && TotalInvested > 0 ? (CurrentValue - TotalInvested) / TotalInvested * 100 : 0;
    public List<(DateTime Date, double Value)> ValueOverTime { get; set; } = [];
}

/// <summary>
/// Enhanced portfolio summary with detailed per-crypto breakdown and competing analysis
/// </summary>
public class DetailedPortfolioSummary
{
    public double TotalInvested { get; set; }
    public double TotalCurrentValue { get; set; }
    public double TotalROI => TotalCurrentValue > 0 && TotalInvested > 0 ? (TotalCurrentValue - TotalInvested) / TotalInvested * 100 : 0;
    
    public List<CryptoInvestmentResult> CryptoResults { get; set; } = [];
    public List<CompetingCoinResult> CompetingResults { get; set; } = [];
    public List<(DateTime Date, double PortfolioValue)> ValueOverTime { get; set; } = [];
}

/// <summary>
/// Chart data point for Syncfusion charts
/// </summary>
public class ChartDataPoint(DateTime x, double y)
{
    public DateTime XValue { get; set; } = x;
    public double YValue { get; set; } = y;
}
