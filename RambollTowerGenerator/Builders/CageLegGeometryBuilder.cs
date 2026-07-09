using System;
using System;
using System.Collections.Generic;
using RambollTowerGenerator.Common;
using RambollTowerGenerator.Models;
using Tekla.Structures.Geometry3d;

namespace RambollTowerGenerator.Builders
{
    public class CageLegGeometryBuilder
    {
        public List<List<Point>> BuildLegNodes(TowerModel tower)
        {
            switch (tower.Cage.CageType)
            {
                case CageType.Vertical:
                    return BuildVerticalLegNodes(
                        tower.BasePoint,
                        tower.LegCount,
                        tower.Geometry.SegmentCount,
                        tower.Geometry.TotalHeight,
                        tower.Geometry.BaseWidth,
                        tower.Geometry.SegmentGap);

                case CageType.ConicalVertical:
                    return BuildConicalVerticalLegNodes(tower);

                default:
                    return BuildConicalLegNodes(
                        tower.BasePoint,
                        tower.LegCount,
                        tower.Geometry.SegmentCount,
                        tower.Geometry.TotalHeight,
                        tower.Geometry.BaseWidth,
                        tower.Geometry.TopWidth,
                        tower.Geometry.SegmentGap);
            }
         }

        private List<List<Point>> BuildConicalLegNodes(Point basePoint, int legCount, int segmentCount, double totalHeight, double baseWidth, double topWidth, double segmentGap)
        {
            var result = new List<List<Point>>();
            double angleStep = (2.0 * Math.PI) / legCount;
            double angleOffset = legCount == 4 ? Math.PI / 4.0 : 0.0;
            int safeSegments = Math.Max(1, segmentCount);

            for (int legIndex = 0; legIndex < legCount; legIndex++)
            {
                double angle = angleOffset + (legIndex * angleStep);
                var nodes = new List<Point>();

                for (int i = 0; i <= safeSegments; i++)
                {
                    double t = (double)i / safeSegments;
                    double z = basePoint.Z + (t * totalHeight);
                    double width = baseWidth + t * (topWidth - baseWidth);

                    AddNode(nodes, basePoint, angle, width, z, legCount);
                }

                result.Add(nodes);
            }

            return result;
        }

        private List<List<Point>> BuildVerticalLegNodes(Point basePoint, int legCount, int segmentCount, double totalHeight, double width, double segmentGap)
        {
            var result = new List<List<Point>>();
            double angleStep = (2.0 * Math.PI) / legCount;
            double angleOffset = legCount == 4 ? Math.PI / 4.0 : 0.0;
            int safeSegments = Math.Max(1, segmentCount);

            for (int legIndex = 0; legIndex < legCount; legIndex++)
            {
                double angle = angleOffset + (legIndex * angleStep);
                var nodes = new List<Point>();

                for (int i = 0; i <= safeSegments; i++)
                {
                    double t = (double)i / safeSegments;
                    double z = basePoint.Z + (t * totalHeight);

                    AddNode(nodes, basePoint, angle, width, z, legCount);
                }

                result.Add(nodes);
            }

            return result;
        }

        private List<List<Point>> BuildConicalVerticalLegNodes(TowerModel tower)
        {
            var result = new List<List<Point>>();
            double angleStep = (2.0 * Math.PI) / tower.LegCount;
            double angleOffset = tower.LegCount == 4 ? Math.PI / 4.0 : 0.0;

            double conicalHeight = tower.Geometry.TotalHeight;

            for (int legIndex = 0; legIndex < tower.LegCount; legIndex++)
            {
                double angle = angleOffset + (legIndex * angleStep);
                var nodes = new List<Point>();

                int conicalSegments = Math.Max(1, tower.Geometry.SegmentCount);
                for (int i = 0; i <= conicalSegments; i++)
                {
                    double t = (double)i / conicalSegments;
                    double z = tower.BasePoint.Z + (t * conicalHeight);
                    double width = tower.Geometry.BaseWidth + t * (tower.Geometry.TopWidth - tower.Geometry.BaseWidth);

                    double halfCentralAngle = Math.PI / tower.LegCount;
                    double radius = width / (2.0 * Math.Sin(halfCentralAngle));
                    double x = tower.BasePoint.X + radius * Math.Cos(angle);
                    double y = tower.BasePoint.Y + radius * Math.Sin(angle);
                    nodes.Add(new Point(x, y, z));
                }

                if (tower.Cage.VerticalHeight > 0)
                {
                    Point transitionPoint = nodes[nodes.Count - 1];
                    int verticalSegments = Math.Max(1, tower.Cage.VerticalSegmentCount);

                    for (int i = 1; i <= verticalSegments; i++)
                    {
                        double t = (double)i / verticalSegments;
                        double z = transitionPoint.Z + (t * tower.Cage.VerticalHeight);
                        nodes.Add(new Point(transitionPoint.X, transitionPoint.Y, z));
                    }
                }

                result.Add(nodes);
            }

            return result;
        }

        private void AddNode(List<Point> nodes, Point basePoint, double angle, double width, double z, int legCount)
        {
            // width = distance to adjacent leg (polygon side length / chord)
            double safeLegCount = Math.Max(3, legCount);
            double halfCentralAngle = Math.PI / safeLegCount;
            double radius = width / (2.0 * Math.Sin(halfCentralAngle));

            double x = basePoint.X + radius * Math.Cos(angle);
            double y = basePoint.Y + radius * Math.Sin(angle);
            nodes.Add(new Point(x, y, z));
        }
    }
}
