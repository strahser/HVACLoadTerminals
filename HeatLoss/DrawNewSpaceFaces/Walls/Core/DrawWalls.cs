using System;
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

    public List<Wall> CreatedWalls => _wallService.CreatedWalls;
    public List<string> FailedFaceKeys => _wallService.FailedManager.FailedFaceKeys;
    public Dictionary<ElementId, (int Count, XYZ Point)> FailedSpaces => _wallService.FailedManager.FailedSpaces;
    public List<Space> CachedSpaces => _spaces;

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
            new LoggingService("DrawWalls.log")
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

    public void RetryFailedWalls(WallCreationContext context)
    {
        context.HvacDocument = _hvacDocument;
            
        using var transactionGroup = new TransactionGroup(_hvacDocument, "Retry Failed Walls");
        transactionGroup.Start();
            
        _wallService.FailedManager.RetryFailedFaces(data =>
        {
            try
            {
                using var transaction = new Transaction(_hvacDocument, $"Retry Wall {data.FaceKey}");
                transaction.Start();

                var wall = Wall.Create(
                    context.HvacDocument,
                    data.Curve,
                    data.Space.Level.Id,
                    false
                );
                    
                WallParametersConfigurator.Configure(wall, data.Space, data.Face, context);
                    
                if (wall != null)
                {
                    _wallService.CreatedWalls.Add(wall);
                    transaction.Commit();
                }
                else
                {
                    transaction.RollBack();
                }
            }
            catch (Exception ex)
            {
                _wallService.FailedManager.UpdateError(data.FaceKey, $"Retry failed: {ex.Message}");
            }
        });
            
        transactionGroup.Assimilate();
    }

    public bool IsReady => 
        _hvacDocument?.IsValidObject == true && 
        _linker?.RoomDocument?.IsValidObject == true;
}