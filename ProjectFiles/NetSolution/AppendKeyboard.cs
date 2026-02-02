#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.HMIProject;
using FTOptix.Retentivity;
using FTOptix.UI;
using FTOptix.NativeUI;
using FTOptix.CoreBase;
using FTOptix.Core;
using FTOptix.NetLogic;
using FTOptix.DataLogger;
using FTOptix.SQLiteStore;
using FTOptix.Store;
using FTOptix.WebUI;

#endregion
public class AppendKeyboard : BaseNetLogic
{
    private IUAVariable hasDecimal;
    private IUAVariable lowRange;
    private IUAVariable highRange;
    private IUAVariable hasRange;
    private IUAVariable isOB;
    private IUAVariable lockSignSwitch;
    private IUAVariable isValEntry;
    private float highRangeVal;
    private float lowRangeVal;
    private bool hasRangeVal;
    private bool isOBVal;
    private bool lockSignSwitchVal;
    private Label numericalLabel;
    private bool isValidEntryVal;

    public override void Start()
    {
        hasDecimal = LogicObject.GetVariable("hasDecimal");
        isOB = LogicObject.GetVariable("isOutOfBounds");
        isOBVal = isOB.Value;
        isValEntry = LogicObject.GetVariable("isValidEntry");
        isValidEntryVal = isValEntry.Value;
        lockSignSwitch = LogicObject.GetVariable("lockSignSwitch");
        lockSignSwitchVal = lockSignSwitch.Value;
        var NumericalKeypadVariables = Project.Current.GetObject("Model/Keypad/NumericalKeypadVariables");
        var hasRange = NumericalKeypadVariables.GetVariable("HasRange");
        var lowRange = NumericalKeypadVariables.GetVariable("MinimumValue");
        var highRange = NumericalKeypadVariables.GetVariable("MaximumValue");
        hasRangeVal = hasRange.Value;
        lowRangeVal = lowRange.Value;
        highRangeVal = highRange.Value;

        if (lowRangeVal >= 0 && hasRangeVal)
        {
            lockSignSwitch.Value = true;
        }
        /*else if (highRangeVal < 0 && hasRangeVal)
        {
            lockSignSwitch.Value = true;
        }
        */
        numericalLabel = ((Label)Owner);
        string startingValueStr = numericalLabel.Text;
        if (startingValueStr.Contains("."))
        {
            hasDecimal.Value = true;
        }
    }

    /// <summary>
    /// From ChatGpt
    /// </summary>
    /// <param name="value"></param>
    [ExportMethod]
    public void AppendValue(string value)
    {
        numericalLabel = ((Label)Owner);
        string startingValueStr = numericalLabel.Text;
        var isBackspace = value == string.Empty;
        string sboxValue = numericalLabel.Text;
        string newValueStr = sboxValue + value;

        // Remove leading zero if adding a new digit and not a decimal point
        if (sboxValue == "0" && value != ".")
        {
            sboxValue = "";
        }
        else if (sboxValue == "-0" && value != ".")
        {
            sboxValue = "-";
        }

        // Check if the user is trying to add a decimal point
        if (value == ".")
        {
            if (!hasDecimal.Value) // Prevent "." if out of range
            {
                numericalLabel.Text += ".";
                hasDecimal.Value = true;
            }
        }
        else
        {
            // Handle non-backspace entry
            if (!isBackspace)
            {
                // Temporarily append the new value to the current text
                newValueStr = sboxValue + value;

                // Attempt to parse the new value as a float
                if (float.TryParse(newValueStr, out float newValue))
                {
                    bool inRange = true;

                    // Check if the new value is within the allowed range
                    if (hasRangeVal)
                    {
                        inRange = newValue >= lowRangeVal && newValue <= highRangeVal;
                        isOBVal = !inRange; // Update isOBVal based on range check
                        isOB.Value = isOBVal; // Sync IUAVariable for out of bounds status
                        if (!inRange)
                        {
                            numericalLabel.Text = newValueStr;
                            isValEntry.Value = validateEntry(newValueStr);
                            return;
                        }
                    }
                    if (inRange)
                    {
                        numericalLabel.Text = newValueStr;
                    }
                    isValEntry.Value = validateEntry(newValueStr);
                }
            }
            else
            {
                // Handle backspace: Remove the last character or reset to "0" if empty
                numericalLabel.Text = (sboxValue.Length <= 1 ? "0" : sboxValue.Substring(0, sboxValue.Length - 1));
                newValueStr = numericalLabel.Text;
                if (hasRangeVal)
                {
                    if (float.TryParse(newValueStr, out float newValue))
                    {
                        isOBVal = !(newValue >= lowRangeVal && newValue <= highRangeVal);
                        isOB.Value = isOBVal; // Sync IUAVariable for out of bounds status
                        isValEntry.Value = validateEntry(newValueStr);
                    }
                }
            }
        }

        //if ((highRangeVal.ToString() == numericalLabel.Text) || (lowRangeVal.ToString() == numericalLabel.Text) && (numericalLabel.Text != "0"))
        //{
        //    hasDecimal.Value = true;
        //}

        // Update hasDecimal based on the current text
        hasDecimal.Value = numericalLabel.Text.Contains('.');

        isValEntry.Value = validateEntry(newValueStr);

    }


    [ExportMethod]
    public void Clear()
    {
        numericalLabel.Text = "0";
        hasDecimal.Value = false;
        // Check if the new value is within the allowed range
        if (hasRangeVal)
        {
            isOBVal = !(0.0 >= lowRangeVal && 0.0 <= highRangeVal); // Update isOBVal based on range check
            isOB.Value = isOBVal; // Sync IUAVariable for out of bounds status

        }
        isValEntry.Value = validateEntry(numericalLabel.Text);
    }

    [ExportMethod]
    public void ToggleSign()
    {
        if (numericalLabel.Text.StartsWith("-"))
        {
            numericalLabel.Text = numericalLabel.Text.Substring(1);
        }
        else
        {
            numericalLabel.Text = "-" + numericalLabel.Text;
        }
        string sboxValue = numericalLabel.Text.ToString();

        if (hasRangeVal)
        {
            if (float.TryParse(sboxValue, out float newValue))
            {
                isOBVal = !(newValue >= lowRangeVal && newValue <= highRangeVal);
                isOB.Value = isOBVal; // Sync IUAVariable for out of bounds status
            }
        }
        isValEntry.Value = validateEntry(sboxValue);
    }

    [ExportMethod]
    public void Backspace()
    {
        string sboxValue = numericalLabel.Text.ToString();
        if (!sboxValue.Contains('.'))
        {
            hasDecimal.Value = false;
        }
        AppendValue(string.Empty);
    }

    public bool validateEntry(string entry)
    {
        bool validEntry = true;
        // Initialize error message 
        string errorMessage = string.Empty;

        // Check if the entry is null or empty
        if (string.IsNullOrEmpty(entry))
        {
            validEntry = false;
            return validEntry;
        }

        // Check for multiple decimal points
        if (entry.Split('.').Length > 2)
        {
            validEntry = false;
            return validEntry;
        }

        // Check for multiple decimal points
        if (entry == "-0")
        {
            validEntry = false;
            return validEntry;
        }

        // Check if the entry is parsable to a float
        if (float.TryParse(entry, out float parsedEntry))
        {
            // Check if the value is zero but contains decimals like "0.000"
            //if (parsedEntry == 0 && entry.Contains('.'))
            //{
            //    validEntry = false;
            //    return validEntry;
            //}

            // Disallow negative zero with decimals like "-0.0", "-0.000", etc.
            if (parsedEntry == 0 && entry.StartsWith("-"))
            {
                return false;
            }

            //Check if the last character is a zero after a decimal
            if (entry.Contains('.') && entry.EndsWith("."))
            {
                validEntry = false;
                return validEntry;
            }

            // If range validation is enabled, check value in range
            if (hasRangeVal)
            {
                if (!(parsedEntry >= lowRangeVal && parsedEntry <= highRangeVal))
                {
                    validEntry = false;
                    return validEntry;
                }
            }
        }
        else
        {
            validEntry = false;
            return validEntry;
        }

        // Valid input found
        return validEntry;
    }


}

