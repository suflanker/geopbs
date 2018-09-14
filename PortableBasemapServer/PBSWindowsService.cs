using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using PBS.APP.ViewModels;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Threading;
using System.Runtime.InteropServices;
using PBS.Util;

namespace PBS.APP
{
    partial class PBSWindowsService : ServiceBase
    {
        public VMMainView VM = null;
        public PBSWindowsService()
        {
            //InitializeComponent();
            this.ServiceName = "PBSWindowsService";
            this.CanShutdown = true;
            this.CanStop = true;
            this.CanPauseAndContinue = false;
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                VM = new VMMainView();
            }
            catch (Exception e)
            {
                Utility.Log(LogLevel.Error,e, Application.Current.FindResource("WindowsServiceStartError").ToString());
            }
        }
        protected override void OnStop()
        {
            try
            {
                if (VM != null)
                    VM.DoCleanJobs();
                VM = null;
                Application.Current.Shutdown(0);//exe process does not terminate immediately, but after a while when the service successfully stopped.
            }
            catch (Exception e)
            {
                Utility.Log(LogLevel.Error,e,Application.Current.FindResource("WindowsServiceStopError").ToString());
            }
        }
    }
}
