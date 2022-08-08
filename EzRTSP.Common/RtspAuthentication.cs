using System.Net;

namespace EzRTSP.Common;

public sealed class RtspAuthentication
{
    public RtspAuthentication(string username, string password)
    {
        Credential = new NetworkCredential(username, password);
    }

    public NetworkCredential Credential { get; }
}