using System;
using System.Device.Location;
using System.Text;

namespace BGLParser
{
    /// <summary>
    /// FSX bgl Taxiway parking Subrecord as per
    /// http://www.fsdeveloper.com/wiki/index.php?title=BGL_File_Format
    /// </summary>
    public class TaxiwayParking : Taxiway
    {
        private Point[] points;

        public class Point
        {
            public int index { private set; get; } = 0;
            public byte airlineCodeCount { private set; get; } = 0;
            public UInt16 number { private set; get; } = 0;
            public byte type { private set; get; } = 0;
            public byte pushback { private set; get; } = 0;
            public byte name { private set; get; } = 0;
            public float radius { private set; get; } = 0;
            public float heading { private set; get; } = 0;
            public float teeOffset1 { private set; get; } = 0;
            public float teeOffset2 { private set; get; } = 0;
            public float teeOffset3 { private set; get; } = 0;
            public float teeOffset4 { private set; get; } = 0;
            public GeoCoordinate location { private set; get; } = new GeoCoordinate();
            public string[] airlineDesignators { private set; get; }

            public Point(int index, UInt32 identification, float radius, float heading, float teeOffset1, float teeOffset2,
                        float teeOffset3, float teeOffset4, UInt32 longitude, UInt32 latitude, byte[]file, UInt32 offset)
            {
                this.index = index;
                this.radius = radius;
                this.heading = heading;
                this.teeOffset1 = teeOffset1;
                this.teeOffset2 = teeOffset2;
                this.teeOffset3 = teeOffset3;
                this.teeOffset4 = teeOffset4;
                location.Longitude = (longitude * (360.0 / (3 * 0x10000000))) - 180.0;
                location.Latitude = 90.0 - latitude * (180.0 / (2 * 0x10000000));
                airlineCodeCount = (byte)((identification   & 0b11111111000000000000000000000000) >> 24);
                number = (UInt16)((identification           & 0b00000000111111111111000000000000) >> 12);
                type = (byte)((identification               & 0b00000000000000000000111100000000) >> 8);
                pushback = (byte)((identification           & 0b00000000000000000000000011000000) >> 6);
                name = (byte)((identification               & 0b00000000000000000000000000111111));
                airlineDesignators = new string[airlineCodeCount];
                for (int i = 0; i < airlineCodeCount; i++)
                {
                    airlineDesignators[i] = Encoding.ASCII.GetString(file, (int)offset, 4).TrimEnd('\0');
                    offset += 4;
                }
            }
        }

        public TaxiwayParking(UInt16 id, UInt32 sizeOfSubSubrecord, UInt16 numberOfPoints, byte[] file, UInt32 offset)
        {
            this.id = id;
            this.sizeOfSubSubrecord = sizeOfSubSubrecord;
            this.numberOfPoints = numberOfPoints;
            points = new Point[numberOfPoints];
            for (int i = 0; i < numberOfPoints; i++)
            {
                points[i] = new Point(i, 
                                      BitConverter.ToUInt32(file, (int)offset),
                                      BitConverter.ToSingle(file, (int)(offset += 4)),
                                      BitConverter.ToSingle(file, (int)(offset += 4)),
                                      BitConverter.ToSingle(file, (int)(offset += 4)),
                                      BitConverter.ToSingle(file, (int)(offset += 4)),
                                      BitConverter.ToSingle(file, (int)(offset += 4)),
                                      BitConverter.ToSingle(file, (int)(offset += 4)),
                                      BitConverter.ToUInt32(file, (int)(offset += 4)),
                                      BitConverter.ToUInt32(file, (int)(offset += 4)),
                                      file, offset += 4);
                offset += (uint)(points[i].airlineCodeCount * 4);
            }
        }

        public Point findClosestTo(GeoCoordinate position)
        {
            double lastDistance = Double.MaxValue;
            Point current = null;
            foreach (Point point in points)
            {
                double dist = position.GetDistanceTo(point.location);
                if (dist < point.radius)
                {
                    current = point;
                    lastDistance = dist;
                }
            }
            if (current != null)
                return current;
            throw new Exception("Parking not found");
        }
    }
}
