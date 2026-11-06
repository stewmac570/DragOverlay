using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;

namespace CastleOverlayV2.Services;

public sealed record CastleSessionInfo(int Number, int Pages)
{
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"Session {Number}  ({Pages * 256 / 1024.0:0.#} KB)");
}

public enum CastleLinkConnectStatus
{
    Connected,
    NotFound,
    PortBusy,
    NoEsc
}

public sealed record CastleLinkConnectResult(CastleLinkConnectStatus Status, string? PortName = null);

public sealed class CastleLinkStoppedRespondingException : IOException
{
    public CastleLinkStoppedRespondingException()
        : base("The Castle Link stopped answering.")
    {
    }
}

public interface ICastleLinkPortDiscovery
{
    IReadOnlyList<string> FindCandidatePorts();
}

public interface ICastleLinkTransport : IDisposable
{
    void Open(string portName);
    void DiscardInput();
    void Write(byte[] data);

    /// <summary>Returns exactly <paramref name="count"/> bytes, or null on a timeout.</summary>
    byte[]? Read(int count, TimeSpan timeout, CancellationToken cancellationToken);
}

public interface ICastleLinkDelay
{
    void Wait(TimeSpan delay, CancellationToken cancellationToken);
}

public interface ICastleLinkClient : IDisposable
{
    string EscFirmware { get; }

    /// <summary>The tune read on connect: 48 settings bytes then 2,560 curve bytes, or null.</summary>
    byte[]? TuneBytes { get; }

    CastleLinkConnectResult Connect(CancellationToken cancellationToken = default);
    IReadOnlyList<CastleSessionInfo> ListSessions(CancellationToken cancellationToken = default);
    void KeepAlive();
    byte[] DownloadSession(CastleSessionInfo session, CancellationToken cancellationToken = default);
}

/// <summary>
/// UI-free Castle Link client. It sends the messages the Castle app sent in the two captures,
/// in the same order, and nothing else. Several of them have no known purpose, so do not
/// remove or reorder any without a new capture. See Docs/Castle_Link_Download_Plan.md.
/// </summary>
public sealed class CastleLinkUsb : ICastleLinkClient
{
    private const byte ShortFrame = 0x43;
    private const byte PageFrame = 0x47;
    private const int ShortFrameLength = 13;
    private const int PageFrameLength = 261;
    private const int PageHeaderLength = 5;
    private const int BlocksBeforeLog = 4;
    private const int BootBlockCount = 6;
    private const int MemoryBlockCount = 320;
    private const int TuneBlockLength = 8;

    private static readonly TimeSpan ReplyTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan SetupPause = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan ListPause = TimeSpan.FromMilliseconds(500);

    private readonly ICastleLinkPortDiscovery _portDiscovery;
    private readonly Func<ICastleLinkTransport> _transportFactory;
    private readonly ICastleLinkDelay _delay;
    private readonly object _exchangeLock = new();
    private ICastleLinkTransport? _transport;

    public string EscFirmware { get; private set; } = "";

    public byte[]? TuneBytes { get; private set; }

    public CastleLinkUsb()
        : this(new WmiCastleLinkPortDiscovery(), () => new SerialCastleLinkTransport(), new ThreadCastleLinkDelay())
    {
    }

    public CastleLinkUsb(
        ICastleLinkPortDiscovery portDiscovery,
        Func<ICastleLinkTransport> transportFactory,
        ICastleLinkDelay? delay = null)
    {
        _portDiscovery = portDiscovery;
        _transportFactory = transportFactory;
        _delay = delay ?? new ThreadCastleLinkDelay();
    }

    public CastleLinkConnectResult Connect(CancellationToken cancellationToken = default)
    {
        CloseTransport();
        IReadOnlyList<string> ports = _portDiscovery.FindCandidatePorts();
        if (ports.Count == 0)
            return new CastleLinkConnectResult(CastleLinkConnectStatus.NotFound);

        bool openFailed = false;
        foreach (string portName in ports)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ICastleLinkTransport candidate = _transportFactory();
            try
            {
                candidate.Open(portName);
                candidate.DiscardInput();
                _transport = candidate;
                Exchange(Request(0x12, 0, 0), cancellationToken);
                return ConnectToEsc(portName, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                CloseTransport();
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                // Busy, unplugged, or something else using the same USB ID.
                openFailed |= ex is not CastleLinkStoppedRespondingException;
                _transport = null;
                candidate.Dispose();
            }
        }

        return new CastleLinkConnectResult(
            openFailed ? CastleLinkConnectStatus.PortBusy : CastleLinkConnectStatus.NotFound);
    }

    public IReadOnlyList<CastleSessionInfo> ListSessions(CancellationToken cancellationToken = default)
    {
        Exchange(Request(0x32, 0, 0), cancellationToken);

        // The ESC lists the newest session first. Both captures stopped at session 2,
        // one entry short of the newest session's number.
        var sessions = new List<CastleSessionInfo>();
        CastleSessionInfo newest = ReadSessionEntry(1, cancellationToken);
        if (newest.Pages == 0)
            return sessions;
        sessions.Add(newest);

        for (int entry = 2; entry <= newest.Number - 1; entry++)
        {
            CastleSessionInfo session = ReadSessionEntry(entry, cancellationToken);
            if (session.Pages > 0)
                sessions.Add(session);
        }
        return sessions;
    }

    /// <summary>The app's idle message pair, sent while the user picks a session.</summary>
    public void KeepAlive()
    {
        Exchange(Request(0x12, 0, 0), CancellationToken.None);
        Exchange(Request(0x01, 0, 0), CancellationToken.None);
    }

    public byte[] DownloadSession(CastleSessionInfo session, CancellationToken cancellationToken = default)
    {
        Exchange(Request(0x33, 0, 0), cancellationToken);
        Exchange(Request(0xD4, 0, 0, 0, 0, 0, 2), cancellationToken);
        Exchange(Request(0xD9, 0, 0, 0, 0, 1, 5), cancellationToken);
        Exchange(Request(0xD7, 0, 0, (byte)(session.Number >> 8), (byte)session.Number), cancellationToken);

        var log = new byte[session.Pages * (PageFrameLength - PageHeaderLength)];
        int blockCount = session.Pages + BlocksBeforeLog;
        for (int block = 0; block < blockCount; block++)
        {
            byte[] reply = Exchange(Request(0x31, (byte)(block >> 8), (byte)block), cancellationToken);
            if (block < BlocksBeforeLog)
                continue;
            if (reply.Length != PageFrameLength || reply[1] != 0x31)
                throw new InvalidDataException($"The Castle Link sent the wrong reply for log block {block}.");
            Array.Copy(reply, PageHeaderLength, log, (block - BlocksBeforeLog) * (PageFrameLength - PageHeaderLength),
                PageFrameLength - PageHeaderLength);
        }

        Exchange(Request(0xD4, 0, 0, 0, 0, 0, 1), cancellationToken);
        return log;
    }

    public void Dispose() => CloseTransport();

    private CastleLinkConnectResult ConnectToEsc(string portName, CancellationToken cancellationToken)
    {
        try
        {
            Exchange(Request(0x01, 0, 0), cancellationToken);
            Exchange(Request(0x12, 0, 0), cancellationToken);
            Exchange(Request(0xD3, 0, 0, 0, 0, 0, 1), cancellationToken);
            Exchange(Request(0xD5, 0, 0), cancellationToken);
            Exchange(Request(0xD3, 0, 0, 0, 0, 0, 2), cancellationToken);
            Exchange(Request(0x05, 0, 0), cancellationToken);
            Exchange(Request(0x09, 0, 0), cancellationToken);
            Exchange(Request(0xD5, 0, 0), cancellationToken);
            byte[] escReply = Exchange(Request(0x05, 0, 0), cancellationToken);
            Exchange(Request(0x09, 0, 0), cancellationToken);

            // Checked against the second reply, which is the one the app saw last.
            if (escReply.Skip(5).All(value => value == 0))
                return new CastleLinkConnectResult(CastleLinkConnectStatus.NoEsc, portName);
            EscFirmware = Encoding.ASCII.GetString(escReply, 5, escReply.Length - 5).TrimEnd('\0');

            Exchange(Request(0xD1, 0, 0, 0, 0, 1, 6), cancellationToken);
            _delay.Wait(SetupPause, cancellationToken);
            Exchange(Request(0xD4, 0, 0, 0, 0, 0, 2), cancellationToken);
            // These two reads are the ESC's tune: settings then curves. See Docs/Castle_Link_Tune_Read.md.
            var tune = new byte[(BootBlockCount + MemoryBlockCount) * TuneBlockLength];
            for (int block = 0; block < BootBlockCount; block++)
                CopyTuneBlock(Exchange(Request(0x06, 0, (byte)block), cancellationToken), 0x06, tune, block);
            for (int block = 0; block < MemoryBlockCount; block++)
                CopyTuneBlock(Exchange(Request(0x0E, (byte)(block >> 8), (byte)block), cancellationToken), 0x0E,
                    tune, BootBlockCount + block);
            TuneBytes = tune;
            Exchange(Request(0xD4, 0, 0, 0, 0, 0, 1), cancellationToken);
            _delay.Wait(ListPause, cancellationToken);
            return new CastleLinkConnectResult(CastleLinkConnectStatus.Connected, portName);
        }
        catch (CastleLinkStoppedRespondingException)
        {
            // The Link itself answered, so the silence is past it.
            return new CastleLinkConnectResult(CastleLinkConnectStatus.NoEsc, portName);
        }
    }

    private static void CopyTuneBlock(byte[] reply, byte command, byte[] tune, int index)
    {
        if (reply.Length != ShortFrameLength || reply[1] != command)
            throw new InvalidDataException("The Castle Link sent the wrong reply while reading the tune.");
        Array.Copy(reply, ShortFrameLength - TuneBlockLength, tune, index * TuneBlockLength, TuneBlockLength);
    }

    private CastleSessionInfo ReadSessionEntry(int entry, CancellationToken cancellationToken)
    {
        byte[] reply = Exchange(Request(0xD8, 0, 0, 0, (byte)entry), cancellationToken);
        if (reply.Length != ShortFrameLength || reply[1] != 0xD8)
            throw new InvalidDataException("The Castle Link sent the wrong reply for the session list.");
        int number = (reply[5] << 8) | reply[6];
        int pages = (reply[7] << 16) | (reply[8] << 8) | reply[9];
        return new CastleSessionInfo(number, pages);
    }

    /// <summary>A 13-byte request: unused bytes are FF and every byte adds up to 0.</summary>
    internal static byte[] Request(byte command, params byte[] arguments)
    {
        var frame = new byte[ShortFrameLength];
        Array.Fill(frame, (byte)0xFF);
        frame[0] = ShortFrame;
        frame[1] = command;
        arguments.CopyTo(frame, 3);
        frame[2] = 0;
        frame[2] = (byte)-frame.Sum(value => (int)value);
        return frame;
    }

    private byte[] Exchange(byte[] request, CancellationToken cancellationToken)
    {
        lock (_exchangeLock)
        {
            ICastleLinkTransport transport = _transport ??
                throw new InvalidOperationException("The Castle Link is not connected.");
            transport.Write(request);

            byte[] first = ReadRequired(transport, 1, cancellationToken);
            int length = first[0] switch
            {
                ShortFrame => ShortFrameLength,
                PageFrame => PageFrameLength,
                _ => throw new InvalidDataException("The Castle Link sent a message that was not recognised.")
            };
            byte[] rest = ReadRequired(transport, length - 1, cancellationToken);
            byte[] frame = first.Concat(rest).ToArray();
            if ((frame.Sum(value => (int)value) & 0xFF) != 0)
                throw new InvalidDataException("A message from the Castle Link failed its checksum.");
            return frame;
        }
    }

    private static byte[] ReadRequired(ICastleLinkTransport transport, int count, CancellationToken cancellationToken)
    {
        try
        {
            return transport.Read(count, ReplyTimeout, cancellationToken)
                ?? throw new CastleLinkStoppedRespondingException();
        }
        catch (Exception ex) when (ex is (IOException or UnauthorizedAccessException or InvalidOperationException)
                                   and not CastleLinkStoppedRespondingException)
        {
            throw new CastleLinkStoppedRespondingException();
        }
    }

    private void CloseTransport()
    {
        _transport?.Dispose();
        _transport = null;
    }
}

public sealed class WmiCastleLinkPortDiscovery : ICastleLinkPortDiscovery
{
    private static readonly Regex ComPort = new(@"\((COM\d+)\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public IReadOnlyList<string> FindCandidatePorts()
    {
        var ports = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var searcher = new ManagementObjectSearcher(
            "SELECT Name, DeviceID FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'");
        foreach (ManagementBaseObject device in searcher.Get())
        {
            string deviceId = Convert.ToString(device["DeviceID"], CultureInfo.InvariantCulture) ?? "";
            string name = Convert.ToString(device["Name"], CultureInfo.InvariantCulture) ?? "";
            if (!deviceId.Contains("VID_C410&PID_8370", StringComparison.OrdinalIgnoreCase))
                continue;
            Match match = ComPort.Match(name);
            if (match.Success)
                ports.Add(match.Groups[1].Value.ToUpperInvariant());
        }
        return ports.OrderBy(port => port, StringComparer.OrdinalIgnoreCase).ToList();
    }
}

public sealed class SerialCastleLinkTransport : ICastleLinkTransport
{
    private SerialPort? _port;

    public void Open(string portName)
    {
        // Captured settings: 9600 8N1 with both control lines off.
        _port = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One)
        {
            DtrEnable = false,
            RtsEnable = false
        };
        _port.Open();
    }

    public void DiscardInput() => Port().DiscardInBuffer();

    public void Write(byte[] data) => Port().Write(data, 0, data.Length);

    public byte[]? Read(int count, TimeSpan timeout, CancellationToken cancellationToken)
    {
        SerialPort port = Port();
        var buffer = new byte[count];
        int received = 0;
        var stopwatch = Stopwatch.StartNew();
        while (received < count)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (stopwatch.Elapsed >= timeout)
                return null;
            if (port.BytesToRead > 0)
                received += port.Read(buffer, received, Math.Min(count - received, port.BytesToRead));
            else
                cancellationToken.WaitHandle.WaitOne(1);
        }
        return buffer;
    }

    public void Dispose()
    {
        if (_port?.IsOpen == true)
            _port.Close();
        _port?.Dispose();
        _port = null;
    }

    private SerialPort Port() =>
        _port is { IsOpen: true } port ? port : throw new InvalidOperationException("Serial port is not open.");
}

public sealed class ThreadCastleLinkDelay : ICastleLinkDelay
{
    public void Wait(TimeSpan delay, CancellationToken cancellationToken)
    {
        cancellationToken.WaitHandle.WaitOne(delay);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
