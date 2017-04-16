using System;

namespace BGLParser
{
    /// <summary>
    /// FSX bgl Sections as per
    /// http://www.fsdeveloper.com/wiki/index.php?title=BGL_File_Format
    /// </summary>
    public class Section
    {
        public UInt32 type { private set; get; } = 0;
        public UInt32 sizeValue { private set; get; } = 0;
        public UInt32 numberOfSubsections { private set; get; } = 0;
        public UInt32 subsectionOffset { private set; get; } = 0;
        public UInt32 totalSubsectionSize { private set; get; } = 0;
        public Subsection[] subsections {private set; get; }

        /// <summary>
        /// Section
        /// </summary>
        /// <param name="type"></param>
        /// <param name="sizeValue"></param>
        /// <param name="numberOfSubsections"></param>
        /// <param name="subsectionOffset"></param>
        /// <param name="totalSubsectionSize"></param>
        /// <param name="file"></param>
        public Section(UInt32 type, UInt32 sizeValue, UInt32 numberOfSubsections, UInt32 subsectionOffset, 
                       UInt32 totalSubsectionSize, byte[] file)
        {
            this.type = type;
            this.sizeValue = sizeValue;
            this.numberOfSubsections = numberOfSubsections;
            this.subsectionOffset = subsectionOffset;
            this.totalSubsectionSize = totalSubsectionSize;
            subsections = new Subsection[numberOfSubsections];
            // Make subsection objects
            for (int i = 0; i < numberOfSubsections; i++)
            {
                if ((((sizeValue & 0x10000) | 0x40000) >> 0x0E) == 16)
                    subsections[i] = new Subsection(
                                                    BitConverter.ToUInt32(file, (int)subsectionOffset),
                                                    BitConverter.ToUInt32(file, (int)(subsectionOffset += 4)),
                                                    BitConverter.ToUInt32(file, (int)(subsectionOffset += 4)),
                                                    BitConverter.ToUInt32(file, (int)(subsectionOffset += 4)),
                                                    file);
                else if ((((sizeValue & 0x10000) | 0x40000) >> 0x0E) == 20)
                    subsections[i] = new Subsection(
                                                    BitConverter.ToUInt32(file, (int)subsectionOffset),
                                                    BitConverter.ToUInt32(file, (int)(subsectionOffset += 4)),
                                                    BitConverter.ToUInt32(file, (int)(subsectionOffset += 4)),
                                                    BitConverter.ToUInt32(file, (int)(subsectionOffset += 4)),
                                                    BitConverter.ToUInt32(file, (int)(subsectionOffset += 4)),
                                                    file);
                subsectionOffset += 4;
            }
        }
    }
}
