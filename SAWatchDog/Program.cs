using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading;

namespace SAWatchDog
{
    class Program
    {
        /// <summary>
        /// Monitors the SpamAssassin Daemon and restarts it if the process ends.
        /// </summary>
        static void Main()
        {
            //Run the service method.
            ServiceBase.Run(new SAWatchDog());
        }

        public class SAWatchDog : ServiceBase
        {
            private ManualResetEvent shutDownEvent = new ManualResetEvent(false);
            private Thread thread;

            //Service Constructor
            public SAWatchDog()
            {
                this.ServiceName = "SAWatchDog";
                this.CanStop = true;
                this.CanPauseAndContinue = false;
                this.AutoLog = true;
            }

            /// <summary>
            /// OnStart service
            /// </summary>
            /// <param name="args">Not Used</param>
            protected override void OnStart(string[] args)
            {
                thread = new Thread(ProcessMonitor);
                thread.Name = "Process Monitor";
                thread.IsBackground = true;
                thread.Start();
            }

            /// <summary>
            /// OnStop service
            /// </summary>
            protected override void OnStop()
            {
                var programPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                var programFolder = "\\hMailServer\\SpamAssassin\\";

                //Process the threadshutdown and wait 10 seconds before forcing it to abort.
                shutDownEvent.Set();
                if (!thread.Join(10000))
                {
                    thread.Abort();
                }

                //Kill any running SpamAssassin Daemons
                Process[] processes = Process.GetProcessesByName("spamd");
                foreach (var process in processes)
                {
                    process.Kill();
                }

                //Rotate log file on shutdown.
                File.Move((programPath + programFolder + "Logs\\SpamD-Current.log"), (programPath + programFolder + "Logs\\SpamD-" + DateTime.Now.ToString("yyyy-M-dd-HH-mm-ss") + ".log"));
            }

            /// <summary>
            /// Monitors the SpamAssassin Daemon and restarts the process if it is not running.
            /// </summary>
            private void ProcessMonitor()
            {
                while (!shutDownEvent.WaitOne(0))
                {
                    var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                    var programFolder = "\\hMailServer\\SpamAssassin\\";
                    
                    ProcessStartInfo startInfo = new ProcessStartInfo();
                    startInfo.Arguments = "-s \"" + programFiles + programFolder + "Logs\\SpamD-Current.log\"";
                    startInfo.WorkingDirectory = programFiles + programFolder;
                    startInfo.FileName = "spamd.exe";
                    startInfo.ErrorDialog = false;

                    var process = Process.Start(startInfo);

                    EventLog.WriteEntry("SpamAssassin Process Monitor", "The SpamAssassin Process Monitor has started.", EventLogEntryType.Information, 10000);

                    while (IsRunning(process))
                    {
                        //Wait one minute
                        Thread.Sleep(60000);
                    }
                }
            }

            /// <summary>
            /// Checks to see if the original process is running.
            /// </summary>
            /// <param name="process">A running process object.</param>
            /// <returns>True if process is running. False if not.</returns>
            public static bool IsRunning(Process process)
            {
                try
                {
                    Process.GetProcessById(process.Id);
                }
                catch (ArgumentException)
                {
                    EventLog.WriteEntry("SpamAssassin Process Monitor", "The SpamAssassin Daemon is not running. It will be restarted soon.", EventLogEntryType.Warning, 10001);
                    return false;
                }
                return true;
            }
        }
    }
}
