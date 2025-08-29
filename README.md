# Crypto DCA Calculator

A comprehensive mobile MAUI application that helps users visualize how their cryptocurrency portfolio would grow over time using Dollar Cost Averaging (DCA) investment strategy, with real-time market data and competitive analysis.

## 🌟 Key Features

### 🔐 Authentication
- Simple mock authentication system
- Demo credentials provided for easy testing
- Secure session management

### 📊 Advanced DCA Analysis
- Support for **multiple cryptocurrencies** with portfolio diversification
- **Top 10 Coins**: BTC, ETH, SOL, XRP, BNB, DOGE, TON, TRX, ADA, SHIB
- Configurable investment parameters:
  - Start date selection with calendar widget
  - Monthly investment amount per cryptocurrency
  - Investment day of the month (1st-31st)
- **Multi-asset Portfolio**: Add multiple cryptocurrencies to your DCA strategy

### 📈 Advanced Portfolio Visualization
- **Portfolio Summary**: Total invested, current value, and comprehensive ROI analysis
- **Holdings Breakdown**: Current cryptocurrency holdings with real-time values
- **Monthly Results Table**: Detailed month-by-month investment progression
- **Competing Performance Charts**: Compare your DCA strategy against investing the same amounts in other Top 10 cryptocurrencies
  - Semi-transparent area charts for easy comparison
  - Your portfolio highlighted as solid green area
  - Performance ranking and summary statistics
- **Real-time Price Ticker**: Live cryptocurrency prices with color-coded changes in the header
- Responsive dark theme UI optimized for mobile devices

### ⚡ Real-time Market Data
- **Live WebSocket Integration**: Real-time price updates via Binance WebSocket API
- **Dynamic Price Ticker**: Horizontal scrolling ticker showing live prices
- **Visual Indicators**: 
  - Green/Red color coding for price movements
  - Live connection status indicator

### 💾 Advanced Data Management
- Local SQLite database for storing:
  - Cryptocurrency price history with real-time updates
  - Multiple user investment configurations
  - Portfolio calculations and competing analysis results
- **Dual API Integration**: 
  - **CoinMarketCap API**: Professional market data with API key support
  - **CoinGecko API**: Backup data source (no registration required)
  - **Binance WebSocket**: Real-time price streaming
- **Smart Fallbacks**: Mock data when APIs unavailable
- Efficient data caching with live price updates
- **Competing Analysis Engine**: Advanced algorithms for comparative performance analysis

### 🌐 **Real Cryptocurrency Pricing**
- **CoinMarketCap API**: Professional market data (requires free API key)
- **CoinGecko API**: Backup data source (no registration required)  
- **Historical Data**: Real past prices for accurate DCA calculations
- **Rate Limiting**: Intelligent caching to respect API limits
- **Graceful Fallbacks**: Mock data ensures app always works

## 🏗️ Technical Implementation

### Architecture
- **Framework**: .NET 8 MAUI (Multi-platform App UI)
- **Database**: SQLite with sqlite-net-pcl
- **Real-time Integration**: WebSocket connections with Binance API
- **API Integration**: HTTP clients with JSON deserialization
- **UI Pattern**: MVVM-lite approach with real-time data binding
- **Navigation**: Shell-based navigation with authentication guards
- **Threading**: MainThread UI updates for WebSocket data

### Key Components

#### Services
- `DatabaseService`: Local data storage and retrieval
- `CoinMarketCapService`: Real API integration for live pricing
- `CryptoPriceService`: Intelligent price fetching with real data + fallbacks  
- `DCACalculatorService`: Portfolio calculation business logic with competing analysis
- `AuthenticationService`: User authentication management
- **`WebSocketService`**: Real-time price streaming from Binance WebSocket API

#### Models
- `Cryptocurrency`: Crypto asset information with real-time price tracking
- `DCAInvestment`: User investment configuration with multi-asset support
- `DCACalculationResult`: Monthly calculation results with competing analysis
- `PortfolioSummary`: Complete portfolio analysis with performance comparison

### Advanced Data Strategy
The app uses a sophisticated multi-layer pricing system:

#### **Real-time Data Sources:**
- **Binance WebSocket API**: Live price streaming via `wss://stream.binance.com/ws/`
  - Real-time price updates with sub-second latency
  - Automatic reconnection and error handling
  - Thread-safe UI updates using MainThread marshaling
- **CoinMarketCap Pro API**: Current prices with professional market data
- **CoinGecko API**: Historical price data and fallback current prices
- **Live Updates**: Continuous WebSocket connections with visual indicators

#### **Intelligent Fallbacks:**
- **Historical Interpolation**: Realistic price patterns based on actual market trends
- **Daily Volatility**: ±5% realistic price movements for missing data
- **WebSocket Fallback**: HTTP API polling when WebSocket unavailable
- **Optimized Storage**: Cached data with real-time updates for performance

#### **Real-time Features:**
- **Live Price Ticker**: Horizontal scrolling header with real-time WebSocket updates
- **Color-coded Changes**: Green/Red indicators for price movements
- **Performance Tracking**: Real-time percentage change calculations
- **Connection Status**: Visual indicators for WebSocket connection health

#### **Configuration:**
- Add your CoinMarketCap API key to `Services/CoinMarketCapService.cs`
- Or use CoinGecko fallback (no API key required)
- WebSocket connections work out-of-the-box with Binance public API

## Installation & Setup

### Prerequisites
- .NET 8 SDK
- Visual Studio 2022 or Visual Studio Code with C# extension
- Android SDK (for Android deployment)
- Xcode (for iOS deployment on macOS)

### Running the Application

1. **Clone the Repository**
   ```bash
   git clone [repository-url]
   cd Crypto_DCA_Calculator
   ```

2. **Restore Dependencies**
   ```bash
   dotnet restore CryptoDCACalculator/CryptoDCACalculator.csproj
   ```

3. **Build the Application**
   ```bash
   dotnet build CryptoDCACalculator/CryptoDCACalculator.csproj
   ```

4. **Run on Different Platforms**

   **Android Emulator:**
   ```bash
   dotnet run -f net8.0-android
   ```

   **iOS Simulator (macOS only):**
   ```bash
   dotnet run -f net8.0-ios
   ```

   **Mac Catalyst (macOS only):**
   ```bash
   dotnet run -f net8.0-maccatalyst
   ```

   **Windows (Windows only):**
   ```bash
   dotnet run -f net8.0-windows10.0.19041.0
   ```

### Configuration

No additional configuration is required. The app uses:
- Local SQLite database (automatically created)
- Mock authentication service
- Generated cryptocurrency price data

## 📱 Usage Guide

### 1. Login
- Use demo credentials: `demo@crypto.com` / `password123`
- Or click "Use Demo Login" for quick access

### 2. 💹 Real-time Price Monitoring
- **Live Ticker**: View real-time cryptocurrency prices in the header ticker
- **Color Indicators**: Green/Red price changes updated via WebSocket
- **Connection Status**: Visual indicators show live data connection health

### 3. Configure Investment Portfolio
- **Multi-asset Support**: Add multiple cryptocurrencies to your DCA strategy
- **Top 10 Coins**: Choose from BTC, ETH, SOL, XRP, BNB, DOGE, TON, TRX, ADA, SHIB
- Set monthly investment amount per cryptocurrency (default: €200)
- Choose start date for DCA strategy with calendar widget
- Select investment day of the month (1st-31st, default: 15th)

### 4. Advanced Performance Analysis
- Click "Calculate DCA Performance" to see comprehensive results:
  - **Portfolio Summary**: Total invested, current value, and detailed ROI analysis
  - **Holdings Breakdown**: Current cryptocurrency holdings with real-time values
  - **Monthly Results Table**: Detailed month-by-month investment progression
  - **Competing Performance Charts**: Compare your DCA strategy against investing the same amounts in other Top 10 cryptocurrencies

### 5. Real-time Data Features
- **Live Updates**: WebSocket integration provides real-time price updates
- **Performance Tracking**: Instant calculation updates as prices change
- **Visual Indicators**: Color-coded price movements and connection status

### 6. Save & Manage Configurations
- Click the 💾 save button to store your investment configuration
- Saved configurations automatically sync with real-time data
- Historical analysis combined with live market data

## 🎯 Example Use Cases

### Single Asset DCA Strategy
- **Cryptocurrency**: Bitcoin (BTC)
- **Start Date**: January 1st, 2024
- **Monthly Amount**: €200
- **Investment Day**: 15th of each month

**Results**: Shows how €200 monthly Bitcoin purchases would have performed from January 2024 to present, with live current value updates.

### Multi-Asset Portfolio Analysis
- **Primary Asset**: Bitcoin (€150/month)
- **Secondary Asset**: Ethereum (€100/month)  
- **Tertiary Asset**: Solana (€50/month)
- **Total Monthly**: €300 across diversified portfolio

**Advanced Features**: 
1. Compare actual portfolio against "what if" scenarios with other Top 10 coins
2. Real-time value tracking with WebSocket price feeds
3. Performance ranking with visual chart comparisons

### Competing Performance Analysis
The app automatically generates **competing performance charts** showing:
- Your actual DCA strategy performance (highlighted in green)
- Alternative performance if same amounts were invested in other Top 10 cryptocurrencies
- Semi-transparent overlay charts for easy comparison
- Performance ranking and statistical summary

## 💾 Database Schema

The local SQLite database contains:

### Tables
- `Cryptocurrency`: Asset information with real-time price integration (Symbol, Name, Current Price, WebSocket Updates)
- `CryptoPriceHistory`: Historical and real-time price data (Symbol, Date, Price, Source)
- `DCAInvestment`: User investment configurations with multi-asset support

### Data Optimization
- **Real-time Updates**: WebSocket price data cached for immediate access
- **Smart Caching**: Price history generated only for required dates (investment days)
- **Performance Tuning**: Cached calculations with live data integration
- **Minimal Footprint**: Optimized storage for mobile devices with live data streams

## Testing Credentials

For easy testing, use these demo accounts:
- `demo@crypto.com` / `password123`
- `investor@dca.com` / `invest123`
- `user@test.com` / `test123`

## License

This project is developed as a coding interview demonstration and is intended for educational purposes.

## Support

For technical questions or issues, please refer to the code comments which document business logic and implementation decisions.
