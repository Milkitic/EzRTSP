namespace EzRTSP.Common.Builder;

public interface IDecodingSettings
{
    Manufacture Manufacture { get; }
    IDecoderValue Decoder { get; }
    IDecoderOptions DecoderOptions { get; }
}