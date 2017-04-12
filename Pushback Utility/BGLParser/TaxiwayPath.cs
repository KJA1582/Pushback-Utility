using System;
using System.Collections.Generic;

namespace Pushback_Utility.BGLParser
{
    /// <summary>
    /// FSX bgl Taxiway path Subrecord as per
    /// http://www.fsdeveloper.com/wiki/index.php?title=BGL_File_Format
    /// </summary>
    public class TaxiwayPath : Taxiway
    {
        internal Point[] points;
        internal enum TYPE : byte
        {
            TAXI = 0x1,
            RUNWAY = 0x2,
            PARKING = 0x3,
            PATH = 0x4,
            CLOSED = 0x5,
        };
        internal class Point
        {
            public UInt16 startPointIndex { private set; get; } = 0;
            public UInt16 endPointIndex { private set; get; } = 0;
            public byte runwayDesignator { private set; get; } = 0;
            public byte type { private set; get; } = 0;
            public bool drawSurfaceFlag { private set; get; } = false;
            public bool drawDetailFlag { private set; get; } = false;
            public bool unused { private set; get; } = false;
            public byte taxiNameIndex { private set; get; } = 0; // or runway number if type is runway
            public bool centerline { private set; get; } = false;
            public bool centerLineLighted { private set; get; } = false;
            public byte leftedge { private set; get; } = 0;
            public bool leftEdgeLighted { private set; get; } = false;
            public byte rightEdge { private set; get; } = 0;
            public bool rightEdgeLighted { private set; get; } = false;
            public byte surface { private set; get; } = 0;
            public float width { private set; get; } = 0;
            public float weightLimit { private set; get; } = 0;
            public UInt32 unknown { private set; get; } = 0;


            public Point(UInt16 startPointIndex, UInt16 compoundEndPoint, byte compoundType, byte taxiNameIndex, byte edges, byte surface, float width, float weightLimit, UInt32 unknown)
            {
                this.startPointIndex = startPointIndex;
                endPointIndex = (UInt16)(compoundEndPoint   & 0b0000111111111111);
                runwayDesignator = (byte)((compoundEndPoint & 0b1111000000000000) >> 12);
                type = (byte)(compoundType                               & 0b00011111);
                drawSurfaceFlag = Convert.ToBoolean((byte)((compoundType & 0b00100000) >> 5));
                drawDetailFlag = Convert.ToBoolean((byte)((compoundType  & 0b01000000) >> 6));
                unused = Convert.ToBoolean((byte)((compoundType          & 0b10000000) >> 7));
                this.taxiNameIndex = taxiNameIndex;
                centerline = Convert.ToBoolean((byte)((edges        & 0b00000001)));
                centerLineLighted = Convert.ToBoolean((byte)((edges & 0b00000010) >> 1));
                leftedge = (byte)((edges                            & 0b00001100) >> 2);
                leftEdgeLighted = Convert.ToBoolean((byte)((edges   & 0b00010000) >> 4));
                rightEdge = (byte)((edges                           & 0b01100000) >> 5);
                rightEdgeLighted = Convert.ToBoolean((byte)((edges  & 0b10000000) >> 7));
                this.surface = surface;
                this.width = width;
                this.weightLimit = weightLimit;
                this.unknown = unknown;
            }

            public override string ToString()
            {
                return startPointIndex + " > " + endPointIndex;
            }
        }

        public TaxiwayPath(UInt16 id, UInt32 sizeOfSubSubrecord, UInt16 numberOfPoints, byte[] file, UInt32 offset)
        {
            this.id = id;
            this.sizeOfSubSubrecord = sizeOfSubSubrecord;
            this.numberOfPoints = numberOfPoints;
            points = new Point[numberOfPoints];
            for (int i = 0; i < numberOfPoints; i++)
            {
                points[i] = new Point(BitConverter.ToUInt16(file, (int)(offset)),
                                      BitConverter.ToUInt16(file, (int)(offset += 2)),
                                      file[(int)(offset += 2)],
                                      file[(int)(offset += 1)],
                                      file[(int)(offset += 1)],
                                      file[(int)(offset += 1)],
                                      BitConverter.ToSingle(file, (int)(offset += 1)),
                                      BitConverter.ToSingle(file, (int)(offset += 4)),
                                      BitConverter.ToUInt32(file, (int)(offset += 4)));
                offset += 4;
            }
        }

        /// <summary>
        /// Returns point of given type which has given endPointIndex
        /// </summary>
        /// <param name="type"></param>
        /// <param name="endPointIndex"></param>
        /// <returns></returns>
        internal Point getBy(TYPE type, UInt16 index)
        {
            foreach (Point point in points)
            {
                if (point.type == (byte)type)
                    if (point.endPointIndex == index)
                        return point;
            }
            return null;
        }

        /// <summary>
        /// Returns point of given type (or all but given type) which has either given startPointIndex or endPointIndex
        /// </summary>
        /// <param name="invertType"></param>
        /// <param name="type"></param>
        /// <param name="startPointIndex"></param>
        /// <param name="endPointIndex"></param>
        /// <returns></returns>
        internal List<Tuple<Point, int>> getBy(bool invertType, TYPE type, UInt16 index)
        {
            List<Tuple<Point, int>> returnList = new List<Tuple<Point, int>>();
            foreach (Point point in points)
            {
                if ((!invertType && point.type == (byte)type) || (invertType && !(point.type == (byte)type)))
                {
                    if (point.endPointIndex == index)
                        returnList.Add(new Tuple<Point, int>(point, point.startPointIndex));
                    if (point.startPointIndex == index)
                        returnList.Add(new Tuple<Point, int>(point, point.endPointIndex));
                }
            }
            if (returnList.Count > 0)
                return returnList;
            throw new Exception("Paths not found");
        }
    }
}
