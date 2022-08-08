namespace EzRTSP;

public class RtspConfiguration
{
    public string Name { get; set; } = "DVR";
    public string Username { get; set; } = "admin";
    public string Password { get; set; } = "admin";

    public string Host { get; set; } = "192.168.1.1";
    public int Port { get; set; } = 554;
    public CameraType CameraType { get; set; }

    public HashSet<string> ChannelConfigurations { get; set; } = new() { "1,1" };
}