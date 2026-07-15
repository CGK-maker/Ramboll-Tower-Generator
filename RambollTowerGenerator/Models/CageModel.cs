using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RambollTowerGenerator.Common;

namespace RambollTowerGenerator.Models
{
    public class CageModel
    {
        public CageType CageType { get; set; } 
        public double VerticalHeight { get; set; } 
        public int VerticalSegmentCount { get; set; }
    }
}
