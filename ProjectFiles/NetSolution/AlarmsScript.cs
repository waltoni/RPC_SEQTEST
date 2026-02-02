#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.HMIProject;
using FTOptix.CoreBase;
using FTOptix.UI;
using FTOptix.NativeUI;
using FTOptix.NetLogic;
using FTOptix.Recipe;
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
using System.Reflection;
using System.Text.RegularExpressions;
#endregion

public class AlarmsScript : BaseNetLogic
{
    [ExportMethod]
    public void Propagate_Alarm_Names()
    {
        var proj = Project.Current;

        // Iterate through Array0, Array1, Array2 folders
        for (int n = 0; n <= 2; n++)
        {
            // Get the folders for this iteration
            Folder instanceFolder = proj.Get<Folder>($"Data/Alarms_Instance/MajorAlarms/Array{n}");
            Folder goToFolder = proj.Get<Folder>($"UI/Templates/Alarm/AlarmGoToButtons/Array{n}");
            Folder alarmPreviewFolder = proj.Get<Folder>($"UI/Templates/Alarm/AlarmPreviews/Array{n}");
            Folder popupsFolder = proj.Get<Folder>($"UI/PopUps/AlarmDetails/Array{n}");

            // Process the children in each folder
            ProcessAlarmInstances(instanceFolder, goToFolder, alarmPreviewFolder, popupsFolder);
        }
    }

    private void ProcessAlarmInstances(Folder instanceFolder, Folder goToFolder, Folder alarmPreviewFolder, Folder popupsFolder)
    {
        var instanceFolderChildren = instanceFolder.Children;
        var goToFolderChildren = goToFolder.Children;
        var alarmPreviewChildren = alarmPreviewFolder.Children;
        var popupsChildren = popupsFolder.Children;

        if (instanceFolderChildren != null && goToFolderChildren != null)
        {
            foreach (DigitalAlarm alarmChild in instanceFolderChildren)
            {
                var alarmMessage = alarmChild.Message;
                if (!string.IsNullOrEmpty(alarmMessage))
                {
                    LocalizedText displayValue = new LocalizedText(alarmMessage, "en-US");
                    if (displayValue != null && displayValue != alarmChild.DisplayName)
                    {
                        alarmChild.DisplayName = displayValue;
                        Log.Info($"Updated message instance display name: {alarmChild.DisplayName.Text}");
                    }

                    string alarmIndex = alarmChild.GetVariable("Index").Value;
                    string alarmArrayIndex = alarmChild.GetVariable("ArrayIndex").Value;

                    // Create composite key for goToChild
                    string goToCompositeKey = $"{alarmArrayIndex}_{alarmIndex}";

                    // Update GoTo Template
                    foreach (var goToChild in goToFolderChildren)
                    {
                        var goToIndex = goToChild.GetVariable("Index").Value;
                        if (alarmIndex == goToIndex)
                        {
                            string validIdentifier = GetValidIdentifier(alarmMessage);
                            UpdateChildBrowseNameAndInstances(goToChild, validIdentifier, "ALM", goToCompositeKey);

                        }
                    }

                    // Update Alarm Preview Template
                    foreach (var alarmPreviewChild in alarmPreviewChildren)
                    {
                        var alarmPreviewIndex = alarmPreviewChild.GetVariable("Index").Value;
                        if (alarmIndex == alarmPreviewIndex)
                        {
                            string validIdentifier = GetValidIdentifier(alarmMessage);
                            UpdateChildBrowseNameAndInstances(alarmPreviewChild, validIdentifier, "Preview", goToCompositeKey);

                        }
                    }
                    // Update Alarm Popups
                    foreach (var popupsChild in popupsChildren)
                    {
                        var popupIndex = popupsChild.GetVariable("Index").Value;
                        if (alarmIndex == popupIndex)
                        {
                            var a = popupsChild.GetType();
                            string validIdentifier = GetValidIdentifier(alarmMessage);

                            if (displayValue != null)
                            {
                                if (displayValue != popupsChild.DisplayName)
                                {
                                    popupsChild.DisplayName = displayValue;
                                    Log.Info($"Updated PopUp display name: {popupsChild.DisplayName.Text}");
                                }
                            }
                        }
                    }
                }
            }
        }
    }


    private void UpdateChildBrowseNameAndInstances(dynamic child, string validIdentifier, string suffix, string goToCompositeKey)
    {
        if (child.BrowseName != $"{validIdentifier}_{goToCompositeKey}_{suffix}")
        {
            if (validIdentifier.ToLower() != "reserved" && validIdentifier.ToLower() != "spare")
            {
                // Update for normal cases
                child.BrowseName = $"{validIdentifier}_{goToCompositeKey}_{suffix}";
                Log.Info($"Updated {suffix} Template to name: {child.BrowseName}");
            }
            else
            {
                // Update for reserved/spare cases
                child.BrowseName = $"{validIdentifier}_{goToCompositeKey}_{suffix}";
                Log.Info($"Updated {suffix} Template to name: {child.BrowseName}");
            }
        }

        // Update instance collection
        dynamic childType = child.GetType();
        PropertyInfo[] properties = childType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        foreach (var prop in properties)
        {
            var value = prop.GetValue(child, null);
            if (value is UAManagedCore.InstanceNodeCollection<FTOptix.UI.BaseUIObject> instanceCollection)
            {
                if (instanceCollection.Count == 0) return;
                int instanceIndex = 0;

                foreach (var instance in instanceCollection)
                {
                    if (instance is FTOptix.UI.Button buttonInstance)
                    {
                        if (buttonInstance != null && ($"{validIdentifier}_{suffix}{instanceIndex}") != buttonInstance.BrowseName)
                        {
                            buttonInstance.BrowseName = $"{validIdentifier}_{suffix}{instanceIndex}";
                            Log.Info($"The Instance: {buttonInstance.BrowseName} has been renamed");
                        }
                    }
                    if (instance is FTOptix.UI.Rectangle rectangleInstance)
                    {
                        if (rectangleInstance != null && ($"{validIdentifier}_{suffix}{instanceIndex}") != rectangleInstance.BrowseName)
                        {
                            rectangleInstance.BrowseName = $"{validIdentifier}_{suffix}{instanceIndex}";
                            instanceIndex += 1;
                            Log.Info($"The Instance: {rectangleInstance.BrowseName} has been renamed");
                        }
                    }
                }
                break;
            }
        }
    }

    private string GetValidIdentifier(string alarmMessage)
    {
        int indexOfParent = alarmMessage.IndexOf("(");
        string newMessage = indexOfParent != -1 ? alarmMessage.Substring(0, indexOfParent).Trim() : alarmMessage;
        string validIdentifier = Regex.Replace(newMessage, @"[^a-zA-Z0-9_]", "_");

        if (char.IsDigit(validIdentifier[0]))
        {
            validIdentifier = "_" + validIdentifier;
        }

        return validIdentifier;
    }
}
