using System.Collections.Generic;
using System.Numerics;
using OpenMOBA.Geometry;

namespace OpenMOBA.Foundation.Terrain.Snapshots {
   public class SectorSnapshot {
      public Matrix4x4 WorldTransform;
      public Matrix4x4 WorldTransformInv;
      public SectorNodeDescription SectorNodeDescription { get; set; }
      public TerrainStaticMetadata StaticMetadata => SectorNodeDescription.StaticMetadata;

      // Ensures boundary regions for edges are reachable given character radius
      public List<ISourceSegmentEdgeDescription> SourceSegmentEdgeDescriptions { get; } = new List<ISourceSegmentEdgeDescription>();

      // Helper for transforms
      public DoubleVector2 WorldToLocal(IntVector3 p) {
         return WorldToLocal(p.ToDoubleVector3());
      }

      public DoubleVector2 WorldToLocal(DoubleVector3 p) {
         return Vector3.Transform(p.ToDotNetVector(), (Matrix4x4)WorldTransformInv).ToOpenMobaVector().XY;
      }

      public IntLineSegment2 WorldToLocal(IntLineSegment3 s) {
         return new IntLineSegment2(WorldToLocal(s.First).LossyToIntVector2(), WorldToLocal(s.Second).LossyToIntVector2());
      }

      public DoubleVector3 LocalToWorld(IntVector2 p) {
         return Vector3.Transform(new IntVector3(p).ToDotNetVector(), (Matrix4x4)WorldTransform).ToOpenMobaVector();
      }

      public DoubleVector3 LocalToWorld(DoubleVector2 p) {
         return Vector3.Transform(new DoubleVector3(p).ToDotNetVector(), (Matrix4x4)WorldTransform).ToOpenMobaVector();
      }
   }
}