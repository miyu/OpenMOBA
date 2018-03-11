using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using OpenMOBA;
using OpenMOBA.DevTool;
using OpenMOBA.DevTool.Debugging;
using OpenMOBA.Foundation;
using OpenMOBA.Foundation.Terrain;
using OpenMOBA.Foundation.Terrain.Visibility;
using OpenMOBA.Geometry;

namespace RoboticsMotionPlan {
   public partial class Program {
      public static void Main(string[] args) {
         Environment.CurrentDirectory = @"C:\my-repositories\miyu\derp\RoboticsMotionPlan\Assets";
         //MapPolygonizerForm.Run("sieg_floor3_marked.png", "sieg_floor3.poly");

         var (landPolys, holePolys) = FileLoader.Load("sieg_floor3.poly");
         var tsm = new TerrainStaticMetadata {
            LocalBoundary = new Rectangle(0, 0, 32000, 32000),
            LocalIncludedContours = landPolys.Map(p => new Polygon2(p, true)),
            LocalExcludedContours = holePolys.Map(p => new Polygon2(p.Select(x => x).Reverse().ToList(), true)),
         };

         var start = FileLoader.LoadPoints("start.csv").First();
         var goodWaypoints = FileLoader.LoadPoints("good_waypoints.csv");
         var badWaypoints = FileLoader.LoadPoints("bad_waypoints.csv");
         var holeMetadata = new SphereHoleStaticMetadata {
            Radius = 13
         };

         var gf = new GameFactory();
         gf.GameCreated += (s, game) => {
            var debugger = GameDebugger.AttachToWithSoftwareRendering(game);
            game.TerrainService.Clear();
            var snd = game.TerrainService.CreateSectorNodeDescription(tsm);
            game.TerrainService.AddSectorNodeDescription(snd);

            foreach (var hsm in badWaypoints) {
               var hd = game.TerrainService.CreateHoleDescription(holeMetadata);
               hd.WorldTransform = Matrix4x4.CreateTranslation(hsm.X, hsm.Y, 0);
               game.TerrainService.AddTemporaryHoleDescription(hd);
            }

            debugger.RenderHook += (_, canvas) => {
               var snapshot = game.TerrainService.SnapshotCompiler.CompileSnapshot();

               // uneroded network
               var unerodedOverlayNetwork = snapshot.OverlayNetworkManager.CompileTerrainOverlayNetwork(0);
               Trace.Assert(unerodedOverlayNetwork.TerrainNodes.Count == 1);
               var unerodedTerrainNode = unerodedOverlayNetwork.TerrainNodes.First();

               // eroded network
               var overlayNetwork = snapshot.OverlayNetworkManager.CompileTerrainOverlayNetwork(15); //1 unit = 0.02m
               Trace.Assert(overlayNetwork.TerrainNodes.Count == 1);
               var terrainNode = overlayNetwork.TerrainNodes.First();
               
               // draw uneroded map
               canvas.DrawPolyTree(unerodedTerrainNode.LocalGeometryView.PunchedLand, StrokeStyle.BlackHairLineSolid, StrokeStyle.RedHairLineSolid);

               // draw eroded map
               canvas.DrawPolyTree(terrainNode.LocalGeometryView.PunchedLand, new StrokeStyle(Color.Gray), new StrokeStyle(Color.DarkRed));

               //var from = new IntVector2(620, 2500);
               var from = new IntVector2(612, 2504);
               var to = new IntVector2(2488, 996);
               canvas.DrawPoint(from, StrokeStyle.LimeThick25Solid);
               canvas.DrawPoint(to, StrokeStyle.RedThick25Solid);
               canvas.DrawPoints(badWaypoints, new StrokeStyle(Color.Red, 25));

               var ok = game.PathfinderCalculator.TryFindPath(terrainNode, from, terrainNode, to, out var roadmap);
               Trace.Assert(ok);

               var actions = roadmap.Plan.OfType<MotionRoadmapWalkAction>().ToArray();
               for (var i = 0; i < actions.Length; i++) {
                  canvas.DrawLine(actions[i].Source, actions[i].Destination, StrokeStyle.CyanThick3Solid);
                  canvas.DrawPoint(actions[i].Source, StrokeStyle.BlackThick5Solid);
                  canvas.DrawPoint(actions[i].Destination, StrokeStyle.BlackThick5Solid);
               }

               // print plan:
               Console.WriteLine("[");
               var n = 0;
               for (var i = 0; i < actions.Length; i++) {
                  var seg = new IntLineSegment2(actions[i].Source, actions[i].Destination);
                  var v = seg.First.To(seg.Second);
                  var len = (double)v.Norm2F();
                  var parts = Math.Max((int)Math.Floor(len * 0.02 / 0.7), 2);

                  void PrintPoint(DoubleVector2 p) => Console.Write("(" + p.X.ToString("F3") + ", " + (3200 - p.Y).ToString("F3") + ")");

                  var curTheta = Math.Atan2(-v.Y, v.X);
                  var nextTheta = curTheta;
                  if (i + 1 != actions.Length) {
                     var ns = new IntLineSegment2(actions[i + 1].Source, actions[i + 1].Destination);
                     var nv = ns.First.To(ns.Second);
                     nextTheta = Math.Atan2(-nv.Y, nv.X);
                  }

                  for (var j = 0; j < parts; j++) {
                     continue;
                     Console.Write("(");
                     PrintPoint(seg.PointAt(j / (double)parts));
                     Console.Write(", ");
                     PrintPoint(seg.PointAt((j + 1) / (double)parts));
                     Console.Write(", ");
                     var theta = j + 1 == parts ? ((curTheta + nextTheta) / 2) : curTheta;
                     Console.Write(theta.ToString("F3"));
                     Console.Write(")");
                     if (i + 1 != actions.Length || j + 1 != parts) Console.Write(", ");

                     if (n >= 50) {
                        var p = seg.PointAt((j + 1) / (double)parts);
                        canvas.DrawPoint(p, StrokeStyle.BlackThick25Solid);
                        canvas.DrawLine(p, p + DoubleVector2.FromRadiusAngle(25, -theta), new StrokeStyle(Color.Magenta, 3));
                     }
                     n++;
                  }
                  if (i + 1 != actions.Length) Console.WriteLine();
               }
               Console.WriteLine("]");
            };
         };
         gf.Create().Run();
      }

      public static class FileLoader {
         public static (List<List<IntVector2>>, List<List<IntVector2>>) Load(string path) {
            var lines = File.ReadAllLines(path).Map(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToArray();
            var landPolys = new List<List<IntVector2>>();
            var holePolys = new List<List<IntVector2>>();
            foreach (var line in lines) {
               var tokens = line.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);
               if (tokens[0] == "land") {
                  landPolys.Add(ParsePoly(tokens));
               } else if (tokens[0] == "hole") {
                  holePolys.Add(ParsePoly(tokens));
               }
            }
            return (landPolys, holePolys);
         }

         private static List<IntVector2> ParsePoly(string[] tokens) {
            var poly = new List<IntVector2>();
            for (var i = 1; i < tokens.Length; i += 2) {
               poly.Add(ParseIv2(tokens.Skip(i).Take(2).Select(t => t.Trim(';', ',')).ToArray()));
            }
            if (poly[0] != poly.Last()) {
               poly.Add(poly[0]);
            }
            return poly;
         }

         private static IntVector2 ParseIv2(string[] tokens) {
            var x = int.Parse(tokens[0]);
            var y = int.Parse(tokens[1]);
            return new IntVector2(x, y);
         }

         public static List<IntVector2> LoadPoints(string path) {
            return File.ReadAllLines(path).Skip(1).Select(line => ParseIv2(line.Split(',').ToArray())).ToList();
         }
      }
   }
}
