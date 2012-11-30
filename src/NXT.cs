/// <summary>
/// The %NXT wrapper library. Includes the Robot class, API namespace with all commands as specified by the %API specification 
/// and a set of helper classes for easy access to robot's resources.
/// 
/// The majority of comments are based directly on the official LEGO Mindstorms NXT Communication Protocol.
/// </summary>
namespace NXT
{
    using System;
    using System.IO.Ports;
    using System.Threading;

    /// <summary>
    /// The main robot class.
    /// </summary>
    public class Robot
    {
        SerialPort serial_port;

        /// <summary>
        /// API commands.
        /// </summary>
        public API.API API;

        /// <summary>
        /// Device info.
        /// </summary>
        public DeviceInfo DeviceInfo;

        /// <summary>
        /// Input ports.
        /// </summary>
        public InPort InPort1,
            InPort2,
            InPort3,
            InPort4;

        /// <summary>
        /// Output ports.
        /// </summary>
        public OutPort OutPortA,
            OutPortB,
            OutPortC,
            OutPortAll;

        /// <summary>
        /// The constructor.
        /// </summary>
        public Robot()
        {
            serial_port = new SerialPort();
            serial_port.BaudRate = 115200 / 4;
            serial_port.DataBits = 8;
            serial_port.Parity = Parity.None;
            serial_port.StopBits = StopBits.One;
            serial_port.ReadTimeout = 500;
            serial_port.WriteTimeout = 500;
            serial_port.Handshake = Handshake.None;

            API = new NXT.API.API(this);

            DeviceInfo = new DeviceInfo(this);

            InPort1 = new InPort(this);
            InPort1.Number = 0;
            InPort2 = new InPort(this);
            InPort2.Number = 1;
            InPort3 = new InPort(this);
            InPort3.Number = 2;
            InPort4 = new InPort(this);
            InPort4.Number = 3;

            OutPortA = new OutPort(this);
            OutPortA.Number = 0;
            OutPortB = new OutPort(this);
            OutPortB.Number = 1;
            OutPortC = new OutPort(this);
            OutPortC.Number = 2;
            OutPortAll = new OutPort(this);
            OutPortAll.Number = 0xFF;
        }

        /// <summary>
        /// Connect to the robot.
        /// </summary>
        /// <param name="port_name">serial port name</param>
        public void Connect(string port_name)
        {
            serial_port.PortName = port_name;
            serial_port.Open();

            try { API.DirectCommand.KeepAlive(); }

            catch (Exception)
            {
                Disconnect();
                throw new Exception("Robot.Connect, connection error.");
            }
        }

        /// <summary>
        /// Disconnect from the robot.
        /// </summary>
        public void Disconnect()
        {
            serial_port.Close();
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
                try { Connect(s); return; }
                catch (Exception) { Disconnect(); }

                //fix the SerialPort bug in .NET libary: additional character is added to port names 
                try { Connect(s.Substring(0, s.Length - 1)); return; }
                catch (Exception) { Disconnect(); }
            }

            throw new Exception("Robot.AutoConnect, could not find the robot.");
        }

        /// <summary>
        /// Generic send command.
        /// </summary>
        public void Send(byte[] command, out byte[] response)
        {
            byte[] data = new byte[command.Length + 2];
            data[0] = (byte)command.Length;
            data[1] = (byte)(command.Length >> 8);
            command.CopyTo(data, 2);

            serial_port.Write(data, 0, data.Length);

            response = null;

            if (command[0] < 0x80)
            {
                int lsb = this.serial_port.ReadByte();
                int msb = this.serial_port.ReadByte();
                int size = (msb << 8) + lsb;

                response = new byte[size];

                serial_port.Read(response, 0, size);

                if ((response.Length < 3) || (response[1] != command[1]))
                    throw new Exception("Robot.Send, Wrong response format.");
                if (response[2] != 0)
                    throw new CommunicationException(response[2]);
            }
        }

        /// <summary>
        /// Communication exceptions with NXT.
        /// </summary>
        public class CommunicationException : Exception
        {
            int error_code;

            /// <summary>
            /// The constructor.
            /// </summary>
            public CommunicationException(int _error_code)
            {
                error_code = _error_code;
            }

            /// <summary>
            /// The error specific message.
            /// </summary>
            public override string Message
            {
                get
                {
                    string message = "CommunicationException: ";

                    switch (error_code)
                    {
                        case 0x20: return message + "Pending communication transaction in progress";
                        case 0x40: return message + "Specified mailbox queue is empty";
                        case 0x81: return message + "No more handles";
                        case 0x82: return message + "No space";
                        case 0x83: return message + "No more files";
                        case 0x84: return message + "End of file expected";
                        case 0x85: return message + "End of file";
                        case 0x86: return message + "Not a linear file";
                        case 0x87: return message + "File not found";
                        case 0x88: return message + "Handle already closed";
                        case 0x89: return message + "No linear space";
                        case 0x8A: return message + "Undefined error";
                        case 0x8B: return message + "File is busy";
                        case 0x8C: return message + "No write buffers";
                        case 0x8D: return message + "Append not possible";
                        case 0x8E: return message + "File is full";
                        case 0x8F: return message + "File exists";
                        case 0x90: return message + "Module not found";
                        case 0x91: return message + "Out of boundary";
                        case 0x92: return message + "Illegal file name";
                        case 0x93: return message + "Illegal handle";
                        case 0xBD: return message + "Request failed (i.e. specified file not found)";
                        case 0xBE: return message + "Unknown command opcode";
                        case 0xBF: return message + "Insane packet";
                        case 0xC0: return message + "Data contains out-of-range values";
                        case 0xDD: return message + "Communication bus error";
                        case 0xDE: return message + "No free memory in communication buffer";
                        case 0xDF: return message + "Specified channel/connection is not valid";
                        case 0xE0: return message + "Specified channel/connection not configured or busy";
                        case 0xEC: return message + "No active program";
                        case 0xED: return message + "Illegal size specified";
                        case 0xEE: return message + "Illegal mailbox queue ID specified";
                        case 0xEF: return message + "Attempted to access invalid field of a structure";
                        case 0xF0: return message + "Bad input or output specified";
                        case 0xFB: return message + "Insufficient memory available";
                        case 0xFF: return message + "Bad arguments";
                        default: return message + "Unidentified error.";
                    }
                }
            }
        }

        /// <summary>
        /// Send command without expecting the response.
        /// </summary>
        public void Send(byte[] command)
        {
            byte[] response;
            Send(command, out response);
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

    namespace API
    {
        /// <summary>
        /// All commands as specified by the API document.
        /// </summary>
        public class API : Component
        {
            /// <summary>
            /// Direct commands.
            /// </summary>
            public DirectCommand DirectCommand;

            /// <summary>
            /// System Commands
            /// </summary>
            public SystemCommand SystemCommand;

            /// <summary>
            /// The constructor.
            /// </summary>
            public API(Robot _robot)
                : base(_robot)
            {
                DirectCommand = new NXT.API.DirectCommand(_robot);
                SystemCommand = new NXT.API.SystemCommand(_robot);
            }
        }

        /// <summary>
        /// Direct commands.
        /// </summary>
        public class DirectCommand : Component
        {
            /// <summary>
            /// The constructor.
            /// </summary>
            public DirectCommand(Robot _robot) : base(_robot) { }

            /// <summary>
            /// CheckResponse will require a confirmation from NXT.
            /// It might be slower and return exceptions.
            /// </summary>
            public bool RequestResponse = false;

            /// <summary>
            /// Start the program stored in the NXT.
            /// 
            /// Recognised extensions:
            /// .rxe - user defined programs
            /// .rtm - try me programs
            /// .rfw - firmware
            /// .rxe - user defined programs
            /// .rpg - on-brick programs
            /// .rtm - try-me programs
            /// </summary>
            public void StartProgram(string name)
            {
                byte[] message = new byte[22];
                if (!RequestResponse)
                    message[0] = 0x80;
                message[1] = 0x00;
                System.Text.Encoding.ASCII.GetBytes(name).CopyTo(message, 2);
                robot.Send(message);
            }

            /// <summary>
            /// Stop executing the current program.
            /// </summary>
            public void StopProgram()
            {
                byte[] message = new byte[2];
                if (!RequestResponse)
                    message[0] = 0x80;
                message[1] = 0x01;
                robot.Send(message);
            }

            /// <summary>
            /// Play a specified sound file. The correct extension is .rso.
            /// </summary>
            public void PlaySoundFile(string name, bool loop)
            {
                byte[] message = new byte[23];
                if (!RequestResponse)
                    message[0] = 0x80;
                message[1] = 0x02;
                if (loop)
                    message[2] = 0x01;
                System.Text.Encoding.ASCII.GetBytes(name).CopyTo(message, 3);
                robot.Send(message);
            }

            /// <summary>
            /// Play tone with the specified frequency and duration.
            /// </summary>
            /// <param name="frequency">200-14000 Hz</param>
            /// <param name="duration">in ms</param>
            public void PlayTone(int frequency, int duration)
            {
                byte[] message = new byte[6];
                if (!RequestResponse)
                    message[0] = 0x80;
                message[1] = 0x03;
                message[2] = (byte)frequency;
                message[3] = (byte)(frequency >> 8);
                message[4] = (byte)duration;
                message[5] = (byte)(duration >> 8);
                robot.Send(message);
            }

            /// <summary>
            /// Set output state for a specfic port.
            /// </summary>
            public void SetOutputState(int port, int power_setpoint, OutputMode mode, RegulationMode regulation_mode, int turn_ratio, RunState run_state, int tacho_limit)
            {
                byte[] message = new byte[12];
                if (!RequestResponse)
                    message[0] = 0x80;
                message[1] = 0x04;
                message[2] = (byte)port;
                message[3] = (byte)Convert.ToSByte(power_setpoint);
                message[4] = (byte)mode;
                message[5] = (byte)regulation_mode;
                message[6] = (byte)Convert.ToSByte(turn_ratio);
                message[7] = (byte)run_state;
                message[8] = (byte)tacho_limit;
                message[9] = (byte)(tacho_limit >> 8);
                message[10] = (byte)(tacho_limit >> 16);
                message[11] = (byte)(tacho_limit >> 24);
                robot.Send(message);
            }

            /// <summary>
            /// Set input mode for the specified port.
            /// </summary>
            public void SetInputMode(int port, int sensor_type, int sensor_mode)
            {
                byte[] message = new byte[5];
                if (!RequestResponse)
                    message[0] = 0x80;
                message[1] = 0x05;
                message[2] = (byte)port;
                message[3] = (byte)sensor_type;
                message[4] = (byte)sensor_mode;
                robot.Send(message);
            }

            /// <summary>
            /// Get readings from the specified output port.
            /// </summary>
            public void GetOutputState(int port, out byte[] response)
            {
                byte[] message = new byte[3];
                message[0] = 0x00; //direct command + request a response
                message[1] = 0x06;
                message[2] = (byte)port;
                robot.Send(message, out response);
            }

            /// <summary>
            /// Get readings from the input port.
            /// </summary>
            public void GetInputValues(int port, out byte[] response)
            {
                byte[] message = new byte[3];
                message[0] = 0x00; //direct command + request a response
                message[1] = 0x07;
                message[2] = (byte)port;
                robot.Send(message, out response);
            }

            /// <summary>
            /// Reset the scaled value for the specified input port.
            /// </summary>
            public void ResetInputScaledValue(int port)
            {
                byte[] message = new byte[3];
                if (!RequestResponse)
                    message[0] = 0x80;
                message[1] = 0x08;
                message[2] = (byte)port;
                robot.Send(message);
            }

            /// <summary>
            /// Write a text message to the specified mailbox.
            /// </summary>
            public void MessageWrite(string data, int box)
            {
                byte[] message = new byte[5 + data.Length];
                if (!RequestResponse)
                    message[0] = 0x80;
                message[1] = 0x09;
                message[2] = (byte)box;
                message[3] = (byte)(data.Length + 1);
                System.Text.Encoding.ASCII.GetBytes(data).CopyTo(message, 4);
                robot.Send(message);
            }

            /// <summary>
            /// Reset motor position.
            /// </summary>
            public void ResetMotorPosition(int port, bool relative)
            {
                byte[] message = new byte[4];
                if (!RequestResponse)
                    message[0] = 0x80;
                message[1] = 0x0A;
                message[2] = (byte)port;
                if (relative)
                    message[3] = 0x01;
                robot.Send(message);
            }

            /// <summary>
            /// Get battery level in mV.
            /// </summary>
            public int GetBatteryLevel()
            {
                byte[] message = new byte[2];
                message[0] = 0x00; //direct command + request a response
                message[1] = 0x0B;
                byte[] response;
                robot.Send(message, out response);
                return (response[3] | (response[4] << 8));
            }

            /// <summary>
            /// Stop playing the current sound.
            /// </summary>
            public void StopSoundPlayback()
            {
                byte[] message = new byte[2];
                if (!RequestResponse)
                    message[0] = 0x80;
                message[1] = 0x0C;
                robot.Send(message);
            }

            /// <summary>
            /// Keep alive, return the current time limit in ms.
            /// </summary>
            public int KeepAlive()
            {
                byte[] message = new byte[2];
                message[0] = 0x00; //direct command + request a response
                message[1] = 0x0D;
                byte[] response;
                robot.Send(message, out response);
                return (response[3] | (response[4] << 8) | (response[5] << 16) | (response[6] << 24));
            }

            /// <summary>
            /// Return the available bytes to read
            /// </summary>
            public int LSGetStatus(int port)
            {
                byte[] message = new byte[3];
                message[0] = 0x00; //direct command + request a response
                message[1] = 0x0E;
                message[2] = (byte)port;
                byte[] response;
                robot.Send(message, out response);
                return response[3];
            }

            /// <summary>
            /// LS Write command. tx_data should not be longer than 16 bytes.
            /// </summary>
            public void LSWrite(int port, byte[] tx_data, int rx_data_length)
            {
                byte[] message = new byte[5 + tx_data.Length];
                if (!RequestResponse)
                    message[0] = 0x80;
                message[1] = 0x0F;
                message[2] = (byte)port;
                message[3] = (byte)tx_data.Length;
                message[4] = (byte)rx_data_length;
                tx_data.CopyTo(message, 5);
                robot.Send(message);
            }

            /// <summary>
            /// LS Read command
            /// </summary>
            public void LSRead(int port, out byte[] response)
            {
                byte[] message = new byte[3];
                if (!RequestResponse)
                    message[0] = 0x80;
                message[1] = 0x10;
                message[2] = (byte)port;
                robot.Send(message, out response);
            }

            /// <summary>
            /// Get name of the current program.
            /// </summary>
            public string GetCurrentProgramName()
            {
                byte[] message = new byte[2];
                message[0] = 0x00; //direct command + request a response
                message[1] = 0x11;
                byte[] response;
                robot.Send(message, out response);
                return System.Text.Encoding.ASCII.GetString(response, 3, 20);
            }

            /// <summary>
            /// Read message from the specified inbox.
            /// </summary>
            public string MessageRead(int remote_inbox, int local_inbox, bool remove)
            {
                byte[] message = new byte[5];
                message[0] = 0x00; //direct command + request a response
                message[1] = 0x13;
                message[2] = (byte)remote_inbox;
                message[3] = (byte)local_inbox;
                if (remove)
                    message[4] = 0x01;
                byte[] response;
                robot.Send(message, out response);
                return System.Text.Encoding.ASCII.GetString(response, 5, response[4]);
            }
        }

        /// <summary>
        /// System commands.
        /// </summary>
        public class SystemCommand : Component
        {
            /// <summary>
            /// The constructor.
            /// </summary>
            public SystemCommand(Robot _robot) : base(_robot) { }

            /// <summary>
            /// CheckResponse will require a confirmation from NXT.
            /// It might be slower and return exceptions.
            /// </summary>
            public bool RequestResponse = false;

            /// <summary>
            /// Open file for reading.
            /// 
            /// Call Close when the file is not needed anymore.
            /// </summary>
            public void OpenRead(string name, ref int file_handle, ref int file_size)
            {
                byte[] message = new byte[3 + name.Length];
                message[0] = 0x01; //status command + response
                message[1] = 0x80; //command code
                System.Text.Encoding.ASCII.GetBytes(name).CopyTo(message, 2);
                byte[] response;
                robot.Send(message, out response);
                file_handle = response[3];
                file_size = response[4] | (response[5] << 8) | (response[6] << 16) | (response[7] << 24);
            }

            /// <summary>
            /// Open file for writing.
            /// 
            /// Call Close when the file is not needed anymore.
            /// </summary>
            public void OpenWrite(string name, ref int file_handle, int file_size)
            {
                byte[] message = new byte[26];
                message[0] = 0x01; //status command + response
                message[1] = 0x81; //command code
                System.Text.Encoding.ASCII.GetBytes(name).CopyTo(message, 2);
                message[22] = (byte)file_size;
                message[23] = (byte)(file_size >> 8);
                message[24] = (byte)(file_size >> 16);
                message[25] = (byte)(file_size >> 24);

                byte[] response;
                robot.Send(message, out response);
                file_handle = response[3];
            }

            /// <summary>
            /// Read data from file.
            /// </summary>
            public void Read(int file_handle, int bytes_to_read, out byte[] data)
            {
                byte[] message = new byte[5];
                message[0] = 0x01; //status command + response
                message[1] = 0x82; //command code
                message[2] = (byte)file_handle;
                message[3] = (byte)bytes_to_read;
                message[4] = (byte)(bytes_to_read >> 8);
                byte[] response;
                robot.Send(message, out response);
                int data_length = (response[4] | (response[5] << 8));
                data = new byte[data_length];
                Array.Copy(response, 6, data, 0, data_length);
            }

            /// <summary>
            /// Write data to the specified file.
            /// </summary>
            public void Write(int file_handle, byte[] data)
            {
                byte[] message = new byte[3 + data.Length];
                if (RequestResponse)
                    message[0] = 0x01; //status command + response
                else
                    message[0] = 0x81; //status command
                message[1] = 0x83; //command code
                message[2] = (byte)file_handle;
                data.CopyTo(message, 3);
                byte[] response;
                robot.Send(message, out response);
                if (RequestResponse & ((response[4] | (response[5] << 8)) != data.Length))
                    throw new Exception("NXT.API.SystemCommand, wrong number of written bytes.");
            }

            /// <summary>
            /// Close the specified file.
            /// </summary>
            public void Close(int file_handle)
            {
                byte[] message = new byte[3];
                if (RequestResponse)
                    message[0] = 0x01; //status command + response
                else
                    message[0] = 0x81; //status command
                message[1] = 0x84; //command code
                message[2] = (byte)file_handle;
                robot.Send(message);
            }

            /// <summary>
            /// Delete file.
            /// </summary>
            public void Delete(string name)
            {
                byte[] message = new byte[22];
                if (RequestResponse)
                    message[0] = 0x01; //status command + response
                else
                    message[0] = 0x81; //status command
                message[1] = 0x85; //command code
                System.Text.Encoding.ASCII.GetBytes(name).CopyTo(message, 2);
                robot.Send(message);
            }

            /// <summary>
            /// Search for file
            /// </summary>
            public void FindFirst(string name, ref string file_name, ref int file_handle, ref int file_size)
            {
                byte[] message = new byte[22];
                message[0] = 0x01; //status command + response
                message[1] = 0x86; //command code
                System.Text.Encoding.ASCII.GetBytes(name).CopyTo(message, 2);
                byte[] response;
                robot.Send(message, out response);
                file_handle = response[3];
                file_name = System.Text.Encoding.ASCII.GetString(response, 4, 19);
                file_size = response[24] | (response[25] << 8) | (response[26] << 16) | (response[27] << 24);
            }

            /// <summary>
            /// Search for the next file with file_handle obtained from FindFirst or Open commands.
            /// </summary>
            /// <param name="file_handle"></param>
            /// <param name="response"></param>
            public void FindNext(int file_handle, out byte[] response)
            {
                robot.Send(new byte[] {0x01, 0x87, (byte)file_handle}, out response);
            }

            /// <summary>
            /// Get minor and major versions of firmware and protocol.
            /// </summary>
            public void GetFirmwareVersion(out byte[] response)
            {
                robot.Send(new byte[] { 0x01, 0x88 }, out response);
            }

            /// <summary>
            /// Open file for writing (linear mode).
            /// </summary>
            public void OpenWriteLinear(string name, ref int file_handle, int file_size)
            {
                byte[] message = new byte[26];
                message[0] = 0x01; //status command + response
                message[1] = 0x89; //command code
                System.Text.Encoding.ASCII.GetBytes(name).CopyTo(message, 2);
                message[22] = (byte)file_size;
                message[23] = (byte)(file_size >> 8);
                message[24] = (byte)(file_size >> 16);
                message[25] = (byte)(file_size >> 24);

                byte[] response;
                robot.Send(message, out response);
                file_handle = response[3];
            }

            /// <summary>
            /// Open file for writing (linear mode).
            /// </summary>
            public void OpenReadLinear(string name, ref int memory_pointer)
            {
                byte[] message = new byte[22];
                message[0] = 0x01; //status command + response
                message[1] = 0x8A; //command code
                System.Text.Encoding.ASCII.GetBytes(name).CopyTo(message, 2);

                byte[] response;
                robot.Send(message, out response);
                memory_pointer = response[3] | (response[4] << 8) | (response[5] << 16) | (response[6] << 24);
            }

            /// <summary>
            /// Open file for writing data.
            /// </summary>
            public void OpenWriteData(string name, ref int file_handle, int file_size)
            {
                byte[] message = new byte[26];
                message[0] = 0x01; //status command + response
                message[1] = 0x8B; //command code
                System.Text.Encoding.ASCII.GetBytes(name).CopyTo(message, 2);
                message[22] = (byte)file_size;
                message[23] = (byte)(file_size >> 8);
                message[24] = (byte)(file_size >> 16);
                message[25] = (byte)(file_size >> 24);

                byte[] response;
                robot.Send(message, out response);
                file_handle = response[3];
            }

            /// <summary>
            /// Open file for appending data.
            /// </summary>
            public void OpenAppendData(string name, ref int file_handle, ref int file_size)
            {
                byte[] message = new byte[22];
                message[0] = 0x01; //status command + response
                message[1] = 0x8C; //command code
                System.Text.Encoding.ASCII.GetBytes(name).CopyTo(message, 2);

                byte[] response;
                robot.Send(message, out response);
                file_handle = response[3];
                file_size = response[4] | (response[5] << 8) | (response[6] << 16) | (response[7] << 24);
            }

            /// <summary>
            /// Boot the brick. USB command only.
            /// </summary>
            public void Boot()
            {
                string boot_command = "Let's dance: SAMBA";
                byte[] message = new byte[21];
                message[0] = 0x01; //status command + response
                message[1] = 0x8C; //command code
                System.Text.Encoding.ASCII.GetBytes(boot_command).CopyTo(message, 2);
                byte[] response;
                robot.Send(message, out response);
 
            }

            /// <summary>
            /// Set brick name.
            /// </summary>
            public void SetBrickName(string name)
            {
                byte[] message = new byte[18];
                if (RequestResponse)
                    message[0] = 0x01; //status command + response
                else
                    message[0] = 0x81; //status command
                message[1] = 0x98;
                System.Text.Encoding.ASCII.GetBytes(name).CopyTo(message, 2);
                robot.Send(message);
            }
            
            /// <summary>
            /// Get device information in a raw format.
            /// </summary>
            public void GetDeviceInfo(out byte[] response)
            {
                robot.Send(new byte[] {0x01, 0x9B}, out response);
            }

            /// <summary>
            /// Delete user flash memory.
            /// </summary>
            public void DeleteUserFlash()
            {
                byte[] response;
                robot.Send(new byte[] { 0x01, 0xA0 }, out response);
            }

            /// <summary>
            /// Number of bytes for the command ready in the buffer.
            /// </summary>
            public void PollLength(int buffer_type, ref int buffer_size)
            {
                byte[] response;
                robot.Send(new byte[] { 0x01, 0xA1, (byte)buffer_type }, out response);
                buffer_size = response[4];
            }

            /// <summary>
            /// Poll the command.
            /// </summary>
            public void Poll(int buffer_type, int command_length, out byte[] response)
            {
                robot.Send(new byte[] { 0x01, 0xA2, (byte)buffer_type, (byte)command_length }, out response);
            }

            /// <summary>
            /// Reset Bluetooth. USB command only.
            /// </summary>
            public void BluetoothFactoryReset()
            {
                byte[] message = new byte[2];
                if (RequestResponse)
                    message[0] = 0x01; //status command + response
                else
                    message[0] = 0x81; //status command
                message[1] = 0x98;
                robot.Send(message);
            }
        }
    }

    /// <summary>
    /// Input port.
    /// </summary>
    public class InPort : Component
    {
        SensorType type = SensorType.NoSensor;
        SensorMode mode = SensorMode.RawMode;
        byte[] state = new byte[8];

        /// <summary>
        /// Port number.
        /// </summary>
        public int Number = 0;

        /// <summary>
        /// The constructor.
        /// </summary>
        public InPort(Robot _robot) : base(_robot) { }

        /// <summary>
        /// Sensor mode.
        /// </summary>
        public SensorMode SensorMode
        {
            set { mode = value; robot.API.DirectCommand.SetInputMode(Number, (int)type, (int)mode); }
            get { return mode; }
        }

        /// <summary>
        /// Sensor type.
        /// </summary>
        public SensorType SensorType
        {
            set { type = value; robot.API.DirectCommand.SetInputMode(Number, (int)type, (int)mode); }
            get { return type; }
        }

        /// <summary>
        /// Update the sensor state.
        /// </summary>
        public void Update()
        {
            if (type == SensorType.Sonar)
            {
                robot.API.DirectCommand.LSWrite(Number, new byte[] { 0x02, 0x42 }, 1);
                byte[] response;
                robot.API.DirectCommand.LSRead(Number, out response);
                int value = (int)response[4] * 10;
                state[0] = (byte)value;
                state[1] = (byte)(value >> 8);
                state[2] = state[0];
                state[3] = state[1];
                state[4] = state[0];
                state[5] = state[1];
                state[6] = state[0];
                state[7] = state[1];
            }
            else
            {
                byte[] response;
                robot.API.DirectCommand.GetInputValues(Number, out response);
                Array.Copy(response, 8, state, 0, 8);
            }
        }

        /// <summary>
        /// Raw reading value.
        /// </summary>
        public int RawADValue
        {
            get { if (AutoUpdate) Update(); return (int)((state[1] << 8) + state[0]); }
        }

        /// <summary>
        /// Scaled reading value.
        /// </summary>
        public int ScaledADValue
        {
            get { if (AutoUpdate) Update(); return (int)((state[3] << 8) + state[2]); }
        }

        /// <summary>
        /// Scaled sensor value.
        /// </summary>
        public short ScaledValue
        {
            get { if (AutoUpdate) Update(); return BitConverter.ToInt16(new byte[] { state[5], state[4] }, 0); }
        }

        /// <summary>
        /// Calibrated value.
        /// </summary>
        public short CalibratedValue
        {
            get { if (AutoUpdate) Update(); return BitConverter.ToInt16(new byte[] { state[7], state[6] }, 0); }
        }

        /// <summary>
        /// Reset the scaled value.
        /// </summary>
        public void ResetScaledValue()
        {
            robot.API.DirectCommand.ResetInputScaledValue(Number);
        }
    }

    /// <summary>
    /// Output port.
    /// </summary>
    public class OutPort : Component
    {
        /// <summary>
        /// Port number.
        /// </summary>
        public int Number = 0;

        int power_setpoint = 0;
        OutputMode mode = OutputMode.MotorOn;
        NXT.RegulationMode regulation_mode = RegulationMode.MotorSpeed;
        int turn_ratio = 0;
        RunState run_state = NXT.RunState.Running;
        int tacho_limit = 0;
        byte[] state = new byte[12];

        /// <summary>
        /// The constructor.
        /// </summary>
        public OutPort(Robot _robot) : base(_robot) { }

        /// <summary>
        /// Update the output port state.
        /// </summary>
        public void Update()
        {
            byte[] response;
            robot.API.DirectCommand.GetOutputState(Number, out response);
            Array.Copy(response, 13, state, 0, 12);
        }

        /// <summary>
        /// Number of counts since the last reset of the motor counter.
        /// </summary>
        public int TachoCount
        {
            get { if (AutoUpdate) Update(); return (state[0] | (state[1] << 8) | (state[2] << 16) | (state[3] << 24)); }
        }

        /// <summary>
        /// Current position relative to last programmed movement
        /// </summary>
        public int BlockTachoCount
        {
            get { if (AutoUpdate) Update(); return (state[4] | (state[5] << 8) | (state[6] << 16) | (state[7] << 24)); }
        }

        /// <summary>
        /// Current position relative to last reset of the rotation sensor.
        /// </summary>
        public int RotationCount
        {
            get { if (AutoUpdate) Update(); return (state[8] | (state[9] << 8) | (state[10] << 16) | (state[11] << 24)); }
        }

        /// <summary>
        /// Power setpoint.
        /// </summary>
        public int PowerSetpoint
        {
            get { return power_setpoint; }
            set { power_setpoint = value; robot.API.DirectCommand.SetOutputState(Number, power_setpoint, mode, regulation_mode, turn_ratio, run_state, tacho_limit); }
        }

        /// <summary>
        /// Output mode.
        /// </summary>
        public OutputMode Mode
        {
            get { return mode; }
            set { mode = value; robot.API.DirectCommand.SetOutputState(Number, power_setpoint, mode, regulation_mode, turn_ratio, run_state, tacho_limit); }
        }

        /// <summary>
        /// Regulation mode.
        /// </summary>
        public NXT.RegulationMode RegulationMode
        {
            get { return regulation_mode; }
            set { regulation_mode = value; robot.API.DirectCommand.SetOutputState(Number, power_setpoint, mode, regulation_mode, turn_ratio, run_state, tacho_limit); }
        }

        /// <summary>
        /// Turn ratio.
        /// </summary>
        public int TurnRatio
        {
            get { return turn_ratio; }
            set { turn_ratio = value; robot.API.DirectCommand.SetOutputState(Number, power_setpoint, mode, regulation_mode, turn_ratio, run_state, tacho_limit); }
        }

        /// <summary>
        /// Run state.
        /// </summary>
        public NXT.RunState RunState
        {
            get { return run_state; }
            set { run_state = value; robot.API.DirectCommand.SetOutputState(Number, power_setpoint, mode, regulation_mode, turn_ratio, run_state, tacho_limit); }
        }

        /// <summary>
        /// Tacho limit.
        /// </summary>
        public int TachoLimit
        {
            get { return tacho_limit; }
            set { tacho_limit = value; robot.API.DirectCommand.SetOutputState(Number, power_setpoint, mode, regulation_mode, turn_ratio, run_state, tacho_limit); }
        }

        /// <summary>
        /// Reset motor position (absolute/relative)
        /// </summary>
        public void ResetMotorPosition(bool relative)
        {
            robot.API.DirectCommand.ResetMotorPosition(Number, relative);
        }
    }

    /// <summary>
    /// Device information.
    /// </summary>
    public class DeviceInfo : Component
    {
        byte[] state = new byte[30];

        /// <summary>
        /// The constructor.
        /// </summary>
        public DeviceInfo(Robot _robot) : base(_robot) { }

        /// <summary>
        /// Update the device information state.
        /// </summary>
        public void Update()
        {
            byte[] response;
            robot.API.SystemCommand.GetDeviceInfo(out response);
            Array.Copy(response, 3, state, 0, 30);
        }

        /// <summary>
        /// Device name
        /// </summary>
        public string Name
        {
            get
            {
                if (AutoUpdate) Update();
                return System.Text.Encoding.ASCII.GetString(state, 0, 15);
            }
            set { robot.API.SystemCommand.SetBrickName(value); }
        }

        /// <summary>
        /// Bluetooth address.
        /// </summary>
        public string BTAddress
        {
            get
            {
                if (AutoUpdate) Update();
                return String.Format("{0:x2}:{1:x2}:{2:x2}:{3:x2}:{4:x2}:{5:x2}:{6:x2}", state[15], state[16], state[17], state[18], state[19], state[20], state[21]);
            }
        }

        /// <summary>
        /// Bluetooth signal strength.
        /// </summary>
        public int BTSignalStrength
        {
            get
            {
                if (AutoUpdate) Update();
                return (int)(state[22] + (state[23] << 8) + (state[24] << 16) + (state[25] << 24));
            }
        }

        /// <summary>
        /// Amount of free flash memory.
        /// </summary>
        public int FreeUserFlash
        {
            get
            {
                if (AutoUpdate) Update();
                return (int)(state[25] + (state[26] << 8) + (state[27] << 16) + (state[28] << 24));
            }
        }

        /// <summary>
        /// Battery level in mv.
        /// </summary>
        public int BatteryLevel
        {
            get { return robot.API.DirectCommand.GetBatteryLevel(); }
        }

        /// <summary>
        /// Protocol version.
        /// </summary>
        public string ProtocolVersion
        {
            get
            {
                byte[] response;
                robot.API.SystemCommand.GetFirmwareVersion(out response);
                return String.Format("{0}.{1}", response[4], response[3]);
            }
        }

        /// <summary>
        /// Firmware version.
        /// </summary>
        public string FrimwareVersion
        {
            get
            {
                byte[] response;
                robot.API.SystemCommand.GetFirmwareVersion(out response);
                return String.Format("{0}.{1}", response[6], response[5]);
            }
        }
    }

    /// <summary>
    /// Sonar registers.
    /// </summary>
    public enum SonarRegister : byte
    {
        MeasurementUnits = 0x14,
        PollInterval = 0x40,
        Mode = 0x41,
        MeasurementByte0 = 0x42,
        MeasurementByte1 = 0x43,
        MeasurementByte2 = 0x44,
        MeasurementByte3 = 0x45,
        MeasurementByte4 = 0x46,
        MeasurementByte5 = 0x47,
        MeasurementByte6 = 0x48
    }

    /// <summary>
    /// Sensor types.
    /// </summary>
    public enum SensorType : byte
    {
        NoSensor = 0x00,
        Switch = 0x01,
        Temperature = 0x02,
        Reflection = 0x03,
        Angle = 0x04,
        LightActive = 0x05,
        LightInactive = 0x06,
        SoundDB = 0x07,
        SoundDBA = 0x08,
        Custom = 0x09,
        LowSpeed = 0x0A,
        LowSpeed9V = 0x0B,
        Sonar = 0x0C,   //additional sensor type for easier setup
        NoOfSensorTypes = 0x0D
    }

    /// <summary>
    /// Sensor mode.
    /// </summary>
    public enum SensorMode : byte
    {
        RawMode = 0x00,
        BooleanMode = 0x20,
        TransitionCntMode = 0x40,
        PeriodCounterMode = 0x60,
        PctFullScaleMode = 0x80,
        CelsiusMode = 0xA0,
        FahrenheitMode = 0xC0,
        AngleStepsMode = 0xE0,
        SlopeMask = 0x1F,
        ModeMask = 0xE0
    }

    /// <summary>
    /// Output mode.
    /// </summary>
    public enum OutputMode : byte
    { 
        MotorOn = 0x01,     //turn on the motor
        Brake = 0x02,       //user run/break instead of run/float in PWM
        MotorOn_Break = 0x03,
        Regulated = 0x04,
        MotorOn_Regulated = 0x05,
        Break_Regulated = 0x06,
        MotorOn_Break_Regulated = 0x07
    }

    /// <summary>
    /// Regulation mode. MotorSync enables synchronisation between several outputs: has to be set on all synchronised outputs.
    /// </summary>
    public enum RegulationMode : byte
    {
        Idle = 0x00,
        MotorSpeed = 0x01,
        MotorSync = 0x02
    }

    /// <summary>
    /// Output running state.
    /// </summary>
    public enum RunState : byte 
    {
        Idle = 0x00,
        RumpUp = 0x10,
        Running = 0x20,
        RumpDown = 0x40
    }
}
