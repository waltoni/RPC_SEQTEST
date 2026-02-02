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
using System.Text.RegularExpressions;
using System.Reflection;
using System.Collections.Generic;
#endregion

public class SensorsScript : BaseNetLogic
{
    [ExportMethod]
    public void CreateSensors()
    {
        // Get a reference to this project. This will simplify further code.
        var proj = Project.Current;


        // Get the folder to hold the sensors.
        Folder folder = proj.Get<Folder>("UI/Templates/Sensors");

        // Checks if there are instances in the project. If there are, then we do not delete and store to make sure we dont readd
        var folderChildren = folder.Children;
        List<string> templatesWithInstances = new List<string>();
        if (folderChildren != null)
        {
            foreach (var child in folderChildren)
            {
                // check to see if there are references in the Machine Options folder 
                // Check if the child is not a folder before deleting it

                var childType = child.GetType();
                PropertyInfo[] properties = childType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                bool instanceFound = false;
                foreach (var prop in properties)
                {
                    // Get the value of the property
                    var value = prop.GetValue(child, null);

                    // Check if the value is an instance of InstanceNodeCollection
                    if (value is UAManagedCore.InstanceNodeCollection<FTOptix.UI.BaseUIObject> instanceCollection)
                    {
                        // Check if the collection has a 'Count' property

                        if (instanceCollection.Count == 0 && !instanceFound)
                        {
                            child.Delete();
                            Log.Info(child.BrowseName.ToString() + ": Deleted");
                            break;
                        }
                        else
                        {
                            instanceFound = true;
                            Log.Info($"The template: {child.BrowseName} has: {instanceCollection.Count} instance(s) and cannot be deleted");
                            templatesWithInstances.Add(child.BrowseName);
                            break;
                        }
                    }
                }
            }
        }

        // Get the sensor type.
        var sensorType = proj.Get("UI/Templates/Global Templates/Sensors_Global/Sensor");

        // Get the Inputs. This is the tag that has all the input Bools.
        var inputTag = proj.Get("CommDrivers/RAEtherNet_IPDriver1/RAEtherNet_IPStation1/Tags/Controller Tags");

        //var tag1 = inputTag.Children;

        // Get the children of the Inputs. These are the input Bools.
        var inputChildren = inputTag.Children;

        // Cycle through all the children.

        // Cycle through all the children.
        foreach (var tagchild in inputChildren)
        {
            if (tagchild.BrowseName.StartsWith("In_")
                && !tagchild.BrowseName.Contains("PB")
                && !tagchild.BrowseName.Contains("CPP")
                && !templatesWithInstances.Contains(tagchild.BrowseName))  // Ensure BrowseName is not in templatesWithInstances
            {
                // Create a new sensor instance.
                var sensor = InformationModel.MakeObject("Sensor", sensorType.NodeId);

                // Get the name elements from the tag name. Element[0] will be the label text.
                string[] labelElements = tagchild.BrowseName.Split('_');

                // Get the label within the sensor instance.
                Label labelInput = sensor.Find<Label>("LabelInput");

                // Set the label text value.
                labelInput.Text = labelElements[1];

                // Set the sensor tag node pointer to the IO tag.
                sensor.Get<NodePointer>("Tag").Value = tagchild.NodeId;


                var newRectangle = InformationModel.Make<RectangleType>(tagchild.BrowseName);
                newRectangle.Width = 50;
                newRectangle.Height = 50;
                newRectangle.FillColor = new Color(0, 0, 0, 0);
                newRectangle.BorderThickness = 0;


                // Add the sensor instance to the rectangle.
                newRectangle.Add(sensor);

                // Add the new rectangle (containing the sensor) to the folder.
                folder.Add(newRectangle);

                Log.Info(tagchild.BrowseName.ToString() + ": Added");
            }
        }
    }
    
    [ExportMethod]
    public void Update_Custom_Sensor()
    {
        // Get the current project
        var proj = Project.Current;

        // Access the folder where solenoid templates are located
        Folder sensorFolder = proj.Get<Folder>("UI/Templates/Sensors");

        // Retrieve the ChangeFrom and ChangeTo values
        string sensorChangeFrom = LogicObject.GetVariable("Sensor_Change_From").Value;
        string sensorChangeTo = LogicObject.GetVariable("Sensor_Change_To").Value;

        // Ensure the ChangeFrom and ChangeTo values are valid
        if (sensorChangeFrom == null || sensorChangeTo == null)
        {
            Log.Error("Change From or Change To values are null.");
            return;
        }

        // Get the children of the solenoid folder
        var sensorFolderChildren = sensorFolder.Children;
        bool foundChangeFrom = false;

        // Iterate through the solenoid templates
        foreach (var sensorChild in sensorFolderChildren)
        {
            // Get the current solenoid's browse name
            string solBrowseName = sensorChild.BrowseName;

            // Check if the solenoid's name matches the ChangeFrom value
            if (solBrowseName == sensorChangeFrom)
            {


                foundChangeFrom = true;
                // Update the solenoid's name to the ChangeTo value
                sensorChild.BrowseName = sensorChangeTo;
                Log.Info($"Updated sensor template name from {sensorChangeFrom} to {sensorChangeTo}");

                string tagNamePLC = sensorChangeTo;

                IUANode changeToTag = null;
                try
                {
                    // Get the Inputs. This is the tag that has all the input Bools.
                    changeToTag = proj.Get($"CommDrivers/RAEtherNet_IPDriver1/RAEtherNet_IPStation1/Tags/Controller Tags/{tagNamePLC}");
                }
                catch
                {
                    Log.Error($"CommDrivers/RAEtherNet_IPDriver1/RAEtherNet_IPStation1/Tags/Controller Tags/{tagNamePLC}/ not found. Check if it's in PLC and Tag importer has been run.");

                }


                if (changeToTag != null)
                {
                    bool statusCheck = true;
                    string labelElement = null;
                    try
                    {
                        // Create a new sensor instance.
                        var sensor = sensorChild.GetObject("Sensor");

                        // Get the name elements from the tag name. Element[0] will be the label text.
                        string[] labelElements = changeToTag.BrowseName.Split('_');

                        // Get the label within the sensor instance.
                        Label LabelInput = sensor.Find<Label>("LabelInput");

                        labelElement = labelElements[1];

                        if (LabelInput != null)
                        {
                            if (LabelInput.Text != labelElement)
                            {
                                // Set the label text value.
                                LabelInput.Text = labelElement;
                                Log.Info($"Template Label updated to: {LabelInput.Text}");
                            }
                        }
                        else
                        {
                            Log.Error($"LabelOutput not found in {sensorChangeFrom}");
                        }

                        if (sensor.Get<NodePointer>("Tag").NodeId != null)
                        {
                            if (sensor.Get<NodePointer>("Tag").NodeId != changeToTag.NodeId)
                            {
                                // Set the sensor tag node pointer to the IO tag.
                                sensor.Get<NodePointer>("Tag").Value = changeToTag.NodeId;
                                Log.Info($"{sensorChangeFrom}: Updated template tag value to {tagNamePLC}");
                            }
                        }
                        else
                        {
                            Log.Error($"Tag not found in {sensorChangeFrom}");
                        }
                    }
                    catch
                    {
                        statusCheck = false;
                    }

                    if (statusCheck)
                    {
                        // Update the child instances, if applicable, similar to how messages were updated
                        UpdateInstancesLabelsAndTagPathsSensor(sensorChild, sensorChangeTo, "SOL", sensorChangeFrom, labelElement, changeToTag, tagNamePLC);
                    }
                    else
                    {
                        Log.Error($"Error updating {sensorChangeFrom}. Instances cannot be updated.");
                    }
                }


                break; // Break out once we've found and updated the target solenoid
            }
        }
        if (!foundChangeFrom)
        {
            Log.Info($"Did not find {sensorChangeFrom} in templates folder");
        }
    }

    private void UpdateInstancesLabelsAndTagPathsSensor(dynamic child, string validIdentifier, string suffix, string changeFrom, string labelElement, IUANode changeToTag, string forceName)
    {


        // Update instance collection
        dynamic childType = child.GetType();
        PropertyInfo[] properties = childType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        foreach (var prop in properties)
        {
            var value = prop.GetValue(child, null);
            if (value is UAManagedCore.InstanceNodeCollection<FTOptix.UI.BaseUIObject> instanceCollection)
            {
                if (instanceCollection.Count == 0) return;

                foreach (var instance in instanceCollection)
                {
                    if (instance is FTOptix.UI.Rectangle sensorInstance)
                    {
                        if (sensorInstance != null && ($"{validIdentifier}") != sensorInstance.BrowseName)
                        {
                            sensorInstance.BrowseName = $"{validIdentifier}";
                            Log.Info($"The Instance: {sensorInstance.BrowseName} has been renamed");

                            // Create a new sensor instance.
                            var solenoid = sensorInstance.GetObject("Sensor");

                            // Get the label within the sensor instance.
                            Label LabelInput = solenoid.Find<Label>("LabelInput");

                            if (LabelInput != null)
                            {
                                if (LabelInput.Text != labelElement)
                                {
                                    // Set the label text value.
                                    LabelInput.Text = labelElement;
                                    Log.Info($"Updated instance label to {LabelInput.Text}");
                                }
                            }
                            else
                            {
                                Log.Error($"LabelOutput not found in {changeFrom}");
                            }
                            if (solenoid.Get<NodePointer>("Tag").NodeId != null)
                            {
                                if (solenoid.Get<NodePointer>("Tag").NodeId != changeToTag.NodeId)
                                {
                                    // Set the sensor tag node pointer to the IO tag.
                                    solenoid.Get<NodePointer>("Tag").Value = changeToTag.NodeId;
                                    Log.Info($"{changeFrom} : Updated tag value to {forceName}");
                                }
                            }

                        }
                    }
                    //else
                    //{
                    //    Log.Warning($"Instance is not a Button, skipping: {instance.GetType()}");
                    //}
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
