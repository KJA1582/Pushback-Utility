using System;
using System.IO;

namespace Pushback_Utility.BGLParser
{
    public class BGLFile
    {
        private Section[] sections;

        public BGLFile(string registryPath, string filePath)
        {
            
            var data = File.ReadAllBytes(registryPath + filePath);
            UInt32 sectionsInFile = BitConverter.ToUInt32(data, 0x14);
            int offset = 0x38; // After BGL header 
            sections = new Section[sectionsInFile];
            for (int i = 0; i < sectionsInFile; i++)
            {
                switch (BitConverter.ToUInt32(data, offset))
                {
                    case 0x0003: // Airport section
                        sections[i] = new Section(
                                                  0x0003,
                                                  BitConverter.ToUInt32(data, offset += 4),
                                                  BitConverter.ToUInt32(data, offset += 4),
                                                  BitConverter.ToUInt32(data, offset += 4),
                                                  BitConverter.ToUInt32(data, offset += 4),
                                                  data);
                        offset += 4;
                        break;
                    default:
                        offset += 24; // section size of 20 + id offset
                        break;
                }
            }
        }

        public Airport findAirport(string icaoIdent)
        {
            foreach (Section section in sections) {
                if (section != null)
                {
                    foreach (Subsection subsection in section.subsections)
                    {
                        foreach (Airport record in subsection.records)
                        {
                            if (record.icaoIdent == icaoIdent)
                                return record;
                        }
                    }
                }
            }
            throw new Exception("Airport not found");
        }
    }
}
