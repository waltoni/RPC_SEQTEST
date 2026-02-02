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

public class CopySequenceWidths : BaseNetLogic
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
}

