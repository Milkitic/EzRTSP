using System.ComponentModel;

namespace EzRTSP;

public class ServerConfiguration
{
    public string? FfmpegDir { get; set; } = "./Tools/ffmpeg";
    public int RpcBindPort { get; set; } = 7021;
    public string RpcBindPath { get; set; } = "/api/rpc";
    public string RtmpPushServer { get; set; } = "rtmp://127.0.0.1:8088/rtmp-push/";
    public string RtmpPullPortAndPath { get; set; } = "8087/live";

    [Description("BitStream: Main, Sub, Third\r\n" +
                 "ChannelConfiguration格式: 频道号（从1开始） + ',' + 码流（支持1,2,3）")]
    public List<RtspConfiguration> RtspConfigurations { get; set; } = new()
    {
        new RtspConfiguration()
    };
}