using EzRTSP.Common;
using EzRTSP.Common.Utils;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace EzRTSP.FfClient.Connection;

public class SignalRClient
{
    public event Action? StopRequested;
    private bool _manualStop;
    private HubConnection? _connection;
    private CancellationTokenSource? _cts;
    private TaskCompletionSource? _tcs;

    private readonly string _host;
    private readonly string _token;

    public SignalRClient(string host, string token)
    {
        _host = host;
        _token = token;
    }

    public static string Module => "signalr";

    public void StartBackground()
    {
        _manualStop = false;
        _cts = new CancellationTokenSource();
        if (_connection == null)
        {
            _connection = new HubConnectionBuilder()
                .WithUrl($"{_host}", options =>
                {
                    options.Headers.Add("FFCLIENT-TOKEN", _token);
                })
                .AddMessagePackProtocol()
                .Build();
            _connection.Closed += async _ =>
            {
                if (_manualStop) return;
                Console.WriteLine("Lost connection from server.");

                if (_connection != null)
                {
                    await _connection.DisposeAsync();
                    _cts.Dispose();
                    _connection = null;
                    _tcs = null;
                    _cts = null;
                }

                StartBackground();
            };

            _connection.On("StopRequested", OnStopRequested);
        }

        ConnectUntilSuccess(_connection, _cts);
    }

    public async Task StopAsync()
    {
        try
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
        catch
        {
            // ignored
        }

        try
        {
            _manualStop = true;
            if (_connection != null)
            {
                await _connection.StopAsync();
                await _connection.DisposeAsync();
            }
        }
        catch
        {
            // ignored
        }
    }

    public async Task RaiseLog(LogType type, string message, string module)
    {
        if (_tcs != null)
        {
            try
            {
                await _tcs.Task;
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }

        await _connection!.InvokeAsync("RaiseLog", type, message, module);
    }

    public async Task SetFFProcId(int processId, int ffmpegProcId)
    {
        if (_tcs != null)
        {
            try
            {
                await _tcs.Task;
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }

        await _connection!.InvokeAsync("SetFFProcId", processId, ffmpegProcId);
    }

    private void ConnectUntilSuccess(HubConnection connection,
        CancellationTokenSource cancellationTokenSource)
    {
        Task.Run(async () =>
        {
            bool success = false;
            try
            {
                while (!cancellationTokenSource.IsCancellationRequested && !success)
                {
                    _tcs = new TaskCompletionSource();
                    try
                    {
                        await connection.StartAsync(cancellationTokenSource.Token);
                        success = true;
                        _tcs?.TrySetResult();
                    }
                    catch (Exception ex)
                    {
                        ConsoleHelper.WriteError("Failed to connect SignalR server: " + ex.Message, Module);
                        _tcs?.TrySetCanceled();
                        await Task.Delay(3000, cancellationTokenSource.Token);
                    }
                }

                ConsoleHelper.WriteInfo("Connected to SignalR server", Module);

            }
            catch (TaskCanceledException)
            {
                return;
            }
        });
    }

    private void OnStopRequested()
    {
        StopRequested?.Invoke();
    }
}