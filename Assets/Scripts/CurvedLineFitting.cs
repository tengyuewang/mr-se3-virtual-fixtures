using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fits a centripetal Catmull-Rom trajectory in SE(3).
/// Positions and orientations are interpolated together in the tangent space of
/// the first pose, then mapped back to Unity poses with the SE(3) exponential.
/// </summary>
public static class CentripetalCatmullRomSE3
{
    private const float Epsilon = 1e-6f;

    public static List<Pose> FitTrajectory(
        IReadOnlyList<Pose> keyPoses,
        float spacing = 0.004f,
        float characteristicLength = 0.1f,
        bool removeDuplicatePoses = true)
    {
        if (keyPoses == null)
            throw new ArgumentNullException(nameof(keyPoses));
        if (spacing <= 0f)
            throw new ArgumentOutOfRangeException(nameof(spacing));
        if (characteristicLength <= 0f)
            throw new ArgumentOutOfRangeException(nameof(characteristicLength));

        List<Pose> poses = Sanitize(keyPoses, removeDuplicatePoses);
        if (poses.Count <= 1)
            return poses;

        MakeQuaternionSignsContinuous(poses);

        Pose reference = poses[0];
        Vector6[] tangentCoordinates = new Vector6[poses.Count];
        float[] knots = BuildKnots(poses, characteristicLength);

        for (int i = 0; i < poses.Count; i++)
            tangentCoordinates[i] = LogRelative(reference, poses[i]);

        Vector6[] derivatives = BuildDerivatives(tangentCoordinates, knots);
        List<Pose> result = new List<Pose>(poses.Count * 4);

        for (int segment = 0; segment < poses.Count - 1; segment++)
        {
            float positionDistance = Vector3.Distance(poses[segment].position, poses[segment + 1].position);
            float rotationDistance = QuaternionAngleRadians(poses[segment].rotation, poses[segment + 1].rotation);
            float rotationSpacing = spacing / characteristicLength;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(
                positionDistance / spacing,
                rotationDistance / rotationSpacing)));

            for (int step = segment == 0 ? 0 : 1; step <= steps; step++)
            {
                float u = step / (float)steps;
                Vector6 xi = EvaluateHermite(
                    tangentCoordinates[segment], tangentCoordinates[segment + 1],
                    derivatives[segment], derivatives[segment + 1],
                    knots[segment + 1] - knots[segment], u);
                result.Add(ExpRelative(reference, xi));
            }
        }

        return result;
    }

    private static List<Pose> Sanitize(IReadOnlyList<Pose> input, bool removeDuplicates)
    {
        List<Pose> result = new List<Pose>(input.Count);
        for (int i = 0; i < input.Count; i++)
        {
            Pose pose = input[i];
            if (!IsFinite(pose.position) || !IsFinite(pose.rotation))
                continue;

            pose.rotation = NormalizeSafe(pose.rotation);
            if (removeDuplicates && result.Count > 0)
            {
                Pose previous = result[result.Count - 1];
                if ((pose.position - previous.position).sqrMagnitude < 1e-10f &&
                    QuaternionAngleRadians(pose.rotation, previous.rotation) < 1e-5f)
                    continue;
            }

            result.Add(pose);
        }
        return result;
    }

    private static void MakeQuaternionSignsContinuous(List<Pose> poses)
    {
        for (int i = 1; i < poses.Count; i++)
        {
            Pose pose = poses[i];
            if (Quaternion.Dot(poses[i - 1].rotation, pose.rotation) < 0f)
                pose.rotation = new Quaternion(-pose.rotation.x, -pose.rotation.y, -pose.rotation.z, -pose.rotation.w);
            poses[i] = pose;
        }
    }

    private static float[] BuildKnots(IReadOnlyList<Pose> poses, float characteristicLength)
    {
        float[] knots = new float[poses.Count];
        for (int i = 1; i < poses.Count; i++)
        {
            float positionTerm = (poses[i].position - poses[i - 1].position).sqrMagnitude /
                                 (characteristicLength * characteristicLength);
            float angle = QuaternionAngleRadians(poses[i - 1].rotation, poses[i].rotation);
            float metricDistance = Mathf.Sqrt(positionTerm + angle * angle);
            float increment = Mathf.Sqrt(Mathf.Max(metricDistance, Epsilon));
            knots[i] = knots[i - 1] + increment;
        }
        return knots;
    }

    private static Vector6[] BuildDerivatives(Vector6[] values, float[] knots)
    {
        int count = values.Length;
        Vector6[] derivatives = new Vector6[count];
        if (count == 2)
        {
            Vector6 slope = (values[1] - values[0]) / (knots[1] - knots[0]);
            derivatives[0] = slope;
            derivatives[1] = slope;
            return derivatives;
        }

        derivatives[0] = (values[1] - values[0]) / (knots[1] - knots[0]);
        derivatives[count - 1] = (values[count - 1] - values[count - 2]) /
                                 (knots[count - 1] - knots[count - 2]);

        for (int i = 1; i < count - 1; i++)
        {
            float previousInterval = knots[i] - knots[i - 1];
            float nextInterval = knots[i + 1] - knots[i];
            float total = previousInterval + nextInterval;
            Vector6 previousSlope = (values[i] - values[i - 1]) / previousInterval;
            Vector6 nextSlope = (values[i + 1] - values[i]) / nextInterval;
            derivatives[i] = previousSlope * (nextInterval / total) +
                             nextSlope * (previousInterval / total);
        }

        return derivatives;
    }

    private static Vector6 EvaluateHermite(
        Vector6 p0, Vector6 p1, Vector6 d0, Vector6 d1, float interval, float u)
    {
        float u2 = u * u;
        float u3 = u2 * u;
        float h00 = 2f * u3 - 3f * u2 + 1f;
        float h10 = u3 - 2f * u2 + u;
        float h01 = -2f * u3 + 3f * u2;
        float h11 = u3 - u2;
        return p0 * h00 + d0 * (interval * h10) + p1 * h01 + d1 * (interval * h11);
    }

    private static Vector6 LogRelative(Pose reference, Pose pose)
    {
        Quaternion referenceInverse = Quaternion.Inverse(reference.rotation);
        Quaternion relativeRotation = NormalizeSafe(referenceInverse * pose.rotation);
        Vector3 omega = LogRotation(relativeRotation);
        Vector3 localTranslation = referenceInverse * (pose.position - reference.position);
        Vector3 velocity = ApplyInverseLeftJacobian(omega, localTranslation);
        return new Vector6(velocity, omega);
    }

    private static Pose ExpRelative(Pose reference, Vector6 xi)
    {
        Quaternion relativeRotation = ExpRotation(xi.angular);
        Vector3 localTranslation = ApplyLeftJacobian(xi.angular, xi.linear);
        return new Pose(
            reference.position + reference.rotation * localTranslation,
            NormalizeSafe(reference.rotation * relativeRotation));
    }

    private static Vector3 LogRotation(Quaternion rotation)
    {
        rotation = NormalizeSafe(rotation);
        if (rotation.w < 0f)
            rotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);

        Vector3 imaginary = new Vector3(rotation.x, rotation.y, rotation.z);
        float sinHalfAngle = imaginary.magnitude;
        if (sinHalfAngle < Epsilon)
            return 2f * imaginary;

        float angle = 2f * Mathf.Atan2(sinHalfAngle, Mathf.Clamp(rotation.w, -1f, 1f));
        return imaginary * (angle / sinHalfAngle);
    }

    private static Quaternion ExpRotation(Vector3 omega)
    {
        float angle = omega.magnitude;
        if (angle < Epsilon)
            return NormalizeSafe(new Quaternion(0.5f * omega.x, 0.5f * omega.y, 0.5f * omega.z, 1f));

        float scale = Mathf.Sin(0.5f * angle) / angle;
        return NormalizeSafe(new Quaternion(
            omega.x * scale,
            omega.y * scale,
            omega.z * scale,
            Mathf.Cos(0.5f * angle)));
    }

    private static Vector3 ApplyLeftJacobian(Vector3 omega, Vector3 value)
    {
        float theta = omega.magnitude;
        Vector3 firstCross = Vector3.Cross(omega, value);
        Vector3 secondCross = Vector3.Cross(omega, firstCross);
        if (theta < 1e-4f)
            return value + 0.5f * firstCross + (1f / 6f) * secondCross;

        float theta2 = theta * theta;
        float a = (1f - Mathf.Cos(theta)) / theta2;
        float b = (theta - Mathf.Sin(theta)) / (theta2 * theta);
        return value + a * firstCross + b * secondCross;
    }

    private static Vector3 ApplyInverseLeftJacobian(Vector3 omega, Vector3 value)
    {
        float theta = omega.magnitude;
        Vector3 firstCross = Vector3.Cross(omega, value);
        Vector3 secondCross = Vector3.Cross(omega, firstCross);
        if (theta < 1e-4f)
            return value - 0.5f * firstCross + (1f / 12f) * secondCross;

        float theta2 = theta * theta;
        float halfAngle = 0.5f * theta;
        float cotangentHalf = Mathf.Cos(halfAngle) / Mathf.Max(Mathf.Sin(halfAngle), Epsilon);
        float coefficient = (1f - halfAngle * cotangentHalf) / theta2;
        return value - 0.5f * firstCross + coefficient * secondCross;
    }

    private static float QuaternionAngleRadians(Quaternion a, Quaternion b)
    {
        float dot = Mathf.Clamp(Mathf.Abs(Quaternion.Dot(NormalizeSafe(a), NormalizeSafe(b))), 0f, 1f);
        return 2f * Mathf.Acos(dot);
    }

    private static Quaternion NormalizeSafe(Quaternion value)
    {
        float magnitude = Mathf.Sqrt(value.x * value.x + value.y * value.y +
                                    value.z * value.z + value.w * value.w);
        if (magnitude < Epsilon)
            return Quaternion.identity;
        float inverse = 1f / magnitude;
        return new Quaternion(value.x * inverse, value.y * inverse, value.z * inverse, value.w * inverse);
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsFinite(Quaternion value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private struct Vector6
    {
        public readonly Vector3 linear;
        public readonly Vector3 angular;

        public Vector6(Vector3 linear, Vector3 angular)
        {
            this.linear = linear;
            this.angular = angular;
        }

        public static Vector6 operator +(Vector6 a, Vector6 b)
            => new Vector6(a.linear + b.linear, a.angular + b.angular);

        public static Vector6 operator -(Vector6 a, Vector6 b)
            => new Vector6(a.linear - b.linear, a.angular - b.angular);

        public static Vector6 operator *(Vector6 value, float scalar)
            => new Vector6(value.linear * scalar, value.angular * scalar);

        public static Vector6 operator /(Vector6 value, float scalar)
            => new Vector6(value.linear / scalar, value.angular / scalar);
    }
}
