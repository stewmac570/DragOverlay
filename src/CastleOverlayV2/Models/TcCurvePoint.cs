namespace CastleOverlayV2.Models;

/// <summary>One logger TC curve point: whole milliseconds after launch and whole motor RPM.</summary>
public sealed record TcCurvePoint(int TimeMs, int Rpm);
