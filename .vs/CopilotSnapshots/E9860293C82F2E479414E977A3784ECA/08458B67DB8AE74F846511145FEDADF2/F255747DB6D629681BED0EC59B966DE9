using System;
using System.Collections.Generic;
using RambollTowerGenerator.Models;
using Tekla.Structures.Model;
using Tekla.Structures.Geometry3d;

namespace RambollTowerGenerator.Builders
{
    public class LegBuilder
    {
        private readonly CageLegGeometryBuilder _cageLegGeometryBuilder;

        public LegBuilder()
        {
            _cageLegGeometryBuilder = new CageLegGeometryBuilder();
        }

        public void CreateLegs(Model model, TowerModel tower)
        {
            List<List<Point>> legNodes = _cageLegGeometryBuilder.BuildLegNodes(tower);
            double segmentGap = tower.Geometry.SegmentGap;

            for (int legIndex = 0; legIndex < tower.LegCount; legIndex++)
            {
                List<Point> nodes = legNodes[legIndex];
                int segmentCount = nodes.Count - 1;

                for (int i = 0; i < segmentCount; i++)
                {
                    Point start = nodes[i];
                    Point end = nodes[i + 1];

                    GetSegmentPointsWithGap(start, end, i, segmentCount, segmentGap, out Point beamStart, out Point beamEnd);

                    Beam leg = new Beam(beamStart, beamEnd);
                    leg.Name = "TOWER_LEG";
                    leg.Profile.ProfileString = tower.Profile.LegProfile;
                    leg.Material.MaterialString = tower.Profile.LegMaterial;
                    leg.Class = (i % 2 == 0) ? "2" : "3";

                    bool isVertical = Math.Abs(beamStart.X - beamEnd.X) < 0.1 && Math.Abs(beamStart.Y - beamEnd.Y) < 0.1;

                    if (isVertical)
                    {
                        ApplyCorrectedVerticalPosition(leg, legIndex);
                    }
                    else
                    {
                        leg.Position.Plane = Position.PlaneEnum.LEFT;
                        leg.Position.Depth = Position.DepthEnum.BEHIND;
                        leg.Position.Rotation = Position.RotationEnum.BELOW;
                        leg.Position.RotationOffset = -45;
                    }

                    leg.SetLabel("LEG_" + legIndex + "_" + i);
                    leg.Insert();
                }
            }
            model.CommitChanges();
        }

        private void GetSegmentPointsWithGap(Point start, Point end, int segmentIndex, int totalSegments, double gap, out Point adjustedStart, out Point adjustedEnd)
        {
            adjustedStart = start;
            adjustedEnd = end;

            if (gap <= 0.0 || totalSegments <= 1)
                return;

            Vector direction = new Vector(end.X - start.X, end.Y - start.Y, end.Z - start.Z);
            double length = direction.GetLength();
            if (length <= 0.001)
                return;

            Vector unit = direction.GetNormal();
            double halfGap = gap / 2.0;

            double startTrim = segmentIndex == 0 ? 0.0 : halfGap;
            double endTrim = segmentIndex == totalSegments - 1 ? 0.0 : halfGap;

            if (startTrim + endTrim >= length)
                return;

            adjustedStart = new Point(
                start.X + unit.X * startTrim,
                start.Y + unit.Y * startTrim,
                start.Z + unit.Z * startTrim);

            adjustedEnd = new Point(
                end.X - unit.X * endTrim,
                end.Y - unit.Y * endTrim,
                end.Z - unit.Z * endTrim);
        }

        private void ApplyCorrectedVerticalPosition(Beam leg, int legIndex)
        {
            // Mapping your UI values to API:
            // Vertical -> Plane (Right = DOWN , LEFT= up)
            // Horizontal   -> Depth (Left=FRONT, RIGHT=BEHIND)
            // Rotation   -> Rotation (Below/Front=BELOW, Back/Down=TOP)

            switch (legIndex)
            {
                case 0: // Leg 1: Vert=Up, Rot=Below, Offset=0, Hor=Right
                    leg.Position.Plane = Position.PlaneEnum.RIGHT;
                    leg.Position.Rotation = Position.RotationEnum.TOP;
                    leg.Position.RotationOffset = 0;
                    leg.Position.Depth = Position.DepthEnum.FRONT;

                    break;

                case 1: // Leg 2: Vert=Up, Rot=Front, Offset=0, Hor=Left
                    leg.Position.Plane = Position.PlaneEnum.RIGHT;
                    leg.Position.Rotation = Position.RotationEnum.BACK;
                    leg.Position.RotationOffset = 0;
                    leg.Position.Depth = Position.DepthEnum.BEHIND;

                    break;

                case 2: // Leg 3: Vert=Top(Up), Rot=Down, Offset=0, Hor=Left
                    leg.Position.Plane = Position.PlaneEnum.LEFT;
                    leg.Position.Rotation = Position.RotationEnum.BELOW;
                    leg.Position.RotationOffset = 0;
                    leg.Position.Depth = Position.DepthEnum.BEHIND;
                    break;

                case 3: // Leg 4: Vert=Down, Rot=Back, Offset=0, Hor=Right
                    leg.Position.Plane = Position.PlaneEnum.LEFT;
                    leg.Position.Rotation = Position.RotationEnum.FRONT;
                    leg.Position.RotationOffset = 0;
                    leg.Position.Depth = Position.DepthEnum.FRONT;

                    break;
            }
        }
    }
}