#region Using directives
using System;
using System.Globalization;
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

public class Add_Sub_Cases : BaseNetLogic
{
    private const int MinCount = 1;
    private const int MaxCount = 62;

    private const string BaseBoxTemplateName = "BaseBox1";
    private const string CaseLocationsTemplateName = "CaseLocations1";

    private const string CustomPatternPath = "Model/RecipeEdit/CustomPattern";

    public override void Start()
    {
    }

    public override void Stop()
    {
    }

    [ExportMethod]
    public void Add()
    {
        var count = GetCurrentCount();
        if (count < 0 || count >= MaxCount)
            return;

        int n = count + 1;

        var baseBox1 = FindNodeRecursive(LogicObject, BaseBoxTemplateName) as IUAObject;
        var caseLocations1 = FindNodeRecursive(LogicObject, CaseLocationsTemplateName) as IUAObject;

        if (baseBox1 == null || caseLocations1 == null)
        {
            Log.Error(nameof(Add_Sub_Cases), $"Templates '{BaseBoxTemplateName}' or '{CaseLocationsTemplateName}' not found.");
            return;
        }

        var baseBoxContainer = baseBox1.Parent;
        var caseLocationsContainer = caseLocations1.Parent;

        if (baseBoxContainer == null || caseLocationsContainer == null)
        {
            Log.Error(nameof(Add_Sub_Cases), "Could not resolve BaseBox or CaseLocations container.");
            return;
        }

        try
        {
            if (baseBoxContainer.Get($"BaseBox{n}") == null)
            {
                var newBaseBox = InformationModel.MakeObject($"BaseBox{n}", baseBox1.ObjectType.NodeId);
                baseBoxContainer.Add(newBaseBox);
                var idxVar = newBaseBox.GetVariable("Index");
                if (idxVar != null)
                    idxVar.Value = (short)n;
            }

            if (caseLocationsContainer.Get($"CaseLocations{n}") == null)
            {
                var newCaseLocations = InformationModel.MakeObject($"CaseLocations{n}", caseLocations1.ObjectType.NodeId);
                caseLocationsContainer.Add(newCaseLocations);
                var idxVar = newCaseLocations.GetVariable("Index");
                if (idxVar != null)
                    idxVar.Value = (short)n;
            }

            InitializeArraysForIndex(n);

            var numCasesVar = GetNumCasesCustomVariable();
            if (numCasesVar != null)
                numCasesVar.Value = n;

            SetCaseSelected(n);
        }
        catch (Exception ex)
        {
            Log.Error(nameof(Add_Sub_Cases), $"Add failed: {ex.Message}");
        }
    }

    [ExportMethod]
    public void Subtract()
    {
        var count = GetCurrentCount();
        if (count <= MinCount)
            return;

        var baseBox1 = FindNodeRecursive(LogicObject, BaseBoxTemplateName);
        var caseLocations1 = FindNodeRecursive(LogicObject, CaseLocationsTemplateName);
        var baseBoxContainer = baseBox1?.Parent;
        var caseLocationsContainer = caseLocations1?.Parent;

        if (baseBoxContainer == null || caseLocationsContainer == null)
            return;

        var baseBoxToRemove = baseBoxContainer.Get($"BaseBox{count}");
        var caseLocationsToRemove = caseLocationsContainer.Get($"CaseLocations{count}");

        if (baseBoxToRemove != null)
            baseBoxContainer.Remove(baseBoxToRemove);
        if (caseLocationsToRemove != null)
            caseLocationsContainer.Remove(caseLocationsToRemove);

        var numCasesVar = GetNumCasesCustomVariable();
        if (numCasesVar != null)
            numCasesVar.Value = count - 1;

        SetCaseSelected(count - 1);
    }

    private void SetCaseSelected(int value)
    {
        var caseSelectedVar = Project.Current?.GetVariable($"{CustomPatternPath}/CaseSelected");
        if (caseSelectedVar != null)
            caseSelectedVar.Value = value;
    }

    private IUANode FindNodeRecursive(IUANode root, string browseName)
    {
        if (root == null)
            return null;

        var current = root;
        while (current != null)
        {
            var found = FindInSubtree(current, browseName);
            if (found != null)
                return found;
            current = current.Owner;
        }

        return null;
    }

    private IUANode FindInSubtree(IUANode node, string browseName)
    {
        if (node == null)
            return null;
        if (string.Equals(node.BrowseName, browseName, StringComparison.OrdinalIgnoreCase))
            return node;
        foreach (var ch in node.Children)
        {
            var found = FindInSubtree(ch, browseName);
            if (found != null)
                return found;
        }
        return null;
    }

    private int GetCurrentCount()
    {
        var numCasesVar = GetNumCasesCustomVariable();
        if (numCasesVar == null)
            return -1;
        return ToInt(numCasesVar.Value);
    }

    private IUAVariable GetNumCasesCustomVariable()
    {
        return Project.Current?.GetVariable($"{CustomPatternPath}/NumCasesCustom");
    }

    private void InitializeArraysForIndex(int n)
    {
        try
        {
            var customPattern = Project.Current?.Get($"{CustomPatternPath}");
            if (customPattern == null)
                return;

            SetArrayElement(customPattern.GetVariable("WidthArray"), n, ToFloat(customPattern.GetVariable("Width").Value));
            SetArrayElement(customPattern.GetVariable("HeightArray"), n, ToFloat(customPattern.GetVariable("Height").Value));
            CopyArrayElement(customPattern.GetVariable("TopMargin"), 1, n);
            CopyArrayElement(customPattern.GetVariable("LeftMargin"), 1, n);
            CopyArrayElement(customPattern.GetVariable("Rot90"), 1, n);
        }
        catch (Exception ex)
        {
            Log.Warning(nameof(Add_Sub_Cases), $"Array init for index {n} failed: {ex.Message}");
        }
    }

    private void SetArrayElement(IUAVariable arrayVar, int index, float value)
    {
        if (arrayVar == null)
            return;
        try
        {
            object arrVal = arrayVar.Value;
            if (arrVal == null) return;
            if (arrVal is UAValue uav) arrVal = uav.Value;
            if (arrVal is float[] floatArr && floatArr.Length > index)
            {
                floatArr[index] = value;
                arrayVar.SetValue(floatArr);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(nameof(Add_Sub_Cases), $"SetArrayElement failed: {ex.Message}");
        }
    }

    private static float ToFloat(object val)
    {
        if (val == null) return 0f;
        if (val is UAValue uav) val = uav.Value;
        if (val is float f) return f;
        if (val is double d) return (float)d;
        return 0f;
    }

    private void CopyArrayElement(IUAVariable arrayVar, int fromIndex, int toIndex)
    {
        if (arrayVar == null || fromIndex == toIndex)
            return;

        try
        {
            object val = arrayVar.Value;
            if (val == null)
                return;

            if (val is UAValue uav)
                val = uav.Value;

            if (val is float[] floatArr && floatArr.Length > Math.Max(fromIndex, toIndex))
            {
                floatArr[toIndex] = floatArr[fromIndex];
                arrayVar.SetValue(floatArr);
                return;
            }

            if (val is bool[] boolArr && boolArr.Length > Math.Max(fromIndex, toIndex))
            {
                boolArr[toIndex] = boolArr[fromIndex];
                arrayVar.SetValue(boolArr);
                return;
            }

            if (val is int[] intArr && intArr.Length > Math.Max(fromIndex, toIndex))
            {
                intArr[toIndex] = intArr[fromIndex];
                arrayVar.SetValue(intArr);
                return;
            }

            var arr = val as Array;
            if (arr != null && arr.Length > Math.Max(fromIndex, toIndex))
            {
                var elem = arr.GetValue(fromIndex);
                arr.SetValue(elem, toIndex);
                arrayVar.SetValue(arr);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(nameof(Add_Sub_Cases), $"CopyArrayElement failed: {ex.Message}");
        }
    }

    private static int ToInt(object uaValue)
    {
        if (uaValue == null)
            return 0;
        try
        {
            if (uaValue is UAValue uav)
                uaValue = uav.Value;
            if (uaValue == null)
                return 0;
            if (uaValue is int i) return i;
            if (uaValue is short s) return s;
            if (uaValue is long l) return (int)l;
            if (uaValue is double d) return (int)d;
            if (uaValue is float f) return (int)f;
            if (uaValue is string str && int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var p))
                return p;
            return Convert.ToInt32(uaValue, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0;
        }
    }
}
