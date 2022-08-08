using EzRTSP.Common;

namespace EzRTSP;

public class ChannelConfiguration : IEquatable<ChannelConfiguration>
{
    public int Channel { get; set; } = 1;

    public BitStream BitStream { get; set; } = BitStream.Main;

    public bool Equals(ChannelConfiguration? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Channel == other.Channel && BitStream == other.BitStream;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((ChannelConfiguration)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Channel, (int)BitStream);
    }

    public static bool operator ==(ChannelConfiguration? left, ChannelConfiguration? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(ChannelConfiguration? left, ChannelConfiguration? right)
    {
        return !Equals(left, right);
    }
}