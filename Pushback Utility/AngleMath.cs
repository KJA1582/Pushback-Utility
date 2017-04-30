using System;
using System.Collections.Generic;
using System.Device.Location;
using System.Windows;

static class AngleMath
{
    public enum DIRECTION
    {
        RIGHT,
        STRAIGHT,
        LEFT,
    }

    public static double Radians(double degrees)
    {
        return degrees * Math.PI / 180;
    }

    public static double Degrees(double radians)
    {
        return radians * 180 / Math.PI;
    }

    public static double reciprocal(double heading)
    {
        return (heading + 180) % 360;
    }

    /// <summary>
    /// Bearing from start to end, parameters in degrees
    /// </summary>
    /// <param name="inDegrees"></param>
    /// <param name="lat1"></param>
    /// <param name="lon1"></param>
    /// <param name="lat2"></param>
    /// <param name="lon2"></param>
    /// <returns></returns>
    public static double bearingDegrees(bool inDegrees, GeoCoordinate start, GeoCoordinate end)
    {
        var dLon = Radians(end.Longitude - start.Longitude);
        var dPhi = Math.Log(Math.Tan(Radians(end.Latitude) / 2 + Math.PI / 4) / 
                   Math.Tan(Radians(start.Latitude) / 2 + Math.PI / 4));
        if (Math.Abs(dLon) > Math.PI)
            dLon = dLon > 0 ? -(2 * Math.PI - dLon) : (2 * Math.PI + dLon);
        if (inDegrees)
            return ToBearing(Math.Atan2(dLon, dPhi));
        return Radians(ToBearing(Math.Atan2(dLon, dPhi)));
    }

    /// <summary>
    /// Calculates how much before the end of the pushback the turn has to be initiated wiht given turnradius and heading delta
    /// headingDelta in degrees
    /// </summary>
    /// <param name="headingDelta"></param>
    /// <param name="turnDiameter"></param>
    /// <returns></returns>
    public static double distanceUntilTurn(double headingDelta, double turnDiameter)
    {
        return (Math.Cos(Radians(headingDelta) / 2) * turnDiameter * Math.Sin(Radians(headingDelta) / 2) -
                Math.Tan(Math.PI / 2 - Radians(headingDelta)) * Math.Sin(Radians(headingDelta) / 2) * turnDiameter *
                Math.Sin(Radians(headingDelta) / 2));
    }

    /// <summary>
    /// Returns direction of end relative to user
    /// </summary>
    /// <param name="userHeading"></param>
    /// <param name="user"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    public static DIRECTION leftOfUser(double userHeading, GeoCoordinate user, GeoCoordinate end)
    {
        double bearingToEnd = bearingDegrees(true, user, end);
        Vector User = new Vector(Math.Cos(Radians(userHeading)), Math.Sin(Radians(userHeading)));
        Vector End = new Vector(Math.Cos(Radians(bearingToEnd)), Math.Sin(Radians(bearingToEnd)));
        double angle = Vector.AngleBetween(User, End);
        if (180 - Math.Abs(angle) < 0.1)
            return DIRECTION.STRAIGHT;
        else if (angle > 0)
            return DIRECTION.RIGHT; 
        else
            return DIRECTION.LEFT;
    }

    /// <summary>
    /// Calculates target heading of pushback
    /// </summary>
    /// <param name="midPoint"></param>
    /// <param name="endPoint"></param>
    /// <returns></returns>
    public static double targetHeading(GeoCoordinate midPoint, GeoCoordinate endPoint)
    {
        double bearingMidToEndPoint = bearingDegrees(true, midPoint, endPoint);
        return reciprocal(bearingMidToEndPoint);
    }

    /// <summary>
    /// Converts lat/lon Points to planar coordinates using complex number theory
    /// </summary>
    /// <param name="referencePoint"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    public static List<Point> convertToPlanarPoints(GeoCoordinate referencePoint, GeoCoordinate[] path)
    {
        List<Point> ret = new List<Point>();
        ret.Add(new Point(0, 0)); // referencePoint is 0,0
        foreach (GeoCoordinate point in path)
        {
            double bearingTo = bearingDegrees(false, referencePoint, point);
            double distanceTo = referencePoint.GetDistanceTo(point);
            Point pPoint = new Point();
            pPoint.X = distanceTo * Math.Cos(bearingTo);
            pPoint.Y = distanceTo * Math.Sin(bearingTo);
            ret.Add(pPoint);
        }
        return ret;
    }

    /// <summary>
    /// Calculates point on a bézier curve through give point array
    /// </summary>
    /// <param name="t"></param>
    /// <param name="points"></param>
    /// <returns></returns>
    public static Point getPointOnBezier(double t, Point[] points)
    {
        // Sum from i to n over
        Point ret = new Point();
        for (int i = 0; i < points.Length; i++)
        {
            double binomial = binomCoefficient(points.Length-1, i);
            ret.X = binomial * Math.Pow(t, i) * Math.Pow((1 - t), points.Length-1 - i) * points[i].X;
            ret.Y = binomial * Math.Pow(t, i) * Math.Pow((1 - t), points.Length-1 - i) * points[i].Y;
        }
        return ret;
    }

    public static GeoCoordinate convertToLatLon(GeoCoordinate referencePoint, Point point)
    {
        double bearing = Math.Atan2(point.Y, point.X);
        if (bearing < 0)
            bearing += 2 * Math.PI;
        double angularDistance = Math.Sqrt(Math.Pow(point.X, 2) + Math.Pow(point.Y, 2)) / 6371000;

        double lat = Math.Asin(Math.Sin(Radians(referencePoint.Latitude)) * Math.Cos(angularDistance) +
                               Math.Cos(Radians(referencePoint.Latitude)) * Math.Sin(angularDistance) * Math.Cos(bearing));
        double lon = Radians(referencePoint.Longitude) + Math.Atan2(Math.Sin(bearing) * Math.Sin(angularDistance) * 
                                                                    Math.Cos(Radians(referencePoint.Latitude)), 
                                                                    Math.Cos(angularDistance) - 
                                                                    Math.Sin(Radians(referencePoint.Latitude)) * Math.Sin(lat));
        return new GeoCoordinate(Degrees(lat), Degrees(lon));
    }

    /// <summary>
    /// Calculates the binomial coefficient (nCk) (N items, choose k)
    /// </summary>
    /// <param name="n">the number items</param>
    /// <param name="k">the number to choose</param>
    /// <returns>the binomial coefficient</returns>
    private static double binomCoefficient(int n, int k)
    {
        if (k > n) { return 0; }
        if (n == k) { return 1; } // only one way to chose when n == k
        if (k > n - k) { k = n - k; } // Everything is symmetric around n-k, so it is quicker to iterate over a smaller k than a larger one.
        double c = 1;
        for (double i = 1; i <= k; i++)
        {
            c *= n--;
            c /= i;
        }
        return c;
    }

    private  static double ToBearing(double radians)
    {
        // convert radians to degrees (as bearing: 0...360)
        return (Degrees(radians) + 360) % 360;
    }
}

