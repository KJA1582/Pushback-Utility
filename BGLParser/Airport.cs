using System;
using System.Collections.Generic;

namespace Pushback_Utility.BGLParser
{
    /// <summary>
    /// FSX bgl Airport Record as per
    /// http://www.fsdeveloper.com/wiki/index.php?title=BGL_File_Format
    /// </summary>
    public class Airport
    {
        public UInt16 id { private set; get; } = 0x003c;
        public UInt32 sizeOfRecord { private set; get; } = 0;
        public byte numberOfRunwaySubrecords { private set; get; } = 0;
        public byte numberOfComSubrecords { private set; get; } = 0;
        public byte numberOfStartSubrecords { private set; get; } = 0;
        public byte numberOfApproaches { private set; get; } = 0;
        public byte numberOfAprons { private set; get; } = 0;
        public byte numberOFHelipads { private set; get; } = 0;
        public UInt32 longitude { private set; get; } = 0;
        public UInt32 latitude { private set; get; } = 0;
        public UInt32 altitude { private set; get; } = 0;
        public UInt32 towerLongitude { private set; get; } = 0;
        public UInt32 towerLatitude { private set; get; } = 0;
        public UInt32 towerAltitude { private set; get; } = 0;
        public float magneticVariation { private set; get; } = 0;
        public string icaoIdent { private set; get; } = "";
        public UInt32 regionIdent { private set; get; } = 0;
        public UInt32 typeOfFuelAndAvailability { private set; get; } = 0;
        public byte unknownFSX1 { private set; get; } = 0;
        public byte trafficScalarFSX { private set; get; } = 0;
        public UInt16 unknownFSX2 { private set; get; } = 0;
        public TaxiwayParking parkings { private set; get; }
        public TaxiwayPath paths { private set; get; }
        public TaxiwayPoint points { private set; get; }

        public Airport(UInt16 id, UInt32 sizeOfRecord, byte numberOfRunwaySubrecords, byte numberOfComSubrecords, 
                       byte numberOfStartSubrecords, byte numberOFApproaches, byte numberOfAprons, 
                       byte numberOFHelipads, UInt32 longitude, UInt32 latitude, UInt32 altitude, 
                       UInt32 towerLongitude, UInt32 towerLatitude, UInt32 towerAltitude, float magneticVariation, 
                       UInt32 icaoIdent, UInt32 regionIdent, UInt32 typeOfFuelAndAvailability,
                       byte unknownFSX1, byte trafficScalarFSX, UInt16 unknownFSX2, byte[] file, UInt32 offset)
        {
            this.id = id;
            this.sizeOfRecord = sizeOfRecord;
            this.numberOfRunwaySubrecords = numberOfRunwaySubrecords;
            this.numberOfComSubrecords = numberOfComSubrecords;
            this.numberOfStartSubrecords = numberOfStartSubrecords;
            this.numberOfApproaches = numberOfApproaches;
            this.numberOfAprons = numberOfAprons;
            this.numberOFHelipads = numberOFHelipads;
            this.longitude = longitude;
            this.latitude = latitude;
            this.altitude = altitude;
            this.towerLongitude = towerLongitude;
            this.towerLatitude = towerLatitude;
            this.towerAltitude = towerAltitude;
            this.magneticVariation = magneticVariation;
            icaoIdent >>= 5;
            while (icaoIdent > 37)
            {
                uint oneCodedChar = icaoIdent % 38;
                if (oneCodedChar == 0)
                    this.icaoIdent = " " + this.icaoIdent;
                else if (oneCodedChar > 1 && oneCodedChar < 12)
                    this.icaoIdent = (char)(48 + oneCodedChar - 2) + this.icaoIdent;
                else
                    this.icaoIdent = (char)(65 + oneCodedChar - 12) + this.icaoIdent;
                icaoIdent = (icaoIdent - oneCodedChar) / 38;
                if (icaoIdent < 38)
                {
                    oneCodedChar = icaoIdent;
                    if (oneCodedChar == 0)
                        this.icaoIdent = " " + this.icaoIdent;
                    else if (oneCodedChar > 1 && oneCodedChar < 12)
                        this.icaoIdent = (char)(48 + oneCodedChar - 2) + this.icaoIdent;
                    else
                        this.icaoIdent = (char)(65 + oneCodedChar - 12) + this.icaoIdent;
                    icaoIdent = (icaoIdent - oneCodedChar) / 38;
                }
            } 
            this.regionIdent = regionIdent;
            this.typeOfFuelAndAvailability = typeOfFuelAndAvailability;
            this.unknownFSX1 = unknownFSX1;
            this.trafficScalarFSX = trafficScalarFSX;
            this.unknownFSX2 = unknownFSX2;
            // Make custom class per subrecord type and add to switch statement, sub subrecord within subrecord 
            // (like always)
            UInt32 toRead = sizeOfRecord - 0x38;
            while (toRead > 0)
            {
                UInt32 size = 0;
                switch (BitConverter.ToUInt16(file, (int)offset))
                {
                    case 0x0019:    // Name subrecord
                        offset += 2; // Jump behind ID
                        size = BitConverter.ToUInt32(file, (int)offset);
                        offset += 4; // Jump behind size
                        offset += (size - 6); // Skip name subrecord
                        toRead -= size;
                        break;
                    case 0x0066: // Included Tower scenery object subrecord
                        offset += 2; // Jump behind ID
                        size = BitConverter.ToUInt32(file, (int)offset);
                        offset += 4; // Jump behind size
                        offset += (size - 6); // Skip include object subrecord
                        toRead -= size;
                        break;
                    case 0x0004: // Runway subrecord
                        offset += 2; // Jump behind ID
                        size = BitConverter.ToUInt32(file, (int)offset);
                        offset += 4; // Jump behind size
                        offset += (size - 6); // Skip runway subrecord
                        toRead -= size;
                        break;
                    case 0x0026: // Helipad subrecord
                        offset += 2; // Jump behind ID
                        size = BitConverter.ToUInt32(file, (int)offset);
                        offset += 4; // Jump behind size
                        offset += (size - 6); // Skip helipad subrecord
                        toRead -= size;
                        break;
                    case 0x0011: // Start subrecord
                        offset += 2; // Jump behind ID
                        size = BitConverter.ToUInt32(file, (int)offset);
                        offset += 4; // Jump behind size
                        offset += (size - 6); // Skip start subrecord
                        toRead -= size;
                        break;
                    case 0x0012: // Com subrecord
                        offset += 2; // Jump behind ID
                        size = BitConverter.ToUInt32(file, (int)offset);
                        offset += 4; // Jump behind size
                        offset += (size - 6); // Skip com subrecord
                        toRead -= size;
                        break;
                    case 0x0033: // Delete airport subrecord
                        offset += 2; // Jump behind ID
                        size = BitConverter.ToUInt32(file, (int)offset);
                        offset += 4; // Jump behind size
                        offset += (size - 6); // Skip delet airport subrecord
                        toRead -= size;
                        break;
                    case 0x0037: // Apron subrecord 1
                        offset += 2; // Jump behind ID
                        size = BitConverter.ToUInt32(file, (int)offset);
                        offset += 4; // Jump behind size
                        offset += (size - 6); // Skip apron subrecord 1
                        toRead -= size;
                        break;
                    case 0x0030: // Apron subrecord 2
                        offset += 2; // Jump behind ID
                        size = BitConverter.ToUInt32(file, (int)offset);
                        offset += 4; // Jump behind size
                        offset += (size - 6); // Skip apron subrecord 2
                        toRead -= size;
                        break;
                    case 0x0031: // Apron edge light subrecord
                        offset += 2; // Jump behind ID
                        size = BitConverter.ToUInt32(file, (int)offset);
                        offset += 4; // Jump behind size
                        offset += (size - 6); // Skip apron edge light subrecord
                        toRead -= size;
                        break;
                    case 0x001A: // Taxiway point subrecord
                        offset += 2; // Jump behind ID
                        size = BitConverter.ToUInt32(file, (int)offset);
                        offset += 4; // Jump behind size
                        UInt16 numberOfTaxiwayPoints = BitConverter.ToUInt16(file, (int)offset);
                        offset += 2; // Jump behind numbers
                        points = new TaxiwayPoint(0x001A, size, numberOfTaxiwayPoints, file, offset);
                        offset += (size - 8); // Skip taxiway point subrecord
                        toRead -= size;
                        break;
                    case 0x003D: // Taxiway parking subrecord
                        offset += 2; // Jump behind ID
                        size = BitConverter.ToUInt32(file, (int)offset);
                        offset += 4; // Jump behind size
                        UInt16 numberOfTaxiwayParkingPoints = BitConverter.ToUInt16(file, (int)offset);
                        offset += 2; // Jump behind numbers
                        parkings = new TaxiwayParking(0x003D, size, numberOfTaxiwayParkingPoints, file, offset);
                        offset += (size - 8); // Skip taxiway parking subrecord
                        toRead -= size;
                        break;
                    case 0x001C: // Taxiway path subrecord
                        offset += 2; // Jump behind ID
                        size = BitConverter.ToUInt32(file, (int)offset);
                        offset += 4; // Jump behind size
                        UInt16 numberOfTaxiwayPaths = BitConverter.ToUInt16(file, (int)offset);
                        offset += 2; // Jump behind numbers
                        paths = new TaxiwayPath(0x001C, size, numberOfTaxiwayPaths, file, offset);
                        offset += (size - 8); // Skip taxiway path subrecord
                        toRead -= size;
                        break;
                    case 0x001D: // Taxi name subrecord
                        offset += 2; // Jump behind ID
                        size = BitConverter.ToUInt32(file, (int)offset);
                        offset += 4; // Jump behind size
                        offset += (size - 6); // Skip taxi name subrecord
                        toRead -= size;
                        break;
                    case 0x003A: // Jetway subrecord
                        offset += 2; // Jump behind ID
                        size = BitConverter.ToUInt16(file, (int)offset);
                        offset += 2; // Jump behind size
                        offset += (size - 4); // Skip jetway subrecord
                        toRead -= size;
                        break;
                    case 0x0024: // Approach subrecord
                        offset += 2; // Jump behind ID
                        size = BitConverter.ToUInt32(file, (int)offset);
                        offset += 4; // Jump behind size
                        offset += (size - 6); // Skip approach subrecord
                        toRead -= size;
                        break;
                    case 0x0022: // Waypoint subrecord
                        offset += 2; // Jump behind ID
                        size = BitConverter.ToUInt32(file, (int)offset);
                        offset += 4; // Jump behind size
                        offset += (size - 6); // Skip waypoint subrecord
                        toRead -= size;
                        break;
                    case 0x0038: // Blast fence subrecord
                        offset += 2; // Jump behind ID
                        size = BitConverter.ToUInt32(file, (int)offset);
                        offset += 4; // Jump behind size
                        offset += (size - 6); // Skip blast fence subrecord
                        toRead -= size;
                        break;
                    case 0x0039: // Boundary fence subrecord
                        offset += 2; // Jump behind ID
                        size = BitConverter.ToUInt32(file, (int)offset);
                        offset += 4; // Jump behind size
                        offset += (size - 6); // Skip boundary fence subrecord
                        toRead -= size;
                        break;
                    case 0x003B: // Unknown subrecord
                        offset += 2; // Jump behind ID
                        size = BitConverter.ToUInt32(file, (int)offset);
                        offset += 4; // Jump behind size
                        offset += (size - 6); // Skip unknown subrecord
                        toRead -= size;
                        break;
                    default: // End up here with a valid subrecord, add to the switch. Otherwise this is bogous data
                        offset += 2;
                        toRead -= 2;
                        break;
                }
            }
        }
    }
}
