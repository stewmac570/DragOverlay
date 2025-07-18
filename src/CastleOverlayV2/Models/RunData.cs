// File: /src/Models/RunData.cs

using ScottPlot;
using System.Collections.Generic;

namespace CastleOverlayV2.Models
{
    /// <summary>
    /// Represents one Castle ESC log run.
    /// </summary>
    public class RunData
    {
        public string FileName { get; set; }
        public List<DataPoint> DataPoints { get; set; }

        public RunData()
        {
            DataPoints = new List<DataPoint>();
        }
    }

}
