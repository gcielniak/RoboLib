/// <summary>
/// The %Roomba wrapper library. Includes the Robot class, SCI namespace with all commands as specified by the %SCI specification 
/// and a set of helper classes for easy access to robot's resources.
/// 
/// The majority of comments are based directly on the official iRobot %Roomba Serial Command Interface (%SCI) Specification document.
/// http://www.irobot.com/images/consumer/hacker/roomba_sci_spec_manual.pdf
/// 
/// </summary>
namespace Roomba
{
    using System;
    using System.Threading;
    using System.IO.Ports;

    /// <summary>
    /// The main class for communication with Roomba through a serial interface.
    /// 
    /// \todo Introduce helper classes for Roomba state, odometry, motors, etc. Implement iCreate specific methods.
    /// </summary>
    public class Robot
    {
        SerialPort com_port;

        /// <summary>
        /// All SCI commands.
        /// </summary>
        public SCI.SCI SCI;

        /// <summary>
        /// LED related commands.
        /// </summary>
        public Leds Leds;

        /// <summary>
        /// Sensor realted commands.
        /// </summary>
        public Sensors Sensors;

        public Robot()
        {
            com_port = new SerialPort();
            com_port.ReadTimeout = 1000;
            com_port.WriteTimeout = 1000;
            com_port.BaudRate = 56700;
            com_port.DataBits = 8;
            com_port.Parity = Parity.None;
            com_port.StopBits = StopBits.One;

            SCI = new SCI.SCI(this);
            Leds = new Leds(this);
            Sensors = new Sensors(this);
        }

        /// <summary>
        /// Connect to the robot.
        /// </summary>
        /// <param name="port_name">serial port name</param>
        public void Connect(string port_name)
        {
            com_port.PortName = port_name;
            com_port.Open();

            SCI.Start();
            //Send a dummy sensor update request to make sure that the connection with the robot is established.
            SCI.UpdateSensorState(0);
        }

        /// <summary>
        /// Disconnect from the robot.
        /// </summary>
        public void Disconnect()
        {
            com_port.Close();
        }

        /// <summary>
        /// Automatically scan all existing serial ports and connect to the first visible robot.
        /// 
        /// It is slower than directly connecting to a specific port.
        /// </summary>
        public void AutoConnect()
        { 
            foreach (string s in SerialPort.GetPortNames())
            {
                try
                {
                    Connect(s);
                    return;
                }
                catch (Exception) 
                {
                    Disconnect();
                }
            }
            throw new Exception("Robot.AutoConnect, could not find the robot.");
        }

        /// <summary>
        /// Send a byte array corresponding to a desired command.
        /// </summary>
        /// <param name="command"></param>
        public void Send(byte[] command)
        {
            com_port.Write(command, 0, command.Length);
        }

        /// <summary>
        /// Receive data, with timeout functionality.
        /// The call to this method has to be preceded by an appropriate request (see the Send function).
        /// </summary>
        /// <param name="data"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public void Receive(ref byte[] data, int offset, int count)
        {
            TimeSpan timeout = TimeSpan.Zero;
            DateTime timeout_start = DateTime.Now;

            while (com_port.BytesToRead != count)
            {
                if (timeout.TotalMilliseconds < com_port.ReadTimeout)
                    timeout = DateTime.Now - timeout_start;
                else
                    throw new Exception("Robot.Receive, Timeout.");
            }

            byte[] reply = new byte[com_port.BytesToRead];

            com_port.Read(reply, 0, reply.Length);
            Array.Copy(reply, 0, data, offset, reply.Length);
        }
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
        public Component(Robot _robot)
        {
            robot = _robot;
        }

        /// <summary>
        /// Automatic update option for methods requesting more than one piece of information.
        /// </summary>
        public bool AutoUpdate = true;
    }

    /// <summary>
    /// All commands as specified by the %SCI document.
    /// </summary>
    namespace SCI
    {
        /// <summary>
        /// Implementation of all SCI commands.
        /// </summary>
        public class SCI : Component
        {
            /// <summary>
            /// The raw sensor data.
            /// </summary>
            public byte[] sensor_state;

            /// <summary>
            /// The constructor.
            /// </summary>
            public SCI(Robot _robot) : base(_robot)
            {
                sensor_state = new byte[26];
            }

            /// <summary>
            /// Starts the SCI. The Start command must be sent before any other SCI commands. This command puts the SCI in passive mode.
            /// </summary>
            public void Start() { robot.Send(new byte[] { 128 }); }

            /// <summary>
            /// Sets the baud rate in bits per second (bps) at which SCI commands and data are sent according to the baud code sent
            /// in the data byte. The default baud rate at power up is 57600 bps. (See Serial Port Settings, above.) Once the baud rate is
            /// changed, it will persist until Roomba is power cycled by removing the battery (or until the battery voltage falls below the minimum
            /// required for processor operation). You must wait 100ms after sending this command before sending additional commands
            /// at the new baud rate. The SCI must be in passive, safe, or full mode to accept this command. This command puts the SCI in
            /// passive mode.
            /// </summary>
            /// <param name="baud_rate">baud rate</param>
            public void Baud(BaudRate baud_rate)
            {
                robot.Send(new byte[] { 129, (byte)baud_rate});
                Thread.Sleep(100);
            }

            /// <summary>
            /// Enables user control of Roomba. This command must be sent after the start command and before any control commands are
            /// sent to the SCI. The SCI must be in passive mode to accept this command. This command puts the SCI in safe mode.
            /// </summary>
            public void Control()
            {
                robot.Send(new byte[] { 130 });
            }

            /// <summary>
            /// This command puts the SCI in safe mode. The SCI must be in full mode to accept this command.
            /// Note: In order to go from passive mode to safe mode, use the Control command.
            /// </summary>
            public void Safe()
            {
                robot.Send(new byte[] { 131 });
            }

            /// <summary>
            /// Enables unrestricted control of Roomba through the SCI and turns off the safety features. The SCI must be in safe mode to
            /// accept this command. This command puts the SCI in full mode.
            /// </summary>
            public void Full()
            {
                robot.Send(new byte[] { 132 });
            }

            /// <summary>
            /// Puts Roomba to sleep, the same as a normal “power” button press. The Device Detect line must be held low for 500 ms to
            /// wake up Roomba from sleep. The SCI must be in safe or full mode to accept this command. This command puts the SCI in
            /// passive mode.
            /// </summary>
            public void Sleep()
            {
                robot.Send(new byte[] { 133 });
            }

            /// <summary>
            /// Starts a spot cleaning cycle, the same as a normal “spot” button press. The SCI must be in safe or full mode to accept this
            /// command. This command puts the SCI in passive mode.
            /// </summary>
            public void Spot()
            {
                robot.Send(new byte[] { 134 });
            }

            /// <summary>
            /// Starts a normal cleaning cycle, the same as a normal “clean” button press. The SCI must be in safe or full mode to accept this
            /// command. This command puts the SCI in passive mode.
            /// </summary>
            public void Clean()
            {
                robot.Send(new byte[] { 135 });
            }

            /// <summary>
            /// Starts a maximum time cleaning cycle, the same as a normal “max” button press. The SCI must be in safe or full mode to
            /// accept this command. This command puts the SCI in passive mode.
            /// </summary>
            public void Max()
            {
                robot.Send(new byte[] { 136 });
            }

            /// <summary>
            /// Controls Roomba’s drive wheels. The command takes four data bytes, which are interpreted as two 16 bit signed values using
            /// twos-complement. The first two bytes specify the average velocity of the drive wheels in millimeters per second (mm/s), with the
            /// high byte sent first. The next two bytes specify the radius, in millimeters, at which Roomba should turn. The longer radii make
            /// Roomba drive straighter; shorter radii make it turn more. A Drive command with a positive velocity and a positive radius will make
            /// Roomba drive forward while turning toward the left. A negative radius will make it turn toward the right. Special cases for the
            /// radius make Roomba turn in place or drive straight, as specified below. The SCI must be in safe or full mode to accept this
            /// command. This command does change the mode.
            /// </summary>
            public void Drive(short velocity, short radius)
            {
                robot.Send(new byte[] { 137, (byte)(velocity >> 8), (byte)velocity, (byte)(radius >> 8), (byte)radius });
            }

            /// <summary>
            /// Controls Roomba’s cleaning motors. The state of each motor is specified by one bit in the data byte. The SCI must be in safe
            /// or full mode to accept this command. This command does not change the mode.
            /// </summary>
            /// <param name="main_brush">Main brush on/off (true/false)</param>
            /// <param name="vacuum">Vacuum on/off (true/false)</param>
            /// <param name="side_brush">Side brush on/off (true/false)</param>
            public void Motors(bool main_brush, bool vacuum, bool side_brush)
            {
                byte[] command = { 138, 0 };
                if (main_brush)
                    command[1] |= 0x04;
                if (vacuum)
                    command[2] |= 0x02;
                if (side_brush)
                    command[3] |= 0x01;

                robot.Send(command);
            }

            /// <summary>
            /// Controls Roomba’s LEDs. The state of each of the spot, clean, max, and dirt detect LEDs is specified by one bit in the first data
            /// byte. The color of the status LED is specified by two bits in the first data byte. The power LED is specified by two data bytes, one
            /// for the color and one for the intensity. The SCI must be in safe or full mode to accept this command. This command does not
            /// change the mode.
            /// </summary>
            /// <param name="led_bits"></param>
            /// <param name="power_color"></param>
            /// <param name="power_intensity"></param>
            public void Leds(int led_bits, int power_color, int power_intensity)
            {
                robot.Send(new byte[] { 139, (byte)led_bits, (byte)power_color, (byte)power_intensity });
            }

            /// <summary>
            /// Specifies a song to the SCI to be played later. Each song is associated with a song number which the Play command uses
            /// to select the song to play. Users can specify up to 16 songs with up to 16 notes per song. Each note is specified by a note
            /// number using MIDI note definitions and a duration specified in fractions of a second. The number of data bytes varies
            /// depending on the length of the song specified. A one note song is specified by four data bytes. For each additional note, two data
            /// bytes must be added. The SCI must be in passive, safe, or full mode to accept this command. This command does not change
            /// the mode.
            /// </summary>
            /// <param name="song_number"></param>
            /// <param name="song"></param>
            public void Song(int song_number, byte[] song)
            {
                int length = song.Length;
                if (song.Length > 32)
                    length = 32;
                byte[] command = new byte[length + 3];
                command[0] = 140;
                command[1] = (byte)song_number;
                command[2] = (byte)(length / 2);
                Array.Copy(song, 0, command, 3, length);
                robot.Send(command);
            }

            /// <summary>
            /// Plays one of 16 songs, as specified by an earlier Song command. If the requested song has not been specified yet,
            /// the Play command does nothing. The SCI must be in safe or full mode to accept this command. This command does not change
            /// the mode.
            /// </summary>
            public void Play(byte song_number)
            {
                robot.Send(new byte[] { 141, song_number });
            }

            /// <summary>
            /// Requests the SCI to send a packet of sensor data bytes. The user can select one of four different sensor packets. The sensor
            /// data packets are explained in more detail in the next section. The SCI must be in passive, safe, or full mode to accept this
            /// command. This command does not change the mode.
            /// </summary>
            /// <param name="packet"></param>
            public void Sensors(SensorPacket packet)
            {
                robot.Send(new byte[] { 142, (byte)packet });
            }

            /// <summary>
            /// Turns on force-seeking-dock mode, which causes the robot to immediately attempt to dock during its cleaning cycle if it
            /// encounters the docking beams from the Home Base. (Note, however, that if the robot was not active in a clean, spot or max
            /// cycle it will not attempt to execute the docking.) Normally the robot attempts to dock only if the cleaning cycle has completed
            /// or the battery is nearing depletion. This command can be sent anytime, but the mode will be cancelled if the robot turns off,
            /// begins charging, or is commanded into SCI safe or full modes.
            /// </summary>
            public void ForceSeekingDock()
            {
                robot.Send(new byte[] { 143 });
            }

            /// <summary>
            /// Update the sensor state. The robot will send back one of four different sensor data packets in response to a Sensor command,
            /// depending on the value of the packet code data byte. The data bytes are specified below in the order in which they will be sent.
            /// A packet code value of 0 sends all of the data bytes. A value of 1 through 3 sends a subset of the sensor data.
            /// Some of the sensor data values are 16 bit values. These values are sent as two bytes, high byte first.
            /// </summary>
            /// <param name="packet"></param>
            public void UpdateSensorState(SensorPacket packet)
            {
                Sensors(packet);

                switch (packet)
                {
                    case SensorPacket.ALL_SENSORS:
                        robot.Receive(ref sensor_state, 0, 26);
                        break;
                    case SensorPacket.PACKET_1:
                        robot.Receive(ref sensor_state, 0, 10);
                        break;
                    case SensorPacket.PACKET_2:
                        robot.Receive(ref sensor_state, 10, 6);
                        break;
                    case SensorPacket.PACKET_3:
                        robot.Receive(ref sensor_state, 16, 10);
                        break;
                    default:
                        break;
                }
            }
        }
   }

    /// <summary>
    /// Charging state of the robot.
    /// </summary>
    public enum ChargingState
    {
        NOT_CHARGING = 0,
        CHARGING_RECOVERY = 1,
        CHARGING = 2,
        TRCIKLE_CHARGING = 3,
        WAITING = 4,
        CHARGING_ERROR = 5
    }

    /// <summary>
    /// Baud rate of the serial connection.
    /// </summary>
    public enum BaudRate
    {
        BAUDRATE_300 = 0,
        BAUDRATE_600 = 1,
        BAUDRATE_1200 = 2,
        BAUDRATE_2400 = 3,
        BAUDRATE_4800 = 4,
        BAUDRATE_9600 = 5,
        BAUDRATE_14400 = 6,
        BAUDRATE_19200 = 7,
        BAUDRATE_28800 = 8,
        BAUDRATE_38400 = 9,
        BAUDRATE_57600 = 10,
        BAUDRATE_115200 = 11
    }

    /// <summary>
    /// Status LED colour.
    /// </summary>
    public enum StatusLED
    {
        STATUSLED_OFF = 0,
        STATUSLED_RED = 1,
        STATUSLED_GREEN = 2,
        STATUSLED_AMBER = 3
    }

    /// <summary>
    /// Sensor packet type. 
    /// A packet code value of 0 sends all of the data bytes. A value of 1 through 3 sends a subset of the sensor data.
    /// </summary>
    public enum SensorPacket
    {
        ALL_SENSORS = 0,
        PACKET_1 = 1,
        PACKET_2 = 2,
        PACKET_3 = 3
    }

    /// <summary>
    /// A convenience class for accessing Roomba's sensors.
    /// </summary>
    public class Sensors : Component
    {
        /// <summary>
        /// Bump sensors.
        /// </summary>
        public Bump Bumps;
        
        /// <summary>
        /// Wheeldrop sensors.
        /// </summary>
        public Wheeldrop Wheeldrops;
        
        /// <summary>
        /// Cliff sensors.
        /// </summary>
        public Cliff Cliffs;

        /// <summary>
        /// Motor Overcurrent sensors.
        /// </summary>
        public MotorOvercurrent MotorOvercurrents;

        /// <summary>
        /// Dirt Detector sensors.
        /// </summary>
        public DirtDetector DirtDetectors;

        /// <summary>
        /// Buttons.
        /// </summary>
        public Button Buttons;

        /// <summary>
        /// The constructor.
        /// </summary>
        public Sensors(Robot _robot)
            : base(_robot)
        {
            Bumps = new Bump(_robot);
            Wheeldrops = new Wheeldrop(_robot);
            Cliffs = new Cliff(_robot);
            MotorOvercurrents = new MotorOvercurrent(_robot);
            DirtDetectors = new DirtDetector(_robot);
            Buttons = new Button(_robot);
        }

        /// <summary>
        /// Update the sensor state.
        /// </summary>
        /// <param name="packet"></param>
        public void Update(SensorPacket packet) { robot.SCI.UpdateSensorState(packet); }

        /// <summary>
        /// The state of the bump sensors: false = no bump, true = bump.
        /// </summary>
        public class Bump : Component
        {
            /// <summary>
            /// The constructor.
            /// </summary>
            public Bump(Robot _robot) : base(_robot) { }

            /// <summary>
            /// Right bump sensor.
            /// </summary>
            public bool Right { get { if (AutoUpdate) robot.Sensors.Update(SensorPacket.PACKET_1); return ((robot.SCI.sensor_state[0] & 0x01) != 0); } }

            /// <summary>
            /// Left bump sensor.
            /// </summary>
            public bool Left { get { if (AutoUpdate) robot.Sensors.Update(SensorPacket.PACKET_1); return ((robot.SCI.sensor_state[0] & 0x02) != 0); } }
        }

        /// <summary>
        /// The state of the Wheeldrop sensors: false = wheel up, true = wheel dropped.
        /// 
        /// Note: Some robots do not report the three wheel drops separately. Instead,
        /// if any of the three wheels drops, all three wheel-drop bits will be set. You
        /// can tell which kind of robot you have by examining the serial number
        /// inside the battery compartment. Wheel drops are separate only if there is
        /// an “E” in the serial number.
        /// </summary>
        public class Wheeldrop : Component
        {
            /// <summary>
            /// The constructor.
            /// </summary>
            /// <param name="_robot"></param>
            public Wheeldrop(Robot _robot) : base(_robot) { }

            /// <summary>
            /// Right wheeldrop sensor.
            /// </summary>
            public bool Right { get { if (AutoUpdate) robot.Sensors.Update(SensorPacket.PACKET_1); return ((robot.SCI.sensor_state[0] & 0x04) != 0); } }

            /// <summary>
            /// Left wheeldrop sensor.
            /// </summary>
            public bool Left { get { if (AutoUpdate) robot.Sensors.Update(SensorPacket.PACKET_1); return ((robot.SCI.sensor_state[0] & 0x08) != 0); } }

            /// <summary>
            /// Caster wheeldrop sensor.
            /// </summary>
            public bool Caster { get { if (AutoUpdate) robot.Sensors.Update(SensorPacket.PACKET_1); return ((robot.SCI.sensor_state[0] & 0x10) != 0); } }
        }

        /// <summary>
        /// The state of the wall sensor: false = no wall, true = wall.
        /// </summary>
        public bool Wall { get { if (AutoUpdate) Update(SensorPacket.PACKET_1); return (robot.SCI.sensor_state[1] != 0); } }

        /// <summary>
        /// The state of the cliff sensors: false = no cliff, true = cliff.
        /// </summary>
        public class Cliff : Component
        {
            /// <summary>
            /// The constructor.
            /// </summary>
            /// <param name="_robot"></param>
            public Cliff(Robot _robot) : base(_robot) { }

            /// <summary>
            /// Left cliff sensor.
            /// </summary>
            public bool Left { get { if (AutoUpdate) robot.Sensors.Update(SensorPacket.PACKET_1); return (robot.SCI.sensor_state[2] != 0); } }

            /// <summary>
            /// Front left cliff sensor.
            /// </summary>
            public bool FrontLeft { get { if (AutoUpdate) robot.Sensors.Update(SensorPacket.PACKET_1); return (robot.SCI.sensor_state[3] != 0); } }

            /// <summary>
            /// Front right cliff sensor.
            /// </summary>
            public bool FrontRight { get { if (AutoUpdate) robot.Sensors.Update(SensorPacket.PACKET_1); return (robot.SCI.sensor_state[4] != 0); } }

            /// <summary>
            /// Right cliff sensor.
            /// </summary>
            public bool Right { get { if (AutoUpdate) robot.Sensors.Update(SensorPacket.PACKET_1); return (robot.SCI.sensor_state[5] != 0); } }
        }

        /// <summary>
        /// The state of the virtual wall sensor: false = no wall, true = wall.
        /// </summary>
        public bool VirtualWall { get { if (AutoUpdate) Update(SensorPacket.PACKET_1); return (robot.SCI.sensor_state[6] != 0); } }

        /// <summary>
        /// The state of the five motors’ overcurrent sensors: false = no overcurrent, true = overcurrent.
        /// </summary>
        public class MotorOvercurrent : Component
        {
            /// <summary>
            /// The constructor.
            /// </summary>
            /// <param name="_robot"></param>
            public MotorOvercurrent(Robot _robot) : base(_robot) { }

            /// <summary>
            /// The state of the side brush motor overrcurrent.
            /// </summary>
            public bool SideBrush { get { if (AutoUpdate) robot.Sensors.Update(SensorPacket.PACKET_1); return ((robot.SCI.sensor_state[7] & 0x01) != 0); } }

            /// <summary>
            /// The state of the vacuum motor overrcurrent.
            /// </summary>
            public bool Vacuum { get { if (AutoUpdate) robot.Sensors.Update(SensorPacket.PACKET_1); return ((robot.SCI.sensor_state[7] & 0x02) != 0); } }

            /// <summary>
            /// The state of the main brush motor overrcurrent.
            /// </summary>
            public bool MainBrush { get { if (AutoUpdate) robot.Sensors.Update(SensorPacket.PACKET_1); return ((robot.SCI.sensor_state[7] & 0x04) != 0); } }

            /// <summary>
            /// The state of the drive right motor overrcurrent.
            /// </summary>
            public bool DriveRight { get { if (AutoUpdate) robot.Sensors.Update(SensorPacket.PACKET_1); return ((robot.SCI.sensor_state[7] & 0x08) != 0); } }

            /// <summary>
            /// The state of the drive left motor overrcurrent.
            /// </summary>
            public bool DriveLeft { get { if (AutoUpdate) robot.Sensors.Update(SensorPacket.PACKET_1); return ((robot.SCI.sensor_state[7] & 0x10) != 0); } }
        }

        /// <summary>
        /// The current dirt detection level (0-255) of the dirt detector. Higher values indicate higher levels of dirt detected.
        /// </summary>
        public class DirtDetector : Component
        {
            /// <summary>
            /// The constructor.
            /// </summary>
            public DirtDetector(Robot _robot) : base(_robot) { }

            /// <summary>
            /// The current dirt level of the left dirt detector.
            /// </summary>
            public int Left { get { if (AutoUpdate) robot.Sensors.Update(SensorPacket.PACKET_1); return (int)robot.SCI.sensor_state[8]; } }

            /// <summary>
            /// The current dirt level of the right dirt detector.
            /// </summary>
            public int Right { get { if (AutoUpdate) robot.Sensors.Update(SensorPacket.PACKET_1); return (int)robot.SCI.sensor_state[9]; } }
        }

        /// <summary>
        /// The command number of the remote control command currently
        /// being received by Roomba. A value of 255 indicates that no
        /// remote control command is being received.
        /// </summary>
        public int RemoteControlCommand { get { if (AutoUpdate) Update(SensorPacket.PACKET_2); return (int)robot.SCI.sensor_state[10]; } }

        /// <summary>
        /// The state of the four Roomba buttons: false = button not pressed, true = button pressed.
        /// </summary>
        public class Button : Component
        {
            /// <summary>
            /// The constructor.
            /// </summary>
            public Button(Robot _robot) : base(_robot) { }

            /// <summary>
            /// The state of the Max button.
            /// </summary>
            public bool Max { get { if (AutoUpdate) robot.Sensors.Update(SensorPacket.PACKET_2); return ((robot.SCI.sensor_state[11] & 0x01) != 0); } }

            /// <summary>
            /// The state of the Clean button.
            /// </summary>
            public bool Clean { get { if (AutoUpdate) robot.Sensors.Update(SensorPacket.PACKET_2); return ((robot.SCI.sensor_state[11] & 0x02) != 0); } }

            /// <summary>
            /// The state of the Spot button.
            /// </summary>
            public bool Spot { get { if (AutoUpdate) robot.Sensors.Update(SensorPacket.PACKET_2); return ((robot.SCI.sensor_state[11] & 0x04) != 0); } }

            /// <summary>
            /// The state of the Power button.
            /// </summary>
            public bool Power { get { if (AutoUpdate) robot.Sensors.Update(SensorPacket.PACKET_2); return ((robot.SCI.sensor_state[11] & 0x08) != 0); } }
        }

        /// <summary>
        /// The distance that Roomba has traveled in millimeters since the
        /// distance it was last requested. This is the same as the sum of
        /// the distance traveled by both wheels divided by two. Positive
        /// values indicate travel in the forward direction; negative in the
        /// reverse direction. If the value is not polled frequently enough, it
        /// will be capped at its minimum or maximum. (16 bit signed integer)
        /// </summary>
        public int Distance { get { if (AutoUpdate) Update(SensorPacket.PACKET_2); return BitConverter.ToInt16(new byte[] { robot.SCI.sensor_state[13], robot.SCI.sensor_state[12] }, 0); } }

        /// <summary>
        /// The angle that Roomba has turned through since the angle was
        /// last requested. The angle is expressed as the difference in
        /// the distance traveled by Roomba’s two wheels in millimeters,
        /// specifically the right wheel distance minus the left wheel
        /// distance, divided by two. This makes counter-clockwise angles
        /// positive and clockwise angles negative. This can be used to
        /// directly calculate the angle that Roomba has turned through
        /// since the last request. Since the distance between Roomba’s
        /// wheels is 258mm, the equations for calculating the angles in
        /// familiar units are:
        /// Angle in radians = (2 * difference) / 258
        /// Angle in degrees = (360 * difference) / (258 * Pi).
        /// If the value is not polled frequently enough, it will be capped at
        /// its minimum or maximum. (16 bit signed integer)
        /// </summary>
        public int Angle { get { if (AutoUpdate) Update(SensorPacket.PACKET_2); return BitConverter.ToInt16(new byte[] { robot.SCI.sensor_state[15], robot.SCI.sensor_state[14] }, 0); } }

        /// <summary>
        /// Get charging state of the robot.
        /// </summary>
        public ChargingState ChargingState { get { if (AutoUpdate) Update(SensorPacket.PACKET_3); return (ChargingState)robot.SCI.sensor_state[16]; } }

        /// <summary>
        /// The voltage of Roomba’s battery in millivolts (mV).
        /// </summary>
        public int Voltage { get { if (AutoUpdate) Update(SensorPacket.PACKET_3); return (int)((robot.SCI.sensor_state[17] << 7) | robot.SCI.sensor_state[18]); } }

        /// <summary>
        /// The current in milliamps (mA) flowing into or out of Roomba’s
        /// battery. Negative currents indicate current is flowing out of the
        /// battery, as during normal running. Positive currents indicate
        /// current is flowing into the battery, as during charging.
        /// </summary>
        public int Current { get { if (AutoUpdate) Update(SensorPacket.PACKET_3); return BitConverter.ToInt16(new byte[] { robot.SCI.sensor_state[20], robot.SCI.sensor_state[19] }, 0); } }

        /// <summary>
        /// The temperature of Roomba’s battery in degrees Celsius.
        /// </summary>
        public int Temperature { get { if (AutoUpdate) Update(SensorPacket.PACKET_3); return (int)((sbyte)(robot.SCI.sensor_state[21])); } }

        /// <summary>
        /// The current charge of Roomba’s battery in milliamp-hours (mAh).
        /// The charge value decreases as the battery is depleted during
        /// running and increases when the battery is charged.
        /// </summary>
        public int Charge { get { if (AutoUpdate) Update(SensorPacket.PACKET_3); return (int)((robot.SCI.sensor_state[22] << 7) | robot.SCI.sensor_state[23]); } }

        /// <summary>
        /// The estimated charge capacity of Roomba’s battery. When the
        /// Charge value reaches the Capacity value, the battery is fully
        /// charged.
        /// </summary>
        public int Capacity { get { if (AutoUpdate) Update(SensorPacket.PACKET_3); return (int)((robot.SCI.sensor_state[24] << 7) | robot.SCI.sensor_state[25]); } }
    }

    /// <summary>
    /// A convenience class for accessing Roomba's LEDs.
    /// </summary>
    public class Leds
    {
        Robot robot;
        byte bits, power_colour, power_intensity;
        
        /// <summary>
        /// Power LED.
        /// </summary>
        public PowerLed Power;

        /// <summary>
        /// The constructor.
        /// </summary>
        public Leds(Robot _robot)
        {
            robot = _robot;
            bits = 0;
            power_colour = 0;
            power_intensity = 0;
            Power = new PowerLed(this);
        }

        /// <summary>
        /// Update all leds on the robot.
        /// </summary>
        void Update()
        {
            robot.SCI.Leds(bits, power_colour, power_intensity);
        }

        /// <summary>
        /// DirtDetect Led - blue light.
        /// </summary>
        public bool DirtDetect
        {
            set
            {
                if (value)
                    bits |= 0x01;
                else
                    bits &= 0xFF - 0x01;
                Update();
            }

            get { return ((bits & 0x01) != 0); }
        }

        /// <summary>
        /// Max Led - green light.
        /// iCreate middle led.
        /// </summary>
        public bool Max
        {
            set
            {
                if (value)
                    bits |= 0x02;
                else
                    bits &= 0xFF - 0x02;
                Update();
            }

            get { return ((bits & 0x02) != 0); }
        }

        /// <summary>
        /// Clean Led - green light.
        /// </summary>
        public bool Clean
        {
            set
            {
                if (value)
                    bits |= 0x04;
                else
                    bits &= 0xFF - 0x04;
                Update();
            }

            get { return ((bits & 0x04) != 0); }
        }

        /// <summary>
        /// Spot Led - green light.
        /// </summary>
        public bool Spot
        {
            set
            {
                if (value)
                    bits |= 0x08;
                else
                    bits &= 0xFF - 0x08;
                Update();
            }

            get { return ((bits & 0x08) != 0); }
        }

        /// <summary>
        /// Status Led - controls the status led colour.
        /// </summary>
        public StatusLED Status
        {
            set { bits |= (byte)((byte)value << 4); Update(); }
            get { return (StatusLED)(bits >> 4); }
        }

        /// <summary>
        /// Power Led - controls the colour and intensity.
        /// iCreate left led.
        /// </summary>
        public class PowerLed
        {
            Leds leds;

            /// <summary>
            /// The constructor.
            /// </summary>
            public PowerLed(Leds _leds)
            {
                leds = _leds;
            }

            /// <summary>
            /// Power Led Colour - 0-255 (green-red).
            /// </summary>
            public int Color
            {
                set { leds.power_colour = (byte)value; leds.Update(); }
                get { return leds.power_colour; }
            }

            /// <summary>
            /// Power Led Intensity - 0-255 (off-on).
            /// </summary>
            public int Intensity
            {
                set { leds.power_intensity = (byte)value; leds.Update(); }
                get { return leds.power_intensity; }
            }
        }
    }
}