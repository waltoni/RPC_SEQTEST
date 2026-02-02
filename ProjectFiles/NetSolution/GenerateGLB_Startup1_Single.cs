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

public class GenerateGLB_Startup1_Single : BaseNetLogic
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
        Log.Info("🔧 Generating XZ-plane (Y-up) scene with variable cube layers...");

        // ------------------------------------------------------------------
        // 🔹 Read from PalletVisualParams
        // ------------------------------------------------------------------
        var paramObj = LogicObject.Owner.GetObject("PalletVisualParams");
        if (paramObj == null)
        {
            Log.Info("❌ Could not find 'PalletVisualParams' under this logic's owner!");
            return;
        }

        int numPerLayer = paramObj.GetVariable("NumPerLayer")?.Value ?? 0;
        int numLayersCompleted = paramObj.GetVariable("NumLayers")?.Value ?? 1;  // completed full layers

        var unitXVar = paramObj.GetVariable("UnitX");
        var unitYVar = paramObj.GetVariable("UnitY");
        var unitRVar = paramObj.GetVariable("UnitR");

        var unitFXVar = paramObj.GetVariable("UnitFX");
        var unitFYVar = paramObj.GetVariable("UnitFY");
        var unitFRVar = paramObj.GetVariable("UnitFR");

        // NEW: sequence arrays (1-based case IDs)
        var seqVar = paramObj.GetVariable("Seq_UnitNum");
        var seqFVar = paramObj.GetVariable("SeqF_UnitNum");

        if (unitXVar == null || unitYVar == null || unitRVar == null)
        {
            Log.Info("❌ Missing UnitX, UnitY, or UnitR arrays in PalletVisualParams.");
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

        int[] seqArray = seqVar != null ? (int[])seqVar.Value : null;
        int[] seqFArray = seqFVar != null ? (int[])seqFVar.Value : null;

        // Arrays are 1-based (index 0 unused)
        numPerLayer = Math.Min(
            numPerLayer,
            Math.Min(unitXArray.Length, unitYArray.Length) - 1
        );

        if (numPerLayer <= 0)
        {
            Log.Info("⚠️ numPerLayer <= 0 after clamping. Nothing to draw.");
            return;
        }

        Log.Info($"📦 Config: numPerLayer={numPerLayer}, completedLayers={numLayersCompleted}");

        // ------------------------------------------------------------------
        // 🔹 Scene setup
        // ------------------------------------------------------------------
        float cubeLength = paramObj.GetVariable("ProductLength").Value;
        float cubeWidth = paramObj.GetVariable("ProductWidth").Value;
        float cubeHeight = paramObj.GetVariable("ProductHeight").Value;

        float PalletLength = paramObj.GetVariable("PalletLength").Value;
        float PalletWidth = paramObj.GetVariable("PalletWidth").Value;
        bool originMirror = paramObj.GetVariable("OriginMirror")?.Value ?? false;

        // ----- Materials -----
        var cubeMat = new MaterialBuilder()
            .WithMetallicRoughnessShader()
            .WithBaseColor(new Vector4(0.73f, 0.55f, 0.33f, 1f));  // cardboard

        var outlineMat = new MaterialBuilder()
            .WithUnlitShader()
            .WithAlpha(AlphaMode.BLEND)
            .WithBaseColor(new Vector4(0f, 0f, 0f, 0.25f));

        var xMat = new MaterialBuilder().WithUnlitShader().WithBaseColor(new Vector4(1f, 0f, 0f, 1f));
        var yMat = new MaterialBuilder().WithUnlitShader().WithBaseColor(new Vector4(0f, 1f, 0f, 1f));
        var zMat = new MaterialBuilder().WithUnlitShader().WithBaseColor(new Vector4(0f, 0.3f, 1f, 1f));
        var originMat = new MaterialBuilder().WithUnlitShader().WithBaseColor(new Vector4(1f, 0f, 0f, 1f));

        var palletMat = new MaterialBuilder()
            .WithMetallicRoughnessShader()
            .WithBaseColor(new Vector4(0.8f, 0.65f, 0.4f, 1f));

        var sheetMat = new MaterialBuilder()
            .WithMetallicRoughnessShader()
            .WithBaseColor(new Vector4(1f, 0.95f, 0.3f, 1f)); // slip sheet

        // ----- Mesh templates -----
        var cubeMesh = new MeshBuilder<VertexPositionNormal, VertexEmpty, VertexEmpty>("Cube");
        AddCubeCustom(cubeMesh, cubeMat, cubeLength, cubeHeight, cubeWidth);

        // Slightly enlarged outline mesh to avoid z-fighting
        float outlineScale = 1.01f;
        var outlineMesh = new MeshBuilder<VertexPositionNormal, VertexEmpty, VertexEmpty>("CubeOutline");
        AddCubeCustom(outlineMesh, outlineMat,
            cubeLength * outlineScale,
            cubeHeight * outlineScale,
            cubeWidth * outlineScale);

        var scene = new SceneBuilder();

        // ----- Pallet visual -----
        float palletWidthVis = 48f;   // X
        float palletDepthVis = 40f;   // Z
        float palletHeightGfx = 5f;

        float boardThickness = 0.5f;
        float boardWidth = 5.5f;
        float stringerHeight = 3.5f;
        float stringerWidth = 3.5f;

        float palletYOffset = -(palletHeightGfx / 2f) + 1f;

        var topBoardMesh = new MeshBuilder<VertexPositionNormal, VertexEmpty, VertexEmpty>("TopDeckBoard");
        AddCubeCustom(topBoardMesh, palletMat, boardWidth, boardThickness, palletDepthVis);

        var stringerMesh = new MeshBuilder<VertexPositionNormal, VertexEmpty, VertexEmpty>("Stringer");
        AddCubeCustom(stringerMesh, palletMat, palletWidthVis, stringerHeight, stringerWidth);

        var bottomBoardMesh = new MeshBuilder<VertexPositionNormal, VertexEmpty, VertexEmpty>("BottomDeckBoard");
        AddCubeCustom(bottomBoardMesh, palletMat, boardWidth, boardThickness, palletDepthVis);

        // Top deck boards
        int topBoardCount = 7;
        float boardSpacing = (palletWidthVis - (topBoardCount * boardWidth)) / (topBoardCount - 1);

        float topBoardY =
            -(palletHeightGfx / 2f) + stringerHeight + (boardThickness / 2f) + palletYOffset;

        float halfPalletWidthVis = palletWidthVis / 2f;

        for (int i = 0; i < topBoardCount; i++)
        {
            float xOffset =
                -halfPalletWidthVis + (i * (boardWidth + boardSpacing)) + (boardWidth / 2f);

            scene.AddRigidMesh(topBoardMesh,
                Matrix4x4.CreateTranslation(xOffset, topBoardY, 0));
        }

        // Stringers
        float halfPalletDepthVis = palletDepthVis / 2f;

        float[] stringerZ =
        {
            -halfPalletDepthVis + stringerWidth / 2f,
            0f,
            halfPalletDepthVis - stringerWidth / 2f
        };

        float stringerY =
            -(palletHeightGfx / 2f) + (stringerHeight / 2f) + palletYOffset;

        foreach (float zPos in stringerZ)
        {
            scene.AddRigidMesh(
                stringerMesh,
                Matrix4x4.CreateTranslation(0f, stringerY, zPos)
            );
        }

        // Bottom deck boards
        int bottomBoardCount = 3;
        float bottomSpacing = (palletWidthVis - (bottomBoardCount * boardWidth)) / (bottomBoardCount - 1);

        float bottomBoardY =
            -(palletHeightGfx / 2f) + (boardThickness / 2f) + palletYOffset;

        for (int i = 0; i < bottomBoardCount; i++)
        {
            float xOffset =
                -halfPalletWidthVis + (i * (boardWidth + bottomSpacing)) + (boardWidth / 2f);

            scene.AddRigidMesh(
                bottomBoardMesh,
                Matrix4x4.CreateTranslation(xOffset, bottomBoardY, 0f)
            );
        }

        // ----- Origin + axes at +X / -Z corner (or +X / +Z if OriginMirror) -----
        float cornerOffsetX = PalletLength / 2f;
        float cornerOffsetZ = originMirror ? PalletWidth / 2f : -PalletWidth / 2f;

        var markerMesh = new MeshBuilder<VertexPositionNormal, VertexEmpty, VertexEmpty>("OriginMarker");
        AddCubeCustom(markerMesh, originMat, 0.25f, 0.25f, 0.25f);
        scene.AddRigidMesh(markerMesh,
            Matrix4x4.CreateTranslation(cornerOffsetX, 0.25f, cornerOffsetZ));

        float arrowLength = 10f;
        float arrowThickness = 0.2f;

        var xArrowMesh = new MeshBuilder<VertexPositionNormal, VertexEmpty, VertexEmpty>("X_Axis");
        AddCubeCustom(xArrowMesh, xMat, arrowLength, arrowThickness, arrowThickness);
        scene.AddRigidMesh(xArrowMesh,
            Matrix4x4.CreateTranslation(cornerOffsetX - arrowLength / 2f, 0f, cornerOffsetZ));

        var yArrowMesh = new MeshBuilder<VertexPositionNormal, VertexEmpty, VertexEmpty>("Y_Axis");
        AddCubeCustom(yArrowMesh, yMat, arrowThickness, arrowLength, arrowThickness);
        scene.AddRigidMesh(yArrowMesh,
            Matrix4x4.CreateTranslation(cornerOffsetX, arrowLength / 2f, cornerOffsetZ));

        var zArrowMesh = new MeshBuilder<VertexPositionNormal, VertexEmpty, VertexEmpty>("Z_Axis");
        AddCubeCustom(zArrowMesh, zMat, arrowThickness, arrowThickness, arrowLength);
        // Z arrow points away from origin: positive Z when not mirrored, negative Z when mirrored
        float zArrowOffset = originMirror ? -arrowLength / 2f : arrowLength / 2f;
        scene.AddRigidMesh(zArrowMesh,
            Matrix4x4.CreateTranslation(cornerOffsetX, 0f, cornerOffsetZ + zArrowOffset));

        // ------------------------------------------------------------------
        // 🔹 UnitsDone / total layers (for build-in-progress)
        // ------------------------------------------------------------------
        int unitsDoneRaw = paramObj.GetVariable("UnitsDone")?.Value ?? 0;
        int unitsDone = Math.Max(0, Math.Min(unitsDoneRaw, numPerLayer));  // [0..numPerLayer]

        bool hasPartialLayer = (unitsDone > 0 && unitsDone < numPerLayer);
        int totalLayersToDraw = numLayersCompleted + (unitsDone > 0 ? 1 : 0);

        Log.Info($"📊 UnitsDone={unitsDone} → totalLayersToDraw={totalLayersToDraw}, hasPartialLayer={hasPartialLayer}");

        //--------------------------------------------------------------
        // 🔹 Slip-Sheets Between Layers (LayerSheet INT32 bitfield)
        //--------------------------------------------------------------
        int layerSheetMask = paramObj.GetVariable("LayerSheet")?.Value ?? 0;

        float sheetX = 48f;
        float sheetZ = 40f;
        float sheetY = 0.10f;

        var sheetMesh = new MeshBuilder<VertexPositionNormal, VertexEmpty, VertexEmpty>("SlipSheet");
        AddCubeCustom(sheetMesh, sheetMat, sheetX, sheetY, sheetZ);

        // Bit 0 → pallet top
        if ((layerSheetMask & 1) != 0)
        {
            float yPos = sheetY / 2f;
            scene.AddRigidMesh(sheetMesh, Matrix4x4.CreateTranslation(0, yPos, 0));
        }

        // Bits 1..NumLayersCompleted → above each completed layer
        // When unitsDone == 0, exclude the top sheet (bit = numLayersCompleted) from this loop
        // as it will be handled separately with TopSheetPlaced check
        int maxLayerForLoop = (unitsDone == 0) ? numLayersCompleted - 1 : numLayersCompleted;
        for (int layer = 0; layer < maxLayerForLoop; layer++)
        {
            int bit = layer + 1;
            if (((layerSheetMask >> bit) & 1) == 1)
            {
                float yPos = (layer + 1) * cubeHeight + (sheetY / 2f);
                scene.AddRigidMesh(sheetMesh, Matrix4x4.CreateTranslation(0, yPos, 0));
            }
        }

        // Top slipsheet - above the topmost layer (only when UnitsDone = 0, LayerSheet bit is set, and TopSheetPlaced = true)
        if (unitsDone == 0)
        {
            int topSheetPlacedRaw = paramObj.GetVariable("TopSheetPlaced")?.Value ?? 0;
            bool topSheetPlaced = (topSheetPlacedRaw != 0);
            int topSheetBit = totalLayersToDraw;
            bool topSheetBitSet = ((layerSheetMask >> topSheetBit) & 1) == 1;
            
            if (topSheetPlaced && topSheetBitSet)
            {
                float yPos = totalLayersToDraw * cubeHeight + (sheetY / 2f);
                scene.AddRigidMesh(sheetMesh, Matrix4x4.CreateTranslation(0, yPos, 0));
            }
        }

        // ------------------------------------------------------------------
        // 🔹 Build per-layer cube map (with sequencing, flips, and UnitsDone)
        // ------------------------------------------------------------------
        int layerFlip = paramObj.GetVariable("LayerFlip")?.Value ?? 0;

        // Build logical cube lists per layer, after sequencing & partial-top logic
        var layerCubeData = new List<CubeData>[totalLayersToDraw];

        for (int layer = 0; layer < totalLayersToDraw; layer++)
        {
            bool isExtraLayerAboveCompleted = (layer >= numLayersCompleted);
            bool isPartialTopLayer = (isExtraLayerAboveCompleted && hasPartialLayer && layer == totalLayersToDraw - 1);

            bool isFlipped = ((layerFlip >> (layer + 1)) & 1) == 1;

            // Choose which arrays/sequence to use
            bool useFlippedArrays = isFlipped && unitFXArray != null && unitFYArray != null && unitFRArray != null;

            int[] activeSeq = null;
            if (useFlippedArrays)
                activeSeq = seqFArray ?? seqArray;
            else
                activeSeq = seqArray ?? seqFArray;

            int cubesInLayer = numPerLayer;   // full pattern per layer

            var list = new List<CubeData>(cubesInLayer);

            for (int i = 0; i < cubesInLayer; i++)
            {
                // Partial top layer: only first UnitsDone placements
                if (isPartialTopLayer && unitsDone > 0 && i >= unitsDone)
                    continue;

                // Default case id is 1..numPerLayer in physical order
                int caseId = i + 1;

                // Apply sequencing if available
                if (activeSeq != null)
                {
                    // Sequence arrays are 1-based: index 0 is dummy (0),
                    // so we look at i+1 (1..numPerLayer)
                    int seqIndex = i + 1;

                    if (seqIndex >= 0 && seqIndex < activeSeq.Length)
                    {
                        int s = activeSeq[seqIndex];
                        if (s >= 1 && s <= numPerLayer)
                        {
                            caseId = s;   // only override if valid
                        }
                    }
                }

                // Safety guard
                if (caseId < 1 || caseId > numPerLayer)
                    continue;

                // Resolve coordinates/rotation for this caseId (1-based)
                float cx, cy; int cr;
                if (useFlippedArrays)
                {
                    cx = unitFXArray[caseId];
                    cy = unitFYArray[caseId];
                    cr = (int)unitFRArray[caseId];
                }
                else
                {
                    cx = unitXArray[caseId];
                    cy = unitYArray[caseId];
                    cr = (int)unitRArray[caseId];
                }

                list.Add(new CubeData { X = cx, Y = cy, R = cr });
            }

            layerCubeData[layer] = list;
        }

        // Convert cube data into instances with cached bounds for neighbor tests
        var layerCubes = new List<CubeInstance>[totalLayersToDraw];
        for (int layer = 0; layer < totalLayersToDraw; layer++)
        {
            var cubeDataList = layerCubeData[layer];
            if (cubeDataList == null || cubeDataList.Count == 0)
            {
                layerCubes[layer] = new List<CubeInstance>();
                continue;
            }

            var instances = new List<CubeInstance>(cubeDataList.Count);
            foreach (var cubeData in cubeDataList)
                instances.Add(CreateInstance(cubeData, cubeLength, cubeWidth));

            layerCubes[layer] = instances;
        }

        // ------------------------------------------------------------------
        // 🔹 Render cubes with interior-center removal
        //     • Interior cubes are removed in deeper layers
        //     • Interior cubes remain in any partial layer and the layer below it
        // ------------------------------------------------------------------
        int topLayerIndex = totalLayersToDraw - 1;

        for (int layer = 0; layer < totalLayersToDraw; layer++)
        {
            var cubes = layerCubes[layer];
            if (cubes == null || cubes.Count == 0) continue;

            bool isTopLayer = (layer == topLayerIndex);
            bool isPartialTopLayer = hasPartialLayer && isTopLayer;
            bool isLayerBelowPartial = hasPartialLayer && (layer == topLayerIndex - 1);

            bool protectThisLayer;
            if (hasPartialLayer)
            {
                // When there is a partial top layer:
                //  - keep interior cubes in the partial layer
                //  - keep interior cubes in the full layer directly below
                protectThisLayer = isPartialTopLayer || isLayerBelowPartial;
            }
            else
            {
                // When there is no partial layer yet:
                //  - keep interior cubes in the top full layer
                protectThisLayer = isTopLayer;
            }

            for (int i = 0; i < cubes.Count; i++)
            {
                var cube = cubes[i];
                bool culled = false;

                if (!protectThisLayer)
                {
                    bool hasTop = HasTopNeighbor(layerCubes, layer, cube);
                    bool hasLeft = HasSideNeighbor(cubes, i, cube, Side.Left);
                    bool hasRight = HasSideNeighbor(cubes, i, cube, Side.Right);
                    bool hasFront = HasSideNeighbor(cubes, i, cube, Side.Front);
                    bool hasBack = HasSideNeighbor(cubes, i, cube, Side.Back);

                    bool fullyHidden = hasTop && hasLeft && hasRight && hasFront && hasBack;
                    culled = fullyHidden;
                }

                if (culled) continue;

                var data = cube.Data;
                bool rot90 = cube.Rot90;
                float angle = rot90 ? MathF.PI / 2f : 0f;
                // Mirror rotation: negate angle when OriginMirror is true
                if (originMirror && rot90)
                {
                    angle = -MathF.PI / 2f;
                }
                var rotation = Matrix4x4.CreateRotationY(angle);

                float halfX = cube.SizeX / 2f;
                float halfZ = cube.SizeZ / 2f;

                // Mirror along width (Z axis) when OriginMirror is true
                float centerX = cornerOffsetX - data.X - halfX;
                float centerZ = originMirror
                    ? cornerOffsetZ - data.Y - halfZ
                    : cornerOffsetZ + data.Y + halfZ;

                float layerGap = 0f;
                float centerY = (cubeHeight / 2f) + (layer * (cubeHeight + layerGap));

                var translation = Matrix4x4.CreateTranslation(centerX, centerY, centerZ);
                var transform = rotation * translation;

                scene.AddRigidMesh(cubeMesh, transform);
                scene.AddRigidMesh(outlineMesh, transform);
            }
        }

        // ------------------------------------------------------------------
        // 🔹 Export GLB to Viewer path (A/B alternating file names)
        // ------------------------------------------------------------------
        var model = scene.ToGltf2();

        for (int i = 0; i < model.LogicalMaterials.Count; i++)
            model.LogicalMaterials[i].DoubleSided = true;

        string viewerDir = Path.Combine(Project.Current.ProjectDirectory, "Viewer");

        var currentNameVar = LogicObject.GetVariable("CurrentFileName");
        if (currentNameVar == null)
        {
            currentNameVar = InformationModel.MakeVariable("CurrentFileName", OpcUa.DataTypes.String);
            LogicObject.Add(currentNameVar);
            currentNameVar.Value = "pallet_layout_A";
        }

        string current = currentNameVar.Value;
        string next = current == "pallet_layout_A" ? "pallet_layout_B" : "pallet_layout_A";
        currentNameVar.Value = next;

        string outputPath = Path.Combine(viewerDir, $"{next}.glb");

        try
        {
            if (!Directory.Exists(viewerDir))
                Directory.CreateDirectory(viewerDir);

            Log.Info($"💾 Saving GLB to: {outputPath}");
            model.SaveGLB(outputPath);
            Log.Info("✅ GLB created successfully");
        }
        catch (Exception ex)
        {
            Log.Info($"❌ Failed to save GLB: {ex.Message}");
        }

        // ------------------------------------------------------------------
        // 🔹 Update HTML to proper GLB file (A or B)
        // ------------------------------------------------------------------
        var browser = Owner.Get<WebBrowser>("WebBrowser");
        browser.Visible = false;

        string templatePath = new ResourceUri(LogicObject.GetVariable("TempFile").Value).Uri;
        string filePath = new ResourceUri(LogicObject.GetVariable("DestFile").Value).Uri;

        string text = File.ReadAllText(templatePath);

        string currentNameHtml = LogicObject.GetVariable("CurrentFileName")?.Value ?? "pallet_layout_A";
        Log.Info($"🔄 Loading GLB: {currentNameHtml}.glb");

        text = text.Replace("$File", currentNameHtml);

        File.WriteAllText(filePath, text);
        Log.Info($"✅ index.html regenerated at: {filePath}");

        browser.Refresh();
        browser.Visible = true;
    }

    // ------------------------------------------------------------------
    // 🔹 Custom cube geometry generator
    // ------------------------------------------------------------------
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
            new Vector3(0, 0,  1), // front
            new Vector3(0, 0, -1), // back
            new Vector3(1, 0,  0), // right
            new Vector3(-1, 0,  0), // left
            new Vector3(0, 1,  0), // top
            new Vector3(0, -1, 0)  // bottom
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
            {  0,  1,  2,  3 }, // front
            {  4,  5,  6,  7 }, // back
            {  8,  9, 10, 11 }, // right
            { 12, 13, 14, 15 }, // left
            { 19, 18, 17, 16 }, // top
            { 20, 21, 22, 23 }  // bottom
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

    // ------------------------------------------------------------------
    // 🔹 Build cube list from Optix variables (for logging)
    // ------------------------------------------------------------------
    private List<CubeData> LoadCubesFromOptixVariables(
        float[] unitXArray, float[] unitYArray, float[] unitRArray, int numCubes)
    {
        var cubes = new List<CubeData>();

        int lastArrayIndex = Math.Min(unitXArray.Length,
                              Math.Min(unitYArray.Length, unitRArray.Length)) - 1;
        int maxIndex = Math.Min(numCubes, lastArrayIndex);

        if (maxIndex < 1)
        {
            Log.Info("⚠️ No cubes to add (NumPerLayer too small or arrays too short).");
            return cubes;
        }

        for (int i = 1; i <= maxIndex; i++)
        {
            cubes.Add(new CubeData
            {
                X = unitXArray[i],
                Y = unitYArray[i],
                R = (int)unitRArray[i]
            });
        }

        Log.Info($"✅ Added {cubes.Count} cubes (indices 1..{maxIndex})");
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

    private static bool HasTopNeighbor(List<CubeInstance>[] layers, int currentLayerIndex, CubeInstance cube)
    {
        int above = currentLayerIndex + 1;
        if (above >= layers.Length) return false;

        var candidates = layers[above];
        if (candidates == null || candidates.Count == 0) return false;

        foreach (var other in candidates)
        {
            if (RangesOverlap(cube.MinX, cube.MaxX, other.MinX, other.MaxX) &&
                RangesOverlap(cube.MinZ, cube.MaxZ, other.MinZ, other.MaxZ))
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
