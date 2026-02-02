#region Using directives
using System;
using System.Linq;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.UI;
using FTOptix.HMIProject;
using FTOptix.NetLogic;
using FTOptix.CoreBase;
using FTOptix.NativeUI;
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
#endregion

public class CustomPalletOperations : BaseNetLogic
{
    public override void Start()
    {
        // Insert code to be executed when the user-defined logic is started
    }

    public override void Stop()
    {
        // Insert code to be executed when the user-defined logic is stopped
    }

    /// <summary>
    /// Gets the PalletImage from CustomPalletContainer -> PalletImage
    /// CustomPalletOperations is attached to CustomPalletContainer, so LogicObject.Owner is CustomPalletContainer
    /// </summary>
    private Image GetPalletImage()
    {
        try
        {
            // CustomPalletOperations is a child of CustomPalletContainer
            // So LogicObject.Owner is CustomPalletContainer
            var customPalletContainer = LogicObject.Owner as Rectangle;
            if (customPalletContainer == null)
            {
                Log.Error("CustomPalletOperations", "LogicObject.Owner is not a Rectangle (CustomPalletContainer)");
                return null;
            }

            // Get PalletImage from CustomPalletContainer
            var palletImage = customPalletContainer.Get<Image>("PalletImage");
            if (palletImage == null)
            {
                Log.Error("CustomPalletOperations", "PalletImage not found in CustomPalletContainer");
                return null;
            }

            return palletImage;
        }
        catch (Exception ex)
        {
            Log.Error("CustomPalletOperations", $"Error getting PalletImage: {ex.Message} - {ex.StackTrace}");
            return null;
        }
    }

    /// <summary>
    /// Gets Box1 as a prototype for creating new boxes.
    /// </summary>
    private BaseUIObject GetBox1Prototype()
    {
        try
        {
            var palletImage = GetPalletImage();
            if (palletImage == null)
            {
                Log.Error("CustomPalletOperations", "Cannot get Box1: PalletImage not found");
                return null;
            }

            var box1 = palletImage.Get<BaseUIObject>("Box1");
            if (box1 == null)
            {
                Log.Error("CustomPalletOperations", "Box1 not found in PalletImage");
                return null;
            }

            return box1;
        }
        catch (Exception ex)
        {
            Log.Error("CustomPalletOperations", $"Error getting Box1 prototype: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets all boxes from the PalletImage.
    /// </summary>
    private System.Collections.Generic.List<BaseUIObject> GetBoxes()
    {
        var palletImage = GetPalletImage();
        if (palletImage == null)
            return new System.Collections.Generic.List<BaseUIObject>();

        var boxes = new System.Collections.Generic.List<BaseUIObject>();
        foreach (var child in palletImage.Children)
        {
            if (child.BrowseName.StartsWith("Box"))
            {
                var baseUIObj = child as BaseUIObject;
                if (baseUIObj != null)
                {
                    boxes.Add(baseUIObj);
                }
            }
        }

        return boxes;
    }

    /// <summary>
    /// Gets the next available box number.
    /// </summary>
    private int GetNextBoxNumber()
    {
        var boxes = GetBoxes();
        if (boxes.Count == 0)
            return 1;

        int maxNumber = 0;
        foreach (var box in boxes)
        {
            string name = box.BrowseName;
            if (name.StartsWith("Box") && name.Length > 3)
            {
                string numberPart = name.Substring(3);
                if (int.TryParse(numberPart, out int number))
                {
                    if (number > maxNumber)
                        maxNumber = number;
                }
            }
        }

        return maxNumber + 1;
    }

    /// <summary>
    /// Creates a new box instance using Box1 as a prototype.
    /// </summary>
    private BaseUIObject CreateBox(string boxName, int index, float leftMargin, float topMargin, float width, float height)
    {
        try
        {
            var palletImage = GetPalletImage();
            if (palletImage == null)
            {
                Log.Error("CustomPalletOperations", "Cannot create box: PalletImage not found");
                return null;
            }

            // Get Box1 as prototype
            var box1Prototype = GetBox1Prototype();
            if (box1Prototype == null)
            {
                Log.Error("CustomPalletOperations", "Cannot create box: Box1 prototype not found");
                return null;
            }

            // Create new box using Box1's object type
            NodeId boxTypeId;
            if (box1Prototype is IUAObject obj)
            {
                boxTypeId = obj.ObjectType.NodeId;
            }
            else
            {
                Log.Error("CustomPalletOperations", "Box1 is not an IUAObject");
                return null;
            }

            var newBoxObj = InformationModel.MakeObject(boxName, boxTypeId);
            if (newBoxObj == null)
            {
                Log.Error("CustomPalletOperations", $"Failed to create box instance: {boxName}");
                return null;
            }

            // Cast IUAObject to BaseUIObject
            var newBox = newBoxObj as BaseUIObject;
            if (newBox == null)
            {
                Log.Error("CustomPalletOperations", $"Failed to cast created object to BaseUIObject: {boxName}");
                return null;
            }

            // Add to the pallet image first (needed for properties to be accessible)
            palletImage.Add(newBox);

            // Copy all properties from Box1 prototype by iterating through its children
            // This ensures we copy all variables including optional ones
            if (box1Prototype.Children != null)
            {
                foreach (var box1Child in box1Prototype.Children)
                {
                    if (box1Child == null)
                        continue;

                    // Skip certain properties that should be unique to each box
                    if (box1Child.BrowseName == "BoxTitle" || 
                        box1Child.BrowseName == "Index" ||
                        box1Child.BrowseName == "LeftMargin" ||
                        box1Child.BrowseName == "TopMargin" ||
                        box1Child.BrowseName == "Width" ||
                        box1Child.BrowseName == "Height")
                    {
                        continue; // These will be set separately
                    }

                    // Copy variables
                    if (box1Child is IUAVariable box1Var)
                    {
                        try
                        {
                            var newBoxVar = newBox.GetVariable(box1Child.BrowseName);
                            if (newBoxVar != null)
                            {
                                // Only copy if Box1 has a value (don't copy null values)
                                if (box1Var.Value != null)
                                {
                                    newBoxVar.Value = box1Var.Value;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Warning("CustomPalletOperations", $"Could not copy property {box1Child.BrowseName}: {ex.Message}");
                        }
                    }
                }
            }

            // Copy HorizontalAlignment and VerticalAlignment (access via variables)
            try
            {
                var box1HorizontalAlignment = box1Prototype.GetVariable("HorizontalAlignment");
                var newBoxHorizontalAlignment = newBox.GetVariable("HorizontalAlignment");
                if (box1HorizontalAlignment != null && newBoxHorizontalAlignment != null && box1HorizontalAlignment.Value != null)
                {
                    newBoxHorizontalAlignment.Value = box1HorizontalAlignment.Value;
                }

                var box1VerticalAlignment = box1Prototype.GetVariable("VerticalAlignment");
                var newBoxVerticalAlignment = newBox.GetVariable("VerticalAlignment");
                if (box1VerticalAlignment != null && newBoxVerticalAlignment != null && box1VerticalAlignment.Value != null)
                {
                    newBoxVerticalAlignment.Value = box1VerticalAlignment.Value;
                }
            }
            catch (Exception ex)
            {
                Log.Warning("CustomPalletOperations", $"Could not copy alignment properties: {ex.Message}");
            }

            // Copy MoveTarget - this should point to the parent (PalletImage)
            try
            {
                var box1MoveTarget = box1Prototype.Get<NodePointer>("MoveTarget");
                var newBoxMoveTarget = newBox.Get<NodePointer>("MoveTarget");
                if (box1MoveTarget != null && newBoxMoveTarget != null && palletImage != null)
                {
                    // Set MoveTarget to point to the PalletImage (which is now the parent)
                    newBoxMoveTarget.Value = palletImage.NodeId;
                }
            }
            catch (Exception ex)
            {
                Log.Warning("CustomPalletOperations", $"Could not copy MoveTarget: {ex.Message}");
            }

            // Set box-specific properties (these override Box1 values)
            var boxTitleVar = newBox.GetVariable("BoxTitle");
            if (boxTitleVar != null)
            {
                boxTitleVar.Value = new LocalizedText("en-US", boxName.Replace("Box", "Box "));
            }

            var indexVar = newBox.GetVariable("Index");
            if (indexVar != null)
            {
                indexVar.Value = index;
            }
            
            var widthVar = newBox.GetVariable("Width");
            if (widthVar != null)
            {
                widthVar.Value = width;
            }
            
            var heightVar = newBox.GetVariable("Height");
            if (heightVar != null)
            {
                heightVar.Value = height;
            }

            var leftMarginVar = newBox.GetVariable("LeftMargin");
            if (leftMarginVar != null)
            {
                leftMarginVar.Value = leftMargin;
            }

            var topMarginVar = newBox.GetVariable("TopMargin");
            if (topMarginVar != null)
            {
                topMarginVar.Value = topMargin;
            }

            Log.Info($"CustomPalletOperations: Created {boxName} at ({leftMargin}, {topMargin})");
            return newBox;
        }
        catch (Exception ex)
        {
            Log.Error("CustomPalletOperations", $"Error creating box: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Adds a new case (box) to the pallet under CustomTest-CustomPalletContainer-PalletImage.
    /// Uses Box1 as the reference object for creating the new case.
    /// </summary>
    [ExportMethod]
    public void AddCase()
    {
        try
        {
            // Verify we can get the pallet image first
            var palletImage = GetPalletImage();
            if (palletImage == null)
            {
                Log.Error("CustomPalletOperations", "Cannot add case: PalletImage not found");
                return;
            }

            // Verify Box1 exists
            var box1 = GetBox1Prototype();
            if (box1 == null)
            {
                Log.Error("CustomPalletOperations", "Cannot add case: Box1 prototype not found");
                return;
            }

            var boxes = GetBoxes();
            if (boxes == null)
            {
                Log.Error("CustomPalletOperations", "Cannot add case: Failed to get boxes list");
                return;
            }

            int nextBoxNumber = GetNextBoxNumber();
            string boxName = $"Box{nextBoxNumber}";

            float leftMargin = 10.0f;
            float topMargin = 10.0f;
            float defaultWidth = 110.0f;
            float defaultHeight = 80.0f;

            // Get Box1 dimensions as reference if available
            var box1WidthVar = box1.GetVariable("Width");
            var box1HeightVar = box1.GetVariable("Height");
            if (box1WidthVar != null && box1HeightVar != null && 
                box1WidthVar.Value != null && box1HeightVar.Value != null)
            {
                try
                {
                    defaultWidth = (float)box1WidthVar.Value;
                    defaultHeight = (float)box1HeightVar.Value;
                }
                catch (Exception ex)
                {
                    Log.Warning("CustomPalletOperations", $"Could not get Box1 dimensions, using defaults: {ex.Message}");
                }
            }

            // If there are existing boxes, position the new box to the right of the last one
            if (boxes.Count > 0)
            {
                try
                {
                    var lastBox = boxes.OrderByDescending(box =>
                    {
                        if (box == null)
                            return 0;
                        string name = box.BrowseName;
                        if (name != null && name.StartsWith("Box") && name.Length > 3)
                        {
                            string numberPart = name.Substring(3);
                            if (int.TryParse(numberPart, out int number))
                                return number;
                        }
                        return 0;
                    }).FirstOrDefault();

                    if (lastBox != null)
                    {
                        var lastLeftMarginVar = lastBox.GetVariable("LeftMargin");
                        var lastWidthVar = lastBox.GetVariable("Width");
                        if (lastLeftMarginVar != null && lastWidthVar != null &&
                            lastLeftMarginVar.Value != null && lastWidthVar.Value != null)
                        {
                            leftMargin = (float)lastLeftMarginVar.Value + (float)lastWidthVar.Value + 10.0f; // 10px spacing
                        }
                        
                        var lastTopMarginVar = lastBox.GetVariable("TopMargin");
                        if (lastTopMarginVar != null && lastTopMarginVar.Value != null)
                        {
                            topMargin = (float)lastTopMarginVar.Value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning("CustomPalletOperations", $"Could not calculate position from existing boxes: {ex.Message}");
                }
            }

            var newBox = CreateBox(boxName, nextBoxNumber, leftMargin, topMargin, defaultWidth, defaultHeight);
            if (newBox != null)
            {
                Log.Info($"CustomPalletOperations: Successfully added {boxName}");
            }
            else
            {
                Log.Error("CustomPalletOperations", $"Failed to create box: {boxName}");
            }
        }
        catch (Exception ex)
        {
            Log.Error("CustomPalletOperations", $"Error in AddCase: {ex.Message} - {ex.StackTrace}");
        }
    }
}
