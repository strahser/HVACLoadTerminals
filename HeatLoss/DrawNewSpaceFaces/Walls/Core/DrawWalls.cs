// Refactored DrawWalls.cs

using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.Core;

public class DrawWalls
{
    private readonly Document _hvacDocument;
    private readonly WallCreationService _wallService;
    private readonly SpaceRoomLinker _linker;
    private readonly List<Space> _spaces;

    public DrawWalls(Document hvacDoc, Document roomDoc)
    {
        _hvacDocument = hvacDoc;
        var rooms = CollectorQuery.GetAllRooms(roomDoc);
        _spaces = CollectorQuery.GetAllSpaces(hvacDoc).Cast<Space>().ToList();
            
        _linker = new SpaceRoomLinker(roomDoc, rooms, _spaces);
        _wallService = new WallCreationService(
            hvacDoc,
            _linker,
            new FailedFacesManager(),
            new LoggingService()
        );
    }

    public void CreateWallsForSpaces(WallCreationContext context)
    {
        context.HvacDocument = _hvacDocument;
        using var transactionGroup = new TransactionGroup(_hvacDocument, "Create Walls");
        transactionGroup.Start();

        foreach (var space in _spaces)
        {
            _wallService.CreateWalls(space, context);
        }

        transactionGroup.Assimilate();
        _wallService.FailedManager.LogFailedOperations();
    }

    public List<Wall> CreatedWalls => _wallService.CreatedWalls;
    public List<string> FailedFaceKeys => _wallService.FailedManager.FailedFaceKeys;
    public Dictionary<ElementId, (int Count, XYZ Point)> FailedSpaces => 
        _wallService.FailedManager.FailedSpaces;
}