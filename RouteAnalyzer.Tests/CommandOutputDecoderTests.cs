using System.Text;
using RouteAnalyzer.Services;

namespace RouteAnalyzer.Tests;

public class CommandOutputDecoderTests
{
    [Fact]
    public void Decode_PrefersTraditionalChineseOutputOverMojibake()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var source = "透過最多 24 個躍點追蹤到 172.17.70.36 的路由";
        var bytes = Encoding.GetEncoding(950).GetBytes(source);

        var decoded = CommandOutputDecoder.Decode(bytes);

        Assert.Equal(source, decoded);
    }

    [Fact]
    public void Decode_PreservesUtf8Output()
    {
        var source = "traceroute to vpn.example.com";
        var bytes = Encoding.UTF8.GetBytes(source);

        var decoded = CommandOutputDecoder.Decode(bytes);

        Assert.Equal(source, decoded);
    }
}
