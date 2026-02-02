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

public class NormalCustomScript : BaseNetLogic
{
    private const int MinCount = 1;
    private const int MaxCount = 62;

    private const string BaseBoxTemplateName = "BaseBox1";
    private const string CaseLocationsTemplateName = "CaseLocations1";

    private const string CustomPatternPath = "Model/RecipeEdit/CustomPatternNormal";

    private DelayedTask rebuildTask;

    public override void Start()
    {
        // Rebuild UI elements when popup opens (e.g. after close/reopen).
        // NumCasesCustom persists; BaseBox/CaseLocations are recreated from YAML with only instance 1.
        if (rebuildTask != null)
        {
            rebuildTask.Dispose();
            rebuildTask = null;
        }
        rebuildTask = new DelayedTask(Rebuild, 100, LogicObject);
        rebuildTask.Start();
    }

    public override void Stop()
    {
        if (rebuildTask != null)
        {
            rebuildTask.Dispose();
            rebuildTask = null;
        }
    }

    private void Rebuild()
    {
        var targetCount = GetCurrentCount();
        if (targetCount < MinCount || targetCount > MaxCount)
            return;

        var baseBox1 = FindNodeRecursive(LogicObject, BaseBoxTemplateName) as IUAObject;
        var caseLocations1 = FindNodeRecursive(LogicObject, CaseLocationsTemplateName) as IUAObject;

        if (baseBox1 == null || caseLocations1 == null)
            return;

        var baseBoxContainer = baseBox1.Parent;
        var caseLocationsContainer = caseLocations1.Parent;

        if (baseBoxContainer == null || caseLocationsContainer == null)
            return;

        int currentCount = CountExistingElements(baseBoxContainer, "BaseBox");

        for (int n = currentCount + 1; n <= targetCount; n++)
        {
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

                // Do NOT call InitializeArraysForIndex here - model arrays already have persisted data.
            }
            catch (Exception ex)
            {
                Log.Warning(nameof(NormalCustomScript), $"Rebuild: failed for index {n}: {ex.Message}");
            }
        }

        var sel = GetCaseSelected();
        SetCaseSelected(sel >= 1 && sel <= targetCount ? sel : 1);
    }

    private static int CountExistingElements(IUANode container, string prefix)
    {
        if (container == null) return 0;
        int max = 0;
        foreach (var child in container.Children)
        {
            var name = child?.BrowseName ?? "";
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && name.Length > prefix.Length)
            {
                var suffix = name.Substring(prefix.Length);
                if (int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > max)
                    max = n;
            }
        }
        return max;
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
            Log.Error(nameof(NormalCustomScript), $"Templates '{BaseBoxTemplateName}' or '{CaseLocationsTemplateName}' not found.");
            return;
        }

        var baseBoxContainer = baseBox1.Parent;
        var caseLocationsContainer = caseLocations1.Parent;

        if (baseBoxContainer == null || caseLocationsContainer == null)
        {
            Log.Error(nameof(NormalCustomScript), "Could not resolve BaseBox or CaseLocations container.");
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
            Log.Error(nameof(NormalCustomScript), $"Add failed: {ex.Message}");
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

    [ExportMethod]
    public void Snap()
    {
        Log.Info(nameof(NormalCustomScript), "Snap() called");

        // Boundary check runs first on every drop (priority over snap)
        ApplyBoundaryCheck();

        if (!GetSnapMode())
        {
            Log.Info(nameof(NormalCustomScript), "Snap: SnapMode off, returning");
            return;
        }

        var cp = Project.Current?.Get(CustomPatternPath);
        if (cp == null)
        {
            Log.Warning(nameof(NormalCustomScript), "Snap: CustomPattern not found");
            return;
        }

        int i = GetCaseSelected();
        int numCases = GetCurrentCount();
        if (i < 1 || i > numCases || numCases < 2)
        {
            Log.Info(nameof(NormalCustomScript), $"Snap: early return (CaseSelected={i}, NumCases={numCases})");
            return;
        }

        float currTop = GetArrayFloat(cp.GetVariable("TopMargin"), i);
        float currLeft = GetArrayFloat(cp.GetVariable("LeftMargin"), i);
        float myW = GetArrayFloat(cp.GetVariable("WidthArray"), i);
        float myH = GetArrayFloat(cp.GetVariable("HeightArray"), i);
        bool myRot = GetArrayBool(cp.GetVariable("Rot90"), i);

        float bestLeft = currLeft, bestTop = currTop;
        float bestDistSq = float.MaxValue;

        for (int j = 1; j <= numCases; j++)
        {
            if (j == i) continue;

            float jLeft = GetArrayFloat(cp.GetVariable("LeftMargin"), j);
            float jTop = GetArrayFloat(cp.GetVariable("TopMargin"), j);
            float jW = GetArrayFloat(cp.GetVariable("WidthArray"), j);
            float jH = GetArrayFloat(cp.GetVariable("HeightArray"), j);
            bool jRot = GetArrayBool(cp.GetVariable("Rot90"), j);

            float jExtW = jW;
            float jExtH = jH;
            float myExtW = myRot ? myH : myW;
            float myExtH = myRot ? myW : myH;

            float[] candidates = {
                jLeft + jExtW, jTop,           /* right of j */
                jLeft, jTop + jExtH,           /* bottom of j */
                jLeft - myExtW, jTop,          /* left of j */
                jLeft, jTop - myExtH           /* above j */
            };

            for (int k = 0; k < 4; k++)
            {
                float snapLeft = candidates[k * 2];
                float snapTop = candidates[k * 2 + 1];
                float dx = currLeft - snapLeft;
                float dy = currTop - snapTop;
                float distSq = dx * dx + dy * dy;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestLeft = snapLeft;
                    bestTop = snapTop;
                }
            }
        }

        SetArrayElement(cp.GetVariable("LeftMargin"), i, bestLeft);
        SetArrayElement(cp.GetVariable("TopMargin"), i, bestTop);

        var pxVar = cp.GetVariable("PixelScaling");
        float pxPerInch = pxVar != null ? ToFloat(pxVar.Value) : 12f;
        if (pxPerInch > 0)
        {
            var xHoldVar = cp.GetVariable("XHold");
            var yHoldVar = cp.GetVariable("YHold");
            if (xHoldVar != null) xHoldVar.Value = bestTop / pxPerInch;
            if (yHoldVar != null) yHoldVar.Value = bestLeft / pxPerInch;
        }

        Log.Info(nameof(NormalCustomScript), $"Snap: applied for case {i} -> ({bestLeft:F1}, {bestTop:F1})");

        // Boundary check again after snap (snap position may be outside bounds)
        ApplyBoundaryCheck();
    }

    private void ApplyBoundaryCheck()
    {
        var cp = Project.Current?.Get(CustomPatternPath);
        if (cp == null) return;

        int i = GetCaseSelected();
        int numCases = GetCurrentCount();
        if (i < 1 || i > numCases) return;

        var xLimitNegVar = cp.GetVariable("XLimitNeg");
        var xLimitPosVar = cp.GetVariable("XLimitPos");
        var yLimitNegVar = cp.GetVariable("YLimitNeg");
        var yLimitPosVar = cp.GetVariable("YLimitPos");
        if (xLimitNegVar == null || xLimitPosVar == null || yLimitNegVar == null || yLimitPosVar == null)
            return;

        var pxVar = cp.GetVariable("PixelScaling");
        float pxPerInch = pxVar != null ? ToFloat(pxVar.Value) : 12f;
        if (pxPerInch <= 0) return;

        // Limits are in inches; TopMargin/LeftMargin are in pixels - convert limits to pixels
        float xLimitNeg = ToFloat(xLimitNegVar.Value) * pxPerInch;
        float xLimitPos = ToFloat(xLimitPosVar.Value) * pxPerInch;
        float yLimitNeg = ToFloat(yLimitNegVar.Value) * pxPerInch;
        float yLimitPos = ToFloat(yLimitPosVar.Value) * pxPerInch;

        float currLeft = GetArrayFloat(cp.GetVariable("LeftMargin"), i);
        float currTop = GetArrayFloat(cp.GetVariable("TopMargin"), i);
        float myW = GetArrayFloat(cp.GetVariable("WidthArray"), i);
        float myH = GetArrayFloat(cp.GetVariable("HeightArray"), i);
        bool myRot = GetArrayBool(cp.GetVariable("Rot90"), i);
        float extW = myRot ? myH : myW;
        float extH = myRot ? myW : myH;

        // X = TopMargin (vertical), Y = LeftMargin (horizontal)
        // Limits are pre-calculated; use directly (no subtraction)
        float leftMin = yLimitNeg;
        float leftMax = yLimitPos;
        float topMin = xLimitNeg;
        float topMax = xLimitPos;

        float clampedLeft = Math.Max(leftMin, Math.Min(leftMax, currLeft));
        float clampedTop = Math.Max(topMin, Math.Min(topMax, currTop));

        if (Math.Abs(clampedLeft - currLeft) > 0.001f || Math.Abs(clampedTop - currTop) > 0.001f)
        {
            SetArrayElement(cp.GetVariable("LeftMargin"), i, clampedLeft);
            SetArrayElement(cp.GetVariable("TopMargin"), i, clampedTop);

            if (pxPerInch > 0)
            {
                var xHoldVar = cp.GetVariable("XHold");
                var yHoldVar = cp.GetVariable("YHold");
                if (xHoldVar != null) xHoldVar.Value = clampedTop / pxPerInch;
                if (yHoldVar != null) yHoldVar.Value = clampedLeft / pxPerInch;
            }
            Log.Info(nameof(NormalCustomScript), $"BoundaryCheck: case {i} clamped from ({currLeft:F0},{currTop:F0}) to ({clampedLeft:F0},{clampedTop:F0}) | limits px: left[{leftMin:F0},{leftMax:F0}] top[{topMin:F0},{topMax:F0}] extW={extW:F0} extH={extH:F0}");
        }
    }

    private bool GetSnapMode()
    {
        var v = Project.Current?.GetVariable($"{CustomPatternPath}/SnapMode");
        if (v?.Value == null) return false;
        object val = v.Value;
        if (val is UAValue uav) val = uav.Value;
        return val is bool b && b;
    }

    private int GetCaseSelected()
    {
        var v = Project.Current?.GetVariable($"{CustomPatternPath}/CaseSelected");
        return v != null ? ToInt(v.Value) : 0;
    }

    private static float GetArrayFloat(IUAVariable arr, int index)
    {
        if (arr == null || index < 0) return 0f;
        object val = arr.Value;
        if (val == null) return 0f;
        if (val is UAValue uav) val = uav.Value;
        if (val is float[] fa && fa.Length > index) return fa[index];
        if (val is Array a && a.Length > index)
        {
            var e = a.GetValue(index);
            if (e is UAValue uv) e = uv.Value;
            if (e is float f) return f;
            if (e is double d) return (float)d;
        }
        return 0f;
    }

    private static bool GetArrayBool(IUAVariable arr, int index)
    {
        if (arr == null || index < 0) return false;
        object val = arr.Value;
        if (val == null) return false;
        if (val is UAValue uav) val = uav.Value;
        if (val is bool[] ba && ba.Length > index) return ba[index];
        if (val is Array a && a.Length > index)
        {
            var e = a.GetValue(index);
            if (e is UAValue uv) e = uv.Value;
            if (e is bool b) return b;
        }
        return false;
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
            Log.Warning(nameof(NormalCustomScript), $"Array init for index {n} failed: {ex.Message}");
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
            Log.Warning(nameof(NormalCustomScript), $"SetArrayElement failed: {ex.Message}");
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
            Log.Warning(nameof(NormalCustomScript), $"CopyArrayElement failed: {ex.Message}");
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
