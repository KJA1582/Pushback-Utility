using Microsoft.FlightSimulator.SimConnect;
using Pushback_Utility.AppUI;
using BGLParser;
using System;
using System.Device.Location;
using System.Runtime.InteropServices;
using System.Windows;

namespace Pushback_Utility.SimConnectInterface
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

        private bool simActive = false;
        private bool pushbackApproved = false;
        private bool pushbackActive = false;
        private bool pushbackInTurn = false;
        private bool pushbackInFinal = false;
        private bool pushbackDone = false;
        private bool pushbackRight = true;
        private bool initialRight = true;

        private double headingDeltaPushback = 0;
        private double headingDeltaToPushbackEnd = 0;
        private double targetHeading = 0;
        private double targetHeadingInitial = 0;
        private double turnEndDistance = 10; // MUST BE SET BY USER

        private Airport airport = null;

        private TaxiwayPoint.Point pushbackEnd = null;
        private TaxiwayParking.Point parking = null;

        private ActiveFiles activeFiles = null;

        private positionReport userPos;

        // To change turn radius, adjust following values. See GeoGebra file for more information and visualization
        private double turnDiameter = 120 / Math.PI; // FOR METERS
        private double rotationalMultiplier = 1;
        private double baseForwardSpeed = 1;
        private double slowDownFactor = 1.015;

        // SimConnect object
        private SimConnect simconnect = null;
        // Refrence to main window for GUI work
        private MainWindow main;

        private enum DATA_DEFINITIONS
        {
            pushbackStartCondition,
            positionReport,
            userVelocityZ,
            userVelocityRotY,
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
            public double altidude;
            public double heading;
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
                    simconnect.AddToDataDefinition(DATA_DEFINITIONS.positionReport, "PLANE ALTITUDE", "meters", 
                                                   SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simconnect.AddToDataDefinition(DATA_DEFINITIONS.positionReport, "PLANE HEADING DEGREES TRUE", "degrees", 
                                                   SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simconnect.AddToDataDefinition(DATA_DEFINITIONS.userVelocityZ, "VELOCITY BODY Z", "meters/second", 
                                                   SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simconnect.AddToDataDefinition(DATA_DEFINITIONS.userVelocityRotY, "ROTATION VELOCITY BODY Y", 
                                                   "degrees per second", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, 
                                                   SimConnect.SIMCONNECT_UNUSED);
                    // IMPORTANT: register it with the simconnect managed wrapper marshaller
                    // if you skip this step, you will only receive a uint in the .dwData field.
                    simconnect.RegisterDataDefineStruct<pushbackStartCondition>(DATA_DEFINITIONS.pushbackStartCondition);
                    simconnect.RegisterDataDefineStruct<positionReport>(DATA_DEFINITIONS.positionReport);
                    simconnect.RegisterDataDefineStruct<adjust>(DATA_DEFINITIONS.userVelocityRotY);
                    simconnect.RegisterDataDefineStruct<adjust>(DATA_DEFINITIONS.userVelocityZ);

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

        public void selected()
        {
            TaxiwayPoint.Point selected = null; 
            // Get point to push back to
            selected = airport.points.findByIndex(((Tuple<TaxiwayPath.Point, int>)main.directions.SelectedItem).Item2);
            double bearingPushbackEndSelected = AngleMath.bearingDegrees(true, pushbackEnd.location, selected.location);
            double bearingPushbackEndParking = AngleMath.bearingDegrees(true, pushbackEnd.location, parking.location);
            double recpirocalHeading = 0;

            if (bearingPushbackEndSelected < 180)
                targetHeading += 180 + bearingPushbackEndSelected;
            else
                targetHeading = bearingPushbackEndSelected - 180;
            targetHeadingInitial = bearingPushbackEndParking;

            if (bearingPushbackEndParking < 180)
                recpirocalHeading += 180 + bearingPushbackEndParking;
            else
                recpirocalHeading = bearingPushbackEndParking - 180;

            if (recpirocalHeading > 180 && ((recpirocalHeading < targetHeading && targetHeading < 360)
                || (0 < targetHeading && targetHeading < bearingPushbackEndParking)))
                pushbackRight = false;
            else if (recpirocalHeading < targetHeading && targetHeading < bearingPushbackEndParking)
                pushbackRight = false;

            if (recpirocalHeading > 180 && recpirocalHeading < targetHeadingInitial && targetHeadingInitial < 360
                && 0 < targetHeadingInitial && targetHeadingInitial < bearingPushbackEndParking)
                initialRight = false;
            else if (recpirocalHeading < targetHeadingInitial && targetHeadingInitial < bearingPushbackEndParking)
                initialRight = false;

            headingDeltaPushback = Math.Abs(headingDeltaPushback - targetHeading);
            if (headingDeltaPushback > 180)
                headingDeltaPushback = 360 - headingDeltaPushback;
            headingDeltaToPushbackEnd = Math.Abs(userPos.heading - bearingPushbackEndParking);
            if (headingDeltaToPushbackEnd > 180)
                headingDeltaToPushbackEnd = 360 - headingDeltaToPushbackEnd;

            pushbackActive = true;
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
                            sendText("On ground, parking brake set. Please confirm all conditions met.");
                        else if (pushbackApproved && !pushbackActive)
                            sendText("Shift+P to start pushback");
                        else if (pushbackApproved && pushbackActive && pBrake && !pushbackDone)
                            sendText("Release parking brake");
                        else if (pushbackApproved && pushbackActive && !pBrake && !pushbackDone)
                            simconnect.RequestDataOnSimObject(REQUESTS.userPositionDuringPushback, 
                                                              DATA_DEFINITIONS.positionReport, 
                                                              SimConnect.SIMCONNECT_OBJECT_ID_USER, 
                                                              SIMCONNECT_PERIOD.ONCE, 0, 0, 0, 0);
                        else if (pushbackApproved && pushbackActive && !pBrake && pushbackDone)
                        {
                            sendText("Pushback complete, set parking brake");
                            adjust hold = new adjust();
                            hold.value = 0;
                            simconnect.SetDataOnSimObject(DATA_DEFINITIONS.userVelocityZ, 
                                                          SimConnect.SIMCONNECT_OBJECT_ID_USER, 0, hold);
                        }
                        else if (pushbackApproved && pushbackActive && pBrake && pushbackDone)
                        {
                            pushbackApproved = pushbackActive = pushbackInTurn = pushbackInFinal = pushbackDone = false;
                            headingDeltaPushback = 0;
                        }
                    }
                    break;
                case REQUESTS.userPositionOnStart:
                    userPos = ((positionReport)data.dwData[0]);
                    headingDeltaPushback = ((positionReport)data.dwData[0]).heading;
                    main.directions.ItemsSource = null;
                    GeoCoordinate userPosition = new GeoCoordinate(((positionReport)data.dwData[0]).latitude, 
                                                              ((positionReport)data.dwData[0]).longitude);
                    Tuple<string, string> closestAirport = activeFiles.getClosestAirportTo(userPosition);
                    airport = new BGLFile(((App)Application.Current).registryPath, closestAirport.Item2).
                                          findAirport(closestAirport.Item1);
                    TaxiwayPath.Point parkingPath = null;
                    // Search closest parking point, i.e. user within radius
                    try
                    {
                        parking = airport.parkings.findClosestTo(userPosition);
                        // Get parking path which references the parking point ID
                        parkingPath = airport.paths.getBy(TaxiwayPath.TYPE.PARKING, (UInt16)parking.index);
                        // Get point to push back to
                        pushbackEnd = airport.points.findByIndex(parkingPath.startPointIndex);
                        // Get paths which references the pushback end point as start or end and is not of parking type
                        main.directions.ItemsSource = airport.paths.getBy(true, TaxiwayPath.TYPE.PARKING, 
                                                                          parkingPath.startPointIndex);  
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show(e.ToString());
                    }
                    break;
                case REQUESTS.userPositionDuringPushback:
                    positionReport user = (positionReport)data.dwData[0];
                    adjust adjSpeed = new adjust();
                    adjust adjRota = new adjust();
                    double untilTurn = AngleMath.distanceUntilTurn(headingDeltaPushback, turnDiameter);
                    userPosition = new GeoCoordinate(user.latitude, user.longitude);
                    double distance = userPosition.GetDistanceTo(pushbackEnd.location);
                    if (!pushbackInTurn)
                        sendText(((int)Math.Round(distance - untilTurn)).ToString() + " m remaining until turning begins");
                    if (targetHeading - 0.5 < user.heading && user.heading < targetHeading + 0.5)
                        pushbackInFinal = true;
                    if (pushbackInFinal && distance > untilTurn + turnEndDistance)
                        pushbackDone = true;
                    else
                    {
                        adjSpeed.value = -baseForwardSpeed;
                        if (!pushbackInTurn &&
                            !(targetHeadingInitial - 0.5 < user.heading && user.heading < targetHeadingInitial + 0.5))
                        {
                            if (initialRight)
                                adjRota.value = 0.1;
                            else
                                adjRota.value = -0.1;
                        }
                        if (!pushbackInFinal && (pushbackInTurn || distance - untilTurn < 1))
                        {
                            pushbackInTurn = true;
                            adjSpeed.value /= slowDownFactor;
                            if (pushbackRight)
                                adjRota.value = rotationalMultiplier;
                            else
                                adjRota.value = -rotationalMultiplier;
                        }
                        simconnect.SetDataOnSimObject(DATA_DEFINITIONS.userVelocityRotY,
                                                      SimConnect.SIMCONNECT_OBJECT_ID_USER, 0, adjRota);
                        simconnect.SetDataOnSimObject(DATA_DEFINITIONS.userVelocityZ,
                                                      SimConnect.SIMCONNECT_OBJECT_ID_USER, 0, adjSpeed);
                    }
                    break;
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
                    simconnect.RequestDataOnSimObject(REQUESTS.pushbackStartCondition, DATA_DEFINITIONS.pushbackStartCondition, 
                                                      SimConnect.SIMCONNECT_OBJECT_ID_USER, 
                                                      SIMCONNECT_PERIOD.SIM_FRAME, 0, 0, 0, 0);
                    simActive = true;
                    break;
                case EVENTS.simStop:
                    simconnect.RequestDataOnSimObject(REQUESTS.pushbackStartCondition, DATA_DEFINITIONS.pushbackStartCondition, 
                                                      SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_PERIOD.NEVER, 0, 0, 0, 0);
                    simActive = false;
                    break;
                case EVENTS.eventShiftP:
                    if (pushbackApproved && !pushbackActive)
                    {
                        simconnect.RequestDataOnSimObject(REQUESTS.userPositionOnStart, DATA_DEFINITIONS.positionReport, 
                                                          SimConnect.SIMCONNECT_OBJECT_ID_USER, 
                                                          SIMCONNECT_PERIOD.ONCE, 0, 0, 0, 0);
                    }
                    break;
                default:
                    break;
            }
        }
    }
}

