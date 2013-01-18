/// <summary>
/// The %Rovio wrapper library. Includes the Robot class, API namespace with all commands as specified by the %API specification 
/// and a set of helper classes for easy access to robot's resources.
/// 
/// The majority of comments are based directly on the official %API Specification for %Rovio document:
/// http://www.wowwee.com/static/support/rovio/manuals/Rovio_API_Specifications_v1.2.pdf.
/// </summary>
namespace Rovio
{
    using System;
    using System.Net;
    using System.IO;
    using System.Drawing;

    /// <summary>
    /// The main class for communication with Rovio through a web client.
    /// </summary>
    public class Robot
    {
        /// <summary>
        /// The constructor.
        /// </summary>
        public Robot(string address, string user, string password)
        {
            web_client = new WebClientTimeOut();
            web_client.BaseAddress = address;
            web_client.Credentials = new NetworkCredential(user, password);
            web_client.Proxy = null;
            web_client.TimeOut = 2000;

            API = new Rovio.API.API(this);
            Drive = new Drive(this);
            Camera = new Camera(this);
            IRSensor = new IRSensor(this);
            NavigationSensor = new NavigationSensor(this);
        }

        /// <summary>
        /// API commands.
        /// </summary>
        public API.API API;

        /// <summary>
        /// Drive commands.
        /// </summary>
        public Drive Drive;

        /// <summary>
        /// Camera commands.
        /// </summary>
        public Camera Camera;

        /// <summary>
        /// Odometry commands.
        /// </summary>
        public Odometry Odometry;

        /// <summary>
        /// IR Sensor commands.
        /// </summary>
        public IRSensor IRSensor;

        /// <summary>
        /// Navigation sensor commands.
        /// </summary>
        public NavigationSensor NavigationSensor;

        /// <summary>
        /// Request a response from the robot.
        /// </summary>
        /// <param name="request">Request string.</param>
        /// <returns>Response string.</returns>
        public string Request(string request)
        {
            return web_client.DownloadString(web_client.BaseAddress + request);
        }

        /// <summary>
        /// Request a stream response from the robot.
        /// Used by GetImage.cgi command.
        /// </summary>
        /// <param name="request">Request string.</param>
        /// <returns>Response stream.</returns>
        public Stream StreamRequest(string request)
        {
            return web_client.OpenRead(web_client.BaseAddress + request);
        }

        /// <summary>
        /// WebClient class supporting timeouts.
        /// </summary>
        private class WebClientTimeOut : WebClient
        {
            public int TimeOut { get; set; }

            protected override WebRequest GetWebRequest(Uri address)
            {
                WebRequest request = (WebRequest)base.GetWebRequest(address);
                request.Timeout = TimeOut;
                return request;
            }
        }

        private WebClientTimeOut web_client;
    }

    /// <summary>
    /// Interface for different convenience classes that require access to the Robot class.
    /// </summary>
    public abstract class Component
    {
        /// <summary>
        /// The robot class, accessible by all dervied classes.
        /// </summary>
        protected Robot robot;

        /// <summary>
        /// The constructor.
        /// </summary>
        public Component(Robot _robot) { robot = _robot; }

        /// <summary>
        /// Automatic update option for methods requesting more than one piece of information.
        /// </summary>
        public bool AutoUpdate = true;

        /// <summary>
        /// The Update function that manually refreshes the state of a given component (e.g. when AutoUpdate = false)
        /// </summary>
        public virtual void Update() {}
    }

    /// <summary>
    /// All commands as specified by the %API document.
    /// </summary>
    namespace API
    {
        /// <summary>
        /// A base class for all movement commands.
        /// </summary>
        public abstract class MovementComponent : Component
        {
            /// <summary>
            /// The constructor.
            /// </summary>
            public MovementComponent(Robot _robot) : base(_robot) { }

            /// <summary>
            /// Parse the specific parameter and return its value.
            /// </summary>
            protected string GetParameter(string value)
            {
                int start, end;
                start = report.IndexOf(value + "=");
                if (start != -1)
                {
                    start += value.Length + 1;
                    end = report.IndexOf("|", start);
                    if (end != -1)
                        return report.Substring(start, end - start);
                    else
                        return report.Substring(start);
                }
                throw new Exception("MovementComponent::GetParamemter, Wrong value.");
            }

            /// <summary>
            /// Request and parse the movement command.
            /// </summary>
            /// <param name="value">action value</param>
            /// <returns>response from the robot</returns>
            public virtual string Request(string value)
            {
                string response = robot.Request("rev.cgi?Cmd=nav&action=" + value);

                int start, end;
                start = response.IndexOf("Cmd = nav");
                if (start != -1)
                {
                    start = response.IndexOf("responses = ", start + 10);
                    if (start != -1)
                    {
                        start += 12;
                        end = response.IndexOf("|", start);
                        if (end == -1)
                            end = response.IndexOf("\n", start);
                        if (end != -1)
                        {
                            string response_id = response.Substring(start, end - start);
                            if (Int32.Parse(response_id) == 0)
                                return response.Substring(end + 1);
                            throw new Rovio.API.Movement.MovementResponseException(Convert.ToInt32(response_id));
                        }
                    }
                }
                throw new Exception("MovementComponent::Request, Wrong response.");
            }

            /// <summary>
            /// Stores the latest response from the robot. Usefull for commands supporting multiple fields in the response.
            /// </summary>
            protected string report;
        }

        /// <summary>
        /// All movement commands.
        /// 
        /// Some of the commands perform multiple actions or return multiple data items (i.e. GetReport(), ManualDrive, GetTuningParameters(), GetMCUReport()).
        /// There is a dedicated class implemented for each such command.
        /// 
        /// Path related commands use the TrueTrack sensor and might require careful timing if issued in a sequence.
        /// </summary>
        public class Movement : MovementComponent
        {
            /// <summary>
            /// Rovio's navigation state.
            /// </summary>
            public enum NavigationState
            {
                /// Idle.
                IDLE = 0,
                /// Driving home.
                HOMING = 1,
                /// Docking.
                DOCKING = 2,
                /// Playing a path.
                PLAYING_PATH = 3,
                /// Recoring a path.
                RECORDING_PATH = 4
            }

            /// <summary>
            /// Charger status.
            /// </summary>
            public enum ChargerStatus
            {
                IDLE = 0,
                COMPLETED = 1,
                CHARGING = 2,
                ERROR = 3
            }

            /// <summary>
            /// Report commands.
            /// </summary>
            public ReportComponent Report;
            
            /// <summary>
            /// Manual Drive commands.
            /// </summary>
            public ManualDriveComponent ManualDrive;

            /// <summary>
            /// Tuning Parameters commands.
            /// </summary>
            public TuningParametersComponent TuningParameters;

            /// <summary>
            /// MCU Report commands.
            /// </summary>
            public MCUReportComponent MCUReport;

            /// The constructor.
            public Movement(Robot _robot)
                : base(_robot)
            {
                Report = new ReportComponent(_robot);
                ManualDrive = new ManualDriveComponent(_robot);
                TuningParameters = new TuningParametersComponent(_robot);
                MCUReport = new MCUReportComponent(_robot);
            }

            /// \brief Get the current status of the robot (in a string format). Refer to Report for accessing individual items of this report.
            /// 
            /// Remarks: 
            /// - %Rovio will resist going outside NorthStar coverage area while recording path
            /// - %Rovio will stop recording if coverage is lost
            /// - %Rovio will stop recording if user connection is lost
            public string GetReport() { return Request("1"); }

            /// Start recording a path.
            public void StartRecording() { Request("2"); }

            /// Stop recording and discard a path.
            public void AbortRecording() { Request("3"); }

            /// Stop recording and store a path.
            public void StopRecording(string path) { Request("4" + "&name=" + path); }

            /// Delete the specified path.
            public void DeletePath(string path) { Request("5" + "&name=" + path); }

            /// Get a list of stored paths.
            public string GetPathList() { return Request("6"); }

            /// \brief Replay the specified path from the closest point to the last one.
            /// 
            /// If the NorthStar signal is lost the playback is interrupted.
            public void PlayPathForward() { Request("7"); }

            /// \brief Replay the specified path from the closest point to the first one.
            /// 
            /// If the NorthStar signal is lost the playback is interrupted.
            public void PlayPathBackward() { Request("8"); }

            /// Stop playing the current path.
            public void StopPlaying() { Request("9"); }

            /// \brief Pause playing the current path.
            /// 
            /// The playback continues whith the next pause command and stops completely with the stop command.
            public void PausePlaying() { Request("10"); }

            /// Rename the specified path.
            public void RenamePath(string old_path, string new_path) { Request("11" + "&name=" + old_path + "&newname=" + new_path); }

            /// Drive to home location without docking.
            public void GoHome() { Request("12"); }

            /// Drive to home location with docking.
            public void GoHomeAndDock() { Request("13"); }

            /// Use the current location as home location.
            public void UpdateHomePosition() { Request("14"); }

            /// \brief Set homing, docking and driving parameters. Refer to TuningParameters for setting the individual elements.
            /// 
            /// The parameters include: LeftRight, Forward, Reverse, DriveTurn, HomingTurn, ManDrive, ManTurn and DockTimeout.
            public void SetTuningParameters() { Request("15"); }

            /// \brief Get homing, docking and driving parameters (in a string format). Refer to TuningParameters for accessing individual elements of the report.
            /// 
            /// The parameters include: LeftRight, Forward, Reverse, DriveTurn, HomingTurn, ManDrive, ManTurn and DockTimeout.
            public string GetTuningParameters() { return Request("16"); }

            /// Stop and reset to idle.
            public void ResetNavStateMachine() { Request("17"); }

            /// Switch on/off the front LED
            public void FrontLight(bool value)
            {
                if (value)
                    Request("19&LIGHT=1");
                else
                    Request("19&LIGHT=0");
            }

            /// Switch on/off the power of the IR sensor.
            public void IRSensor(bool value)
            {
                if (value)
                    Request("19&IR=1");
                else
                    Request("19&IR=0");
            }

            /// Get the report from the robot's microcontroller. Refer to MCUReport for accessing individual items of the report.
            public string GetMCUReport()
            {
                string response = robot.Request("rev.cgi?Cmd=nav&action=20");
                int start = response.IndexOf("Cmd = nav\n");
                if (start != -1)
                {
                    start = response.IndexOf("responses = ", start + 10);
                    if (start != -1)
                    {
                        start += 12;
                        string sub = response.Substring(start);

                        if (response.Substring(start).Length == 31)
                            return response.Substring(start, 30);
                    }
                }
                throw new Exception("Movement::GetMCUReport, Wrong response.");
            }

            /// Delete all stored paths.
            public void ClearAllPaths() { Request("21"); }

            /// Return the navigation status.
            public NavigationState GetStatus()
            {
                return (NavigationState)Convert.ToInt32(Request("22"));
            }

            /// Store value for the specified parameter.
            public void SaveParameter(int index, int value) { Request("23" + "&index=" + index.ToString() + "&value=" + value.ToString()); }

            /// Read the specified parameter value.
            public int ReadParameter(int index)
            {
                string response = Request("24" + "&index=" + index.ToString());
                string s_index = response.Substring(response.IndexOf("index = ") + 8);
                s_index = s_index.Substring(0, s_index.IndexOf("|"));
                string s_value = response.Substring(response.IndexOf("value = ") + 8);
                if (Convert.ToInt32(s_index) == index)
                    return Convert.ToInt32(s_value);
                else
                    throw new Exception("Movement::ReadParameter, Wrong response.");
            }

            /// NorthStar/TrueTrack version.
            public string GetLibNSVersion() { return Request("25"); }

            /// Email the current image / set an action (in path recording mode).
            public void EmailImage(string email_address) { Request("26&email=" + email_address); }

            /// Clear home location.
            public void ResetHomeLocation() { Request("27"); }

            /// Report class.
            /// \todo 'ui_status' is not documented,
            /// \todo Use the defined enums for popular commands (e.g. Resolution)
            public class ReportComponent : MovementComponent
            {
                /// <summary>
                /// Contructor.
                /// </summary>
                public ReportComponent(Robot _robot) : base(_robot) { }

                /// <summary>
                /// Update the report.
                /// </summary>
                public override void Update()
                {
                    report = robot.API.Movement.GetReport();
                }

                /// <summary>
                /// Average X location of %Rovio in relation to the strongest beacon.
                /// </summary>
                public double X { get { if (AutoUpdate) Update(); return Convert.ToDouble(GetParameter("x")); } }

                /// <summary>
                /// Average Y location of %Rovio in relation to the strongest beacon.
                /// </summary>
                public double Y { get { if (AutoUpdate) Update(); return Convert.ToDouble(GetParameter("y")); } }

                /// <summary>
                /// Average orientation of %Rovio in relation to the strongest beacon.
                /// </summary>
                public double Theta { get { if (AutoUpdate) Update(); return Convert.ToDouble(GetParameter("theta")); } }

                /// <summary>
                /// Room ID:
                /// 0: home base,
                /// 1-9: mutable room ID. 
                /// </summary>
                public int RoomID { get { if (AutoUpdate) Update(); return Convert.ToInt32(GetParameter("room")); } }

                /// <summary>
                /// Navigation signal strength.
                /// %> 47000: strong signal
                /// %< 5000: no signal
                /// </summary>
                public int NavigationSS { get { if (AutoUpdate) Update(); return Convert.ToInt32(GetParameter("ss")); } }

                /// <summary>
                /// Signal strength for docking beacon. (0 - 65535)
                /// </summary>
                public int BeaconSS { get { if (AutoUpdate) Update(); return Convert.ToInt32(GetParameter("beacon")); } }

                /// <summary>
                /// Horizontal position of a beacon (as seen by NorthStar).  (-32767 - 32768)
                /// </summary>
                public int BeaconX { get { if (AutoUpdate) Update(); return Convert.ToInt32(GetParameter("beacon_x")); } }

                /// <summary>
                /// The next strongest room beacon ID.
                /// -1: no room found,
                /// 1-9: mutable room ID. 
                /// </summary>
                public int NextRoomID { get { if (AutoUpdate) Update(); return Convert.ToInt32(GetParameter("next_room")); } }

                /// <summary>
                /// The next strongest room beacon signal. (0 - 65535)
                /// > 47000: strong signal
                /// < 5000: no signal
                /// </summary>
                public int NextRoomSS { get { if (AutoUpdate) Update(); return Convert.ToInt32(GetParameter("next_room_ss")); } }

                /// <summary>
                /// Rovio status.
                /// </summary>
                public Movement.NavigationState Status { get { if (AutoUpdate) Update(); return (Movement.NavigationState)Convert.ToInt32(GetParameter("state")); } }

                /// <summary>
                /// Mysterious and non-documented variable: UserInterface status?
                /// </summary>
                public int UIStatus { get { if (AutoUpdate) Update(); return Convert.ToInt32(GetParameter("ui_status")); } }

                /// <summary>
                /// Status of robot resistance to drive into areas badly covered by NorthStar. NOT IN USE!
                /// </summary>
                public int Resistance { get { if (AutoUpdate) Update(); return Convert.ToInt32(GetParameter("resistance")); } }

                /// <summary>
                /// Current status of the navigation state machine.
                /// </summary>
                public int StateMachine { get { if (AutoUpdate) Update(); return Convert.ToInt32(GetParameter("sm")); } }

                /// <summary>
                /// Current waypoint on the path. (1 - 10)
                /// </summary>
                public int WayPoint { get { if (AutoUpdate) Update(); return Convert.ToInt32(GetParameter("pp")); } }

                /// <summary>
                /// Flags:
                /// 1: home position,
                /// 2: obstacle detected,
                /// 3: IR detector activated
                /// </summary>
                public int Flags { get { if (AutoUpdate) Update(); return Convert.ToInt32(GetParameter("flags")); } }

                /// <summary>
                /// Camera brightness level.
                /// </summary>
                public int Brightness { get { if (AutoUpdate) Update(); return Convert.ToInt32(GetParameter("brightness")); } }

                /// <summary>
                /// Camera resolution.
                /// </summary>
                public Camera.ImageResolution Resolution { get { if (AutoUpdate) Update(); return (Camera.ImageResolution)Convert.ToInt32(GetParameter("resolution")); } }

                /// <summary>
                /// Camera compression ratio.
                /// </summary>
                public Camera.ImageCompression Compression { get { if (AutoUpdate) Update(); return (Camera.ImageCompression)Convert.ToInt32(GetParameter("video_compression")); } }

                /// <summary>
                /// Camera frame rate.
                /// </summary>
                public int Framerate { get { if (AutoUpdate) Update(); return Convert.ToInt32(GetParameter("frame_rate")); } }

                /// <summary>
                /// User privilige settings.
                /// 0: admin,
                /// 1: guest
                /// </summary>
                public int Privilege { get { if (AutoUpdate) Update(); return Convert.ToInt32(GetParameter("privilege")); } }

                /// <summary>
                /// Authentication:
                /// 0: requires user/password
                /// 1: does not require user/password
                /// </summary>
                public int UserCheck { get { if (AutoUpdate) Update(); return Convert.ToInt32(GetParameter("user_check")); } }

                /// <summary>
                /// Speaker volume.
                /// </summary>
                public int SpeakerVolume { get { if (AutoUpdate) Update(); return Convert.ToInt32(GetParameter("speaker_volume")); } }

                /// <summary>
                /// Micorpohne volume.
                /// </summary>
                public int MicVolume { get { if (AutoUpdate) Update(); return Convert.ToInt32(GetParameter("mic_volume")); } }

                /// <summary>
                /// Wifi signal strength. (0 - 254)
                /// </summary>
                public int WifiSS { get { if (AutoUpdate) Update(); return Convert.ToInt32(GetParameter("wifi_ss")); } }

                /// <summary>
                /// Time display in the image:
                /// 0: off
                /// 1: on
                /// </summary>
                public int ShowTime { get { if (AutoUpdate) Update(); return Convert.ToInt32(GetParameter("show_time")); } }

                /// <summary>
                /// DDNS update status:
                /// 0: no update,
                /// 1: updating,
                /// 2: update successful,
                /// 3: update failed
                /// </summary>
                public int DDNSState { get { if (AutoUpdate) Update(); return Convert.ToInt32(GetParameter("ddns_state")); } }

                /// <summary>
                /// Email update status. NOT IN USE!
                /// </summary>
                public int EmailState { get { if (AutoUpdate) Update(); return Convert.ToInt32(GetParameter("email_state")); } }

                /// <summary>
                /// Battery level:
                /// < 100: turn itself off,
                /// 100-106: go back home,
                /// 106-127: normal
                /// </summary>
                public int BatteryLevel { get { if (AutoUpdate) Update(); return Convert.ToInt32(GetParameter("battery")); } }

                /// <summary>
                /// Charging status:
                /// 0-79: not charging
                /// 80: charging
                /// </summary>
                public int Charging { get { if (AutoUpdate) Update(); return Convert.ToInt32(GetParameter("charging")); } }

                /// <summary>
                /// Head position:
                /// 204: low
                /// 135-140: mid
                /// 65: high
                /// </summary>
                public int HeadPosition { get { if (AutoUpdate) Update(); return Convert.ToInt32(GetParameter("head_position")); } }

                /// <summary>
                /// Camera flicker frequency.
                /// </summary>
                public Camera.CameraFlickerFrequency FlickerFrequency { get { if (AutoUpdate) Update(); return (Camera.CameraFlickerFrequency)Convert.ToInt32(GetParameter("ac_freq")); } }
            }

            /// <summary>
            /// Manage robot parameters used during navigation: homing, docking and automatic driving.
            /// </summary>
            public class TuningParametersComponent : MovementComponent
            {
                /// <summary>
                /// The constructor.
                /// </summary>
                /// <param name="_robot"></param>
                public TuningParametersComponent(Robot _robot) : base(_robot) { }

                /// <summary>
                /// Update the report.
                /// </summary>
                public override void Update()
                {
                    report = robot.API.Movement.GetTuningParameters();
                }

                /// <summary>
                /// LeftRight value.
                /// </summary>
                public int LeftRight { get { if (AutoUpdate) Update(); return Convert.ToInt32(GetParameter("LeftRight")); } }

                /// <summary>
                /// Forward value.
                /// </summary>
                public int Forward { get { if (AutoUpdate) Update(); return Convert.ToInt32(GetParameter("Forward")); } }

                /// <summary>
                /// Reverse value.
                /// </summary>
                public int Reverse { get { if (AutoUpdate) Update(); return Convert.ToInt32(GetParameter("Reverse")); } }

                /// <summary>
                /// DriveTurn value.
                /// </summary>
                public int DriveTurn { get { if (AutoUpdate) Update(); return Convert.ToInt32(GetParameter("DriveTurn")); } }

                /// <summary>
                /// HomingTurn value.
                /// </summary>
                public int HomingTurn { get { if (AutoUpdate) Update(); return Convert.ToInt32(GetParameter("HomingTurn")); } }

                /// <summary>
                /// Manual drive value.
                /// </summary>
                public int ManDrive { get { if (AutoUpdate) Update(); return Convert.ToInt32(GetParameter("ManDrive")); } }

                /// <summary>
                /// Manual turn value.
                /// </summary>
                public int ManTurn { get { if (AutoUpdate) Update(); return Convert.ToInt32(GetParameter("ManTurn")); } }

                /// <summary>
                /// Dock time out.
                /// </summary>
                public int DockTimeout { get { if (AutoUpdate) Update(); return Convert.ToInt32(GetParameter("DockTimeout")); } }
            }

            /// <summary>
            /// Manual drive commands. Majority of the drive commands feature the speed parameter: 1 (fastest) - 10 (slowest). 
            /// Note that depending on the type of surface, the robot might have problems executing commands with very low speeds (i.e. it will stall).
            /// </summary>
            public class ManualDriveComponent : MovementComponent
            {
                /// <summary>
                /// The constructor.
                /// </summary>
                public ManualDriveComponent(Robot _robot) : base(_robot) { }

                /// <summary>
                /// Helper method that sends the right manual drive command to the robot.
                /// </summary>
                /// <param name="d_value">Movement type</param>
                /// <param name="s_value">Speed</param>
                void Drive(int d_value, int s_value) { Request("18" + "&drive=" + d_value.ToString() + "&speed=" + s_value.ToString()); }

                /// <summary>
                /// Stop the robot.
                /// </summary>
                public void Stop() { Drive(0, 0); }

                /// <summary>
                /// Move forward.
                /// </summary>
                /// <param name="speed">1 (fastest) - 10 (slowest)</param>
                public void Forward(int speed) { Drive(1, speed); }

                /// <summary>
                /// Move backward.
                /// </summary>
                /// <param name="speed"></param>
                public void Backward(int speed) { Drive(2, speed); }

                /// <summary>
                /// Move straight left.
                /// </summary>
                /// <param name="speed"></param>
                public void StraightLeft(int speed) { Drive(3, speed); }

                /// <summary>
                /// Move straight right.
                /// </summary>
                /// <param name="speed"></param>
                public void StraightRight(int speed) { Drive(4, speed); }

                /// <summary>
                /// Rotate left.
                /// </summary>
                /// <param name="speed"></param>
                public void RotateLeft(int speed) { Drive(5, speed); }

                /// <summary>
                /// Rotate right.
                /// </summary>
                /// <param name="speed"></param>
                public void RotateRight(int speed) { Drive(6, speed); }

                /// <summary>
                /// Diagonal forward left.
                /// </summary>
                /// <param name="speed"></param>
                public void DiagForwardLeft(int speed) { Drive(7, speed); }

                /// <summary>
                /// Diagonal forward right.
                /// </summary>
                /// <param name="speed"></param>
                public void DiagForwardRight(int speed) { Drive(8, speed); }

                /// <summary>
                /// Diagonal backward left.
                /// </summary>
                /// <param name="speed"></param>
                public void DiagBackwardLeft(int speed) { Drive(9, speed); }

                /// <summary>
                /// Diagonal backward right.
                /// </summary>
                /// <param name="speed"></param>
                public void DiagBackwardRight(int speed) { Drive(10, speed); }

                /// <summary>
                /// %Camera head up position.
                /// </summary>
                public void HeadUp() { Drive(11, 1); }

                /// <summary>
                /// %Camera head down position.
                /// </summary>
                public void HeadDown() { Drive(12, 1); }

                /// <summary>
                /// %Camera head middle position.
                /// </summary>
                public void HeadMiddle() { Drive(13, 1); }

                /// <summary>
                /// Rotate left by 20 degree angle increments.
                /// </summary>
                /// <param name="speed"></param>
                public void RotateLeft20(int speed) { Drive(17, speed); }

                /// <summary>
                /// Rotate right by 20 degree angle increments.
                /// </summary>
                /// <param name="speed"></param>
                public void RotateRight20(int speed) { Drive(18, speed); }
            }

            /// <summary>
            /// Provides a run-time report from Rovio's microcontroller.
            /// Run the Update() method before accessing individual parameters of the report.
            /// This solution reduces the data traffic when accessing multiple parameters at the same time.
            /// </summary>
            public class MCUReportComponent : MovementComponent
            {
                /// <summary>
                /// The constructor.
                /// </summary>
                public MCUReportComponent(Robot _robot) : base(_robot) { }

                /// <summary>
                /// Update the report.
                /// </summary>
                public override void Update()
                {
                    report = robot.API.Movement.GetMCUReport();
                }

                /// <summary>
                /// Left wheel odometry: positive rotation direction.
                /// </summary>
                public bool LeftWheelRot { get { if (AutoUpdate) Update(); return (Convert.ToInt32(report.Substring(4, 2), 16) != 4); } }

                /// <summary>
                /// Left wheel odometry: number of ticks.
                /// </summary>
                public int LeftWheelTicks { get { if (AutoUpdate) Update(); return Convert.ToInt32(report.Substring(6, 4), 16); } }

                /// <summary>
                /// Right wheel odometry: positive rotation direction.
                /// </summary>
                public bool RightWheelRot { get { if (AutoUpdate) Update(); return (Convert.ToInt32(report.Substring(10, 2), 16) != 4); } }

                /// <summary>
                /// Right wheel odometry: number of ticks.
                /// </summary>
                public int RightWheelTicks { get { if (AutoUpdate) Update(); return Convert.ToInt32(report.Substring(12, 4), 16); } }

                /// <summary>
                /// Rear wheel odometry: positive rotation direction.
                /// </summary>
                public bool RearWheelRot { get { if (AutoUpdate) Update(); return (Convert.ToInt32(report.Substring(16, 2), 16) != 4); } }

                /// <summary>
                /// Rear wheel odometry: number of ticks.
                /// </summary>
                public int RearWheelTicks { get { if (AutoUpdate) Update(); return Convert.ToInt32(report.Substring(18, 4), 16); } }

                /// <summary>
                /// Position of the head.
                /// </summary>
                public int HeadPosition { get { if (AutoUpdate) Update(); return Convert.ToInt32(report.Substring(24, 2), 16); } }

                /// <summary>
                /// Battery level.
                /// </summary>
                public int BatteryLevel { get { if (AutoUpdate) Update(); return Convert.ToInt32(report.Substring(26, 2), 16); } }

                /// <summary>
                /// Status.
                /// </summary>
                public int Status { get { if (AutoUpdate) Update(); return Convert.ToInt32(report.Substring(28, 2), 16); } }

                /// <summary>
                /// The state of the front LED light.
                /// </summary>
                public bool LightOn { get { if (AutoUpdate) Update(); if ((Convert.ToInt32(report.Substring(28, 2), 16) & 0x01) == 0) return false; else return true; } }

                /// <summary>
                /// The power state of the IR proximity sensor.
                /// </summary>
                public bool IRPowerOn { get { if (AutoUpdate) Update(); if ((Convert.ToInt32(report.Substring(28, 2), 16) & 0x02) == 0) return false; else return true; } }

                /// <summary>
                /// The state of the IR proximity sensor.
                /// </summary>
                public bool IRDetectorOn { get { if (AutoUpdate) Update(); if ((Convert.ToInt32(report.Substring(28, 2), 16) & 0x04) == 0) return false; else return true; } }

                /// <summary>
                /// Charger status.
                /// </summary>
                public Movement.ChargerStatus ChargerStatus { get { if (AutoUpdate) Update(); return (Movement.ChargerStatus)((Convert.ToInt32(report.Substring(28, 2), 16) & 0x38) >> 3); } }
            }

            /// <summary>
            /// %Report errors in movement responses received from %Rovio.
            /// </summary>
            public class MovementResponseException : Exception
            {
                int value;

                /// <summary>
                /// The constructor.
                /// </summary>
                public MovementResponseException(int _value) { value = _value; }

                /// <summary>
                /// The error specific message.
                /// </summary>
                public override string Message
                {
                    get
                    {
                        string message = "MovementResponseException: ";

                        switch (value)
                        {
                            /// CGI command successful.
                            case 0: return message + "SUCCESS";
                            /// CGI command general failure.
                            case 1: return message + "FAILURE";
                            /// Robot is executing autonomous function.
                            case 2: return message + "ROBOT_BUSY";
                            /// CGI command not implemented.
                            case 3: return message + "FEATURE_NOT_IMPLEMENTED";
                            /// CGI nav command: unknown action requested.
                            case 4: return message + "UNKNOWN_CGI_ACTION";
                            /// No NS signal available.
                            case 5: return message + "NO_NS_SIGNAL";
                            /// Path memory is full.
                            case 6: return message + "NO_EMPTY_PATH_AVAILABLE";
                            /// Failed to read FLASH.
                            case 7: return message + "FAILED_TO_READ_PATH";
                            /// FLASH error.
                            case 8: return message + "PATH_BASEADDRESS_NOT_INITIALIZED";
                            /// No path with such name.
                            case 9: return message + "PATH_NOT_FOUND";
                            /// Path name parameter is missing.
                            case 10: return message + "PATH_NAME_NOT_FOUND";
                            /// Save path command received while not in recording mode.
                            case 11: return message + "NOT_RECORING_PATH";
                            /// Flash subsystem failure.
                            case 12: return message + "FLASH_NOTINITIALIZED";
                            /// Flash operation failed.
                            case 13: return message + "FAILED_TO_DELETE_PATH";
                            /// Flash operation failed.
                            case 14: return message + "FAILED_TO_READ_FROM_FLASH";
                            /// Flash operation failed.
                            case 15: return message + "FAILED_TO_WRITE_TO_FLASH";
                            /// Flash failed.
                            case 16: return message + "FLASH_NOT_READY";
                            case 17: return message + "NO_MEMORY_AVAILABLE";
                            case 18: return message + "NO_MCU_PORT_AVAILABLE";
                            case 19: return message + "NO_NS_PORT_AVAILABLE";
                            case 20: return message + "NS_PACKET_CHECKSUM_ERROR";
                            case 21: return message + "NS_UART_ERAD_ERROR";
                            /// One or more parameters are out of expected range.
                            case 22: return message + "PARAMETER_OUTOFRANGE";
                            /// One or more parameters are missing.
                            case 23: return message + "NO_PARAMEMTER";
                            default: return message + "Unrecongnized error.";
                        }
                    }
                }
            }
        }

        /// <summary>
        /// %Camera control.
        /// </summary>
        public class Camera : Component
        {
            /// <summary>
            /// Specifies the camera image resolution.
            /// </summary>
            public enum ImageResolution
            {
                /// <summary>
                /// 176x144 pixels, QCIF resolution
                /// </summary>
                QCIF = 0,
                /// <summary>
                /// 320x240 pixels, CGA resolution
                /// </summary>
                CGA = 1,
                /// <summary>
                /// 352x288 pixels, CIF resolution (default)
                /// </summary>
                CIF = 2,
                /// <summary>
                /// 640x480 pixels, VGA resolution
                /// </summary>
                VGA = 3
            }

            /// <summary>
            /// Specifies the camera compression ratio.
            /// </summary>
            public enum ImageCompression
            {
                /// <summary>
                /// Low quality
                /// </summary>
                LOW,
                /// <summary>
                /// Medium quality (default)
                /// </summary>
                MED,
                /// <summary>
                /// High quality
                /// </summary>
                HI
            }

            /// <summary>
            /// Specifies the camera anti-flickering frequency.
            /// </summary>
            public enum CameraFlickerFrequency
            {
                /// <summary>
                /// Auto-detect
                /// </summary>
                AUTO = 0,
                /// <summary>
                /// 50 Hz
                /// </summary>
                F50HZ = 50,
                /// <summary>
                /// 60 Hz
                /// </summary>
                F60HZ = 60
            }

            /// <summary>
            /// Camera head position.
            /// </summary>
            public enum HeadPosition
            {
                UP,
                MIDDLE,
                DOWN
            }

            /// <summary>
            /// The camera class.
            /// </summary>
            /// <param name="_robot"></param>
            public Camera(Robot _robot) : base(_robot) { }

            /// <summary>
            /// Set camera resolution.
            /// </summary>
            public ImageResolution Resolution
            {
                set
                {
                    switch (value)
                    {
                        case ImageResolution.QCIF:
                            robot.Request("ChangeResolution.cgi?ResType=0");
                            break;
                        case ImageResolution.CGA:
                            robot.Request("ChangeResolution.cgi?ResType=1");
                            break;
                        case ImageResolution.CIF:
                            robot.Request("ChangeResolution.cgi?ResType=2");
                            break;
                        case ImageResolution.VGA:
                            robot.Request("ChangeResolution.cgi?ResType=3");
                            break;
                        default: break;
                    }
                }
            }

            /// <summary>
            /// Set camera compression.
            /// </summary>
            public ImageCompression Compression
            {
                set
                {
                    switch (value)
                    {
                        case ImageCompression.LOW:
                            robot.Request("ChangeCompressRatio.cgi?Ratio=0");
                            break;
                        case ImageCompression.MED:
                            robot.Request("ChangeCompressRatio.cgi?Ratio=1");
                            break;
                        case ImageCompression.HI:
                            robot.Request("ChangeCompressRatio.cgi?Ratio=2");
                            break;
                        default: break;
                    }
                }
            }

            /// <summary>
            /// Set image framerate.
            /// </summary>
            public int Framerate { set { robot.Request("ChangeFramerate.cgi?Framerate=" + value.ToString()); } }

            /// <summary>
            /// Set image brightness.
            /// </summary>
            public int Brightness { set { robot.Request("ChangeBrightness.cgi?Brightness=" + value.ToString()); } }

            /// <summary>
            /// Set speaker volume.
            /// </summary>
            public int SpeakerVolume { set { robot.Request("ChangeSpeakerVolume.cgi?SpeakerVolume=" + value.ToString()); } }

            /// <summary>
            /// Set microphone volume.
            /// </summary>
            public int MicVolume { set { robot.Request("ChangeMicVolume.cgi?MicVolume=" + value.ToString()); } }

            /// <summary>
            /// Camera frequency.
            /// </summary>
            public CameraFlickerFrequency FlickerFrequency
            {
                set
                {
                    switch (value)
                    {
                        case CameraFlickerFrequency.AUTO:
                            robot.Request("SetCamera.cgi?Frequency=0");
                            break;
                        case CameraFlickerFrequency.F50HZ:
                            robot.Request("SetCamera.cgi?Frequency=50");
                            break;
                        case CameraFlickerFrequency.F60HZ:
                            robot.Request("SetCamera.cgi?Frequency=60");
                            break;
                        default: break;
                    }
                }
            }

            /// <summary>
            /// Collect a camera image (in a Bitmap format)
            /// </summary>
            /// <returns>Bitmap image</returns>
            public Bitmap GetImage()
            {
                return new Bitmap(robot.StreamRequest("Jpeg/CamImg0000.jpg"));
            }
        }

        /// <summary>
        /// Manage user accounts.
        /// </summary>
        public class User : Component
        {
            /// <summary>
            /// The constructor.
            /// </summary>
            public User(Robot _robot) : base(_robot) { }

            /// <summary>
            /// Get the username who sent this HTTP request.
            /// 
            /// \todo Implement the proper return value: list of strings.
            /// </summary>
            public string GetMyself(bool show_privilege)
            {
                if (show_privilege)
                    return robot.Request("GetMyself.cgi?ShowPrivilege=true");
                else
                    return robot.Request("GetMyself.cgi?ShowPrivilege=false");
            }

            /// <summary>
            /// Add a user or change the password for the existing user.
            /// </summary>
            public void SetUser(string name, string password)
            {
                robot.Request("SetUser.cgi?User=" + name + "&Pass=" + password);
            }

            /// <summary>
            /// Delete a user account.
            /// </summary>
            public void DeleteUser(string name)
            {
                robot.Request("DelUser.cgi?User=" + name);
            }

            /// <summary>
            /// Get the users list.
            /// 
            /// \todo Implement the proper return value: list of strings.
            /// </summary>
            public string GetUser(bool show_privilege)
            {
                if (show_privilege)
                    return robot.Request("GetUser.cgi?ShowPrivilege=true");
                else
                    return robot.Request("GetUser.cgi?ShowPrivilege=false");
            }

            /// <summary>
            /// Enable or disable user authorization check.
            /// </summary>
            public void SetUserCheck(bool value)
            {
                if (value)
                    robot.Request("SetUserCheck.cgi?Check=true");
                else
                    robot.Request("SetUserCheck.cgi?Check=false");
            }
        }

        /// <summary>
        /// Manage time settings and time zones.
        /// </summary>
        public class Time : Component
        {
            /// <summary>
            /// The constructor.
            /// </summary>
            public Time(Robot _robot) : base(_robot) { }

            /// <summary>
            /// Set server time zone and time.
            /// Sec1970 - seconds since "00:00:00 1/1/1970".
            /// TimeZone – Time zone in minutes. (e.g. Beijing is GMT+08:00, TimeZone = -480)
            /// 
            /// \todo Implement with DateTime input parameter.
            /// </summary>
            public void SetTime(int seconds, int time_zone)
            {
                robot.Request("SetTime.cgi?&Sec1970=" + seconds + "&TimeZone=" + time_zone);                
            }

            /// <summary>
            /// Get current time zone and time.
            /// 
            /// \todo Implement with DateTime input parameter.
            /// </summary>
            public string GetTime()
            {
                return robot.Request("GetTime.cgi");
            }

            /// <summary>
            /// Set a logo string on the image.
            /// showstring:
            /// time - time
            /// date - date
            /// ver - version
            /// pos:
            /// 0 – top left
            /// 1 – top right
            /// 2 – bottom left
            /// 3 – bottom right
            /// 
            /// \todo Implement the enum input parameters.
            /// </summary>
            public void SetLogo(string type, string position)
            {
                robot.Request("SetLogo.cgi?&showstring=" + type + "&pos=" + position);
            }

            /// <summary>
            /// Get a logo string on the image.
            /// 
            /// /todo Fix return value. The current Rovio implementation repeats the return value twice.
            /// </summary>
            public string GetLogo()
            {
                return robot.Request("GetLogo.cgi");
            }
        }

        /// <summary>
        /// %Network management.
        /// </summary>
        public class Network : Component
        {
            /// <summary>
            /// The constructor.
            /// </summary>
            public Network(Robot _robot) : base(_robot) { }

            /// <summary>
            /// Set IP settings.
            /// 
            /// \todo Fix the input value according to the following format:
            /// /SetIP.cgi?[Interface=<eth1|wlan0>][&Enable=<true|false>][&IPWay=<manually|dhcp>][&CameraName=sName]
            /// [&IP=sIP][&Netmask=sNetmask][&Gateway=sGateway][&DNS0=sDNS0][&DNS1=sDNS1][&DNS2=sDNS2]
            /// </summary>
            public void SetIP(string value)
            {
                robot.Request("SetIP.cgi?" + value);
            }

            /// <summary>
            /// Get IP settings.
            /// Interface:
            /// eth1
            /// wlan0
            /// 
            /// \todo Implement the enum input parameter.
            /// </summary>
            public string GetIP(string type)
            {
                return robot.Request("GetIP.cgi?Interface=" + type);
            }

            /// <summary>
            /// Set Wifi settings.
            /// 
            /// \todo Fix the input parameter according to the following format:
            /// /SetWlan.cgi?[Mode=<Managed|Ad-Hoc>][&Channel=sChannel] [&ESSID=sEssid][&WepSet=<Disable|K64|K128|ASC>]
            /// [&WepAsc=sWepAsc][&Wep64type=<Wep64HEX|Wep64ASC>][&Wep64=sWep64][&Wep128type=<Wep128HEX|Wep128ASC>][&Wep128=sWep128]
            /// </summary>
            public void SetWlan(string value)
            {
                robot.Request("SetWlan.cgi?" + value);
            }

            /// <summary>
            /// Get Wifi settings.
            /// </summary>
            public string GetWlan()
            {
                return robot.Request("GetWlan.cgi");
            }

            /// <summary>
            /// Set dyndns.org service
            /// Service – DDNS service provider
            /// User – username
            /// Pass – password
            /// IP – IP address (null for auto detect)
            /// Proxy – name of the proxy
            /// ProxyPort – port of the proxy
            /// ProxyUser – username of the proxy
            /// ProxyPass – password of the proxy
            /// 
            /// Set the account for dyndns.org. To connect the dyndns server, 
            /// if HTTP proxy is required, set the Proxy relative value, 
            /// otherwise leave them blank. If sIPAddress is not set, the device will detect 
            /// the IP address automatically.
            /// 
            /// \todo Fix the input parameter according to the following format:
            /// /SetDDNS.cgi?[Enable=<true|false>][Service=<dyndns|no-ip|dnsomatic>]
            /// [&User=sUsername][&Pass=sPassword][&DomainName=sDomainName][&IP=sIPAddress]
            /// [&Proxy=sProxyServer][&ProxyPort=iProxyServerPort][&ProxyUser=sProxyUsername]
            /// [&ProxyPass=sProxyPassword][&RedirectUrl=sUrl]
            /// </summary>
            public void SetDDNS(string value)
            {
                robot.Request("SetDDNS.cgi?" + value);
            }

            /// <summary>
            /// Get DDNS settings.
            /// 
            /// Each line represents an item, and every item is in the format as Name = Value. (
            /// Refer to SetDDNS.cgi) Return information represent by “Info” should be one of the following values: 
            /// Updated Updating Failed Updating IP Checked Not Update
            /// </summary>
            public string GetDDNS()
            {
                return robot.Request("GetDDNS.cgi");
            }

            /// <summary>
            /// Set MAC address.
            /// </summary>
            public void SetMAC(string address)
            {
                robot.Request("SetMac.cgi?MAC=" + address);
            }

            /// <summary>
            /// Get MAC address.
            /// </summary>
            public string GetMAC()
            {
                return robot.Request("GetMac.cgi");
            }
        }

        /// <summary>
        /// Manage server settings.
        /// </summary>
        public class Server : Component
        {
            /// <summary>
            /// The constructor.
            /// </summary>
            public Server(Robot _robot) : base(_robot) { }

            /// <summary>
            /// Set the parameters for HTTP server (Currently only TCP port).
            /// 
            /// Modyfing one port.
            /// </summary>
            public void SetHTTP(string value)
            {
                robot.Request("SetHttp.cgi?Port=" + value);
            }

            /// <summary>
            /// Set the parameters for HTTP server (Currently only TCP port).
            /// 
            /// Modyfing two ports.
            /// </summary>
            public void SetHTTP(string port0, string port1)
            {
                robot.Request("SetHttp.cgi?Port0=" + port0 + "&Port1=" + port1);
            }

            /// <summary>
            /// Get HTTP server's settings.
            /// </summary>
            public string GetHTTP()
            {
                return robot.Request("GetHttp.cgi");
            }
        }

        /// <summary>
        /// Manage email settings.
        /// </summary>
        public class Mail : Component
        {
            /// <summary>
            /// The constructor.
            /// </summary>
            public Mail(Robot _robot) : base(_robot) { }

            /// <summary>
            /// Configure email for sending IPCam images.
            /// 
            /// Enable – Ignored
            /// MailServer - mail server address
            /// Sender - sender’s email address
            /// Receiver - receiver’s email address, multi-receivers separated by ‘;’
            /// Subject - subject of email
            /// User - user name for logging into the MailServer
            /// PassWord - password for logging into the MailServer
            /// CheckFlag - whether the MailServer needs to check password
            /// Interval - Ignored
            /// </summary>
            public void SetMail(string value)
            {
                robot.Request("SetMail.cgi?Enable=true&" + value);
            }

            /// <summary>
            /// Get email settings.
            /// 
            /// MailServer, Port, Sender, Receiver, Subject, Body, User, PassWord, CheckFlag and Enable
            /// </summary>
            public string GetMail()
            {
                return robot.Request("GetMail.cgi");
            }

            /// <summary>
            /// Send an email with IPCam images.
            /// </summary>
            public void SendMail()
            {
                robot.Request("SendMail.cgi");
            }
        }

        /// <summary>
        /// Manage camera settings, get audio and video streams, etc.
        /// 
        /// \todo Partially implemented: GetStatus command only.
        /// </summary>
        public class Other : Component
        {
            /// <summary>
            /// Status.
            /// </summary>
            public StatusComponent Status;

            /// <summary>
            /// The constructor.
            /// </summary>
            public Other(Robot _robot)
                : base(_robot)
            {
                Status = new StatusComponent(_robot);
            }

            /// <summary>
            /// Set camera's name.
            /// </summary>
            public void SetCameraName(string name)
            {
                robot.Request("SetName.cgi?CameraName=" + name);
            }

            /// <summary>
            /// Get camera's name.
            /// </summary>
            public string GetCameraName()
            {
                return robot.Request("GetName.cgi");
            }

            /// <summary>
            /// Get Rovio’s system logs information.
            /// </summary>
            public string GetLog()
            {
                return robot.Request("GetLog.cgi");
            }

            /// <summary>
            /// Get Rovio’s base firmware version.
            /// Rovio also has a UI version and a NS2 version this function only get the base OS version.
            /// </summary>
            public string GetVer()
            {
                return robot.Request("GetVer.cgi");
            }

            /// <summary>
            /// Change all settings to factory-default.
            /// </summary>
            public void SetFactoryDefault()
            {
                robot.Request("SetFactoryDefault.cgi");
            }

            /// <summary>
            /// Reboot Rovio.
            /// </summary>
            public void Reboot()
            {
                robot.Request("Reboot.cgi");
            }

            /// <summary>
            /// Get images/status with "multipart/x-mixed-replace" mime-type.
            /// 
            /// GetData.cgi is designed for "server-push". "Server-push" means that user need not always detect camera's state, 
            /// and the camera server transfers the camera data on its own.
            /// </summary>
            public string GetData(bool status)
            {
                if (status)
                    return robot.Request("GetData.cgi?Status=true");
                else
                    return robot.Request("GetData.cgi?Status=false");
            }

            /// <summary>
            /// Send audio to server and playback at server side.
            /// 
            /// The data flow is from client to IPCam, which is different from GetData.cgi.
            /// The audio data must be send with HTTP POST method. Audio format: 16bit PCM, 8000Hz
            /// </summary>
            public string GetAudio()
            {
                return robot.Request("GetAudio.cgi");
            }

            /// <summary>
            /// Set the media format.
            /// 
            /// Audio = 0 – 4
            /// Video = 0 – 1
            /// </summary>
            public void SetMediaFormat(string audio, string video)
            {
                robot.Request("GetMediaFormat.cgi?Audio=" + audio + "&Video=" + video);
            }

            /// <summary>
            /// Get the media format.
            /// 
            /// Audio
            /// 0 – AMR
            /// 1 – PCM
            /// 2 – IMAADPCM
            /// 3 – ULAW
            /// 4 – ALAW
            /// 
            /// Video
            /// 1 – H263
            /// 2 – MPEG4
            /// </summary>
            public string GetMediaFormat()
            {
                return robot.Request("GetMediaFormat.cgi");
            }

            /// <summary>
            /// Upload new firmware image.
            /// </summary>
            public void Upload()
            {
                robot.Request("Upload.cgi");
            }

            /// <summary>
            /// Use this command to combine several commands to a single http request, that is, 
            /// user can call two or more commands through Cmd.cgi.
            /// 
            /// Some commands may use the same parameter's name, so the parameter's position should be in correct order. 
            /// If you place parameters of sCommandName1 after sCommandName2, the behaviors of IP Camera is unexpected. 
            /// If there are sub-commands that get information from Rovio, (such as GetUser.cgi, PPPoE.cgi?Action=GetInfo), 
            /// "RedirectUrl" should not be specified, otherwise the browser will be
            /// redirected to the address specified by "RedirectUrl", and certainly not what you want.
            /// </summary>
            public void Cmd(string value)
            {
                robot.Request("Cmd.cgi?Cmd=" + value);
            }

            /// <summary>
            /// Return the run-time status of Rovio including camera settings, true track sensor settings, etc. Part of the Other command.
            /// Call Update() before accessing individual parameters of the report.
            /// This solution reduces the data traffic when accessing multiple parameters at the same time.
            /// </summary>
            public class StatusComponent : Component
            {
                string report;

                /// <summary>
                /// The constructor.
                /// </summary>
                public StatusComponent(Robot _robot) : base(_robot) { }

                /// <summary>
                /// Update the report.
                /// </summary>
                public override void Update()
                {
                    report = robot.Request("GetStatus.cgi");
                    report = report.Substring(report.IndexOf("Status = ") + 9);
                }

                /// <summary>
                /// Camera state: 0 = off, 1 = on.
                /// </summary>
                public int CameraState { get { if (AutoUpdate) Update(); return Convert.ToInt32(report.Substring(0, 2)); } }

                /// <summary>
                /// Modem state: 0 = off, 1 = on line(common mode), 2 = connecting(common mode).
                /// </summary>
                public int ModemState { get { if (AutoUpdate) Update(); return Convert.ToInt32(report.Substring(2, 2)); } }

                /// <summary>
                /// PPPoE State - same as Modem state.
                /// </summary>
                public int PPPoEState { get { if (AutoUpdate) Update(); return Convert.ToInt32(report.Substring(4, 2)); } }

                /// <summary>
                /// Reserved.
                /// </summary>
                public int XDirection { get { if (AutoUpdate) Update(); return Convert.ToInt32(report.Substring(6, 3)); } }

                /// <summary>
                /// Reserved.
                /// </summary>
                public int YDirection { get { if (AutoUpdate) Update(); return Convert.ToInt32(report.Substring(9, 3)); } }

                /// <summary>
                /// Reserved.
                /// </summary>
                public int Focus { get { if (AutoUpdate) Update(); return Convert.ToInt32(report.Substring(12, 3)); } }

                /// <summary>
                /// Camera brightness: 0-255.
                /// </summary>
                public int Brightness { get { if (AutoUpdate) Update(); return Convert.ToInt32(report.Substring(15, 3)); } }

                /// <summary>
                /// Camera contrast: 0-255.
                /// </summary>
                public int Contrast { get { if (AutoUpdate) Update(); return Convert.ToInt32(report.Substring(18, 3)); } }

                /// <summary>
                /// Camera resolution.
                /// </summary>
                public Camera.ImageResolution Resolution { get { if (AutoUpdate) Update(); return (Camera.ImageResolution)Convert.ToInt32(report.Substring(21, 1)); } }

                /// <summary>
                /// Reserved.
                /// </summary>
                public Camera.ImageCompression Compression { get { if (AutoUpdate) Update(); return (Camera.ImageCompression)Convert.ToInt32(report.Substring(22, 1)); } }

                /// <summary>
                /// Priviliege: 0 = super user(administrator), 1 = common user.
                /// </summary>
                public int Privilege { get { if (AutoUpdate) Update(); return Convert.ToInt32(report.Substring(23, 1)); } }

                /// <summary>
                /// Picture index: 999999 = invalid picture.
                /// </summary>
                public int PictureIndex { get { if (AutoUpdate) Update(); return Convert.ToInt32(report.Substring(24, 6)); } }

                /// <summary>
                /// Email state: 0 = do not send motion-detected pictures, 1 = send motion-detected pictures, success,
                /// 2 = send motion-detected pictures, fail (wrong IP, user or password?).
                /// </summary>
                public int EmailState { get { if (AutoUpdate) Update(); return Convert.ToInt32(report.Substring(30, 1)); } }

                /// <summary>
                /// User check: 0 = do not check user, any user can connect and act as a super user,
                /// 1 = username and password required, only username is "administrator" has the super privilege.
                /// </summary>
                public int UserCheck { get { if (AutoUpdate) Update(); return Convert.ToInt32(report.Substring(31, 1)); } }

                /// <summary>
                /// Image file length: length in bytes.
                /// </summary>
                public int ImageFileLength { get { if (AutoUpdate) Update(); return Convert.ToInt32(report.Substring(32, 8)); } }

                /// <summary>
                /// 4 bytes left(0-9999),  4 bytes - top(0-9999), 4 bytes - right(0-9999), 4 bytes - bottom(0-9999)
                /// 
                /// \todo fix the return value
                /// </summary>
                public int MonitorRect { get { if (AutoUpdate) Update(); return Convert.ToInt32(report.Substring(40, 16)); } }

                /// <summary>
                /// FTP state: 0 = disable ftp upload, 1 = enable ftp upload, and upload success,
                /// 2 = enable ftp upload, but fail(wrong IP, user or password?).
                /// </summary>
                public int FTPState { get { if (AutoUpdate) Update(); return Convert.ToInt32(report.Substring(56, 1)); } }

                /// <summary>
                /// Camera image saturation: 0-255.
                /// </summary>
                public int Saturation { get { if (AutoUpdate) Update(); return Convert.ToInt32(report.Substring(57, 3)); } }

                /// <summary>
                /// Motion detection index: 999999 = init value.
                /// </summary>
                public int MotionDetectedIndex { get { if (AutoUpdate) Update(); return Convert.ToInt32(report.Substring(60, 6)); } }

                /// <summary>
                /// Hue: 0-255.
                /// </summary>
                public int Hue { get { if (AutoUpdate) Update(); return Convert.ToInt32(report.Substring(66, 3)); } }

                /// <summary>
                /// Sharpness: 0-255.
                /// </summary>
                public int Sharpness { get { if (AutoUpdate) Update(); return Convert.ToInt32(report.Substring(69, 3)); } }

                /// <summary>
                /// 0 = no motion detection, non-zero = motion detection
                /// </summary>
                public int MotionDetectWay { get { if (AutoUpdate) Update(); return Convert.ToInt32(report.Substring(72, 1)); } }

                /// <summary>
                /// Camera's frequency: 0 = outdoor, 1 = 50Hz, 2 = 60Hz
                /// </summary>
                public Camera.CameraFlickerFrequency FlickerFrequency { get { if (AutoUpdate) Update(); return (Camera.CameraFlickerFrequency)Convert.ToInt32(report.Substring(73, 1)); } }

                /// <summary>
                /// 0 = fixed mode, 1 = round robin mode.
                /// </summary>
                public int ChannelMode { get { if (AutoUpdate) Update(); return Convert.ToInt32(report.Substring(74, 1)); } }

                /// <summary>
                /// In fixed mode, the value may be from 0 to 3 In round robin mode, the value may be from 1 to 15.
                /// </summary>
                public int ChannelValue { get { if (AutoUpdate) Update(); return Convert.ToInt32(report.Substring(75, 2)); } }

                /// <summary>
                /// Audio volume.
                /// </summary>
                public int AudioVolume { get { if (AutoUpdate) Update(); return Convert.ToInt32(report.Substring(77, 3)); } }

                /// <summary>
                /// DNS state: 0 = no update, 1 = updating, 2 = update successful, 3 = update failed.
                /// </summary>
                public int DynamicDNSState { get { if (AutoUpdate) Update(); return Convert.ToInt32(report.Substring(80, 1)); } }

                /// <summary>
                /// 0 = audio disabled, 1 = audio enabled.
                /// </summary>
                public int AudioState { get { if (AutoUpdate) Update(); return Convert.ToInt32(report.Substring(81, 1)); } }

                /// <summary>
                /// Framerate
                /// </summary>
                public int Framerate { get { if (AutoUpdate) Update(); return Convert.ToInt32(report.Substring(82, 3)); } }

                /// <summary>
                /// Speaker volume.
                /// </summary>
                public int SpeakerVolume { get { if (AutoUpdate) Update(); return Convert.ToInt32(report.Substring(85, 3)); } }

                /// <summary>
                /// Microphone volume.
                /// </summary>
                public int MicVolume { get { if (AutoUpdate) Update(); return Convert.ToInt32(report.Substring(88, 3)); } }

                /// <summary>
                /// 0 = do not show time in image, 1 = show time in image.
                /// </summary>
                public int ShowTime { get { if (AutoUpdate) Update(); return Convert.ToInt32(report.Substring(91, 1)); } }

                /// <summary>
                /// 0-15, 0 is max.
                /// </summary>
                public int WifiStrength { get { if (AutoUpdate) Update(); return Convert.ToInt32(report.Substring(92, 1)); } }

                /// <summary>
                /// 0-255, 255 is Max.
                /// </summary>
                public int BatteryLevel { get { if (AutoUpdate) Update(); return Convert.ToInt32(report.Substring(93, 2)); } }
            }
        }

        /// <summary>
        /// Contains all %API commands.
        /// </summary>
        public class API : Component
        {
            /// <summary>
            /// Movement commands.
            /// </summary>
            public Rovio.API.Movement Movement;
            
            /// <summary>
            /// Camera commands.
            /// </summary>
            public Rovio.API.Camera Camera;
            
            /// <summary>
            /// User commands.
            /// </summary>
            public Rovio.API.User User;
            
            /// <summary>
            /// Time commands.
            /// </summary>
            public Rovio.API.Time Time;
            
            /// <summary>
            /// Network commands.
            /// </summary>
            public Rovio.API.Network Network;
            
            /// <summary>
            /// Server commands.
            /// </summary>
            public Rovio.API.Server Server;
            
            /// <summary>
            /// Mail commands.
            /// </summary>
            public Rovio.API.Mail Mail;
            
            /// <summary>
            /// Other commands.
            /// </summary>
            public Rovio.API.Other Other;

            /// <summary>
            /// The constructor.
            /// </summary>
            public API(Robot _robot)
                : base(_robot)
            {
                Movement = new Rovio.API.Movement(_robot);
                Camera = new Rovio.API.Camera(_robot);
                User = new Rovio.API.User(_robot);
                Time = new Rovio.API.Time(_robot);
                Network = new Rovio.API.Network(_robot);
                Server = new Rovio.API.Server(_robot);
                Mail = new Rovio.API.Mail(_robot);
                Other = new Rovio.API.Other(_robot);
            }
        }
    }

    /// <summary>
    /// A convenience class for acessing camera realted functionality.
    /// </summary>
    public class Camera : Component
    {
        Bitmap image;
        API.Camera.HeadPosition head_position;
        API.Camera.ImageResolution resolution;
        API.Camera.ImageCompression image_compression;
        API.Camera.CameraFlickerFrequency flicker_frequency;
        int brightness;
        int framerate;


        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="_robot"></param>
        public Camera(Robot _robot) : base(_robot) 
        {
            head_position = Rovio.API.Camera.HeadPosition.DOWN;
            resolution = Rovio.API.Camera.ImageResolution.CGA;
            image_compression = Rovio.API.Camera.ImageCompression.LOW;
            flicker_frequency = Rovio.API.Camera.CameraFlickerFrequency.AUTO;
            brightness = 0;
            framerate = 0;
        }

        /// <summary>
        /// Update all camera parameters excluding the image.
        /// </summary>
        public override void Update()
        {
            robot.API.Movement.Report.Update();

            //store the current AutoUpdate value of the Report and switch it off when accessing multiple values
            bool auto_update = robot.API.Movement.Report.AutoUpdate;
            robot.API.Movement.Report.AutoUpdate = false;

            int hp = robot.API.Movement.Report.HeadPosition;
            if (hp <= 100)
                head_position = API.Camera.HeadPosition.UP;
            else if (hp <= 170)
                head_position = API.Camera.HeadPosition.MIDDLE;
            else
                head_position = API.Camera.HeadPosition.DOWN;

            resolution = robot.API.Movement.Report.Resolution;
 

            //restore the AutoUpdate value of the Report
            robot.API.Movement.Report.AutoUpdate = auto_update;
        }

        /// <summary>
        /// Update the camera image.
        /// </summary>
        public void UpdateImage()
        {
            image = robot.API.Camera.GetImage();
        }

        /// <summary>
        /// The latest image from the camera.
        /// </summary>
        public Bitmap Image
        {
            get 
            {
                if (AutoUpdate) UpdateImage();
                return image;
            }
        }

        /// <summary>
        /// Camera head position.
        /// </summary>
        public API.Camera.HeadPosition HeadPosition
        {
            set
            {
                switch (value)
                {
                    case API.Camera.HeadPosition.DOWN: robot.API.Movement.ManualDrive.HeadDown(); break;
                    case API.Camera.HeadPosition.MIDDLE: robot.API.Movement.ManualDrive.HeadMiddle(); break;
                    case API.Camera.HeadPosition.UP: robot.API.Movement.ManualDrive.HeadUp(); break;
                }
            }

            get
            {
                if (AutoUpdate) Update();
                return head_position;
            }
        }

        /// <summary>
        /// Image resolution.
        /// </summary>
        public API.Camera.ImageResolution Resolution
        {
            set { robot.API.Camera.Resolution = value; }
            get { if (AutoUpdate) Update(); return resolution; }
        }

        /// <summary>
        /// Image compression.
        /// </summary>
        public API.Camera.ImageCompression Compression
        {
            set { robot.API.Camera.Compression = value; }
            get { if (AutoUpdate) Update(); return image_compression; }
        }

        /// <summary>
        /// Camera flicker frequency.
        /// </summary>
        public API.Camera.CameraFlickerFrequency FlickerFrequency
        {
            set { robot.API.Camera.FlickerFrequency = value; }
            get { if (AutoUpdate) Update(); return flicker_frequency; }
        }

        /// <summary>
        /// Camera brightness.
        /// </summary>
        public int Brightness
        {
            set { robot.API.Camera.Brightness = value; }
            get { if (AutoUpdate) Update(); return brightness; }
        }

        /// <summary>
        /// Internal frame rate.
        /// </summary>
        public int Framerate
        {
            set { robot.API.Camera.Framerate = value; }
            get { if (AutoUpdate) Update(); return framerate; }
        }
    }

    /// <summary>
    /// A convenience class for odometry sensor.
    /// </summary>
    public class Odometry : Component
    {
        int left_ticks;
        int right_ticks;
        int rear_ticks;

        /// <summary>
        /// The constructor.
        /// </summary>
        public Odometry(Robot _robot) : base(_robot) 
        {
            left_ticks = 0;
            right_ticks = 0;
            rear_ticks = 0;
        }

        /// <summary>
        /// Update the odometry readings.
        /// </summary>
        public override void Update()
        {
            robot.API.Movement.MCUReport.Update();

            //store the current AutoUpdate value of the MCU Report and switch it off when accessing multiple values
            bool auto_update = robot.API.Movement.MCUReport.AutoUpdate;
            robot.API.Movement.MCUReport.AutoUpdate = false;

            left_ticks = robot.API.Movement.MCUReport.LeftWheelTicks;
            if (robot.API.Movement.MCUReport.LeftWheelRot == false)
                left_ticks = -left_ticks;

            right_ticks = robot.API.Movement.MCUReport.RightWheelTicks;
            if (robot.API.Movement.MCUReport.RightWheelRot == false)
                right_ticks = -right_ticks;

            rear_ticks = robot.API.Movement.MCUReport.LeftWheelTicks;
            if (robot.API.Movement.MCUReport.LeftWheelRot == false)
                rear_ticks = -rear_ticks;

            //restore the AutoUpdate value of the MCU Report
            robot.API.Movement.MCUReport.AutoUpdate = auto_update;
        }

        /// <summary>
        /// Left wheel ticks including rotation direction.
        /// </summary>
        public int LeftWheelTicks
        {
            get
            {
                if (AutoUpdate) Update();
                return left_ticks;
            }
        }

        /// <summary>
        /// Right wheel ticks including rotation direction.
        /// </summary>
        public int RightWheelTicks
        {
            get
            {
                if (AutoUpdate) Update();
                return right_ticks;
            }
        }

        /// <summary>
        /// Rear wheel ticks including rotation direction.
        /// </summary>
        public int RearWheelTicks
        {
            get
            {
                if (AutoUpdate) Update();
                return rear_ticks;
            }
        }
    }

    /// <summary>
    /// A convenience class for infra red proximity sensor.
    /// </summary>
    public class IRSensor : Component
    {
        bool power_on;
        bool detection;

        /// <summary>
        /// The constructor.
        /// </summary>
        public IRSensor(Robot _robot) : base(_robot) 
        {
            power_on = false;
            detection = false;
        }

        /// <summary>
        /// Update the IRSensor value.
        /// </summary>
        public override void Update()
        {
            robot.API.Movement.MCUReport.Update();

            //store the current AutoUpdate value of the MCU Report and switch it off when accessing multiple values
            bool auto_update = robot.API.Movement.MCUReport.AutoUpdate;
            robot.API.Movement.MCUReport.AutoUpdate = false;

            power_on = robot.API.Movement.MCUReport.IRPowerOn;
            detection = robot.API.Movement.MCUReport.IRDetectorOn;

            //restore the AutoUpdate value of the MCU Report
            robot.API.Movement.MCUReport.AutoUpdate = auto_update;
        }

        /// <summary>
        /// Activate the sensor.
        /// </summary>
        public bool PowerOn
        {
            set { robot.API.Movement.IRSensor(value); }
            get { if (AutoUpdate) Update(); return power_on; }
        }

        /// <summary>
        /// Sensor output: false - no obstacle, true - obstacle in front.
        /// </summary>
        public bool Detection
        {
            get { if (AutoUpdate) Update(); return detection; }
        }
    }

    /// <summary>
    /// A convenience class for accessing the TrueTrack navigation sensor.
    /// </summary>
    public class NavigationSensor : Component
    {
        double x, y, theta;
        int signal_strength;

        /// <summary>
        /// The constructor.
        /// </summary>
        public NavigationSensor(Robot _robot) : base(_robot) 
        {
            x = 0;
            y = 0;
            theta = 0;
            signal_strength = 0;
        }

        /// <summary>
        /// Update the IRSensor value.
        /// </summary>
        public override void Update()
        {
            robot.API.Movement.Report.Update();

            //store the current AutoUpdate value of the Report and switch it off when accessing multiple values
            bool auto_update = robot.API.Movement.Report.AutoUpdate;
            robot.API.Movement.Report.AutoUpdate = false;

            x = robot.API.Movement.Report.X;
            y = robot.API.Movement.Report.Y;
            theta = robot.API.Movement.Report.Theta;
            signal_strength = robot.API.Movement.Report.NavigationSS;

            //restore the AutoUpdate value of the Report
            robot.API.Movement.Report.AutoUpdate = auto_update;
        }

        /// <summary>
        /// X position.
        /// </summary>
        double X { get { if (AutoUpdate) Update(); return x; } }

        /// <summary>
        /// X position.
        /// </summary>
        double Y { get { if (AutoUpdate) Update(); return y; } }

        /// <summary>
        /// X position.
        /// </summary>
        double Theta { get { if (AutoUpdate) Update(); return theta; } }

        /// <summary>
        /// Signal strength of the navigation sensor.
        /// </summary>
        int SignalStrength { get { if (AutoUpdate) Update(); return signal_strength; } }
    }

    /// <summary>
    /// A convinience class for driving commands.
    /// </summary>
    public class Drive : Component
    {
        /// <summary>
        /// The constructor.
        /// </summary>
        public Drive(Robot _robot) : base(_robot) { }

        /// <summary>
        /// Stop the robot.
        /// </summary>
        public void Stop() { robot.API.Movement.ManualDrive.Stop(); }

        /// <summary>
        /// Move forward.
        /// </summary>
        /// <param name="speed">1 (fastest) - 10 (slowest)</param>
        public void Forward(int speed) { robot.API.Movement.ManualDrive.Forward(speed); }

        /// <summary>
        /// Move backward.
        /// </summary>
        /// <param name="speed"></param>
        public void Backward(int speed) { robot.API.Movement.ManualDrive.Backward(speed); }

        /// <summary>
        /// Move straight left.
        /// </summary>
        /// <param name="speed"></param>
        public void StraightLeft(int speed) { robot.API.Movement.ManualDrive.StraightLeft(speed); }

        /// <summary>
        /// Move straight right.
        /// </summary>
        /// <param name="speed"></param>
        public void StraightRight(int speed) { robot.API.Movement.ManualDrive.StraightRight(speed); }

        /// <summary>
        /// Rotate left.
        /// </summary>
        /// <param name="speed"></param>
        public void RotateLeft(int speed) { robot.API.Movement.ManualDrive.StraightLeft(speed); }

        /// <summary>
        /// Rotate right.
        /// </summary>
        /// <param name="speed"></param>
        public void RotateRight(int speed) { robot.API.Movement.ManualDrive.RotateRight(speed); }

        /// <summary>
        /// Diagonal forward left.
        /// </summary>
        /// <param name="speed"></param>
        public void DiagForwardLeft(int speed) { robot.API.Movement.ManualDrive.DiagForwardLeft(speed); }

        /// <summary>
        /// Diagonal forward right.
        /// </summary>
        /// <param name="speed"></param>
        public void DiagForwardRight(int speed) { robot.API.Movement.ManualDrive.DiagForwardRight(speed); }

        /// <summary>
        /// Diagonal backward left.
        /// </summary>
        /// <param name="speed"></param>
        public void DiagBackwardLeft(int speed) { robot.API.Movement.ManualDrive.DiagBackwardLeft(speed); }

        /// <summary>
        /// Diagonal backward right.
        /// </summary>
        /// <param name="speed"></param>
        public void DiagBackwardRight(int speed) { robot.API.Movement.ManualDrive.DiagBackwardRight(speed); }

        /// <summary>
        /// Rotate left by 20 degree angle increments.
        /// </summary>
        /// <param name="speed"></param>
        public void RotateLeft20(int speed) { robot.API.Movement.ManualDrive.RotateLeft20(speed); }

        /// <summary>
        /// Rotate right by 20 degree angle increments.
        /// </summary>
        /// <param name="speed"></param>
        public void RotateRight20(int speed) { robot.API.Movement.ManualDrive.RotateRight20(speed); }
    }
}