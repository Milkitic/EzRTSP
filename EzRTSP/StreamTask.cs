using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using EzRTSP.Common;
using EzRTSP.Common.Builder;
using EzRTSP.Common.Utils;

namespace EzRTSP;

public class StreamTask : IEquatable<StreamTask>
{
    public event Action<StreamTask>? ProcessExit;

    private readonly StreamManagement _streamManagement;
    private readonly StreamCodec _preferredStreamCodec;

    private Process? _clientProc;
    public bool IsRunning => _clientProc is { Id: > 0, HasExited: false };

    public StreamTask(RtspIdentity rtspIdentity,
        StreamManagement streamManagement,
        StreamCodec preferredStreamCodec)
    {
        Identity = rtspIdentity;
        _streamManagement = streamManagement;
        _preferredStreamCodec = preferredStreamCodec;
    }

    public RtspIdentity Identity { get; }

    public async Task RunAsync(string username, string password, Size? resolution)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "Tools/FFClient/EzRTSP.FfClient.exe")),
            UseShellExecute = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        var list = processStartInfo.ArgumentList;
        var rtspPath = _streamManagement.CameraType switch
        {
            CameraType.Hikvision => HikvisionRouteHelper.FromSettings(Identity.Channel, Identity.BitStream),
            _ => throw new ArgumentOutOfRangeException()
        };

        var pid = Environment.ProcessId;
        var serverUri = new Uri(_streamManagement.ServerBindUri);
        var serverToken = _streamManagement.ServerBindToken;
        var rtspSource = Identity.RawRtspAddress ?? $"rtsp://{Identity.Host}:{Identity.Port}{rtspPath}";
        var identifier = Identity.ToIdentifierString();

        list.Add("--host-pid");
        list.Add(pid.ToString());
        list.Add("--host-ws-uri");
        list.Add("http://127.0.0.1:" + serverUri.Port + serverUri.PathAndQuery);
        list.Add("--host-ws-token");
        list.Add(serverToken);
        list.Add("--rtsp-source");
        list.Add(rtspSource);
        list.Add("--identifier");
        list.Add(identifier);
        list.Add("--rtsp-user");
        list.Add(username);
        list.Add("--rtsp-pass");
        list.Add(password);
        list.Add("--preferred-codec");
        list.Add(_preferredStreamCodec.ToString());
        if (resolution != null)
        {
            list.Add("--size");
            list.Add(resolution.Value.Width + "x" + resolution.Value.Height);
        }

        var module = $"task @ {rtspSource}";
        if (_streamManagement.RtmpPushUri != null)
        {
            list.Add("--target-rtmp");
            list.Add(_streamManagement.RtmpPushUri.ToString());
            var rtmpPushPort = _streamManagement.RtmpPushUri.Port;
            var rtmpPullPortAndPath = _streamManagement.RtmpPullPortAndPath;
            var app = _streamManagement.RtmpPushUri.Segments[1].Trim('/');
            var host = _streamManagement.RtmpPushUri.Host;
            if (host is "127.0.0.1" or "localhost")
            {
                var localhost = await Dns.GetHostEntryAsync(Dns.GetHostName());
                foreach (var ipAddress in localhost.AddressList.Where(k => k.AddressFamily == AddressFamily.InterNetwork).DistinctBy(k => k.ToString()))
                {
                    var rtmpPull = $"http://{ipAddress}:{rtmpPullPortAndPath}?port={rtmpPushPort}&app={app}&stream={identifier}";
                    ConsoleHelper.WriteInfo("RTMP pull address: " + rtmpPull, module);
                }
            }
            else
            {
                var rtmpPull = $"http://{host}:{rtmpPullPortAndPath}?port={rtmpPushPort}&app={app}&stream={identifier}";
                ConsoleHelper.WriteInfo("RTMP Pull: " + rtmpPull, module);
            }
        }
        else if (_streamManagement.BaseDir != null)
        {
            list.Add("--target-hls");
            list.Add(_streamManagement.BaseDir);
        }

        var ffmpegDir = _streamManagement.FfmpegDir;
        if (ffmpegDir != null && Directory.Exists(ffmpegDir))
        {
            var file = Directory.EnumerateFiles(ffmpegDir, "ffmpeg*").FirstOrDefault();
            if (file != null)
            {
                list.Add("--ffmpeg-dir");
                list.Add(Path.GetFullPath(Path.GetDirectoryName(file)));
            }
        }

        int exitCode = -1;
        int subPid = -1;
        while (exitCode != 0)
        {
            try
            {
                _clientProc = new Process
                {
                    EnableRaisingEvents = true,
                    StartInfo = processStartInfo,
                };
                _clientProc.Start();
                subPid = _clientProc.Id;
                await _clientProc.WaitForExitAsync();
                exitCode = _clientProc.ExitCode;
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteError($"Process start failed: " + ex.Message, module);
            }

            if (exitCode == 0) continue;
            if (Program.ProcessManagement.TryRemove(subPid, out var val))
            {
                try
                {
                    var proc = Process.GetProcessById(val);
                    proc.Kill();
                    ConsoleHelper.WriteInfo($"Killed actual ff instance", module);
                }
                catch
                {
                    // ignored
                }
            }

            ConsoleHelper.WriteError($"Client exited({exitCode}), retry to restart client after 3 seconds...", module);
            await Task.Delay(3000);
        }

        ProcessExit?.Invoke(this);
    }

    public async Task StopAsync()
    {
        if (_clientProc == null || !IsRunning) return;

        var sw = Stopwatch.StartNew();
        using (var cts = new CancellationTokenSource(1000))
        {
            while (sw.ElapsedMilliseconds < 3000)
            {
                // SignalR friendly exit

                try
                {
                    await _clientProc.WaitForExitAsync(cts.Token);
                    break;
                }
                catch (TaskCanceledException)
                {
                }
            }
        }

        if (IsRunning) _clientProc.Kill();
    }

    #region IEquatable

    public bool Equals(StreamTask? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Identity.Equals(other.Identity) && _preferredStreamCodec == other._preferredStreamCodec;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((StreamTask)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Identity, (int)_preferredStreamCodec);
    }

    public static bool operator ==(StreamTask? left, StreamTask? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(StreamTask? left, StreamTask? right)
    {
        return !Equals(left, right);
    }

    #endregion
}