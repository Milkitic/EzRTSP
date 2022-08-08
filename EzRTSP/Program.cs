using System.Collections.Concurrent;
using System.Diagnostics;
using EzRTSP.Common;
using Milki.Extensions.Configuration;

namespace EzRTSP;

class Program
{
    public static List<StreamManagement> StreamManagements { get; } = new();
    public static ConcurrentDictionary<int, int> ProcessManagement { get; } = new();

    static async Task Main()
    {
#if DEBUG
        Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
#endif
        CleanInstances();

        var configuration = ConfigurationFactory.GetConfiguration<ServerConfiguration>(".");
        ConfigurationFactory.Save(configuration);
        var rtmpPushServer = configuration.RtmpPushServer;
        var wsBindPort = configuration.RpcBindPort;
        var wsBindPath = configuration.RpcBindPath.StartsWith('/')
            ? configuration.RpcBindPath
            : '/' + configuration.RpcBindPath;
        var wsBindToken = Guid.NewGuid().ToString("N");
        var wsBindUri = new Uri("http://0.0.0.0:" + wsBindPort);

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls(wsBindUri.ToString());
        builder.Services.AddSignalR().AddMessagePackProtocol();

        var app = builder.Build();
        app.UseRouting();
        app.UseEndpoints(k => k.MapHub<RpcHub>(wsBindPath));
        var appRunTask = app.RunAsync();
        await Task.Delay(500);
        foreach (var rtspConfiguration in configuration.RtspConfigurations)
        {
            var tag = rtspConfiguration.Name;
            var rtspUsername = rtspConfiguration.Username;
            var rtspPassword = rtspConfiguration.Password;
            var rtspHost = rtspConfiguration.Host;
            var rtspPort = rtspConfiguration.Port;

            var management = new StreamManagement(new Uri(rtmpPushServer), wsBindUri + wsBindPath.TrimStart('/'),
                wsBindToken, rtspUsername, rtspPassword, configuration.RtmpPullPortAndPath)
            {
                CameraType = rtspConfiguration.CameraType,
                FfmpegDir = configuration.FfmpegDir
            };
            StreamManagements.Add(management);
            foreach (var channelConfiguration in rtspConfiguration.ChannelConfigurations)
            {
                var split = channelConfiguration.Split(',');
                if (split.Length > 0)
                {
                    var channel = int.Parse(split[0]);
                    var bitStream = (BitStream)int.Parse(split[1]);
                    var rtspIdentity = new RtspIdentity(rtspHost, rtspPort, channel, bitStream, tag);

                    management.AddTaskAndRunAsync(rtspIdentity);
                }
                else
                {
                    var rawAddress = split[0];
                    var rtspIdentity = new RtspIdentity(rawAddress, tag);

                    management.AddTaskAndRunAsync(rtspIdentity);
                }
            }
        }

        await appRunTask;
    }

    private static void CleanInstances()
    {
        var processes = Process.GetProcessesByName("ffmpeg");
        foreach (var process in processes)
        {
            if (process.MainModule?.FileName?.StartsWith(Environment.CurrentDirectory) == true)
            {
                process.Kill();
            }
        }
    }
}