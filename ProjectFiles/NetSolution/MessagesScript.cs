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

public class MessagesScript : BaseNetLogic
{
    [ExportMethod]
    public void Propagate_Message_Names()
    {
        var proj = Project.Current;
        Folder instanceFolder = proj.Get<Folder>("Data/Messages_Instance/Messages");
        Folder goToFolder = proj.Get<Folder>("UI/Templates/Message_Icons");

        // Iterate through Array0 and Array1 folders for both Messages and Message_Icons
        for (int n = 0; n <= 1; n++)
        {
            var instanceArrayFolder = instanceFolder.Get<Folder>($"Array{n}");
            var goToArrayFolder = goToFolder.Get<Folder>($"Array{n}");

            var instanceFolderChildren = instanceArrayFolder.Children;
            var goToFolderChildren = goToArrayFolder.Children;

            if (instanceFolderChildren != null && goToFolderChildren != null)
            {
                foreach (DigitalAlarm messageChild in instanceFolderChildren)
                {
                    if (messageChild != null)
                    {
                        // Get ArrayIndex and Index for messageChild
                        string messageIndex = messageChild.GetVariable("Index").Value;
                        string messageArrayIndex = messageChild.GetVariable("ArrayIndex").Value;

                        // Create composite key for messageChild
                        string messageCompositeKey = $"{messageArrayIndex}_{messageIndex}";

                        foreach (var goToChild in goToFolderChildren)
                        {
                            // Get ArrayIndex and Index for goToChild
                            string goToIndex = goToChild.GetVariable("Index").Value;
                            string goToArrayIndex = goToChild.GetVariable("ArrayIndex").Value;

                            // Create composite key for goToChild
                            string goToCompositeKey = $"{goToArrayIndex}_{goToIndex}";

                            // Match on composite keys
                            if (messageCompositeKey == goToCompositeKey)
                            {
                                var messageMessage = messageChild.Message;

                                if (!string.IsNullOrEmpty(messageMessage))
                                {
                                    LocalizedText displayValue = new LocalizedText(messageMessage, "en-US");
                                    if (displayValue != null && displayValue != messageChild.DisplayName)
                                    {
                                        messageChild.DisplayName = displayValue;
                                        Log.Info($"Updated message instance display name: {messageChild.DisplayName.Text}");
                                    }

                                    string validIdentifier = GetValidIdentifier(messageMessage);

                                    // Update the goToChild's BrowseName and its instances using the composite key
                                    UpdateChildBrowseNameAndInstances(goToChild, validIdentifier, "MSG", messageCompositeKey);
                                    break; // Break out after matching composite key
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private void UpdateChildBrowseNameAndInstances(dynamic child, string validIdentifier, string suffix, string compositeKey)
    {
        if (child.BrowseName != $"{validIdentifier}_{compositeKey}_{suffix}")
        {
            if (validIdentifier.ToLower() != "reserved" && validIdentifier.ToLower() != "spare")
            {
                // Update for normal cases
                child.BrowseName = $"{validIdentifier}_{compositeKey}_{suffix}";
                Log.Info($"Updated {suffix} Template to name: {child.BrowseName}");
            }
            else
            {
                // Update for reserved/spare cases with composite key
                child.BrowseName = $"{validIdentifier}_{compositeKey}_{suffix}";
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
                    // Check if the instance is a Button before casting
                    if (instance is FTOptix.UI.Button buttonInstance && ($"{validIdentifier}_{suffix}{instanceIndex}") != buttonInstance.BrowseName)
                    {
                        buttonInstance.BrowseName = $"{validIdentifier}_{suffix}{instanceIndex}";
                        instanceIndex += 1;
                        Log.Info($"The Instance: {buttonInstance.BrowseName} has been renamed");
                    }
                    if (instance is FTOptix.UI.Rectangle rectangleInstance && ($"{validIdentifier}_{suffix}{instanceIndex}") != rectangleInstance.BrowseName)
                    {
                        rectangleInstance.BrowseName = $"{validIdentifier}_{suffix}{instanceIndex}";
                        instanceIndex += 1;
                        Log.Info($"The Instance: {rectangleInstance.BrowseName} has been renamed");
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

