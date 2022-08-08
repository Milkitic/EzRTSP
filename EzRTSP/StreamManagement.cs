using System.Collections.Concurrent;
using EzRTSP.Common;
using EzRTSP.Common.Utils;

namespace EzRTSP;

public class StreamManagement : IDisposable
{
    protected readonly string? DefaultUsername;
    protected readonly string? DefaultPassword;

    public StreamManagement(Uri rtmpPushUri, string serverBindPath, string serverBindToken,
        string? defaultUsername = null, string? defaultPassword = null, string rtmpPullPortAndPath = "0")
        : this(serverBindPath, serverBindToken, defaultUsername, defaultPassword,
            rtmpPullPortAndPath)
    {
        RtmpPushUri = rtmpPushUri;
    }

    public StreamManagement(string baseDir, string serverBindPath, string serverBindToken,
        string? defaultUsername = null, string? defaultPassword = null, string rtmpPullPortAndPath = "0")
        : this(serverBindPath, serverBindToken, defaultUsername, defaultPassword,
            rtmpPullPortAndPath)
    {
        BaseDir = baseDir;
    }

    private StreamManagement(string serverBindUri, string serverBindToken,
        string? defaultUsername, string? defaultPassword, string rtmpPullPortAndPath = "0")
    {
        ServerBindUri = serverBindUri;
        ServerBindToken = serverBindToken;
        RtmpPullPortAndPath = rtmpPullPortAndPath;
        DefaultUsername = defaultUsername;
        DefaultPassword = defaultPassword;
    }

    public string ServerBindUri { get; }
    public string ServerBindToken { get; }
    public string RtmpPullPortAndPath { get; }
    public string? BaseDir { get; }
    public Uri? RtmpPushUri { get; }
    public CameraType CameraType { get; init; }
    public string? FfmpegDir { get; init; }

    public ConcurrentDictionary<RtspIdentity, StreamTask> StreamTasks { get; } = new();

    public async Task<StreamTask> AddTaskAndRunAsync(RtspIdentity rtspIdentity, RtspAuthentication? authentication = null,
        StreamCodec preferredStreamCodec = StreamCodec.Copy, Size? convertResolution = null)
    {
        if (StreamTasks.TryGetValue(rtspIdentity, out var streamTask))
        {
            if (streamTask.IsRunning) return streamTask;
        }
        else
        {
            streamTask = new StreamTask(rtspIdentity, this, preferredStreamCodec);
            while (!StreamTasks.TryAdd(rtspIdentity, streamTask))
            {
            }
        }

        if (streamTask.IsRunning) return streamTask;
        try
        {
            authentication ??= new RtspAuthentication(DefaultUsername, DefaultPassword);
            await streamTask.RunAsync(DefaultUsername ?? authentication.Credential.UserName,
                DefaultPassword ?? authentication.Credential.Password, convertResolution);
        }
        catch
        {
            StreamTasks.TryRemove(rtspIdentity, out _);
            throw;
        }

        streamTask.ProcessExit += StreamTask_ProcessExit;
        return streamTask;
    }

    public async Task<StreamTask?> RemoveTask(RtspIdentity rtspIdentity)
    {
        if (StreamTasks.TryGetValue(rtspIdentity, out var task))
        {
            await task.StopAsync();
            return task;
        }
        else
        {
            ConsoleHelper.WriteWarn("No available task found.", "management");
            return null;
        }
    }

    public virtual void Dispose()
    {
        foreach (var streamTask in StreamTasks.Values.ToList())
        {
            streamTask.StopAsync().Wait();
        }
    }

    private void StreamTask_ProcessExit(StreamTask obj)
    {
        while (!StreamTasks.TryRemove(obj.Identity, out _))
        {
        }
    }
}