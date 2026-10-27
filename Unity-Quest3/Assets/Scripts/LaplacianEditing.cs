using System;
using System.Collections.Generic;
using MathNet.Numerics.LinearAlgebra;
using UnityEngine;

public class LaplacianEditing 
{
    public static Pose[] SolveShapePreservingSE3(
        List<Pose> allPoses, List<int> handles, List<Pose> targetPoses,
        Vector3[] prevOmegaAll, Vector3[] prevOmegaAtHandles,
        out Vector3[] newPrevOmegaAll, out Vector3[] newPrevOmegaAtHandles,
        float lam = 100.0f, float mu = 1e-3f, bool pinEnds = false,
        float endWeight = 1e3f, float? falloffSigma = null)
    {
        int n = allPoses.Count;

        // 1) 连续化的 X0 和参考 g0
        Matrix<double> g0;
        var X0 = GetXisContinuous(allPoses, prevOmegaAll, out newPrevOmegaAll, out g0);

        // 2) L 系、右端项
        var L = BuildOpenPolylineLaplacian(n);
        var LtL = L.TransposeThisAndMultiply(L);
        var delta = L * X0;

        int k = handles.Count;
        var C = Matrix<double>.Build.Dense(k, n);
        for (int r = 0; r < k; r++) C[r, handles[r]] = 1.0;
        var CtC = C.TransposeThisAndMultiply(C);

        // 3) 连续化的目标 CtT（需要句柄的 prevOmega）
        if (prevOmegaAtHandles == null || prevOmegaAtHandles.Length != k)
            prevOmegaAtHandles = new Vector3[k]; // 初次全0
        var CtT = C.TransposeThisAndMultiply(
            BuildTargetsXiContinuous(allPoses, handles, targetPoses, prevOmegaAtHandles, g0)
        );

        // 4) 正则项：确保 mu 项回到 X0（而不是 0）
        Matrix<double> WtW, WtWX0;
        if (mu > 0f && falloffSigma.HasValue && k > 0)
        {
            var geod = Vector<double>.Build.Dense(n, i =>
            {
                double best = double.PositiveInfinity;
                for (int j = 0; j < k; j++)
                    best = Math.Min(best, Math.Abs(i - handles[j]));
                return best;
            });
            double sigma = falloffSigma.Value;
            var w = (geod.PointwisePower(2.0) / (-(2.0 * sigma * sigma))).PointwiseExp();
            var oneMinusW = w.Map(val => 1.0 - val);
            var W = Matrix<double>.Build.DiagonalOfDiagonalVector(oneMinusW);
            WtW = W.TransposeThisAndMultiply(W);
            WtWX0 = WtW * X0;
        }
        else
        {
            WtW = Matrix<double>.Build.DenseIdentity(n);
            WtWX0 = X0; // ← 关键修正：回到 X0，而不是零
        } 

        // 5) 组装并求解
        var A = LtL + lam * CtC + mu * WtW;
        var b = (L.TransposeThisAndMultiply(delta)) + (lam * CtT) + (mu * WtWX0);

        if (pinEnds && n >= 2)
        {
            A[0, 0] += endWeight; A[n - 1, n - 1] += endWeight;
            b.SetRow(0, b.Row(0) + endWeight * X0.Row(0));
            b.SetRow(n - 1, b.Row(n - 1) + endWeight * X0.Row(n - 1));
        }

        Matrix<double> X;
        try { X = A.Solve(b); }
        catch { X = A.LU().Solve(b); }

        // 6) 更新句柄的 prevOmega（把这次“目标”的 ω 记下来用于下一帧）
        newPrevOmegaAtHandles = new Vector3[k];
        for (int r = 0; r < k; r++)
        {
            var xi_hat_row = (BuildTargetsXiContinuous(allPoses, handles, targetPoses, prevOmegaAtHandles, g0)).Row(r);
            newPrevOmegaAtHandles[r] = new Vector3((float)xi_hat_row[0], (float)xi_hat_row[1], (float)xi_hat_row[2]);
        }

        // 7) 回 SE(3)
        var poses = GetPoses(X, g0);
        return poses.ToArray();
    }

    public static Vector3[] SolveShapePreservingPosition(
        List<Pose> allPoses, List<int> handles, List<Pose> targets,
        float lam = 100.0f, float mu = 1e-3f, bool pinEnds = false,
        float endWeight = 1e3f, float? falloffSigma = null)
    {
        var X0 = ToMatrix(allPoses);

        int n = allPoses.Count;
        var L = BuildOpenPolylineLaplacian(n);

        var delta = L * X0;
        var LtL = L.TransposeThisAndMultiply(L);

        int k = handles.Count;
        var C = Matrix<double>.Build.Dense(k, n);
        for (int r = 0; r < k; r++) C[r, handles[r]] = 1.0;

        var CtC = C.TransposeThisAndMultiply(C);
        var targetMatrix = ToMatrix(targets);
        var CtT = C.TransposeThisAndMultiply(targetMatrix);

        var WtW = Matrix<double>.Build.DenseIdentity(n);
        var WtWX0 = Matrix<double>.Build.Dense(n, 3);

        if (mu > 0)
        {
            if (falloffSigma.HasValue && k > 0)
            {
                // 1) 逐元素计算 geod[i] = min_j |i - handles[j]|
                var geod = Vector<double>.Build.Dense(n, i =>
                {
                    double best = double.PositiveInfinity;
                    for (int j = 0; j < handles.Count; j++)
                    {
                        double d = Math.Abs(i - handles[j]);
                        if (d < best) best = d;
                    }
                    return best;
                });

                // 2) w = exp( - geod^2 / (2*sigma^2) )
                double sigma = falloffSigma.Value;
                var w = (geod.PointwisePower(2.0) / (-(2.0 * sigma * sigma))).PointwiseExp();

                // 3) W = diag(1 - w)   （注意这里需要 Vector，而不是 Matrix）
                var oneMinusW = w.Map(val => 1.0 - val); // Vector<double>
                var W = Matrix<double>.Build.DiagonalOfDiagonalVector(oneMinusW);

                WtW = W.TransposeThisAndMultiply(W);
                WtWX0 = WtW * X0;
            }
            else
            {
                WtW = Matrix<double>.Build.DenseIdentity(n);
                WtWX0 = X0; // ← 关键修正：回到 X0，而不是零
            }
        }

        var A = LtL + lam * CtC + mu * WtW;
        var b = (L.TransposeThisAndMultiply(delta)) + (lam * CtT) + (mu * WtWX0);

        // 固定端点
        if (pinEnds && n >= 2)
        {
            A[0, 0] += endWeight;
            A[n - 1, n - 1] += endWeight;
            var firstRow = X0.Row(0);
            var lastRow = X0.Row(n - 1);
            b.SetRow(0, b.Row(0) + endWeight * firstRow);
            b.SetRow(n - 1, b.Row(n - 1) + endWeight * lastRow);
        }

        // 解线性方程组
        Matrix<double> X;
        try
        {
            X = A.Solve(b);
        }
        catch (Exception)
        {
            var lu = A.LU();
            X = lu.Solve(b);
        }

        Vector3[] newPositions = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            newPositions[i] = new Vector3((float)X[i, 0], (float)X[i, 1], (float)X[i, 2]);
        }

        return newPositions;

    }

    public static Quaternion[] SolveShapePreservingRotation(
        List<Pose> allPoses, List<int> handles, List<Pose> targetPoses,
        Vector3[] prevOmegaAll, Vector3[] prevOmegaAtHandles,
        out Vector3[] newPrevOmegaAll, out Vector3[] newPrevOmegaAtHandles,
        float lam = 100.0f, float mu = 1e-3f, bool pinEnds = false,
        float endWeight = 1e3f, float? falloffSigma = null)
    {
        int n = allPoses.Count;

        // 1) 连续化的 X0 和参考 g0
        Matrix<double> g0;
        var X0_full = GetXisContinuous(allPoses, prevOmegaAll, out newPrevOmegaAll, out g0);

        var X0 = X0_full.SubMatrix(0, X0_full.RowCount, 0, 3); 

        // 2) L 系、右端项
        var L = BuildOpenPolylineLaplacian(n);
        var LtL = L.TransposeThisAndMultiply(L);
        var delta = L * X0;

        int k = handles.Count;
        var C = Matrix<double>.Build.Dense(k, n);
        for (int r = 0; r < k; r++) C[r, handles[r]] = 1.0;
        var CtC = C.TransposeThisAndMultiply(C);

        // 3) 连续化的目标 CtT（需要句柄的 prevOmega）
        if (prevOmegaAtHandles == null || prevOmegaAtHandles.Length != k)
            prevOmegaAtHandles = new Vector3[k]; // 初次全0 

        var T = BuildTargetsXiContinuous(allPoses, handles, targetPoses, prevOmegaAtHandles, g0);
        T = T.SubMatrix(0, T.RowCount, 0, 3);

        var CtT = C.TransposeThisAndMultiply(T);

        // 4) 正则项：确保 mu 项回到 X0（而不是 0）
        Matrix<double> WtW, WtWX0;
        if (mu > 0f && falloffSigma.HasValue && k > 0)
        {
            var geod = Vector<double>.Build.Dense(n, i =>
            {
                double best = double.PositiveInfinity;
                for (int j = 0; j < k; j++)
                    best = Math.Min(best, Math.Abs(i - handles[j]));
                return best;
            });
            double sigma = falloffSigma.Value;
            var w = (geod.PointwisePower(2.0) / (-(2.0 * sigma * sigma))).PointwiseExp();
            var oneMinusW = w.Map(val => 1.0 - val);
            var W = Matrix<double>.Build.DiagonalOfDiagonalVector(oneMinusW);
            WtW = W.TransposeThisAndMultiply(W);
            WtWX0 = WtW * X0;
        }
        else
        {
            WtW = Matrix<double>.Build.DenseIdentity(n);
            WtWX0 = X0; // ← 关键修正：回到 X0，而不是零
        }

        // 5) 组装并求解
        var A = LtL + lam * CtC + mu * WtW;
        var b = (L.TransposeThisAndMultiply(delta)) + (lam * CtT) + (mu * WtWX0);

        if (pinEnds && n >= 2)
        {
            A[0, 0] += endWeight; A[n - 1, n - 1] += endWeight;
            b.SetRow(0, b.Row(0) + endWeight * X0.Row(0));
            b.SetRow(n - 1, b.Row(n - 1) + endWeight * X0.Row(n - 1));
        }

        Matrix<double> X;
        try { X = A.Solve(b); }
        catch { X = A.LU().Solve(b); }

        // 6) 更新句柄的 prevOmega（把这次“目标”的 ω 记下来用于下一帧）
        newPrevOmegaAtHandles = new Vector3[k];
        for (int r = 0; r < k; r++)
        {
            var xi_hat_row = (BuildTargetsXiContinuous(allPoses, handles, targetPoses, prevOmegaAtHandles, g0)).Row(r);
            newPrevOmegaAtHandles[r] = new Vector3((float)xi_hat_row[0], (float)xi_hat_row[1], (float)xi_hat_row[2]);
        }

        // 7) 回 SO(3)
        Quaternion[] newRotations = new Quaternion[n];

        Matrix<double> g0_R = g0.SubMatrix(0, 3, 0, 3); // 提取3×3旋转子矩阵 

        for (int i = 0; i < n; i++)
        {
            var w = new Vector3((float)X[i, 0], (float)X[i, 1], (float)X[i, 2]);

            var R_rel = SE3.ExpSO3(w);   // ExpSO3 给出的相对旋转（局部）

            // 世界空间旋转 = g0 的旋转 * 当前的局部旋转
            var R_world = g0_R * R_rel;

            // 转换成Pose/Quaternion
            var tempT = SE3.Compose(R_world, Vector3.zero);
            newRotations[i] = SE3.MatrixToPose(tempT).rotation;
        }

        return newRotations;
    }

    // 构建开放多边形的拉普拉斯矩阵
    public static Matrix<double> BuildOpenPolylineLaplacian(int n)
    {
        var L = Matrix<double>.Build.Dense(n, n);
        if (n == 1)
        {
            L[0, 0] = 0.0;
            return L;
        }

        L[0, 0] = -1.0;
        L[0, 1] = 1.0;
        L[n - 1, n - 2] = 1.0;
        L[n - 1, n - 1] = -1.0;

        for (int i = 1; i < n - 1; i++)
        {
            L[i, i - 1] = 1.0;
            L[i, i] = -2.0;
            L[i, i + 1] = 1.0;
        }
        return L;
    }

    // 将 Vector3 数组转换为 MathNet 的 Matrix<double>
    public static Matrix<double> ToMatrix(List<Pose> allPoses)
    {
        int n = allPoses.Count;
        var matrix = Matrix<double>.Build.Dense(n, 3);
        for (int i = 0; i < n; i++)
        {
            matrix[i, 0] = allPoses[i].position.x;
            matrix[i, 1] = allPoses[i].position.y;
            matrix[i, 2] = allPoses[i].position.z;
        }
        return matrix;
    }

    // 计算所有点的 ξ（连续化版），并返回更新后的 prevOmega
    static Matrix<double> GetXisContinuous(
        List<Pose> allPoses,
        Vector3[] prevOmegaAll,
        out Vector3[] newPrevOmegaAll,
        out Matrix<double> g0)
    {
        int n = allPoses.Count;
        if (prevOmegaAll == null || prevOmegaAll.Length != n)
            prevOmegaAll = new Vector3[n]; // 初次全0

        var xis = Matrix<double>.Build.Dense(n, 6);
        g0 = SE3.PoseToMatrix(allPoses[0]);
        var g0Inv = SE3.InvSE3(g0);

        newPrevOmegaAll = new Vector3[n];

        for (int i = 0; i < n; i++)
        {
            var gi = SE3.PoseToMatrix(allPoses[i]);
            var rel = g0Inv * gi;
            var xi = SE3.LogSE3_Continuous(rel, prevOmegaAll[i]);
            xis.SetRow(i, xi);

            // 记录本次得到的 ω 作为下帧的 prev
            newPrevOmegaAll[i] = new Vector3((float)xi[0], (float)xi[1], (float)xi[2]);
        }
        return xis;
    }

    // 仅对句柄目标构造 ξ（连续化），需要传入每个句柄的 prevOmega（与 handles 对应）
    static Matrix<double> BuildTargetsXiContinuous(
        List<Pose> allPoses,
        List<int> handles,
        List<Pose> targetPoses,
        Vector3[] prevOmegaAtHandles,
        Matrix<double> g0)
    {
        if (handles.Count != targetPoses.Count)
            throw new ArgumentException("handles.Count must equal targetPoses.Count");
        if (prevOmegaAtHandles == null || prevOmegaAtHandles.Length != handles.Count)
            prevOmegaAtHandles = new Vector3[handles.Count];

        var g0Inv = SE3.InvSE3(g0);
        int k = handles.Count;
        var T = Matrix<double>.Build.Dense(k, 6);

        for (int r = 0; r < k; r++)
        {
            var gi_hat = SE3.PoseToMatrix(targetPoses[r]);
            var rel = g0Inv * gi_hat;
            var xi_hat = SE3.LogSE3_Continuous(rel, prevOmegaAtHandles[r]);
            T.SetRow(r, xi_hat);
            // 注意：prevOmegaAtHandles 的更新由调用方负责
        }
        return T;
    }

    static List<Pose> GetPoses(Matrix<double> xisStar, Matrix<double> g0)
    {
        List<Pose> newPoses = new List<Pose>();
        for (int i = 0; i < xisStar.RowCount; i++)
        {
            var xi = xisStar.Row(i);
            var m = g0 * SE3.ExpSE3(xi);
            newPoses.Add(SE3.MatrixToPose(m));
        }
        return newPoses;
    }

}


public static class SE3
{
    public static Matrix<double> Hat3(Vector3 w)
    {
        return Matrix<double>.Build.DenseOfArray(new double[,]
        {
            { 0.0,   -w.z,   w.y },
            {  w.z,   0.0,  -w.x },
            { -w.y,   w.x,   0.0 }
        });
    }

    static Vector3 Vee3(Matrix<double> W)
    {
        return new Vector3(
            (float)W[2, 1],
            (float)W[0, 2],
            (float)W[1, 0]
        );
    }

    public static Matrix<double> ExpSO3(Vector3 w)
    {
        double th = w.magnitude;
        if (th < 1e-12)
        {
            var W = Hat3(w);
            return Matrix<double>.Build.DenseIdentity(3) + W + W * W * 0.5;
        }
        var Wm = Hat3(w);
        double s = Math.Sin(th) / th;
        double c = (1 - Math.Cos(th)) / (th * th);
        return Matrix<double>.Build.DenseIdentity(3) + Wm * s + (Wm * Wm) * c;
    }

    public static Vector3 LogSO3(Matrix<double> R)
    {
        double tr = ((R.Trace() - 1.0) * 0.5);
        tr = Math.Max(-1.0, Math.Min(1.0, tr));
        double th = Math.Acos(tr);
        if (th < 1e-12) return Vector3.zero;
        var W = (R - R.Transpose()) / (2.0 * Math.Sin(th));
        return (float)th * Vee3(W);
    }

    public static Matrix<double> LeftJ_SO3(Vector3 w)
    {
        double th = w.magnitude;
        var Wm = Hat3(w);

        if (th < 1e-8)
        {
            // series: I + 1/2 W + 1/6 W^2
            return Matrix<double>.Build.DenseIdentity(3) + Wm * 0.5 + (Wm * Wm) * (1.0 / 6.0);
        }

        double th2 = th * th;
        double A = (1.0 - Math.Cos(th)) / th2;      // (1 - cosθ)/θ²
        double B = (th - Math.Sin(th)) / (th2 * th); // (θ - sinθ)/θ³

        return Matrix<double>.Build.DenseIdentity(3) + Wm * A + (Wm * Wm) * B;
    }

    public static Matrix<double> LeftJInv_SO3(Vector3 w)
    {
        double th = w.magnitude;
        if (th < 1e-8)
        {
            var W = Hat3(w);
            return Matrix<double>.Build.DenseIdentity(3) - W * 0.5 + (W * W) * (1.0 / 12.0);
        }
        var Wm = Hat3(w);
        double half = 0.5 * th;
        double A = half / Math.Tan(half);  // 0.5*th*cot(0.5 th)
        return Matrix<double>.Build.DenseIdentity(3) - Wm * 0.5 + (Wm * Wm) * ((1.0 - A) / (th * th));
    }

    // Pose ↔ (R,t)
    public static void Decompose(Matrix<double> T, out Matrix<double> R, out Vector3 t)
    {
        R = T.SubMatrix(0, 3, 0, 3);
        t = new Vector3((float)T[0, 3], (float)T[1, 3], (float)T[2, 3]);
    }

    public static Matrix<double> Compose(Matrix<double> R, Vector3 t)
    {
        var T = Matrix<double>.Build.DenseIdentity(4);
        T.SetSubMatrix(0, 0, R);
        T[0, 3] = t.x; T[1, 3] = t.y; T[2, 3] = t.z;
        return T;
    }

    public static Matrix<double> InvSE3(Matrix<double> T)
    {
        Decompose(T, out var R, out var t);
        var Rt = R.Transpose();
        var Ti = Matrix<double>.Build.DenseIdentity(4);
        Ti.SetSubMatrix(0, 0, Rt);
        var nt = -(Rt * Vector<double>.Build.DenseOfArray(new double[] { t.x, t.y, t.z }));
        Ti[0, 3] = nt[0]; Ti[1, 3] = nt[1]; Ti[2, 3] = nt[2];
        return Ti;
    }

    public static Matrix<double> ExpSE3(Vector<double> xi)
    {
        var w = new Vector3((float)xi[0], (float)xi[1], (float)xi[2]);
        var v = Vector<double>.Build.DenseOfArray(new double[] { xi[3], xi[4], xi[5] });
        var R = ExpSO3(w);
        var J = LeftJ_SO3(w);
        var t = J * v;
        return Compose(R, new Vector3((float)t[0], (float)t[1], (float)t[2]));
    }

    public static Vector<double> LogSE3(Matrix<double> T)
    {
        Decompose(T, out var R, out var t);
        var w = LogSO3(R);
        var Jinv = LeftJInv_SO3(w);
        var v = Jinv * Vector<double>.Build.DenseOfArray(new double[] { t.x, t.y, t.z });
        var xi = Vector<double>.Build.Dense(6, 0.0);
        xi[0] = w.x; xi[1] = w.y; xi[2] = w.z;
        xi[3] = v[0]; xi[4] = v[1]; xi[5] = v[2];
        return xi;
    }

    public static Matrix<double> PoseToMatrix(Pose p)
    {
        // 由 Pose 构造 4x4 SE3 变换矩阵
        Quaternion q = p.rotation;
        Vector3 t = p.position;

        // Unity 的 Matrix4x4.rotation 接口可以帮你创建旋转矩阵
        Matrix4x4 u = Matrix4x4.TRS(t, q, Vector3.one);  // scale = 1

        return Matrix<double>.Build.DenseOfArray(new double[,]
        {
        { u[0,0], u[0,1], u[0,2], u[0,3] },
        { u[1,0], u[1,1], u[1,2], u[1,3] },
        { u[2,0], u[2,1], u[2,2], u[2,3] },
        { u[3,0], u[3,1], u[3,2], u[3,3] }
        });
    }

    public static Pose MatrixToPose(Matrix<double> m)
    {
        // --- 1. 读取位移 ---
        Vector3 position = new Vector3(
            (float)m[0, 3],
            (float)m[1, 3],
            (float)m[2, 3]
        );

        // --- 2. 读取旋转子矩阵（3×3）---
        Vector3 right = new Vector3(
            (float)m[0, 0],
            (float)m[1, 0],
            (float)m[2, 0]
        );
        Vector3 up = new Vector3(
            (float)m[0, 1],
            (float)m[1, 1],
            (float)m[2, 1]
        );
        Vector3 forward = new Vector3(
            (float)m[0, 2],
            (float)m[1, 2],
            (float)m[2, 2]
        );

        // --- 3. 去掉数值缩放误差并正交化（三步稳定版）---
        right = right.normalized;

        // 保证 up 与 right 正交
        up = (up - Vector3.Dot(up, right) * right).normalized;

        // 用右手坐标系重建 forward（用 cross 更稳）
        forward = Vector3.Cross(right, up).normalized;

        // --- 4. 构造四元数 ---
        Quaternion rotation = Quaternion.LookRotation(forward, up);

        return new Pose(position, rotation);
    }

    // ========= 角速度连续化（unwrap）工具 =========
    public static Vector3 UnwrapOmega(Vector3 omega, Vector3 prev)
    {
        // 以当前角轴为方向，在 {ω + 2πk u} 中找与 prev 最近的
        Vector3 u = omega.sqrMagnitude < 1e-16 ? new Vector3(1, 0, 0) : omega.normalized;
        Vector3 best = omega;
        float bestDist = (best - prev).sqrMagnitude;

        Vector3 candP = omega + 2f * Mathf.PI * u;
        float dP = (candP - prev).sqrMagnitude;
        if (dP < bestDist) { best = candP; bestDist = dP; }

        Vector3 candM = omega - 2f * Mathf.PI * u;
        float dM = (candM - prev).sqrMagnitude;
        if (dM < bestDist) { best = candM; /*bestDist = dM;*/ }

        return best;
    }

    // 用四元数实现的 LogSO3 + 连续化（比 trace 版更稳）
    public static Vector3 LogSO3_Continuous(Matrix<double> R, Vector3 prevOmega)
    {
        // 把 R 塞进 4x4 再用你已有的 MatrixToPose 拿到 Quaternion
        var T = Compose(R, Vector3.zero);
        Quaternion q = MatrixToPose(T).rotation;

        // 统一四元数符号，避免 w 穿零产生翻转
        if (q.w < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);

        Vector3 n = new Vector3(q.x, q.y, q.z);
        double s = Math.Sqrt(n.x * n.x + n.y * n.y + n.z * n.z);
        double theta = 2.0 * Math.Atan2(s, Math.Abs(q.w));

        Vector3 u = (s < 1e-12) ? new Vector3(1, 0, 0) : (n / (float)s);
        Vector3 omega = (float)theta * u;

        return UnwrapOmega(omega, prevOmega);
    }

    public static Vector<double> LogSE3_Continuous(Matrix<double> T, Vector3 prevOmega)
    {
        Decompose(T, out var R, out var t);
        var w = LogSO3_Continuous(R, prevOmega);
        var Jinv = LeftJInv_SO3(w);
        var v = Jinv * Vector<double>.Build.DenseOfArray(new double[] { t.x, t.y, t.z });
        var xi = Vector<double>.Build.Dense(6);
        xi[0] = w.x; xi[1] = w.y; xi[2] = w.z;
        xi[3] = v[0]; xi[4] = v[1]; xi[5] = v[2];
        return xi;
    }
}
