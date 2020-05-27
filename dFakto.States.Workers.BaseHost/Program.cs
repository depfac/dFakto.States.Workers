using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.WindowsServices;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace dFakto.States.Workers.TestsHost
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            AppContext.SetSwitch("System.Net.Http.UseSocketsHttpHandler", false);

            bool isService = !(Debugger.IsAttached || (args != null && args.Contains("--console")));
            if (isService)
            {
                string pathToExe = Process.GetCurrentProcess().MainModule?.FileName;
                string pathToContentRoot = Path.GetDirectoryName(pathToExe);
                Directory.SetCurrentDirectory(pathToContentRoot);
            }

            string environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
            IConfigurationBuilder configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", true)
                .AddJsonFile($"appsettings.{environment}.json", true)
                .AddEnvironmentVariables();
            if (args != null)
            {
                configBuilder.AddCommandLine(args.Where(arg => arg != "--console").ToArray());
            }
            IConfiguration config = configBuilder.Build();

            var logConfig = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .ReadFrom.Configuration(config)
                .Enrich.FromLogContext();
            if(!isService)
            {
                logConfig.WriteTo.Console(new MessageTemplateTextFormatter(
                    "{Timestamp:u} [{Level:u3}]: {SourceContext} - {Message}{NewLine}{Exception}", null));
            }
            Log.Logger = logConfig.CreateLogger();

            IWebHostBuilder builder = 
                WebHost.CreateDefaultBuilder(args)
                    .UseConfiguration(config)
                    .UseSerilog()
                    .UseContentRoot(Directory.GetCurrentDirectory())
                    .UseSentry()
                    .UseStartup<Startup>();

            try
            {
                IWebHost host = builder.Build();
                if (isService)
                {
                    host.RunAsService();
                }
                else
                {
                    host.Run();
                }
                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}