﻿using Microsoft.FlightSimulator.SimConnect;
using Pushback_Utility.AppUI;
using BGLParser;
using System;
using System.Device.Location;
using System.Runtime.InteropServices;
using System.Windows;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace Pushback_Utility.SimConnectInterface
{
    public class Sim
    {
        // ------------------------------------------------------------------------------
        // Constants
        // ------------------------------------------------------------------------------

        private const double turnRadius = 60 / Math.PI;

        // ------------------------------------------------------------------------------
        // PROPERTIES
        // ------------------------------------------------------------------------------

        public bool connected { get; private set; } = false;

        // ------------------------------------------------------------------------------
        // FIELDS
        // ------------------------------------------------------------------------------

        // Status connection
        private bool simActive = false;

        // Status pushback, reset at end of pushback
        private bool pushbackApproved = false;
        private bool pushbackActive = false;
        private bool pushbackInTurn = false;

        // Selection and path, reset at end of pushback
        private List<string> files = new List<string>();
        private List<GeoCoordinate> pushbackPath = new List<GeoCoordinate>();

        private ActiveFiles activeFiles = null;

        // SimConnect object
        private SimConnect simconnect = null;

        // Reference to main window for GUI work
        private MainWindow main;

        private enum DATA_DEFINITIONS
        {
            pushbackStartCondition,
            positionReport,
            userVelocityZ,
            userVelocityRotY,
            userHeading,
        }
       
        private enum REQUESTS
        {
            pushbackStartCondition,
            userPositionOnStart,
            userPositionDuringPushback,
            airportList,
        };

        private enum EVENTS
        {
            simStart,
            simStop,
            eventShiftP,
            menu,
        };

        private enum INPUT_GROUPS
        {
            inputStart,
        };

        private enum GROUPS
        {
            groupKeys,
            groupEvents,
        };

        // this is how you declare a data structure so that
        // simconnect knows how to fill it/read it.

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        private struct pushbackStartCondition
        {
            public double onGround;
            public double parkingBrakeSet;
        };

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        private struct positionReport
        {
            public double latitude;
            public double longitude;
            public double heading;
            public double parkingBrake;
        };

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        private struct adjust
        {
            public double value;
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
                    // Load list of active scenery files
                    activeFiles = new ActiveFiles(((App)Application.Current).registryPath);
                    // Connect to FS
                    simconnect = new SimConnect("PBUtil", handle, ID, null, 0);
                    // Set recv handlers
                    simconnect.OnRecvQuit += new SimConnect.RecvQuitEventHandler(onRecvQuit);
                    simconnect.OnRecvEvent += new SimConnect.RecvEventEventHandler(onRecvEvent);
                    simconnect.OnRecvSimobjectData += new SimConnect.RecvSimobjectDataEventHandler(onRecvSimobjectData);

                    // Events subscription
                    simconnect.SubscribeToSystemEvent(EVENTS.simStart, "SimStart");
                    simconnect.SubscribeToSystemEvent(EVENTS.simStop, "SimStop");

                    // Start of pushback with Shift+P
                    simconnect.MapClientEventToSimEvent(EVENTS.eventShiftP, null);
                    simconnect.AddClientEventToNotificationGroup(GROUPS.groupKeys, EVENTS.eventShiftP, false);
                    simconnect.MapInputEventToClientEvent(INPUT_GROUPS.inputStart, "shift+p", EVENTS.eventShiftP, 1, null, 0, 
                                                          true);

                    // Group priorities
                    simconnect.SetNotificationGroupPriority(GROUPS.groupKeys, SimConnect.SIMCONNECT_GROUP_PRIORITY_HIGHEST);
                    simconnect.SetNotificationGroupPriority(GROUPS.groupEvents, SimConnect.SIMCONNECT_GROUP_PRIORITY_HIGHEST);
                    simconnect.SetInputGroupPriority(INPUT_GROUPS.inputStart, SimConnect.SIMCONNECT_GROUP_PRIORITY_HIGHEST);

                    // Input group states
                    simconnect.SetInputGroupState(INPUT_GROUPS.inputStart, 1);

                    // Define data structures
                    simconnect.AddToDataDefinition(DATA_DEFINITIONS.pushbackStartCondition, "SIM ON GROUND", null, 
                                                   SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simconnect.AddToDataDefinition(DATA_DEFINITIONS.pushbackStartCondition, "BRAKE PARKING INDICATOR", null, 
                                                   SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simconnect.AddToDataDefinition(DATA_DEFINITIONS.positionReport, "PLANE LATITUDE", "degrees", 
                                                   SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simconnect.AddToDataDefinition(DATA_DEFINITIONS.positionReport, "PLANE LONGITUDE", "degrees", 
                                                   SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simconnect.AddToDataDefinition(DATA_DEFINITIONS.positionReport, "PLANE HEADING DEGREES TRUE", "degrees", 
                                                   SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simconnect.AddToDataDefinition(DATA_DEFINITIONS.positionReport, "BRAKE PARKING INDICATOR", null,
                                                   SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simconnect.AddToDataDefinition(DATA_DEFINITIONS.userVelocityZ, "VELOCITY BODY Z", "meters/second", 
                                                   SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simconnect.AddToDataDefinition(DATA_DEFINITIONS.userVelocityRotY, "ROTATION VELOCITY BODY Y", 
                                                   "degrees per second", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, 
                                                   SimConnect.SIMCONNECT_UNUSED);
                    simconnect.AddToDataDefinition(DATA_DEFINITIONS.userHeading, "PLANE HEADING DEGREES TRUE",
                                                   "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    // IMPORTANT: register it with the simconnect managed wrapper marshaller
                    // if you skip this step, you will only receive a uint in the .dwData field.
                    simconnect.RegisterDataDefineStruct<pushbackStartCondition>(DATA_DEFINITIONS.pushbackStartCondition);
                    simconnect.RegisterDataDefineStruct<positionReport>(DATA_DEFINITIONS.positionReport);
                    simconnect.RegisterDataDefineStruct<adjust>(DATA_DEFINITIONS.userVelocityRotY);
                    simconnect.RegisterDataDefineStruct<adjust>(DATA_DEFINITIONS.userVelocityZ);
                    simconnect.RegisterDataDefineStruct<adjust>(DATA_DEFINITIONS.userHeading);

                    connected = true;
                }
                catch (COMException ex)
                {
                    // A connection to the SimConnect server could not be established
                    MessageBox.Show("Exception: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    main.button.Content = "Connect to FS";
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
        private void sendText(string text)
        {
            if (connected)
            {
                simconnect.Text(SIMCONNECT_TEXT_TYPE.PRINT_RED, 1, null, text);
            }
        }

        /// <summary>
        /// Closes the connection to the sim
        /// </summary>
        private void closeConnection()
        {
            if (connected)
            {
                activeFiles = null;
                simconnect.Dispose();
                simconnect = null;
                main.button.Content = "Connect to FS";
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
        /// Callback for SimObject data requests
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="data"></param>
        private void onRecvSimobjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
        {
            bool pBrake = false;
            switch ((REQUESTS)data.dwRequestID)
            {
                case REQUESTS.pushbackStartCondition:
                    {
                        if (simActive && !pushbackActive)
                        {
                            pBrake = Convert.ToBoolean(((pushbackStartCondition)data.dwData[0]).parkingBrakeSet);
                            bool onGround = Convert.ToBoolean(((pushbackStartCondition)data.dwData[0]).onGround);
                            pushbackApproved = pBrake && onGround;
                        }
                        if (simActive)
                        {
                            pBrake = Convert.ToBoolean(((pushbackStartCondition)data.dwData[0]).parkingBrakeSet);
                            if (!pushbackApproved)
                                sendText("Please confirm on ground and parking brake set.");
                            else
                                simconnect.RequestDataOnSimObject(REQUESTS.userPositionOnStart, DATA_DEFINITIONS.positionReport,
                                                                  SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_PERIOD.ONCE,
                                                                  0, 0, 0, 0);
                        }
                        break;
                    }
                case REQUESTS.userPositionOnStart:
                    {
                        GeoCoordinate userPosition = new GeoCoordinate(((positionReport)data.dwData[0]).latitude,
                                                                       ((positionReport)data.dwData[0]).longitude);
                        Tuple<string, string> closestAirport = activeFiles.getClosestAirportTo(userPosition);
                        Airport airport = new BGLFile(((App)Application.Current).registryPath, closestAirport.Item2).
                                              findAirport(closestAirport.Item1);
                        // Search closest parking point, i.e. user within radius
                        try
                        {
                            TaxiwayParking.Point parking = airport.parkings.findClosestTo(userPosition);
                            // Get files
                            string filePrefix = closestAirport.Item2.Split('\\').Last() + closestAirport.Item1 +
                                                parking.type.ToString() + parking.name.ToString() + parking.number.ToString();
                            string[] filesInDir = Directory.GetFiles("airports");
                            string menu = "Pushback\0Choose diection:\0";
                            foreach (string file in filesInDir)
                                if (file.Contains(filePrefix))
                                {
                                    files.Add(file);
                                    menu += file.Replace("airports\\" + filePrefix, "").Replace(".xml", "") + "\0";
                                }
                            menu += "Cancel\0";
                            simconnect.Text(SIMCONNECT_TEXT_TYPE.MENU, 0, EVENTS.menu, menu);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Exception: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        break;
                    }
                case REQUESTS.userPositionDuringPushback:
                    {
                        GeoCoordinate userPosition = new GeoCoordinate(((positionReport)data.dwData[0]).latitude,
                                                                           ((positionReport)data.dwData[0]).longitude);
                        adjust positionAdjustment = new adjust();
                        if (pushbackPath.Count > 0)
                        {
                            double distanceToPoint = userPosition.GetDistanceTo(pushbackPath[0]);
                            double reciprocalBearingToPoint = AngleMath.reciprocal(
                                                                  AngleMath.bearingDegrees(true, userPosition, pushbackPath[0]));

                            if (Convert.ToBoolean(((positionReport)data.dwData[0]).parkingBrake) && pushbackPath.Count > 0)
                                sendText("Release parking brake.");
                            else if (!Convert.ToBoolean(((positionReport)data.dwData[0]).parkingBrake) && pushbackPath.Count > 1)
                            {
                                double targetHeading = AngleMath.targetHeading(pushbackPath[0], pushbackPath[1]);
                                double headingDelta = Math.Abs(((positionReport)data.dwData[0]).heading - targetHeading);
                                if (headingDelta > 180)
                                    headingDelta -= 180;;

                                sendText("Until turn: " + Math.Round(distanceToPoint, 2) + " m");

                                if (distanceToPoint > 0.1 && !pushbackInTurn)
                                {
                                    if (AngleMath.leftOfUser(((positionReport)data.dwData[0]).heading, userPosition,
                                        pushbackPath.First()) == AngleMath.DIRECTION.LEFT)
                                    {
                                        positionAdjustment.value = 1;
                                        simconnect.SetDataOnSimObject(DATA_DEFINITIONS.userVelocityRotY,
                                                                      SimConnect.SIMCONNECT_OBJECT_ID_USER, 0, positionAdjustment);
                                    }
                                    else if (AngleMath.leftOfUser(((positionReport)data.dwData[0]).heading, userPosition,
                                             pushbackPath.First()) == AngleMath.DIRECTION.RIGHT)
                                    {
                                        positionAdjustment.value = -1;
                                        simconnect.SetDataOnSimObject(DATA_DEFINITIONS.userVelocityRotY,
                                                                      SimConnect.SIMCONNECT_OBJECT_ID_USER, 0, positionAdjustment);
                                    }
                                    else if (AngleMath.leftOfUser(((positionReport)data.dwData[0]).heading, userPosition,
                                             pushbackPath.First()) == AngleMath.DIRECTION.STRAIGHT)
                                    {
                                        positionAdjustment.value = reciprocalBearingToPoint;
                                        simconnect.SetDataOnSimObject(DATA_DEFINITIONS.userHeading,
                                                                      SimConnect.SIMCONNECT_OBJECT_ID_USER, 0, positionAdjustment);
                                    }
                                }
                                else
                                {
                                    pushbackInTurn = true;
                                    if (headingDelta < 0.1)
                                    {
                                        pushbackInTurn = false;
                                        pushbackPath.RemoveAt(0);
                                    }
                                    else if (AngleMath.leftOfUser(((positionReport)data.dwData[0]).heading, userPosition,
                                             pushbackPath[1]) == AngleMath.DIRECTION.LEFT)
                                        positionAdjustment.value = 1;
                                    else if (AngleMath.leftOfUser(((positionReport)data.dwData[0]).heading, userPosition,
                                             pushbackPath[1]) == AngleMath.DIRECTION.RIGHT)
                                        positionAdjustment.value = -1;
                                    else
                                    {
                                        pushbackInTurn = false;
                                        pushbackPath.RemoveAt(0);
                                    }
                                    simconnect.SetDataOnSimObject(DATA_DEFINITIONS.userVelocityRotY,
                                                                      SimConnect.SIMCONNECT_OBJECT_ID_USER, 0, positionAdjustment);
                                }

                                if (distanceToPoint > 0.1 && !pushbackInTurn)
                                {
                                    positionAdjustment.value = -1;
                                    simconnect.SetDataOnSimObject(DATA_DEFINITIONS.userVelocityZ,
                                                                  SimConnect.SIMCONNECT_OBJECT_ID_USER, 0, positionAdjustment);
                                    break;
                                }
                                else if (pushbackInTurn)
                                {
                                    positionAdjustment.value = 0;
                                    simconnect.SetDataOnSimObject(DATA_DEFINITIONS.userVelocityZ,
                                                                  SimConnect.SIMCONNECT_OBJECT_ID_USER, 0, positionAdjustment);
                                }
                                else
                                {
                                    pushbackPath.RemoveAt(0);
                                    break;
                                }
                            }
                            else if (!Convert.ToBoolean(((positionReport)data.dwData[0]).parkingBrake) && pushbackPath.Count == 1)
                            {
                                sendText("Until stop: " + Math.Round(distanceToPoint, 2) + " m");
                                if (distanceToPoint > 0.1)
                                {
                                    if (AngleMath.leftOfUser(((positionReport)data.dwData[0]).heading, userPosition,
                                        pushbackPath.First()) == AngleMath.DIRECTION.LEFT)
                                    {
                                        positionAdjustment.value = 1;
                                        simconnect.SetDataOnSimObject(DATA_DEFINITIONS.userVelocityRotY,
                                                                      SimConnect.SIMCONNECT_OBJECT_ID_USER, 0, positionAdjustment);
                                    }
                                    else if (AngleMath.leftOfUser(((positionReport)data.dwData[0]).heading, userPosition,
                                             pushbackPath.First()) == AngleMath.DIRECTION.RIGHT)
                                    {
                                        positionAdjustment.value = -1;
                                        simconnect.SetDataOnSimObject(DATA_DEFINITIONS.userVelocityRotY,
                                                                      SimConnect.SIMCONNECT_OBJECT_ID_USER, 0, positionAdjustment);
                                    }
                                    else if (AngleMath.leftOfUser(((positionReport)data.dwData[0]).heading, userPosition,
                                             pushbackPath.First()) == AngleMath.DIRECTION.STRAIGHT)
                                    {
                                        positionAdjustment.value = reciprocalBearingToPoint;
                                        simconnect.SetDataOnSimObject(DATA_DEFINITIONS.userHeading,
                                                                      SimConnect.SIMCONNECT_OBJECT_ID_USER, 0, positionAdjustment);
                                    }
                                    positionAdjustment.value = -1;
                                    simconnect.SetDataOnSimObject(DATA_DEFINITIONS.userVelocityZ,
                                                                  SimConnect.SIMCONNECT_OBJECT_ID_USER, 0, positionAdjustment);
                                }
                                else
                                    pushbackPath.RemoveAt(0);
                                break;
                            }
                        }
                        else if (!Convert.ToBoolean(((positionReport)data.dwData[0]).parkingBrake) && pushbackPath.Count == 0)
                        {
                            sendText("Set parking brake.");

                            pushbackPath.Clear();
                            files.Clear();
                            pushbackApproved = pushbackActive = pushbackInTurn = false;

                            positionAdjustment.value = 0;
                            simconnect.SetDataOnSimObject(DATA_DEFINITIONS.userVelocityZ,
                                                          SimConnect.SIMCONNECT_OBJECT_ID_USER, 0, positionAdjustment);
                        }
                        else if (Convert.ToBoolean(((positionReport)data.dwData[0]).parkingBrake))
                            simconnect.RequestDataOnSimObject(REQUESTS.userPositionDuringPushback,
                                                              DATA_DEFINITIONS.positionReport,
                                                              SimConnect.SIMCONNECT_OBJECT_ID_USER,
                                                              SIMCONNECT_PERIOD.NEVER, 0, 0, 0, 0);
                        break;
                    }
                default:
                    break;
            }
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
                case EVENTS.eventShiftP:
                    if (simActive)
                        simconnect.RequestDataOnSimObject(REQUESTS.pushbackStartCondition,
                                                          DATA_DEFINITIONS.pushbackStartCondition,
                                                          SimConnect.SIMCONNECT_OBJECT_ID_USER,
                                                          SIMCONNECT_PERIOD.ONCE, 0, 0, 0, 0);  
                    break;
                case EVENTS.menu:
                    if (((int)data.dwData) < 9)
                    {
                        try
                        {
                            StreamReader doc = File.OpenText(files[(int)data.dwData]);
                            XmlSerializer xsSubmit = new XmlSerializer(typeof(List<GeoCoordinate>));
                            pushbackPath = (List<GeoCoordinate>)xsSubmit.Deserialize(doc);
                            simconnect.RequestDataOnSimObject(REQUESTS.userPositionDuringPushback,
                                                              DATA_DEFINITIONS.positionReport,
                                                              SimConnect.SIMCONNECT_OBJECT_ID_USER,
                                                              SIMCONNECT_PERIOD.SIM_FRAME, 0, 0, 0, 0);
                        }
                        catch (ArgumentOutOfRangeException) { }
                    }
                    break;
                default:
                    break;
            }
        }
    }
}

