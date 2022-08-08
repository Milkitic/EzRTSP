using System.Diagnostics;
using System.Text.RegularExpressions;
using EzRTSP.Common;
using EzRTSP.Common.Builder;
using EzRTSP.Common.Builder.AMD;
using EzRTSP.Common.Builder.CPU;
using EzRTSP.Common.Builder.Intel;
using EzRTSP.Common.Builder.NVIDIA;
using EzRTSP.Common.Utils;
using EzRTSP.FfClient.Connection;

namespace EzRTSP.FfClient;

public sealed class FfProcessWrapper
{
    public event Action<string, string>? GenericErrorOccurs;

    private static readonly Regex HlsRegex = new(@"\[hls @ (.+)\] Opening '(.+)realplay\.m3u8\.tmp' for writing");
    private static readonly Regex ProgressRegex = new(@"\[info\] frame= (.+) fps= (.+)");

    private readonly string? _baseHlsPushDir;
    private readonly Uri? _baseRtmpPushUri;
    private readonly StreamCodec _preferredStreamCodec;
    private readonly string? _ffmpegDirectory;
    private readonly SignalRClient _signalRClient;
    private readonly Uri _rtspHost;
    private readonly string _targetIdentifier;

    private bool _isPreparing;
    private Process? _ffmpegProc;

    public FfProcessWrapper(
        string? ffmpegDirectory,
        SignalRClient signalRClient,
        string? baseHlsPushDir,
        Uri? baseRtmpPushUri,
        Uri rtspHost,
        string targetIdentifier,
        StreamCodec preferredStreamCodec)
    {
        _ffmpegDirectory = ffmpegDirectory;
        _signalRClient = signalRClient;
        _rtspHost = rtspHost;
        _targetIdentifier = targetIdentifier;
        _baseHlsPushDir = baseHlsPushDir;
        _baseRtmpPushUri = baseRtmpPushUri;
        _preferredStreamCodec = preferredStreamCodec;
    }

    public bool IsRunning => _isPreparing || _ffmpegProc is { Id: > 0, HasExited: false };
    public string Module => $"task @ {_rtspHost}";

    public async Task StartAsync(string? username, string? password, Size? resolution)
    {
        if (IsRunning) return;
        _isPreparing = true;
        try
        {
            var prepareTcs = new TaskCompletionSource<object?>();

            var builder = new RtspCommandBuilder()
                .UseUri(_rtspHost.Host, _rtspHost.Port, _rtspHost.AbsolutePath)
                .WithHlsTime(1)
                .WithHlsListSize(5);
            if (username != null && password != null)
            {
                builder.WithAuthentication(username, password);
            }

            if (resolution != null)
            {
                builder.WithOutputResolution(resolution.Value.Width, resolution.Value.Height);
            }

            if (_baseRtmpPushUri != null)
            {
                var baseUri = _baseRtmpPushUri.ToString().TrimEnd('/');
                builder.ToRtmp($"{baseUri}/{_targetIdentifier}");
            }
            else
            {
                if (_baseHlsPushDir == null)
                {
                    throw new ArgumentNullException(nameof(_baseHlsPushDir), "HlsPushDir is null");
                }

                var baseDir = Path.Combine(_baseHlsPushDir, _targetIdentifier);
                var filePath = Path.Combine(baseDir, "realplay.m3u8");
                if (!Directory.Exists(baseDir))
                {
                    Directory.CreateDirectory(baseDir);
                }
                else if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                builder.ToM3U8File(filePath);
            }

            BuildEncoderAndDecoder(builder);

            if (builder.EncodingSettings == null)
                throw new Exception("None of available encoders can be found.");
            if (builder.DecodingSettings == null)
                throw new Exception("None of available decoders can be found.");

            var args = builder.Build();
            var arguments = "-loglevel level -hide_banner " + args;
            _ffmpegProc = new Process
            {
                EnableRaisingEvents = true,
                StartInfo = new ProcessStartInfo
                {
                    Arguments = arguments,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };
            if (_ffmpegDirectory == null)
            {
                _ffmpegProc.StartInfo.FileName = "ffmpeg";
            }
            else
            {
                var file = Directory.EnumerateFiles(_ffmpegDirectory, "ffmpeg*").FirstOrDefault();
                _ffmpegProc.StartInfo.FileName = file;
            }

            string? errMsg = null;
            string? innerErrMsg = null;

            _ffmpegProc.OutputDataReceived += OnErrorDataReceived;
            _ffmpegProc.ErrorDataReceived += OnErrorDataReceived;

            _ffmpegProc.Start();
            _ffmpegProc.BeginOutputReadLine();
            _ffmpegProc.BeginErrorReadLine();
            await _signalRClient.SetFFProcId(Environment.ProcessId, _ffmpegProc.Id);
            try
            {
                await prepareTcs.Task;
            }
            catch
            {
                _ffmpegProc.Kill();
                throw;
            }

            await _ffmpegProc.WaitForExitAsync();

            Thread.Sleep(100);
            var exitCode = _ffmpegProc.ExitCode;

            if (exitCode != 0)
            {
                var message = (innerErrMsg == null
                        ? errMsg
                        : (errMsg == null
                            ? innerErrMsg
                            : innerErrMsg + " -> " + errMsg)
                    ) ?? $"Process exited unexpectedly. ({exitCode})";
                throw new Exception(message);
            }

            _ffmpegProc = null;

            async void OnErrorDataReceived(object obj, DataReceivedEventArgs e)
            {
                if (e.Data == null) return;
                if (builder.RtmpUri != null)
                {
                    var match = ProgressRegex.Match(e.Data);
                    if (match.Success)
                    {
                        if (prepareTcs.Task.IsCompleted) return;
                        ConsoleHelper.WriteInfo("RTMP streaming Started", Module);
                        prepareTcs.TrySetResult(null);
                        await _signalRClient.RaiseLog(LogType.Info, "RTMP streaming Started", Module);
                        return;
                    }
                }
                else
                {
                    var match = HlsRegex.Match(e.Data);
                    if (match.Success)
                    {
                        if (prepareTcs.Task.IsCompleted) return;
                        ConsoleHelper.WriteInfo("HLS streaming Started", Module);
                        prepareTcs.TrySetResult(null);
                        await _signalRClient.RaiseLog(LogType.Info, "HLS streaming Started", Module);
                        return;
                    }
                }

                if (e.Data.StartsWith("[error] ", StringComparison.Ordinal) ||
                    e.Data.StartsWith("[fatal] ", StringComparison.Ordinal) ||
                    e.Data.StartsWith("[panic] ", StringComparison.Ordinal))
                {
                    var i = e.Data.IndexOf("] ", StringComparison.Ordinal);
                    errMsg = e.Data[(i + 2)..];
                    if (!prepareTcs.Task.IsCompleted)
                    {
                        prepareTcs.SetException(new Exception("Pre-load exception: " + errMsg));
                    }

                    return;
                }

                if (e.Data.Contains(" [error] ", StringComparison.Ordinal))
                {
                    var i = e.Data.IndexOf(" [error] ", StringComparison.Ordinal);
                    innerErrMsg = e.Data[(i + 9)..];
                    GenericErrorOccurs?.Invoke(innerErrMsg, Module);
                }
                else if (e.Data.Contains(" [fatal] ", StringComparison.Ordinal))
                {
                    var i = e.Data.IndexOf(" [fatal] ", StringComparison.Ordinal);
                    innerErrMsg = e.Data[(i + 9)..];
                    if (!prepareTcs.Task.IsCompleted)
                    {
                        prepareTcs.SetException(new Exception("Pre-load exception: " + innerErrMsg));
                    }
                }
                else if (e.Data.Contains(" [panic] ", StringComparison.Ordinal))
                {
                    var i = e.Data.IndexOf(" [panic] ", StringComparison.Ordinal);
                    innerErrMsg = e.Data[(i + 9)..];
                    if (!prepareTcs.Task.IsCompleted)
                    {
                        prepareTcs.SetException(new Exception("Pre-load exception: " + innerErrMsg));
                    }
                }
            }
        }
        finally
        {
            _isPreparing = false;
        }
    }

    public async Task StopAsync()
    {
        if (_ffmpegProc == null || !IsRunning) return;

        using var cts = new CancellationTokenSource(1000);
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 3000)
        {
            await using (var streamWriter = _ffmpegProc.StandardInput)
            {
                try
                {
                    await streamWriter.WriteAsync('q');
                }
                catch
                {
                    // ignored
                }
            }

            try
            {
                await _ffmpegProc.WaitForExitAsync(cts.Token);
                break;
            }
            catch (TaskCanceledException)
            {
            }
        }

        if (IsRunning) _ffmpegProc.Kill();
    }

    private void BuildEncoderAndDecoder(RtspCommandBuilder builder)
    {
        if (_preferredStreamCodec == StreamCodec.Copy)
        {
            BuildCopy(builder);
            return;
        }

        var supportedGpuList = GPUSelectHelper.EnumerateSupportedGPU().ToList();
        if (supportedGpuList.Count == 0)
        {
            BuildCopy(builder);
            return;
        }

        var nvList = supportedGpuList.Where(k => k.Manufacture == Manufacture.NVIDIA).ToList();
        var amdList = supportedGpuList.Where(k => k.Manufacture == Manufacture.AMD).ToList();
        var intelList = supportedGpuList.Where(k => k.Manufacture == Manufacture.INTEL).ToList();
        if (_preferredStreamCodec == StreamCodec.X264)
        {
            BuildCpu(builder);
            return;
        }
        if (_preferredStreamCodec == StreamCodec.H264_NVENC && nvList.Count != 0)
        {
            if (TryBuildNv(builder, nvList)) return;
        }
        else if (_preferredStreamCodec == StreamCodec.H264_QSV && intelList.Count != 0)
        {
            if (TryBuildIntel(builder, intelList)) return;
        }
        else if (_preferredStreamCodec == StreamCodec.H264_AMF && amdList.Count != 0)
        {
            if (TryBuildAmd(builder, amdList)) return;
        }
        else
        {
            if (TryBuildIntel(builder, intelList)) return;
            if (TryBuildNv(builder, nvList)) return;
            if (TryBuildAmd(builder, amdList)) return;
        }

        // fallback to copy
        BuildCopy(builder);
    }

    private static bool TryBuildAmd(RtspCommandBuilder builder, List<ManufactureInfo> list)
    {
        if (list.Count <= 0) return false;
        BuildAmd(builder);
        return true;
    }

    private static bool TryBuildNv(RtspCommandBuilder builder, List<ManufactureInfo> list)
    {
        if (list.Count <= 0) return false;
        BuildNv(builder);
        return true;
    }

    private static bool TryBuildIntel(RtspCommandBuilder builder, List<ManufactureInfo> list)
    {
        if (list.Count <= 0) return false;
        BuildIntel(builder);
        return true;
    }

    private static void BuildCopy(RtspCommandBuilder builder)
    {
        builder.WithDecodingSettings(DefaultDecodingSettings.Default);
        builder.WithEncodingSettings(DefaultEncodingSettings.DefaultCopy);
    }

    private static void BuildCpu(RtspCommandBuilder builder)
    {
        builder.UseDefaultDecodingSettings(DefaultDecodingSettings.Default);
        builder.UseDefaultEncodingSettings(DefaultEncodingSettings.Default);
    }

    private static void BuildNv(RtspCommandBuilder builder)
    {
        builder.UseNvidiaDecodingSettings(NvDecodingSettings.Default);
        builder.UseNvidiaEncodingSettings(NvEncodingSettings.Default);
    }

    private static void BuildAmd(RtspCommandBuilder builder)
    {
        builder.UseDefaultDecodingSettings(DefaultDecodingSettings.Default);
        builder.UseAmdEncodingSettings(AmdEncodingSettings.Default);
    }

    private static void BuildIntel(RtspCommandBuilder builder)
    {
        builder.UseDefaultDecodingSettings(DefaultDecodingSettings.Default);
        builder.UseIntelEncodingSettings(IntelEncodingSettings.Default)
            .WithHlsTime(2);
    }
}