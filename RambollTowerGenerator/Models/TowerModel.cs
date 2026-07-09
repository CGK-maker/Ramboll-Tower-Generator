using System.Collections.Generic;
using RambollTowerGenerator.Common;
using Tekla.Structures.Geometry3d;
using Tekla.Structures.Model;

namespace RambollTowerGenerator.Models
{
    public class TowerModel
    {
        public TowerType TowerType { get; set; } = TowerType.ThreeLeg;
        public Point BasePoint { get; set; }
        public GeometryModel Geometry { get; }
        public BracingModel Bracing { get; }
        public ProfileModel Profile { get; }
        public JointModel Joint { get; }
        public CageModel Cage { get; }

        public List<List<Beam>> LegBeams { get; }

        public int LegCount
        {
            get { return TowerType == TowerType.ThreeLeg ? 3 : 4; }
        }

        public TowerModel()
        {
            Geometry = new GeometryModel();
            Bracing = new BracingModel();
            Profile = new ProfileModel();
            Joint = new JointModel();
            Cage = new CageModel();
            LegBeams = new List<List<Beam>>();
        }
    }
}
