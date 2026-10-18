using System.Collections.Generic;

namespace CastleOverlayV2.Utils
{
    /// <summary>Display names, number formats and list order for channels.</summary>
    public static class ChannelText
    {
        /// <summary>Channel order used by the cursor box.</summary>
        public static readonly IReadOnlyList<string> Order = new[]
        {
            "RPM", "Throttle %", "Voltage", "Current", "Ripple", "PowerOut",
            "MotorTemp", "ESC Temp", "MotorTiming", "Acceleration",
            "RaceBox Speed", "RaceBox Distance", "RaceBox G-Force X",
            "Logger Throttle %", "Logger RPM", "Logger Wheel Speed", "Logger Wheel rev/s",
        };

        public static string DisplayName(string channelName) => channelName switch
        {
            "PowerOut" => "Power Out",
            "MotorTemp" => "Motor Temp",
            "MotorTiming" => "Motor Timing",
            "RaceBox Speed" => "Speed",
            "RaceBox Distance" => "Distance",
            "RaceBox G-Force X" => "G-Force X",
            "Logger Throttle %" => "Throttle %",
            "Logger RPM" => "RPM",
            "Logger Wheel Speed" => "Wheel Speed",
            "Logger Wheel rev/s" => "Wheel rev/s",
            _ => channelName
        };

        public static string Format(string channelName, double v) => channelName switch
        {
            "RPM" => v.ToString("N0"),
            "Throttle %" => v.ToString("F0"),
            "PowerOut" => v.ToString("F0"),
            "ESC Temp" => v.ToString("F0"),
            "MotorTemp" => v.ToString("F0"),
            "MotorTiming" => v.ToString("F0"),
            "Voltage" => v.ToString("F2"),
            "Ripple" => v.ToString("F2"),
            "Current" => v.ToString("F1"),
            "Acceleration" => v.ToString("F2"),
            "RaceBox Speed" => v.ToString("F1"),
            "RaceBox Distance" => v.ToString("F0"),
            "RaceBox G-Force X" => v.ToString("F2"),
            "Logger Throttle %" => v.ToString("F0"),
            "Logger RPM" => v.ToString("N0"),
            "Logger Wheel Speed" => v.ToString("F1"),
            "Logger Wheel rev/s" => v.ToString("F1"),
            _ => v.ToString("F2"),
        };
    }
}
