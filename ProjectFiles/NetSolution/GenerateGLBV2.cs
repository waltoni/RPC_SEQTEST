using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Reflection;
using FTOptix.NetLogic;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.HMIProject;
using FTOptix.CoreBase;
using FTOptix.UI;
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
using FTOptix.OPCUAServer;

public class GenerateGLBV2 : BaseNetLogic
{
    private const float NeighborTolerance = 0.01f;

    struct CubeData
    {
        public float X;
        public float Y;
        public int R;
    }

    [ExportMethod]
    public void CreateGLB()
    {
        Log.Info("GenerateGLBV2: building final pallet scene with interior culling...");

        var paramObj = LogicObject.Owner.GetObject("PalletVisualParams");
        if (paramObj == null)
        {
            Log.Info("GenerateGLBV2: PalletVisualParams not found.");
            return;
        }

        int numPerLayer = paramObj.GetVariable("NumPerLayer")?.Value ?? 0;
        int numLayers = paramObj.GetVariable("NumLayers")?.Value ?? 1;

        var unitXVar = paramObj.GetVariable("UnitX");
        var unitYVar = paramObj.GetVariable("UnitY");
        var unitRVar = paramObj.GetVariable("UnitR");

        var unitFXVar = paramObj.GetVariable("UnitFX");
        var unitFYVar = paramObj.GetVariable("UnitFY");
        var unitFRVar = paramObj.GetVariable("UnitFR");

        if (unitXVar == null || unitYVar == null || unitRVar == null)
        {
            Log.Info("GenerateGLBV2: Missing UnitX, UnitY, or UnitR.");
            return;
        }

        var unitXArray = (float[])unitXVar.Value;
        var unitYArray = (float[])unitYVar.Value;
        var unitRArray = (float[])unitRVar.Value;

        float[] unitFXArray = null;
        float[] unitFYArray = null;
        float[] unitFRArray = null;

        if (unitFXVar != null && unitFYVar != null && unitFRVar != null)
        {
            unitFXArray = (float[])unitFXVar.Value;
            unitFYArray = (float[])unitFYVar.Value;
            unitFRArray = (float[])unitFRVar.Value;
        }

        // 1-based arrays
        numPerLayer = Math.Min(
            numPerLayer,
            Math.Min(unitXArray.Length, unitYArray.Length) - 1);

        if (numPerLayer <= 0)
        {
            Log.Info("GenerateGLBV2: numPerLayer <= 0; nothing to draw.");
            return;
        }

        Log.Info($"GenerateGLBV2: numPerLayer={numPerLayer}, numLayers={numLayers}");

        // Dimensions
        float cubeLength = paramObj.GetVariable("ProductLength").Value; // X
        float cubeWidth = paramObj.GetVariable("ProductWidth").Value;  // Z
        float cubeHeight = paramObj.GetVariable("ProductHeight").Value; // Y

        float palletLength = paramObj.GetVariable("PalletLength").Value;
        float palletWidth = paramObj.GetVariable("PalletWidth").Value;

        // ------------------------------------------------------------------
        // Materials
        // ------------------------------------------------------------------
        var cubeMat = new MaterialBuilder()
            .WithMetallicRoughnessShader()
            .WithBaseColor(new Vector4(0.73f, 0.55f, 0.33f, 1f)); // cardboard

        var outlineMat = new MaterialBuilder()
            .WithUnlitShader()
            .WithAlpha(AlphaMode.BLEND)
            .WithBaseColor(new Vector4(0f, 0f, 0f, 0.25f));

        var palletMat = new MaterialBuilder()
            .WithMetallicRoughnessShader()
            .WithBaseColor(new Vector4(0.8f, 0.65f, 0.4f, 1f));

        var sheetMat = new MaterialBuilder()
            .WithMetallicRoughnessShader()
            .WithBaseColor(new Vector4(1f, 0.95f, 0.3f, 1f));

        var xMat = new MaterialBuilder().WithUnlitShader().WithBaseColor(new Vector4(1, 0, 0, 1));
        var yMat = new MaterialBuilder().WithUnlitShader().WithBaseColor(new Vector4(0, 1, 0, 1));
        var zMat = new MaterialBuilder().WithUnlitShader().WithBaseColor(new Vector4(0, 0.3f, 1, 1));
        var originMat = new MaterialBuilder().WithUnlitShader().WithBaseColor(new Vector4(1, 0, 0, 1));

        // ------------------------------------------------------------------
        // Mesh templates
        // ------------------------------------------------------------------
        // Note: We'll create custom meshes per cube with only visible faces
        // Base cube mesh template (used for outline only - outline always shows all faces)
        float outlineScale = 1.01f;
        var outlineMesh = new MeshBuilder<VertexPositionNormal, VertexEmpty, VertexEmpty>("CubeOutline");
        AddCubeCustom(outlineMesh, outlineMat,
            cubeLength * outlineScale,
            cubeHeight * outlineScale,
            cubeWidth * outlineScale);

        var scene = new SceneBuilder();
        
        // ------------------------------------------------------------------
        // Pallet geometry
        // ------------------------------------------------------------------
        float palletVisWidth = 48f; // X
        float palletVisDepth = 40f; // Z
        float palletHeightGfx = 5f;

        float boardThickness = 0.5f;
        float boardWidth = 5.5f;
        float stringerHeight = 3.5f;
        float stringerWidth = 3.5f;

        float palletYOffset = -(palletHeightGfx / 2f) + 1f;

        var topBoardMesh = new MeshBuilder<VertexPositionNormal, VertexEmpty, VertexEmpty>("TopDeckBoard");
        AddCubeCustom(topBoardMesh, palletMat, boardWidth, boardThickness, palletVisDepth);

        var stringerMesh = new MeshBuilder<VertexPositionNormal, VertexEmpty, VertexEmpty>("Stringer");
        AddCubeCustom(stringerMesh, palletMat, palletVisWidth, stringerHeight, stringerWidth);

        var bottomBoardMesh = new MeshBuilder<VertexPositionNormal, VertexEmpty, VertexEmpty>("BottomDeckBoard");
        AddCubeCustom(bottomBoardMesh, palletMat, boardWidth, boardThickness, palletVisDepth);

        // top boards
        int topBoardCount = 7;
        float topBoardSpacing =
            (palletVisWidth - (topBoardCount * boardWidth)) / (topBoardCount - 1);

        float topBoardY =
            -(palletHeightGfx / 2f) + stringerHeight + (boardThickness / 2f) + palletYOffset;

        float halfWidthVis = palletVisWidth / 2f;
        for (int i = 0; i < topBoardCount; i++)
        {
            float xOffset =
                -halfWidthVis + (i * (boardWidth + topBoardSpacing)) + (boardWidth / 2);

            scene.AddRigidMesh(topBoardMesh,
                Matrix4x4.CreateTranslation(xOffset, topBoardY, 0));
        }

        // stringers
        float halfDepthVis = palletVisDepth / 2f;
        float[] stringerZ =
        {
            -halfDepthVis + stringerWidth/2,
            0,
            halfDepthVis - stringerWidth/2
        };

        float stringerY =
            -(palletHeightGfx / 2f) + (stringerHeight / 2f) + palletYOffset;

        foreach (float zPos in stringerZ)
            scene.AddRigidMesh(stringerMesh,
                Matrix4x4.CreateTranslation(0, stringerY, zPos));

        // bottom boards
        int bottomBoardCount = 3;
        float bottomBoardSpacing =
            (palletVisWidth - (bottomBoardCount * boardWidth)) / (bottomBoardCount - 1);

        float bottomBoardY =
            -(palletHeightGfx / 2f) + (boardThickness / 2f) + palletYOffset;

        for (int i = 0; i < bottomBoardCount; i++)
        {
            float xOffset =
                -halfWidthVis + (i * (bottomBoardSpacing + boardWidth)) + (boardWidth / 2);

            scene.AddRigidMesh(bottomBoardMesh,
                Matrix4x4.CreateTranslation(xOffset, bottomBoardY, 0));
        }
        
        // ------------------------------------------------------------------
        // Axes + origin
        // ------------------------------------------------------------------
        float cornerOffsetX = palletLength / 2f;
        float cornerOffsetZ = -palletWidth / 2f;
        
        var markerMesh = new MeshBuilder<VertexPositionNormal, VertexEmpty, VertexEmpty>("OriginMarker");
        AddCubeCustom(markerMesh, originMat, 0.25f, 0.25f, 0.25f);
        scene.AddRigidMesh(markerMesh, Matrix4x4.CreateTranslation(cornerOffsetX, 0.25f, cornerOffsetZ));

        float arrowLength = 10f;
        float arrowThickness = 0.2f;

        var xArrowMesh = new MeshBuilder<VertexPositionNormal, VertexEmpty, VertexEmpty>("X_Axis");
        AddCubeCustom(xArrowMesh, xMat, arrowLength, arrowThickness, arrowThickness);
        scene.AddRigidMesh(xArrowMesh,
            Matrix4x4.CreateTranslation(cornerOffsetX - arrowLength / 2f, 0, cornerOffsetZ));

        var yArrowMesh = new MeshBuilder<VertexPositionNormal, VertexEmpty, VertexEmpty>("Y_Axis");
        AddCubeCustom(yArrowMesh, yMat, arrowThickness, arrowLength, arrowThickness);
        scene.AddRigidMesh(yArrowMesh,
            Matrix4x4.CreateTranslation(cornerOffsetX, arrowLength / 2f, cornerOffsetZ));

        var zArrowMesh = new MeshBuilder<VertexPositionNormal, VertexEmpty, VertexEmpty>("Z_Axis");
        AddCubeCustom(zArrowMesh, zMat, arrowThickness, arrowThickness, arrowLength);
        scene.AddRigidMesh(zArrowMesh,
            Matrix4x4.CreateTranslation(cornerOffsetX, 0, cornerOffsetZ + arrowLength / 2f));

        // ------------------------------------------------------------------
        // Slip sheets
        // ------------------------------------------------------------------
        int layerSheetMask = paramObj.GetVariable("LayerSheet")?.Value ?? 0;

        float sheetX = 48f;
        float sheetZ = 40f;
        float sheetY = 0.10f;

        var sheetMesh = new MeshBuilder<VertexPositionNormal, VertexEmpty, VertexEmpty>("SlipSheet");
        AddCubeCustom(sheetMesh, sheetMat, sheetX, sheetY, sheetZ);

        if ((layerSheetMask & 1) != 0)
        {
            scene.AddRigidMesh(sheetMesh,
                Matrix4x4.CreateTranslation(0, sheetY / 2f, 0));
        }

        for (int layer = 0; layer < numLayers; layer++)
        {
            int bit = (layer + 1);
            if (((layerSheetMask >> bit) & 1) == 1)
            {
                float yPos = (layer + 1) * cubeHeight + (sheetY / 2f);
                scene.AddRigidMesh(sheetMesh, Matrix4x4.CreateTranslation(0, yPos, 0));
            }
        }
        
        // ------------------------------------------------------------------
        // Build layer patterns (normal / flipped)
        // ------------------------------------------------------------------
        var normalCubes = LoadCubes(unitXArray, unitYArray, unitRArray, numPerLayer);
        var flippedCubes =
            (unitFXArray != null && unitFYArray != null && unitFRArray != null)
            ? LoadCubes(unitFXArray, unitFYArray, unitFRArray, numPerLayer)
            : normalCubes;

        int layerFlip = paramObj.GetVariable("LayerFlip")?.Value ?? 0;

        var layerCubes = new List<CubeInstance>[numLayers];

        for (int layer = 0; layer < numLayers; layer++)
        {
            bool isFlippedLayer = ((layerFlip >> (layer + 1)) & 1) == 1;
            var source = isFlippedLayer ? flippedCubes : normalCubes;
            var instances = new List<CubeInstance>(source.Count);

            foreach (var data in source)
                instances.Add(CreateInstance(data, cubeLength, cubeWidth));

            layerCubes[layer] = instances;
        }

        int topLayerIndex = numLayers - 1;

        // ------------------------------------------------------------------
        // Render cubes with interior-center removal on all but the top layer
        // ------------------------------------------------------------------
        for (int layer = 0; layer < numLayers; layer++)
        {
            var cubes = layerCubes[layer];
            if (cubes == null || cubes.Count == 0) continue;

            bool isTopLayer = (layer == topLayerIndex);

            for (int i = 0; i < cubes.Count; i++)
            {
                var cube = cubes[i];
                
                // Check for neighbors on all sides
                bool hasTop = HasTopNeighbor(layerCubes, layer, cube);
                bool hasLeft = HasSideNeighbor(cubes, i, cube, Side.Left);
                bool hasRight = HasSideNeighbor(cubes, i, cube, Side.Right);
                bool hasFront = HasSideNeighbor(cubes, i, cube, Side.Front);
                bool hasBack = HasSideNeighbor(cubes, i, cube, Side.Back);

                // Fully cull cubes that are completely hidden (only for non-top layers)
                bool culled = false;
                if (!isTopLayer)
                {
                    bool fullyHidden = hasTop && hasLeft && hasRight && hasFront && hasBack;
                    culled = fullyHidden;
                }

                if (culled) continue;

                var data = cube.Data;
                bool rot90 = cube.Rot90;
                float angle = rot90 ? MathF.PI / 2f : 0f;
                var rotation = Matrix4x4.CreateRotationY(angle);

                float halfX = cube.SizeX / 2f;
                float halfZ = cube.SizeZ / 2f;

                float centerX = cornerOffsetX - data.X - halfX;
                float centerZ = cornerOffsetZ + data.Y + halfZ;
                float layerGap = 0f;
                float centerY = (cubeHeight / 2f) + (layer * (cubeHeight + layerGap));

                var translation = Matrix4x4.CreateTranslation(centerX, centerY, centerZ);
                var transform = rotation * translation;

                // Create a custom mesh for this cube (all faces except bottom)
                var cubeMesh = new MeshBuilder<VertexPositionNormal, VertexEmpty, VertexEmpty>($"Cube_{layer}_{i}");
                AddCubeCustom(cubeMesh, cubeMat, cubeLength, cubeHeight, cubeWidth);
                
                scene.AddRigidMesh(cubeMesh, transform);
                scene.AddRigidMesh(outlineMesh, transform);
            }
        }

        // ------------------------------------------------------------------
        // Export GLB A/B
        // ------------------------------------------------------------------
        var model = scene.ToGltf2();
        
        // Set DoubleSided = true for all materials to ensure all faces render correctly
        // (including top surfaces that might have normals pointing in different directions)
        foreach (var m in model.LogicalMaterials)
            m.DoubleSided = true;

        string viewerDir = Path.Combine(Project.Current.ProjectDirectory, "Viewer");

        var currentNameVar = LogicObject.GetVariable("CurrentFileName");
        if (currentNameVar == null)
        {
            currentNameVar = InformationModel.MakeVariable("CurrentFileName", OpcUa.DataTypes.String);
            LogicObject.Add(currentNameVar);
            currentNameVar.Value = "pallet_layout_A";
        }

        string cur = currentNameVar.Value;
        string next = cur == "pallet_layout_A" ? "pallet_layout_B" : "pallet_layout_A";
        currentNameVar.Value = next;

        string outputPath = Path.Combine(viewerDir, $"{next}.glb");

        try
        {
            if (!Directory.Exists(viewerDir))
                Directory.CreateDirectory(viewerDir);

            model.SaveGLB(outputPath);
            Log.Info($"GenerateGLBV2: GLB saved to {outputPath}");
        }
        catch (Exception ex)
        {
            Log.Info($"GenerateGLBV2: Failed to save GLB: {ex.Message}");
        }

        // ------------------------------------------------------------------
        // Update HTML / browser
        // ------------------------------------------------------------------
        var browser = Owner.Get<WebBrowser>("WebBrowser");
        browser.Visible = false;

        string templatePath = new ResourceUri(LogicObject.GetVariable("TempFile").Value).Uri;
        string filePath = new ResourceUri(LogicObject.GetVariable("DestFile").Value).Uri;

        string text = File.ReadAllText(templatePath);
        string currentNameHtml = LogicObject.GetVariable("CurrentFileName")?.Value ?? "pallet_layout_A";
        text = text.Replace("$File", currentNameHtml);

        File.WriteAllText(filePath, text);

        browser.Refresh();
        browser.Visible = true;
    }

    // ----------------------------------------------------------------------
    // Cube geometry
    // ----------------------------------------------------------------------
    private static void AddCubeCustom(
        MeshBuilder<VertexPositionNormal, VertexEmpty, VertexEmpty> mesh,
        MaterialBuilder mat,
        float width, float height, float depth)
    {
        float hx = width / 2f;
        float hy = height / 2f;
        float hz = depth / 2f;

        var normals = new[]
        {
            new Vector3(0, 0,  1),  // front (0)
            new Vector3(0, 0, -1),  // back (1)
            new Vector3(1, 0,  0),  // right (2)
            new Vector3(-1, 0,  0), // left (3)
            new Vector3(0, 1,  0), // top (4)
            new Vector3(0, -1, 0)  // bottom (5) - always excluded
        };

        var positions = new[]
        {
            new Vector3(-hx, -hy,  hz), new Vector3( hx, -hy,  hz),
            new Vector3( hx,  hy,  hz), new Vector3(-hx,  hy,  hz),

            new Vector3( hx, -hy, -hz), new Vector3(-hx, -hy, -hz),
            new Vector3(-hx,  hy, -hz), new Vector3( hx,  hy, -hz),

            new Vector3( hx, -hy,  hz), new Vector3( hx, -hy, -hz),
            new Vector3( hx,  hy, -hz), new Vector3( hx,  hy,  hz),

            new Vector3(-hx, -hy, -hz), new Vector3(-hx, -hy,  hz),
            new Vector3(-hx,  hy,  hz), new Vector3(-hx,  hy, -hz),

            new Vector3(-hx,  hy,  hz), new Vector3( hx,  hy,  hz),
            new Vector3( hx,  hy, -hz), new Vector3(-hx,  hy, -hz),

            new Vector3(-hx, -hy, -hz), new Vector3( hx, -hy, -hz),
            new Vector3( hx, -hy,  hz), new Vector3(-hx, -hy,  hz)
        };

        int[,] faceIndices =
        {
            {  0,  1,  2,  3 }, // front (0)
            {  4,  5,  6,  7 }, // back (1)
            {  8,  9, 10, 11 }, // right (2)
            { 12, 13, 14, 15 }, // left (3)
            { 19, 18, 17, 16 }, // top (4)
            { 20, 21, 22, 23 }  // bottom (5) - always excluded
        };

        var prim = mesh.UsePrimitive(mat);

        // Only create 5 faces (skip bottom face - index 5) since bottom is never visible
        for (int f = 0; f < 5; f++)
        {
            Vector3 n = normals[f];

            var v0 = new VertexPositionNormal(positions[faceIndices[f, 0]], n);
            var v1 = new VertexPositionNormal(positions[faceIndices[f, 1]], n);
            var v2 = new VertexPositionNormal(positions[faceIndices[f, 2]], n);
            var v3 = new VertexPositionNormal(positions[faceIndices[f, 3]], n);

            prim.AddTriangle(v0, v1, v2);
            prim.AddTriangle(v2, v3, v0);
        }
    }

    // ----------------------------------------------------------------------
    // Cube loader
    // ----------------------------------------------------------------------
    private List<CubeData> LoadCubes(
        float[] unitXArray,
        float[] unitYArray,
        float[] unitRArray,
        int numCubes)
    {
        var cubes = new List<CubeData>();

        int lastIdx = Math.Min(unitXArray.Length,
                        Math.Min(unitYArray.Length, unitRArray.Length)) - 1;
        int maxIdx = Math.Min(numCubes, lastIdx);

        for (int i = 1; i <= maxIdx; i++)
        {
            cubes.Add(new CubeData
            {
                X = unitXArray[i],
                Y = unitYArray[i],
                R = (int)unitRArray[i]
            });
        }

        return cubes;
    }

    private static CubeInstance CreateInstance(CubeData data, float cubeLength, float cubeWidth)
    {
        bool rot90 = data.R == 90;
        float sizeX = rot90 ? cubeWidth : cubeLength;
        float sizeZ = rot90 ? cubeLength : cubeWidth;

        return new CubeInstance(
            data,
            rot90,
            sizeX,
            sizeZ,
            data.X,
            data.X + sizeX,
            data.Y,
            data.Y + sizeZ);
    }

    private static bool HasTopNeighbor(List<CubeInstance>[] layerCubes, int layerIndex, CubeInstance target)
    {
        int above = layerIndex + 1;
        if (above >= layerCubes.Length) return false;

        var candidates = layerCubes[above];
        if (candidates == null) return false;

        foreach (var other in candidates)
        {
            if (RangesOverlap(target.MinX, target.MaxX, other.MinX, other.MaxX) &&
                RangesOverlap(target.MinZ, target.MaxZ, other.MinZ, other.MaxZ))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasSideNeighbor(IReadOnlyList<CubeInstance> cubes, int targetIndex, CubeInstance target, Side side)
    {
        for (int i = 0; i < cubes.Count; i++)
        {
            if (i == targetIndex) continue;
            var other = cubes[i];

            switch (side)
            {
                case Side.Left:
                    if (AlmostEquals(other.MaxX, target.MinX) &&
                        RangesOverlap(other.MinZ, other.MaxZ, target.MinZ, target.MaxZ))
                        return true;
                    break;
                case Side.Right:
                    if (AlmostEquals(other.MinX, target.MaxX) &&
                        RangesOverlap(other.MinZ, other.MaxZ, target.MinZ, target.MaxZ))
                        return true;
                    break;
                case Side.Front:
                    if (AlmostEquals(other.MinZ, target.MaxZ) &&
                        RangesOverlap(other.MinX, other.MaxX, target.MinX, target.MaxX))
                        return true;
                    break;
                case Side.Back:
                    if (AlmostEquals(other.MaxZ, target.MinZ) &&
                        RangesOverlap(other.MinX, other.MaxX, target.MinX, target.MaxX))
                        return true;
                    break;
            }
        }

        return false;
    }

    private static bool RangesOverlap(float minA, float maxA, float minB, float maxB)
    {
        return (MathF.Min(maxA, maxB) - MathF.Max(minA, minB)) > NeighborTolerance;
    }

    private static bool AlmostEquals(float a, float b)
    {
        return MathF.Abs(a - b) <= NeighborTolerance;
    }

    private readonly struct CubeInstance
    {
        public CubeInstance(
            CubeData data,
            bool rot90,
            float sizeX,
            float sizeZ,
            float minX,
            float maxX,
            float minZ,
            float maxZ)
        {
            Data = data;
            Rot90 = rot90;
            SizeX = sizeX;
            SizeZ = sizeZ;
            MinX = minX;
            MaxX = maxX;
            MinZ = minZ;
            MaxZ = maxZ;
        }

        public CubeData Data { get; }
        public bool Rot90 { get; }
        public float SizeX { get; }
        public float SizeZ { get; }
        public float MinX { get; }
        public float MaxX { get; }
        public float MinZ { get; }
        public float MaxZ { get; }
    }

    private enum Side
    {
        Left,
        Right,
        Front,
        Back
    }
}
