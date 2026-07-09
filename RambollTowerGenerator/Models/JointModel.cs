using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RambollTowerGenerator.Models
{
    /// <summary>
    /// Model for L-parallel splice connections at segment joints
    /// </summary>
    public class JointModel
    {
        // Splice plate properties
        public string SplicePlateProfile { get; set; } = "PL10";
        public string SplicePlateMaterial { get; set; } = "STEEL_UNDEFINED";

        // Bolt properties
        public string BoltSize { get; set; } = "M16";
        public string BoltStandard { get; set; } = "8.8";
        public double BoltTolerance { get; set; } = 2.0;

        // Splice configuration
        public double SplicePlateLength { get; set; } = 400.0; // Length of splice plate (along leg)
        public double SplicePlateWidth { get; set; } = 100.0; // Width of splice plate (across leg face)
        public double SplicePlateThickness { get; set; } = 10.0; // Thickness of splice plate
        public double SplicePlateOverlap { get; set; } = 200.0; // Overlap on each side of gap
        public int BoltRows { get; set; } = 2; // Number of bolt rows
        public int BoltsPerRow { get; set; } = 2; // Number of bolts per row
        public double BoltSpacingX { get; set; } = 60.0; // Horizontal spacing between bolts
        public double BoltSpacingY { get; set; } = 80.0; // Vertical spacing between bolt rows
        public double EdgeDistance { get; set; } = 40.0; // Distance from plate edge to first bolt
    }
}
