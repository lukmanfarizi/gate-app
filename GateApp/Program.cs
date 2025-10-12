using System.Windows.Forms;
using GateApp.Forms;
using GateApp.Services;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace GateApp;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var configuration = BuildConfiguration();
        using var logService = LogService.Create(configuration);
        var apiService = new ApiService(configuration, logService.Logger);
        var cameraService = new CameraService(configuration, logService.Logger);
        var gateControllerClient = new GateControllerClient(configuration, logService.Logger);
        var printService = new PrintService(configuration, logService.Logger);
        var scannerService = new ScannerService(configuration, logService.Logger);

        Application.ApplicationExit += (_, _) =>
        {
            cameraService.Dispose();
            apiService.Dispose();
            gateControllerClient.Dispose();
            printService.Dispose();
            scannerService.Dispose();
        };

        Application.Run(new MainForm(configuration,
                                     apiService,
                                     cameraService,
                                     gateControllerClient,
                                     printService,
                                     scannerService,
                                     logService));
    }

    private static IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
    }
}
