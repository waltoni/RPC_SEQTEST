#region Using directives
using System;
using UAManagedCore;
using FTOptix.UI;
using FTOptix.NetLogic;
using System.Text;
#endregion

public class Pallet3VisualStartupConf : BaseNetLogic
{

    [ExportMethod]
    public void replaceSVG()
    {
        // LogicObject.Owner is the button, so LogicObject.Owner is the MainWindow
        AdvancedSVGImage svgImage = LogicObject.Owner.Get<AdvancedSVGImage>("SVGPallet3Startup");

        // Retrieve parameters
        var paramObj = LogicObject.Owner.GetObject("Pallet3VisualizationParametersStartup");
        float palletWidth = paramObj.GetVariable("PalletWidth").Value;
        float palletLength = paramObj.GetVariable("PalletLength").Value;
        float[] unitX = paramObj.GetVariable("UnitX").Value;
        float[] unitY = paramObj.GetVariable("UnitY").Value;
        float[] unitR = paramObj.GetVariable("UnitR").Value;
        float[] unitFX = paramObj.GetVariable("UnitFX").Value;
        float[] unitFY = paramObj.GetVariable("UnitFY").Value;
        float[] unitFR = paramObj.GetVariable("UnitFR").Value;
        int numPerLayer = paramObj.GetVariable("NumPerLayer").Value;
        float productWidth = paramObj.GetVariable("ProductWidth").Value;
        float productLength = paramObj.GetVariable("ProductLength").Value;
        int maxOverhangLength = paramObj.GetVariable("MaxOverhangLength").Value;
        int maxOverhangWidth = paramObj.GetVariable("MaxOverhangWidth").Value;
        int unitsDone = paramObj.GetVariable("UnitsDone").Value;
        bool isTopLayerFlipped = paramObj.GetVariable("isTopLayerFlipped").Value;
        int[] seqUnitNum = paramObj.GetVariable("Seq_UnitNum").Value;
        int[] seqFUnitNum = paramObj.GetVariable("SeqF_UnitNum").Value;
        bool isMirror = paramObj.GetVariable("isMirror").Value;

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

        // Calculate slat dimensions and positions relative to the pallet
        float slatHeight = palletLength / 12; // Adjust this ratio as needed for visual appeal
        float slatSpacing = palletLength / 6;  // Space slats evenly across pallet length

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

        StringBuilder unitsSvgContent = new StringBuilder();
        for (int i = 1; i <= numPerLayer; i++)
        {
            float x, y, r;
            int seq;

            if (!isTopLayerFlipped)
            {
                x = unitX[i];
                r = unitR[i];
                y = isMirror ? FlipY(unitY[i], r, productWidth, productLength, maxOverhangWidth, canvasWidth) : unitY[i];
                seq = Array.IndexOf(seqUnitNum, i);
            }
            else
            {
                x = unitFX[i];
                r = unitFR[i];
                y = isMirror ? FlipY(unitFY[i], r, productWidth, productLength, maxOverhangWidth, canvasWidth) : unitFY[i];
                seq = Array.IndexOf(seqFUnitNum, i);
            }

            // Final position on canvas
            float boxX = y + palletOffsetX;
            float boxY = x + palletOffsetY;
            fillColor = seq <= unitsDone ? "#8B4513" : "#959494";

            float width = (r == 90.0f) ? productLength : productWidth;
            float height = (r == 90.0f) ? productWidth : productLength;

            unitsSvgContent.AppendLine($"    <rect x=\"{boxX}\" y=\"{boxY}\" width=\"{width}\" height=\"{height}\" fill=\"{fillColor}\" stroke=\"{borderColor}\" stroke-width=\"{borderWidth}\" opacity=\"{opacity}\"/>");
            unitsSvgContent.AppendLine($"    <text x=\"{boxX + 1}\" y=\"{boxY + textSize}\" fill=\"{textColor}\" font-size=\"{textSize}\" font-family=\"Arial\">{i}</text>");
        }

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

        svgImage.SetImageContent(svgContent);
    }

    private float FlipY(float originalY, float rotation, float productWidth, float productLength, int overhangWidth, float canvasWidth)
    {
        float unitLength = (rotation == 90.0f || rotation == 270.0f) ? productLength : productWidth;
        return Math.Abs(canvasWidth - originalY - unitLength - (overhangWidth > 0 ? overhangWidth * 2 : 0));
    }
}
