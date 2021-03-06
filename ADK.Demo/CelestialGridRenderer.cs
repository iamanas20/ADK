﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADK.Demo
{
    public class CelestialGridRenderer : BaseSkyRenderer
    {
        Pen penEquatorialGrid;
        Pen penHorizontalGrid;
        Pen penEclipticLine;

        public CelestialGridRenderer(Sky sky, ISkyMap skyMap) : base(sky, skyMap)
        {
            Color colorGridEquatorial = Color.FromArgb(0, 64, 64);
            penEquatorialGrid = new Pen(Map.Antialias ? colorGridEquatorial : Color.FromArgb(200, colorGridEquatorial));
            penEquatorialGrid.DashStyle = DashStyle.Dash;

            Color colorGridHorizontal = Color.FromArgb(0, 64, 0);
            penHorizontalGrid = new Pen(Map.Antialias ? colorGridHorizontal : Color.FromArgb(200, colorGridHorizontal));
            penHorizontalGrid.DashStyle = DashStyle.Dash;

            Color colorLineEcliptic = Color.FromArgb(128, 128, 0);
            penEclipticLine = new Pen(Map.Antialias ? colorLineEcliptic : Color.FromArgb(200, colorLineEcliptic));
            penEclipticLine.DashStyle = DashStyle.Dash;
        }

        public override void Render(Graphics g)
        {
            DrawGrid(g, penEquatorialGrid, Sky.GridEquatorial);
            DrawGrid(g, penHorizontalGrid, Sky.GridHorizontal);
            DrawGrid(g, penEclipticLine, Sky.LineEcliptic);
        }

        // TODO: move to separate renderer
        private void DrawGrid(Graphics g, Pen penGrid, CelestialGrid grid)
        {
            bool isAnyPoint = false;

            // Azimuths 
            for (int j = 0; j < grid.Columns; j++)
            {
                var segments = grid.Column(j)
                    .Select(p => Angle.Separation(grid.ToHorizontal(p), Map.Center) < Map.ViewAngle * 1.2 ? p : null)
                    .Split(p => p == null, true);

                foreach (var segment in segments)
                {
                    for (int k = 0; k < 2; k++)
                    {
                        if (segment.First().RowIndex > 1)
                            segment.Insert(0, grid[segment.First().RowIndex - 1, j]);
                    }

                    for (int k = 0; k < 2; k++)
                    {
                        if (segment.Last().RowIndex < grid.Rows - 2)
                            segment.Add(grid[segment.Last().RowIndex + 1, j]);
                    }

                    PointF[] refPoints = new PointF[2];
                    for (int k = 0; k < 2; k++)
                    {
                        var coord = grid.FromHorizontal(Map.Center);
                        coord.Longitude = segment[0].Longitude;
                        coord.Latitude += -Map.ViewAngle * 1.2 + k * (Map.ViewAngle * 2 * 1.2);
                        coord.Latitude = Math.Min(coord.Latitude, 80);
                        coord.Latitude = Math.Max(coord.Latitude, -80);
                        var refHorizontal = grid.ToHorizontal(coord);
                        refPoints[k] = Map.Projection.Project(refHorizontal);
                    }

                    DrawGroupOfPoints(g, penGrid, segment.Select(s => Map.Projection.Project(grid.ToHorizontal(s))).ToArray(), refPoints);

                    isAnyPoint = true;
                }
            }

            // Altitude circles
            for (int i = 0; i < grid.Rows; i++)
            {
                var segments = grid.Row(i)
                    .Select(p => Angle.Separation(grid.ToHorizontal(p), Map.Center) < Map.ViewAngle * 1.2 ? p : null)
                    .Split(p => p == null, true).ToList();

                // segment that starts with point "0 degrees"
                var seg0 = segments.FirstOrDefault(s => s.First().ColumnIndex == 0);

                // segment that ends with point "345 degrees"
                var seg23 = segments.FirstOrDefault(s => s.Last().ColumnIndex == 23);

                // join segments into one
                if (seg0 != null && seg23 != null && seg0 != seg23)
                {
                    segments.Remove(seg0);
                    seg23.AddRange(seg0);
                }

                foreach (var segment in segments)
                {
                    if (segment.Count == 24)
                    {
                        g.DrawClosedCurve(penGrid, segment.Select(s => Map.Projection.Project(grid.ToHorizontal(s))).ToArray());
                    }
                    else
                    {
                        for (int k = 0; k < 2; k++)
                        {
                            int col = segment.First().ColumnIndex;
                            if (col == 0)
                                segment.Insert(0, grid[i, 23]);
                            else
                                segment.Insert(0, grid[i, col - 1]);
                        }

                        for (int k = 0; k < 2; k++)
                        {
                            int col = segment.Last().ColumnIndex;

                            if (col < 23)
                                segment.Add(grid[i, col + 1]);
                            else if (col == 23)
                                segment.Add(grid[i, 0]);
                        }

                        PointF[] refPoints = new PointF[2];
                        for (int k = 0; k < 2; k++)
                        {
                            var coord = grid.FromHorizontal(Map.Center);
                            coord.Longitude += -Map.ViewAngle * 1.2 + k * (Map.ViewAngle * 1.2 * 2);
                            coord.Latitude = segment[0].Latitude;
                            var refHorizontal = grid.ToHorizontal(coord);
                            refPoints[k] = Map.Projection.Project(refHorizontal);
                        }

                        if (!Geometry.IsOutOfScreen(refPoints[0], Map.Width, Map.Height) || !Geometry.IsOutOfScreen(refPoints[1], Map.Width, Map.Height))
                        {
                            refPoints = Geometry.LineRectangleIntersection(refPoints[0], refPoints[1], Map.Width, Map.Height);
                        }

                        DrawGroupOfPoints(g, penGrid, segment.Select(s => Map.Projection.Project(grid.ToHorizontal(s))).ToArray(), refPoints);
                    }

                    isAnyPoint = true;
                }
            }

            // Special case: there are no points visible 
            // on the screen at the current position and zoom.
            // Then we select one point that is closest to screen senter. 
            if (!isAnyPoint)
            {
                GridPoint closestPoint = grid.Points.OrderBy(p => Angle.Separation(grid.ToHorizontal(p), Map.Center)).First();

                {
                    var segment = new List<GridPoint>();
                    segment.Add(closestPoint);
                    int i = closestPoint.RowIndex;

                    for (int k = 0; k < 2; k++)
                    {
                        int col = segment.First().ColumnIndex;
                        if (col == 0)
                            segment.Insert(0, grid[i, 23]);
                        else
                            segment.Insert(0, grid[i, col - 1]);
                    }

                    for (int k = 0; k < 2; k++)
                    {
                        int col = segment.Last().ColumnIndex;

                        if (col < 23)
                            segment.Add(grid[i, col + 1]);
                        else if (col == 23)
                            segment.Add(grid[i, 0]);
                    }

                    PointF[] refPoints = new PointF[2];
                    for (int k = 0; k < 2; k++)
                    {
                        var coord = grid.FromHorizontal(Map.Center);
                        coord.Longitude += -Map.ViewAngle * 1.2 + k * (Map.ViewAngle * 1.2 * 2);
                        coord.Latitude = segment[0].Latitude;
                        var refHorizontal = grid.ToHorizontal(coord);
                        refPoints[k] = Map.Projection.Project(refHorizontal);
                    }

                    if (!Geometry.IsOutOfScreen(refPoints[0], Map.Width, Map.Height) || !Geometry.IsOutOfScreen(refPoints[1], Map.Width, Map.Height))
                    {
                        refPoints = Geometry.LineRectangleIntersection(refPoints[0], refPoints[1], Map.Width, Map.Height);
                    }

                    DrawGroupOfPoints(g, penGrid, segment.Select(s => Map.Projection.Project(grid.ToHorizontal(s))).ToArray(), refPoints);
                }


                {
                    var segment = new List<GridPoint>();
                    segment.Add(closestPoint);
                    int j = closestPoint.ColumnIndex;

                    for (int k = 0; k < 2; k++)
                    {
                        if (segment.First().RowIndex > 1)
                            segment.Insert(0, grid[segment.First().RowIndex - 1, j]);
                    }

                    for (int k = 0; k < 2; k++)
                    {
                        if (segment.Last().RowIndex < grid.Rows - 2)
                            segment.Add(grid[segment.Last().RowIndex + 1, j]);
                    }

                    PointF[] refPoints = new PointF[2];
                    for (int k = 0; k < 2; k++)
                    {
                        var coord = grid.FromHorizontal(Map.Center);
                        coord.Longitude = segment[0].Longitude;
                        coord.Latitude += -Map.ViewAngle * 1.2 + k * (Map.ViewAngle * 2 * 1.2);
                        coord.Latitude = Math.Min(coord.Latitude, 80);
                        coord.Latitude = Math.Max(coord.Latitude, -80);
                        var refHorizontal = grid.ToHorizontal(coord);
                        refPoints[k] = Map.Projection.Project(refHorizontal);
                    }

                    DrawGroupOfPoints(g, penGrid, segment.Select(s => Map.Projection.Project(grid.ToHorizontal(s))).ToArray(), refPoints);
                }
            }
        }

        private void DrawGroupOfPoints(Graphics g, Pen penGrid, PointF[] points, PointF[] refPoints)
        {
            // Do not draw figure containing less than 2 points
            if (points.Length < 2)
            {
                return;
            }

            // Two points can be simply drawn as a line
            if (points.Length == 2)
            {
                g.DrawLine(penGrid, points[0], points[1]);
                return;
            }

            // Coordinates of the screen center
            var origin = new PointF(Map.Width / 2, Map.Height / 2);

            // Small radius is a screen diagonal
            double r = Math.Sqrt(Map.Width * Map.Width + Map.Height * Map.Height) / 2;

            // From 3 to 5 points. Probably we can straighten curve to line.
            // Apply some calculations to detect conditions when it's possible.
            if (points.Length > 2 && points.Length < 6)
            {
                // Determine start, middle and end points of the curve
                PointF pStart = points[0];
                PointF pMid = points[points.Length / 2];
                PointF pEnd = points[points.Length - 1];

                // Get angle between middle and last points of the curve
                double alpha = Geometry.AngleBetweenVectors(pMid, pStart, pEnd);

                double d1 = Geometry.DistanceBetweenPoints(pStart, origin);
                double d2 = Geometry.DistanceBetweenPoints(pEnd, origin);

                // It's almost a straight line
                if (alpha > 179)
                {
                    // Check the at lease one last point of the curve 
                    // is far enough from the screen center
                    if (d1 > r * 2 || d2 > r * 2)
                    {
                        g.DrawLine(penGrid, refPoints[0], refPoints[1]);
                        return;
                    }
                }

                // If both of last points of the line are far enough from the screen center 
                // then assume that the curve is an arc of a big circle.
                // Check the curvature of that circle by comparing its radius with small radius
                if (d1 > r * 2 && d2 > r * 2)
                {
                    var circle = Geometry.FindCircle(points);
                    if (circle.R / r > 60)
                    {
                        g.DrawLine(penGrid, refPoints[0], refPoints[1]);
                        return;
                    }
                }
            }

            if (points.All(p => Geometry.DistanceBetweenPoints(p, origin) < r * 60))
            {
                // Draw the curve in regular way
                g.DrawCurve(penGrid, points);
            }
        }
    }
}
