using System;
using System.Collections.Generic;
using RambollTowerGenerator.Models;
using Tekla.Structures.Geometry3d;
using Tekla.Structures.Model;

namespace RambollTowerGenerator.Builders
{
    /// <summary>
    /// Builder for creating L-parallel splice connections at segment joints
    /// </summary>
    public class JointBuilder
    {
        private readonly CageLegGeometryBuilder _geometryBuilder;

        public JointBuilder()
        {
            _geometryBuilder = new CageLegGeometryBuilder();
        }

        /// <summary>
        /// Creates splice connections at all segment joints
        /// </summary>
        public void CreateSpliceConnections(Model model, TowerModel tower)
        {
            List<List<Point>> legNodes = _geometryBuilder.BuildLegNodes(tower);
            double segmentGap = tower.Geometry.SegmentGap;

            if (segmentGap <= 0.0)
            {
                return;
            }

            for (int legIndex = 0; legIndex < tower.LegCount; legIndex++)
            {
                List<Point> nodes = legNodes[legIndex];
                int segmentCount = nodes.Count - 1;

                for (int i = 1; i < segmentCount; i++)
                {
                    Point jointPoint = nodes[i];
                    Point previousPoint = nodes[i - 1];
                    Point nextPoint = nodes[i + 1];

                    GetSegmentEndpointsAtJoint(previousPoint, jointPoint, nextPoint, 
                        i - 1, i, segmentCount, segmentGap, 
                        out Point lowerSegmentEnd, out Point upperSegmentStart);

                    CreateSpliceConnection(model, tower, lowerSegmentEnd, upperSegmentStart, 
                        legIndex, i, previousPoint, nextPoint);
                }
            }

            model.CommitChanges();
        }

        private void GetSegmentEndpointsAtJoint(Point previous, Point joint, Point next, 
            int lowerSegmentIndex, int upperSegmentIndex, int totalSegments, double gap,
            out Point lowerEnd, out Point upperStart)
        {
            Vector lowerDirection = new Vector(joint.X - previous.X, joint.Y - previous.Y, joint.Z - previous.Z);
            Vector upperDirection = new Vector(next.X - joint.X, next.Y - joint.Y, next.Z - joint.Z);

            double lowerLength = lowerDirection.GetLength();
            double upperLength = upperDirection.GetLength();

            Vector lowerUnit = lowerLength > 0.001 ? lowerDirection.GetNormal() : new Vector(0, 0, 1);
            Vector upperUnit = upperLength > 0.001 ? upperDirection.GetNormal() : new Vector(0, 0, 1);

            double halfGap = gap / 2.0;

            lowerEnd = new Point(
                joint.X - lowerUnit.X * halfGap,
                joint.Y - lowerUnit.Y * halfGap,
                joint.Z - lowerUnit.Z * halfGap);

            upperStart = new Point(
                joint.X + upperUnit.X * halfGap,
                joint.Y + upperUnit.Y * halfGap,
                joint.Z + upperUnit.Z * halfGap);
        }

        private void CreateSpliceConnection(Model model, TowerModel tower, 
            Point lowerEnd, Point upperStart, int legIndex, int jointIndex,
            Point previousPoint, Point nextPoint)
        {
            Point centerPoint = new Point(
                (lowerEnd.X + upperStart.X) / 2.0,
                (lowerEnd.Y + upperStart.Y) / 2.0,
                (lowerEnd.Z + upperStart.Z) / 2.0);

            Vector axialDirection = new Vector(
                upperStart.X - lowerEnd.X,
                upperStart.Y - lowerEnd.Y,
                upperStart.Z - lowerEnd.Z);

            double gapDistance = axialDirection.GetLength();
            Vector axialUnit = gapDistance > 0.001 ? axialDirection.GetNormal() : new Vector(0, 0, 1);

            bool isVertical = Math.Abs(lowerEnd.X - upperStart.X) < 0.1 && 
                              Math.Abs(lowerEnd.Y - upperStart.Y) < 0.1;

            CoordinateSystem spliceCS = CalculateSpliceCoordinateSystem(
                centerPoint, axialUnit, isVertical, legIndex);

            double legWidth = GetLegProfileWidth(tower.Profile.LegProfile);

            double plateWidth = tower.Joint.SplicePlateWidth > 0
                ? tower.Joint.SplicePlateWidth
                : legWidth - 20.0;

            string plateProfile = "PL" + tower.Joint.SplicePlateThickness;

            CreateSplicePlate(model, plateProfile, tower, spliceCS, plateWidth, legWidth,
                legIndex, jointIndex, 1);
            CreateSplicePlate(model, plateProfile, tower, spliceCS, plateWidth, legWidth,
                legIndex, jointIndex, 2);

            CreateBoltGroup(model, tower, spliceCS, legWidth, 
                legIndex, jointIndex, centerPoint, axialUnit);
        }

        private CoordinateSystem CalculateSpliceCoordinateSystem(Point origin, 
            Vector axialDirection, bool isVertical, int legIndex)
        {
            Vector xAxis, yAxis, zAxis;

            zAxis = axialDirection;

            if (isVertical)
            {
                double angle = legIndex * (2.0 * Math.PI / 3.0);
                xAxis = new Vector(
                    Math.Cos(angle),
                    Math.Sin(angle),
                    0.0);
            }
            else
            {
                if (Math.Abs(axialDirection.X) < 0.001 && Math.Abs(axialDirection.Y) < 0.001)
                {
                    xAxis = new Vector(1, 0, 0);
                }
                else
                {
                    xAxis = new Vector(-axialDirection.Y, axialDirection.X, 0.0);
                }
            }

            xAxis = xAxis.GetNormal();
            yAxis = zAxis.Cross(xAxis).GetNormal();

            return new CoordinateSystem(origin, xAxis, yAxis);
        }

        private void CreateSplicePlate(Model model, string plateProfile, TowerModel tower, 
            CoordinateSystem spliceCS, double plateWidth, double legWidth,
            int legIndex, int jointIndex, int plateNumber)
        {
            ContourPlate plate = new ContourPlate();
            plate.Profile.ProfileString = plateProfile;
            plate.Material.MaterialString = tower.Joint.SplicePlateMaterial;
            plate.Class = "6";
            plate.Name = "SPLICE_PLATE";

            double plateLength = tower.Joint.SplicePlateLength;
            double halfLength = plateLength / 2.0;
            double halfWidth = plateWidth / 2.0;

            double legThk = GetLegThickness(tower.Profile.LegProfile);
            double centroidDist = GetCentroidDistance(legWidth, legThk);
            double plateOffset = (plateNumber == 1) ? centroidDist : -centroidDist;

            Vector plateNormal = spliceCS.AxisX;
            Vector acrossDir = spliceCS.AxisY;
            Vector alongDir = spliceCS.AxisX.Cross(spliceCS.AxisY).GetNormal();

            Contour contour = new Contour();
            contour.AddContourPoint(new ContourPoint(
                new Point(
                    spliceCS.Origin.X + plateNormal.X * plateOffset + acrossDir.X * (-halfWidth) + alongDir.X * (-halfLength),
                    spliceCS.Origin.Y + plateNormal.Y * plateOffset + acrossDir.Y * (-halfWidth) + alongDir.Y * (-halfLength),
                    spliceCS.Origin.Z + plateNormal.Z * plateOffset + acrossDir.Z * (-halfWidth) + alongDir.Z * (-halfLength)),
                null));

            contour.AddContourPoint(new ContourPoint(
                new Point(
                    spliceCS.Origin.X + plateNormal.X * plateOffset + acrossDir.X * (halfWidth) + alongDir.X * (-halfLength),
                    spliceCS.Origin.Y + plateNormal.Y * plateOffset + acrossDir.Y * (halfWidth) + alongDir.Y * (-halfLength),
                    spliceCS.Origin.Z + plateNormal.Z * plateOffset + acrossDir.Z * (halfWidth) + alongDir.Z * (-halfLength)),
                null));

            contour.AddContourPoint(new ContourPoint(
                new Point(
                    spliceCS.Origin.X + plateNormal.X * plateOffset + acrossDir.X * (halfWidth) + alongDir.X * (halfLength),
                    spliceCS.Origin.Y + plateNormal.Y * plateOffset + acrossDir.Y * (halfWidth) + alongDir.Y * (halfLength),
                    spliceCS.Origin.Z + plateNormal.Z * plateOffset + acrossDir.Z * (halfWidth) + alongDir.Z * (halfLength)),
                null));

            contour.AddContourPoint(new ContourPoint(
                new Point(
                    spliceCS.Origin.X + plateNormal.X * plateOffset + acrossDir.X * (-halfWidth) + alongDir.X * (halfLength),
                    spliceCS.Origin.Y + plateNormal.Y * plateOffset + acrossDir.Y * (-halfWidth) + alongDir.Y * (halfLength),
                    spliceCS.Origin.Z + plateNormal.Z * plateOffset + acrossDir.Z * (-halfWidth) + alongDir.Z * (halfLength)),
                null));

            plate.Contour = contour;
            plate.SetLabel($"SPLICE_LEG{legIndex}_J{jointIndex}_P{plateNumber}");
            plate.Insert();
        }

        private void CreateBoltGroup(Model model, TowerModel tower, 
            CoordinateSystem spliceCS, double legWidth,
            int legIndex, int jointIndex, 
            Point centerPoint, Vector axialUnit)
        {
            BoltArray boltArray = new BoltArray();

            double boltDiameter = ParseBoltSize(tower.Joint.BoltSize);
            boltArray.BoltSize = boltDiameter;
            boltArray.BoltStandard = tower.Joint.BoltStandard;
            boltArray.Tolerance = tower.Joint.BoltTolerance;
            boltArray.BoltType = BoltGroup.BoltTypeEnum.BOLT_TYPE_SITE;
            boltArray.CutLength = 100;
            boltArray.ThreadInMaterial = BoltGroup.BoltThreadInMaterialEnum.THREAD_IN_MATERIAL_YES;

            int boltRows = tower.Joint.BoltRows;
            int boltsPerRow = tower.Joint.BoltsPerRow;
            double spacingX = tower.Joint.BoltSpacingX;
            double spacingY = tower.Joint.BoltSpacingY;

            double totalWidth = (boltsPerRow - 1) * spacingX;
            double totalLength = (boltRows - 1) * spacingY;

            Vector plateNormal = spliceCS.AxisX;
            Vector acrossDir = spliceCS.AxisY;
            Vector alongDir = spliceCS.AxisX.Cross(spliceCS.AxisY).GetNormal();

            double startXOffset = -totalWidth / 2.0;
            double startYOffset = -totalLength / 2.0;

            Point firstBoltPos = new Point(
                centerPoint.X + acrossDir.X * startXOffset + alongDir.X * startYOffset,
                centerPoint.Y + acrossDir.Y * startXOffset + alongDir.Y * startYOffset,
                centerPoint.Z + acrossDir.Z * startXOffset + alongDir.Z * startYOffset);

            Point secondBoltPos = new Point(
                firstBoltPos.X + plateNormal.X * 100,
                firstBoltPos.Y + plateNormal.Y * 100,
                firstBoltPos.Z + plateNormal.Z * 100);

            boltArray.FirstPosition = firstBoltPos;
            boltArray.SecondPosition = secondBoltPos;

            for (int i = 0; i < boltsPerRow; i++)
            {
                if (i == 0)
                    boltArray.AddBoltDistX(0.0);
                else
                    boltArray.AddBoltDistX(spacingX);
            }

            for (int i = 0; i < boltRows; i++)
            {
                if (i == 0)
                    boltArray.AddBoltDistY(0.0);
                else
                    boltArray.AddBoltDistY(spacingY);
            }

            boltArray.Position.Depth = Position.DepthEnum.MIDDLE;
            boltArray.Position.Plane = Position.PlaneEnum.MIDDLE;
            boltArray.Position.Rotation = Position.RotationEnum.FRONT;

            boltArray.Insert();
        }

        private string GetContourPlateProfile(string profileString)
        {
            if (string.IsNullOrEmpty(profileString))
                return "PL10";

            string upper = profileString.Trim().ToUpperInvariant();

            if (upper.Contains("*"))
            {
                string[] parts = upper.Split('*');
                string last = parts[parts.Length - 1].Trim();
                if (last.StartsWith("PL"))
                    return last;
                return "PL" + last;
            }

            if (upper.StartsWith("PL"))
                return profileString;

            return "PL" + profileString;
        }

        private double GetLegProfileWidth(string profileString)
        {
            if (string.IsNullOrEmpty(profileString))
                return 100.0;

            try
            {
                string[] parts = profileString.Split('*');
                if (parts.Length >= 1)
                {
                    string widthPart = parts[0].Replace("BLL", "").Replace("L", "").Trim();
                    if (double.TryParse(widthPart, out double width))
                    {
                        return width;
                    }
                }
            }
            catch
            {
            }

            return 100.0;
        }

        private double ParseBoltSize(string boltSize)
        {
            if (string.IsNullOrEmpty(boltSize))
                return 16.0;

            try
            {
                string numericPart = boltSize.Replace("M", "").Replace("m", "").Trim();
                if (double.TryParse(numericPart, out double diameter))
                {
                    return diameter;
                }
            }
            catch
            {
            }

            return 16.0;
        }

        private bool IsLProfile(string profileString)
        {
            if (string.IsNullOrEmpty(profileString))
                return false;
            string upper = profileString.Trim().ToUpperInvariant();
            return upper.StartsWith("BLL") || upper.StartsWith("L") || upper.Contains("L");
        }

        private double GetLegThickness(string profileString)
        {
            if (string.IsNullOrEmpty(profileString))
                return 5.0;
            try
            {
                string[] parts = profileString.Split('*');
                if (parts.Length >= 3 && double.TryParse(parts[parts.Length - 1].Trim(), out double thickness))
                    return thickness;
            }
            catch { }
            return 5.0;
        }

        private double GetCentroidDistance(double legWidth, double legThickness)
        {
            if (legThickness <= 0 || legWidth <= 0)
                return 27.0;
            double b = legWidth;
            double t = legThickness;
            double c = (b * b + b * t - t * t) / (2.0 * (2.0 * b - t));
            return Math.Max(c, 1.0);
        }
    }
}
