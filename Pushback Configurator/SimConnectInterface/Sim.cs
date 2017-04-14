using Microsoft.FlightSimulator.SimConnect;
using BGLParser;
using Pushback_Configurator.AppUI;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Device.Location;
using System.Collections.Generic;

namespace Pushback_Configurator.SimConnectInterface
{
    public class Sim
    {
        // ------------------------------------------------------------------------------
        // PROPERTIES
        // ------------------------------------------------------------------------------

        public bool connected { get; private set; } = false;

        // ------------------------------------------------------------------------------
        // FIELDS
        // ------------------------------------------------------------------------------

        // SimConnect object
        private SimConnect simconnect = null;
        // Refrence to main window for GUI work
        private MainWindow main;

        private bool simActive = false;
        private TaxiwayParking.Point parking;
        private List<TaxiwayPoint.Point> setMarkers = new List<TaxiwayPoint.Point>();
        private List<Tuple<TaxiwayPath.Point, int>> tempPoints = new List<Tuple<TaxiwayPath.Point, int>>();
        private uint tempMarkerID;
        private List<uint> setMarkerIDs = new List<uint>();
        private double altitude = 0;
        private int currentTempMarkerIndex = 0;
        private Airport airport;

        private enum DATA_DEFINITIONS
        {
            positionReport,
            positionSet,
        };
       
        private enum REQUESTS
        {
            userPosition,
            markerSet,
            markerTemp,
            markerDelete,
        };

        private enum EVENTS
        {
            simStart,
            simStop,
            text,
        };

        private enum INPUT_GROUPS
        {
        };

        private enum GROUPS
        {
        };

        // this is how you declare a data structure so that
        // simconnect knows how to fill it/read it.

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        private struct positionReport
        {
            public double onGround;
            public double latitude;
            public double longitude;
            public double altitude;
        };

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        private struct positionSet
        {
            public double latitude;
            public double longitude;
        };

        // ------------------------------------------------------------------------------
        // METHODS
        // ------------------------------------------------------------------------------

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="m"></param>
        public Sim(MainWindow m)
        {
            main = m;
        }

        /// <summary>
        /// Connects to the sim / Disconnect from the sim
        /// </summary>
        /// <param name="handle"></param>
        /// <param name="ID"></param>
        public void changeConnection(IntPtr handle, uint ID)
        {
            if (connected)
            {
                closeConnection();
            }
            else
            {
                try
                {
                    // Connect to FS
                    simconnect = new SimConnect("PBConfig", handle, ID, null, 0);
                    // Set recv handlers
                    simconnect.OnRecvQuit += new SimConnect.RecvQuitEventHandler(onRecvQuit);
                    simconnect.OnRecvEvent += new SimConnect.RecvEventEventHandler(onRecvEvent);
                    simconnect.OnRecvSimobjectData += new SimConnect.RecvSimobjectDataEventHandler(onRecvSimobjectData);
                    simconnect.OnRecvAssignedObjectId += new SimConnect.RecvAssignedObjectIdEventHandler(onRecvAssignedObjectId);
                    // user position
                    simconnect.AddToDataDefinition(DATA_DEFINITIONS.positionReport, "SIM ON GROUND", null, 
                                                   SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simconnect.AddToDataDefinition(DATA_DEFINITIONS.positionReport, "PLANE LATITUDE", "degrees", 
                                                   SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simconnect.AddToDataDefinition(DATA_DEFINITIONS.positionReport, "PLANE LONGITUDE", "degrees", 
                                                   SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simconnect.AddToDataDefinition(DATA_DEFINITIONS.positionReport, "PLANE ALTITUDE", "meters", 
                                                   SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simconnect.AddToDataDefinition(DATA_DEFINITIONS.positionSet, "PLANE LATITUDE", "degrees",
                                                   SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simconnect.AddToDataDefinition(DATA_DEFINITIONS.positionSet, "PLANE LONGITUDE", "degrees",
                                                   SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

                    // Events subscription
                    simconnect.SubscribeToSystemEvent(EVENTS.simStart, "SimStart");
                    simconnect.SubscribeToSystemEvent(EVENTS.simStop, "SimStop");
                    // Register with marshaller
                    simconnect.RegisterDataDefineStruct<positionReport>(DATA_DEFINITIONS.positionReport);
                    simconnect.RegisterDataDefineStruct<positionSet>(DATA_DEFINITIONS.positionSet);

                    connected = true;
                }
                catch (COMException ex)
                {
                    // A connection to the SimConnect server could not be established
                    MessageBox.Show("Exception: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Invoke SimConnect ReceiveMessage()
        /// </summary>
        public void handleMessage()
        {
            if (connected)
            {
                simconnect.ReceiveMessage();
            }
        }

        /// <summary>
        /// Displays given message as red static text
        /// </summary>
        /// <param name="text"></param>
        public void sendText(string text)
        {
            if (connected)
            {
                simconnect.Text(SIMCONNECT_TEXT_TYPE.PRINT_RED, 1, null, text);
            }
        }

        /// <summary>
        /// Saves the curretn selected node for pushback
        /// </summary>
        public void set()
        {
            setMarkers.Add(airport.points.findByIndex(tempPoints[currentTempMarkerIndex].Item2));
            simconnect.AIRemoveObject(tempMarkerID, REQUESTS.markerDelete);
            SIMCONNECT_DATA_INITPOSITION init = new SIMCONNECT_DATA_INITPOSITION();
            TaxiwayPoint.Point point = airport.points.findByIndex(tempPoints[currentTempMarkerIndex].Item2);
            init.Latitude = point.location.Latitude;
            init.Longitude = point.location.Longitude;
            init.Altitude = altitude;
            simconnect.AICreateSimulatedObject("AC_Unit_sm", init, REQUESTS.markerSet);
            tempPoints = airport.paths.getBy(true, TaxiwayPath.TYPE.PARKING, (UInt16)point.index);
        }

        /// <summary>
        /// Starts customization process
        /// </summary>
        public void customizePosition()
        {
            if (simActive)
                simconnect.RequestDataOnSimObject(REQUESTS.userPosition, DATA_DEFINITIONS.positionReport,
                                                  SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_PERIOD.ONCE, 0, 0, 0, 0);
        }

        /// <summary>
        /// Cylces through available nodes to set
        /// </summary>
        public void cycle()
        {
            if (currentTempMarkerIndex < tempPoints.Count - 1)
                currentTempMarkerIndex++;
            else
                currentTempMarkerIndex = 0;
            simconnect.AIRemoveObject(tempMarkerID, REQUESTS.markerDelete);
            SIMCONNECT_DATA_INITPOSITION init = new SIMCONNECT_DATA_INITPOSITION();
            init.Latitude = airport.points.findByIndex(tempPoints[currentTempMarkerIndex].Item2).location.Latitude;
            init.Longitude = airport.points.findByIndex(tempPoints[currentTempMarkerIndex].Item2).location.Longitude;
            init.Altitude = altitude;
            simconnect.AICreateSimulatedObject("Propane_Tank_sm", init, REQUESTS.markerTemp);
        }

        /// <summary>
        /// Finishes the setup
        /// </summary>
        public void finish()
        {
            main.display.Content = "";
            foreach (TaxiwayPoint.Point point in setMarkers)
            {
                main.display.Content += point.location.ToString() + "\n";
                tempPoints = null;
            }
            foreach (uint id in setMarkerIDs)
                simconnect.AIRemoveObject(id, REQUESTS.markerDelete);
        }
        
        /// <summary>
        /// Closes the connection to the sim
        /// </summary>
        private void closeConnection()
        {
            if (connected)
            {
                simconnect.Dispose();
                simconnect = null;
                connected = false;
            }
        }

        /// <summary>
        /// Received a sim quit event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="data"></param>
        private void onRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
        {
            closeConnection();
        }

        /// <summary>
        /// Callback for events (Sim Start and alike)
        /// </summary>
        private void onRecvEvent(SimConnect sender, SIMCONNECT_RECV_EVENT data)
        {
            switch ((EVENTS)data.uEventID)
            {
                case EVENTS.simStart:
                    simActive = true;
                    break;
                case EVENTS.simStop:
                    simActive = false;
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Callback for data recieve on sim objects
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="data"></param>
        private void onRecvSimobjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
        {
            switch ((REQUESTS)data.dwRequestID)
            {
                case REQUESTS.userPosition:
                    GeoCoordinate userPosition = new GeoCoordinate(((positionReport)data.dwData[0]).latitude,
                                                              ((positionReport)data.dwData[0]).longitude);
                    altitude = ((positionReport)data.dwData[0]).altitude * 3.28084;
                    Tuple<string, string> closestAirport = main.activeFiles.getClosestAirportTo(userPosition);
                    airport = new BGLFile(((App)Application.Current).registryPath, closestAirport.Item2).
                                      findAirport(closestAirport.Item1);
                    parking = airport.parkings.findClosestTo(userPosition);
                    // Get parking path which references the parking point ID
                    TaxiwayPath.Point parkingPath = airport.paths.getBy(TaxiwayPath.TYPE.PARKING, (UInt16)parking.index);
                    // Get point to push back to
                    setMarkers.Add(airport.points.findByIndex(parkingPath.startPointIndex));
                    // Set this marker
                    SIMCONNECT_DATA_INITPOSITION init = new SIMCONNECT_DATA_INITPOSITION();
                    init.Latitude = airport.points.findByIndex(parkingPath.startPointIndex).location.Latitude;
                    init.Longitude = airport.points.findByIndex(parkingPath.startPointIndex).location.Longitude;
                    init.Altitude = altitude;
                    simconnect.AICreateSimulatedObject("AC_Unit_sm", init, REQUESTS.markerSet);
                    tempPoints = airport.paths.getBy(true, TaxiwayPath.TYPE.PARKING, parkingPath.startPointIndex);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Assigned AI object IDs to save
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="data"></param>
        private void onRecvAssignedObjectId(SimConnect sender, SIMCONNECT_RECV_ASSIGNED_OBJECT_ID data)
        {
            switch ((REQUESTS)data.dwRequestID)
            {
                case REQUESTS.markerSet:
                    setMarkerIDs.Add(data.dwObjectID);
                    break;
                case REQUESTS.markerTemp:
                    tempMarkerID = data.dwObjectID;
                    break;
                default:
                    break;
            }
        }
    }
}

