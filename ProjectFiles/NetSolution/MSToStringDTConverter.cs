#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.UI;
using FTOptix.NativeUI;
using FTOptix.HMIProject;
using FTOptix.NetLogic;
using FTOptix.CoreBase;
using FTOptix.RAEtherNetIP;
using FTOptix.WebUI;
using FTOptix.Alarm;
using FTOptix.EventLogger;
using FTOptix.SQLiteStore;
using FTOptix.Store;
using FTOptix.System;
using FTOptix.Retentivity;
using FTOptix.Report;
using FTOptix.CommunicationDriver;
using FTOptix.SerialPort;
using FTOptix.Core;
#endregion

public class MSToStringDTConverter : BaseNetLogic
{
    private PeriodicTask updateLogs;
    private IUAVariable milliSecondsVar;
    private IUAVariable dateTimeStringVar;
    private int lastMs = -1; // Track last value to skip unnecessary updates

    public override void Start()
    {
        // Cache variable references once at startup
        milliSecondsVar = LogicObject.GetVariable("MilliSeconds");
        dateTimeStringVar = LogicObject.GetVariable("DateTimeString");
        
        updateLogs = new PeriodicTask(IncrementalVariable, 250, LogicObject);
        updateLogs.Start();
    }

    public override void Stop()
    {
        updateLogs?.Dispose();
        updateLogs = null;
        
        // Clear cached references
        milliSecondsVar = null;
        dateTimeStringVar = null;
        lastMs = -1;
    }
    
    private void IncrementalVariable()
    {
        // Use cached variable reference
        int ms = milliSecondsVar.Value;
        
        // Skip update if value hasn't changed (optimization)
        if (ms == lastMs)
            return;
        
        lastMs = ms;

        // Convert milliseconds to TimeSpan
        TimeSpan timeSpan = TimeSpan.FromMilliseconds(ms);

        // Initialize formatted time string
        string mstoString;

        // Check if days should be displayed
        if (timeSpan.Days > 0)
        {
            string dayLabel = timeSpan.Days == 1 ? "Day" : "Days";
            mstoString = string.Format("{0} {1}, {2:D2}:{3:D2}:{4:D2}",
                                       timeSpan.Days,
                                       dayLabel,
                                       timeSpan.Hours,
                                       timeSpan.Minutes,
                                       timeSpan.Seconds);
        }
        else
        {
            mstoString = string.Format("{0:D2}:{1:D2}:{2:D2}",
                                       timeSpan.Hours,
                                       timeSpan.Minutes,
                                       timeSpan.Seconds);
        }

        // Use cached variable reference
        dateTimeStringVar.Value = mstoString;
    }



}
