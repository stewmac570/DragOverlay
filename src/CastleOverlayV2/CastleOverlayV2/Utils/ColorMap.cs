using ScottPlot;

namespace CastleOverlayV2.Utils
{
    public static class ColorMap
    {
        public static ScottPlot.Color GetColor(int index)
        {
            // Replace with real Castle colors
            return index switch
            {
                0 => Colors.Blue,
                1 => Colors.Red,
                2 => Colors.Green,
                _ => Colors.Black
            };
        }
    }
}
