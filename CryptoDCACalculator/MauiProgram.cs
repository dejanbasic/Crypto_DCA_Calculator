using Microsoft.Extensions.Logging;
using CryptoDCACalculator.Services;
using CryptoDCACalculator.Views;
using Syncfusion.Maui.Core.Hosting;

namespace CryptoDCACalculator;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureSyncfusionCore()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// Register Services
		builder.Services.AddSingleton<DatabaseService>();
		builder.Services.AddSingleton<AuthenticationService>();
		builder.Services.AddSingleton<CoinMarketCapService>();
		builder.Services.AddSingleton<CryptoWebSocketService>();
		builder.Services.AddSingleton<CryptoPriceService>();
		builder.Services.AddSingleton<DCACalculatorService>();

		// Register App and Pages
		builder.Services.AddSingleton<App>();
		builder.Services.AddSingleton<AppShell>();
		builder.Services.AddSingleton<LoginPage>();
		builder.Services.AddSingleton<DCACalculatorPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
