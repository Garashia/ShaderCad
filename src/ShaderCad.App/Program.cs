using System;
using System;
using Avalonia;
using Serilog;

namespace ShaderCad.App;

internal class Program
{
    static void Main(string[] args)
    {
        // 1. Serilog のグローバルロガー初期化
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File("logs/shadercad-.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        // 2. グローバル例外ハンドラの設定（クラッシュ時の崩壊防止）
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                Log.Fatal(ex, "予期せぬ致命的なエラーが発生し、アプリケーションがクラッシュしました。");
            }
            else
            {
                Log.Fatal("未知の致命的なエラーが発生しました: {ExceptionObject}", e.ExceptionObject);
            }
            Log.CloseAndFlush();
        };

        try
        {
            Log.Information("ShaderCad アプリケーションを起動しています...");
            
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "アプリケーションのメインループで致命的なエラーが発生しました。");
        }
        finally
        {
            Log.Information("ShaderCad アプリケーションを終了します。");
            Log.CloseAndFlush();
        }
    }

    // Avalonia configuration
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
