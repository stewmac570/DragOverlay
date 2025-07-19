
namespace CastleOverlayV2.Models
{

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
