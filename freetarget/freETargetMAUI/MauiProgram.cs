using Microsoft.Extensions.Logging;
using Microcharts.Maui;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace freETargetMAUI;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseSkiaSharp()
			.UseMicrocharts()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddSingleton<freETarget.StorageController>(s => 
			new freETarget.StorageController(msg => System.Diagnostics.Debug.WriteLine($"DB LOG: {msg}"))
		);
		builder.Services.AddTransient<MainPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
