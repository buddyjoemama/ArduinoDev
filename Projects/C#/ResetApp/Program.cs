using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;

namespace ResetToBootloader
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("No serial port defined. Returning.");
                return;
            }

            Console.WriteLine("Resetting Arduino. Using port {0}...", args[0]);

            try
            {
                SerialPort p = new SerialPort(args[0], 1200);
                p.Open();
                p.Close();
            }
            catch { }

            Thread.Sleep(5500);
            Console.WriteLine("Arduino reset into bootloader!");

        }
    }
}
