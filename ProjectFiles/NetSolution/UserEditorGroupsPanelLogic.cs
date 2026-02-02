#region Using directives
using System.Collections.Generic;
using FTOptix.HMIProject;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.NetLogic;
using FTOptix.UI;
using FTOptix.WebUI;
#endregion

public class UserEditorGroupsPanelLogic : BaseNetLogic
{
    public override void Start()
    {
        // Get the User variable from the owner
        userVariable = Owner.GetVariable("User");

        // Store the current session user into the User variable
        var pathResolverResult = LogicObject.Context.ResolvePath(LogicObject, "{Session}/User");
        if (pathResolverResult != null && pathResolverResult.ResolvedNode != null)
            userVariable.Value = pathResolverResult.ResolvedNode.NodeId;

        // Editable variable
        editable = Owner.GetVariable("Editable");
        editable.VariableChange += Editable_VariableChange;

        // Subscribe to changes of User variable (if other logic changes it)
        userVariable.VariableChange += UserVariable_VariableChange;

        // Initialize nodes and UI
        UpdateGroupsAndUser();
        BuildUIGroups();

        if (editable.Value)
            SetCheckedValues();
    }

    private void Editable_VariableChange(object sender, VariableChangeEventArgs e)
    {
        UpdateGroupsAndUser();
        BuildUIGroups();

        if (e.NewValue)
            SetCheckedValues();
    }

    private void UserVariable_VariableChange(object sender, VariableChangeEventArgs e)
    {
        UpdateGroupsAndUser();
        if (editable.Value)
            SetCheckedValues();
        else
            BuildUIGroups();
    }

    private void UpdateGroupsAndUser()
    {
        if (userVariable.Value.Value != null)
            user = InformationModel.Get(userVariable.Value);

        groups = LogicObject.GetAlias("Groups");
    }

    private void BuildUIGroups()
    {
        if (groups == null)
            return;

        if (panel != null)
            panel.Delete();

        panel = InformationModel.MakeObject<ColumnLayout>("Container");
        panel.HorizontalAlignment = HorizontalAlignment.Stretch;

        // Get allowed groups based on session user
        var allowedGroups = GetAllowedGroupsForSessionUser();

        foreach (var group in groups.Children)
        {
            if (!allowedGroups.Contains(group.BrowseName.ToUpper()))
                continue; // Skip groups not allowed for this session user

            if (editable.Value)
            {
                var groupCheckBox = InformationModel.MakeObject<Panel>(group.BrowseName, RPC_SEQTEST.ObjectTypes.GroupCheckbox);

                groupCheckBox.GetVariable("Group").Value = group.NodeId;
                groupCheckBox.GetVariable("User").Value = userVariable.Value;
                groupCheckBox.HorizontalAlignment = HorizontalAlignment.Stretch;

                panel.Add(groupCheckBox);
                panel.Height += groupCheckBox.Height;
            }
            else
            {
                // In read-only mode, show all allowed groups (not just groups the user has)
                var groupLabel = InformationModel.MakeObject<Panel>(group.BrowseName, RPC_SEQTEST.ObjectTypes.GroupLabel);
                groupLabel.GetVariable("Group").Value = group.NodeId;
                groupLabel.HorizontalAlignment = HorizontalAlignment.Stretch;

                panel.Add(groupLabel);
                panel.Height += groupLabel.Height;
            }
        }

        var scrollView = Owner.Get("ScrollView");
        if (scrollView != null)
            scrollView.Add(panel);
    }

    private void SetCheckedValues()
    {
        if (groups == null)
            return;

        if (panel == null)
            return;

        var groupCheckBoxes = panel.Refs.GetObjects(OpcUa.ReferenceTypes.HasOrderedComponent, false);

        foreach (var groupCheckBoxNode in groupCheckBoxes)
        {
            var group = groups.Get(groupCheckBoxNode.BrowseName);
            groupCheckBoxNode.GetVariable("Checked").Value = UserHasGroup(group.NodeId);
        }
    }

    private bool UserHasGroup(NodeId groupNodeId)
    {
        if (user == null)
            return false;

        var userGroups = user.Refs.GetObjects(FTOptix.Core.ReferenceTypes.HasGroup, false);
        foreach (var userGroup in userGroups)
        {
            if (userGroup.NodeId == groupNodeId)
                return true;
        }
        return false;
    }

    // Returns the list of groups to display based on session user
    private List<string> GetAllowedGroupsForSessionUser()
    {
        var allowedGroups = new List<string>();
        if (user == null)
            return allowedGroups;

        string sessionUserName = user.BrowseName.ToUpper(); // Operator, Maintenance, OEM

        switch (sessionUserName)
        {
            case "OPERATOR":
                allowedGroups.Add("OPERATORS");
                break;
            case "MAINTENANCE":
                allowedGroups.Add("OPERATORS");
                allowedGroups.Add("MAINTENANCE");
                break;
            case "OEM":
                allowedGroups.Add("OPERATORS");
                allowedGroups.Add("MAINTENANCE");
                allowedGroups.Add("OEM");
                break;
        }

        return allowedGroups;
    }

    // Variables
    private IUAVariable userVariable;
    private IUAVariable editable;

    private IUANode groups;
    private IUANode user;
    private ColumnLayout panel;
}
