namespace EzRTSP.Common.Builder.AMD;

// currently no specialized decoder of amd is available
internal class AmdDecoderValue : IDecoderValue
{
    public AmdDecoderValue(string value)
    {
        Value = value;
    }

    public string Value { get; }
}