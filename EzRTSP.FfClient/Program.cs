using System.Diagnostics;
using CommandLine;
using EzRTSP.Common;
using EzRTSP.Common.Utils;
using EzRTSP.FfClient.Connection;

namespace EzRTSP.FfClient;

public class Program
{
    private static SignalRClient? _signalRClient;
    private static FfProcessWrapper? _wrapper;

    public static async Task<int> Main(string[] args)
    {
        var result = Parser.Default.ParseArguments<Options>(args);
        if (!result.Errors.Any())
        {
            var options = result.Value;
            var success = BindHostProcess(options.HostPid);
            if (!success)
            {
                ConsoleHelper.WriteError("Bind host pid error: " + options.HostPid, "main");
                return 2;
            }

            _signalRClient = new SignalRClient(options.HostWsUri, options.HostWsToken);
            _signalRClient.StopRequested += SignalRClient_OnStopRequested;
            _signalRClient.StartBackground();

            _wrapper = new FfProcessWrapper(options.FfmpegDirectory, _signalRClient,
                options.TargetHls,
                options.TargetRtmp == null ? null : new Uri(options.TargetRtmp),
                new Uri(options.RtspSource),
                options.Identifier,
                options.PreferredStreamCodec);
            Size? size;
            if (options.OutputSize == null)
            {
                size = default;
            }
            else
            {
                var split = options.OutputSize.Split('x');
                size = new Size(int.Parse(split[0]), int.Parse(split[1]));
            }

            _wrapper.GenericErrorOccurs += Wrapper_GenericErrorOccurs;
            try
            {
                new Task(async () =>
                {
                    while (Console.ReadKey(true).KeyChar != 'q') { }
                    await _wrapper.StopAsync();
                }).Start();
                await _wrapper.StartAsync(options.RtspUser, options.RtspPass, size);
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteError(ex.Message, _wrapper.Module);
                await _signalRClient.RaiseLog(LogType.Error, "Error on startup: " + ex, _wrapper.Module);
                return 3;
            }

            await _signalRClient.StopAsync();
        }
        else
        {
            return 1;
        }

        return 0;
    }

    private static async void Wrapper_GenericErrorOccurs(string message, string module)
    {
        await _signalRClient!.RaiseLog(LogType.Error, message, module);
    }


    private static bool BindHostProcess(int hostPid)
    {
        try
        {
            var process = Process.GetProcessById(hostPid);
            if (process.HasExited) return false;
            new Task(async () =>
            {
                await process.WaitForExitAsync();
                new Task(() =>
                {
                    Thread.Sleep(3000);
                    Environment.Exit(0);
                }).Start();
                ConsoleHelper.WriteWarn("EzRTSP Server closed, exiting.", "main");
                if (_wrapper != null) await _wrapper.StopAsync();
            }).Start();
            return true;
        }
        catch (Exception ex)
        {
#if DEBUG
            ConsoleHelper.WriteError(ex.Message, "main");
#endif
            return false;
        }
    }

    private static async void SignalRClient_OnStopRequested()
    {
        if (_wrapper != null) await _wrapper.StopAsync();
    }
}

public class Options
{
    [Option("host-pid", Required = true)]
    public int HostPid { get; set; }
    [Option("host-ws-uri", Required = true)]
    public string HostWsUri { get; set; } = null!;
    [Option("host-ws-token", Required = true)]
    public string HostWsToken { get; set; } = null!;
    [Option("rtsp-source", Required = true)]
    public string RtspSource { get; set; } = null!;
    [Option("identifier", Required = true)]
    public string Identifier { get; set; } = null!;
    [Option("rtsp-user", Required = false)]
    public string? RtspUser { get; set; }
    [Option("rtsp-pass", Required = false)]
    public string? RtspPass { get; set; }
    [Option("size", Required = false)]
    public string? OutputSize { get; set; }
    [Option("target-hls", Required = false)]
    public string? TargetHls { get; set; }
    [Option("target-rtmp", Required = false)]
    public string? TargetRtmp { get; set; }
    [Option("ffmpeg-dir", Required = false)]
    public string? FfmpegDirectory { get; set; }
    [Option("preferred-codec", Required = false, Default = StreamCodec.Copy)]
    public StreamCodec PreferredStreamCodec { get; set; }
}