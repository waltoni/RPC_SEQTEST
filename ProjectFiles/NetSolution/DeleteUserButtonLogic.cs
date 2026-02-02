#region Using directives
using FTOptix.HMIProject;
using UAManagedCore;
using FTOptix.NetLogic;
using FTOptix.UI;
using System.Linq;
using FTOptix.WebUI;
using FTOptix.Recipe;
using FTOptix.Report;
using FTOptix.AuditSigning;
#endregion

public class DeleteUserButtonLogic : BaseNetLogic
{
    [ExportMethod]
    public void DeleteUser(NodeId userToDelete)
    {
        Log.Info("DeleteUserButtonLogic", "DeleteUser method called.");

        var userObjectToRemove = InformationModel.Get(userToDelete);
        if (userObjectToRemove == null)
        {
            Log.Error("UserEditor", "Cannot obtain the selected user.");
            return;
        }

        // Get the username (BrowseName) of the user to be deleted
        string username = userObjectToRemove.BrowseName;
        Log.Info("DeleteUserButtonLogic", $"Attempting to delete user: {username}");

        // Check if the user is a protected user that cannot be deleted
        string[] protectedUsers = { "OEM", "Maintenance", "Operator" };
        if (protectedUsers.Contains(username))
        {
            Log.Warning("DeleteUserButtonLogic", $"Cannot delete protected user '{username}'. Protected users (OEM, Maintenance, Operator) cannot be deleted.");
            return;
        }

        var userVariable = Owner.Owner.Owner.Owner.GetVariable("Users");
        if (userVariable == null)
        {
            Log.Error("UserEditor", "Missing user variable in UserEditor Panel.");
            return;
        }

        if (userVariable.Value == null || (NodeId)userVariable.Value == NodeId.Empty)
        {
            Log.Error("UserEditor", "Fill User variable in UserEditor.");
            return;
        }

        var usersFolder = InformationModel.Get(userVariable.Value);
        if (usersFolder == null)
        {
            Log.Error("UserEditor", "Cannot obtain Users folder.");
            return;
        }

        Log.Info("DeleteUserButtonLogic", $"Removing user '{username}' from Users folder.");
        usersFolder.Remove(userObjectToRemove);
        Log.Info("DeleteUserButtonLogic", $"User '{username}' successfully deleted.");

        if (usersFolder.Children.Count > 0)
        {
            var usersList = (ListBox)Owner.Owner.Owner.Get<ListBox>("HorizontalLayout1/UsersList");
            usersList.SelectedItem = usersFolder.Children.First().NodeId;
            Log.Info("DeleteUserButtonLogic", "User list selection updated to first remaining user.");
        }
        else
        {
            Log.Info("DeleteUserButtonLogic", "No users remaining in the Users folder.");
        }
    }
}
