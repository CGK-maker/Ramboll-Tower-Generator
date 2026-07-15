using RambollTowerGenerator.Common;

namespace RambollTowerGenerator.Models
{
    public class BracingModel
    {
        public BracingType BracingType { get; set; } = BracingType.XBracing;

        public string DiagonalProfile { get; set; } 
        public string DiagonalMaterial { get; set; }

        public string HorizontalProfile { get; set; } 
        public string HorizontalMaterial { get; set; } 

        public int BracingLevels { get; set; }
    }
}
