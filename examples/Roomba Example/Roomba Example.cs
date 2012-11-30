using System;
using System.Threading;

namespace Roomba_Example
{
    class Program
    {
        static void Main(string[] args)
        {
            Roomba.Robot robot = new Roomba.Robot();

            try { robot.AutoConnect(); }
            catch (Exception exc) { Console.WriteLine(exc.Message); return; }

            /*
            //example usage
            robot.SCI.Full();
            robot.SCI.Drive(100,32768);
            robot.Leds.Status = Roomba.SCI.StatusLED.STATUSLED_AMBER;

            robot.Sensors.Update(Roomba.SCI.SensorPacket.ALL_SENSORS);
            Console.WriteLine("Left bumper state = {0} ", robot.Sensors.Bumps.Left);
            */

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
