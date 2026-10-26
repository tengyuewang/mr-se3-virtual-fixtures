using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class PathCoreTests
{
    [Test]
    public void Se3SplinePreservesEndpoints()
    {
        List<Pose> input = new List<Pose>
        {
            new Pose(Vector3.zero, Quaternion.identity),
            new Pose(new Vector3(0.05f, 0.03f, 0.01f), Quaternion.Euler(0f, 25f, 0f)),
            new Pose(new Vector3(0.1f, 0.02f, 0.04f), Quaternion.Euler(15f, 50f, 5f))
        };

        List<Pose> result = CentripetalCatmullRomSE3.FitTrajectory(input);

        Assert.That(Vector3.Distance(result[0].position, input[0].position), Is.LessThan(1e-5f));
        Assert.That(Quaternion.Angle(result[0].rotation, input[0].rotation), Is.LessThan(1e-3f));
        Assert.That(Vector3.Distance(result[result.Count - 1].position, input[input.Count - 1].position), Is.LessThan(1e-4f));
        Assert.That(Quaternion.Angle(result[result.Count - 1].rotation, input[input.Count - 1].rotation), Is.LessThan(1e-2f));
    }

    [Test]
    public void Se3SplineSamplesPureRotation()
    {
        List<Pose> input = new List<Pose>
        {
            new Pose(Vector3.zero, Quaternion.identity),
            new Pose(Vector3.zero, Quaternion.Euler(0f, 90f, 0f))
        };

        List<Pose> result = CentripetalCatmullRomSE3.FitTrajectory(input);

        Assert.That(result.Count, Is.GreaterThan(2));
        Assert.That(Quaternion.Angle(result[result.Count - 1].rotation, input[1].rotation), Is.LessThan(1e-2f));
    }

    [Test]
    public void LaplacianPositionEditingMovesHandleAndPinsEndpoints()
    {
        List<Pose> input = new List<Pose>();
        for (int i = 0; i < 5; i++)
            input.Add(new Pose(new Vector3(i * 0.05f, 0f, 0f), Quaternion.identity));

        Vector3[] result = LaplacianEditing.SolveShapePreservingPosition(
            input,
            new List<int> { 2 },
            new List<Pose> { new Pose(new Vector3(0.1f, 0.1f, 0f), Quaternion.identity) },
            lam: 500f,
            mu: 0.5f,
            pinEnds: true,
            endWeight: 1e4f,
            falloffSigma: 3f);

        Assert.That(result[2].y, Is.GreaterThan(0.08f));
        Assert.That(Vector3.Distance(result[0], input[0].position), Is.LessThan(0.005f));
        Assert.That(Vector3.Distance(result[4], input[4].position), Is.LessThan(0.005f));
    }
}
