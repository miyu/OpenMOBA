using ClipperLib;
using OpenMOBA.Foundation.Terrain;
using OpenMOBA.Geometry;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;

namespace OpenMOBA.Foundation {
   public static class MapLoader {
      public static void LoadMeshAsMap(this TerrainService terrainService, string objPath, DoubleVector3 meshOffset, DoubleVector3 worldOffset, int scaling = 50000) {
         Environment.CurrentDirectory = @"V:\my-repositories\miyu\derp\OpenMOBA.DevTool\bin\Debug\net461";

         var lines = File.ReadLines(objPath);
         var verts = new List<DoubleVector3>();
         var previousEdges = new Dictionary<(int, int), (SectorNodeDescription, IntLineSegment2)>();

         void Herp(SectorNodeDescription node, int a, int b, IntLineSegment2 seg) {
            if (a > b) {
               (a, b) = (b, a); // a < b
               seg = new IntLineSegment2(seg.Second, seg.First);
            }

            if (previousEdges.TryGetValue((a, b), out var prev)) {
               var (prevNode, prevSeg) = prev;
               terrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(node, prevNode, seg, prevSeg));
               terrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(prevNode, node, prevSeg, seg));
            } else {
               previousEdges.Add((a, b), (node, seg));
            }
         }

         foreach (var (i, line) in lines.Select(l => l.Trim()).Enumerate()) {
            if (line.Length == 0) continue;
            if (line.StartsWith("#")) continue;
            var tokens = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            switch (tokens[0]) {
               case "v":
                  var v = meshOffset + new DoubleVector3(double.Parse(tokens[1]), double.Parse(tokens[2]), double.Parse(tokens[3]));
                  v = new DoubleVector3(v.X, -v.Z, v.Y);
                  v = v * scaling + worldOffset;
                  // todo: flags for dragon / bunny to switch handiness + rotate
                  verts.Add(v);
                  //                  verts.Add(new DoubleVector3(v.X, v.Y, v.Z));
                  break;
               case "f":
                  //                  Console.WriteLine($"Loading face of line {i}");
                  var i1 = int.Parse(tokens[1]) - 1;
                  var i2 = int.Parse(tokens[2]) - 1;
                  var i3 = int.Parse(tokens[3]) - 1;

                  var v1 = verts[i1]; // origin
                  var v2 = verts[i2]; // a, x dim
                  var v3 = verts[i3]; // b, y dim

                  /***
                   *            ___      
                   *    /'.      |     ^
                   * b /.t '.    | h   | vert
                   *  /__'___'. _|_    |
                   *     a
                   *  |-------| w
                   *  |---| m
                   *  
                   *          ___      
                   *  \.       |     ^
                   * b \'.     | h   | vert
                   *    \_'.  _|_    |
                   *    |--| w
                   *  |-| m
                   */
                  var a = v2 - v1;
                  var b = v3 - v1;
                  var theta = Math.Acos(a.Dot(b) / (a.Norm2D() * b.Norm2D())); // a.b =|a||b|cos(theta)

                  var w = a.Norm2D();
                  var h = b.Norm2D() * Math.Sin(theta);
                  var m = b.Norm2D() * Math.Cos(theta);

                  var scaleBound = 1000; //ClipperBase.loRange
                  var localUpscale = scaleBound * 0.9f / (float)Math.Max(Math.Abs(m), Math.Max(Math.Abs(h), w));
                  var globalDownscale = 1.0f / localUpscale;
                  // Console.WriteLine(localUpscale + " " + (int)(m * localUpscale) + " " + (int)(h * localUpscale) + " " + (int)(w * localUpscale));

                  var po = new IntVector2(0, 0);
                  var pa = new IntVector2((int)(w * localUpscale), 0);
                  var pb = new IntVector2((int)(m * localUpscale), (int)(h * localUpscale));
                  var metadata = new TerrainStaticMetadata {
                     LocalBoundary = m < 0 ? new Rectangle((int)(m * localUpscale), 0, (int)((w - m) * localUpscale), (int)(h * localUpscale)) : new Rectangle(0, 0, (int)(w * localUpscale), (int)(h * localUpscale)),
                     LocalIncludedContours = new List<Polygon2> {
                        new Polygon2(new List<IntVector2> { po, pb, pa, po }, false)
                     },
                     LocalExcludedContours = new List<Polygon2>()
                  };

                  foreach (var zzz in metadata.LocalIncludedContours) {
                     foreach (var p in zzz.Points) {
                        if (Math.Abs(p.X) >= ClipperBase.loRange || Math.Abs(p.Y) >= ClipperBase.loRange) {
                           throw new Exception("!!!!");
                        }
                     }
                  }

                  var snd = terrainService.CreateSectorNodeDescription(metadata);
                  var triangleToWorld = Matrix4x4.Identity;

                  var alen = (float)a.Norm2D();
                  triangleToWorld.M11 = globalDownscale * (float)a.X / alen;
                  triangleToWorld.M12 = globalDownscale * (float)a.Y / alen;
                  triangleToWorld.M13 = globalDownscale * (float)a.Z / alen;
                  triangleToWorld.M14 = 0.0f;

                  var n = a.Cross(b).ToUnit();
                  var vert = n.Cross(a).ToUnit();
                  //                  var blen = (float)b.Norm2D();
                  triangleToWorld.M21 = globalDownscale * (float)vert.X;
                  triangleToWorld.M22 = globalDownscale * (float)vert.Y;
                  triangleToWorld.M23 = globalDownscale * (float)vert.Z;
                  triangleToWorld.M24 = 0.0f;

                  triangleToWorld.M31 = globalDownscale * (float)n.X;
                  triangleToWorld.M32 = globalDownscale * (float)n.Y;
                  triangleToWorld.M33 = globalDownscale * (float)n.Z;
                  triangleToWorld.M34 = 0.0f;

                  triangleToWorld.M41 = (float)v1.X;
                  triangleToWorld.M42 = (float)v1.Y;
                  triangleToWorld.M43 = (float)v1.Z;
                  triangleToWorld.M44 = 1.0f;

                  snd.WorldTransform = triangleToWorld;
                  snd.WorldToLocalScalingFactor = localUpscale;
                  terrainService.AddSectorNodeDescription(snd);

                  // var store = new SectorGraphDescriptionStore();
                  // var ts = new TerrainService(store, new TerrainSnapshotCompiler(store));
                  // ts.AddSectorNodeDescription(snd);
                  // ts.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(snd, snd, new IntLineSegment2(po, pa), new IntLineSegment2(po, pa)));
                  // ts.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(snd, snd, new IntLineSegment2(pa, pb), new IntLineSegment2(pa, pb)));
                  // ts.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(snd, snd, new IntLineSegment2(pb, po), new IntLineSegment2(pb, po)));
                  // ts.CompileSnapshot().OverlayNetworkManager.CompileTerrainOverlayNetwork(0.0);

                  Herp(snd, i1, i2, new IntLineSegment2(po, pa));
                  Herp(snd, i2, i3, new IntLineSegment2(pa, pb));
                  Herp(snd, i3, i1, new IntLineSegment2(pb, po));
                  break;
            }
         }

         var lowerbound = verts.Aggregate(new DoubleVector3(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity), (a, b) => new DoubleVector3(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z)));
         var upperbound = verts.Aggregate(new DoubleVector3(double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity), (a, b) => new DoubleVector3(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z)));
         // Console.WriteLine("Loaded map bounds: " + lowerbound + " " + upperbound + " " + (upperbound + lowerbound) / 2 + " " + (upperbound - lowerbound));
      }
   }
}
