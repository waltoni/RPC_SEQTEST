#region Using directives
using UAManagedCore;
using FTOptix.UI;
using FTOptix.NetLogic;
using System.Text;
#endregion

public class PalletVisualRecipeSelection : BaseNetLogic
{
    [ExportMethod]
    public void replaceSVG()
    {
        AdvancedSVGImage svgImage = LogicObject.Owner.Get<AdvancedSVGImage>("SVGPalletVisualRecipeSelect");

        var paramObj = LogicObject.Owner.GetObject("PalletVisualizationDBParameters");
        float palletWidth = paramObj.GetVariable("PalletWidth").Value;
        float palletLength = paramObj.GetVariable("PalletLength").Value;
        float[] unitX = paramObj.GetVariable("UnitX").Value;
        float[] unitY = paramObj.GetVariable("UnitY").Value;
        float[] unitR = paramObj.GetVariable("UnitR").Value;
        int numPerLayer = paramObj.GetVariable("NumPerLayer").Value;
        float productWidth = paramObj.GetVariable("ProductWidth").Value;
        float productLength = paramObj.GetVariable("ProductLength").Value;
        int maxOverhangLength = paramObj.GetVariable("MaxOverhangLength").Value;
        int maxOverhangWidth = paramObj.GetVariable("MaxOverhangWidth").Value;

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

        // Create boxes positioned relative to pallet origin
        StringBuilder unitsSvgContent = new StringBuilder();
        for (int i = 1; i <= numPerLayer; i++)
        {
            float boxX = unitY[i] + palletOffsetX;  
            float boxY = unitX[i] + palletOffsetY; 
            float rotation = unitR[i];

            float boxWidth = (rotation == 90.0f) ? productLength : productWidth;
            float boxHeight = (rotation == 90.0f) ? productWidth : productLength;

            unitsSvgContent.AppendLine($"    <rect x=\"{boxX}\" y=\"{boxY}\" width=\"{boxWidth}\" height=\"{boxHeight}\" fill=\"{fillColor}\" stroke=\"{borderColor}\" stroke-width=\"{borderWidth}\" opacity=\"{opacity}\"/>");

            unitsSvgContent.AppendLine($"    <text x=\"{boxX + 1}\" y=\"{boxY + textSize}\" fill=\"{textColor}\" font-size=\"{textSize}\" font-family=\"Arial\">{i}</text>");
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
