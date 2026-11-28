using System.Buffers.Binary;
using System.Globalization;

namespace CastleOverlayV2.Services;

/// <summary>
/// Rebuilds a Big logger run's CSV from its saved files (the logger's <c>raw N</c>: run_N.bin,
/// .slo and .gps), line for line the same as the logger's own <c>dump N</c>. Sending the saved
/// files is far faster: run 14 on 28 Nov 2026 was 827 KB stored but 22 MB as CSV, 21 minutes over
/// WiFi. Mirrors dumpRunFile() in rc-logger firmware/big_logger/big_logger.ino and
/// tools/big_logger_rebuild.py, including the logger's single-precision sums. Change all three together.
/// </summary>
public static class BigLoggerRawRebuild
{
    private const uint SampleUs = 1000;
    private const uint SignalLostUs = 100000;
    private const uint SpeedTimeoutUs = 500000;
    private const uint GpsStaleUs = 200000;
    private const int OldSlowHz = 200;
    private const ushort NoLaser = 0xFFFF;
    private const uint TimeMask = 0x3FFFFFFF;
    private const float PackScale = 3.2f;
    private const float MpsToMph = 2.23694f;

    public const string Header =
        "Time (s),Throttle (ms),Motor RPM,Wheel (rev/s),Pack Volts (V),Accel Fwd (g),Pitch (deg),Roll (deg)," +
        "Front Laser (mm),Rear Laser (mm)," +
        "GPS Speed (mph),GPS Speed Error (mph),GPS Sats,Latitude,Longitude,GPS Heading (deg)";

    private static readonly (float Raw, float Mm)[] FrontTable =
        { (13.7f, 2), (15.9f, 4), (18.4f, 6), (22.4f, 8), (23.7f, 10), (45.2f, 30), (54.5f, 40) };
    private static readonly (float Raw, float Mm)[] RearTable =
        { (14.5f, 2), (17.4f, 4), (19.1f, 6), (22.2f, 8), (24.0f, 10), (31.4f, 14), (45.5f, 30), (55.6f, 40) };

    private readonly record struct Slow(uint TUs, short AccelFwd, short Pitch, short Roll, ushort PackMv, ushort Front, ushort Rear);
    private readonly record struct Gps(uint TUs, int Lat, int Lon, int SpeedMms, int Heading, ushort SpeedAccMms, byte Sats, byte FixOk);

    private sealed class Edges
    {
        public readonly uint[] T = new uint[9];
        public int Idx;
        public uint Count;

        public void Record(int n, uint now)
        {
            int i = Idx + 1;
            if (i > n) i = 0;
            Idx = i;
            T[i] = now;
            Count++;
        }
    }

    private sealed class Smoother
    {
        private readonly int _size;
        private readonly ushort[] _v = new ushort[50];
        private int _count, _next;
        private uint _sum;

        public Smoother(int slowHz) => _size = Math.Clamp(50 * slowHz / 1000, 1, 50);

        public void Add(ushort mm)
        {
            if (mm == NoLaser) return;
            if (_count == _size) _sum -= _v[_next]; else _count++;
            _v[_next] = mm;
            _sum += mm;
            _next = (_next + 1) % _size;
        }

        public float Average() => _count > 0 ? (float)_sum / (float)_count : -1.0f;
    }

    private static float RevsPerSec(uint count, uint last, uint first, int n, uint now)
    {
        if (count <= (uint)n) return 0;
        uint since = (int)(now - last) < 0 ? 0 : now - last;
        if (since > SpeedTimeoutUs) return 0;
        uint period = last - first;
        uint stretched = since * (uint)n;
        if (stretched > period) period = stretched;
        if (period == 0) return 0;
        return 1000000.0f / (float)period;
    }

    private static float LaserHeight(float raw, (float Raw, float Mm)[] t)
    {
        int i = 0;
        while (i < t.Length - 2 && raw > t[i + 1].Raw) i++;
        var a = t[i];
        var b = t[i + 1];
        float d = raw - a.Raw;
        float span = b.Mm - a.Mm;
        float prod = d * span;
        float width = b.Raw - a.Raw;
        float q = prod / width;
        return a.Mm + q;
    }

    private static bool Due(uint t, uint now) => (int)(t - now) <= 0;

    private static readonly ulong[] Pow10 = { 1, 10, 100, 1000, 10000, 100000, 1000000, 10000000 };

    /// <summary>
    /// printf("%.Nf") on the Nano: the exact binary value, rounded half to even, and "-" kept
    /// for a negative value that rounds to zero ("-0.00"). .NET's "F" rounds half away from zero.
    /// </summary>
    public static string F(double v, int places)
    {
        bool negative = double.IsNegative(v);
        long bits = BitConverter.DoubleToInt64Bits(Math.Abs(v));
        int exp = (int)((bits >> 52) & 0x7FF);
        ulong mant = (ulong)bits & 0xFFFFFFFFFFFFFUL;
        if (exp == 0) exp = 1; else mant |= 1UL << 52;
        int e = exp - 1075;                       // value = mant * 2^e
        UInt128 scaled = (UInt128)mant * Pow10[places];
        UInt128 q;
        if (e >= 0)
        {
            q = scaled << e;
        }
        else
        {
            int k = -e;
            if (k > 127)
            {
                q = 0;                            // far below the last digit: rounds to zero
            }
            else
            {
                q = scaled >> k;
                UInt128 rem = scaled - (q << k);
                UInt128 half = (UInt128)1 << (k - 1);
                if (rem > half || (rem == half && (q & 1) == 1)) q++;
            }
        }
        string digits = q.ToString(CultureInfo.InvariantCulture);
        if (places > 0)
        {
            if (digits.Length <= places) digits = new string('0', places - digits.Length + 1) + digits;
            digits = digits[..^places] + "." + digits[^places..];
        }
        return negative ? "-" + digits : digits;
    }

    /// <summary>Writes the CSV (header line first). Returns the number of data rows.</summary>
    public static long Write(byte[] bin, byte[] slo, byte[] gps, TextWriter writer, CancellationToken cancellationToken = default)
    {
        if (bin.Length < 24) throw new InvalidDataException("The run file is too short.");
        string magic = System.Text.Encoding.ASCII.GetString(bin, 0, 4);
        if (magic != "BLG2" && magic != "BLG1") throw new InvalidDataException("Not a Big logger run file.");
        bool v1 = magic == "BLG1";
        ulong startUs = BinaryPrimitives.ReadUInt64LittleEndian(bin.AsSpan(8));
        int wheelMagnets = bin[16];
        int polePairs = bin[17];
        int slowHz = BinaryPrimitives.ReadUInt16LittleEndian(bin.AsSpan(18));
        if (slowHz == 0) slowHz = OldSlowHz;

        var events = new List<(uint Type, ulong T)>();
        int pos = 24;
        while (pos + 16 <= bin.Length)
        {
            uint count = BinaryPrimitives.ReadUInt32LittleEndian(bin.AsSpan(pos));
            ulong baseUs = BinaryPrimitives.ReadUInt64LittleEndian(bin.AsSpan(pos + 8));
            pos += 16;
            for (uint k = 0; k < count && pos + 4 <= bin.Length; k++)
            {
                uint ev = BinaryPrimitives.ReadUInt32LittleEndian(bin.AsSpan(pos));
                pos += 4;
                ulong t = baseUs - ((((uint)baseUs) - (ev & TimeMask)) & TimeMask);
                events.Add((ev >> 30, t));
            }
        }

        var slow = new List<Slow>();
        if (v1)
        {
            for (int o = 0; o + 24 <= slo.Length; o += 24)
            {
                var s = slo.AsSpan(o);
                short a0 = BinaryPrimitives.ReadInt16LittleEndian(s[4..]);
                short a1 = BinaryPrimitives.ReadInt16LittleEndian(s[6..]);
                short a2 = BinaryPrimitives.ReadInt16LittleEndian(s[8..]);
                // RAD_TO_DEG is a double in Arduino, so these sums are in double after atan2f.
                const double radToDeg = 57.295779513082320876798154814105;
                short pitch = (short)Math.Round(MathF.Atan2(a0, a2) * radToDeg * 100.0, MidpointRounding.AwayFromZero);
                short roll = (short)Math.Round(MathF.Atan2(a1, a2) * radToDeg * 100.0, MidpointRounding.AwayFromZero);
                slow.Add(new Slow(BinaryPrimitives.ReadUInt32LittleEndian(s), a0, pitch, roll,
                    BinaryPrimitives.ReadUInt16LittleEndian(s[16..]),
                    BinaryPrimitives.ReadUInt16LittleEndian(s[18..]),
                    BinaryPrimitives.ReadUInt16LittleEndian(s[20..])));
            }
        }
        else
        {
            for (int o = 0; o + 16 <= slo.Length; o += 16)
            {
                var s = slo.AsSpan(o);
                slow.Add(new Slow(BinaryPrimitives.ReadUInt32LittleEndian(s),
                    BinaryPrimitives.ReadInt16LittleEndian(s[4..]),
                    BinaryPrimitives.ReadInt16LittleEndian(s[6..]),
                    BinaryPrimitives.ReadInt16LittleEndian(s[8..]),
                    BinaryPrimitives.ReadUInt16LittleEndian(s[10..]),
                    BinaryPrimitives.ReadUInt16LittleEndian(s[12..]),
                    BinaryPrimitives.ReadUInt16LittleEndian(s[14..])));
            }
        }

        var fixes = new List<Gps>();
        for (int o = 0; o + 24 <= gps.Length; o += 24)
        {
            var g = gps.AsSpan(o);
            fixes.Add(new Gps(BinaryPrimitives.ReadUInt32LittleEndian(g),
                BinaryPrimitives.ReadInt32LittleEndian(g[4..]),
                BinaryPrimitives.ReadInt32LittleEndian(g[8..]),
                BinaryPrimitives.ReadInt32LittleEndian(g[12..]),
                BinaryPrimitives.ReadInt32LittleEndian(g[16..]),
                BinaryPrimitives.ReadUInt16LittleEndian(g[20..]),
                g[22], g[23]));
        }

        writer.WriteLine(Header);
        var w = new Edges();
        var m = new Edges();
        uint rise = 0, pulse = 0, pulseAt = 0;
        int ei = 0, si = 0, gi = 0;
        bool have = events.Count > 0;
        ulong lastEventUs = startUs;
        Slow cur = default;
        bool haveCur = false;
        var front = new Smoother(slowHz);
        var rear = new Smoother(slowHz);
        Gps gpsCur = default;
        bool haveGpsCur = false;
        long rows = 0;
        var line = new System.Text.StringBuilder(160);

        for (ulong tick = startUs; have || si < slow.Count || tick <= lastEventUs; tick += SampleUs)
        {
            if ((rows & 0xFFFF) == 0) cancellationToken.ThrowIfCancellationRequested();
            while (have && events[ei].T <= tick)
            {
                var (type, t) = events[ei];
                uint t32 = (uint)t;
                if (type == 0) w.Record(wheelMagnets, t32);
                else if (type == 1) m.Record(polePairs, t32);
                else if (type == 2) rise = t32;
                else
                {
                    uint pw = t32 - rise;
                    if (pw > 500 && pw < 2600) { pulse = pw; pulseAt = t32; }
                }
                lastEventUs = t;
                ei++;
                have = ei < events.Count;
            }
            uint now = (uint)tick;
            while (si < slow.Count && Due(slow[si].TUs, now))
            {
                cur = slow[si];
                haveCur = true;
                front.Add(cur.Front);
                rear.Add(cur.Rear);
                si++;
            }
            while (gi < fixes.Count && Due(fixes[gi].TUs, now))
            {
                gpsCur = fixes[gi];
                haveGpsCur = true;
                gi++;
            }

            float wr = RevsPerSec(w.Count, w.T[w.Idx], w.T[w.Idx == wheelMagnets ? 0 : w.Idx + 1], wheelMagnets, now);
            float mr = RevsPerSec(m.Count, m.T[m.Idx], m.T[m.Idx == polePairs ? 0 : m.Idx + 1], polePairs, now);
            uint thr = (pulse != 0 && now - pulseAt <= SignalLostUs) ? pulse : 0;
            float rpmF = mr * 60.0f;
            rpmF += 0.5f;
            uint rpm = (uint)rpmF;
            double secs = (tick - startUs) / 1000000.0;
            float thrMs = (float)thr / 1000.0f;

            line.Clear();
            line.Append(F(secs, 3)).Append(',').Append(F(thrMs, 3)).Append(',')
                .Append(rpm.ToString(CultureInfo.InvariantCulture)).Append(',').Append(F(wr, 2));
            if (haveCur)
            {
                float packV = (float)cur.PackMv / 1000.0f;
                packV *= PackScale;
                line.Append(',').Append(F(packV, 2))
                    .Append(',').Append(F((float)cur.AccelFwd / 1000.0f, 3))
                    .Append(',').Append(F((float)cur.Pitch / 100.0f, 2))
                    .Append(',').Append(F((float)cur.Roll / 100.0f, 2));
            }
            else
            {
                line.Append(",,,,");
            }
            foreach (var (sm, table) in new[] { (front, FrontTable), (rear, RearTable) })
            {
                float raw = sm.Average();
                if (raw < 0) line.Append(',');
                else line.Append(',').Append(F(LaserHeight(raw, table), 1));
            }

            bool fresh = haveGpsCur && now - gpsCur.TUs <= GpsStaleUs;
            if (fresh && gpsCur.FixOk != 0)
            {
                float speed = (float)gpsCur.SpeedMms / 1000.0f;
                speed *= MpsToMph;
                float acc = (float)gpsCur.SpeedAccMms / 1000.0f;
                acc *= MpsToMph;
                line.Append(',').Append(F(speed, 2)).Append(',').Append(F(acc, 2))
                    .Append(',').Append(gpsCur.Sats.ToString(CultureInfo.InvariantCulture))
                    .Append(',').Append(F(gpsCur.Lat / 1e7, 7)).Append(',').Append(F(gpsCur.Lon / 1e7, 7))
                    .Append(',').Append(F(gpsCur.Heading / 1e5, 1));
            }
            else if (fresh)
            {
                line.Append(",,,").Append(gpsCur.Sats.ToString(CultureInfo.InvariantCulture)).Append(",,,");
            }
            else
            {
                line.Append(",,,,,,");
            }
            writer.WriteLine(line);
            rows++;
        }
        return rows;
    }
}
