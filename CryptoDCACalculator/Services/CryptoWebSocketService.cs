using System.Net.WebSockets;
using System.Text;
using System.Globalization;
using Newtonsoft.Json;

namespace CryptoDCACalculator.Services;

/// <summary>
/// WebSocket service for real-time cryptocurrency price updates
/// Connects to Binance WebSocket API for live market data
/// </summary>
public class CryptoWebSocketService(DatabaseService databaseService) : IDisposable
{
    private readonly DatabaseService _databaseService = databaseService;
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly List<string> _subscribedSymbols = [];
    private bool _isConnected;

    // Binance WebSocket base URL (without /ws since we'll add /stream for combined streams)
    private const string BinanceWebSocketUrl = "wss://stream.binance.com:9443";

    public event EventHandler<PriceUpdateEventArgs>? PriceUpdated;

    /// <summary>
    /// Connects to the WebSocket and subscribes to price updates for specified cryptocurrencies
    /// </summary>
    public async Task ConnectAsync(List<string> cryptoSymbols)
    {
        try
        {
            Console.WriteLine($"WebSocket ConnectAsync called with {cryptoSymbols.Count} symbols: {string.Join(", ", cryptoSymbols)}");

            if (_isConnected && _webSocket?.State == WebSocketState.Open)
            {
                await DisconnectAsync();
            }

            _cancellationTokenSource = new CancellationTokenSource();
            _webSocket = new ClientWebSocket();

            // Convert symbols to Binance format (e.g., BTC -> BTCUSDT)
            var binanceSymbols = cryptoSymbols
                .Where(symbol => !symbol.Equals("USDT", StringComparison.OrdinalIgnoreCase))
                .Select(symbol => $"{symbol.ToLower()}usdt@ticker")
                .ToList();

            Console.WriteLine($"Converted to Binance symbols: {string.Join(", ", binanceSymbols)}");

            if (binanceSymbols.Count == 0)
            {
                Console.WriteLine("ERROR: No valid symbols to subscribe to!");
                Console.WriteLine("Using fallback symbols for testing: BTC, ETH, ADA");
                
                // Fallback to some known working symbols for testing
                binanceSymbols = ["btcusdt@ticker", "ethusdt@ticker", "adausdt@ticker"];
                cryptoSymbols = ["BTC", "ETH", "ADA"];
            }

            _subscribedSymbols.Clear();
            _subscribedSymbols.AddRange(cryptoSymbols);

            // Use the combined stream endpoint for multiple streams
            var streamUrl = $"{BinanceWebSocketUrl}/stream?streams={string.Join("/", binanceSymbols)}";
            Console.WriteLine($"Final WebSocket URL: {streamUrl}");

            // Test if it's a URL length issue
            if (streamUrl.Length > 2000)
            {
                Console.WriteLine($"WARNING: URL might be too long ({streamUrl.Length} chars)");
                // Limit to first 3 symbols if URL is too long
                binanceSymbols = binanceSymbols.Take(3).ToList();
                streamUrl = $"{BinanceWebSocketUrl}/stream?streams={string.Join("/", binanceSymbols)}";
                Console.WriteLine($"Shortened URL: {streamUrl}");
            }

            await _webSocket.ConnectAsync(new Uri(streamUrl), _cancellationTokenSource.Token);
            _isConnected = true;

            Console.WriteLine($"✅ Connected to Binance WebSocket with {binanceSymbols.Count} streams");

            // Start listening for messages
            _ = Task.Run(() => ListenForMessagesAsync(_cancellationTokenSource.Token));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ WebSocket connection failed: {ex.Message}");
            _isConnected = false;
            throw;
        }
    }

    /// <summary>
    /// Disconnects from the WebSocket
    /// </summary>
    public async Task DisconnectAsync()
    {
        _isConnected = false;
        _cancellationTokenSource?.Cancel();

        if (_webSocket?.State == WebSocketState.Open)
        {
            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnecting", CancellationToken.None);
        }

        _webSocket?.Dispose();
        _cancellationTokenSource?.Dispose();
    }

    /// <summary>
    /// Listens for incoming WebSocket messages and processes price updates
    /// </summary>
    private async Task ListenForMessagesAsync(CancellationToken cancellationToken)
    {
        if (_webSocket == null) return;

        var buffer = new byte[4096];

        try
        {
            while (_webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await ProcessPriceUpdateAsync(message);
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
        catch (WebSocketException ex)
        {
            Console.WriteLine($"WebSocket error: {ex.Message}");
            // Try to reconnect after a delay
            await Task.Delay(5000, cancellationToken);
            if (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await ConnectAsync(_subscribedSymbols);
                }
                catch
                {
                    // Reconnection failed, continue with offline mode
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected WebSocket error: {ex.Message}");
        }
    }

    /// <summary>
    /// Processes incoming price update messages from Binance WebSocket
    /// </summary>
    private async Task ProcessPriceUpdateAsync(string message)
    {
        try
        {
            // With the combined stream endpoint, all messages should be in stream format
            if (message.StartsWith("{\"stream\":"))
            {
                // Parse manually to ensure we get the right fields
                var jObject = Newtonsoft.Json.Linq.JObject.Parse(message);
                var stream = jObject["stream"]?.ToString();

                if (jObject["data"] is Newtonsoft.Json.Linq.JObject dataObject)
                {
                    // Extract fields manually to avoid any deserialization issues
                    var symbol = dataObject["s"]?.ToString(); // lowercase 's'
                    var lastPrice = dataObject["c"]?.ToString(); // lowercase 'c' for close price
                    var priceChangePercent = dataObject["P"]?.ToString(); // uppercase 'P'

                    if (!string.IsNullOrEmpty(symbol) && !string.IsNullOrEmpty(lastPrice))
                    {
                        // Create ticker data manually
                        var tickerData = new BinanceTickerData
                        {
                            Symbol = symbol,
                            LastPrice = lastPrice,
                            PriceChangePercent = priceChangePercent
                        };

                        await ProcessTickerData(tickerData);
                    }
                    else
                    {
                        Console.WriteLine($"Missing essential data - symbol: {symbol}, lastPrice: {lastPrice}");
                    }
                }
                else
                {
                    Console.WriteLine("No data object found in stream message");
                }
            }
            else
            {
                Console.WriteLine($"Unexpected message format (not stream): {message[..Math.Min(100, message.Length)]}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing WebSocket message: {ex.Message}");
            Console.WriteLine($"Message preview: {message[..Math.Min(200, message.Length)]}");
        }
    }

    /// <summary>
    /// Processes ticker data and updates prices
    /// </summary>
    private async Task ProcessTickerData(BinanceTickerData tickerData)
    {
        try
        {
            Console.WriteLine($"Processing: {tickerData.Symbol} = ${tickerData.LastPrice}");

            if (string.IsNullOrEmpty(tickerData.Symbol) || string.IsNullOrEmpty(tickerData.LastPrice))
            {
                Console.WriteLine("Missing symbol or price data, skipping");
                return;
            }

            // Get the coin symbol (remove USDT suffix)
            var coinSymbol = tickerData.Symbol.Replace("USDT", "").ToUpper();

            // Parse price with detailed logging
            if (!double.TryParse(tickerData.LastPrice, NumberStyles.Float, CultureInfo.InvariantCulture, out double price))
            {
                Console.WriteLine($"Failed to parse price: '{tickerData.LastPrice}' for {coinSymbol}");
                return;
            }

            // Sanity check - crypto prices shouldn't exceed $1M per coin
            if (price > 1_000_000 || price < 0)
            {
                Console.WriteLine($"SUSPICIOUS PRICE: {coinSymbol} = ${price:F2} - This seems incorrect!");
                
                // Let's try parsing with different culture settings
                if (double.TryParse(tickerData.LastPrice, NumberStyles.Float, CultureInfo.CurrentCulture, out double priceAlt))
                {
                    Console.WriteLine($"Alternative parsing with current culture: {priceAlt:F8}");
                }
                
                // Check if there are any unusual characters
                Console.WriteLine($"Price string length: {tickerData.LastPrice.Length}");
                Console.WriteLine($"Price string bytes: {string.Join(",", Encoding.UTF8.GetBytes(tickerData.LastPrice))}");
                
                return; // Skip suspicious prices
            }

            // Update the price in database
            await _databaseService.UpdateCurrentPricesAsync(new Dictionary<string, double> { { coinSymbol, price } });
            Console.WriteLine($"✓ Real-time update: {coinSymbol} = ${price:F2}");

            // Notify UI of price update
            PriceUpdated?.Invoke(this, new PriceUpdateEventArgs(coinSymbol, price, DateTime.Now));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing ticker data: {ex.Message}");
            Console.WriteLine($"Ticker data: Symbol={tickerData.Symbol}, LastPrice={tickerData.LastPrice}");
        }
    }

    public void Dispose()
    {
        DisconnectAsync().Wait();
        _webSocket?.Dispose();
        _cancellationTokenSource?.Dispose();
    }
}

/// <summary>
/// Event arguments for price update notifications
/// </summary>
public class PriceUpdateEventArgs(string symbol, double price, DateTime timestamp) : EventArgs
{
    public string Symbol { get; } = symbol;
    public double Price { get; } = price;
    public DateTime Timestamp { get; } = timestamp;
}

/// <summary>
/// Binance ticker data format
/// </summary>
internal class BinanceTickerData
{
    [JsonProperty("s")]
    public string? Symbol { get; set; }

    [JsonProperty("c")]
    public string? LastPrice { get; set; }

    [JsonProperty("P")]
    public string? PriceChangePercent { get; set; }

    [JsonProperty("o")]
    public string? OpenPrice { get; set; }

    [JsonProperty("h")]
    public string? HighPrice { get; set; }

    [JsonProperty("l")]
    public string? LowPrice { get; set; }

    [JsonProperty("v")]
    public string? Volume { get; set; }
}
