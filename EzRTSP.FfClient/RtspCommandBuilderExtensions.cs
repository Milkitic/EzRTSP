using EzRTSP.Common.Builder.AMD;
using EzRTSP.Common.Builder.CPU;
using EzRTSP.Common.Builder.Intel;
using EzRTSP.Common.Builder.NVIDIA;

namespace EzRTSP.FfClient;

public static class RtspCommandBuilderExtensions
{
    public static RtspCommandBuilder UseDefaultDecodingSettings(this RtspCommandBuilder rtspCommandBuilder,
        DefaultDecodingSettings decodingSettings)
    {
        rtspCommandBuilder.WithDecodingSettings(decodingSettings);
        return rtspCommandBuilder;
    }

    public static RtspCommandBuilder UseDefaultEncodingSettings(this RtspCommandBuilder rtspCommandBuilder,
        DefaultEncodingSettings encodingSettings)
    {
        rtspCommandBuilder.WithEncodingSettings(encodingSettings);
        return rtspCommandBuilder;
    }

    public static RtspCommandBuilder UseAmdEncodingSettings(this RtspCommandBuilder rtspCommandBuilder,
        AmdEncodingSettings encodingSettings)
    {
        rtspCommandBuilder.WithEncodingSettings(encodingSettings);
        return rtspCommandBuilder;
    }

    public static RtspCommandBuilder UseIntelDecodingSettings(this RtspCommandBuilder rtspCommandBuilder,
        IntelDecodingSettings decodingSettings)
    {
        rtspCommandBuilder.WithDecodingSettings(decodingSettings);
        return rtspCommandBuilder;
    }

    public static RtspCommandBuilder UseIntelEncodingSettings(this RtspCommandBuilder rtspCommandBuilder,
        IntelEncodingSettings encodingSettings)
    {
        rtspCommandBuilder.WithEncodingSettings(encodingSettings);
        return rtspCommandBuilder;
    }

    public static RtspCommandBuilder UseNvidiaDecodingSettings(this RtspCommandBuilder rtspCommandBuilder,
        NvDecodingSettings decodingSettings)
    {
        rtspCommandBuilder.WithDecodingSettings(decodingSettings);
        return rtspCommandBuilder;
    }

    public static RtspCommandBuilder UseNvidiaEncodingSettings(this RtspCommandBuilder rtspCommandBuilder,
        NvEncodingSettings encodingSettings)
    {
        rtspCommandBuilder.WithEncodingSettings(encodingSettings);
        return rtspCommandBuilder;
    }
}