
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Focus_Bear.Utils
{
    public class LogWriter
    {
        public static void LogWrite(string logMessage)
        {
            Console.WriteLine(logMessage);
 
            try
            {
                var path = Path.Combine(AppSettings.AppDataDirLogsPath, "Debug_Logs.txt");
                
                using (StreamWriter w = File.AppendText(path))
                {
                    Log(logMessage, w);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
 
        }

        private static void Log(string logMessage, TextWriter txtWriter)
        {
            try
            {
                txtWriter.WriteLine("{0} - {1}", DateTime.Now.ToString(), logMessage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }
    }
}
