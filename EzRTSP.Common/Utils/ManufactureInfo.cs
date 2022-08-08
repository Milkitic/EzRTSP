using EzRTSP.Common.Builder;

namespace EzRTSP.Common.Utils;

public readonly struct ManufactureInfo
{
    public ManufactureInfo(string name, int index, Manufacture manufacture)
    {
        Name = name;
        Index = index;
        Manufacture = manufacture;
    }

    public string Name { get; }
    public int Index { get; }
    public Manufacture Manufacture { get; }
}