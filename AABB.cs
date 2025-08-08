using System;
using System.Globalization;
using System.Linq.Expressions;
using TMPro;
using Unity.Mathematics;
using UnityEditor.Experimental.GraphView;
using UnityEditor.Rendering.LookDev;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;
using static UnityEngine.UI.Image;

[ExecuteInEditMode]
public class AABB : MonoBehaviour
{
    public float angle = 0f;
    public float test = 0f;
    public float depth = 14f;
    public enum Panel
    {
        XZ,
        XY,
        ZY
    }

    static float square(float x) => x * x;

    // Update is called once per frame
    void Update()
    {
        DrawFrustum();
        Pers();
    }

    static bool GetCircleClipPoints(float3 circleCenter, float3 circleNormal, float circleRadius, float near, out float3 p0, out float3 p1)
    {
        // The intersection of two planes is a line where the direction is the cross product of the two plane normals.
        // In this case, it is the plane containing the circle, and the near plane.
        var lineDirection = math.normalize(math.cross(circleNormal, math.float3(0, 0, 1)));

        // Find a direction on the circle plane towards the nearest point on the intersection line.
        // It has to be perpendicular to the circle normal to be in the circle plane. The direction to the closest
        // point on a line is perpendicular to the line direction. Thus this is given by the cross product of the
        // line direction and the circle normal, as this gives us a vector that is perpendicular to both of those.
        var nearestDirection = math.cross(lineDirection, circleNormal);

        // Distance from circle center to the intersection line along `nearestDirection`.
        // This is done using a ray-plane intersection, where the plane is the near plane.
        // ({0, 0, near} - circleCenter) . {0, 0, 1} / (nearestDirection . {0, 0, 1})
        var distance = (near - circleCenter.z) / nearestDirection.z;

        // The point on the line nearest to the circle center when traveling only in the circle plane.
        var nearestPoint = circleCenter + nearestDirection * distance;

        // Any line through a circle makes a chord where the endpoints are the intersections with the circle.
        // The half length of the circle chord can be found by constructing a right triangle from three points:
        // (a) The circle center.
        // (b) The nearest point.
        // (c) A point that is on circle and the intersection line.
        // The hypotenuse is formed by (a) and (c) and will have length `circleRadius` as it is on the circle.
        // The known side if formed by (a) and (b), which we have already calculated the distance of in `distance`.
        // The unknown side formed by (b) and (c) is then found using Pythagoras.
        var chordHalfLength = math.sqrt(square(circleRadius) - square(distance));
        p0 = nearestPoint + lineDirection * chordHalfLength;
        p1 = nearestPoint - lineDirection * chordHalfLength;

        return math.abs(distance) <= circleRadius;
    }

    static void GetProjectedCircleHorizon(float2 center, float radius, float2 U, float2 V, out float2 uv1, out float2 uv2)
    {
        // U is assumed to be constructed such that it is never 0, but V can be if the circle projects to a line segment.
        // In that case, the solution can be trivially found using U only.
        var vl = math.length(V);
        if (vl < 1e-6f)
        {
            uv1 = math.float2(radius, 0);
            uv2 = math.float2(-radius, 0);
        }
        else
        {
            var ul = math.length(U);
            var ulinv = math.rcp(ul);
            var vlinv = math.rcp(vl);

            // Normalize U and V in the plane.
            var u = U * ulinv;
            var v = V * vlinv;

            // Major and minor axis of the ellipse.
            var a = ul * radius;
            var b = vl * radius;

            // Project the camera position into a 2D coordinate system with the circle at (0, 0) and
            // the ellipse major and minor axes as the coordinate system axes. This allows us to use the standard
            // form of the ellipse equation, greatly simplifying the calculations.
            var cameraUV = math.float2(math.dot(-center, u), math.dot(-center, v));

            // Find the polar line of the camera position in the normalized UV coordinate system.
            var polar = math.float3(cameraUV.x / square(a), cameraUV.y / square(b), -1);
            var (t1, t2) = IntersectEllipseLine(a, b, polar);

            // Find Y by putting polar into line equation and solving. Denormalize by dividing by U and V lengths.
            uv1 = math.float2(t1 * ulinv, (-polar.x / polar.y * t1 - polar.z / polar.y) * vlinv);
            uv2 = math.float2(t2 * ulinv, (-polar.x / polar.y * t2 - polar.z / polar.y) * vlinv);
        }
    }

    static (float, float) IntersectEllipseLine(float a, float b, float3 line)
    {
        // The line is represented as a homogenous 2D line {u, v, w} such that ux + vy + w = 0.
        // The ellipse is represented by the implicit equation x^2/a^2 + y^2/b^2 = 1.
        // We solve the line equation for y:  y = (ux + w) / v
        // We then substitute this into the ellipse equation and expand and re-arrange a bit:
        //   x^2/a^2 + ((ux + w) / v)^2/b^2 = 1 =>
        //   x^2/a^2 + ((ux + w)^2 / v^2)/b^2 = 1 =>
        //   x^2/a^2 + (ux + w)^2/(v^2 b^2) = 1 =>
        //   x^2/a^2 + (u^2 x^2 + w^2 + 2 u x w)/(v^2 b^2) = 1 =>
        //   x^2/a^2 + x^2 u^2 / (v^2 b^2) + w^2/(v^2 b^2) + x 2 u w / (v^2 b^2) = 1 =>
        //   x^2 (1/a^2 + u^2 / (v^2 b^2)) + x 2 u w / (v^2 b^2) + w^2 / (v^2 b^2) - 1 = 0
        // We now have a quadratic equation with:
        //   a = 1/a^2 + u^2 / (v^2 b^2)
        //   b = 2 u w / (v^2 b^2)
        //   c = w^2 / (v^2 b^2) - 1
        var div = math.rcp(square(line.y) * square(b));
        var qa = 1f / square(a) + square(line.x) * div;
        var qb = 2f * line.x * line.z * div;
        var qc = square(line.z) * div - 1f;
        var sqrtD = math.sqrt(qb * qb - 4f * qa * qc);
        var x1 = (-qb + sqrtD) / (2f * qa);
        var x2 = (-qb - sqrtD) / (2f * qa);
        return (x1, x2);
    }

    public void OnDrawGizmos() 
    {
        //var origin = Vector3.zero;
        //var sphereOrigin = new Vector3(0f, -5f, -10f);
        //var range = 8f;
        //Gizmos.DrawWireSphere(sphereOrigin, range);
        //Debug.DrawLine(origin, sphereOrigin, Color.red);
        //var centre = new Vector2(sphereOrigin.x, sphereOrigin.z);
        ////Debug.DrawLine(origin, centre, Color.red);
        //var direction = math.normalize(centre);
        //var d = math.length(centre);
        //var l = math.sqrt(d * d - range * range);
        //var h = l * range / d;
        //var c = direction * (l * h / range);
        //Debug.DrawLine(origin, new Vector3(c.x, origin.y, c.y), Color.yellow);

        //var c0 = c + math.float2(-direction.y, direction.x) * h;
        //var c1 = c + math.float2(direction.y, -direction.x) * h;
        //Debug.DrawLine(origin, new Vector3(c0.x, origin.y, c0.y), Color.blue);
        //Debug.DrawLine(origin, new Vector3(c1.x, origin.y, c1.y), Color.blue);
        //Debug.DrawLine(sphereOrigin, new Vector3(c0.x, origin.y, c0.y), Color.violet);
        //Debug.DrawLine(new Vector3(c.x, origin.y, c.y), new Vector3(c0.x, origin.y, c0.y), Color.green);
    }

    static void GetSphereHorizon(float2 center, float radius, float near, float clipRadius, out float2 p0, out float2 p1)
    {
        var direction = math.normalize(center);

        // Distance from camera to center of sphere
        var d = math.length(center);

        // Distance from camera to sphere horizon edge
        var l = math.sqrt(d * d - radius * radius);

        // Height of circle horizon
        var h = l * radius / d;

        // Center of circle horizon
        var c = direction * (l * h / radius);

        p0 = math.float2(float.MinValue, 1f);
        p1 = math.float2(float.MaxValue, 1f);

        // Handle clipping
        if (center.y - radius < near)
        {
            p0 = math.float2(center.x + clipRadius, near);
            p1 = math.float2(center.x - clipRadius, near);
        }

        // Circle horizon points
        var c0 = c + math.float2(-direction.y, direction.x) * h;
        if (square(d) >= square(radius) && c0.y >= near)
        {
            if (c0.x > p0.x) { p0 = c0; }
            if (c0.x < p1.x) { p1 = c0; }
        }

        var c1 = c + math.float2(direction.y, -direction.x) * h;
        if (square(d) >= square(radius) && c1.y >= near)
        {
            if (c1.x > p0.x) { p0 = c1; }
            if (c1.x < p1.x) { p1 = c1; }
        }
    }

    static float3 EvaluateNearConic(float near, float3 o, float3 d, float r, float3 u, float3 v, float theta)
    {
        var h = (near - o.z) / (d.z + r * u.z * math.cos(theta) + r * v.z * math.sin(theta));
        return math.float3(o.xy + h * (d.xy + r * u.xy * math.cos(theta) + r * v.xy * math.sin(theta)), near);
    }

    // o, d, u and v are expected to contain {x or y, z}. I.e. pass in x values to find tangents where x' = 0
    // Returns the two theta values as a float2.
    static float2 FindNearConicTangentTheta(float2 o, float2 d, float r, float2 u, float2 v)
    {
        var sqrt = math.sqrt(square(d.x) * square(u.y) + square(d.x) * square(v.y) - 2f * d.x * d.y * u.x * u.y - 2f * d.x * d.y * v.x * v.y + square(d.y) * square(u.x) + square(d.y) * square(v.x) - square(r) * square(u.x) * square(v.y) + 2f * square(r) * u.x * u.y * v.x * v.y - square(r) * square(u.y) * square(v.x));
        var denom = d.x * v.y - d.y * v.x - r * u.x * v.y + r * u.y * v.x;
        return 2 * math.atan((-d.x * u.y + d.y * u.x + math.float2(1, -1) * sqrt) / denom);
    }

    static float2 FindNearConicYTheta(float near, float3 o, float3 d, float r, float3 u, float3 v, float y)
    {
        var sqrt = math.sqrt(-square(d.y) * square(o.z) + 2 * square(d.y) * o.z * near - square(d.y) * square(near) + 2 * d.y * d.z * o.y * o.z - 2 * d.y * d.z * o.y * near - 2 * d.y * d.z * o.z * y + 2 * d.y * d.z * y * near - square(d.z) * square(o.y) + 2 * square(d.z) * o.y * y - square(d.z) * square(y) + square(o.y) * square(r) * square(u.z) + square(o.y) * square(r) * square(v.z) - 2 * o.y * o.z * square(r) * u.y * u.z - 2 * o.y * o.z * square(r) * v.y * v.z - 2 * o.y * y * square(r) * square(u.z) - 2 * o.y * y * square(r) * square(v.z) + 2 * o.y * square(r) * u.y * u.z * near + 2 * o.y * square(r) * v.y * v.z * near + square(o.z) * square(r) * square(u.y) + square(o.z) * square(r) * square(v.y) + 2 * o.z * y * square(r) * u.y * u.z + 2 * o.z * y * square(r) * v.y * v.z - 2 * o.z * square(r) * square(u.y) * near - 2 * o.z * square(r) * square(v.y) * near + square(y) * square(r) * square(u.z) + square(y) * square(r) * square(v.z) - 2 * y * square(r) * u.y * u.z * near - 2 * y * square(r) * v.y * v.z * near + square(r) * square(u.y) * square(near) + square(r) * square(v.y) * square(near));
        var denom = d.y * o.z - d.y * near - d.z * o.y + d.z * y + o.y * r * u.z - o.z * r * u.y - y * r * u.z + r * u.y * near;
        return 2 * math.atan((r * (o.y * v.z - o.z * v.y - y * v.z + v.y * near) + math.float2(1, -1) * sqrt) / denom);
    }

    static void GetConeSideTangentPoints(float3 vertex, float3 axis, float cosHalfAngle, float circleRadius, float coneHeight, float range, float3 circleU, float3 circleV, out float3 l1, out float3 l2)
    {
        l1 = l2 = 0;

        if (math.dot(math.normalize(-vertex), axis) >= cosHalfAngle) //圆锥底面的投影包含了相机？
        {
            return;
        }

        var d = -math.dot(vertex, axis);
        // If d is zero, this leads to a numerical instability in the code later on. This is why we make the value
        // an epsilon if it is zero.
        if (d == 0f) d = 1e-6f;
        var sign = d < 0 ? -1f : 1f;
        // sign *= vertex.z < 0 ? -1f : 1f;
        // `origin` is the center of the circular slice we're about to calculate at distance `d` from the `vertex`.
        var origin = vertex + axis * d;
        // Get the radius of the circular slice of the cone at the `origin`.
        var radius = math.abs(d) * circleRadius / coneHeight;
        DrawCircle(origin, radius, Color.blueViolet, Panel.XY, axis);
        //DrawCircle(origin, radius, Color.pink, Panel.XY, axis);
        // `circleU` and `circleV` are the two vectors perpendicular to the cone's axis. `cameraUV` is thus the
        // position of the camera projected onto the plane of the circular slice. This basically creates a new
        // 2D coordinate space, with (0, 0) located at the center of the circular slice, which why this variable
        // is called `origin`.
        var cameraUV = math.float2(math.dot(circleU, -origin), math.dot(circleV, -origin));
        // Use homogeneous coordinates to find the tangents.
        var polar = math.float3(cameraUV, -square(radius));
        var p1 = math.float2(-1, -polar.x / polar.y * (-1) - polar.z / polar.y);
        var p2 = math.float2(1, -polar.x / polar.y * 1 - polar.z / polar.y);
        Debug.DrawLine(origin, origin + p1.x * circleU + p1.y * circleV, Color.yellow);
        Debug.DrawLine(origin, origin + p2.x * circleU + p2.y * circleV, Color.yellow);
        var lineDirection = math.normalize(p2 - p1);
        var lineNormal = math.float2(lineDirection.y, -lineDirection.x);
        var distToLine = math.dot(p1, lineNormal);
        var lineCenter = lineNormal * distToLine;
        var l = math.sqrt(radius * radius - distToLine * distToLine);
        var x1UV = lineCenter + l * lineDirection;
        var x2UV = lineCenter - l * lineDirection;
        Debug.DrawLine(origin + x1UV.x * circleU + x1UV.y * circleV, origin + x2UV.x * circleU + x2UV.y * circleV, Color.aquamarine);
        //Debug.DrawLine(origin, origin + x1UV.x * circleU + x1UV.y * circleV, Color.pink);
        Debug.DrawLine(origin, origin + x2UV.x * circleU + x2UV.y * circleV, Color.pink);
        var dir1 = math.normalize((origin + x1UV.x * circleU + x1UV.y * circleV) - vertex) * sign;
        var dir2 = math.normalize((origin + x2UV.x * circleU + x2UV.y * circleV) - vertex) * sign;
        l1 = dir1 * range;
        l2 = dir2 * range;

        Debug.DrawLine(vertex, vertex + l1, Color.peru);
        Debug.DrawLine(vertex, vertex + l2, Color.peru);
    }

    static void GetSphereYPlaneHorizon(float3 center, float sphereRadius, float near, float clipRadius, float y, out float3 left, out float3 right)
    {
        // Note: The y-plane is the plane that is determined by `y` in that it contains the vector (1, 0, 0)
        // and goes through the points (0, y, 1) and (0, 0, 0). This would become a straight line in screen-space, and so it
        // represents the boundary between two rows of tiles.

        // Near-plane clipping - will get overwritten if no clipping is needed.
        // `y` is given for the view plane (Z=1), scale it so that it is on the near plane instead.
        var yNear = y * near;
        // Find the two points of intersection between the clip circle of the sphere and the y-plane.
        // Found using Pythagoras with a right triangle formed by three points:
        // (a) center of the clip circle
        // (b) a point straight above the clip circle center on the y-plane
        // (c) a point that is both on the circle and the y-plane (this is the point we want to find in the end)
        // The hypotenuse is formed by (a) and (c) with length equal to the clip radius. The known side is
        // formed by (a) and (b) and is simply the distance from the center to the y-plane along the y-axis.
        // The remaining side gives us the x-displacement needed to find the intersection points.
        var clipHalfWidth = math.sqrt(square(clipRadius) - square(yNear - center.y));
        left = math.float3(center.x - clipHalfWidth, yNear, near);
        right = math.float3(center.x + clipHalfWidth, yNear, near);

        // Basis vectors in the y-plane for being able to parameterize the plane.
        var planeU = math.normalize(math.float3(0, y, 1));
        var planeV = math.float3(1, 0, 0);

        // Calculate the normal of the y-plane. Found from: (0, y, 1) × (1, 0, 0) = (0, 1, -y)
        // This is used to represent the plane along with the origin, which is just 0 and thus doesn't show up
        // in the calculations.
        var normal = math.normalize(math.float3(0, 1, -y));

        // We want to first find the circle from the intersection of the y-plane and the sphere.

        // The shortest distance from the sphere center and the y-plane. The sign determines which side of the plane
        // the center is on.
        var signedDistance = math.dot(normal, center);

        // Unsigned shortest distance from the sphere center to the plane.
        var distanceToPlane = math.abs(signedDistance);

        // The center of the intersection circle in the y-plane, which is the point on the plane closest to the
        // sphere center. I.e. this is at `distanceToPlane` from the center.
        var centerOnPlane = math.float2(math.dot(center, planeU), math.dot(center, planeV));

        // Distance from origin to the circle center.
        var distanceInPlane = math.length(centerOnPlane);

        // Direction from origin to the circle center.
        var directionPS = centerOnPlane / distanceInPlane;

        // Calculate the radius of the circle using Pythagoras. We know that any point on the circle is a point on
        // the sphere. Thus we can construct a triangle with the sphere center, circle center, and a point on the
        // circle. We then want to find its distance to the circle center, as that will be equal to the radius. As
        // the point is on the sphere, it must be `sphereRadius` from the sphere center, forming the hypotenuse. The
        // other side is between the sphere and circle centers, which we've already calculated to be
        // `distanceToPlane`.
        var circleRadius = math.sqrt(square(sphereRadius) - square(distanceToPlane));

        // Now that we have the circle, we can find the horizon points. Since we've parametrized the plane, we can
        // just do this in 2D.

        // Any of these conditions will yield NaN due to negative square roots. They are signs that clipping is needed,
        // so we fallback on the already calculated values in that case.
        if (square(distanceToPlane) <= square(sphereRadius) && square(circleRadius) <= square(distanceInPlane))
        {
            // Distance from origin to circle horizon edge.
            var l = math.sqrt(square(distanceInPlane) - square(circleRadius));

            // Height of circle horizon.
            var h = l * circleRadius / distanceInPlane;

            // Center of circle horizon.
            var c = directionPS * (l * h / circleRadius);

            // Calculate the horizon points in the plane.
            var leftOnPlane = c + math.float2(directionPS.y, -directionPS.x) * h;
            var rightOnPlane = c + math.float2(-directionPS.y, directionPS.x) * h;

            // Transform horizon points to view space and use if not clipped.
            var leftCandidate = leftOnPlane.x * planeU + leftOnPlane.y * planeV;
            if (leftCandidate.z >= near) left = leftCandidate;

            var rightCandidate = rightOnPlane.x * planeU + rightOnPlane.y * planeV;
            if (rightCandidate.z >= near) right = rightCandidate;
        }
    }

    public void Pers()
    {
        var lightOrigin = new Vector3(0, 0, depth);
        var range = 10f;
        var ray = new Vector3(1f, 1f, angle).normalized;

        var near = 4f;
        var y = 0.75f;
        var yNear = y * near;
        var sphereTile0 = Vector3.zero;
        var sphereTile1 = Vector3.zero;

        var planeU = math.normalize(math.float3(0, y, 1));
        var planeV = math.float3(1, 0, 0);

        var normal = math.normalize(math.float3(0, 1, -y));

        var signedDistance = math.dot(normal, lightOrigin);

        var distanceToPlane = math.abs(signedDistance);

        var centerOnPlane = math.float2(math.dot(lightOrigin, planeU), math.dot(lightOrigin, planeV));

        var centerPos = centerOnPlane.x * planeU + centerOnPlane.y * planeV;
        Debug.DrawLine(centerPos, lightOrigin, Color.darkGreen);
        Debug.DrawLine(centerPos, centerPos + planeU, Color.white);
        Debug.DrawLine(centerPos, centerPos + planeV, Color.white);

        var distanceInPlane = math.length(centerOnPlane);

        var directionPS = centerOnPlane / distanceInPlane;

        var circleRadius = math.sqrt(square(range) - square(distanceToPlane));
        DrawCircle(centerPos, circleRadius, Color.red, Panel.XY, normal);

        if (square(distanceToPlane) <= square(range) && square(circleRadius) <= square(distanceInPlane))
        {
            var l = math.sqrt(square(distanceInPlane) - square(circleRadius));

            var h = l * circleRadius / distanceInPlane;

            var c = directionPS * (l * h / circleRadius);

            var leftOnPlane = c + math.float2(directionPS.y, -directionPS.x) * h;
            var rightOnPlane = c + math.float2(-directionPS.y, directionPS.x) * h;

            var leftCandidate = leftOnPlane.x * planeU + leftOnPlane.y * planeV;
            if (leftCandidate.z >= near) sphereTile0 = leftCandidate;

            var rightCandidate = rightOnPlane.x * planeU + rightOnPlane.y * planeV;
            if (rightCandidate.z >= near) sphereTile1 = rightCandidate;

            Debug.DrawLine(sphereTile0, c.x * planeU + c.y * planeV, Color.purple);
            Debug.DrawLine(sphereTile0, centerPos, Color.plum);
            Debug.DrawLine(Vector3.zero, centerPos, Color.darkCyan);
            Debug.DrawLine(centerPos, c.x * planeU + c.y * planeV, Color.black);

            var d = math.float2(directionPS.y, -directionPS.x) * circleRadius;
            Debug.DrawLine(centerPos + d.x * planeU + d.y * planeV, lightOrigin, Color.lightGreen);
            Debug.DrawLine(centerPos + d.x * planeU + d.y * planeV, centerPos, Color.greenYellow);
        }

        DrawCircle(lightOrigin, range, Color.blue, Panel.XY, directionPS.x * planeU + directionPS.y * planeV);
        Debug.DrawLine(Vector3.zero, sphereTile0, Color.yellow);
        Debug.DrawLine(Vector3.zero, sphereTile1, Color.yellow);
    }

    public void Orth() 
    {
        var lightOrigin = Vector3.zero;
        var range = 10f;
        var ray = new Vector3(1f, 1f, angle).normalized;
        var orientation = Quaternion.FromToRotation(Vector3.back, ray);
        float3 rayValue = new float3(ray);
        DrawCircle(lightOrigin, range, Color.yellow, Panel.XY, Vector3.forward);

        var radius = 6f;
        var height = 8f;
        var origin = lightOrigin + ray * height;
        var originFloat3 = new float3(origin);
        Debug.DrawLine(lightOrigin, origin, Color.red);
        DrawCircle(origin, radius, Color.blue, Panel.XY, ray);
        //Debug.DrawLine(lightOrigin, lightOrigin + ray * height + orientation * new Vector3(radius, 0, 0), Color.blue);
        //Debug.DrawLine(lightOrigin, lightOrigin + ray * height + orientation * new Vector3(-radius, 0, 0), Color.blue);
        //Debug.DrawLine(lightOrigin, lightOrigin + ray * height + orientation * new Vector3(0, radius, 0), Color.blue);
        //Debug.DrawLine(lightOrigin, lightOrigin + ray * height + orientation * new Vector3(0, -radius, 0), Color.blue);

        //var planeY = test;
        //Debug.DrawLine(new Vector3(-100, planeY, 0), new Vector3(100, planeY, 0), Color.black);

        //var circleRadius = math.sqrt(range * range - height * height);
        //var circleUp = math.normalize(math.float3(0, 1, 0) - rayValue * rayValue.y);
        //var circleRight = math.normalize(math.float3(1, 0, 0) - rayValue * rayValue.x);
        //var circleBoundY0 = originFloat3 - circleUp * circleRadius;
        //var circleBoundY1 = originFloat3 + circleUp * circleRadius;
        //var circleBoundX0 = originFloat3 - circleRight * circleRadius;
        //var circleBoundX1 = originFloat3 + circleRight * circleRadius;
        //Debug.DrawLine(origin, circleBoundY0, Color.yellow);
        //Debug.DrawLine(origin, circleBoundY1, Color.yellow);
        //Debug.DrawLine(origin, circleBoundX0, Color.yellow);
        //Debug.DrawLine(origin, circleBoundX1, Color.yellow);

        //var intersectionDistance = (planeY - origin.y) / circleUp.y;
        //var closestPointX = origin.x + intersectionDistance * circleUp.x;
        //var intersectionDirX = -ray.z / math.length(math.float3(-ray.z, 0, ray.x));
        //var sideDistance = math.sqrt(square(radius) - square(intersectionDistance));
        //var circleX0 = closestPointX - sideDistance * intersectionDirX;
        //var circleX1 = closestPointX + sideDistance * intersectionDirX;
        //Debug.DrawLine(origin, new Vector3(circleX0, planeY, 0), Color.pink);
        //Debug.DrawLine(origin, new Vector3(circleX1, planeY, 0), Color.pink);

        //var sphereDistance = height + radius * radius / height;
        //var sphereRadius = math.sqrt(square(radius * radius) / (height * height) + radius * radius);
        //var directionXYSqInv = math.rcp(math.lengthsq(rayValue.xy));
        //var polarIntersection = -radius * radius / height * directionXYSqInv * rayValue.xy;
        //var polarDir = math.sqrt((square(sphereRadius) - math.lengthsq(polarIntersection)) * directionXYSqInv) * math.float2(rayValue.y, -rayValue.x);
        //var conePBase = new float2(lightOrigin.x, lightOrigin.y) + sphereDistance * rayValue.xy + polarIntersection;
        //var coneP0 = conePBase - polarDir;
        //var coneP1 = conePBase + polarDir;
        //Debug.DrawLine(new Vector3(coneP0.x, coneP0.y, 0f), new Vector3(conePBase.x, conePBase.y, 0f), Color.black);
        //Debug.DrawLine(new Vector3(coneP1.x, coneP1.y, 0f), new Vector3(conePBase.x, conePBase.y, 0f), Color.black);
        //Debug.DrawLine(new Vector3(coneP0.x, coneP0.y, 0f), lightOrigin, Color.black);
        //Debug.DrawLine(new Vector3(coneP1.x, coneP1.y, 0f), lightOrigin, Color.black);
        //Debug.DrawLine(lightOrigin + new Vector3((sphereDistance * rayValue.xy).x, (sphereDistance * rayValue.xy).y, 0), lightOrigin, Color.red);
        //var coneDir0X = coneP0.x - lightOrigin.x;
        //var coneDir0YInv = math.rcp(coneP0.y - lightOrigin.y);
        //var coneDir1X = coneP1.x - lightOrigin.x;
        //var coneDir1YInv = math.rcp(coneP1.y - lightOrigin.y);
        //var deltaY = planeY - lightOrigin.y;
        //var coneT0 = deltaY * coneDir0YInv;
        //var coneT1 = deltaY * coneDir1YInv;
        //if (coneT0 >= 0 && coneT0 <= 1)
        //    Debug.DrawLine(lightOrigin, new Vector3(lightOrigin.x + coneT0 * coneDir0X, planeY, 0f), Color.pink);
        //if (coneT1 >= 0 && coneT1 <= 1)
        //    Debug.DrawLine(lightOrigin, new Vector3(lightOrigin.x + coneT1 * coneDir1X, planeY, 0f), Color.pink);

        //var circleUp = math.normalize(math.float3(0, 1, 0) - rayValue * rayValue.y);
        //var intersectionDistance = (planeY - origin.y) / circleUp.y;
        //var closestPointX = origin.x + intersectionDistance * circleUp.x;
        //var intersectionDirX = -ray.z / math.length(math.float3(-ray.z, 0, ray.x));
        //var sideDistance = math.sqrt(square(radius) - square(intersectionDistance));
        //var circleX0 = closestPointX - sideDistance * intersectionDirX;
        //var circleX1 = closestPointX + sideDistance * intersectionDirX;
        //Debug.DrawLine(origin, new Vector3(circleX0, planeY, 0), Color.pink);
        //Debug.DrawLine(origin, new Vector3(circleX1, planeY, 0), Color.pink);

        //var intersectionDistance = (planeY - origin.y) / circleUp.y;
        //var closestPointX = origin.x + intersectionDistance * circleUp.x;
        //var intersectionDirX = -ray.z / math.length(math.float3(-ray.z, 0, ray.x));
        //var sideDistance = math.sqrt(square(radius) - square(intersectionDistance));
        //var circleX0 = closestPointX - sideDistance * intersectionDirX;
        //var circleX1 = closestPointX + sideDistance * intersectionDirX;
        //Debug.DrawLine(origin, new Vector3(circleX0, planeY, 0), Color.pink);
        //Debug.DrawLine(origin, new Vector3(circleX1, planeY, 0), Color.pink);

        //var circleRadius = math.sqrt(range * range - height * height);
        //var circleUp = math.normalize(math.float3(0, 1, 0) - rayValue * rayValue.y);
        //var circleRight = math.normalize(math.float3(1, 0, 0) - rayValue * rayValue.x);
        //var circleBoundY0 = originFloat3 - circleUp * circleRadius;
        //var circleBoundY1 = originFloat3 + circleUp * circleRadius;
        //var circleBoundX0 = originFloat3 - circleRight * circleRadius;
        //var circleBoundX1 = originFloat3 + circleRight * circleRadius;
        //Debug.DrawLine(origin, circleBoundY0, Color.yellow);
        //Debug.DrawLine(origin, circleBoundY1, Color.yellow);
        //Debug.DrawLine(origin, circleBoundX0, Color.yellow);
        //Debug.DrawLine(origin, circleBoundX1, Color.yellow);

        //Orth + sphereBound
        //var range = 10f;
        //DrawCircle(lightOrigin, range, Color.yellow, Panel.XY, Vector3.forward);

        // Orth + circleBound
        //var range = 10f;
        //var circleCenter = lightOrigin + ray * height;
        //var circleRadius = math.sqrt(range * range - height * height);
        //var circleRadiusSq = square(circleRadius);
        //var circleUp = math.normalize(math.float3(0, 1, 0) - rayValue * rayValue.y);
        //var circleRight = math.normalize(math.float3(1, 0, 0) - rayValue * rayValue.x);
        //var centre = new float3(circleCenter);
        //var circleBoundY0 = centre - circleUp * circleRadius;
        //var circleBoundY1 = centre + circleUp * circleRadius;
        //var circleBoundX0 = centre - circleRight * circleRadius;
        //var circleBoundX1 = centre + circleRight * circleRadius;
        //Debug.DrawLine(centre, circleBoundY0, Color.yellow);
        //Debug.DrawLine(centre, circleBoundY1, Color.yellow);
        //Debug.DrawLine(centre, circleBoundX0, Color.yellow);
        //Debug.DrawLine(centre, circleBoundX1, Color.yellow);

        //var planeY = test;
        //Debug.DrawLine(new Vector3(-100, planeY, 0), new Vector3(100, planeY, 0), Color.black);

        //var intersectionDistance = (planeY - origin.y) / circleUp.y;
        //var closestPointX = origin.x + intersectionDistance * circleUp.x;
        //var intersectionDirX = -ray.z / math.length(math.float3(-ray.z, 0, ray.x));
        //var sideDistance = math.sqrt(square(radius) - square(intersectionDistance));
        //var circleX0 = closestPointX - sideDistance * intersectionDirX;
        //var circleX1 = closestPointX + sideDistance * intersectionDirX;
        //Debug.DrawLine(origin, new Vector3(circleX0, planeY, 0), Color.pink);
        //Debug.DrawLine(origin, new Vector3(circleX1, planeY, 0), Color.pink);


        // Orth + Tile + Sphere
        //var range = 10f;
        //var planeY = -2f;
        //Debug.DrawLine(new Vector3(-100, planeY, 0), new Vector3(100, planeY, 0), Color.black);
        //var sphereX = math.sqrt(range * range - square(planeY - lightOrigin.y));
        //var sphereX0 = math.float3(lightOrigin.x - sphereX, planeY, lightOrigin.z);
        //var sphereX1 = math.float3(lightOrigin.x + sphereX, planeY, lightOrigin.z);
        //Debug.DrawLine(lightOrigin, sphereX0, Color.black);
        //Debug.DrawLine(lightOrigin, sphereX1, Color.black);
        //DrawCircle(lightOrigin, range, Color.yellow, Panel.XY, Vector3.forward);

        //Orth + Tile + Circle
        //var sphereDistance = height + radius * radius / height;
        //var sphereRadius = math.sqrt(square(radius * radius) / height / height + radius * radius);
        //var directionXYSqInv = math.rcp(math.lengthsq(rayValue.xy));
        //var polarIntersection = -radius * radius / height * directionXYSqInv * rayValue.xy;
        //var polarDir = math.sqrt((square(sphereRadius) - math.lengthsq(polarIntersection)) * directionXYSqInv) * math.float2(rayValue.y, -rayValue.x);
        //var conePBase = new float2(lightOrigin.x, lightOrigin.y) + sphereDistance * rayValue.xy + polarIntersection;
        //var coneP0 = conePBase - polarDir;
        //var coneP1 = conePBase + polarDir;
        //Debug.DrawLine(new Vector3(coneP0.x, coneP0.y, 0f), new Vector3(conePBase.x, conePBase.y, 0f), Color.black);
        //Debug.DrawLine(new Vector3(coneP1.x, coneP1.y, 0f), new Vector3(conePBase.x, conePBase.y, 0f), Color.black);

        //var coneDir0X = coneP0.x - lightOrigin.x;
        //var coneDir0YInv = math.rcp(coneP0.y - lightOrigin.y);
        //var coneDir1X = coneP1.x - lightOrigin.x;
        //var coneDir1YInv = math.rcp(coneP1.y - lightOrigin.y);

        //var planeY = test;
        //Debug.DrawLine(new Vector3(-100, planeY, 0), new Vector3(100, planeY, 0), Color.black);
        //var deltaY = planeY - lightOrigin.y;
        //var coneT0 = deltaY * coneDir0YInv;
        //var coneT1 = deltaY * coneDir1YInv;
        //if (coneT0 >= 0 && coneT0 <= 1)
        //    Debug.DrawLine(lightOrigin, new Vector3(lightOrigin.x + coneT0 * coneDir0X, planeY, 0f), Color.pink);
        //if (coneT1 >= 0 && coneT1 <= 1)
        //    Debug.DrawLine(lightOrigin, new Vector3(lightOrigin.x + coneT1 * coneDir1X, planeY, 0f), Color.pink);
    }

    public void Orth_Spot_Sphere() 
    {
        var lightOrigin = Vector3.zero;
        var range = 10f;
        var ray = new Vector3(1f, 1f, angle).normalized;
        var orientation = Quaternion.FromToRotation(Vector3.back, ray);
        float3 rayValue = new float3(ray);
        var sphereBoundY0 = lightOrigin - new Vector3(0, range, 0);
        var sphereBoundY1 = lightOrigin + new Vector3(0, range, 0);
        var sphereBoundX0 = lightOrigin - new Vector3(range, 0, 0);
        var sphereBoundX1 = lightOrigin + new Vector3(range, 0, 0);
        DrawCircle(lightOrigin, range, Color.yellow, Panel.XY, Vector3.forward);
        Debug.DrawLine(lightOrigin, sphereBoundY0, Color.yellow);
        Debug.DrawLine(lightOrigin, sphereBoundY1, Color.yellow);
        Debug.DrawLine(lightOrigin, sphereBoundX0, Color.yellow);
        Debug.DrawLine(lightOrigin, sphereBoundX1, Color.yellow);

        var radius = 6f;
        var height = 8f;
        var origin = lightOrigin + ray * height;
        Debug.DrawLine(lightOrigin, origin, Color.red);
        DrawCircle(origin, radius, Color.blue, Panel.XY, ray);
        Debug.DrawLine(lightOrigin, lightOrigin + ray * height + orientation * new Vector3(radius, 0, 0), Color.blue);
        Debug.DrawLine(lightOrigin, lightOrigin + ray * height + orientation * new Vector3(-radius, 0, 0), Color.blue);
        Debug.DrawLine(lightOrigin, lightOrigin + ray * height + orientation * new Vector3(0, radius, 0), Color.blue);
        Debug.DrawLine(lightOrigin, lightOrigin + ray * height + orientation * new Vector3(0, -radius, 0), Color.blue);
    }
    public void Orth_Spot_Cone() 
    {
        var lightOrigin = Vector3.zero;
        var range = 10f;
        var ray = new Vector3(1f, 1f, angle).normalized;
        var orientation = Quaternion.FromToRotation(Vector3.back, ray);
        float3 rayValue = new float3(ray);

        var radius = 6f;
        var height = 8f;
        var origin = lightOrigin + ray * height;
        var originFloat3 = new float3(origin);
        Debug.DrawLine(lightOrigin, origin, Color.red);
        DrawCircle(origin, radius, Color.blue, Panel.XY, ray);
        Debug.DrawLine(lightOrigin, lightOrigin + ray * height + orientation * new Vector3(radius, 0, 0), Color.blue);
        Debug.DrawLine(lightOrigin, lightOrigin + ray * height + orientation * new Vector3(-radius, 0, 0), Color.blue);
        Debug.DrawLine(lightOrigin, lightOrigin + ray * height + orientation * new Vector3(0, radius, 0), Color.blue);
        Debug.DrawLine(lightOrigin, lightOrigin + ray * height + orientation * new Vector3(0, -radius, 0), Color.blue);

        var circleRadius = math.sqrt(range * range - height * height);
        var circleUp = math.normalize(math.float3(0, 1, 0) - rayValue * rayValue.y);
        var circleRight = math.normalize(math.float3(1, 0, 0) - rayValue * rayValue.x);
        var circleBoundY0 = originFloat3 - circleUp * circleRadius;
        var circleBoundY1 = originFloat3 + circleUp * circleRadius;
        var circleBoundX0 = originFloat3 - circleRight * circleRadius;
        var circleBoundX1 = originFloat3 + circleRight * circleRadius;
        Debug.DrawLine(origin, circleBoundY0, Color.yellow);
        Debug.DrawLine(origin, circleBoundY1, Color.yellow);
        Debug.DrawLine(origin, circleBoundX0, Color.yellow);
        Debug.DrawLine(origin, circleBoundX1, Color.yellow);
    }
    public void Orth_PlaneY()
    {
        var lightOrigin = Vector3.zero;
        var range = 10f;
        var ray = new Vector3(1f, 1f, angle).normalized;
        var orientation = Quaternion.FromToRotation(Vector3.back, ray);
        float3 rayValue = new float3(ray);
        DrawCircle(lightOrigin, range, Color.yellow, Panel.XY, Vector3.forward);

        var radius = 6f;
        var height = 8f;
        var origin = lightOrigin + ray * height;
        var originFloat3 = new float3(origin);
        Debug.DrawLine(lightOrigin, origin, Color.red);
        DrawCircle(origin, radius, Color.blue, Panel.XY, ray);
        Debug.DrawLine(lightOrigin, lightOrigin + ray * height + orientation * new Vector3(radius, 0, 0), Color.blue);
        Debug.DrawLine(lightOrigin, lightOrigin + ray * height + orientation * new Vector3(-radius, 0, 0), Color.blue);
        Debug.DrawLine(lightOrigin, lightOrigin + ray * height + orientation * new Vector3(0, radius, 0), Color.blue);
        Debug.DrawLine(lightOrigin, lightOrigin + ray * height + orientation * new Vector3(0, -radius, 0), Color.blue);

        var planeY = test;
        Debug.DrawLine(new Vector3(-100, planeY, 0), new Vector3(100, planeY, 0), Color.black);

        var sphereX = math.sqrt(range * range - square(planeY - lightOrigin.y));
        var sphereX0 = math.float3(lightOrigin.x - sphereX, planeY, lightOrigin.z);
        var sphereX1 = math.float3(lightOrigin.x + sphereX, planeY, lightOrigin.z);
        Debug.DrawLine(lightOrigin, sphereX0, Color.black);
        Debug.DrawLine(lightOrigin, sphereX1, Color.black);
    }
    public void Orth_Spot_Cone_PlaneY() 
    {
        var lightOrigin = Vector3.zero;
        //var range = 10f;
        var ray = new Vector3(1f, 1f, angle).normalized;
        var orientation = Quaternion.FromToRotation(Vector3.back, ray);
        float3 rayValue = new float3(ray);
        //DrawCircle(lightOrigin, range, Color.yellow, Panel.XY, Vector3.forward);

        var radius = 6f;
        var height = 8f;
        var origin = lightOrigin + ray * height;
        var originFloat3 = new float3(origin);
        Debug.DrawLine(lightOrigin, origin, Color.red);
        DrawCircle(origin, radius, Color.blue, Panel.XY, ray);
        Debug.DrawLine(lightOrigin, lightOrigin + ray * height + orientation * new Vector3(radius, 0, 0), Color.blue);
        Debug.DrawLine(lightOrigin, lightOrigin + ray * height + orientation * new Vector3(-radius, 0, 0), Color.blue);
        Debug.DrawLine(lightOrigin, lightOrigin + ray * height + orientation * new Vector3(0, radius, 0), Color.blue);
        Debug.DrawLine(lightOrigin, lightOrigin + ray * height + orientation * new Vector3(0, -radius, 0), Color.blue);

        var planeY = test;
        Debug.DrawLine(new Vector3(-100, planeY, 0), new Vector3(100, planeY, 0), Color.black);

        var sphereDistance = height + radius * radius / height;
        var sphereRadius = math.sqrt(square(radius * radius) / (height * height) + radius * radius);
        var directionXYSqInv = math.rcp(math.lengthsq(rayValue.xy));
        var polarIntersection = -radius * radius / height * directionXYSqInv * rayValue.xy;
        var polarDir = math.sqrt((square(sphereRadius) - math.lengthsq(polarIntersection)) * directionXYSqInv) * math.float2(rayValue.y, -rayValue.x);
        var conePBase = new float2(lightOrigin.x, lightOrigin.y) + sphereDistance * rayValue.xy + polarIntersection;
        var coneP0 = conePBase - polarDir;
        var coneP1 = conePBase + polarDir;
        Debug.DrawLine(new Vector3(coneP0.x, coneP0.y, 0f), new Vector3(conePBase.x, conePBase.y, 0f), Color.black);
        Debug.DrawLine(new Vector3(coneP1.x, coneP1.y, 0f), new Vector3(conePBase.x, conePBase.y, 0f), Color.black);
        Debug.DrawLine(new Vector3(coneP0.x, coneP0.y, 0f), lightOrigin, Color.black);
        Debug.DrawLine(new Vector3(coneP1.x, coneP1.y, 0f), lightOrigin, Color.black);
        Debug.DrawLine(lightOrigin + new Vector3((sphereDistance * rayValue.xy).x, (sphereDistance * rayValue.xy).y, 0), lightOrigin, Color.red);
        var coneDir0X = coneP0.x - lightOrigin.x;
        var coneDir0YInv = math.rcp(coneP0.y - lightOrigin.y);
        var coneDir1X = coneP1.x - lightOrigin.x;
        var coneDir1YInv = math.rcp(coneP1.y - lightOrigin.y);
        var deltaY = planeY - lightOrigin.y;
        var coneT0 = deltaY * coneDir0YInv;
        var coneT1 = deltaY * coneDir1YInv;
        if (coneT0 >= 0 && coneT0 <= 1)
            Debug.DrawLine(lightOrigin, new Vector3(lightOrigin.x + coneT0 * coneDir0X, planeY, 0f), Color.pink);
        if (coneT1 >= 0 && coneT1 <= 1)
            Debug.DrawLine(lightOrigin, new Vector3(lightOrigin.x + coneT1 * coneDir1X, planeY, 0f), Color.pink);
    }
    public void Orth_Spot_Circle_PlaneY() 
    {
        var lightOrigin = Vector3.zero;
        var range = 10f;
        var ray = new Vector3(1f, 1f, angle).normalized;
        var orientation = Quaternion.FromToRotation(Vector3.back, ray);
        float3 rayValue = new float3(ray);
        //DrawCircle(lightOrigin, range, Color.yellow, Panel.XY, Vector3.forward);

        var radius = 6f;
        var height = 8f;
        var origin = lightOrigin + ray * height;
        var originFloat3 = new float3(origin);
        Debug.DrawLine(lightOrigin, origin, Color.red);
        DrawCircle(origin, radius, Color.blue, Panel.XY, ray);
        Debug.DrawLine(lightOrigin, lightOrigin + ray * height + orientation * new Vector3(radius, 0, 0), Color.blue);
        Debug.DrawLine(lightOrigin, lightOrigin + ray * height + orientation * new Vector3(-radius, 0, 0), Color.blue);
        Debug.DrawLine(lightOrigin, lightOrigin + ray * height + orientation * new Vector3(0, radius, 0), Color.blue);
        Debug.DrawLine(lightOrigin, lightOrigin + ray * height + orientation * new Vector3(0, -radius, 0), Color.blue);

        var planeY = test;
        Debug.DrawLine(new Vector3(-100, planeY, 0), new Vector3(100, planeY, 0), Color.black);

        var circleRadius = math.sqrt(range * range - height * height);
        var circleUp = math.normalize(math.float3(0, 1, 0) - rayValue * rayValue.y);
        var circleRight = math.normalize(math.float3(1, 0, 0) - rayValue * rayValue.x);
        var circleBoundY0 = originFloat3 - circleUp * circleRadius;
        var circleBoundY1 = originFloat3 + circleUp * circleRadius;
        var circleBoundX0 = originFloat3 - circleRight * circleRadius;
        var circleBoundX1 = originFloat3 + circleRight * circleRadius;
        Debug.DrawLine(origin, circleBoundY0, Color.yellow);
        Debug.DrawLine(origin, circleBoundY1, Color.yellow);
        Debug.DrawLine(origin, circleBoundX0, Color.yellow);
        Debug.DrawLine(origin, circleBoundX1, Color.yellow);

        var intersectionDistance = (planeY - origin.y) / circleUp.y;
        var closestPointX = origin.x + intersectionDistance * circleUp.x;
        var intersectionDirX = -ray.z / math.length(math.float3(-ray.z, 0, ray.x));
        var sideDistance = math.sqrt(square(radius) - square(intersectionDistance));
        var circleX0 = closestPointX - sideDistance * intersectionDirX;
        var circleX1 = closestPointX + sideDistance * intersectionDirX;
        Debug.DrawLine(origin, new Vector3(circleX0, planeY, 0), Color.pink);
        Debug.DrawLine(origin, new Vector3(circleX1, planeY, 0), Color.pink);
    }

    public void Pers_Sphere() 
    {
        var lightOrigin = new Vector3(0, 0, 14f);
        var range = 10f;
        var ray = new Vector3(1f, 1f, angle).normalized;
        var orientation = Quaternion.FromToRotation(Vector3.back, ray);
        float3 rayValue = new float3(ray);
        DrawCircle(lightOrigin, range, Color.yellow, Panel.ZY, Vector3.forward);

        //var radius = 6f;
        //var height = 8f;
        //var origin = lightOrigin + ray * height;
        //var originFloat3 = new float3(origin);
        //Debug.DrawLine(lightOrigin, origin, Color.red);
        //DrawCircle(origin, radius, Color.blue, Panel.XY, ray);

        var cameraPosWS = Vector3.zero;
        var near = 5f;
        var rangesq = square(range);
        var sphereClipRadius = math.sqrt(rangesq - square(near - lightOrigin.z));

        GetSphereHorizon(new float2(lightOrigin.y, lightOrigin.z), range, near, sphereClipRadius, out var sphereBoundYZ0, out var sphereBoundYZ1);
        var sphereBoundY0 = math.float3(lightOrigin.x, sphereBoundYZ0);
        var sphereBoundY1 = math.float3(lightOrigin.x, sphereBoundYZ1);
        Debug.DrawLine(cameraPosWS, sphereBoundY0, Color.green);
        Debug.DrawLine(cameraPosWS, sphereBoundY1, Color.green);
    }
    public void Pers_Spot_Circle() 
    {
        var lightOrigin = new Vector3(0, 0, depth);
        var range = 10f;
        var ray = new Vector3(1f, 1f, angle).normalized;
        var orientation = Quaternion.FromToRotation(Vector3.back, ray);
        float3 rayValue = new float3(ray);
        //DrawCircle(lightOrigin, range, Color.yellow, Panel.ZY, Vector3.forward);

        var radius = 6f;
        var height = 8f;
        var origin = lightOrigin + ray * height;
        Debug.DrawLine(lightOrigin, origin, Color.red);
        DrawCircle(origin, radius, Color.blue, Panel.XY, ray);
        Debug.DrawLine(lightOrigin, lightOrigin + ray * height + orientation * new Vector3(radius, 0, 0), Color.blue);
        Debug.DrawLine(lightOrigin, lightOrigin + ray * height + orientation * new Vector3(-radius, 0, 0), Color.blue);
        Debug.DrawLine(lightOrigin, lightOrigin + ray * height + orientation * new Vector3(0, radius, 0), Color.blue);
        Debug.DrawLine(lightOrigin, lightOrigin + ray * height + orientation * new Vector3(0, -radius, 0), Color.blue);

        var cameraPosWS = Vector3.zero;
        var near = 5f;
        var rangesq = square(range);
        var sphereClipRadius = math.sqrt(rangesq - square(near - lightOrigin.z));

        var baseUY = math.abs(math.abs(rayValue.x) - 1) < 1e-6f ? math.float3(0, 1, 0) : math.normalize(math.cross(rayValue, math.float3(1, 0, 0)));
        var baseVY = math.cross(rayValue, baseUY);
        GetProjectedCircleHorizon(new float2(origin.y, origin.z), radius, baseUY.yz, baseVY.yz, out var baseY1UV, out var baseY2UV);
        var baseY1 = new float3(origin) + baseY1UV.x * baseUY + baseY1UV.y * baseVY;
        var baseY2 = new float3(origin) + baseY2UV.x * baseUY + baseY2UV.y * baseVY;
        Debug.DrawLine(origin, baseY2, Color.yellow);
        Debug.DrawLine(origin, baseY1, Color.yellow);
        Debug.DrawLine(cameraPosWS, baseY2, Color.yellow);
        Debug.DrawLine(cameraPosWS, baseY1, Color.yellow);
    }

    public void Pers_Spot_Circle_Clipping() 
    {
        var lightOrigin = new Vector3(0, 0, depth);
        var range = 10f;
        var ray = new Vector3(1f, 1f, angle).normalized;
        var orientation = Quaternion.FromToRotation(Vector3.back, ray);
        float3 rayValue = new float3(ray);
        //DrawCircle(lightOrigin, range, Color.yellow, Panel.ZY, Vector3.forward);

        var radius = 6f;
        var height = 8f;
        var origin = lightOrigin + ray * height;
        Debug.DrawLine(lightOrigin, origin, Color.red);
        DrawCircle(origin, radius, Color.blue, Panel.XY, ray);
        Debug.DrawLine(lightOrigin, lightOrigin + ray * height + orientation * new Vector3(radius, 0, 0), Color.blue);
        Debug.DrawLine(lightOrigin, lightOrigin + ray * height + orientation * new Vector3(-radius, 0, 0), Color.blue);
        Debug.DrawLine(lightOrigin, lightOrigin + ray * height + orientation * new Vector3(0, radius, 0), Color.blue);
        Debug.DrawLine(lightOrigin, lightOrigin + ray * height + orientation * new Vector3(0, -radius, 0), Color.blue);

        var cameraPosWS = Vector3.zero;
        var near = 5f;
        var rangesq = square(range);
        var sphereClipRadius = math.sqrt(rangesq - square(near - lightOrigin.z));

        var baseUY = math.abs(math.abs(rayValue.x) - 1) < 1e-6f ? math.float3(0, 1, 0) : math.normalize(math.cross(rayValue, math.float3(1, 0, 0)));
        var baseVY = math.cross(rayValue, baseUY);

        var baseRadius = math.sqrt(range * range - height * height);
        var baseCenter = lightOrigin + ray * height;
        // Handle base circle clipping by intersecting it with the near-plane if needed.
        if (GetCircleClipPoints(baseCenter, ray, baseRadius, near, out var baseClip0, out var baseClip1))
        {
            Debug.DrawLine(cameraPosWS, baseClip0, Color.red);
            Debug.DrawLine(cameraPosWS, baseClip1, Color.red);
        }
    }

    public void Pers_Spot_Cone_Clipping() 
    {
        var lightOrigin = new Vector3(0, 0, depth);
        var range = 10f;
        var ray = new Vector3(1f, 1f, angle).normalized;
        var orientation = Quaternion.FromToRotation(Vector3.back, ray);
        float3 rayValue = new float3(ray);
        //DrawCircle(lightOrigin, range, Color.yellow, Panel.ZY, Vector3.forward);

        var radius = 6f;
        var height = 8f;
        var origin = lightOrigin + ray * height;
        Debug.DrawLine(lightOrigin, origin, Color.red);
        DrawCircle(origin, radius, Color.blue, Panel.XY, ray);

        var cameraPosWS = Vector3.zero;
        var near = 4f;
        var rangesq = square(range);
        var sphereClipRadius = math.sqrt(rangesq - square(near - lightOrigin.z));

        var baseUY = math.abs(math.abs(rayValue.x) - 1) < 1e-6f ? math.float3(0, 1, 0) : math.normalize(math.cross(rayValue, math.float3(1, 0, 0)));
        var baseVY = math.cross(rayValue, baseUY);

        var baseRadius = math.sqrt(range * range - height * height);
        var baseCenter = lightOrigin + ray * height;

        // Calculate Z bounds of cone and check if it's overlapping with the near plane.
        // From https://www.iquilezles.org/www/articles/diskbbox/diskbbox.htm
        var baseExtentZ = baseRadius * math.sqrt(1.0f - square(rayValue.z));
        var coneIsClipping = near >= math.min(baseCenter.z - baseExtentZ, lightOrigin.z) && near <= math.max(baseCenter.z + baseExtentZ, lightOrigin.z);

        var coneU = math.normalize(math.cross(rayValue, lightOrigin));
        // The cross product will be the 0-vector if the light-direction and camera-to-light-position vectors are parallel.
        // In that case, {1, 0, 0} is orthogonal to the light direction and we use that instead.
        coneU = math.normalize(coneU); // math.csum(coneU) != 0f ? math.normalize(coneU) : math.float3(1, 0, 0);
        var coneV = math.cross(rayValue, coneU);

        Debug.DrawLine(origin, origin + new Vector3(coneU.x, coneU.y, coneU.z) * radius, Color.white);
        Debug.DrawLine(origin, origin + new Vector3(coneV.x, coneV.y, coneV.z) * radius, Color.white);

        Debug.DrawLine(lightOrigin, origin + new Vector3(coneU.x, coneU.y, coneU.z) * radius, Color.blue);
        Debug.DrawLine(lightOrigin, origin + new Vector3(coneV.x, coneV.y, coneV.z) * radius, Color.blue);
        Debug.DrawLine(lightOrigin, origin - new Vector3(coneU.x, coneU.y, coneU.z) * radius, Color.blue);
        Debug.DrawLine(lightOrigin, origin - new Vector3(coneV.x, coneV.y, coneV.z) * radius, Color.blue);

        if (coneIsClipping)
        {
            var r = baseRadius / height;

            // Find the Y bounds of the near-plane cone intersection, i.e. where y' = 0
            var thetaY = FindNearConicTangentTheta(new float2(lightOrigin.y, lightOrigin.z), rayValue.yz, r, coneU.yz, coneV.yz);
            //var p0Y = EvaluateNearConic(near, lightOrigin, rayValue, r, coneU, coneV, thetaY.x);
            var p1Y = EvaluateNearConic(near, lightOrigin, rayValue, r, coneU, coneV, thetaY.y);
            //Debug.DrawLine(lightOrigin, p0Y, Color.yellow);
            Debug.DrawLine(lightOrigin, p1Y, Color.yellow);

            var h = coneU * math.cos(thetaY.y) + coneV * math.sin(thetaY.y);
            var z = EvaluateNearConic(origin.z + h.z * radius, lightOrigin, rayValue, r, coneU, coneV, thetaY.y);
            Debug.DrawLine(origin, origin + new Vector3(h.x, h.y, h.z) * radius, Color.black);
            Debug.DrawLine(lightOrigin, z, Color.yellow);
            h = coneU * math.cos(thetaY.x) + coneV * math.sin(thetaY.x);
            z = EvaluateNearConic(origin.z + h.z * radius, lightOrigin, rayValue, r, coneU, coneV, thetaY.x);
            Debug.DrawLine(origin, origin + new Vector3(h.x, h.y, h.z) * radius, Color.black);
            Debug.DrawLine(lightOrigin, z, Color.yellow);
        }

        Debug.DrawLine(new Vector3(0, 100, near), new Vector3(0, -100, near), Color.green);
    }

    public void Pers_Spot_Cone_PlaneYMaxMin()
    {
        var lightOrigin = new Vector3(2, 2, depth);
        var range = 10f;
        var ray = new Vector3(1f, 1f, angle).normalized;
        var orientation = Quaternion.FromToRotation(Vector3.back, ray);
        float3 rayValue = new float3(ray);

        var radius = 6f;
        var height = 8f;
        var origin = lightOrigin + ray * height;
        Debug.DrawLine(lightOrigin, origin, Color.red);
        Debug.DrawLine(Vector3.zero, lightOrigin, Color.red);
        DrawCircle(origin, radius, Color.blue, Panel.XY, ray);

        var cameraPosWS = Vector3.zero;
        var near = 4f;
        var rangesq = square(range);
        var sphereClipRadius = math.sqrt(rangesq - square(near - lightOrigin.z));

        var baseUY = math.abs(math.abs(rayValue.x) - 1) < 1e-6f ? math.float3(0, 1, 0) : math.normalize(math.cross(rayValue, math.float3(1, 0, 0)));
        var baseVY = math.cross(rayValue, baseUY);

        var baseRadius = math.sqrt(range * range - height * height);
        var baseCenter = lightOrigin + ray * height;

        var halfAngle = math.atan(radius / height) * 0.5f;
        var cosHalfAngle = math.cos(halfAngle);

        // Calculate Z bounds of cone and check if it's overlapping with the near plane.
        // From https://www.iquilezles.org/www/articles/diskbbox/diskbbox.htm
        var baseExtentZ = baseRadius * math.sqrt(1.0f - square(rayValue.z));
        var coneIsClipping = near >= math.min(baseCenter.z - baseExtentZ, lightOrigin.z) && near <= math.max(baseCenter.z + baseExtentZ, lightOrigin.z);

        var coneU = math.normalize(math.cross(rayValue, lightOrigin));
        // The cross product will be the 0-vector if the light-direction and camera-to-light-position vectors are parallel.
        // In that case, {1, 0, 0} is orthogonal to the light direction and we use that instead.
        coneU = math.normalize(coneU); // math.csum(coneU) != 0f ? math.normalize(coneU) : math.float3(1, 0, 0);
        var coneV = math.cross(rayValue, coneU);
        Debug.DrawLine(origin, origin + new Vector3(coneU.x, coneU.y, coneU.z) * radius, Color.white);
        Debug.DrawLine(origin, origin + new Vector3(coneV.x, coneV.y, coneV.z) * radius, Color.white);

        // Calculate the lines making up the sides of the cone as seen from the camera. `l1` and `l2` form lines
        // from the light position.
        GetConeSideTangentPoints(lightOrigin, ray, cosHalfAngle, baseRadius, height, range, coneU, coneV, out var l1, out var l2);

        if ((l1.x != 0.0f) && (l1.y != 0.0f) && (l1.z != 0.0f))
        {
            var planeNormal = math.float3(0, 1, -0.75f);
            var l1t = math.dot(-lightOrigin, planeNormal) / math.dot(l1, planeNormal);
            Vector3 l1x = lightOrigin + new Vector3(l1.x, l1.y, l1.z) * l1t;
        }
    }

    public void Pers_Cone_Clipping_PlaneY()
    {
        var vertex = new Vector3[8];
        var lightOrigin = new Vector3(0, 0, depth);
        var range = 10f;
        var ray = new Vector3(1f, 1f, angle).normalized;
        var orientation = Quaternion.FromToRotation(Vector3.back, ray);
        float3 rayValue = new float3(ray);
        //DrawCircle(lightOrigin, range, Color.yellow, Panel.ZY, Vector3.forward);

        var radius = 6f;
        var height = 8f;
        var origin = lightOrigin + ray * height;
        Debug.DrawLine(lightOrigin, origin, Color.red);
        DrawCircle(origin, radius, Color.blue, Panel.XY, ray);

        var cameraPosWS = Vector3.zero;
        var near = 4f;
        var rangesq = square(range);
        var sphereClipRadius = math.sqrt(rangesq - square(near - lightOrigin.z));

        var baseUY = math.abs(math.abs(rayValue.x) - 1) < 1e-6f ? math.float3(0, 1, 0) : math.normalize(math.cross(rayValue, math.float3(1, 0, 0)));
        var baseVY = math.cross(rayValue, baseUY);

        var baseRadius = math.sqrt(range * range - height * height);
        var baseCenter = lightOrigin + ray * height;

        // Calculate Z bounds of cone and check if it's overlapping with the near plane.
        // From https://www.iquilezles.org/www/articles/diskbbox/diskbbox.htm
        var baseExtentZ = baseRadius * math.sqrt(1.0f - square(rayValue.z));
        var coneIsClipping = near >= math.min(baseCenter.z - baseExtentZ, lightOrigin.z) && near <= math.max(baseCenter.z + baseExtentZ, lightOrigin.z);

        var coneU = math.normalize(math.cross(rayValue, lightOrigin));
        // The cross product will be the 0-vector if the light-direction and camera-to-light-position vectors are parallel.
        // In that case, {1, 0, 0} is orthogonal to the light direction and we use that instead.
        coneU = math.normalize(coneU); // math.csum(coneU) != 0f ? math.normalize(coneU) : math.float3(1, 0, 0);
        var coneV = math.cross(rayValue, coneU);

        Debug.DrawLine(origin, origin + new Vector3(coneU.x, coneU.y, coneU.z) * radius, Color.white);
        Debug.DrawLine(origin, origin + new Vector3(coneV.x, coneV.y, coneV.z) * radius, Color.white);

        Debug.DrawLine(lightOrigin, origin + new Vector3(coneU.x, coneU.y, coneU.z) * radius, Color.blue);
        Debug.DrawLine(lightOrigin, origin + new Vector3(coneV.x, coneV.y, coneV.z) * radius, Color.blue);
        Debug.DrawLine(lightOrigin, origin - new Vector3(coneU.x, coneU.y, coneU.z) * radius, Color.blue);
        Debug.DrawLine(lightOrigin, origin - new Vector3(coneV.x, coneV.y, coneV.z) * radius, Color.blue);

        var planeY = 0.75f;
        if (coneIsClipping)
        {
            var y = planeY * near;
            var r = baseRadius / height;
            var theta = FindNearConicYTheta(near, lightOrigin, ray, r, coneU, coneV, y);
            var h = rayValue + r * coneU * math.cos(theta.x) + r * coneV * math.sin(theta.x);
            var test = coneU * math.cos(theta.x) + coneV * math.sin(theta.x);
            var p0 = math.float3(EvaluateNearConic(near, lightOrigin, ray, r, coneU, coneV, theta.x).x, y, near);
            var p1 = math.float3(EvaluateNearConic(near, lightOrigin, ray, r, coneU, coneV, theta.y).x, y, near);
            //Debug.DrawLine(origin, origin + radius * new Vector3(test.x, test.y, test.z), Color.black);
            //Debug.DrawLine(lightOrigin, lightOrigin + range * new Vector3(h.x, h.y, h.z) * (height / range), Color.pink);
            Debug.DrawLine(lightOrigin, new Vector3(p0.x, p0.y, p0.z), Color.yellow);
            Debug.DrawLine(lightOrigin, new Vector3(p1.x, p1.y, p1.z), Color.yellow);
        }

        Debug.DrawLine(new Vector3(0, 100, near), new Vector3(0, -100, near), Color.green);
        Debug.DrawLine(new Vector3(100, 3, near), new Vector3(-100, 3, near), Color.green);
        Debug.DrawLine(new Vector3(0, 3, 100), new Vector3(0, 3, -100), Color.green);
    }

    /// <summary>
    /// 画线圈
    /// </summary>
    /// <param name="position">位置</param>
    /// <param name="radius">半径</param>
    /// <param name="color">颜色</param>
    /// <param name="duration">持续时间</param>
    /// <param name="displayPanel">显示座标轴</param>
    /// <param name="detail">圆的线段数量 越小越多</param>
    public static void DrawCircle(Vector3 position, float radius, Color color, Panel displayPanel, Vector3 normal, float detail = 0.1f)
    {
        Vector3 lastPoint = Vector3.zero, currentPoint = Vector3.zero;
        var orientation = Quaternion.FromToRotation(Vector3.back, normal);
        for (float theta = 0; theta < 2 * Mathf.PI; theta += detail)
        {
            float x = radius * Mathf.Cos(theta);
            float z = radius * Mathf.Sin(theta);

            Vector3 endPoint = Vector3.zero;
            switch (displayPanel)
            {
                case Panel.XZ:
                    endPoint = orientation * new Vector3(x, 0, z) + position;
                    break;
                case Panel.XY:
                    endPoint = orientation * new Vector3(x, z, 0) + position;
                    break;
                case Panel.ZY:
                    endPoint = orientation * new Vector3(0, x, z) + position;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(displayPanel), displayPanel, null);
            }
            

            if (theta == 0)
            {
                lastPoint = endPoint;
            }
            else
            {
                Debug.DrawLine(currentPoint, endPoint, color);
            }


            currentPoint = endPoint;
        }


        Debug.DrawLine(lastPoint, currentPoint, color);
    }

    public static void DrawFrustum() 
    {
        var vertex = new Vector3[8];
        for (var i = 0; i < 4; i++)
        {
            // Convert index to x, y, and z in [-1, 1]
            var x = ((i << 1) & 2) - 1;
            var y = (i & 2) - 1;
            vertex[i] = new Vector3(x * 3, y * 3, 4);
        }
        Debug.DrawLine(vertex[0], vertex[1], Color.green);
        Debug.DrawLine(vertex[1], vertex[3], Color.green);
        Debug.DrawLine(vertex[3], vertex[2], Color.green);
        Debug.DrawLine(vertex[0], vertex[2], Color.green);

        for (var i = 4; i < 8; i++)
        {
            // Convert index to x, y, and z in [-1, 1]
            var x = ((i << 1) & 2) - 1;
            var y = (i & 2) - 1;
            vertex[i] = new Vector3(x * 30, y * 30, 40);
        }
        Debug.DrawLine(vertex[4], vertex[5], Color.green);
        Debug.DrawLine(vertex[5], vertex[7], Color.green);
        Debug.DrawLine(vertex[7], vertex[6], Color.green);
        Debug.DrawLine(vertex[4], vertex[6], Color.green);

        Debug.DrawLine(Vector3.zero, vertex[5], Color.green);
        Debug.DrawLine(Vector3.zero, vertex[7], Color.green);
        Debug.DrawLine(Vector3.zero, vertex[6], Color.green);
        Debug.DrawLine(Vector3.zero, vertex[4], Color.green);
    }
}
