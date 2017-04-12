using System;
using System.Device.Location;

namespace Pushback_Utility.BGLParser
{
    /// <summary>
    /// FSX bgl Taxiway point Subrecord as per
    /// http://www.fsdeveloper.com/wiki/index.php?title=BGL_File_Format
    /// </summary>
    public class TaxiwayPoint : Taxiway
    {
        internal Point[] points;

        internal class Point
        {
            public int index { private set; get; } = 0;
            public byte type { private set; get; } = 0;
            public byte flag { private set; get; } = 0;
            public UInt16 unknown { private set; get; } = 0;
            public GeoCoordinate location { private set; get; } = new GeoCoordinate();

            public Point(int index, byte type, byte flag, UInt16 unknown, UInt32 longitude, UInt32 latitude)
            {
                this.index = index;
                this.type = type;
                this.flag = flag;
                this.unknown = unknown;
                location.Longitude = (longitude * (360.0 / (3 * 0x10000000))) - 180.0;
                location.Latitude = 90.0 - latitude * (180.0 / (2 * 0x10000000));
            }
        }

        public TaxiwayPoint(UInt16 id, UInt32 sizeOfSubSubrecord, UInt16 numberOfPoints, byte[] file, UInt32 offset)
        {
            this.id = id;
            this.sizeOfSubSubrecord = sizeOfSubSubrecord;
            this.numberOfPoints = numberOfPoints;
            points = new Point[numberOfPoints];
            for (int i = 0; i < numberOfPoints; i++)
            {
                points[i] = new Point(i,
                                      file[(int)offset],
                                      file[(int)(offset += 1)],
                                      BitConverter.ToUInt16(file, (int)(offset += 1)),
                                      BitConverter.ToUInt32(file, (int)(offset += 2)),
                                      BitConverter.ToUInt32(file, (int)(offset += 4)));
                offset += 4;
            }
        }

        internal Point findByIndex(int index)
        {
            foreach (Point point in points)
            {
                if (point.index == index)
                    return point;
            }
            throw new Exception("Point not fond");
        }
    }
}
