﻿using System;
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

    public static double targetHeading(GeoCoordinate midPoint, GeoCoordinate endPoint)
    {
        double bearingMidToEndPoint = bearingDegrees(true, midPoint, endPoint);
        return reciprocal(bearingMidToEndPoint);
    }

    private  static double ToBearing(double radians)
    {
        // convert radians to degrees (as bearing: 0...360)
        return (Degrees(radians) + 360) % 360;
    }
}

