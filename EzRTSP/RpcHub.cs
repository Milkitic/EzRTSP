using EzRTSP.Common;
using EzRTSP.Common.Utils;
using Microsoft.AspNetCore.SignalR;

namespace EzRTSP;

public class RpcHub : Hub
{
    public void RaiseLog(LogType type, string message, string module)
    {
        switch (type)
        {
            case LogType.Info:
                ConsoleHelper.WriteInfo(message, module);
                break;
            case LogType.Warn:
                ConsoleHelper.WriteWarn(message, module);
                break;
            case LogType.Error:
                ConsoleHelper.WriteError(message, module);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    public void SetFFProcId(int processId, int ffmpegProcId)
    {
        //ConsoleHelper.WriteInfo($"Got FF Instance: {processId}->{ffmpegProcId}", "hub");
        Program.ProcessManagement.TryAdd(processId, ffmpegProcId);
    }
}