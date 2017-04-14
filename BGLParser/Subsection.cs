using System;

namespace Pushback_Utility.BGLParser
{
    /// <summary>
    /// FSX bgl Subsections as per
    /// http://www.fsdeveloper.com/wiki/index.php?title=BGL_File_Format
    /// </summary>
    public class Subsection
    {
        public UInt32 qmidA { private set; get; } = 0;
        public UInt32 qmidB { private set; get; } = 0; // Only for 20byte subsections
        public UInt32 numberOfRecords { private set; get; } = 0;
        public UInt32 dataOffset { private set; get; } = 0;
        public UInt32 totalDataSize { private set; get; } = 0;
        public object[] records { private set; get; }

        /// <summary>
        /// 16Byte sections
        /// </summary>
        /// <param name="qmidA"></param>
        /// <param name="numberOfRecords"></param>
        /// <param name="dataOffset"></param>
        /// <param name="totalDataSize"></param>
        public Subsection(UInt32 qmidA, UInt32 numberOfRecords, UInt32 dataOffset, UInt32 totalDataSize, byte[] file)
        {
            this.qmidA = qmidA;
            this.numberOfRecords = numberOfRecords;
            this.dataOffset = dataOffset;
            this.totalDataSize = totalDataSize;
            // Records
            makeRecords(file);
        }

        /// <summary>
        /// 20byte sections
        /// </summary>
        /// <param name="qmidA"></param>
        /// <param name="qmidB"></param>
        /// <param name="numberOfRecords"></param>
        /// <param name="dataOffset"></param>
        /// <param name="totalDataSize"></param>
        public Subsection(UInt32 qmidA, UInt32 qmidB, UInt32 numberOfRecords, UInt32 dataOffset, UInt32 totalDataSize, byte[] file)
        {
            this.qmidA = qmidA;
            this.qmidB = qmidB;
            this.numberOfRecords = numberOfRecords;
            this.dataOffset = dataOffset;
            this.totalDataSize = totalDataSize;
            // Records
            makeRecords(file);
        }

        private void makeRecords(byte[] file)
        {
            records = new object[numberOfRecords];
            for (int i = 0; i < numberOfRecords; i++) {
                // Make custom class per record type and add to switch statement
                switch (BitConverter.ToUInt16(file, (int)dataOffset))
                {
                    case 0x003c:
                        UInt32 size = BitConverter.ToUInt32(file, (int)(dataOffset += 2));
                        records[i] = new Airport(0x003c,
                                                 size,
                                                 file[dataOffset += 4],
                                                 file[dataOffset += 1],
                                                 file[dataOffset += 1],
                                                 file[dataOffset += 1],
                                                 file[dataOffset += 1],
                                                 file[dataOffset += 1],
                                                 BitConverter.ToUInt32(file, (int)(dataOffset += 1)),
                                                 BitConverter.ToUInt32(file, (int)(dataOffset += 4)),
                                                 BitConverter.ToUInt32(file, (int)(dataOffset += 4)),
                                                 BitConverter.ToUInt32(file, (int)(dataOffset += 4)),
                                                 BitConverter.ToUInt32(file, (int)(dataOffset += 4)),
                                                 BitConverter.ToUInt32(file, (int)(dataOffset += 4)),
                                                 BitConverter.ToSingle(file, (int)(dataOffset += 4)),
                                                 BitConverter.ToUInt32(file, (int)(dataOffset += 4)),
                                                 BitConverter.ToUInt32(file, (int)(dataOffset += 4)),
                                                 BitConverter.ToUInt32(file, (int)(dataOffset += 4)),
                                                 file[dataOffset += 4],
                                                 file[dataOffset += 1],
                                                 BitConverter.ToUInt16(file, (int)(dataOffset += 1)),
                                                 file, dataOffset += 2);
                        dataOffset += (size - 0x38);
                        break;
                    default:
                        size = BitConverter.ToUInt32(file, (int)(dataOffset += 2));
                        dataOffset += (size - 2);
                        break;
                }
            }
        }
    }
}
