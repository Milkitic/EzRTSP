using System.Net;
using System.Text;
using System.Web;
using EzRTSP.Common.Builder;
using Size = EzRTSP.Common.Size;

namespace EzRTSP.FfClient;

public class RtspCommandBuilder : IFFCommandBuilder
{
    public string? RtspHost { get; private set; }
    public int RtspPort { get; private set; } = 554;
    public string? RtspRoute { get; private set; }

    public IDecodingSettings? DecodingSettings { get; private set; }
    public IEncodingSettings? EncodingSettings { get; private set; }
    public Size? OutputResolution { get; private set; }
    public int? KeyFrames { get; private set; }
    public int? HlsListSize { get; private set; }
    public NetworkCredential? Credential { get; private set; }

    public string? SavePath { get; private set; }
    public string? RtmpUri { get; private set; }

    public RtspCommandBuilder UseUri(string rtspHost, int rtspPort = 554, string? route = null)
    {
        RtspHost = rtspHost;
        RtspPort = rtspPort;
        RtspRoute = route;
        return this;
    }

    public RtspCommandBuilder WithAuthentication(string username, string password)
    {
        Credential = new NetworkCredential(HttpUtility.UrlEncode(username),
            HttpUtility.UrlEncode(password));
        return this;
    }

    public RtspCommandBuilder WithHlsTime(int count)
    {
        KeyFrames = count;
        return this;
    }

    public RtspCommandBuilder WithHlsListSize(int count)
    {
        HlsListSize = count;
        return this;
    }

    public RtspCommandBuilder WithOutputResolution(int width, int height)
    {
        OutputResolution = new Size(width, height);
        return this;
    }

    public RtspCommandBuilder WithDecodingSettings(IDecodingSettings decodingSettings)
    {
        DecodingSettings = decodingSettings;
        return this;
    }

    public RtspCommandBuilder WithEncodingSettings(IEncodingSettings encodingSettings)
    {
        EncodingSettings = encodingSettings;
        return this;
    }

    /// <summary>
    /// m3u8 file
    /// </summary>
    /// <param name="rtmpUri"></param>
    /// <returns></returns>
    public RtspCommandBuilder ToRtmp(string rtmpUri)
    {
        RtmpUri = rtmpUri;
        return this;
    }

    /// <summary>
    /// m3u8 file
    /// </summary>
    /// <param name="savePath"></param>
    /// <returns></returns>
    public RtspCommandBuilder ToM3U8File(string savePath)
    {
        SavePath = savePath;
        return this;
    }

    public string Build()
    {
        if (RtspHost == null) throw new Exception("host is null");

        var list = new List<string?>();
        string protocol = "rtsp:";
        var uri = Credential == null
            ? $"{protocol}//{RtspHost}:{RtspPort}{RtspRoute}"
            : $"{protocol}//{Credential?.UserName}:{Credential?.Password}@{RtspHost}:{RtspPort}{RtspRoute}";
        var type = "-rtsp_transport tcp";
        var hwaccelCmd = "-hwaccel auto";
        var decoderCmd = DecodingSettings?.Decoder == null
            ? null
            : "-c:v " + DecodingSettings.Decoder?.Value + " ";
        var decoderOptionsCmd = DecodingSettings?.DecoderOptions?.Value;
        var inputCmd = $"-re -i {uri} ";
        var resolutionCmd = OutputResolution == null
            ? null
            : $"-s {OutputResolution.Value.Width}x{OutputResolution.Value.Height}";
        var encoderCmd = EncodingSettings?.Encoder == null
           ? null
           : "-c:v " + EncodingSettings.Encoder?.Value + " ";
        var encoderOptionsCmd = EncodingSettings?.EncoderOptions?.Value;

        list.Add(type);
        list.Add(hwaccelCmd);
        list.Add(decoderCmd);
        list.Add(decoderOptionsCmd);
        list.Add(inputCmd);
        list.Add(resolutionCmd);

        if (RtmpUri != null)
        {
            list.Add(encoderCmd);
            list.Add(encoderOptionsCmd);
            list.Add("-c:a copy");
            list.Add("-b:a ezrtsp");
            list.Add("-f flv");
            list.Add($"-y {RtmpUri}?sign={Convert.ToBase64String(Encoding.UTF8.GetBytes(RtmpUri.Split('/').Last()))}");
        }
        else
        {
            var keyFrameCmds = KeyFrames == null
                ? null
                : $"-force_key_frames \"expr: gte(t, n_forced * {KeyFrames.Value})\"";
            var hlsTimeCmds = KeyFrames == null
                ? null
                : $"-hls_init_time {KeyFrames.Value} " +
                  $"-hls_time {KeyFrames.Value}";
            var hlsListCmds = HlsListSize == null
                ? null
                : $"-hls_list_size {HlsListSize.Value} " +
                  $"-hls_wrap {HlsListSize.Value}";
            var fileCmd = SavePath == null
                ? null
                : $"-f hls \"{SavePath}\"";
            list.Add(keyFrameCmds);
            list.Add(encoderCmd);
            list.Add(encoderOptionsCmd);
            list.Add("-c:a copy");
            list.Add("-b:a ezrtsp");
            list.Add(hlsTimeCmds);
            list.Add(hlsListCmds);
            list.Add(fileCmd);
        }

        var args = string.Join(" ", list.Where(k => k != null));
        return args;
    }
}