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
using ClipperLib;
using OpenMOBA;
using OpenMOBA.DevTool;
using OpenMOBA.DevTool.Debugging;
using OpenMOBA.Foundation;
using OpenMOBA.Foundation.Terrain;
using OpenMOBA.Foundation.Terrain.Declarations;
using OpenMOBA.Geometry;

namespace RoboticsMotionPlan {
   public partial class Program {
      //public const int MapWidth = 3200;
      //public const int MapHeight = 3200;
      public static int MapWidth = 801;
      public static int MapHeight = 798;

      public static void Main(string[] args) {
         Environment.CurrentDirectory = @"V:\my-repositories\miyu\derp\RoboticsMotionPlan\Assets";
         //MapPolygonizerForm.Run(@"C:\Users\Warty\occ.txt", "sieg_floor3.poly", "sieg_plan.plan");
         //MapPolygonizerForm.Run("gates.png", "gates.poly", "gates.plan");
         MapPolygonizerForm.Run("de_dust2.png", "de_dust2.poly", "de_dust2.plan");
         //         MapPolygonizerForm.Run("gates.png", "gates.poly");

         var (landPolys, holePolys) = FileLoader.LoadMap("gates.poly");
         var tsm = new TerrainStaticMetadata {
            LocalBoundary = new Rectangle(0, 0, MapWidth, MapHeight),
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
               canvas.DrawPolyNode(unerodedTerrainNode.LocalGeometryView.PunchedLand, StrokeStyle.BlackHairLineSolid, StrokeStyle.RedHairLineSolid);

               // draw eroded map
               canvas.DrawPolyNode(terrainNode.LocalGeometryView.PunchedLand, new StrokeStyle(Color.Gray), new StrokeStyle(Color.DarkRed));

               // draw waypoints
               canvas.DrawPoints(goodWaypoints, new StrokeStyle(Color.Blue, 25));
               canvas.DrawPoints(badWaypoints, new StrokeStyle(Color.Red, 25));

               void DrawThetaedPoint(DoubleVector2 p, double theta, bool highlight) {
                  canvas.DrawPoint(p, highlight ? StrokeStyle.OrangeThick35Solid : StrokeStyle.BlackThick25Solid);
                  canvas.DrawLine(
                     p,
                     p + DoubleVector2.FromRadiusAngle(50, theta),
                     new StrokeStyle(Color.Magenta, 3));
               }

               var emittedPoints = new List<(DoubleVector2, double, bool)>();

               // find big waypoints
               var bigWaypoints = new List<(DoubleVector2, bool)>();
               for (var i = 0; i < goodWaypoints.Count; i++) {
                  var from = i == 0 ? start : goodWaypoints[i - 1];
                  var to = goodWaypoints[i];

                  var ok = game.PathfinderCalculator.TryFindPath(terrainNode, from, terrainNode, to, out var roadmap);
                  Trace.Assert(ok);
                  var actions = roadmap.Plan.OfType<MotionRoadmapWalkAction>().ToArray();
                  var waypoints = new[] { actions[0].Source.ToDoubleVector2() }
                     .Concat(actions.Map(a => a.Destination.ToDoubleVector2())).ToArray();
                  bigWaypoints.AddRange(waypoints.Map((w, ind) => (w, ind == waypoints.Length - 1)));
               }

               var thetas = bigWaypoints.Zip(bigWaypoints.Skip(1), (a, b) => Math.Atan2(b.Item1.Y - a.Item1.Y, b.Item1.X - a.Item1.X))
                                          .ToArray();
               var chamferSpacing = 10;
               var chamferSpacingThreshold = 100;
               for (var i = 0; i < thetas.Length; i++) {
                  var (src, srcIsRoi) = bigWaypoints[i];
                  var (dst, dstIsRoi) = bigWaypoints[i + 1];
                  var srcToDstTheta = thetas[i];

                  // if close or last goal, don't chamfer
                  if (i + 1 == thetas.Length) {
                     emittedPoints.Add((dst, srcToDstTheta, dstIsRoi));
                     continue;
                  }

                  var dstToFollowingTheta = thetas[i + 1];
                  if (src.To(dst).Norm2D() < chamferSpacingThreshold) {
                     emittedPoints.Add((dst, dstToFollowingTheta, dstIsRoi));
                     continue;
                  }
                  var chamfer1 = dst - DoubleVector2.FromRadiusAngle(chamferSpacing, dstToFollowingTheta) - DoubleVector2.FromRadiusAngle(chamferSpacing, srcToDstTheta);
                  var chamfer2 = dst + DoubleVector2.FromRadiusAngle(chamferSpacing, dstToFollowingTheta) + 2 * DoubleVector2.FromRadiusAngle(chamferSpacing, srcToDstTheta);
                  emittedPoints.Add((chamfer1, srcToDstTheta, false));
                  if (dstIsRoi) {
                     emittedPoints.Add((dst, srcToDstTheta, true));
                  }
                  emittedPoints.Add((chamfer2, dstToFollowingTheta, false));
               }

               for (var i = 0; i < emittedPoints.Count - 1; i++) {
                  canvas.DrawLine(emittedPoints[i].Item1, emittedPoints[i + 1].Item1, StrokeStyle.CyanThick3Solid);
               }

               void PrintPoint(DoubleVector2 p) => Console.Write("(" + p.X.ToString("F3") + ", " + (MapHeight - p.Y).ToString("F3") + ")");

               Console.WriteLine("[");
               for (var i = 0; i < emittedPoints.Count; i++) {
                  var (p, theta, isRoi) = emittedPoints[i];
                  DrawThetaedPoint(p, theta, isRoi);
                  Console.Write("(");
                  PrintPoint(p);
                  Console.Write(", ");
                  Console.Write((-theta).ToString("F3"));
                  Console.Write(", ");
                  Console.Write(isRoi ? "True" : "False");
                  Console.Write(")");
                  if (i + 1 != emittedPoints.Count) Console.Write(", ");
                  Console.WriteLine();
               }
               Console.WriteLine("]");
            };
         };
         gf.Create().Run();
      }

      public static class FileLoader {
         public static (List<List<IntVector2>>, List<List<IntVector2>>) LoadMap(string path) {
            var lines = File.ReadAllLines(path).Map(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToArray();
            var landPolys = new List<List<IntVector2>>();
            var holePolys = new List<List<IntVector2>>();
            foreach (var line in lines) {
               var tokens = line.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);
               if (tokens[0] == "land") {
                  landPolys.Add(ParsePoly(tokens));
               } else if (tokens[0] == "hole") {
                  holePolys.Add(ParsePoly(tokens));
               } else if (tokens[0] == "hole_rev") {
                  holePolys.Add(ParsePoly(tokens).Select(x => x).Reverse().ToList());
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

         public static List<(DoubleVector2, double, bool)> LoadPlan(string path) {
            var text = File.ReadAllText(path);
            var lines = text.Trim().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return lines.Map(line => line.Split(',').Map(tok => tok.Trim('(', ' ', ')', ',')))
                        .Where(tokens => tokens[0] != "[" && tokens[0] != "]" && !tokens[0].StartsWith("//"))
                        .ToArray()
                        .Map(tokens => {
                           var p = new DoubleVector2(double.Parse(tokens[0]), MapHeight - double.Parse(tokens[1]) - 15);
                           var theta = -double.Parse(tokens[2]);
                           var isRoi = bool.Parse(tokens[3].ToLower());
                           return (p, theta, isRoi);
                        }).ToList();
         }
      }
   }
}
