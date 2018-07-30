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
using Dargon.PlayOn.Foundation.Terrain.CompilationResults.Local;
using Dargon.PlayOn.Foundation.Terrain.CompilationResults.Overlay;
using Dargon.PlayOn.Foundation.Terrain.Declarations;
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
      private static readonly MaterialDescription SomewhatRough = new MaterialDescription { Properties = { Metallic = 0, Roughness = 0.04f } };

      private static SDXVector3 cameraTarget = new SDXVector3(0, 0, 0);
      private static SDXVector3 cameraOffset = new SDXVector3(0, -0.1f, 1) * 2000; //new Vector3(3, 2.5f, 5) - cameraTarget;
      private static SDXVector3 cameraUp = new SDXVector3(0, 1, 0);

      private static GraphicsLoop graphicsLoop;
      private static Game game;

      private static Canvas3DDebugMultiCanvasHost.Canvas3DDebugCanvas debugCanvas;
      private static Dictionary<Guid, IMesh<VertexPositionNormalColorTexture>> lgvMeshesByLgvGuid = new Dictionary<Guid, IMesh<VertexPositionNormalColorTexture>>();
      private static Entity player;
      private static Entity baddie;
      private static List<Entity> rocks = new List<Entity>();

      private static List<IntVector2> fred = new List<IntVector2>();

      public static void Main(string[] args) {
         graphicsLoop = GraphicsLoop.CreateWithNewWindow(1280, 720, InitFlags.DisableVerticalSync | InitFlags.EnableDebugStats);

         var gameFactory = new GameFactory();
         game = gameFactory.Create();

         var preset = SectorMetadataPresets.Test2D;
         var snd = game.TerrainFacade.CreateSectorNodeDescription(preset);
         snd.WorldTransform = Matrix4x4.Multiply(Matrix4x4.CreateScale(1000.0f / preset.LocalBoundary.Width), Matrix4x4.CreateTranslation(0, 0, 0));
         snd.WorldToLocalScalingFactor = (cDouble)preset.LocalBoundary.Width / (cDouble)1000;
         game.TerrainFacade.AddSectorNodeDescription(snd);


         player = game.EntityWorld.CreateEntity();
         game.EntityWorld.AddEntityComponent(player, new MovementComponent {
            WorldPosition = new DoubleVector3(-450, -450, 0),
            BaseRadius = 30,
            BaseSpeed = 100,
            IsPathfindingEnabled = true,
            PathingDestination = new DoubleVector3(-450, -450, 0)
         });

         baddie = game.EntityWorld.CreateEntity();
         game.EntityWorld.AddEntityComponent(baddie, new MovementComponent {
            WorldPosition = new DoubleVector3(0, 0, 0),
            BaseRadius = 30,
            BaseSpeed = 100,
            IsPathfindingEnabled = true,
            PathingDestination = new DoubleVector3(0, 0, 0)
         });

         var r = new Random(0);
         for (var i = 0; i < 10; i++) {
            var rock = game.EntityWorld.CreateEntity();
            game.EntityWorld.AddEntityComponent(rock, new MovementComponent {
               WorldPosition = new DoubleVector3(r.Next(-500, 500), r.Next(-500, 500), 0),
               BaseRadius = 10,
               BaseSpeed = 100,
               IsPathfindingEnabled = false
            });
            rocks.Add(rock);
         }

         var scene = new Scene();
         debugCanvas = new Canvas3DDebugMultiCanvasHost.Canvas3DDebugCanvas(graphicsLoop.GraphicsFacade, graphicsLoop.Presets, scene);

         for (var frame = 0; graphicsLoop.IsRunning(out var renderer, out var input); frame++) {
            scene.Clear();
            Step(graphicsLoop, game, input, scene);
            Render(scene, renderer);
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
               game.MovementSystem.Pathfind(player, intersectionWorld);
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
               if (!node.LandPolyNode.PointInLandPolygonNonrecursive(player.MovementComponent.LocalPositionIv2)) continue;

               // recompute intersectionWorld because floating point error in raycast logic
               var intersectionWorld = node.SectorNodeDescription.LocalToWorld(intersectionLocal.XY);

               var barriersBvh = node.LandPolyNode.FindContourAndChildHoleBarriersBvh();
               var q = new IntLineSegment2(player.MovementComponent.LocalPositionIv2, intersectionLocal.XY.LossyToIntVector2());
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
               debugCanvas.DrawLine(player.MovementComponent.WorldPosition, node.SectorNodeDescription.LocalToWorld(q.PointAt(tFar)), StrokeStyle.LimeThick5Solid);
            }
         }

         // i love rocks
         var rocksExisting = new List<Entity>();
         foreach (var rock in rocks) {
            var distance = rock.MovementComponent.WorldPosition.To(player.MovementComponent.WorldPosition).Norm2D();
            if (distance > player.MovementComponent.ComputedRadius) {
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

         var terrainOverlayNetwork = game.TerrainFacade.CompileSnapshot().OverlayNetworkManager.CompileTerrainOverlayNetwork(0);
         foreach (var node in terrainOverlayNetwork.TerrainNodes) {
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
         }

         if (terrainOverlayNetwork.TryFindTerrainOverlayNode(baddie.MovementComponent.WorldPosition, out var n)) {
            debugCanvas.DrawCrossSectorVisibilityPolygon(n, baddie.MovementComponent.LocalPositionIv2);
         } else {
            Console.WriteLine("Err, can't find pos?");
         }

         debugCanvas.Transform = Matrix4x4.Identity;
         debugCanvas.DrawLineStrip(fred, StrokeStyle.RedThick5Solid);

         scene.AddRenderable(
            graphicsLoop.Presets.UnitSphere,
            MatrixCM.Translation(player.MovementComponent.WorldPosition.ToSharpDX()) * MatrixCM.Scaling((float)player.MovementComponent.BaseRadius * 2),
            SomewhatRough,
            SDXColor.Lime);

         scene.AddRenderable(
            graphicsLoop.Presets.UnitSphere,
            MatrixCM.Translation(baddie.MovementComponent.WorldPosition.ToSharpDX()) * MatrixCM.Scaling((float)baddie.MovementComponent.BaseRadius * 2),
            SomewhatRough,
            SDXColor.Red);

         foreach (var rock in rocks) {
            scene.AddRenderable(
               graphicsLoop.Presets.UnitSphere,
               MatrixCM.Translation(rock.MovementComponent.WorldPosition.ToSharpDX()) * MatrixCM.Scaling((float)rock.MovementComponent.BaseRadius * 2),
               SomewhatRough,
               SDXColor.Brown);
         }

         debugCanvas.DrawEntityPaths(game.EntityWorld);

         var snapshot = scene.ExportSnapshot();
         renderer.RenderScene(snapshot);
         snapshot.ReleaseReference();
      }

      private static Matrix ComputeProjViewMatrix(Size clientSize) {
         var verticalFov = (float)Math.PI / 4;
         var aspect = clientSize.Width / (float)clientSize.Height;
         var proj = MatrixCM.PerspectiveFovRH(verticalFov, aspect, 500.0f, 3000.0f);
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