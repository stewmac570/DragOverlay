using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace CastleOverlayV2.Services;

/// <summary>
/// The Big logger over WiFi (rc-logger firmware from 28 Nov 2026). The logger joins a saved
/// network while its throttle sits at neutral, broadcasts "BIGLOGGER 1 &lt;ip&gt;" on UDP 4210
/// every 2 s, and answers the same line commands as USB on TCP 2323. A WiFi logger is named
/// "wifi:&lt;ip&gt;" wherever a COM port name would go, so every USB feature works over WiFi too.
/// </summary>
public static class LoggerWifi
{
    public const string Prefix = "wifi:";
    public const int BeaconPort = 4210;
    public const int TcpPort = 2323;
    private static readonly Regex Beacon = new(@"^BIGLOGGER \d+ (?<ip>\d+\.\d+\.\d+\.\d+)$", RegexOptions.Compiled);

    public static bool IsWifiName(string name) => name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>Listens for the logger's broadcast. Empty if none is heard in <paramref name="listenFor"/>.</summary>
    public static IReadOnlyList<string> FindLoggers(TimeSpan listenFor, CancellationToken cancellationToken = default)
    {
        var found = new List<string>();
        try
        {
            using var udp = new UdpClient();
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, BeaconPort));
            udp.Client.ReceiveTimeout = 500;
            var stopwatch = Stopwatch.StartNew();
            var from = new IPEndPoint(IPAddress.Any, 0);
            while (stopwatch.Elapsed < listenFor && found.Count == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    string text = Encoding.ASCII.GetString(udp.Receive(ref from)).Trim();
                    Match match = Beacon.Match(text);
                    if (match.Success)
                        found.Add(Prefix + match.Groups["ip"].Value);
                }
                catch (SocketException)
                {
                    // Timed out waiting for a packet: keep listening until the time is up.
                }
            }
        }
        catch (SocketException)
        {
            // The port is taken or blocked: no WiFi logger can be found this way.
        }
        return found;
    }
}

/// <summary>USB ports first. If no logger is plugged in, listens for one on WiFi.</summary>
public sealed class UsbThenWifiPortDiscovery : IMiniLoggerPortDiscovery
{
    private readonly IMiniLoggerPortDiscovery _usb;
    private readonly TimeSpan _listenFor;

    public UsbThenWifiPortDiscovery(IMiniLoggerPortDiscovery? usb = null, TimeSpan? listenFor = null)
    {
        _usb = usb ?? new WmiMiniLoggerPortDiscovery();
        _listenFor = listenFor ?? TimeSpan.FromSeconds(3);   // the logger broadcasts every 2 s
    }

    public IReadOnlyList<string> FindCandidatePorts()
    {
        IReadOnlyList<string> ports = _usb.FindCandidatePorts();
        return ports.Count > 0 ? ports : LoggerWifi.FindLoggers(_listenFor);
    }
}

/// <summary>Serial for a COM port, TCP for a "wifi:" name.</summary>
public sealed class AutoMiniLoggerTransportFactory : IMiniLoggerTransportFactory
{
    public IMiniLoggerLineTransport Create() => new AutoMiniLoggerLineTransport();
}

public sealed class AutoMiniLoggerLineTransport : IMiniLoggerLineTransport
{
    private IMiniLoggerLineTransport? _inner;

    public void Open(string portName, int baudRate, bool dtrEnable, bool rtsEnable)
    {
        _inner = LoggerWifi.IsWifiName(portName) ? new TcpMiniLoggerLineTransport() : new SerialMiniLoggerLineTransport();
        _inner.Open(portName, baudRate, dtrEnable, rtsEnable);
    }

    public void DiscardInput() => Inner().DiscardInput();
    public void WriteLine(string line) => Inner().WriteLine(line);
    public string? ReadLine(TimeSpan timeout, CancellationToken cancellationToken) => Inner().ReadLine(timeout, cancellationToken);
    public void Close() => _inner?.Close();
    public void Dispose() { _inner?.Dispose(); _inner = null; }

    private IMiniLoggerLineTransport Inner() =>
        _inner ?? throw new InvalidOperationException("The logger connection is not open.");
}

/// <summary>The logger's line commands over TCP. Same reading rules as the serial transport.</summary>
public sealed class TcpMiniLoggerLineTransport : IMiniLoggerLineTransport
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private readonly byte[] _readBuffer = new byte[64 * 1024];
    private readonly StringBuilder _buffer = new();
    private readonly Queue<string> _lines = new();

    public void Open(string portName, int baudRate, bool dtrEnable, bool rtsEnable)
    {
        string host = LoggerWifi.IsWifiName(portName) ? portName[LoggerWifi.Prefix.Length..] : portName;
        _client = new TcpClient { NoDelay = true, ReceiveBufferSize = 1024 * 1024 };
        if (!_client.ConnectAsync(host, LoggerWifi.TcpPort).Wait(TimeSpan.FromSeconds(5)))
            throw new IOException($"The logger at {host} did not answer on WiFi.");
        _stream = _client.GetStream();
    }

    public void DiscardInput()
    {
        NetworkStream stream = Stream();
        while (stream.DataAvailable)
            stream.Read(_readBuffer, 0, _readBuffer.Length);
        _buffer.Clear();
        _lines.Clear();
    }

    public void WriteLine(string line)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(line + "\n");
        Stream().Write(bytes, 0, bytes.Length);
    }

    public string? ReadLine(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        NetworkStream stream = Stream();
        while (stopwatch.Elapsed < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_lines.Count > 0)
                return _lines.Dequeue();
            if (!stream.DataAvailable)
            {
                if (_client is { Connected: false })
                    throw new IOException("The logger closed the WiFi connection.");
                cancellationToken.WaitHandle.WaitOne(5);
                continue;
            }
            int n = stream.Read(_readBuffer, 0, _readBuffer.Length);
            if (n == 0)
                throw new IOException("The logger closed the WiFi connection.");
            for (int i = 0; i < n; i++)
            {
                char ch = (char)_readBuffer[i];
                if (ch == '\n')
                {
                    _lines.Enqueue(_buffer.ToString());
                    _buffer.Clear();
                }
                else
                {
                    _buffer.Append(ch);
                }
            }
        }
        return _lines.Count > 0 ? _lines.Dequeue() : null;
    }

    public void Close()
    {
        _stream?.Close();
        _client?.Close();
    }

    public void Dispose()
    {
        Close();
        _stream?.Dispose();
        _client?.Dispose();
        _stream = null;
        _client = null;
    }

    private NetworkStream Stream() =>
        _stream ?? throw new InvalidOperationException("The WiFi connection is not open.");
}

/// <summary>The Big logger's <c>wifi</c> commands. Passwords go to the logger and are never read back.</summary>
public sealed class BigLoggerWifiSettings
{
    private readonly MiniLoggerUsb _logger;

    public BigLoggerWifiSettings(MiniLoggerUsb logger) => _logger = logger;

    /// <summary>One line, e.g. "WiFi on MyNet, 192.168.0.50, ..." or "WiFi off: throttle live".</summary>
    public string Status(CancellationToken cancellationToken = default) =>
        _logger.RunCommand("wifi status", line => line.StartsWith("WiFi", StringComparison.Ordinal) ||
                                                  line.StartsWith("Commands:", StringComparison.Ordinal),
            cancellationToken).LastOrDefault() ?? "";

    /// <summary>False when the logger's firmware has no WiFi.</summary>
    public bool Supported(CancellationToken cancellationToken = default) =>
        Status(cancellationToken).StartsWith("WiFi", StringComparison.Ordinal);

    public IReadOnlyList<string> Networks(CancellationToken cancellationToken = default) =>
        _logger.RunCommand("wifi list", line => line == "END", cancellationToken)
            .Where(line => line.StartsWith("Network: ", StringComparison.Ordinal))
            .Select(line => line["Network: ".Length..])
            .ToList();

    public string Add(string name, string password, CancellationToken cancellationToken = default)
    {
        if (name.Contains('|') || name.Contains('\n') || password.Contains('\n'))
            return "ERROR: the name can't contain | or a line break.";
        return Reply($"wifi add {name}|{password}", cancellationToken);
    }

    public string Remove(string name, CancellationToken cancellationToken = default) =>
        Reply($"wifi remove {name}", cancellationToken);

    public string SetUpdatePassword(string password, CancellationToken cancellationToken = default) =>
        Reply($"wifi otapass {password}", cancellationToken);

    private string Reply(string command, CancellationToken cancellationToken) =>
        _logger.RunCommand(command, line => line.StartsWith("OK", StringComparison.Ordinal) ||
                                            line.StartsWith("ERROR", StringComparison.Ordinal),
            cancellationToken).LastOrDefault() ?? "ERROR: no reply";
}
