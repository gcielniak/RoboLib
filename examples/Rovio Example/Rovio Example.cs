using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Rovio_Example
{
    class Program
    {
        static void Main(string[] args)
        {
            Rovio.Robot robot = new Rovio.Robot("http://ip_address/", "user", "password");

            //check if we can receive responses from the robot
            try { robot.API.Movement.GetLibNSVersion(); } // a dummy request
            catch (Exception) { Console.WriteLine("Could not connect to the robot"); return; }

            //move the robot forward

            //using the CGI commands
            //robot.Request("rev.cgi?Cmd=nav&action=18&drive=1&speed=5");

            //using the wrapper library - API commands
            //robot.API.Movement.ManualDrive.Forward(5);

            //using the convenience class
            //robot.Drive.Forward(5);

            //main loop
            while (true)
            {
                if (Console.KeyAvailable)
                {
                    ConsoleKey key = Console.ReadKey(true).Key;

                    //loop until the ESC key is pressed
                    if (key == ConsoleKey.Escape) break;

                    //custom keyboard handler
                    switch (key)
                    {
                        case ConsoleKey.Q:
                            //enter your code here
                            break;
                        default:
                            break;
                    }
                }
            }
        }
    }
}
