// See https://gamedev.stackexchange.com/questions/96459/fast-ray-sphere-collision-code
// See http://www-labs.iro.umontreal.ca/~sherknie/articles/faq_Divers/graphics-algorithms-faq.txt
//     "Subject 5.07: How do I determine the intersection between a ray and a sphere"
// Returns ray parameters <tnear, tfar> in order reached by ray.
// If tnear > tfar, no intersection! If equal, then ray is tangent to sphere.
float2 findRayAndCenteredSphereIntersections(float2 rayOrigin, float2 rayDirection, float sphereRadius) {
    float a = dot(rayDirection, rayDirection);
    float b = 2.0 * dot(rayDirection, rayOrigin);
    float c = dot(rayOrigin, rayOrigin) - sphereRadius * sphereRadius;
    float discriminant = b * b - 4 * a * c;

	// (-b +- sqrt(b^2 - 4ac)) / 2a
	if (discriminant < 0) return float2(1E30, -1E30);
	float rootDiscriminant = sqrt(discriminant);
	float twoA = 2.0 * a;
    return vec2((-b - rootDiscriminant) / twoA, (-b + rootDiscriminant) / twoA);
}