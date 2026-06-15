using LinherKeyboardWedge.App.Configuration;
using LinherKeyboardWedge.App.Logging;

namespace LinherKeyboardWedge.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        using var logger = new DailyFileLogger(AppPaths.LogDirectory);
        Application.ThreadException += (_, e) => logger.LogError("UI", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception exception)
            {
                logger.LogError("APP", exception);
            }
        };

        var configService = new ConfigService(AppPaths.ConfigPath, logger);
        var settings = configService.Load();

        Application.Run(new MainForm(settings, configService, logger));
    }
}
