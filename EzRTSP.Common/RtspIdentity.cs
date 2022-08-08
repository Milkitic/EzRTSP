namespace EzRTSP.Common;

public sealed class RtspIdentity : IEquatable<RtspIdentity>
{
    public RtspIdentity(string host, int port, int channel, BitStream bitStream, string tag = "")
    {
        Host = host;
        Port = port;
        Channel = channel;
        BitStream = bitStream;
        Tag = tag;
    }
    public RtspIdentity(string host, int channel, BitStream bitStream, string tag = "")
    {
        Host = host;
        Channel = channel;
        BitStream = bitStream;
        Tag = tag;
        Port = 554;
    }

    public RtspIdentity(string rawRtspAddress, string tag = "")
    {
        RawRtspAddress = rawRtspAddress;
        Tag = tag;
    }

    public string? Host { get; }
    public int Port { get; }
    public int Channel { get; }
    public BitStream BitStream { get; }
    public string? Tag { get; }
    public string? RawRtspAddress { get; }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((RtspIdentity)obj);
    }

    public bool Equals(RtspIdentity? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        if (RawRtspAddress == null)
        {
            return Host == other.Host && Port == other.Port && Channel == other.Channel && BitStream == other.BitStream && Tag == other.Tag;
        }

        return RawRtspAddress == other.RawRtspAddress && Tag == other.Tag;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            if (RawRtspAddress == null)
            {
                var hashCode = (Host != null ? Host.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ Port;
                hashCode = (hashCode * 397) ^ Channel;
                hashCode = (hashCode * 397) ^ (int)BitStream;
                hashCode = (hashCode * 397) ^ (Tag != null ? Tag.GetHashCode() : 0);
                return hashCode;
            }
            else
            {
                var hashCode = (RawRtspAddress != null ? RawRtspAddress.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Tag != null ? Tag.GetHashCode() : 0);
                return hashCode;
            }
        }
    }

    public static bool operator ==(RtspIdentity left, RtspIdentity right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(RtspIdentity left, RtspIdentity right)
    {
        return !Equals(left, right);
    }

    public override string ToString()
    {
        return RawRtspAddress ?? $"{Host}:{Port}/{Tag}{Channel}0{(int)BitStream}";
    }

    public string ToIdentifierString()
    {
        return Uri.EscapeDataString(
            RawRtspAddress == null
                ? $"{Host}-{Port}-{Tag}{Channel}0{(int)BitStream}".Replace('.', '-')
                : RawRtspAddress.Replace(':', '-').Replace('/', '-').Replace('.', '-')
        );
    }
}