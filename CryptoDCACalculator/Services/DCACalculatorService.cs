using CryptoDCACalculator.Models;

namespace CryptoDCACalculator.Services;

/// <summary>
/// Service responsible for calculating DCA investment performance
/// Handles all the business logic for computing portfolio growth over time
/// </summary>
public class DCACalculatorService(CryptoPriceService priceService, DatabaseService databaseService)
{
    private readonly CryptoPriceService _priceService = priceService;
    private readonly DatabaseService _databaseService = databaseService;

    /// <summary>
    /// Calculates detailed portfolio analysis with per-crypto breakdown and competing analysis
    /// </summary>
    public async Task<DetailedPortfolioSummary> CalculateDetailedPortfolioAsync(
        List<DCAInvestment> investments, 
        List<string>? competingCoins = null)
    {
        var summary = new DetailedPortfolioSummary();
        var valueOverTime = new Dictionary<DateTime, double>();

        // Pre-generate price history
        await PreGeneratePriceHistoryAsync(investments);

        // Calculate each crypto investment separately
        foreach (var investment in investments)
        {
            var cryptoResult = await CalculateCryptoInvestmentAsync(investment);
            summary.CryptoResults.Add(cryptoResult);

            // Accumulate value over time
            foreach (var monthlyResult in cryptoResult.MonthlyResults)
            {
                var monthKey = new DateTime(monthlyResult.Date.Year, monthlyResult.Date.Month, 1);
                if (valueOverTime.ContainsKey(monthKey))
                    valueOverTime[monthKey] += monthlyResult.CurrentValue;
                else
                    valueOverTime[monthKey] = monthlyResult.CurrentValue;
            }
        }

        // Calculate competing coins if requested
        if (competingCoins != null && competingCoins.Count > 0)
        {
            summary.CompetingResults = await CalculateCompetingCoinsAsync(investments, competingCoins);
        }

        // Sort value over time
        summary.ValueOverTime = [.. valueOverTime.OrderBy(kvp => kvp.Key).Select(kvp => (kvp.Key, kvp.Value))];

        // Calculate totals
        summary.TotalInvested = summary.CryptoResults.Sum(r => r.TotalInvested);
        summary.TotalCurrentValue = summary.CryptoResults.Sum(r => r.CurrentValue);

        return summary;
    }

    /// <summary>
    /// Calculates detailed results for a single cryptocurrency investment
    /// </summary>
    public async Task<CryptoInvestmentResult> CalculateCryptoInvestmentAsync(DCAInvestment investment)
    {
        var cryptoInfo = await _databaseService.GetCryptocurrenciesAsync();
        var crypto = cryptoInfo.FirstOrDefault(c => c.Symbol == investment.CryptoSymbol);
        
        var result = new CryptoInvestmentResult
        {
            Symbol = investment.CryptoSymbol,
            Name = crypto?.Name ?? investment.CryptoSymbol
        };

        var monthlyResults = await CalculateInvestmentAsync(investment);
        result.MonthlyResults = monthlyResults;
        result.TotalInvested = monthlyResults.Sum(r => r.InvestedAmount);
        result.TotalHoldings = monthlyResults.Sum(r => r.CryptoAmount);
        
        // Calculate current value
        if (result.TotalHoldings > 0)
        {
            var currentPrice = await _priceService.GetPriceAsync(investment.CryptoSymbol, DateTime.Now);
            result.CurrentValue = result.TotalHoldings * currentPrice;
        }

        return result;
    }

    /// <summary>
    /// Calculates how competing top 10 coins would have performed with the same investment schedule
    /// </summary>
    public async Task<List<CompetingCoinResult>> CalculateCompetingCoinsAsync(
        List<DCAInvestment> originalInvestments, 
        List<string> competingCoins)
    {
        var results = new List<CompetingCoinResult>();
        var cryptoInfo = await _databaseService.GetCryptocurrenciesAsync();

        foreach (var coin in competingCoins)
        {
            var crypto = cryptoInfo.FirstOrDefault(c => c.Symbol == coin);
            var competingResult = new CompetingCoinResult
            {
                Symbol = coin,
                Name = crypto?.Name ?? coin
            };

            var valueOverTime = new Dictionary<DateTime, double>();
            double totalInvested = 0;
            double totalHoldings = 0;

            // Calculate what would happen if we invested in this competing coin
            // using the same schedule as the original investments
            foreach (var originalInvestment in originalInvestments)
            {
                var competingInvestment = new DCAInvestment
                {
                    CryptoSymbol = coin,
                    StartDate = originalInvestment.StartDate,
                    MonthlyAmount = originalInvestment.MonthlyAmount,
                    InvestmentDay = originalInvestment.InvestmentDay,
                    AllocationPercentage = originalInvestment.AllocationPercentage
                };

                var monthlyResults = await CalculateInvestmentAsync(competingInvestment);
                totalInvested += monthlyResults.Sum(r => r.InvestedAmount);
                totalHoldings += monthlyResults.Sum(r => r.CryptoAmount);

                // Accumulate value over time
                foreach (var monthlyResult in monthlyResults)
                {
                    var monthKey = new DateTime(monthlyResult.Date.Year, monthlyResult.Date.Month, 1);
                    if (valueOverTime.ContainsKey(monthKey))
                        valueOverTime[monthKey] += monthlyResult.CurrentValue;
                    else
                        valueOverTime[monthKey] = monthlyResult.CurrentValue;
                }
            }

            competingResult.TotalInvested = totalInvested;
            
            // Calculate current value
            if (totalHoldings > 0)
            {
                var currentPrice = await _priceService.GetPriceAsync(coin, DateTime.Now);
                competingResult.CurrentValue = totalHoldings * currentPrice;
            }

            competingResult.ValueOverTime = [.. valueOverTime.OrderBy(kvp => kvp.Key).Select(kvp => (kvp.Key, kvp.Value))];

            results.Add(competingResult);
        }

        return results;
    }

    /// <summary>
    /// Calculates DCA results for a single cryptocurrency investment
    /// </summary>
    private async Task<List<DCACalculationResult>> CalculateInvestmentAsync(DCAInvestment investment)
    {
        var results = new List<DCACalculationResult>();
        var currentDate = new DateTime(investment.StartDate.Year, investment.StartDate.Month, investment.InvestmentDay);
        var today = DateTime.Now.Date;

        // Ensure we don't start in the future
        if (currentDate > today)
            return results;

        // If the investment day doesn't exist in the start month, move to next valid month
        while (investment.InvestmentDay > DateTime.DaysInMonth(currentDate.Year, currentDate.Month))
        {
            currentDate = currentDate.AddMonths(1);
            currentDate = new DateTime(currentDate.Year, currentDate.Month, investment.InvestmentDay);
        }

        double totalCryptoAmount = 0;

        while (currentDate.Date <= today)
        {
            // Skip if the day doesn't exist in this month (e.g., Feb 31st)
            if (investment.InvestmentDay > DateTime.DaysInMonth(currentDate.Year, currentDate.Month))
            {
                currentDate = currentDate.AddMonths(1);
                continue;
            }

            var investmentAmount = investment.MonthlyAmount * (investment.AllocationPercentage / 100.0);
            var priceAtPurchase = await _priceService.GetPriceAsync(investment.CryptoSymbol, currentDate);
            var cryptoAmountPurchased = investmentAmount / priceAtPurchase;

            totalCryptoAmount += cryptoAmountPurchased;

            // Calculate current value
            var currentPrice = await _priceService.GetPriceAsync(investment.CryptoSymbol, DateTime.Now);
            var currentValue = totalCryptoAmount * currentPrice;

            results.Add(new DCACalculationResult
            {
                Date = currentDate,
                InvestedAmount = investmentAmount,
                CryptoAmount = cryptoAmountPurchased,
                PriceAtPurchase = priceAtPurchase,
                CurrentValue = currentValue
            });

            // Move to next month
            currentDate = currentDate.AddMonths(1);

            // Adjust if the day doesn't exist in the next month
            if (investment.InvestmentDay > DateTime.DaysInMonth(currentDate.Year, currentDate.Month))
            {
                currentDate = new DateTime(currentDate.Year, currentDate.Month, DateTime.DaysInMonth(currentDate.Year, currentDate.Month));
            }
            else
            {
                currentDate = new DateTime(currentDate.Year, currentDate.Month, investment.InvestmentDay);
            }
        }

        return results;
    }

    /// <summary>
    /// Pre-generates price history for all required dates to optimize performance
    /// </summary>
    private async Task PreGeneratePriceHistoryAsync(List<DCAInvestment> investments)
    {
        var investmentDays = investments.Select(i => i.InvestmentDay).Distinct().ToList();
        
        foreach (var investment in investments)
        {
            await _priceService.GeneratePriceHistoryAsync(
                investment.CryptoSymbol, 
                investment.StartDate, 
                DateTime.Now,
                investmentDays);
        }
    }
}
