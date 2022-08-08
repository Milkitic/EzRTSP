namespace EzRTSP.Common.Builder.NVIDIA;

public class NvEncoderOptions : IEncoderOptions
{
    private List<KeyValuePair<string, string>> _settings = new List<KeyValuePair<string, string>>();

    public NvEncoderOptions()
    {
    }

    public NvEncoderOptions(string option, string value)
    {
        _settings.Add(new KeyValuePair<string, string>(option, value));
    }

    public string Value => string.Join(" ",
        _settings.Select(k => $"{k.Key} {k.Value}")
    );

    public NvEncoderOptions WithGPU(int index)
    {
        _settings.Add(new KeyValuePair<string, string>("-gpu", index.ToString()));
        return this;
    }

    public NvEncoderOptions WithPreset(IPresetValue preset)
    {
        _settings.Add(new KeyValuePair<string, string>("-preset", preset.Value));
        return this;
    }

    public NvEncoderOptions WithVsync(int value)
    {
        _settings.Add(new KeyValuePair<string, string>("-vsync", value.ToString()));
        return this;
    }

    public NvEncoderOptions WithForcingAsIDRFrames()
    {
        _settings.Add(new KeyValuePair<string, string>("-forced-idr", "1"));
        return this;
    }

    public static NvEncoderOptions Default =>
        new NvEncoderOptions()
            .WithPreset(NvEncoderPresetValue.HighPerformance)
            .WithVsync(0)
            .WithForcingAsIDRFrames();
}