// WallCreationService.cs

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.Calculators;
using HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.ParametersHandlersStrategy;
using HVACLoadTerminals.Utils;

namespace HVACLoadTerminals.HeatLoss.DrawNewSpaceFaces.Walls.Core;

public class WallCreationService(
    Document hvacDoc,
    SpaceRoomLinker linker,
    FailedFacesManager failedManager,
    ILogger logger)
{
    public FailedFacesManager FailedManager { get; } = failedManager;

    public List<Wall> CreatedWalls { get; } = [];

    public void CreateWalls(Space space, WallCreationContext context)
    {
        var room = linker.GetRoomBySpace(space);
        if (room == null) return;

        // Используем RoomDocument из линкера
        var faces = VerticalWallFacesCalculator.GetExternalFaces(
            linker.RoomDocument, 
            room, 
            context.Filter
        );

        foreach (var face in faces)
        {
            ProcessFace(space, face, context);
        }
    }

        private void ProcessFace(Space space, ConstructionSurfaceModel face, WallCreationContext context)
        {
            string faceKey = $"{space.Id}_{face.FaceId}";
            Curve curve = null;

            try
            {
                curve = FaceGeometryValidator.GetFaceCurve(face._Face);
                if (curve == null)
                {
                    FailedManager.RegisterFailure(
                        faceKey, 
                        space, 
                        face, 
                        null, 
                        "Invalid face geometry"
                    );
                    return;
                }

                using var transaction = new Transaction(hvacDoc, $"Create Wall {faceKey}");
                transaction.Start();

                var wall = Wall.Create(context.HvacDocument, curve, space.Level.Id, false);
                WallParametersConfigurator.Configure(wall, space, face, context);
                
                CreatedWalls.Add(wall);
                transaction.Commit();
                FailedManager.RemoveFace(faceKey);
            }
            catch (Exception ex)
            {
                FailedManager.RegisterFailure(faceKey, space, face, curve, ex.Message);
            }
        }
    }

    public static class FaceGeometryValidator
    {
        public static Curve GetFaceCurve(Face face)
        {
            try
            {
                return face?.GetEdgesAsCurveLoops()?.FirstOrDefault()?.FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }
    }

    public static class WallParametersConfigurator
    {
        public static void Configure(
            Wall wall, 
            Space space,
            ConstructionSurfaceModel face,
            WallCreationContext context)
        {
            var strategy = new WallParametersStrategyFactory(
                context.HvacDocument, // Используем документ из контекста
                context.NorthDirection
            ).CreateStrategy(space, context.GroundLevel);
        
            strategy.ApplyParameters(wall, space, face, null, context.GroundLevel);
        }
    }
