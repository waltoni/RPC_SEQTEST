#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.HMIProject;
using FTOptix.Retentivity;
using FTOptix.UI;
using FTOptix.NativeUI;
using FTOptix.CoreBase;
using FTOptix.System;
using FTOptix.NetLogic;
using FTOptix.SerialPort;
using FTOptix.Core;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using Org.BouncyCastle.Crypto;
using iText.IO.Font.Constants;
using iText.Kernel.Font;
using iText.IO.Font;
using System.IO;
using FTOptix.Store;
using System.Collections.Generic;
using System.Linq;
using Table = iText.Layout.Element.Table;
using iText.IO.Image;
using iText.Kernel.Colors;
using iText.Layout.Borders;
using iText.Layout.Properties;
using iText.Kernel.Events;
using System.Reflection.Metadata;
using Document = iText.Layout.Document;
using System.Collections;
using Image = iText.Layout.Element.Image;
#endregion

public class ReportGenerator : BaseNetLogic
{
    //test force repo update
    public override void Start()
    {
        // Insert code to be executed when the user-defined logic is started
    }

    public override void Stop()
    {
        // Insert code to be executed when the user-defined logic is stopped
    }

    public class COEntry
    {
        public string Path { get; set; }
        public string Info { get; set; }
        public string Description { get; set; }
    }

    [ExportMethod]
    public void makeFlipChartPdf()
    {
        // Convert ValueMap to Dictionary and extract image paths
        FTOptix.CoreBase.ValueMapConverterType converterCOPath = (ValueMapConverterType)Project.Current.Get("Data/Converters/COMapImagePath");
        FTOptix.CoreBase.ValueMapConverterType converterCOInfo = (ValueMapConverterType)Project.Current.Get("Data/Converters/COMapImageInfo");
        var converterPathPairs = converterCOPath.Children.ElementAt(0).Children;
        var converterInfoPairs = converterCOInfo.Children.ElementAt(0).Children;

        var changePointsDictionary = new Dictionary<string, COEntry>();

        // Call getCPEnablements to get the enabled states array
        bool[] coEnabledArray = getCPEnablements();
        // Fetch the change point descriptions before the loop
        var changePointDescriptions = getChangePointDescriptions();

        // Iterate through coEnabledArray
        for (int keyIndex = 0; keyIndex < coEnabledArray.Length; keyIndex++)
        {
            if (!coEnabledArray[keyIndex])
            {
                Log.Info($"KeyIndex {keyIndex} is not enabled. Skipping.");
                continue;
            }

            // Find the corresponding path entry
            var pathChildPair = converterPathPairs.FirstOrDefault(pair =>
            {
                var pathKeyVar = (pair as UAObject)?.Children.ElementAt(0) as UAVariable;
                return pathKeyVar != null && int.TryParse(pathKeyVar.Value.Value.ToString(), out int index) && index == keyIndex;
            });

            if (pathChildPair == null)
            {
                Log.Warning($"No path entry found for keyIndex {keyIndex}. Skipping.");
                continue;
            }

            // Retrieve image path
            var pathValueVar = (pathChildPair as UAObject)?.Children.ElementAt(1) as UAVariable;
            string imagePath = pathValueVar != null ? new ResourceUri(pathValueVar.Value).Uri : null;

            // Find the corresponding info entry
            var infoChildPair = converterInfoPairs.FirstOrDefault(pair =>
            {
                var infoKeyVar = (pair as UAObject)?.Children.ElementAt(0) as UAVariable;
                return infoKeyVar != null && int.TryParse(infoKeyVar.Value.Value.ToString(), out int index) && index == keyIndex;
            });

            if (infoChildPair == null)
            {
                Log.Warning($"No info entry found for keyIndex {keyIndex}. Skipping.");
                continue;
            }

            // Retrieve localized text
            var cOInfoVar = (infoChildPair as UAObject)?.Children.ElementAt(1) as UAVariable;
            LocalizedText cODescription = (LocalizedText)cOInfoVar.Value;
            string infoText = cODescription?.Text ?? "N/A";

            // Retrieve description
            string descriptionText = changePointDescriptions.ContainsKey(keyIndex)
                ? changePointDescriptions[keyIndex]
                : "No description available";

            // Add to dictionary
            changePointsDictionary[keyIndex.ToString()] = new COEntry
            {
                Path = imagePath,
                Info = infoText,
                Description = descriptionText
            };

            Log.Info($"Added entry for keyIndex {keyIndex}: Path={imagePath}, Info={infoText}, Description={descriptionText}");
        }

        Log.Info($"Finished processing. Total entries: {changePointsDictionary.Count}");


        // PDF settings and font loading
        var fontRegularPath = GetFontRegularFilePath();
        var fontBoldPath = GetFontBoldFilePath();
        var logoPath = GetLogoFilePath();
        var machinePath = GetMachineImageByOptionsPath();
        var coCalloutPath = GetCOCalloutFilePath();
        var reportFlipChartExportPath = GetFlipChartReportExportFilePath();
        var firstPageTitle = LogicObject.GetVariable("MachineTitleByOptions").Value;
        PdfFont fontRegular = PdfFontFactory.CreateFont(fontRegularPath, PdfEncodings.IDENTITY_H);
        PdfFont fontBold = PdfFontFactory.CreateFont(fontBoldPath, PdfEncodings.IDENTITY_H);

        using (PdfWriter writer = new PdfWriter(reportFlipChartExportPath))
        {
            using (PdfDocument pdf = new PdfDocument(writer))
            {
                pdf.SetDefaultPageSize(iText.Kernel.Geom.PageSize.A4.Rotate());

                // Create the Document instance once, outside the loop
                Document document = new Document(pdf);
                document.SetFont(fontBold);

                // Add logo to the top right of each page
                AddLogo(document, pdf, logoPath);

                AddFirstPage(document, pdf, machinePath, fontBold, firstPageTitle);
                document.Add(new AreaBreak(AreaBreakType.NEXT_PAGE));


                bool firstPage = true;

                // Iterate through each entry in the dictionary and add to new pages
                foreach (var entry in changePointsDictionary)
                {
                    if (!firstPage)
                    {
                        document.Add(new AreaBreak(AreaBreakType.NEXT_PAGE));
                    }
                    else
                    {
                        firstPage = false;
                    }

                    // Access the Path and Description fields from COEntry
                    string imageName = entry.Key;
                    var entryData = entry.Value;
                    string imagePath = entryData.Path;
                    string infoText = entryData.Info;
                    string descriptionText = entryData.Description;

                    // Add logo to the top right of each page
                    AddLogoTopRight(document, pdf, logoPath);
                    // Add logo to the top right of each page
                    AddCOCallout(document, pdf, coCalloutPath, imageName);

                    // Add the image and description together on the page
                    AddChangeOverPage(document, pdf, imagePath, coCalloutPath, fontRegular, fontBold, imageName, descriptionText, infoText);
                }

                document.Close();
            }
        }
    }
    private void AddChangeOverPage(Document document, PdfDocument pdf, string imagePath, string coCalloutPath, PdfFont font, PdfFont fontBold, string title, string descriptionText, string infoText)
    {
        var pageSize = pdf.GetDefaultPageSize();


        // Create a table with 2 rows and 2 columns
        float[] columnWidths = { 300, 400 }; // Adjust column widths as needed
        Table table = new Table(columnWidths);
        table.UseAllAvailableWidth();

        // Add the title and description to the top-left cell (combined content)
        Cell titleAndDescriptionCell = new Cell(1, 1) // 1 row, 1 column
            .Add(new Paragraph(title)
                .SetFont(font)
                .SetFontSize(30)
                .SetTextAlignment(iText.Layout.Properties.TextAlignment.LEFT))
            .Add(new Paragraph(descriptionText)
                .SetFont(fontBold)
                .SetFontSize(18)
                .SetTextAlignment(iText.Layout.Properties.TextAlignment.LEFT)
                .SetWidth(400)  // Set width to control wrapping
                .SetMultipliedLeading(1.2f)) // Adjust line spacing
            .SetBorder(Border.NO_BORDER); // No border for this cell

        // Add the title and description cell to the table
        table.AddCell(titleAndDescriptionCell);

        // Add a blank cell for the top-right (buffer space)
        table.AddCell(new Cell(1, 1).SetBorder(Border.NO_BORDER)); // No content, just a buffer

        // Add the info text to the bottom-left cell
        Cell infoTextCell = new Cell(1, 1) // 1 row, 1 column
            .Add(new Paragraph(infoText)
                .SetFont(font)
                .SetFontSize(12)
                .SetTextAlignment(iText.Layout.Properties.TextAlignment.LEFT)
                .SetWidth(400)  // Adjust width to fit
                .SetMultipliedLeading(1.2f)) // Adjust line spacing
            .SetBorder(Border.NO_BORDER); // No border for this cell

        // Add the info text cell to the table
        table.AddCell(infoTextCell);

        // Add the image to the bottom-right cell
        iText.Layout.Element.Image image = new iText.Layout.Element.Image(ImageDataFactory.Create(imagePath));
        image.ScaleToFit(350, 250); // Scale the image to fit within the cell

        // Add the image cell (invisible border)
        Cell imageCell = new Cell(1, 1) // 1 row, 1 column
            .Add(image)
            .SetBorder(Border.NO_BORDER); // No border for this cell
                                          // Add the image cell to the table
        table.AddCell(imageCell);



        // Add the table to the document
        document.Add(table);
    }

    [ExportMethod]
    public void makePdf()
    {
        var fontRegularPath = GetFontRegularFilePath();
        var fontBoldPath = GetFontBoldFilePath();
        var reportExportPath = GetReportExportFilePath();
        var logoPath = GetLogoFilePath();

        PdfFont fontRegular = PdfFontFactory.CreateFont(fontRegularPath, PdfEncodings.IDENTITY_H);
        PdfFont fontBold = PdfFontFactory.CreateFont(fontBoldPath, PdfEncodings.IDENTITY_H);

        string machineSerialName = LogicObject.GetVariable("projectName").Value;
        machineSerialName = machineSerialName.Replace("_", "");
        var transposedRows = GetTransposedRows();

        using (PdfWriter writer = new PdfWriter(reportExportPath))
        {
            using (PdfDocument pdf = new PdfDocument(writer))
            {
                // Set to portrait mode
                pdf.SetDefaultPageSize(iText.Kernel.Geom.PageSize.A4);

                Document document = new Document(pdf);
                document.SetFont(fontBold);

                int maxColumnsPerPage = 6; // Reduced for portrait orientation
                int totalColumns = transposedRows[0].Count;
                int currentColumn = 1;
                float fontSize = 8;
                float paddingSize = 5;


                // Define column widths in points
                float pageWidth = 595f; // A4 width in points
                float margins = 36f * 2; // Example margins of 36 points on each side
                float descriptionColumnWidth = 150f; // Fixed width for the description column in points
                float availableWidth = pageWidth - margins - descriptionColumnWidth; // Remaining width for other columns

                while (currentColumn < totalColumns)
                {
                    if (currentColumn > 1)
                    {
                        document.Add(new AreaBreak(AreaBreakType.NEXT_PAGE));
                    }

                    // Add header elements at the top of each new page
                    AddLogo(document, pdf, logoPath);
                    AddCompanyHeader(document, fontRegular);
                    AddTitle(document, fontBold);
                    AddMachineSerialName(document, fontBold, machineSerialName);

                    document.Add(new Paragraph("\n\n\n\n"));

                    // Determine the number of additional columns for this page
                    int columnsOnPage = Math.Min(maxColumnsPerPage, totalColumns - currentColumn);
                    int blankColumnsToAdd = (maxColumnsPerPage - 1) - columnsOnPage; // Calculate the blank columns needed


                    // Calculate dynamic widths for the remaining columns
                    float[] columnWidths = new float[columnsOnPage + 1];
                    columnWidths[0] = descriptionColumnWidth; // Fixed width for description column
                    for (int i = 1; i < columnWidths.Length; i++)
                    {
                        columnWidths[i] = availableWidth / columnsOnPage; // Divide remaining space equally
                    }

                    // Create the table with calculated column widths
                    Table table = new Table(columnWidths);

                    // Add headers: description column first, followed by dynamic columns
                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(transposedRows[0].Keys.ElementAt(0)) // Fixed description header
                            .SetFont(fontBold)
                            .SetFontSize(fontSize))
                        .SetBackgroundColor(new DeviceGray(0.9f))
                        .SetBorder(new SolidBorder(1.1f))
                        .SetPadding(paddingSize));

                    for (int col = currentColumn; col < currentColumn + columnsOnPage; col++)
                    {
                        table.AddHeaderCell(new Cell()
                            .Add(new Paragraph(transposedRows[0].Keys.ElementAt(col))
                                .SetFont(fontBold)
                                .SetFontSize(fontSize))
                            .SetBackgroundColor(new DeviceGray(0.9f))
                            .SetBorder(new SolidBorder(1.1f))
                            .SetPadding(paddingSize));
                    }

                    // Add rows: description cell first, followed by dynamic cells
                    for (int row = 1; row < transposedRows.Count; row++)
                    {
                        // Add description column
                        table.AddCell(new Cell()
                            .Add(new Paragraph(transposedRows[row].Values.ElementAt(0)?.ToString() ?? "N/A")
                                .SetFont(fontBold)
                                .SetFontSize(fontSize))
                            .SetBackgroundColor(new DeviceGray(0.9f))
                            .SetBorder(new SolidBorder(1.1f))
                            .SetPadding(paddingSize));

                        // Add additional columns
                        for (int col = currentColumn; col < currentColumn + columnsOnPage; col++)
                        {
                            var cellValue = transposedRows[row].Values.ElementAt(col);
                            string cellText = cellValue?.ToString() ?? "N/A";

                            table.AddCell(new Cell()
                                .Add(new Paragraph(cellText)
                                    .SetFont(fontBold)
                                    .SetFontSize(fontSize))
                                .SetBorder(new SolidBorder(1.1f))
                                .SetPadding(paddingSize));
                        }
                    }



                    document.Add(table);
                    currentColumn += columnsOnPage;
                }

                // Add a page break before the final blank page
                document.Add(new AreaBreak(AreaBreakType.NEXT_PAGE));

                // Define the column widths for the blank page table
                float remainingWidth = 595f - (36f * 2) - descriptionColumnWidth; // A4 width - margins - description column
                float blankColumnWidth = remainingWidth / 6; // Equal width for the 6 blank columns

                float[] blankPageColumnWidths = new float[7];
                blankPageColumnWidths[0] = descriptionColumnWidth; // Fixed width for the description column
                for (int i = 1; i < blankPageColumnWidths.Length; i++)
                {
                    blankPageColumnWidths[i] = blankColumnWidth; // Equal share for blank columns
                }


                // Add header elements at the top of each new page
                AddLogo(document, pdf, logoPath);
                AddCompanyHeader(document, fontRegular);
                AddTitle(document, fontBold);
                AddMachineSerialName(document, fontBold, machineSerialName);

                document.Add(new Paragraph("\n\n\n\n"));

                // Create the table for the blank page
                Table blankPageTable = new Table(blankPageColumnWidths);

                // Add header row: description column header and blank headers for 6 columns
                blankPageTable.AddHeaderCell(new Cell()
                    .Add(new Paragraph("Adjustment Description") // Fixed header for the description column
                        .SetFont(fontBold)
                        .SetFontSize(fontSize))
                    .SetBackgroundColor(new DeviceGray(0.9f))
                    .SetBorder(new SolidBorder(1.1f))
                    .SetPadding(paddingSize));

                for (int i = 1; i < 7; i++) // Add blank headers
                {
                    blankPageTable.AddHeaderCell(new Cell()
                        .Add(new Paragraph("") // Empty header cells
                            .SetFont(fontBold)
                            .SetFontSize(8))
                        .SetBackgroundColor(new DeviceGray(0.9f))
                        .SetBorder(new SolidBorder(1.1f))
                        .SetPadding(5));
                }

                // Add rows: description column with content and empty cells for the rest
                for (int row = 1; row < transposedRows.Count; row++)
                {
                    // Add the description column content
                    blankPageTable.AddCell(new Cell()
                        .Add(new Paragraph(transposedRows[row].Values.ElementAt(0)?.ToString() ?? "N/A")
                            .SetFont(fontBold)
                            .SetFontSize(fontSize))
                        .SetBackgroundColor(new DeviceGray(0.9f))
                        .SetBorder(new SolidBorder(1.1f))
                        .SetPadding(paddingSize));

                    // Add empty cells for the 6 blank columns
                    for (int i = 1; i < 7; i++)
                    {
                        blankPageTable.AddCell(new Cell()
                            .Add(new Paragraph("") // Empty content
                                .SetFont(fontBold)
                                .SetFontSize(8))
                            .SetBorder(new SolidBorder(1.1f))
                            .SetPadding(5));
                    }
                }

                // Add the blank page table to the document
                document.Add(blankPageTable);

                document.Close();
            }
        }
    }

    private void AddLogo(Document document, PdfDocument pdf, string logoPath)
    {
        var pageSize = pdf.GetDefaultPageSize();
        iText.Layout.Element.Image logo = new iText.Layout.Element.Image(ImageDataFactory.Create(logoPath));
        logo.SetFixedPosition(20, pageSize.GetHeight() - 60);
        logo.ScaleToFit(190, 38);
        document.Add(logo);
    }
    private void AddLogoTopRight(Document document, PdfDocument pdf, string logoPath)
    {
        var pageSize = pdf.GetDefaultPageSize();
        iText.Layout.Element.Image logo = new iText.Layout.Element.Image(ImageDataFactory.Create(logoPath));
        logo.SetFixedPosition(pageSize.GetWidth() - 250, pageSize.GetHeight() - 60);
        logo.ScaleToFit(190, 38);
        document.Add(logo);
    }

    private void AddFirstPage(Document document, PdfDocument pdf, string machinePath, PdfFont fontBold, string title)
    {
        var pageSize = pdf.GetDefaultPageSize();

        // Define title paragraph and position it above the image
        Paragraph titleParagraph = new Paragraph(title)
            .SetFont(fontBold)
            .SetFontSize(30)
            .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER);

        // Calculate position for the title
        float titleX = (pageSize.GetWidth() - 600) / 2;  // Center the title horizontally
        float titleY = pageSize.GetHeight() - 175;        // Position title near the top of the page

        titleParagraph.SetFixedPosition(titleX, titleY, 600);
        document.Add(titleParagraph);

        // Load and position the image below the title
        iText.Layout.Element.Image logo = new iText.Layout.Element.Image(ImageDataFactory.Create(machinePath));
        logo.SetFixedPosition((pageSize.GetWidth() - 450) / 2, titleY - 415);  // Center image horizontally, adjust Y position to fit below title
        logo.ScaleToFit(450, 400);  // Scale the image

        document.Add(logo);
    }


    private void AddCOCallout(Document document, PdfDocument pdf, string coPath, string calloutIndex)
    {
        var pageSize = pdf.GetDefaultPageSize();
        iText.Layout.Element.Image logo = new iText.Layout.Element.Image(ImageDataFactory.Create(coPath));

        // Parse calloutIndex to an integer
        if (int.TryParse(calloutIndex, out int index))
        {
            // Apply conditional positioning based on the parsed integer value
            if (index < 10)
            {
                logo.SetFixedPosition(22, pageSize.GetHeight() - 87);
            }
            else
            {
                logo.SetFixedPosition(30, pageSize.GetHeight() - 87);
            }
        }
        else
        {
            // Handle the case where calloutIndex is not a valid integer, if needed
            throw new ArgumentException("calloutIndex must be a valid integer.");
        }

        logo.ScaleToFit(50, 50);
        document.Add(logo);
    }


    private void AddCompanyHeader(Document document, PdfFont font)
    {
        var pageSize = document.GetPdfDocument().GetDefaultPageSize();
        document.Add(new Paragraph("W. 8120 SUNSET HIGHWAY\nSPOKANE, WA 99224\nPHONE: (509) 838-6226\nFAX: (509) 747-8532\nwww.pearsonpkg.com")
            .SetFont(font)
            .SetFontSize(8)
            .SetTextAlignment(iText.Layout.Properties.TextAlignment.RIGHT)
            .SetFixedPosition(pageSize.GetWidth() - 220, pageSize.GetHeight() - 70, 200));
    }

    private void AddTitle(Document document, PdfFont font)
    {
        var pageSize = document.GetPdfDocument().GetDefaultPageSize();
        document.Add(new Paragraph("Change Over Report")
            .SetFont(font)
            .SetFontSize(12)
            .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER)
            .SetFixedPosition(pageSize.GetWidth() / 2 - 50, pageSize.GetHeight() - 90, 100));
    }

    private void AddMachineSerialName(Document document, PdfFont font, string machineSerialName)
    {
        var pageSize = document.GetPdfDocument().GetDefaultPageSize();
        document.Add(new Paragraph($"Change Chart for \nMachine Serial #: {machineSerialName}")
            .SetFont(font)
            .SetFontSize(10)
            .SetTextAlignment(iText.Layout.Properties.TextAlignment.LEFT)
            .SetFixedPosition(20, pageSize.GetHeight() - 110, 300));
    }

    private IUAVariable GlueOption;
    private IUAVariable TotalGlueSettings;
    private IUAVariable TotalCustomSettings;
    private IUAVariable CPEnabled;
    private IUAVariable CustomEnabled;


    private bool[] getCPEnablements()
    {
        CPEnabled = LogicObject.GetVariable("CPEnabled");
        int coEnabled = CPEnabled.Value;//LogicObject.Owner.GetObject("COReportVariables").GetVariable("CPEnabled").Value;
        bool[] coEnabledArray = new bool[32];

        // Populate the coEnabledArray based on the bitwise representation of enabled change points
        for (int i = 0; i < 32; i++)
        {
            coEnabledArray[i] = (coEnabled & (1 << i)) != 0;
        }

        return coEnabledArray;

    }



    // Method to handle the logic for transposing the ResultSet
    private List<Dictionary<string, object>> GetTransposedRows()
    {
        bool[] coEnabledArray = getCPEnablements();

        // Build dynamic query with quoted ChangeOverSettings columns where enabled
        List<string> selectedColumns = new List<string> { "Name" };
        for (int i = 0; i < coEnabledArray.Length; i++)
        {
            if (coEnabledArray[i])
            {
                selectedColumns.Add($"\"/ChangeOverSettings_{i}\"");
            }
        }

        string query = $"SELECT {string.Join(", ", selectedColumns)} FROM RPCSchema ORDER BY Name";
        Log.Info($"Generated Query: {query}");

        var myStore = Project.Current.Get<Store>("Data/DataStores/RPCRecipeDB");
        Object[,] ResultSet;
        String[] Header;

        // Perform the query
        myStore.Query(query, out Header, out ResultSet);

        // Log dimensions of ResultSet
        Log.Info($"ResultSet Dimensions: Rows = {ResultSet.GetLength(0)}, Columns = {ResultSet.GetLength(1)}");
        Log.Info($"Header Length: {Header.Length}");
        Log.Info($"coEnabledArray Length: {coEnabledArray.Length}");

        // Retrieve descriptions for each enabled change point
        var cpDescriptionsDict = getChangePointDescriptions();

        // Initialize a list to hold the transposed rows
        var transposedRows = new List<Dictionary<string, object>>();
        var headerRow = new Dictionary<string, object>
    {
        { "Adjustment Description", "Description" } // Placeholder for the header
    };

        // Fill in the other headers based on the names from ResultSet
        for (int col = 0; col < ResultSet.GetLength(0); col++) // Iterate over columns
        {
            headerRow[ResultSet[col, 0]?.ToString() ?? $"Column {col}"] = ResultSet[col, 0]; // Add column headers
        }

        transposedRows.Add(headerRow); // Add header row to transposed rows

        // Loop through enabled change point indices to create adjusted rows
        List<int> validChangePointIndices = new List<int>();
        for (int index = 0; index < coEnabledArray.Length; index++)
        {
            if (coEnabledArray[index])
            {
                validChangePointIndices.Add(index);
            }
        }

        foreach (int enabledIndex in validChangePointIndices)
        {
            var adjustedRow = new Dictionary<string, object>
        {
            { "Adjustment Description", cpDescriptionsDict.ContainsKey(enabledIndex)
                ? $"{enabledIndex}: {cpDescriptionsDict[enabledIndex]}"
                : $"Index {enabledIndex}" }
        };


            for (int row = 0; row < ResultSet.GetLength(0); row++) // Start from 1 to skip header row
            {
                int columnIndexInResultSet = validChangePointIndices.IndexOf(enabledIndex) + 1; // +1 for the Name column

                if (columnIndexInResultSet < ResultSet.GetLength(1)) // Check bounds
                {
                    string recipeName = ResultSet[row, 0]?.ToString(); // Assuming the first column is 'Name'
                    if (!string.IsNullOrWhiteSpace(recipeName))
                    {
                        adjustedRow[recipeName] = ResultSet[row, columnIndexInResultSet]; // Fill in the values
                    }
                }
            }

            Log.Info($"Adjusted Row for Enabled Index {enabledIndex}: {string.Join(", ", adjustedRow.Select(kvp => $"{kvp.Key}: {kvp.Value?.ToString() ?? "N/A"}"))}");

            // Add adjusted row only if it contains valid data
            if (adjustedRow.Values.Any(value => value != null && value.ToString() != "0"))
            {
                transposedRows.Add(adjustedRow);
            }
        }



        //var glueMachine = LogicObject.Owner.GetObject("COReportVariables").GetVariable("GlueOption");

        // Log the transposed rows for debugging purposes
        foreach (var row in transposedRows)
        {
            string rowData = string.Join(", ", row.Values.Select(value => value?.ToString() ?? "N/A"));
            Log.Info($"Transposed Row: {rowData}");
        }

        return transposedRows; // Return the list of transposed rows
    }

    private Dictionary<int, string> getChangePointDescriptions()
    {
        var cpDescriptions = Project.Current.GetObject("Model/Descriptions/CPDescriptions");
        var cpDescriptionsChildren = cpDescriptions.Children;
        var cpDescriptionsDict = new Dictionary<int, string>();

        foreach (var cp in cpDescriptionsChildren)
        {
            var cpDescriptionVariable = cp as UAVariable;
            if (cpDescriptionVariable == null)
            {
                Log.Error("Child is not a UAVariable");
                continue;
            }

            // Extract the integer index from the BrowseName
            string browseName = cp.BrowseName;
            if (int.TryParse(new string(browseName.Where(char.IsDigit).ToArray()), out int index))
            {

                LocalizedText cpDescription = (LocalizedText)cpDescriptionVariable.Value;
                if (cpDescription != null)
                {
                    if (cpDescription.TextId == "")
                    {
                        cpDescriptionsDict[index] = cpDescription.Text;
                    }
                    else
                    {
                        cpDescriptionsDict[index] = cpDescription.TextId;
                    }
                    // Log the extracted TextId if needed
                    // Log.Info(cpDescription.TextId);
                }
                else
                {
                    Log.Error($"CPDescription value is null for index {index}");
                }
            }
            else
            {
                Log.Error($"Failed to parse index from BrowseName: {browseName}");
            }
        }

        return cpDescriptionsDict;
    }

    private string GetFontRegularFilePath()
    {
        var fontPathVariable = LogicObject.GetVariable("FontPathRegular");
        if (fontPathVariable == null)
        {
            Log.Error("FontFile variable not found");
            return "";
        }

        return new ResourceUri(fontPathVariable.Value).Uri;
    }
    private string GetFontBoldFilePath()
    {
        var fontPathVariable = LogicObject.GetVariable("FontPathBold");
        if (fontPathVariable == null)
        {
            Log.Error("FontFile variable not found");
            return "";
        }

        return new ResourceUri(fontPathVariable.Value).Uri;
    }
    private string GetReportExportFilePath()
    {
        var reportExportPathVariable = LogicObject.GetVariable("ChangeOverChartReport");
        if (reportExportPathVariable == null)
        {
            Log.Error("COChartFile variable not found");
            return "";
        }

        return new ResourceUri(reportExportPathVariable.Value).Uri;
    }

    private string GetFlipChartReportExportFilePath()
    {
        var reportExportPathVariable = LogicObject.GetVariable("FlipChartReport");
        if (reportExportPathVariable == null)
        {
            Log.Error("FlipChartFile variable not found");
            return "";
        }

        return new ResourceUri(reportExportPathVariable.Value).Uri;
    }

    private string GetLogoFilePath()
    {
        var logoPathVariable = LogicObject.GetVariable("logo");
        if (logoPathVariable == null)
        {
            Log.Error("LogoFile variable not found");
            return "";
        }

        return new ResourceUri(logoPathVariable.Value).Uri;
    }
    private string GetMachineImageByOptionsPath()
    {
        var logoPathVariable = LogicObject.GetVariable("MachineImageByOptions");
        if (logoPathVariable == null)
        {
            Log.Error("MachineImageByOptions variable not found");
            return "";
        }

        return new ResourceUri(logoPathVariable.Value).Uri;
    }
    private string GetCOCalloutFilePath()
    {
        var logoPathVariable = LogicObject.GetVariable("COCalloutImage");
        if (logoPathVariable == null)
        {
            Log.Error("COCalloutImagePath variable not found");
            return "";
        }

        return new ResourceUri(logoPathVariable.Value).Uri;
    }



}
