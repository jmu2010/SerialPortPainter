using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;  // 串口库

namespace WinFormNewApp
{
    class ClassMySerial
    {
        public string[] ports;

        public void find_available_ports()
        {
            ports = SerialPort.GetPortNames();

            foreach (string port in ports)
            {
                Console.WriteLine(port);
            }
            //Console.WriteLine("Find finish.");
        }

    }
}
