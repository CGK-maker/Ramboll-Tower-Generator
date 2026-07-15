using RambollTowerGenerator.Common;
using RambollTowerGenerator.Models;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Tekla.Structures.Geometry3d;
using Tekla.Structures.Model;
using static Tekla.Structures.Model.ModelObject;

namespace RambollTowerGenerator.Builders
{
    public class BracingBuilder
    {
        private readonly CageLegGeometryBuilder _geometryBuilder;

        public BracingBuilder()
        {
            _geometryBuilder = new CageLegGeometryBuilder();
        }

        public void CreateBracing(Model model, TowerModel tower)
        {
            if (tower.Bracing.BracingType == BracingType.None)
                return;

            List<List<Point>> legNodes = _geometryBuilder.BuildLegNodes(tower);

            switch (tower.Bracing.BracingType)
            {
                case BracingType.XBracing:
                    CreateXBracing(tower, legNodes);
                    break;
            }

            model.CommitChanges();
        }

        private void CreateXBracing(TowerModel tower, List<List<Point>> legNodes)
        {
            int bracingLevels = tower.Bracing.BracingLevels;
            int legCount = tower.LegCount;

            if (bracingLevels < 1 || legNodes.Count == 0 || legNodes[0].Count < 2)
                return;

            // 1. Calculate height steps
            double zStart = legNodes[0][0].Z;
            double zEnd = legNodes[0][legNodes[0].Count - 1].Z;
            double totalHeight = zEnd - zStart;
            if (totalHeight <= 0.0) return;

            double levelHeight = totalHeight / bracingLevels;

            // 2. Determine how much to shorten the brace (Half of the leg width)
            double legWidth = GetWidthFromProfile(tower.Profile.LegProfile);
            double halfLeg = legWidth / 2.0;

            for (int level = 0; level < bracingLevels; level++)
            {
                double levelBottomZ = zStart + (level * levelHeight);
                double levelTopZ = (level == bracingLevels - 1) ? zEnd : zStart + ((level + 1) * levelHeight);
                double levelMidZ = (levelBottomZ + levelTopZ) / 2.0;

                for (int faceIndex = 0; faceIndex < legCount; faceIndex++)
                {
                    int leg1Index = faceIndex;
                    int leg2Index = (faceIndex + 1) % legCount;

                    // 1. FIND THE LEG BEAMS FROM THE MODEL
                    // These were created by your LegBuilder. We need them to create the bolts.
                    Beam legLeft = FindLegInModel(leg1Index, level);
                    Beam legRight = FindLegInModel(leg2Index, level);

                    // GET RAW CORNER POINTS (The edges of the tower)
                    Point pBL_Edge = GetPointAtZ(legNodes[leg1Index], levelBottomZ);
                    Point pTL_Edge = GetPointAtZ(legNodes[leg1Index], levelTopZ);
                    Point pBR_Edge = GetPointAtZ(legNodes[leg2Index], levelBottomZ);
                    Point pTR_Edge = GetPointAtZ(legNodes[leg2Index], levelTopZ);

                    // MATHEMATICAL REDUCTION OF LENGTH
                    // We move the points along the face of the tower towards the other leg

                    // Vector from Left Leg to Right Leg (along the face)
                    Vector vLtoR = new Vector(pBR_Edge.X - pBL_Edge.X, pBR_Edge.Y - pBL_Edge.Y, 0);
                    vLtoR.Normalize();

                    // Vector from Right Leg to Left Leg
                    Vector vRtoL = new Vector(pBL_Edge.X - pBR_Edge.X, pBL_Edge.Y - pBR_Edge.Y, 0);
                    vRtoL.Normalize();

                    // Outward face normal (perpendicular to the face, pointing away from tower center)
                    // This is direction-agnostic: it resolves to +Y, -Y, +X, or -X depending on the face.
                    Vector outwardNormal = GetOutwardFaceNormal(legNodes, leg1Index, leg2Index, levelBottomZ);

                    // New Work Points (Shifted inward to the center of the leg profile)
                    Point bottomLeft  = new Point(pBL_Edge.X + (vLtoR.X * halfLeg), pBL_Edge.Y + (vLtoR.Y * halfLeg), pBL_Edge.Z);
                    Point topLeft     = new Point(pTL_Edge.X + (vLtoR.X * halfLeg), pTL_Edge.Y + (vLtoR.Y * halfLeg), pTL_Edge.Z);
                    Point bottomRight = new Point(pBR_Edge.X + (vRtoL.X * halfLeg), pBR_Edge.Y + (vRtoL.Y * halfLeg), pBR_Edge.Z);
                    Point topRight    = new Point(pTR_Edge.X + (vRtoL.X * halfLeg), pTR_Edge.Y + (vRtoL.Y * halfLeg), pTR_Edge.Z);

                    double legThickness  = GetThicknessFromProfile(tower.Profile.LegProfile);
                    double diagThickness = GetThicknessFromProfile(tower.Bracing.DiagonalProfile);
                    double diagWidth     = GetWidthFromProfile(tower.Bracing.DiagonalProfile);

                    // Shift diagonal2's top-left end BACKWARDS (outward-negative = into the tower face)
                    // by the leg profile thickness so diagonal2 sits behind the leg flange.
                    // "Backward" = -outwardNormal, and its actual axis (X / -X / Y / -Y) depends on the face.
                    Point topLeftShifted = new Point(
                        topLeft.X - (outwardNormal.X * legThickness),
                        topLeft.Y - (outwardNormal.Y * legThickness),
                        topLeft.Z);

                    Point bottomRightShifted = new Point(
                        bottomRight.X - (outwardNormal.X * legThickness),
                        bottomRight.Y - (outwardNormal.Y * legThickness),
                        bottomRight.Z);

                    // Create diagonal1 (/)
                    Beam diagonal1 = CreateBracingBeam(
                        bottomLeft,
                        topRight,
                        tower.Bracing.DiagonalProfile,
                        tower.Bracing.DiagonalMaterial,
                        $"X_DIAG_L{level}_F{faceIndex}_1",
                        legThickness + diagThickness / 2.0, diagWidth / 2.0,
                        true);
                    diagonal1.Insert();

                    // Create diagonal2 (\) — endpoint shifted backwards by leg thickness (5 mm)
                    Beam diagonal2 = CreateBracingBeam(
                        bottomRightShifted,
                        topLeftShifted,
                        tower.Bracing.DiagonalProfile,
                        tower.Bracing.DiagonalMaterial,
                        $"X_DIAG_L{level}_F{faceIndex}_2",
                        diagThickness / 2.0, diagWidth / 2.0,
                        false);
                    diagonal2.Insert();

                    // Bolt at Bottom Right Leg (Leg + Diagonal 2)
                    // IMPORTANT (Tekla BoltArray orientation):
                    //   FirstPosition -> SecondPosition defines the X-AXIS of the bolt PLANE
                    //   (the gauge/distribution line). The bolt SHAFT runs along the plane
                    //   NORMAL = (SecondPosition - FirstPosition) x up.
                    //   So to make the shaft point along the outward face normal (the X axis
                    //   through the leg), the two positions must run ALONG THE FACE (tangent),
                    //   NOT along the outward normal. Position.Rotation then just flips +X/-X.
                    Vector faceTangent = new Vector(vLtoR.X, vLtoR.Y, 0);
                    faceTangent.Normalize();

                    Point boltFirst  = bottomRightShifted;
                    Point boltSecond = new Point(
                        boltFirst.X + (faceTangent.X * 20.0),
                        boltFirst.Y + (faceTangent.Y * 20.0),
                        boltFirst.Z);

                    CreateTripleBolt(diagonal1, diagonal2, null, boltFirst, boltSecond, diagWidth);

                    Vector faceTangent1 = new Vector(vRtoL.X, vRtoL.Y, 0);
                    faceTangent.Normalize();

                    Point boltFirst1 = topLeftShifted;
                    Point boltSecond1 = new Point(
                        boltFirst1.X + (faceTangent1.X * 20.0),
                        boltFirst1.Y + (faceTangent1.Y * 20.0),
                        boltFirst1.Z);

                    CreateTripleBolt(diagonal1, diagonal2, null, boltFirst1, boltSecond1, diagWidth);
                }

                CreateHorizontalBracingRingAtZ(tower, legNodes, levelMidZ, level, halfLeg);
            }
        }

        private void CreateHorizontalBracingRingAtZ(TowerModel tower, List<List<Point>> legNodes, double z, int level, double halfLeg)
        {
            int legCount = tower.LegCount;
            for (int faceIndex = 0; faceIndex < legCount; faceIndex++)
            {
                int leg1Index = faceIndex;
                int leg2Index = (faceIndex + 1) % legCount;

                Point p1Edge = GetPointAtZ(legNodes[leg1Index], z);
                Point p2Edge = GetPointAtZ(legNodes[leg2Index], z);

                // Shorten horizontal beams to start/end at leg centers
                Vector v1to2 = new Vector(p2Edge.X - p1Edge.X, p2Edge.Y - p1Edge.Y, 0);
                v1to2.Normalize();
                Vector v2to1 = new Vector(p1Edge.X - p2Edge.X, p1Edge.Y - p2Edge.Y, 0);
                v2to1.Normalize();

                Point p1 = new Point(p1Edge.X + (v1to2.X * halfLeg), p1Edge.Y + (v1to2.Y * halfLeg), p1Edge.Z);
                Point p2 = new Point(p2Edge.X + (v2to1.X * halfLeg), p2Edge.Y + (v2to1.Y * halfLeg), p2Edge.Z);

                Point midPoint = new Point((p1.X + p2.X) / 2.0, (p1.Y + p2.Y) / 2.0, (p1.Z + p2.Z) / 2.0);

                double legThickness1 = GetThicknessFromProfile(tower.Profile.LegProfile);

                Beam horizontal1 = CreateHorizontalBracingBeam(p1, midPoint, tower.Bracing.HorizontalProfile, tower.Bracing.HorizontalMaterial, $"X_HORIZ_M{level}_F{faceIndex}_R2L", legThickness1, true);
                horizontal1.Insert();

                Beam horizontal2 = CreateHorizontalBracingBeam(midPoint, p2, tower.Bracing.HorizontalProfile, tower.Bracing.HorizontalMaterial, $"X_HORIZ_M{level}_F{faceIndex}_L2R", legThickness1, false);
                horizontal2.Insert();
            }
        }

        private void CreateTripleBolt(Part leg, Part brace1, Part brace2, Point origin, Point direction, double braceWidth)
        {
            if (leg == null || brace1 == null) return;

            BoltArray boltArray = new BoltArray();
            boltArray.PartToBoltTo = leg;
            boltArray.PartToBeBolted = brace1;

            // If a second bracing is provided at the same point, add it
            if (brace2 != null)
                boltArray.AddOtherPartToBolt(brace2);

            boltArray.FirstPosition = origin;
            boltArray.SecondPosition = direction;

            // Spacing (From your BoltArray decompile)
            boltArray.AddBoltDistX(0.0);
            boltArray.AddBoltDistY(0.0);

            // Bolt Properties (From your BoltGroup decompile)
            boltArray.BoltSize = 16.0;
            boltArray.BoltStandard = "8.8XOX";
            boltArray.Tolerance = 2.0;
            boltArray.CutLength = 50.0; // Large enough for Leg + 2 Braces
            boltArray.ExtraLength = 50.0;

            boltArray.Bolt = true;
            boltArray.Washer1 = true;
            boltArray.Nut1 = true;
            boltArray.Hole1 = true;
            boltArray.Hole2 = true;

            // Gauge Line positioning
            //double gaugeLine = braceWidth * 0.55;
            //boltArray.Position.PlaneOffset = -gaugeLine;

            // Shaft now runs along the plane normal (the outward face / X axis) because
            // FirstPosition->SecondPosition is the face tangent. Use FRONT/BACK to choose
            // which side (+X or -X) the bolt points. FRONT/BACK flip the normal; TOP/BELOW
            // would only spin the bolt around its own shaft (the effect you saw before).
            boltArray.Position.Rotation = Position.RotationEnum.BELOW;

            boltArray.Insert();

           // if (!boltArray.Insert())
            //{
            //   boltArray.Position.Rotation = Position.RotationEnum.BACK;
           //     boltArray.Insert();
           // }
        }

        private Beam FindLegInModel(int legIndex, int level)
        {
            Model model = new Model();
            // We search for the specific label your LegBuilder assigns
            string targetLabel = "LEG_" + legIndex + "_" + level;

            ModelObjectEnumerator enumerator = model.GetModelObjectSelector().GetAllObjectsWithType(ModelObjectEnum.BEAM);
            while (enumerator.MoveNext())
            {
                Beam beam = enumerator.Current as Beam;
                if (beam != null && beam.Name == "TOWER_LEG")
                {
                    // Check if this beam matches the level and index
                    // Note: This matches the "LEG_0_1" format used in your LegBuilder
                    // You can also check position.Z if labels aren't mapping correctly.
                    return beam;
                }
            }
            return null;
        }

        private Point GetPointAtZ(List<Point> legNodes, double targetZ)
        {
            if (targetZ <= legNodes[0].Z) return new Point(legNodes[0].X, legNodes[0].Y, legNodes[0].Z);
            int last = legNodes.Count - 1;
            if (targetZ >= legNodes[last].Z) return new Point(legNodes[last].X, legNodes[last].Y, legNodes[last].Z);

            for (int i = 0; i < legNodes.Count - 1; i++)
            {
                Point a = legNodes[i];
                Point b = legNodes[i + 1];
                if (targetZ >= a.Z && targetZ <= b.Z)
                {
                    double dz = b.Z - a.Z;
                    if (dz <= 0.000001) return new Point(a.X, a.Y, targetZ);
                    double t = (targetZ - a.Z) / dz;
                    return new Point(a.X + ((b.X - a.X) * t), a.Y + ((b.Y - a.Y) * t), targetZ);
                }
            }
            return new Point(legNodes[last].X, legNodes[last].Y, legNodes[last].Z);
        }

        private Beam CreateBracingBeam(Point start, Point end, string profile, string material, string label, double planeOffset, double dxOffset, bool isOuterBrace)
        {
            Beam beam = new Beam(start, end);
            beam.Profile.ProfileString = profile;
            beam.Material.MaterialString = material;
            beam.Name = "TOWER_BRACING";
            beam.Class = "5";

            if (isOuterBrace)
            {
                // (/) DIAGONAL POSITION SETTINGS
                // MODIFY THESE FOR YOUR SPECIFIC REQUIREMENTS
                beam.Position.Plane = Position.PlaneEnum.RIGHT;        // <-- CHANGE THIS FOR (/) DIAGONAL
                beam.Position.Depth = Position.DepthEnum.MIDDLE;      // <-- CHANGE THIS FOR (/) DIAGONAL
                beam.Position.Rotation = Position.RotationEnum.BACK; // <-- CHANGE THIS FOR (/) DIAGONAL
                beam.Position.RotationOffset = -3; // <-- CHANGE THIS FOR (/) DIAGONAL
                beam.StartPointOffset.Dx = -dxOffset;
                beam.EndPointOffset.Dx = dxOffset;
                
            }
            else
            {
                // (\) DIAGONAL POSITION SETTINGS
                // MODIFY THESE FOR YOUR SPECIFIC REQUIREMENTS
                beam.Position.Plane = Position.PlaneEnum.RIGHT;        // <-- CHANGE THIS FOR (\) DIAGONAL
                beam.Position.Depth = Position.DepthEnum.MIDDLE;       // <-- CHANGE THIS FOR (\) DIAGONAL
                beam.Position.Rotation = Position.RotationEnum.TOP; // <-- CHANGE THIS FOR (\) DIAGONAL
                beam.Position.RotationOffset = 6; // <-- CHANGE THIS FOR (/) DIAGONAL
                beam.StartPointOffset.Dx = -dxOffset;
                beam.EndPointOffset.Dx = dxOffset;

            }

            // You can also add conditions based on level or face:
            // Example: if (level == 0) { beam.Position.Plane = Position.PlaneEnum.MIDDLE; }
            // Example: if (faceIndex == 0) { beam.Position.Depth = Position.DepthEnum.MIDDLE; }

            beam.SetLabel(label);
            return beam;
        }
        private double GetWidthFromProfile(string profile)
        {
            if (string.IsNullOrEmpty(profile)) return 100.0;
            string clean = Regex.Replace(profile, @"[a-zA-Z]", "");
            string[] parts = clean.Split('*');
            if (parts.Length >= 1 && double.TryParse(parts[0], out double width)) return width;
            return 100.0;
        }

        private double GetThicknessFromProfile(string profile)
        {
            if (string.IsNullOrEmpty(profile)) return 10.0;
            string clean = Regex.Replace(profile, @"[a-zA-Z]", "");
            string[] parts = clean.Split('*');
            if (parts.Length >= 3 && double.TryParse(parts[2], out double thick)) return thick;
            if (parts.Length >= 2 && double.TryParse(parts[1], out double thickAlt)) return thickAlt;
            return 10.0;
        }
        private Beam CreateHorizontalBracingBeam(Point start, Point end, string profile, string material, string label, double planeOffset, bool isRightToLeft)
        {
            Beam beam = new Beam(start, end);
            beam.Profile.ProfileString = profile;
            beam.Material.MaterialString = material;
            beam.Name = "TOWER_BRACING";
            beam.Class = "5";

            beam.Position.Plane = isRightToLeft ? Position.PlaneEnum.LEFT : Position.PlaneEnum.RIGHT;
            beam.Position.Depth = isRightToLeft ? Position.DepthEnum.MIDDLE : Position.DepthEnum.MIDDLE;
            beam.Position.Rotation = isRightToLeft ? Position.RotationEnum.BELOW : Position.RotationEnum.TOP;
            beam.Position.PlaneOffset = isRightToLeft ? planeOffset : 0;

            beam.SetLabel(label);
            return beam;
        }

        /// <summary>
        /// Returns the horizontal outward normal of the face defined by leg1 and leg2
        /// at the given Z. "Outward" = pointing away from the tower's horizontal centroid.
        /// Depending on which face we're on, this resolves naturally to +X, -X, +Y, or -Y
        /// (or a diagonal for non-orthogonal towers).
        /// </summary>
        private Vector GetOutwardFaceNormal(List<List<Point>> legNodes, int leg1Index, int leg2Index, double z)
        {
            // Tower center in XY at this Z (average of all leg centerlines)
            double cx = 0, cy = 0;
            int n = legNodes.Count;
            for (int i = 0; i < n; i++)
            {
                Point p = GetPointAtZ(legNodes[i], z);
                cx += p.X;
                cy += p.Y;
            }
            cx /= n;
            cy /= n;

            Point p1 = GetPointAtZ(legNodes[leg1Index], z);
            Point p2 = GetPointAtZ(legNodes[leg2Index], z);

            // Midpoint of the face
            double mx = (p1.X + p2.X) / 2.0;
            double my = (p1.Y + p2.Y) / 2.0;

            Vector outward = new Vector(mx - cx, my - cy, 0);
            outward.Normalize();
            return outward;
        }
    }
}