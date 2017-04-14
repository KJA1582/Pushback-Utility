using System;

namespace BGLParser
{
    public abstract class Taxiway
    {
        public UInt16 id { protected set; get; } = 0;
        public UInt32 sizeOfSubSubrecord { protected set; get; } = 0;
        public UInt16 numberOfPoints { protected set; get; } = 0;
    }
}
