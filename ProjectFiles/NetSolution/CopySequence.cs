#region Using directives
using System;
using System.Collections.Generic;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.HMIProject;
using FTOptix.CoreBase;
using FTOptix.UI;
using FTOptix.NetLogic;
#endregion

public class CopySequence : BaseNetLogic
{
    [ExportMethod]
    public void CopyRectangleWidths()
    {
        var proj = Project.Current;
        
        // Get the NormalSequence template
        var normalSequence = proj.Get("UI/Templates/RecipeEditPanels/NormalSequence");
        if (normalSequence == null)
        {
            Log.Error("NormalSequence template not found");
            return;
        }
        
        // Get RecipesEditorSeq -> ScrollView -> ColumnLayout
        var recipesEditorSeq = normalSequence.Get<Panel>("RecipesEditorSeq");
        if (recipesEditorSeq == null)
        {
            Log.Error("RecipesEditorSeq not found");
            return;
        }
        
        var scrollView = recipesEditorSeq.Get<ScrollView>("ScrollView");
        if (scrollView == null)
        {
            Log.Error("ScrollView not found");
            return;
        }
        
        var columnLayout = scrollView.Get<ColumnLayout>("ColumnLayout");
        if (columnLayout == null)
        {
            Log.Error("ColumnLayout not found");
            return;
        }
        
        // Get Sequence1
        var sequence1 = columnLayout.Get<Rectangle>("Sequence1");
        if (sequence1 == null)
        {
            Log.Error("Sequence1 not found");
            return;
        }
        
        // Get SequenceIndexes from Sequence1
        var sequence1Indexes = sequence1.Get<RowLayout>("SequenceIndexes");
        if (sequence1Indexes == null)
        {
            Log.Error("Sequence1/SequenceIndexes not found");
            return;
        }
        
        // Collect all Rectangle widths from Sequence1
        var rectangleWidths = new Dictionary<string, double>();
        CollectRectangleWidths(sequence1Indexes, rectangleWidths);
        
        Log.Info($"Found {rectangleWidths.Count} Rectangle widths in Sequence1");
        foreach (var kvp in rectangleWidths)
        {
            Log.Info($"  {kvp.Key}: {kvp.Value}");
        }
        
        // Set SequenceIndexes width to -1 (Auto) for Sequence1
        var sequence1WidthVar = sequence1Indexes.GetVariable("Width");
        if (sequence1WidthVar != null)
        {
            var oldWidth1 = sequence1WidthVar.Value != null ? (double)sequence1WidthVar.Value : 0;
            sequence1WidthVar.Value = -1.0;
            Log.Info($"Set Sequence1/SequenceIndexes width: {oldWidth1} -> -1.0 (Auto)");
        }
        
        // Copy widths to Sequence2 through Sequence32
        for (int seqNum = 2; seqNum <= 32; seqNum++)
        {
            var sequenceN = columnLayout.Get<Rectangle>($"Sequence{seqNum}");
            if (sequenceN == null)
            {
                Log.Warning($"Sequence{seqNum} not found, skipping");
                continue;
            }
            
            var sequenceNIndexes = sequenceN.Get<RowLayout>("SequenceIndexes");
            if (sequenceNIndexes == null)
            {
                Log.Warning($"Sequence{seqNum}/SequenceIndexes not found, skipping");
                continue;
            }
            
            // Set SequenceIndexes width to -1 (Auto)
            var sequenceNWidthVar = sequenceNIndexes.GetVariable("Width");
            if (sequenceNWidthVar != null)
            {
                var oldWidth = sequenceNWidthVar.Value != null ? (double)sequenceNWidthVar.Value : 0;
                sequenceNWidthVar.Value = -1.0;
                Log.Info($"Set Sequence{seqNum}/SequenceIndexes width: {oldWidth} -> -1.0 (Auto)");
            }
            
            // Apply widths to matching Rectangles
            ApplyRectangleWidths(sequenceNIndexes, rectangleWidths);
            Log.Info($"Copied widths to Sequence{seqNum}");
        }
        
        Log.Info("✅ Finished copying Rectangle widths from Sequence1 to all other sequences and setting SequenceIndexes width to -1 (Auto)");
    }
    
    [ExportMethod]
    public void CopySequenceIndexesProperties()
    {
        var proj = Project.Current;
        
        // Get the NormalSequence template
        var normalSequence = proj.Get("UI/Templates/RecipeEditPanels/NormalSequence");
        if (normalSequence == null)
        {
            Log.Error("NormalSequence template not found");
            return;
        }
        
        // Get RecipesEditorSeq -> ScrollView -> ColumnLayout
        var recipesEditorSeq = normalSequence.Get<Panel>("RecipesEditorSeq");
        if (recipesEditorSeq == null)
        {
            Log.Error("RecipesEditorSeq not found");
            return;
        }
        
        var scrollView = recipesEditorSeq.Get<ScrollView>("ScrollView");
        if (scrollView == null)
        {
            Log.Error("ScrollView not found");
            return;
        }
        
        var columnLayout = scrollView.Get<ColumnLayout>("ColumnLayout");
        if (columnLayout == null)
        {
            Log.Error("ColumnLayout not found");
            return;
        }
        
        // Get Sequence1 as reference
        var sequence1 = columnLayout.Get<Rectangle>("Sequence1");
        if (sequence1 == null)
        {
            Log.Error("Sequence1 not found");
            return;
        }
        
        // Get SequenceIndexes from Sequence1
        var sequence1Indexes = sequence1.Get<RowLayout>("SequenceIndexes");
        if (sequence1Indexes == null)
        {
            Log.Error("Sequence1/SequenceIndexes not found");
            return;
        }
        
        // Collect properties from Sequence1/SequenceIndexes
        var properties = new Dictionary<string, Dictionary<string, double>>();
        var switchProperties = new Dictionary<string, Dictionary<string, object>>();
        CollectSequenceIndexesProperties(sequence1Indexes, properties, switchProperties);
        
        Log.Info($"Collected properties from Sequence1/SequenceIndexes:");
        foreach (var kvp in properties)
        {
            Log.Info($"  {kvp.Key}:");
            foreach (var prop in kvp.Value)
            {
                Log.Info($"    {prop.Key}: {prop.Value}");
            }
        }
        foreach (var kvp in switchProperties)
        {
            Log.Info($"  {kvp.Key} (Switch):");
            foreach (var prop in kvp.Value)
            {
                Log.Info($"    {prop.Key}: {prop.Value}");
            }
        }
        
        // Set Sequence1 Rectangle width to -1 (Auto)
        var sequence1WidthVar = sequence1.GetVariable("Width");
        if (sequence1WidthVar != null)
        {
            sequence1WidthVar.Value = -1.0;
            Log.Info($"Set Sequence1 Rectangle width to -1.0 (Auto)");
        }
        
        // Copy to Sequence2 through Sequence32 in NormalSequence
        for (int seqNum = 2; seqNum <= 32; seqNum++)
        {
            var sequenceN = columnLayout.Get<Rectangle>($"Sequence{seqNum}");
            if (sequenceN == null)
            {
                Log.Warning($"Sequence{seqNum} not found, skipping");
                continue;
            }
            
            // Set Rectangle width to -1 (Auto)
            var sequenceNWidthVar = sequenceN.GetVariable("Width");
            if (sequenceNWidthVar != null)
            {
                sequenceNWidthVar.Value = -1.0;
            }
            
            var sequenceNIndexes = sequenceN.Get<RowLayout>("SequenceIndexes");
            if (sequenceNIndexes == null)
            {
                Log.Warning($"Sequence{seqNum}/SequenceIndexes not found, skipping");
                continue;
            }
            
            // Set SequenceIndexes width to -1 (Auto)
            var sequenceNIndexesWidthVar = sequenceNIndexes.GetVariable("Width");
            if (sequenceNIndexesWidthVar != null)
            {
                sequenceNIndexesWidthVar.Value = -1.0;
            }
            
            // Apply properties
            ApplySequenceIndexesProperties(sequenceNIndexes, properties, switchProperties);
            Log.Info($"Copied properties to NormalSequence Sequence{seqNum}");
        }
        
        // Get the FlippedSequence template
        var flippedSequence = proj.Get("UI/Templates/RecipeEditPanels/FlippedSequence");
        if (flippedSequence == null)
        {
            Log.Error("FlippedSequence template not found");
            return;
        }
        
        // Get FlippedSequence RecipesEditorSeq -> ScrollView -> ColumnLayout
        var flippedRecipesEditorSeq = flippedSequence.Get<Panel>("RecipesEditorSeq");
        if (flippedRecipesEditorSeq == null)
        {
            Log.Error("FlippedSequence RecipesEditorSeq not found");
            return;
        }
        
        var flippedScrollView = flippedRecipesEditorSeq.Get<ScrollView>("ScrollView");
        if (flippedScrollView == null)
        {
            Log.Error("FlippedSequence ScrollView not found");
            return;
        }
        
        var flippedColumnLayout = flippedScrollView.Get<ColumnLayout>("ColumnLayout");
        if (flippedColumnLayout == null)
        {
            Log.Error("FlippedSequence ColumnLayout not found");
            return;
        }
        
        // Copy to ALL sequences in FlippedSequence (Sequence1 through Sequence32)
        for (int seqNum = 1; seqNum <= 32; seqNum++)
        {
            var sequenceN = flippedColumnLayout.Get<Rectangle>($"Sequence{seqNum}");
            if (sequenceN == null)
            {
                Log.Warning($"FlippedSequence Sequence{seqNum} not found, skipping");
                continue;
            }
            
            // Set Rectangle width to -1 (Auto)
            var sequenceNWidthVar = sequenceN.GetVariable("Width");
            if (sequenceNWidthVar != null)
            {
                sequenceNWidthVar.Value = -1.0;
            }
            
            var sequenceNIndexes = sequenceN.Get<RowLayout>("SequenceIndexes");
            if (sequenceNIndexes == null)
            {
                Log.Warning($"FlippedSequence Sequence{seqNum}/SequenceIndexes not found, skipping");
                continue;
            }
            
            // Set SequenceIndexes width to -1 (Auto)
            var sequenceNIndexesWidthVar = sequenceNIndexes.GetVariable("Width");
            if (sequenceNIndexesWidthVar != null)
            {
                sequenceNIndexesWidthVar.Value = -1.0;
            }
            
            // Apply properties
            ApplySequenceIndexesProperties(sequenceNIndexes, properties, switchProperties);
            Log.Info($"Copied properties to FlippedSequence Sequence{seqNum}");
        }
        
        Log.Info("✅ Finished copying SequenceIndexes properties from NormalSequence Sequence1 to all sequences (NormalSequence and FlippedSequence)");
    }
    
    [ExportMethod]
    public void CopyFlippedSequenceWidths()
    {
        var proj = Project.Current;
        
        // Get the NormalSequence template to use as reference
        var normalSequence = proj.Get("UI/Templates/RecipeEditPanels/NormalSequence");
        if (normalSequence == null)
        {
            Log.Error("NormalSequence template not found");
            return;
        }
        
        // Get NormalSequence RecipesEditorSeq -> ScrollView -> ColumnLayout
        var normalRecipesEditorSeq = normalSequence.Get<Panel>("RecipesEditorSeq");
        if (normalRecipesEditorSeq == null)
        {
            Log.Error("NormalSequence RecipesEditorSeq not found");
            return;
        }
        
        var normalScrollView = normalRecipesEditorSeq.Get<ScrollView>("ScrollView");
        if (normalScrollView == null)
        {
            Log.Error("NormalSequence ScrollView not found");
            return;
        }
        
        var normalColumnLayout = normalScrollView.Get<ColumnLayout>("ColumnLayout");
        if (normalColumnLayout == null)
        {
            Log.Error("NormalSequence ColumnLayout not found");
            return;
        }
        
        // Get NormalSequence Sequence1 as reference
        var normalSequence1 = normalColumnLayout.Get<Rectangle>("Sequence1");
        if (normalSequence1 == null)
        {
            Log.Error("NormalSequence Sequence1 not found");
            return;
        }
        
        // Get SequenceIndexes from NormalSequence Sequence1
        var normalSequence1Indexes = normalSequence1.Get<RowLayout>("SequenceIndexes");
        if (normalSequence1Indexes == null)
        {
            Log.Error("NormalSequence Sequence1/SequenceIndexes not found");
            return;
        }
        
        // Collect all Rectangle widths from NormalSequence Sequence1
        var rectangleWidths = new Dictionary<string, double>();
        CollectRectangleWidths(normalSequence1Indexes, rectangleWidths);
        
        Log.Info($"Found {rectangleWidths.Count} Rectangle widths in NormalSequence Sequence1 (using as reference for FlippedSequence)");
        foreach (var kvp in rectangleWidths)
        {
            Log.Info($"  {kvp.Key}: {kvp.Value}");
        }
        
        // Get the FlippedSequence template
        var flippedSequence = proj.Get("UI/Templates/RecipeEditPanels/FlippedSequence");
        if (flippedSequence == null)
        {
            Log.Error("FlippedSequence template not found");
            return;
        }
        
        // Get FlippedSequence RecipesEditorSeq -> ScrollView -> ColumnLayout
        var flippedRecipesEditorSeq = flippedSequence.Get<Panel>("RecipesEditorSeq");
        if (flippedRecipesEditorSeq == null)
        {
            Log.Error("FlippedSequence RecipesEditorSeq not found");
            return;
        }
        
        var flippedScrollView = flippedRecipesEditorSeq.Get<ScrollView>("ScrollView");
        if (flippedScrollView == null)
        {
            Log.Error("FlippedSequence ScrollView not found");
            return;
        }
        
        var flippedColumnLayout = flippedScrollView.Get<ColumnLayout>("ColumnLayout");
        if (flippedColumnLayout == null)
        {
            Log.Error("FlippedSequence ColumnLayout not found");
            return;
        }
        
        // Copy widths to ALL sequences in FlippedSequence (Sequence1 through Sequence32)
        for (int seqNum = 1; seqNum <= 32; seqNum++)
        {
            var sequenceN = flippedColumnLayout.Get<Rectangle>($"Sequence{seqNum}");
            if (sequenceN == null)
            {
                Log.Warning($"FlippedSequence Sequence{seqNum} not found, skipping");
                continue;
            }
            
            var sequenceNIndexes = sequenceN.Get<RowLayout>("SequenceIndexes");
            if (sequenceNIndexes == null)
            {
                Log.Warning($"FlippedSequence Sequence{seqNum}/SequenceIndexes not found, skipping");
                continue;
            }
            
            // Set SequenceIndexes width to -1 (Auto)
            var sequenceNWidthVar = sequenceNIndexes.GetVariable("Width");
            if (sequenceNWidthVar != null)
            {
                var oldWidth = sequenceNWidthVar.Value != null ? (double)sequenceNWidthVar.Value : 0;
                sequenceNWidthVar.Value = -1.0;
                Log.Info($"Set FlippedSequence Sequence{seqNum}/SequenceIndexes width: {oldWidth} -> -1.0 (Auto)");
            }
            
            // Apply widths to matching Rectangles
            ApplyRectangleWidths(sequenceNIndexes, rectangleWidths);
            Log.Info($"Copied widths to FlippedSequence Sequence{seqNum}");
        }
        
        Log.Info("✅ Finished copying Rectangle widths from NormalSequence Sequence1 to all FlippedSequence sequences (including Sequence1) and setting SequenceIndexes width to -1 (Auto)");
    }
    
    private void CollectRectangleWidths(IUANode parent, Dictionary<string, double> widths)
    {
        if (parent == null)
            return;
            
        foreach (var child in parent.Children)
        {
            // Check if this is a Rectangle
            if (child is Rectangle rectangle)
            {
                var widthVar = rectangle.GetVariable("Width");
                if (widthVar != null && widthVar.Value != null)
                {
                    var width = (double)widthVar.Value;
                    widths[rectangle.BrowseName] = width;
                    Log.Info($"  Collected width for Rectangle '{rectangle.BrowseName}': {width}");
                }
            }
            
            // Recursively search children
            if (child != null && child.Children.Count > 0)
            {
                this.CollectRectangleWidths(child, widths);
            }
        }
    }
    
    private void ApplyRectangleWidths(IUANode parent, Dictionary<string, double> widths)
    {
        if (parent == null)
            return;
            
        foreach (var child in parent.Children)
        {
            // Check if this is a Rectangle
            if (child is Rectangle rectangle)
            {
                if (widths.ContainsKey(rectangle.BrowseName))
                {
                    var widthVar = rectangle.GetVariable("Width");
                    if (widthVar != null)
                    {
                        var oldWidth = widthVar.Value != null ? (double)widthVar.Value : 0;
                        widthVar.Value = widths[rectangle.BrowseName];
                        Log.Info($"    Updated Rectangle '{rectangle.BrowseName}' width: {oldWidth} -> {widths[rectangle.BrowseName]}");
                    }
                }
            }
            
            // Recursively search children
            if (child != null && child.Children.Count > 0)
            {
                this.ApplyRectangleWidths(child, widths);
            }
        }
    }
    
    private void CollectSequenceIndexesProperties(IUANode sequenceIndexes, Dictionary<string, Dictionary<string, double>> properties, Dictionary<string, Dictionary<string, object>> switchProperties)
    {
        if (sequenceIndexes == null)
            return;
        
        // CaseNumber -> Spinbox1: Width and Height
        var caseNumber = sequenceIndexes.Get("CaseNumber");
        if (caseNumber != null)
        {
            var spinbox1 = caseNumber.Get<SpinBox>("SpinBox1");
            if (spinbox1 != null)
            {
                if (!properties.ContainsKey("CaseNumber"))
                    properties["CaseNumber"] = new Dictionary<string, double>();
                
                var widthVar = spinbox1.GetVariable("Width");
                if (widthVar != null && widthVar.Value != null)
                {
                    properties["CaseNumber"]["Width"] = (double)widthVar.Value;
                }
                
                // Height is a property, not a variable
                try
                {
                    properties["CaseNumber"]["Height"] = spinbox1.Height;
                    Log.Info($"  Collected CaseNumber/SpinBox1 Height: {properties["CaseNumber"]["Height"]}");
                }
                catch (Exception ex)
                {
                    Log.Warning($"  CaseNumber/SpinBox1 Height property access failed: {ex.Message}");
                }
            }
        }
        
        // Pick -> Switch1: Width, Height, VerticalAlignment, TopMargin
        var pick = sequenceIndexes.Get("Pick");
        if (pick != null)
        {
            var switch1 = pick.Get<Switch>("Switch1");
            if (switch1 != null)
            {
                if (!properties.ContainsKey("Pick"))
                    properties["Pick"] = new Dictionary<string, double>();
                if (!switchProperties.ContainsKey("Pick"))
                    switchProperties["Pick"] = new Dictionary<string, object>();
                
                var widthVar = switch1.GetVariable("Width");
                if (widthVar != null && widthVar.Value != null)
                {
                    properties["Pick"]["Width"] = (double)widthVar.Value;
                }
                
                // Height is a property, not a variable
                try
                {
                    properties["Pick"]["Height"] = switch1.Height;
                    Log.Info($"  Collected Pick/Switch1 Height: {properties["Pick"]["Height"]}");
                }
                catch (Exception ex)
                {
                    Log.Warning($"  Pick/Switch1 Height property access failed: {ex.Message}");
                }
                
                var topMarginVar = switch1.GetVariable("TopMargin");
                if (topMarginVar != null && topMarginVar.Value != null)
                {
                    properties["Pick"]["TopMargin"] = (double)topMarginVar.Value;
                    Log.Info($"  Collected Pick/Switch1 TopMargin: {properties["Pick"]["TopMargin"]}");
                }
                else
                {
                    Log.Warning("  Pick/Switch1 TopMargin variable not found or is null");
                }
                
                // VerticalAlignment is a property, not a variable
                try
                {
                    switchProperties["Pick"]["VerticalAlignment"] = switch1.VerticalAlignment;
                    Log.Info($"  Collected Pick/Switch1 VerticalAlignment: {switch1.VerticalAlignment}");
                }
                catch (Exception ex)
                {
                    Log.Warning($"  Pick/Switch1 VerticalAlignment property access failed: {ex.Message}");
                }
            }
        }
        
        // Drop -> Switch1: Width, Height, VerticalAlignment, TopMargin
        var drop = sequenceIndexes.Get("Drop");
        if (drop != null)
        {
            var switch1 = drop.Get<Switch>("Switch1");
            if (switch1 != null)
            {
                if (!properties.ContainsKey("Drop"))
                    properties["Drop"] = new Dictionary<string, double>();
                if (!switchProperties.ContainsKey("Drop"))
                    switchProperties["Drop"] = new Dictionary<string, object>();
                
                var widthVar = switch1.GetVariable("Width");
                if (widthVar != null && widthVar.Value != null)
                {
                    properties["Drop"]["Width"] = (double)widthVar.Value;
                }
                
                // Height is a property, not a variable
                try
                {
                    properties["Drop"]["Height"] = switch1.Height;
                    Log.Info($"  Collected Drop/Switch1 Height: {properties["Drop"]["Height"]}");
                }
                catch (Exception ex)
                {
                    Log.Warning($"  Drop/Switch1 Height property access failed: {ex.Message}");
                }
                
                var topMarginVar = switch1.GetVariable("TopMargin");
                if (topMarginVar != null && topMarginVar.Value != null)
                {
                    properties["Drop"]["TopMargin"] = (double)topMarginVar.Value;
                    Log.Info($"  Collected Drop/Switch1 TopMargin: {properties["Drop"]["TopMargin"]}");
                }
                else
                {
                    Log.Warning("  Drop/Switch1 TopMargin variable not found or is null");
                }
                
                // VerticalAlignment is a property, not a variable
                try
                {
                    switchProperties["Drop"]["VerticalAlignment"] = switch1.VerticalAlignment;
                    Log.Info($"  Collected Drop/Switch1 VerticalAlignment: {switch1.VerticalAlignment}");
                }
                catch (Exception ex)
                {
                    Log.Warning($"  Drop/Switch1 VerticalAlignment property access failed: {ex.Message}");
                }
            }
        }
        
        // Rotation -> Switch1: Width, Height, VerticalAlignment, TopMargin
        var rotation = sequenceIndexes.Get("Rotation");
        if (rotation != null)
        {
            var switch1 = rotation.Get<Switch>("Switch1");
            if (switch1 != null)
            {
                if (!properties.ContainsKey("Rotation"))
                    properties["Rotation"] = new Dictionary<string, double>();
                if (!switchProperties.ContainsKey("Rotation"))
                    switchProperties["Rotation"] = new Dictionary<string, object>();
                
                var widthVar = switch1.GetVariable("Width");
                if (widthVar != null && widthVar.Value != null)
                {
                    properties["Rotation"]["Width"] = (double)widthVar.Value;
                }
                
                // Height is a property, not a variable
                try
                {
                    properties["Rotation"]["Height"] = switch1.Height;
                    Log.Info($"  Collected Rotation/Switch1 Height: {properties["Rotation"]["Height"]}");
                }
                catch (Exception ex)
                {
                    Log.Warning($"  Rotation/Switch1 Height property access failed: {ex.Message}");
                }
                
                var topMarginVar = switch1.GetVariable("TopMargin");
                if (topMarginVar != null && topMarginVar.Value != null)
                {
                    properties["Rotation"]["TopMargin"] = (double)topMarginVar.Value;
                    Log.Info($"  Collected Rotation/Switch1 TopMargin: {properties["Rotation"]["TopMargin"]}");
                }
                else
                {
                    Log.Warning("  Rotation/Switch1 TopMargin variable not found or is null");
                }
                
                // VerticalAlignment is a property, not a variable
                try
                {
                    switchProperties["Rotation"]["VerticalAlignment"] = switch1.VerticalAlignment;
                    Log.Info($"  Collected Rotation/Switch1 VerticalAlignment: {switch1.VerticalAlignment}");
                }
                catch (Exception ex)
                {
                    Log.Warning($"  Rotation/Switch1 VerticalAlignment property access failed: {ex.Message}");
                }
            }
        }
        
        // LengthApr -> Spinbox1: Width and Height
        var lengthApr = sequenceIndexes.Get("LengthApr");
        if (lengthApr != null)
        {
            var spinbox1 = lengthApr.Get<SpinBox>("SpinBox1");
            if (spinbox1 != null)
            {
                if (!properties.ContainsKey("LengthApr"))
                    properties["LengthApr"] = new Dictionary<string, double>();
                
                var widthVar = spinbox1.GetVariable("Width");
                if (widthVar != null && widthVar.Value != null)
                {
                    properties["LengthApr"]["Width"] = (double)widthVar.Value;
                }
                
                // Height is a property, not a variable
                try
                {
                    properties["LengthApr"]["Height"] = spinbox1.Height;
                    Log.Info($"  Collected LengthApr/SpinBox1 Height: {properties["LengthApr"]["Height"]}");
                }
                catch (Exception ex)
                {
                    Log.Warning($"  LengthApr/SpinBox1 Height property access failed: {ex.Message}");
                }
            }
        }
        
        // WidthApr -> Spinbox1: Width and Height
        var widthApr = sequenceIndexes.Get("WidthApr");
        if (widthApr != null)
        {
            var spinbox1 = widthApr.Get<SpinBox>("SpinBox1");
            if (spinbox1 != null)
            {
                if (!properties.ContainsKey("WidthApr"))
                    properties["WidthApr"] = new Dictionary<string, double>();
                
                var widthVar = spinbox1.GetVariable("Width");
                if (widthVar != null && widthVar.Value != null)
                {
                    properties["WidthApr"]["Width"] = (double)widthVar.Value;
                }
                
                // Height is a property, not a variable
                try
                {
                    properties["WidthApr"]["Height"] = spinbox1.Height;
                    Log.Info($"  Collected WidthApr/SpinBox1 Height: {properties["WidthApr"]["Height"]}");
                }
                catch (Exception ex)
                {
                    Log.Warning($"  WidthApr/SpinBox1 Height property access failed: {ex.Message}");
                }
            }
        }
        
        // HeightApr -> Spinbox1: Width and Height
        var heightApr = sequenceIndexes.Get("HeightApr");
        if (heightApr != null)
        {
            var spinbox1 = heightApr.Get<SpinBox>("SpinBox1");
            if (spinbox1 != null)
            {
                if (!properties.ContainsKey("HeightApr"))
                    properties["HeightApr"] = new Dictionary<string, double>();
                
                var widthVar = spinbox1.GetVariable("Width");
                if (widthVar != null && widthVar.Value != null)
                {
                    properties["HeightApr"]["Width"] = (double)widthVar.Value;
                }
                
                // Height is a property, not a variable
                try
                {
                    properties["HeightApr"]["Height"] = spinbox1.Height;
                    Log.Info($"  Collected HeightApr/SpinBox1 Height: {properties["HeightApr"]["Height"]}");
                }
                catch (Exception ex)
                {
                    Log.Warning($"  HeightApr/SpinBox1 Height property access failed: {ex.Message}");
                }
            }
        }
        
        // RetZ -> Spinbox1: Width and Height
        var retZ = sequenceIndexes.Get("RetZ");
        if (retZ != null)
        {
            var spinbox1 = retZ.Get<SpinBox>("SpinBox1");
            if (spinbox1 != null)
            {
                if (!properties.ContainsKey("RetZ"))
                    properties["RetZ"] = new Dictionary<string, double>();
                
                var widthVar = spinbox1.GetVariable("Width");
                if (widthVar != null && widthVar.Value != null)
                {
                    properties["RetZ"]["Width"] = (double)widthVar.Value;
                }
                
                // Height is a property, not a variable
                try
                {
                    properties["RetZ"]["Height"] = spinbox1.Height;
                    Log.Info($"  Collected RetZ/SpinBox1 Height: {properties["RetZ"]["Height"]}");
                }
                catch (Exception ex)
                {
                    Log.Warning($"  RetZ/SpinBox1 Height property access failed: {ex.Message}");
                }
            }
        }
    }
    
    private void ApplySequenceIndexesProperties(IUANode sequenceIndexes, Dictionary<string, Dictionary<string, double>> properties, Dictionary<string, Dictionary<string, object>> switchProperties)
    {
        if (sequenceIndexes == null || properties == null)
            return;
        
        // CaseNumber -> Spinbox1: Width and Height
        if (properties.ContainsKey("CaseNumber"))
        {
            var caseNumber = sequenceIndexes.Get("CaseNumber");
            if (caseNumber != null)
            {
                var spinbox1 = caseNumber.Get<SpinBox>("SpinBox1");
                if (spinbox1 != null)
                {
                    if (properties["CaseNumber"].ContainsKey("Width"))
                    {
                        var widthVar = spinbox1.GetVariable("Width");
                        if (widthVar != null)
                        {
                            widthVar.Value = properties["CaseNumber"]["Width"];
                        }
                    }
                    
                    if (properties["CaseNumber"].ContainsKey("Height"))
                    {
                        try
                        {
                            var oldHeight = spinbox1.Height;
                            spinbox1.Height = (float)properties["CaseNumber"]["Height"];
                            Log.Info($"    Applied CaseNumber/SpinBox1 Height: {oldHeight} -> {properties["CaseNumber"]["Height"]}");
                        }
                        catch (Exception ex)
                        {
                            Log.Warning($"    CaseNumber/SpinBox1 Height property set failed: {ex.Message}");
                        }
                    }
                    else
                    {
                        Log.Warning("    CaseNumber/SpinBox1 Height not in collected properties");
                    }
                }
            }
        }
        
        // Pick -> Switch1: Width, Height, VerticalAlignment, TopMargin
        if (properties.ContainsKey("Pick"))
        {
            var pick = sequenceIndexes.Get("Pick");
            if (pick != null)
            {
                var switch1 = pick.Get<Switch>("Switch1");
                if (switch1 != null)
                {
                    if (properties["Pick"].ContainsKey("Width"))
                    {
                        var widthVar = switch1.GetVariable("Width");
                        if (widthVar != null)
                        {
                            widthVar.Value = properties["Pick"]["Width"];
                        }
                    }
                    
                    if (properties["Pick"].ContainsKey("Height"))
                    {
                        try
                        {
                            var oldHeight = switch1.Height;
                            switch1.Height = (float)properties["Pick"]["Height"];
                            Log.Info($"    Applied Pick/Switch1 Height: {oldHeight} -> {properties["Pick"]["Height"]}");
                        }
                        catch (Exception ex)
                        {
                            Log.Warning($"    Pick/Switch1 Height property set failed: {ex.Message}");
                        }
                    }
                    else
                    {
                        Log.Warning("    Pick/Switch1 Height not in collected properties");
                    }
                    
                    if (properties["Pick"].ContainsKey("TopMargin"))
                    {
                        var topMarginVar = switch1.GetVariable("TopMargin");
                        if (topMarginVar != null)
                        {
                            var oldTopMargin = topMarginVar.Value != null ? (double)topMarginVar.Value : 0;
                            topMarginVar.Value = properties["Pick"]["TopMargin"];
                            Log.Info($"    Applied Pick/Switch1 TopMargin: {oldTopMargin} -> {properties["Pick"]["TopMargin"]}");
                        }
                        else
                        {
                            Log.Warning("    Pick/Switch1 TopMargin variable not found when applying");
                        }
                    }
                    else
                    {
                        Log.Warning("    Pick/Switch1 TopMargin not in collected properties");
                    }
                    
                    if (switchProperties.ContainsKey("Pick") && switchProperties["Pick"].ContainsKey("VerticalAlignment"))
                    {
                        try
                        {
                            var oldVal = switch1.VerticalAlignment;
                            switch1.VerticalAlignment = (VerticalAlignment)switchProperties["Pick"]["VerticalAlignment"];
                            Log.Info($"    Applied Pick/Switch1 VerticalAlignment: {oldVal} -> {switchProperties["Pick"]["VerticalAlignment"]}");
                        }
                        catch (Exception ex)
                        {
                            Log.Warning($"    Pick/Switch1 VerticalAlignment property set failed: {ex.Message}");
                        }
                    }
                    else
                    {
                        Log.Warning("    Pick/Switch1 VerticalAlignment not in collected switchProperties");
                    }
                }
            }
        }
        
        // Drop -> Switch1: Width, Height, VerticalAlignment, TopMargin
        if (properties.ContainsKey("Drop"))
        {
            var drop = sequenceIndexes.Get("Drop");
            if (drop != null)
            {
                var switch1 = drop.Get<Switch>("Switch1");
                if (switch1 != null)
                {
                    if (properties["Drop"].ContainsKey("Width"))
                    {
                        var widthVar = switch1.GetVariable("Width");
                        if (widthVar != null)
                        {
                            widthVar.Value = properties["Drop"]["Width"];
                        }
                    }
                    
                    if (properties["Drop"].ContainsKey("Height"))
                    {
                        try
                        {
                            var oldHeight = switch1.Height;
                            switch1.Height = (float)properties["Drop"]["Height"];
                            Log.Info($"    Applied Drop/Switch1 Height: {oldHeight} -> {properties["Drop"]["Height"]}");
                        }
                        catch (Exception ex)
                        {
                            Log.Warning($"    Drop/Switch1 Height property set failed: {ex.Message}");
                        }
                    }
                    else
                    {
                        Log.Warning("    Drop/Switch1 Height not in collected properties");
                    }
                    
                    if (properties["Drop"].ContainsKey("TopMargin"))
                    {
                        var topMarginVar = switch1.GetVariable("TopMargin");
                        if (topMarginVar != null)
                        {
                            var oldTopMargin = topMarginVar.Value != null ? (double)topMarginVar.Value : 0;
                            topMarginVar.Value = properties["Drop"]["TopMargin"];
                            Log.Info($"    Applied Drop/Switch1 TopMargin: {oldTopMargin} -> {properties["Drop"]["TopMargin"]}");
                        }
                        else
                        {
                            Log.Warning("    Drop/Switch1 TopMargin variable not found when applying");
                        }
                    }
                    else
                    {
                        Log.Warning("    Drop/Switch1 TopMargin not in collected properties");
                    }
                    
                    if (switchProperties.ContainsKey("Drop") && switchProperties["Drop"].ContainsKey("VerticalAlignment"))
                    {
                        try
                        {
                            var oldVal = switch1.VerticalAlignment;
                            switch1.VerticalAlignment = (VerticalAlignment)switchProperties["Drop"]["VerticalAlignment"];
                            Log.Info($"    Applied Drop/Switch1 VerticalAlignment: {oldVal} -> {switchProperties["Drop"]["VerticalAlignment"]}");
                        }
                        catch (Exception ex)
                        {
                            Log.Warning($"    Drop/Switch1 VerticalAlignment property set failed: {ex.Message}");
                        }
                    }
                    else
                    {
                        Log.Warning("    Drop/Switch1 VerticalAlignment not in collected switchProperties");
                    }
                }
            }
        }
        
        // Rotation -> Switch1: Width, Height, VerticalAlignment, TopMargin
        if (properties.ContainsKey("Rotation"))
        {
            var rotation = sequenceIndexes.Get("Rotation");
            if (rotation != null)
            {
                var switch1 = rotation.Get<Switch>("Switch1");
                if (switch1 != null)
                {
                    if (properties["Rotation"].ContainsKey("Width"))
                    {
                        var widthVar = switch1.GetVariable("Width");
                        if (widthVar != null)
                        {
                            widthVar.Value = properties["Rotation"]["Width"];
                        }
                    }
                    
                    if (properties["Rotation"].ContainsKey("Height"))
                    {
                        try
                        {
                            var oldHeight = switch1.Height;
                            switch1.Height = (float)properties["Rotation"]["Height"];
                            Log.Info($"    Applied Rotation/Switch1 Height: {oldHeight} -> {properties["Rotation"]["Height"]}");
                        }
                        catch (Exception ex)
                        {
                            Log.Warning($"    Rotation/Switch1 Height property set failed: {ex.Message}");
                        }
                    }
                    else
                    {
                        Log.Warning("    Rotation/Switch1 Height not in collected properties");
                    }
                    
                    if (properties["Rotation"].ContainsKey("TopMargin"))
                    {
                        var topMarginVar = switch1.GetVariable("TopMargin");
                        if (topMarginVar != null)
                        {
                            var oldTopMargin = topMarginVar.Value != null ? (double)topMarginVar.Value : 0;
                            topMarginVar.Value = properties["Rotation"]["TopMargin"];
                            Log.Info($"    Applied Rotation/Switch1 TopMargin: {oldTopMargin} -> {properties["Rotation"]["TopMargin"]}");
                        }
                        else
                        {
                            Log.Warning("    Rotation/Switch1 TopMargin variable not found when applying");
                        }
                    }
                    else
                    {
                        Log.Warning("    Rotation/Switch1 TopMargin not in collected properties");
                    }
                    
                    if (switchProperties.ContainsKey("Rotation") && switchProperties["Rotation"].ContainsKey("VerticalAlignment"))
                    {
                        try
                        {
                            var oldVal = switch1.VerticalAlignment;
                            switch1.VerticalAlignment = (VerticalAlignment)switchProperties["Rotation"]["VerticalAlignment"];
                            Log.Info($"    Applied Rotation/Switch1 VerticalAlignment: {oldVal} -> {switchProperties["Rotation"]["VerticalAlignment"]}");
                        }
                        catch (Exception ex)
                        {
                            Log.Warning($"    Rotation/Switch1 VerticalAlignment property set failed: {ex.Message}");
                        }
                    }
                    else
                    {
                        Log.Warning("    Rotation/Switch1 VerticalAlignment not in collected switchProperties");
                    }
                }
            }
        }
        
        // LengthApr -> Spinbox1: Width and Height
        if (properties.ContainsKey("LengthApr"))
        {
            var lengthApr = sequenceIndexes.Get("LengthApr");
            if (lengthApr != null)
            {
                var spinbox1 = lengthApr.Get<SpinBox>("SpinBox1");
                if (spinbox1 != null)
                {
                    if (properties["LengthApr"].ContainsKey("Width"))
                    {
                        var widthVar = spinbox1.GetVariable("Width");
                        if (widthVar != null)
                        {
                            widthVar.Value = properties["LengthApr"]["Width"];
                        }
                    }
                    
                    if (properties["LengthApr"].ContainsKey("Height"))
                    {
                        try
                        {
                            var oldHeight = spinbox1.Height;
                            spinbox1.Height = (float)properties["LengthApr"]["Height"];
                            Log.Info($"    Applied LengthApr/SpinBox1 Height: {oldHeight} -> {properties["LengthApr"]["Height"]}");
                        }
                        catch (Exception ex)
                        {
                            Log.Warning($"    LengthApr/SpinBox1 Height property set failed: {ex.Message}");
                        }
                    }
                }
            }
        }
        
        // WidthApr -> Spinbox1: Width and Height
        if (properties.ContainsKey("WidthApr"))
        {
            var widthApr = sequenceIndexes.Get("WidthApr");
            if (widthApr != null)
            {
                var spinbox1 = widthApr.Get<SpinBox>("SpinBox1");
                if (spinbox1 != null)
                {
                    if (properties["WidthApr"].ContainsKey("Width"))
                    {
                        var widthVar = spinbox1.GetVariable("Width");
                        if (widthVar != null)
                        {
                            widthVar.Value = properties["WidthApr"]["Width"];
                        }
                    }
                    
                    if (properties["WidthApr"].ContainsKey("Height"))
                    {
                        try
                        {
                            var oldHeight = spinbox1.Height;
                            spinbox1.Height = (float)properties["WidthApr"]["Height"];
                            Log.Info($"    Applied WidthApr/SpinBox1 Height: {oldHeight} -> {properties["WidthApr"]["Height"]}");
                        }
                        catch (Exception ex)
                        {
                            Log.Warning($"    WidthApr/SpinBox1 Height property set failed: {ex.Message}");
                        }
                    }
                }
            }
        }
        
        // HeightApr -> Spinbox1: Width and Height
        if (properties.ContainsKey("HeightApr"))
        {
            var heightApr = sequenceIndexes.Get("HeightApr");
            if (heightApr != null)
            {
                var spinbox1 = heightApr.Get<SpinBox>("SpinBox1");
                if (spinbox1 != null)
                {
                    if (properties["HeightApr"].ContainsKey("Width"))
                    {
                        var widthVar = spinbox1.GetVariable("Width");
                        if (widthVar != null)
                        {
                            widthVar.Value = properties["HeightApr"]["Width"];
                        }
                    }
                    
                    if (properties["HeightApr"].ContainsKey("Height"))
                    {
                        try
                        {
                            var oldHeight = spinbox1.Height;
                            spinbox1.Height = (float)properties["HeightApr"]["Height"];
                            Log.Info($"    Applied HeightApr/SpinBox1 Height: {oldHeight} -> {properties["HeightApr"]["Height"]}");
                        }
                        catch (Exception ex)
                        {
                            Log.Warning($"    HeightApr/SpinBox1 Height property set failed: {ex.Message}");
                        }
                    }
                }
            }
        }
        
        // RetZ -> Spinbox1: Width and Height
        if (properties.ContainsKey("RetZ"))
        {
            var retZ = sequenceIndexes.Get("RetZ");
            if (retZ != null)
            {
                var spinbox1 = retZ.Get<SpinBox>("SpinBox1");
                if (spinbox1 != null)
                {
                    if (properties["RetZ"].ContainsKey("Width"))
                    {
                        var widthVar = spinbox1.GetVariable("Width");
                        if (widthVar != null)
                        {
                            widthVar.Value = properties["RetZ"]["Width"];
                        }
                    }
                    
                    if (properties["RetZ"].ContainsKey("Height"))
                    {
                        try
                        {
                            var oldHeight = spinbox1.Height;
                            spinbox1.Height = (float)properties["RetZ"]["Height"];
                            Log.Info($"    Applied RetZ/SpinBox1 Height: {oldHeight} -> {properties["RetZ"]["Height"]}");
                        }
                        catch (Exception ex)
                        {
                            Log.Warning($"    RetZ/SpinBox1 Height property set failed: {ex.Message}");
                        }
                    }
                }
            }
        }
    }
}
