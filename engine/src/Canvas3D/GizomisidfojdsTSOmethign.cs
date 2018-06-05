using System;
using System.Windows.Forms;
using Canvas3D.LowLevel;
using SharpDX;

namespace Canvas3D {
   public class GizomisidfojdsTSOmethign {
      private const float HandleLength = 2.0f;
      private readonly InputSomethingOSDJFH input;
      private readonly IMesh sphereMesh;
      private readonly IMesh cubeMesh;

      private Vector3 position = new Vector3(0, 0.5f, 0);
      private bool selected = false;
      private bool isDraggingHandle = false;
      private Plane draggingPlane;
      private Vector3[] dragDirectionBasis = null;
      private Vector3 someDelta;

      public GizomisidfojdsTSOmethign(InputSomethingOSDJFH input, IMesh sphereMesh, IMesh cubeMesh) {
         this.input = input;
         this.sphereMesh = sphereMesh;
         this.cubeMesh = cubeMesh;
      }

      public void HandleFrameEnter(Scene scene, Matrix projView) {
         var viewProj = projView;
         viewProj.Transpose();
         var ray = Ray.GetPickRay(input.X, input.Y, new ViewportF(0, 0, 1280, 720, 1.0f, 100.0f), viewProj);

         if (input.IsMouseJustDown(MouseButtons.Left)) {
            var bbx = new BoundingBox(position - new Vector3(0.0f, 0.05f, 0.05f), position + new Vector3(HandleLength, 0.05f, 0.05f));
            var bby = new BoundingBox(position - new Vector3(0.05f, 0.0f, 0.05f), position + new Vector3(0.05f, HandleLength, 0.05f));
            var bbxy = new BoundingBox(position - new Vector3(0, 0.0f, 0.05f), position + new Vector3(HandleLength, HandleLength, 0.05f));
            Vector3 intersectionPoint;
            if (selected && ray.Intersects(ref bbx, out intersectionPoint)) {
               isDraggingHandle = true;
               draggingPlane = new Plane(position, Vector3.UnitZ);
               dragDirectionBasis = new[] { Vector3.UnitX };
               if (!ray.Intersects(ref draggingPlane, out Vector3 p)) throw new InvalidOperationException();
               someDelta = p - position;
            } else if (selected && ray.Intersects(ref bby, out intersectionPoint)) {
               isDraggingHandle = true;
               draggingPlane = new Plane(position, Vector3.UnitZ);
               dragDirectionBasis = new[] { Vector3.UnitY };
               if (!ray.Intersects(ref draggingPlane, out Vector3 p)) throw new InvalidOperationException();
               someDelta = p - position;
            } else if (selected && ray.Intersects(ref bbxy, out intersectionPoint)) {
               isDraggingHandle = true;
               draggingPlane = new Plane(position, Vector3.UnitZ);
               dragDirectionBasis = new[] { Vector3.UnitX, Vector3.UnitY };
               if (!ray.Intersects(ref draggingPlane, out Vector3 p)) throw new InvalidOperationException();
               someDelta = p - position;
            } else {
               selected = ray.Intersects(new BoundingSphere(position, 0.5f));
               Console.WriteLine(selected);
            }
         }

         if (input.IsMouseUp(MouseButtons.Left)) {
            isDraggingHandle = false;
         }


         if (isDraggingHandle) {
            if (!ray.Intersects(ref draggingPlane, out Vector3 intersectionPoint)) {
               throw new InvalidOperationException();
            }
            var unconstrainedDelta = intersectionPoint - position - someDelta;
            var constrainedDelta = Vector3.Zero;
            foreach (var v in dragDirectionBasis) {
               var numerator = Vector3.Dot(v, unconstrainedDelta);
               var denominator = v.LengthSquared();
               constrainedDelta += new Vector3(
                  (v.X * numerator) / denominator,
                  (v.Y * numerator) / denominator,
                  (v.Z * numerator) / denominator);
            }
            position += constrainedDelta;
         }

         if (selected) {
            var sphereBatch = RenderJobBatch.Create(sphereMesh);
            sphereBatch.Wireframe = true;
            sphereBatch.Jobs.Add(new RenderJobDescription {
               Color = Color.Cyan,
               MaterialProperties = { Metallic = 0, Roughness = 0 },
               MaterialResourcesIndex = -1,
               WorldTransform = MatrixCM.Translation(position) * MatrixCM.Scaling(1.1f)
            });
            scene.AddRenderJobBatch(sphereBatch);
            scene.AddRenderable(
               cubeMesh,
               MatrixCM.Translation(position + new Vector3(HandleLength / 2, 0, 0)) * MatrixCM.Scaling(HandleLength, 0.1f, 0.1f),
               new MaterialDescription { Properties = { Metallic = 0.0f, Roughness = 0.04f } },
               Color.Red);
            scene.AddRenderable(
               cubeMesh,
               MatrixCM.Translation(position + new Vector3(0, HandleLength / 2, 0)) * MatrixCM.Scaling(0.1f, HandleLength, 0.1f),
               new MaterialDescription { Properties = { Metallic = 0.0f, Roughness = 0.04f } },
               Color.Lime);
            scene.AddRenderable(
               cubeMesh,
               MatrixCM.Translation(position + new Vector3(0, 0, HandleLength / 2)) * MatrixCM.Scaling(0.1f, 0.1f, HandleLength),
               new MaterialDescription { Properties = { Metallic = 0.0f, Roughness = 0.04f } },
               Color.Blue);

            scene.AddRenderable(
               cubeMesh,
               MatrixCM.Translation(position + new Vector3(HandleLength / 2, HandleLength / 2, 0)) * MatrixCM.Scaling(HandleLength, HandleLength, 0.01f),
               new MaterialDescription { Properties = { Metallic = 0.0f, Roughness = 0.04f } },
               Color.Yellow);

            scene.AddRenderable(
               sphereMesh,
               MatrixCM.Translation(position + new Vector3(HandleLength, HandleLength, 0)) * MatrixCM.Scaling(0.1f),
               new MaterialDescription { Properties = { Metallic = 0.0f, Roughness = 0.04f } },
               Color.Blue);
         }
      }
   }
}