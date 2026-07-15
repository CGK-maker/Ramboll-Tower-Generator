using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RambollTowerGenerator.Models
{
    public class GeometryModel
    {
        public int SegmentCount { get; set; }
        public double TotalHeight { get; set; }
        public double BaseWidth { get; set; }
        public double TopWidth { get; set; }
        public double SegmentGap { get; set; }
    }
}
