#region Using directives
using UAManagedCore;
using FTOptix.UI;
using FTOptix.NetLogic;
using System.Text;
#endregion

public class PalletFlipVisualizer : BaseNetLogic
{
    // Cached references to avoid repeated lookups
    private IUAObject paramObj;
    private AdvancedSVGImage svgImage;
    private IUAVariable palletWidthVar;
    private IUAVariable palletLengthVar;
    private IUAVariable unitFXVar;
    private IUAVariable unitFYVar;
    private IUAVariable unitFRVar;
    private IUAVariable numPerLayerVar;
    private IUAVariable productWidthVar;
    private IUAVariable productLengthVar;
    private IUAVariable panelIndexVar;
    private IUAVariable selectedBoxVar;
    private IUAVariable maxOverhangLengthVar;
    private IUAVariable maxOverhangWidthVar;
    private IUAVariable rot180Var;
    private IUAVariable labelOrientVar;
    private IUAVariable seqFUnitNumVar;

    public override void Start()
    {
        // Cache object and variable references once at startup
        svgImage = LogicObject.Owner.Get<AdvancedSVGImage>("AdvancedSVGImageFlip");
        paramObj = LogicObject.Owner.GetObject("PalletFlipVisualizationParameters");
        
        if (paramObj != null)
        {
            palletWidthVar = paramObj.GetVariable("PalletWidth");
            palletLengthVar = paramObj.GetVariable("PalletLength");
            unitFXVar = paramObj.GetVariable("UnitFX");
            unitFYVar = paramObj.GetVariable("UnitFY");
            unitFRVar = paramObj.GetVariable("UnitFR");
            numPerLayerVar = paramObj.GetVariable("NumPerLayer");
            productWidthVar = paramObj.GetVariable("ProductWidth");
            productLengthVar = paramObj.GetVariable("ProductLength");
            panelIndexVar = paramObj.GetVariable("RecipeEditPanelIndex");
            selectedBoxVar = paramObj.GetVariable("SelectedBoxForCustom");
            maxOverhangLengthVar = paramObj.GetVariable("MaxOverhangLength");
            maxOverhangWidthVar = paramObj.GetVariable("MaxOverhangWidth");
            rot180Var = paramObj.GetVariable("Rot180");
            labelOrientVar = paramObj.GetVariable("LabelOrient");
            seqFUnitNumVar = paramObj.GetVariable("SeqF_UnitNum");
        }
    }

    public override void Stop()
    {
        // Clear cached references
        paramObj = null;
        svgImage = null;
        palletWidthVar = null;
        palletLengthVar = null;
        unitFXVar = null;
        unitFYVar = null;
        unitFRVar = null;
        numPerLayerVar = null;
        productWidthVar = null;
        productLengthVar = null;
        panelIndexVar = null;
        selectedBoxVar = null;
        maxOverhangLengthVar = null;
        maxOverhangWidthVar = null;
        rot180Var = null;
        labelOrientVar = null;
        seqFUnitNumVar = null;
    }

    [ExportMethod]
    public void replaceSVG()
    {
        if (svgImage == null || paramObj == null)
        {
            Log.Error("PalletFlipVisualizer", "Cached references not initialized. Call Start() first.");
            return;
        }

        // Use cached variable references
        float palletWidth = palletWidthVar.Value;
        float palletLength = palletLengthVar.Value;
        float[] unitFX = unitFXVar.Value;
        float[] unitFY = unitFYVar.Value;
        float[] unitFR = unitFRVar.Value;
        int numPerLayer = numPerLayerVar.Value;
        float productWidth = productWidthVar.Value;
        float productLength = productLengthVar.Value;
        int panelIndex = panelIndexVar.Value;
        int selectedBox = selectedBoxVar.Value;
        int maxOverhangLength = maxOverhangLengthVar.Value;
        int maxOverhangWidth = maxOverhangWidthVar.Value;
        bool[] rot180 = rot180Var != null ? rot180Var.Value : new bool[64];
        int labelOrient = labelOrientVar != null ? labelOrientVar.Value : 1;
        int[] seqFUnitNum = seqFUnitNumVar != null ? seqFUnitNumVar.Value : new int[64];

        // Calculate viewBox dimensions with max overhang margins
        float canvasWidth = palletWidth + 2 * maxOverhangWidth;
        float canvasHeight = palletLength + 2 * maxOverhangLength;

        // Pallet is always positioned at (maxOverhangWidth, maxOverhangLength) to center it with margins
        float palletOffsetX = maxOverhangWidth;
        float palletOffsetY = maxOverhangLength;

        // SVG template - pallet drawn at full dimensions, positioned with margins
        string svgContent = "<?xml version=\"1.0\" encoding=\"iso-8859-1\"?>\r\n" +
            "<svg version=\"1.1\" xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {canvasWidth} {canvasHeight}\" width=\"{canvasWidth}\" height=\"{canvasHeight}\">\r\n" +
            "  <g id=\"Pallet\">\r\n" +
            "    <!-- Base rectangle representing the pallet frame at full dimensions -->\r\n" +
            "    <rect x=\"{palletOffsetX}\" y=\"{palletOffsetY}\" width=\"{palletWidth}\" height=\"{palletLength}\" fill=\"#d2a679\" stroke=\"#8c6b39\" stroke-width=\"1\"/>\r\n" +
            "    <!-- Horizontal slats with wood grain effect -->\r\n" +
            "    {HorizontalSlats}\r\n" +
            "  </g>\r\n" +
            "</svg>";

        float slatHeight = palletLength / 12;
        float slatSpacing = palletLength / 6;

        // Create horizontal slats positioned relative to pallet
        StringBuilder horizontalSlats = new StringBuilder();
        for (int i = 0; i < 5; i++)
        {
            float slatY = palletOffsetY + (i + 1) * slatSpacing - slatHeight / 2;
            horizontalSlats.AppendLine($"    <rect x=\"{palletOffsetX}\" y=\"{slatY}\" width=\"{palletWidth}\" height=\"{slatHeight}\" fill=\"#b07535\" stroke=\"#8c6b39\" stroke-width=\"1\"/>");
        }

        // Replace pallet-related placeholders
        svgContent = svgContent
            .Replace("{palletLength}", palletLength.ToString())
            .Replace("{palletWidth}", palletWidth.ToString())
            .Replace("{palletOffsetX}", palletOffsetX.ToString())
            .Replace("{palletOffsetY}", palletOffsetY.ToString())
            .Replace("{canvasWidth}", canvasWidth.ToString())
            .Replace("{canvasHeight}", canvasHeight.ToString())
            .Replace("{HorizontalSlats}", horizontalSlats.ToString());

        // Box styling
        string borderColor = "black";
        string textColor = "black";
        float textSize = 3;
        string borderWidth = "0.1";
        string fillColor = "#8B4513";
        string opacity = ".9";

        bool onCustomPanel = panelIndex == 4;


        StringBuilder unitsSvgContent = new StringBuilder();
        for (int i = 1; i <= numPerLayer; i++)
        {
            if (onCustomPanel)
            {
                fillColor = i == selectedBox ? "#0474ab" : "#8B4513";
            }
            float boxX = unitFY[i] + palletOffsetX;
            float boxY = unitFX[i] + palletOffsetY;
            float rotation = unitFR[i];

            float boxWidth = (rotation == 90.0f) ? productLength : productWidth;
            float boxHeight = (rotation == 90.0f) ? productWidth : productLength;

            //add box
            unitsSvgContent.AppendLine($"    <rect x=\"{boxX}\" y=\"{boxY}\" width=\"{boxWidth}\" height=\"{boxHeight}\" fill=\"{fillColor}\" stroke=\"{borderColor}\" stroke-width=\"{borderWidth}\" opacity=\"{opacity}\"/>");

            //add label
            unitsSvgContent.AppendLine($"    <text x=\"{boxX + 1}\" y=\"{boxY + textSize}\" fill=\"{textColor}\" font-size=\"{textSize}\" font-family=\"Arial\">{i}</text>");
            
            // Draw line on specified side of box based on LabelOrient, UnitFR, and Rot180
            // LabelOrient: Bit flags - Bit 1(2)=Bottom, Bit 2(4)=Top, Bit 3(8)=Left, Bit 4(16)=Right (bit 0 not used)
            // UnitFR: If != 0, rotate LabelOrient one position clockwise
            // Rot180 is 1-based (indices 1-64). SeqF_UnitNum[k] contains the case number that Rot180[k] applies to
            // Example: if Rot180[1] = true and SeqF_UnitNum[1] = 15, then case 15 should be rotated
            // To find which Rot180 index applies to case i, search for k where SeqF_UnitNum[k] == i
            int rot180Index = 0; // 0 means not found
            // Search through Rot180 indices (1-64) to find which one maps to case i
            // SeqF_UnitNum[k] contains the case number that Rot180[k] applies to
            // Example: if SeqF_UnitNum[1] = 15, then Rot180[1] applies to case 15
            for (int k = 1; k < seqFUnitNum.Length && k <= 64; k++)
            {
                if (seqFUnitNum[k] == i)
                {
                    rot180Index = k;
                    break;
                }
            }
            
            // Start with base LabelOrient - determine which bit is set (bits 1-4, not bit 0)
            // Bit 1 (value 2) = Bottom, Bit 2 (value 4) = Top, Bit 3 (value 8) = Left, Bit 4 (value 16) = Right
            int baseOrient = 0;
            if ((labelOrient & 2) != 0) baseOrient = 2;      // Bit 1 → Bottom
            else if ((labelOrient & 4) != 0) baseOrient = 4; // Bit 2 → Top
            else if ((labelOrient & 8) != 0) baseOrient = 8; // Bit 3 → Left
            else if ((labelOrient & 16) != 0) baseOrient = 16; // Bit 4 → Right
            
            int finalOrient = baseOrient;
            
            // Apply UnitFR rotation (one position clockwise if UnitFR != 0)
            // Top and Bottom rotate clockwise: Top(4) -> Right(16), Bottom(2) -> Left(8)
            // Left and Right rotate clockwise: Left(8) -> Top(4), Right(16) -> Bottom(2)
            if (rotation != 0.0f)
            {
                switch (finalOrient)
                {
                    case 2: finalOrient = 8; break;   // Bottom -> Left (clockwise)
                    case 4: finalOrient = 16; break;  // Top -> Right (clockwise)
                    case 8: finalOrient = 4; break;   // Left -> Top (clockwise)
                    case 16: finalOrient = 2; break;  // Right -> Bottom (clockwise)
                }
            }
            
            // Apply Rot180 rotation (180 degrees if true)
            // Check rot180[rot180Index] where rot180Index is the Rot180 index that maps to visual position i
            if (rot180Index >= 1 && rot180Index < rot180.Length && rot180[rot180Index])
            {
                // Rotate 180 degrees: 2<->4, 8<->16
                switch (finalOrient)
                {
                    case 2: finalOrient = 4; break;   // Bottom -> Top
                    case 4: finalOrient = 2; break;   // Top -> Bottom
                    case 8: finalOrient = 16; break;  // Left -> Right
                    case 16: finalOrient = 8; break;  // Right -> Left
                }
            }
            
            // Draw line on the side determined by finalOrient (always draw if baseOrient was found)
            if (baseOrient != 0)
            {
                
                // Draw line on the side determined by finalOrient
                // finalOrient: 2=Bottom, 4=Top, 8=Left, 16=Right
                // Line length is half the short side of the box, centered on each side
                // Lines are inset slightly to prevent clipping at edges
                string lineColor = "white";
                float lineWidth = 0.4f;
                float inset = lineWidth / 2.0f + 0.1f; // Inset by half stroke width plus small margin
                float shortSide = boxWidth < boxHeight ? boxWidth : boxHeight;
                float lineLength = shortSide / 2.0f;
                
                // Convert to case number for drawing
                // 2=Bottom(1), 4=Top(2), 8=Left(3), 16=Right(4)
                int drawSide = 0;
                if (finalOrient == 2) drawSide = 1;       // Bottom
                else if (finalOrient == 4) drawSide = 2;  // Top
                else if (finalOrient == 8) drawSide = 3;  // Left
                else if (finalOrient == 16) drawSide = 4; // Right
                
                switch (drawSide)
                {
                    case 1: // Bottom side - horizontal line centered
                        {
                            float lineX1 = boxX + (boxWidth - lineLength) / 2.0f + inset;
                            float lineX2 = boxX + (boxWidth + lineLength) / 2.0f - inset;
                            float lineY = boxY + boxHeight - inset;
                            unitsSvgContent.AppendLine($"    <line x1=\"{lineX1}\" y1=\"{lineY}\" x2=\"{lineX2}\" y2=\"{lineY}\" stroke=\"{lineColor}\" stroke-width=\"{lineWidth}\"/>");
                        }
                        break;
                    case 2: // Top side - horizontal line centered
                        {
                            float lineX1 = boxX + (boxWidth - lineLength) / 2.0f + inset;
                            float lineX2 = boxX + (boxWidth + lineLength) / 2.0f - inset;
                            float lineY = boxY + inset;
                            unitsSvgContent.AppendLine($"    <line x1=\"{lineX1}\" y1=\"{lineY}\" x2=\"{lineX2}\" y2=\"{lineY}\" stroke=\"{lineColor}\" stroke-width=\"{lineWidth}\"/>");
                        }
                        break;
                    case 3: // Left side - vertical line centered
                        {
                            float lineX = boxX + inset;
                            float lineY1 = boxY + (boxHeight - lineLength) / 2.0f + inset;
                            float lineY2 = boxY + (boxHeight + lineLength) / 2.0f - inset;
                            unitsSvgContent.AppendLine($"    <line x1=\"{lineX}\" y1=\"{lineY1}\" x2=\"{lineX}\" y2=\"{lineY2}\" stroke=\"{lineColor}\" stroke-width=\"{lineWidth}\"/>");
                        }
                        break;
                    case 4: // Right side - vertical line centered
                        {
                            float lineX = boxX + boxWidth - inset;
                            float lineY1 = boxY + (boxHeight - lineLength) / 2.0f + inset;
                            float lineY2 = boxY + (boxHeight + lineLength) / 2.0f - inset;
                            unitsSvgContent.AppendLine($"    <line x1=\"{lineX}\" y1=\"{lineY1}\" x2=\"{lineX}\" y2=\"{lineY2}\" stroke=\"{lineColor}\" stroke-width=\"{lineWidth}\"/>");
                        }
                        break;
                }
            }
        }


        // Insert boxes into SVG content
        int insertIndex = svgContent.IndexOf("</g>");
        if (insertIndex != -1)
        {
            svgContent = svgContent.Insert(insertIndex, unitsSvgContent.ToString());
        }
        else
        {
            Log.Error("PalletVisualizer", "Failed to find insertion point in SVG content.");
            return;
        }

        // Update the SVG with modified content
        svgImage.SetImageContent(svgContent);
    }
}
