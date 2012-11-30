using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NXT_Example
{
    class Program
    {
        static void Main(string[] args)
        {
            NXT.Robot robot = new NXT.Robot();

            try { robot.AutoConnect(); }
            catch (Exception exc) { Console.WriteLine(exc.Message); return; }

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
