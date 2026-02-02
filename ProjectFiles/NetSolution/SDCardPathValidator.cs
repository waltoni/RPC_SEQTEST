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
using System.IO;
using System.Collections.Generic;
using System.Reflection;
#endregion

public class SDCardPathValidator : BaseNetLogic
{
    public override void Start()
    {
        // Insert code to be executed when the user-defined logic is started
        //GrabUserPaths();
    }

    public override void Stop()
    {
        // Insert code to be executed when the user-defined logic is stopped
    }


    [ExportMethod]
    public void GrabUserPaths(string pdfsFolderPath)
    {
        var proj = Project.Current;

        // Get the folder to hold the sensors.
        Folder folder = proj.Get<Folder>(pdfsFolderPath);

        if (folder != null)
        {
            var folderChildren = folder.Children;
            if (folderChildren != null)
            {
                foreach (var child in folderChildren)
                {
                    var a = child.GetType().ToString();
                    if (child.GetType().ToString() == "SDPathBaseObject")
                    {
                        IUAVariable uAVariablePath = child.Children[0] as IUAVariable;
                        IUAVariable uAVariableValid = child.Children[1] as IUAVariable;
                        IUAVariable uAVariableErrorMessage = child.Children[2] as IUAVariable;
                        (bool isPathValid, string errorMessage) = ValidatePathAndReturnValidity(uAVariablePath.Value);
                        if (uAVariableValid != null)
                        {
                            uAVariableValid.Value = isPathValid;
                            uAVariableErrorMessage.Value = errorMessage;
                        }
                    }
                }
            }
        }
        else
        {
            Log.Info($"Folder Path: {pdfsFolderPath} not found");
        }
    }


    private void ValidatePaths(string pathToValidate)
    {

        //string pathToValidate = LogicObject.GetVariable("pathToValidate").Value;
        string errorMessage = LogicObject.GetVariable("ErrorMessage").Value;
        pathToValidate = ResolveUSBPath(pathToValidate);
        //Log.Info(pathToValidate);
        bool isPathValid = LogicObject.GetVariable("isPathValid").Value;

        if (!string.IsNullOrEmpty(pathToValidate))
        {
            try
            {
                //string path = resourceUri.Uri;


                if (File.Exists(pathToValidate))
                {
                    isPathValid = true;
                    Log.Info($"The path {pathToValidate} is valid.");
                    LogicObject.GetVariable("ErrorMessage").Value = ($"The path {pathToValidate} is valid./n");
                }
                else
                {
                    Log.Error($"The path {pathToValidate} is invalid or the resource does not exist.");
                    LogicObject.GetVariable("ErrorMessage").Value += ($"The path {pathToValidate} is invalid or the resource does not exist./n");
                }
            }
            catch (Exception ex)
            {
                // Handle any exceptions (like invalid URI format)
                Log.Error($"Error validating the path: {ex.Message}");
                LogicObject.GetVariable("ErrorMessage").Value = ($"Error validating the path: {ex.Message}/n");
            }
        }
        else
        {
            Log.Info("Path is null or empty");
            LogicObject.GetVariable("ErrorMessage").Value = ("Path is null or empty/n");
        }

        // Update the logic object variable with the validation result
        LogicObject.GetVariable("isPathValid").Value = isPathValid;
    }

    private string ResolveUSBPath(string path)
    {
        if (path.Contains("%USB1%"))
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                // Replace %USB1% with actual path like "E:\" for Windows
                return path.Replace("%USB1%", "E:\\");
            }
            else
            {
                // Replace %USB1% with actual path like "///storage/sd1" for other OS
                return path.Replace("%USB1%", "///storage/usb1");
            }
        }
        return path;
    }

    private (bool, string) ValidatePathAndReturnValidity(string pathToValidate)
    {
        string originalPath = pathToValidate;
        string errorMessage = "";
        pathToValidate = ResolveUSBPath(pathToValidate);
        bool isPathValid = false;

        if (!string.IsNullOrEmpty(pathToValidate))
        {
            try
            {
                if (File.Exists(pathToValidate))
                {
                    isPathValid = true;
                    Log.Info($"The path {originalPath} is valid.");
                }
                else
                {
                    Log.Error($"{originalPath} is not found on the SD Card.");
                    errorMessage = ($"{originalPath} is not found on the SD Card.");
                }
            }
            catch (Exception ex)
            {
                // Handle any exceptions (like invalid URI format)
                Log.Error($"Error validating the path: {ex.Message}");
                errorMessage = ($"Error validating the path: {ex.Message}");
            }
        }
        else
        {
            Log.Info("Path is null or empty");
            errorMessage = ("Path set in Optix is null or empty");
        }

        return (isPathValid, errorMessage);
    }




    [ExportMethod]
    public void initializeSDCard()
    {

        var sdInitializePath = GetSDInitializePath();

        var sdPushPath = GetSDPushPath();

        if (Directory.Exists(sdInitializePath) && Directory.Exists(sdPushPath))
        {
            CopyDirectory(sdInitializePath, sdPushPath);
        }
        else
        {
            Log.Error("One or both of the specified directories do not exist.");
        }



    }
    public static void CopyDirectory(string sourceDir, string destinationDir)
    {
        // Ensure the destination directory ends with a directory separator
        if (!destinationDir.EndsWith(Path.DirectorySeparatorChar))
        {
            destinationDir += Path.DirectorySeparatorChar;
        }

        // Get the subdirectories for the specified directory.
        DirectoryInfo dir = new DirectoryInfo(sourceDir);

        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException(
                "Source directory does not exist or could not be found: "
                + sourceDir);
        }

        DirectoryInfo[] dirs = dir.GetDirectories();
        // If the destination directory doesn't exist, create it.
        Directory.CreateDirectory(destinationDir);

        // Get the files in the directory and copy them to the new location.
        FileInfo[] files = dir.GetFiles();
        foreach (FileInfo file in files)
        {
            string tempPath = Path.Combine(destinationDir, file.Name);
            // Check if the file already exists in the destination directory
            if (!File.Exists(tempPath))
            {
                file.CopyTo(tempPath, false);
            }
        }

        // Copy subdirectories and their contents to new location.
        foreach (DirectoryInfo subdir in dirs)
        {
            string tempPath = Path.Combine(destinationDir, subdir.Name);
            CopyDirectory(subdir.FullName, tempPath);
        }
    }


    private string GetSDInitializePath()
    {
        var sdInitVariable = LogicObject.GetVariable("SDInitialize");
        var errorMessage = LogicObject.GetVariable("ErrorMessage");
        if (sdInitVariable == null)
        {
            Log.Error("SDInitializeFile variable not found");
            return "";
        }

        ResourceUri sdInitUri;
        try
        {
            errorMessage.Value = "";
            sdInitUri = new ResourceUri(sdInitVariable.Value).Uri;
        }
        catch (Exception ex)
        {
            Log.Error($"Error getting the SD Init Path: {ex.Message}");
            errorMessage.Value = $"Error getting the SD Push Path: {ex.Message}";
            return "";
        }

        return sdInitUri;
    }

    private string GetSDPushPath()
    {
        var sdPushVariable = LogicObject.GetVariable("SDPath");
        var errorMessage = LogicObject.GetVariable("ErrorMessage");
        if (sdPushVariable == null)
        {
            Log.Error("SDInitializeFile variable not found");
            return "";
        }
        ResourceUri spPushUri;

        try
        {
            errorMessage.Value = "";
            spPushUri = new ResourceUri(sdPushVariable.Value).Uri;
        }
        catch (Exception ex)
        {
            Log.Error($"Error getting the SD Push Path: {ex.Message}");
            errorMessage.Value = $"Error getting the SD Push Path: {ex.Message}";
            return "";
        }

        return spPushUri;
    }


}
