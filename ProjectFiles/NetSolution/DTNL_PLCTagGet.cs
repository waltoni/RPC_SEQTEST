#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.SQLiteStore;
using FTOptix.EventLogger;
using FTOptix.HMIProject;
using FTOptix.NetLogic;
using FTOptix.UI;
using FTOptix.NativeUI;
using FTOptix.Store;
using FTOptix.RAEtherNetIP;
using FTOptix.System;
using FTOptix.Retentivity;
using FTOptix.CoreBase;
using FTOptix.Alarm;
using FTOptix.CommunicationDriver;
using FTOptix.SerialPort;
using FTOptix.Core;
using FTOptix.WebUI;
using FTOptix.Recipe;
using FTOptix.Report;
using FTOptix.AuditSigning;
using Tag = FTOptix.RAEtherNetIP.Tag;
using System.Linq;
using System.Text.RegularExpressions;
#endregion

public class DTNL_PLCTagGet : BaseNetLogic
{

    [ExportMethod]
    public void CreateSensors()
    {
        // Get a reference to this project. This will simplify further code.
        var proj = Project.Current;

        Folder ioMapsFoler = proj.Get<Folder>("UI/Templates/MachineOptions/IOMaps");

        var folderIoMapsChildren = ioMapsFoler.Children;

        foreach (var child in folderIoMapsChildren)
        {
            
            if (child.BrowseName.Contains("Input"))
            {
                Log.Info(child.BrowseName.ToString());  
            }


        }

        // Get the folder to hold the sensors.
        Folder folder = proj.Get<Folder>("UI/Templates/Sensors");



        // Clear all previous sensors. This prevents duplicates.
        var folderChildren = folder.Children;
        foreach (var child in folderChildren)
        {
            // check to see if there are references in the Machine Options folder 
            // Check if the child is not a folder before deleting it
            if (child != null)
            {
                Log.Info(child.BrowseName.ToString() + ": Deleted");
                child.Delete();

            }
        }

        //see if theres a way to just modify if found
        //use logic below in loop and update if instance is found
        //this will update the instances appropriately 
        //or make a duplicate and give ability to keep old ones 

        // Get the sensor type.
        var sensorType = proj.Get("UI/Templates/Global Templates/Sensors_Global/Sensors");

        // Get the Inputs. This is the tag that has all the input Bools.
        var inputTag = proj.Get("CommDrivers/RAEtherNet_IPDriver1/RAEtherNet_IPStation1/Tags/Controller Tags");

        //var tag1 = inputTag.Children;

        // Get the children of the Inputs. These are the input Bools.
        var inputChildren = inputTag.Children;

        // Cycle through all the children.

        // Cycle through all the children.
        foreach (var tagchild in inputChildren)
        {
            if (tagchild.BrowseName.StartsWith("In_") && !tagchild.BrowseName.Contains("PB") && !tagchild.BrowseName.Contains("CPP") && !tagchild.BrowseName.Contains("CPP"))
            {
                // Create a new sensor instance.
                var sensor = InformationModel.MakeObject(tagchild.BrowseName, sensorType.NodeId);

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

                // Create a new rectangle to hold the sensor.
                //var rectangle = InformationModel.MakeObject(tagchild.BrowseName + "_Rect", newRectangle.NodeId);
                //rectangle.Get<Width>("Width").Value = 50;

                // Add the sensor instance to the rectangle.
                newRectangle.Add(sensor);

                // Add the new rectangle (containing the sensor) to the folder.
                folder.Add(newRectangle);

                Log.Info(tagchild.BrowseName.ToString() + ": Added");
            }
        }
    }



    [ExportMethod]
    public void CreateSolenoids()
    {
        // Get a reference to this project. This will simplify further code.
        var proj = Project.Current;

        // Get the folder to hold the sensors.
        Folder folder = proj.Get<Folder>("UI/Templates/SOL");

        // Clear all previous sensors. This prevents duplicates.
        var folderChildren = folder.Children;
        foreach (var child in folderChildren)
        {
            // check to see if there are references in the Machine Options folder 
            // Check if the child is not a folder before deleting it
            if (child != null)
            {
                Log.Info(child.BrowseName.ToString() + ": Deleted");
                child.Delete();

            }
        }

        // Get the sensor type.
        var solType = proj.Get("UI/Templates/Global Templates/SOL_Global/SOL");

        // Get the Inputs. This is the tag that has all the input Bools.
        var inputTag = proj.Get("CommDrivers/RAEtherNet_IPDriver1/RAEtherNet_IPStation1/Tags/Controller Tags");

        //var tag1 = inputTag.Children;

        // Get the children of the Inputs. These are the input Bools.
        var inputChildren = inputTag.Children;

        // Cycle through all the children.

        // Cycle through all the children.
        foreach (var tagchild in inputChildren)
        {
            if (tagchild.BrowseName.StartsWith("Force_") && (tagchild.BrowseName != ("Force_Enabled")))// && !tagchild.BrowseName.Contains("PB") && !tagchild.BrowseName.Contains("CPP") && !tagchild.BrowseName.Contains("CPP"))
            {
                // Create a new sensor instance.
                var solenoid = InformationModel.MakeObject("SOL", solType.NodeId);

                // Get the name elements from the tag name. Element[0] will be the label text.
                string[] labelElements = tagchild.BrowseName.Split('_');

                // Get the label within the sensor instance.
                Label LabelOutput = solenoid.Find<Label>("LabelOutput");

                // Set the label text value.
                LabelOutput.Text = labelElements[1];

                // Set the sensor tag node pointer to the IO tag.
                solenoid.Get<NodePointer>("Tag").Value = tagchild.NodeId;

                //var newRectangle = InformationModel.Make<RectangleType>(tagchild.BrowseName);
                //newRectangle.Width = 50;

                var newRectangle = InformationModel.Make<RectangleType>(tagchild.BrowseName.Replace("Force_",""));
                newRectangle.Width = 50;

                newRectangle.Add(solenoid);

                // Add the new rectangle (containing the sensor) to the folder.
                folder.Add(newRectangle);

                Log.Info(tagchild.BrowseName.ToString() + ": Added");
            }
        }
    }


    [ExportMethod]
    public void CreateAlarms()
    {
        //// Get a reference to this project. This will simplify further code.
        var proj = Project.Current;

        //// Get the folder to hold the Alarm Details.
        Folder folder = proj.Get<Folder>("UI/AlarmDetails");

        //// Clear all previous sensors. This prevents duplicates.
        var folderChildren = folder.Children;
        foreach (var child in folderChildren)
        {
            child.Delete();
        }

        //// Get the Alarm type.
        var alarmType = proj.Get("UI/Templates/Alarm/AlarmPopUp");

        //// Set the index value to zero for the start of indexing
        string AlarmPopUpName = "PopUp_00_";

        //Get the link for the Preview links
        var alarmPreviewPath = proj.Get("UI/Screens/Alarms/AlarmPreview/Horizontal_Preview_List/ScrollAlarms/VerticalLayoutLeft");
        // var alarmPreviewChildren = alarmPreviewPath.Children;


        // Cycle through all the children.
        for (int i = 0; i < 32; i++)
        {
            AlarmPopUpName = "PopUp_00_";
            AlarmPopUpName = AlarmPopUpName + i.ToString();
            // Create a new sensor PopUp Instance.
            var alarm = InformationModel.MakeObject(AlarmPopUpName, alarmType.NodeId);

            // Add the new sensor popup instance to the folder.
            folder.Add(alarm);

            var alarmPreviewChildren = alarmPreviewPath.Children;
            //Now associate the preview link to the newly created Popup
            //var alarmChild = alarmPreviewChildren("PreviewLink" + i.ToString());
            //need to Associate the alias to the link

        }

    }

    [ExportMethod]
    public void Create_ChangePoints()
    {
        // Get a reference to this project. This will simplify further code.
        var proj = Project.Current;

        // Get the folder to hold the sensors.
        Folder folder = proj.Get<Folder>("UI/ChangePoints");

        // Clear all previous sensors. This prevents duplicates.
        var folderChildren = folder.Children;
        foreach (var child in folderChildren)
        {
            child.Delete();
        }

        // Get the sensor type.
        var CP_Type = proj.Get("UI/Sensors_Global/CP");

        // Get the Inputs. This is the tag that has all the input Bools.
        var inputTag = proj.Get("CommDrivers/RAEtherNet_IPDriver1/RAEtherNet_IPStation1/Tags/Controller Tags/_HMI/CO_Checklist");
        
        // Get the children of the Inputs. These are the input Bools.
        var inputChildren = inputTag.Children;

        // Cycle through all the children.
        for (int i = 0; i < 32; i++)
     // foreach (Tag tagchild in inputChildren)
        {
            

            // Create a new sensor instance.
            var changepoint = InformationModel.MakeObject("CP"+i.ToString(), CP_Type.NodeId);

            // Get the label within the sensor instance.
            Label labelInput = changepoint.Find<Label>("LabelInput");

            // Set the label text value.
            labelInput.Text = i.ToString(); 

            // Set the sensor tag node pointer to the IO tag.
            //changepoint.Get<NodePointer>("Tag").Value = tagchild.NodeId;

            // Add the new sensor instance to the folder.
            folder.Add(changepoint);
        }
    }

}
