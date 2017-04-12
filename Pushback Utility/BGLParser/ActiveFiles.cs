using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Device.Location;
using System.IO;
using System.Windows;
using System.Xml.Linq;

namespace Pushback_Utility.BGLParser
{
    public class ActiveFiles
    {
        
        internal Dictionary<string, object[]> activeFiles = new Dictionary<string, object[]>();

       

        public ActiveFiles()
        {
            IEnumerable<XElement> airports = XElement.Parse(File.ReadAllText(((App)Application.Current).registryPath + "runways.xml")).Elements("ICAO");
            foreach (XElement airport in airports)
                activeFiles.Add((string)airport.Attribute("id"), 
                                new object[] { airport.Element("File").Value,
                                Convert.ToDouble(airport.Element("Latitude").Value),
                                Convert.ToDouble(airport.Element("Longitude").Value) });
        }

        public Tuple<string, string> getClosestAirportTo(GeoCoordinate position)
        {
            double lastDistance = double.MaxValue;
            string closest = "";
            foreach (KeyValuePair<string, object[]> port in activeFiles)
            {
                GeoCoordinate portPos = new GeoCoordinate((double)port.Value[1], (double)port.Value[2]);
                double distanceUserPort = position.GetDistanceTo(portPos);
                if (distanceUserPort < lastDistance)
                {
                    closest = port.Key;
                    lastDistance = distanceUserPort;
                }
            }
            try
            {
                return new Tuple<string, string>(closest, (string)activeFiles[closest][0]);
            }
            catch (KeyNotFoundException)
            {
                throw new Exception("File not found");
            }
        }
    }
}
