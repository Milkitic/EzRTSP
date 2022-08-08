﻿namespace EzRTSP.Common.Builder;

public interface IEncodingSettings
{
    Manufacture Manufacture { get; }
    IEncoderValue Encoder { get; }
    //IPresetValue EncoderPreset { get; }
    IEncoderOptions EncoderOptions { get; }
}