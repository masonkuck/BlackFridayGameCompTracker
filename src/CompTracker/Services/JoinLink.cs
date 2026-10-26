using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using QRCoder;

namespace CompTracker.Services;

// The address guests type (or scan) to open the app on their phones, plus its QR code.
// Set "JoinUrl" in appsettings.json if the detected address is wrong (e.g. a VPN or virtual adapter wins).
public class JoinLink(IConfiguration config, IServer server)
{
    private readonly ConcurrentDictionary<string, string> svgCache = new();

    // Worked out on each call: the PC's Wi-Fi address can change (DHCP) while the app is running.
    public string? Url
    {
        get
        {
            if (config["JoinUrl"] is { Length: > 0 } configured)
                return configured;
            var ip = LanAddress();
            return ip is null ? null : $"http://{ip}:{Port()}/";
        }
    }

    // The address of the page being viewed, so a dev tunnel or other public link shares itself. Falls back to the
    // LAN address when the page is open on localhost, since a phone can't reach that.
    public string? UrlFor(string pageBaseUri)
    {
        if (config["JoinUrl"] is { Length: > 0 } configured)
            return configured;
        if (Uri.TryCreate(pageBaseUri, UriKind.Absolute, out var page) && !IsLocal(page.Host))
            return page.GetLeftPart(UriPartial.Authority) + "/";
        return Url;
    }

    private static bool IsLocal(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || (IPAddress.TryParse(host.Trim('[', ']'), out var ip) && IPAddress.IsLoopback(ip));

    // Black on white with a quiet zone: inverted (light on dark) codes don't scan on every phone.
    public string QrSvg(string url) => svgCache.GetOrAdd(url, u =>
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(u, QRCodeGenerator.ECCLevel.M);
        return new SvgQRCode(data).GetGraphic(8, "#000000", "#ffffff", drawQuietZones: true,
            sizingMode: SvgQRCode.SizingMode.ViewBoxAttribute);
    });

    // An IPv4 address on a network that has a gateway (the home router), so not loopback or an isolated virtual switch.
    private static IPAddress? LanAddress() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Select(n => n.GetIPProperties())
            .Where(p => p.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any)))
            .SelectMany(p => p.UnicastAddresses)
            .Select(a => a.Address)
            .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a));

    private int Port()
    {
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses ?? [];
        foreach (var address in addresses)
        {
            // Kestrel reports e.g. "http://0.0.0.0:5080"; swap the wildcard host so Uri can parse it.
            if (Uri.TryCreate(address.Replace("://+", "://x").Replace("://*", "://x").Replace("[::]", "x"), UriKind.Absolute, out var uri))
                return uri.Port;
        }
        return 5080;
    }
}
