// #define ENABLE_SOFTWARE_RENDER

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;
using Canvas3D;
using Canvas3D.LowLevel;
using Dargon.PlayOn;
using Dargon.PlayOn.DevTool;
using Dargon.PlayOn.DevTool.Debugging;
using Dargon.PlayOn.Foundation;
using Dargon.PlayOn.Foundation.ECS;
using Dargon.PlayOn.Foundation.Terrain.CompilationResults.Local;
using Dargon.PlayOn.Foundation.Terrain.CompilationResults.Overlay;
using Dargon.PlayOn.Foundation.Terrain.Declarations;
using Dargon.PlayOn.Foundation.Terrain.Motion;
using Dargon.PlayOn.Geometry;
using SharpDX;
using SDPoint = System.Drawing.Point;
using SDRectangle = System.Drawing.Rectangle;
using SDXColor = SharpDX.Color;
using SDXVector2 = SharpDX.Vector2;
using SDXVector3 = SharpDX.Vector3;
using SDXVector4 = SharpDX.Vector4;
using SDXPlane = SharpDX.Plane;
using SNVector2 = System.Numerics.Vector2;
using SNVector3 = System.Numerics.Vector3;
using SNVector4 = System.Numerics.Vector4;
using SNPlane = System.Numerics.Plane;

#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace TestGameTheGame {
   public static class Program {
      private const bool kTestMap = true;
      private const float kMapScale = kTestMap ? 1.0f : 10.0f;

      private static readonly MaterialDescription SomewhatRough = new MaterialDescription { Properties = { Metallic = 0, Roughness = 0.04f } };

      private static SDXVector3 cameraTarget = new SDXVector3(0, 0, 0);
      private static SDXVector3 cameraOffset = new SDXVector3(0, -0.1f, 1) * 1400 * kMapScale; //new Vector3(3, 2.5f, 5) - cameraTarget;
      private static SDXVector3 cameraUp = new SDXVector3(0, 1, 0);

      private static GraphicsLoop graphicsLoop;
      private static Game game;

      private static IDebugCanvas debugCanvas;
      private static Dictionary<Guid, IMesh<VertexPositionNormalColorTexture>> lgvMeshesByLgvGuid = new Dictionary<Guid, IMesh<VertexPositionNormalColorTexture>>();
      private static Entity player;
      private static Entity baddie;
      private static List<Entity> rocks = new List<Entity>();
      private static List<Entity> minions = new List<Entity>();

      private static List<IntVector2> fred = new List<IntVector2>();

      public static void Main(string[] args) {
         graphicsLoop = GraphicsLoop.CreateWithNewWindow(1280, 720, InitFlags.EnableDebugStats);

         var gameFactory = new GameFactory();
         game = gameFactory.Create();

         var preset = kTestMap ? SectorMetadataPresets.Test2D : SectorMetadataPresets.DotaStyleMoba;
         var snd = game.TerrainFacade.CreateSectorNodeDescription(preset);
         snd.WorldTransform = Matrix4x4.Multiply(Matrix4x4.CreateScale(kMapScale * 1000.0f / preset.LocalBoundary.Width), Matrix4x4.CreateTranslation(0, 0, 0));
         snd.WorldToLocalScalingFactor = ((cDouble)preset.LocalBoundary.Width / (cDouble)1000) / kMapScale;
         game.TerrainFacade.AddSectorNodeDescription(snd);

         player = game.EntityWorld.CreateEntity();
         game.EntityWorld.AddEntityComponent(
            player, 
            MotionComponent.Create(
               new DoubleVector3(-450, -450, 0),
               new MotionStatistics {
                  Radius = kTestMap ? 30 : 100,
                  Speed = kTestMap ? 100 : 355
               }));

         // baddie = game.EntityWorld.CreateEntity();
         // game.EntityWorld.AddEntityComponent(baddie, MotionComponent.Create(new DoubleVector3(0, 0, 0)));

         // var r = new Random(0);
         // for (var i = 0; i < 10; i++) {
         //    var rock = game.EntityWorld.CreateEntity();
         //    var worldPosition = new DoubleVector3(r.Next(-500, 500), r.Next(-500, 500), 0);
         //    game.EntityWorld.AddEntityComponent(rock, MotionComponent.Create(worldPosition));
         // }

         var swarm = new Swarm();
         swarm.SetDestination(new DoubleVector3(-50, -50, 0));
         for (var i = 0; i < 30; i++) {
            for (var j = 0; j < 30; j++) {
               var minion = game.EntityWorld.CreateEntity();
               game.EntityWorld.AddEntityComponent(
                  minion,
                  MotionComponent.Create(
                     new DoubleVector3(-425 + i * 10, -425 + j * 10, 0),
                     new MotionStatistics {
                        Radius = 5,
                        Speed = 100
                     },
                     swarm));
               minions.Add(minion);
            }
         }

         var scene = new Scene();

#if !ENABLE_SOFTWARE_RENDER
         debugCanvas = new Canvas3DDebugMultiCanvasHost.Canvas3DDebugCanvas(graphicsLoop.GraphicsFacade, graphicsLoop.Presets, scene);
#else
         var rotation = 95 * Math.PI / 180.0;
         var scale = 1.0f;
         var displaySize = new Size((int)(1400 * scale), (int)(700 * scale));
         var center = new SNVector3(0, 0, 0);
         var projector = new PerspectiveProjector(
            center + Vector3s.FromRadiusAngleAroundXAxis(500, (float)rotation),
            //				center + DoubleVector3.FromRadiusAngleAroundXAxis(200, rotation),
            center,
            Vector3s.FromRadiusAngleAroundXAxis(1, (float)(rotation - Math.PI / 2)),
            displaySize.Width,
            displaySize.Height);
         //         projector = null;
         //         var debugMultiCanvasHost = new MonoGameCanvasHost();
         var debugMultiCanvasHost = DebugMultiCanvasHost.CreateAndShowCanvas(
            displaySize,
            new SDPoint(100, 100),
            projector);
#endif
         for (var frame = 0; graphicsLoop.IsRunning(out var renderer, out var input); frame++) {
#if ENABLE_SOFTWARE_RENDER
            debugCanvas = debugMultiCanvasHost.CreateAndAddCanvas(frame);
#endif
            scene.Clear();
            Step(graphicsLoop, game, input, scene);
            debugCanvas.BatchDraw(() => Render(scene, renderer));
#if ENABLE_SOFTWARE_RENDER
            if (frame == 300) break;
#endif
         }
      }

      private static void Step(GraphicsLoop graphicsLoop, Game game, InputSomethingOSDJFH input, Scene scene) {
         var expectedTicks = (int)(graphicsLoop.Statistics.FrameTime.TotalSeconds * 60);
         while (game.GameTimeManager.Ticks < expectedTicks) {
            game.Tick();
         }

         var viewProj = ComputeProjViewMatrix(graphicsLoop.Form.ClientSize);
         viewProj.Transpose();
         var ray = Ray.GetPickRay(input.X, input.Y, new ViewportF(0, 0, 1280, 720, 1.0f, 100.0f), viewProj);

         var terrainOverlayNetwork = game.TerrainFacade.CompileSnapshot().OverlayNetworkManager.CompileTerrainOverlayNetwork(0);

         // rmb moves
         if (input.IsMouseDown(MouseButtons.Right)) {
            foreach (var node in terrainOverlayNetwork.TerrainNodes) {
               var origin = node.SectorNodeDescription.LocalToWorld(DoubleVector2.Zero);
               var normal = node.SectorNodeDescription.LocalToWorldNormal(DoubleVector3.UnitZ);
               var plane = new SDXPlane(ToSharpDX(origin), ToSharpDX(normal));
               if (!plane.Intersects(ref ray, out SDXVector3 intersection)) continue;

               var intersectionLocal = node.SectorNodeDescription.WorldToLocal(intersection.ToOpenMoba());
               if (!node.LandPolyNode.PointInLandPolygonNonrecursive(intersectionLocal.XY.LossyToIntVector2())) continue;

               // recompute intersectionWorld because floating point error in raycast logic
               var intersectionWorld = node.SectorNodeDescription.LocalToWorld(intersectionLocal.XY);
               game.MotionFacade.SetPathfindingDestination(player, intersectionWorld);
            }
         }

         // lazaars
         if (input.IsKeyDown(Keys.Q)) {
            foreach (var node in terrainOverlayNetwork.TerrainNodes) {
               var origin = node.SectorNodeDescription.LocalToWorld(DoubleVector2.Zero);
               var normal = node.SectorNodeDescription.LocalToWorldNormal(DoubleVector3.UnitZ);
               var plane = new SDXPlane(ToSharpDX(origin), ToSharpDX(normal));
               if (!plane.Intersects(ref ray, out SDXVector3 intersection)) continue;

               var intersectionLocal = node.SectorNodeDescription.WorldToLocal(intersection.ToOpenMoba());
               if (!node.SectorNodeDescription.StaticMetadata.LocalBoundary.Contains(new SDPoint((int)intersectionLocal.X, (int)intersectionLocal.Y))) continue;
               if (!node.LandPolyNode.PointInLandPolygonNonrecursive(player.MotionComponent.Internals.Localization.LocalPositionIv2)) continue;

               // recompute intersectionWorld because floating point error in raycast logic
               var intersectionWorld = node.SectorNodeDescription.LocalToWorld(intersectionLocal.XY);

               var barriersBvh = node.LandPolyNode.FindContourAndChildHoleBarriersBvh();
               var q = new IntLineSegment2(player.MotionComponent.Internals.Localization.LocalPositionIv2, intersectionLocal.XY.LossyToIntVector2());
               debugCanvas.Transform = node.SectorNodeDescription.WorldTransform;
               debugCanvas.DrawLine(q, StrokeStyle.RedHairLineSolid);
               foreach (var seg in barriersBvh.Segments) {
                  debugCanvas.DrawLine(seg, StrokeStyle.RedThick5Solid);
               }

               var intersectingLeaves = barriersBvh.FindPotentiallyIntersectingLeaves(q);
               var tFar = 1.0;
               foreach (var bvhNode in intersectingLeaves) {
                  for (var i = bvhNode.SegmentsStartIndexInclusive; i < bvhNode.SegmentsEndIndexExclusive; i++) {
                     if (GeometryOperations.TryFindNonoverlappingSegmentSegmentIntersectionT(ref q, ref bvhNode.Segments[i], out var t)) {
                        Console.WriteLine(t);
                        tFar = Math.Min(tFar, t);
                     }
                  }
               }

               debugCanvas.Transform = Matrix4x4.Identity;
               debugCanvas.DrawLine(player.MotionComponent.Internals.Pose.WorldPosition, node.SectorNodeDescription.LocalToWorld(q.PointAt(tFar)), StrokeStyle.LimeThick5Solid);
            }
         }

         // i love rocks
         var rocksExisting = new List<Entity>();
         foreach (var rock in rocks) {
            var distance = rock.MotionComponent.Internals.Pose.WorldPosition.To(player.MotionComponent.Internals.Pose.WorldPosition).Norm2D();
            if (distance > player.MotionComponent.Internals.ComputedStatistics.Radius) {
               rocksExisting.Add(rock);
               continue;
            }

            game.EntityWorld.RemoveEntity(rock);
         }

         rocks = rocksExisting;

         // W draws walls
         if (input.IsKeyJustDown(Keys.W)) {
            foreach (var node in terrainOverlayNetwork.TerrainNodes) {
               var origin = node.SectorNodeDescription.LocalToWorld(DoubleVector2.Zero);
               var normal = node.SectorNodeDescription.LocalToWorldNormal(DoubleVector3.UnitZ);
               var plane = new SDXPlane(ToSharpDX(origin), ToSharpDX(normal));
               if (!plane.Intersects(ref ray, out SDXVector3 intersection)) continue;

               var intersectionLocal = node.SectorNodeDescription.WorldToLocal(intersection.ToOpenMoba());
               if (!node.SectorNodeDescription.StaticMetadata.LocalBoundary.Contains(new SDPoint((int)intersectionLocal.X, (int)intersectionLocal.Y))) continue;

               // recompute intersectionWorld because floating point error in raycast logic
               var intersectionWorld = node.SectorNodeDescription.LocalToWorld(intersectionLocal.XY);
               fred.Add(intersectionWorld.XY.LossyToIntVector2());

               // todo: we really need to iterate over LGVs rather than tnodes
               break;
            }
         }

         if (input.IsKeyJustDown(Keys.E)) {
            if (fred.Count > 0) {
               fred.RemoveAt(fred.Count - 1);
            }
         }

         if (input.IsKeyJustDown(Keys.R) && fred.Count >= 2) {
            var polyTree = PolylineOperations.ExtrudePolygon(fred, 10);
            var boundsLower = fred.Aggregate(new IntVector2(int.MaxValue, int.MaxValue), (a, b) => new IntVector2(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y)));
            var boundsUpper = fred.Aggregate(new IntVector2(int.MinValue, int.MinValue), (a, b) => new IntVector2(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y)));
            var bounds = new SDRectangle(boundsLower.X, boundsLower.Y, boundsUpper.X - boundsLower.X, boundsUpper.Y - boundsLower.Y);

            var holeStaticMetadata = new PrismHoleStaticMetadata(
               bounds,
               new[] { new Polygon2(polyTree.Childs[0].Contour) },
               polyTree.Childs[0].Childs.Map(c => new Polygon2(((IEnumerable<IntVector2>)c.Contour).Reverse().ToList())));

            var terrainHole = game.TerrainFacade.CreateHoleDescription(holeStaticMetadata);
            game.TerrainFacade.AddTemporaryHoleDescription(terrainHole);

            if (!input.IsKeyDown(Keys.ShiftKey)) {
               var removeEvent = game.CreateRemoveTemporaryHoleEvent(new GameTime(game.GameTimeManager.Now.Ticks + 90), terrainHole);
               game.GameEventQueueManager.AddGameEvent(removeEvent);
            }

            fred.Clear();
         }
      }

      private static void Render(Scene scene, IRenderContext renderer) {
         scene.SetCamera(cameraTarget + cameraOffset, ComputeProjViewMatrix(graphicsLoop.Form.ClientSize));

         scene.AddRenderable(
            graphicsLoop.Presets.UnitCube,
            Matrix.Identity,
            SomewhatRough,
            SDXColor.White);

         var terrainOverlayNetwork = game.TerrainFacade.CompileSnapshot().OverlayNetworkManager.CompileTerrainOverlayNetwork(5);
         foreach (var (nodeIndex, node) in terrainOverlayNetwork.TerrainNodes.Enumerate()) {
            if (!lgvMeshesByLgvGuid.TryGetValue(node.LocalGeometryView.Guid, out var mesh)) {
               VertexPositionNormalColorTexture[] F(Triangle3 triangle) {
                  return triangle.Points.Map(
                     p => new VertexPositionNormalColorTexture(
                        new SDXVector3((float)p.X, (float)p.Y, 0),
                        -SDXVector3.UnitZ,
                        SDXColor.White,
                        new SDXVector2(0, 0)));
               }

               var triangleList = node.LocalGeometryView.Triangulation.Islands
                                      .SelectMany(i => i.Triangles)
                                      .SelectMany(triangle => F(triangle).Concat(F(triangle).Reverse()))
                                      .ToArray();

               mesh = graphicsLoop.GraphicsFacade.CreateMesh(triangleList);
               lgvMeshesByLgvGuid.Add(node.LocalGeometryView.Guid, mesh);
            }

            scene.AddRenderable(
               mesh,
               ConvertSystemNumericsToSharpDX(Matrix4x4.Transpose(node.SectorNodeDescription.WorldTransform)),
               SomewhatRough,
               SDXColor.White);

#if ENABLE_SOFTWARE_RENDER
            debugCanvas.Transform = node.SectorNodeDescription.WorldTransform;
            debugCanvas.DrawPolyNode(node.LandPolyNode);
            debugCanvas.DrawTriangulation(node.LocalGeometryView.Triangulation, StrokeStyle.BlackThick3Solid);
#endif
            // if (nodeIndex == 0) {
            // } else {
            //    debugCanvas.DrawPolyNode(node.LandPolyNode, StrokeStyle.LimeHairLineSolid, StrokeStyle.CyanHairLineSolid);
            //    debugCanvas.DrawTriangulation(node.LocalGeometryView.Triangulation, StrokeStyle.MagentaHairLineSolid);
            // }

            debugCanvas.Transform = Matrix4x4.Identity;
            // debugCanvas.DrawPoint(new DoubleVector2(50, 50), StrokeStyle.RedThick25Solid);
            // debugCanvas.DrawPoint(new DoubleVector2(-127.331153869629, -151.531051635742), StrokeStyle.RedThick25Solid);
            // debugCanvas.DrawPoint(new DoubleVector2(-390.349945068359, -359.916748046875), StrokeStyle.LimeThick25Solid);
         }

         // if (terrainOverlayNetwork.TryFindTerrainOverlayNode(baddie.MotionComponent.Internals.Pose.WorldPosition, out var n)) {
         //    debugCanvas.DrawCrossSectorVisibilityPolygon(n, baddie.MotionComponent.Internals.Localization.LocalPositionIv2);
         // } else {
         //    Console.WriteLine("Err, can't find pos?");
         // }

         debugCanvas.Transform = Matrix4x4.Identity;
         debugCanvas.DrawLineStrip(fred, StrokeStyle.RedThick5Solid);

         scene.AddRenderable(
            graphicsLoop.Presets.UnitSphere,
            MatrixCM.Translation(player.MotionComponent.Internals.Pose.WorldPosition.ToSharpDX()) * MatrixCM.Scaling((float)player.MotionComponent.BaseStatistics.Radius * 2),
            SomewhatRough,
            SDXColor.Lime);

         // scene.AddRenderable(
         //    graphicsLoop.Presets.UnitSphere,
         //    MatrixCM.Translation(baddie.MotionComponent.Internals.Pose.WorldPosition.ToSharpDX()) * MatrixCM.Scaling((float)baddie.MotionComponent.BaseStatistics.Radius * 2),
         //    SomewhatRough,
         //    SDXColor.Red);

         foreach (var rock in rocks) {
            scene.AddRenderable(
               graphicsLoop.Presets.UnitSphere,
               MatrixCM.Translation(rock.MotionComponent.Internals.Pose.WorldPosition.ToSharpDX()) * MatrixCM.Scaling((float)rock.MotionComponent.BaseStatistics.Radius * 2),
               SomewhatRough,
               SDXColor.Brown);
         }

         foreach (var minion in minions) {
            scene.AddRenderable(
               graphicsLoop.Presets.UnitSphere,
               MatrixCM.Translation(minion.MotionComponent.Internals.Pose.WorldPosition.ToSharpDX()) * MatrixCM.Scaling((float)minion.MotionComponent.BaseStatistics.Radius * 2),
               SomewhatRough,
               SDXColor.Magenta);

#if ENABLE_SOFTWARE_RENDER
            debugCanvas.Transform = Matrix4x4.Identity;
            debugCanvas.DrawPoint(minion.MotionComponent.Internals.Pose.WorldPosition, new StrokeStyle(System.Drawing.Color.Black, 2 * (float)minion.MotionComponent.BaseStatistics.Radius));
            debugCanvas.DrawPoint(minion.MotionComponent.Internals.Pose.WorldPosition, new StrokeStyle(System.Drawing.Color.White, 2 * (float)minion.MotionComponent.BaseStatistics.Radius - 2));
#endif
         }

         debugCanvas.DrawEntityPaths(game.EntityWorld);

         var snapshot = scene.ExportSnapshot();
         renderer.RenderScene(snapshot);
         snapshot.ReleaseReference();
      }

      private static Matrix ComputeProjViewMatrix(Size clientSize) {
         var verticalFov = (float)Math.PI / 4;
         var aspect = clientSize.Width / (float)clientSize.Height;
         var proj = MatrixCM.PerspectiveFovRH(verticalFov, aspect, 500.0f, 100000.0f);
         var view = MatrixCM.ViewLookAtRH(cameraTarget + cameraOffset, cameraTarget, cameraUp);
         return proj * view;
      }

      private static Matrix ConvertSystemNumericsToSharpDX(Matrix4x4 value) {
         return new Matrix(
            value.M11, value.M12, value.M13, value.M14,
            value.M21, value.M22, value.M23, value.M24,
            value.M31, value.M32, value.M33, value.M34,
            value.M41, value.M42, value.M43, value.M44);
      }

      public static SDXVector3 ToSharpDX(this DoubleVector3 v) {
         return new SDXVector3((float)v.X, (float)v.Y, (float)v.Z);
      }

      public static DoubleVector3 ToOpenMoba(this SDXVector3 v) {
         return new DoubleVector3(v.X, v.Y, v.Z);
      }
   }
}