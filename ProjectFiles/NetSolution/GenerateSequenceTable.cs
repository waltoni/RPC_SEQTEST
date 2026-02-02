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
using FTOptix.UI;
using FTOptix.Core;
#endregion

public class GenerateSequenceTable : BaseNetLogic
{
    private const int MinRows = 1;
    // Keep this conservative for performance; adjust if you truly need more.
    private const int MaxRows = 200;
    private const int DelayMsPerElement = 20;

    private IUAVariable numPerLayerVar;
    private DelayedTask rebuildTask;
    private DelayedTask createNextTask;
    private DelayedTask initialSyncTask;
    private int pendingDesiredRows = -1;
    private int lastBuiltRows = -1;

    public override void Start()
    {
        numPerLayerVar = LogicObject.GetVariable("NumPerLayer");
        if (numPerLayerVar == null)
        {
            Log.Error(nameof(GenerateSequenceTable), "Missing LogicObject variable 'NumPerLayer'.");
            return;
        }

        // Rebuild when NumPerLayer changes (performance goal).
        numPerLayerVar.VariableChange += NumPerLayer_VariableChange;

        // Performance-focused startup behavior:
        // - Do NOT rebuild immediately on Start()
        // - Do a single delayed sync (gives tag time to populate), then only rebuild if out of sync.
        if (initialSyncTask != null)
        {
            initialSyncTask.Dispose();
            initialSyncTask = null;
        }

        initialSyncTask = new DelayedTask(() =>
        {
            var desired = ClampRows(ToInt(numPerLayerVar?.Value));
            if (desired > 0 && IsOutOfSync(desired))
                ScheduleRebuild(desired);
        }, 250, LogicObject);

        initialSyncTask.Start();
    }

    public override void Stop()
    {
        if (numPerLayerVar != null)
            numPerLayerVar.VariableChange -= NumPerLayer_VariableChange;

        numPerLayerVar = null;

        if (rebuildTask != null)
        {
            rebuildTask.Dispose();
            rebuildTask = null;
        }

        if (createNextTask != null)
        {
            createNextTask.Dispose();
            createNextTask = null;
        }

        if (initialSyncTask != null)
        {
            initialSyncTask.Dispose();
            initialSyncTask = null;
        }
    }

    private void NumPerLayer_VariableChange(object sender, VariableChangeEventArgs e)
    {
        // Avoid doing any heavy UI/model work directly in the change callback.
        var desired = ClampRows(ToInt(e.NewValue));

        // If PLC hasn't initialized yet, don't thrash.
        if (desired <= 0)
            return;

        if (!IsOutOfSync(desired))
            return;

        ScheduleRebuild(desired);
    }

    private void ScheduleRebuild(int desiredRows)
    {
        pendingDesiredRows = desiredRows;

        // Debounce (PLC may write multiple times quickly).
        if (rebuildTask != null)
        {
            rebuildTask.Dispose();
            rebuildTask = null;
        }

        rebuildTask = new DelayedTask(() =>
        {
            var desired = pendingDesiredRows;
            pendingDesiredRows = -1;
            Rebuild(desired);
        }, 50, LogicObject);

        rebuildTask.Start();
    }

    private bool IsOutOfSync(int desiredRows)
    {
        if (Owner == null)
            return false;

        var seq1 = Owner.Get("Sequence1");
        if (seq1 == null)
            return false;

        var existing = CountExistingSequenceRows();
        // If we don't know yet, treat as out of sync so we can build once.
        if (existing <= 0)
            return true;

        return existing != desiredRows || lastBuiltRows != desiredRows;
    }

    private int CountExistingSequenceRows()
    {
        if (Owner == null)
            return 0;

        int count = 0;
        foreach (var child in Owner.Children)
        {
            var n = ParseSequenceSuffix(child?.BrowseName);
            if (n.HasValue)
                count = Math.Max(count, n.Value);
        }

        // Count is the highest SequenceN found; if there are gaps, rebuild will fix ordering.
        return count;
    }

    private void Rebuild(int desiredRows)
    {
        desiredRows = ClampRows(desiredRows);
        if (desiredRows <= 0 || Owner == null)
            return;

        if (createNextTask != null)
        {
            createNextTask.Dispose();
            createNextTask = null;
        }

        var seq1Node = Owner.Get("Sequence1") as IUAObject;
        if (seq1Node == null)
        {
            Log.Error(nameof(GenerateSequenceTable), "Owner is missing 'Sequence1'.");
            return;
        }

        // Ensure template has Index = 1
        var seq1IndexVar = seq1Node.GetVariable("Index");
        if (seq1IndexVar != null)
            seq1IndexVar.Value = (short)1;

        var seqType = seq1Node.ObjectType;
        if (seqType == null)
        {
            Log.Error(nameof(GenerateSequenceTable), "Could not resolve Sequence1.ObjectType (needed to instantiate new rows).");
            return;
        }

        var seqTypeId = seqType.NodeId;

        // 1) Remove any Sequence{n} where n > desiredRows
        // Iterate on a snapshot since we'll mutate the tree.
        var childrenSnapshot = Owner.Children.ToArray();
        foreach (var child in childrenSnapshot)
        {
            if (child == null)
                continue;

            var n = ParseSequenceSuffix(child.BrowseName);
            if (!n.HasValue)
                continue;

            if (n.Value <= 1)
                continue; // keep template row

            if (n.Value > desiredRows)
                Owner.Remove(child);
        }

        // 2) Ensure Sequence2..Sequence{desiredRows} exist, create missing ones (staggered for smoother UI)
        CreateNextOrFinish(2, desiredRows, seqTypeId);
    }

    private void CreateNextOrFinish(int n, int desiredRows, NodeId seqTypeId)
    {
        if (Owner == null)
            return;

        if (n > desiredRows)
        {
            lastBuiltRows = desiredRows;
            createNextTask = null;
            return;
        }

        var name = $"Sequence{n}";
        var existing = Owner.Get(name) as IUAObject;
        if (existing != null)
        {
            var indexVar = existing.GetVariable("Index");
            if (indexVar != null)
                indexVar.Value = (short)n;
            CreateNextOrFinish(n + 1, desiredRows, seqTypeId);
            return;
        }

        var newRow = InformationModel.MakeObject(name, seqTypeId);
        Owner.Add(newRow);

        var newIndexVar = newRow.GetVariable("Index");
        if (newIndexVar != null)
            newIndexVar.Value = (short)n;

        if (n >= desiredRows)
        {
            lastBuiltRows = desiredRows;
            createNextTask = null;
            return;
        }

        var nextN = n + 1;
        createNextTask = new DelayedTask(() =>
        {
            createNextTask = null;
            CreateNextOrFinish(nextN, desiredRows, seqTypeId);
        }, DelayMsPerElement, LogicObject);
        createNextTask.Start();
    }

    private static int ClampRows(int v)
    {
        if (v < MinRows)
            return 0; // treat as not-initialized / disabled
        if (v > MaxRows)
            return MaxRows;
        return v;
    }

    private static int ToInt(object uaValue)
    {
        // Be defensive: Value/VariableChange may provide boxed primitives or UAValue.
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
            if (uaValue is uint ui) return (int)ui;
            if (uaValue is ushort us) return us;
            if (uaValue is byte b) return b;
            if (uaValue is sbyte sb) return sb;
            if (uaValue is double d) return (int)d;
            if (uaValue is float f) return (int)f;
            if (uaValue is decimal m) return (int)m;
            if (uaValue is string str && int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                return parsed;

            return Convert.ToInt32(uaValue, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0;
        }
    }

    private static int? ParseSequenceSuffix(string browseName)
    {
        if (string.IsNullOrEmpty(browseName))
            return null;

        if (!browseName.StartsWith("Sequence", StringComparison.OrdinalIgnoreCase))
            return null;

        var suffix = browseName.Substring("Sequence".Length);
        if (suffix.Length == 0)
            return null;

        if (int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
            return n;

        return null;
    }
}
