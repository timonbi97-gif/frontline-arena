using System.Collections.Generic;
using UnityEngine;

// Grid A* over World's navigation grid, with greedy line-of-sight smoothing.
public static class Pathfinder
{
    const int N = World.N;
    static readonly float[] g = new float[N * N];
    static readonly int[] came = new int[N * N];
    static readonly int[] seen = new int[N * N];
    static readonly int[] closed = new int[N * N];
    static int stamp;
    static int[] hn = new int[4096];
    static float[] hf = new float[4096];
    static int hc;
    static readonly int[] dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
    static readonly int[] dz = { 0, 0, 1, -1, 1, -1, 1, -1 };
    static readonly List<int> tmp = new List<int>();

    public static int budget;

    public static bool TryUse()
    {
        if (budget <= 0) return false;
        budget--;
        return true;
    }

    static void Push(int n, float f)
    {
        if (hc == hn.Length)
        {
            System.Array.Resize(ref hn, hc * 2);
            System.Array.Resize(ref hf, hc * 2);
        }
        int i = hc++;
        while (i > 0)
        {
            int p = (i - 1) >> 1;
            if (hf[p] <= f) break;
            hn[i] = hn[p]; hf[i] = hf[p]; i = p;
        }
        hn[i] = n; hf[i] = f;
    }

    static int Pop()
    {
        int top = hn[0];
        hc--;
        if (hc > 0)
        {
            int n = hn[hc]; float f = hf[hc]; int i = 0;
            while (true)
            {
                int c = 2 * i + 1;
                if (c >= hc) break;
                if (c + 1 < hc && hf[c + 1] < hf[c]) c++;
                if (hf[c] >= f) break;
                hn[i] = hn[c]; hf[i] = hf[c]; i = c;
            }
            hn[i] = n; hf[i] = f;
        }
        return top;
    }

    static bool Nearest(World w, ref int x, ref int z)
    {
        if (w.IsFree(x, z)) return true;
        for (int rad = 1; rad <= 5; rad++)
            for (int ox = -rad; ox <= rad; ox++)
                for (int oz = -rad; oz <= rad; oz++)
                {
                    if (Mathf.Abs(ox) != rad && Mathf.Abs(oz) != rad) continue;
                    if (w.IsFree(x + ox, z + oz)) { x += ox; z += oz; return true; }
                }
        return false;
    }

    static float H(int x, int z, int bx, int bz)
    {
        int ax = Mathf.Abs(x - bx), az = Mathf.Abs(z - bz);
        return ((ax + az) + (1.4142f - 2f) * Mathf.Min(ax, az)) * 1.15f;
    }

    public static bool Find(Vector3 from, Vector3 to, List<Vector3> path, int maxExpand = 7000)
    {
        path.Clear();
        var w = World.I;
        if (w == null) return false;
        var a = World.ToCell(from); var b = World.ToCell(to);
        int ax = a.x, az = a.y, bx = b.x, bz = b.y;
        if (!Nearest(w, ref ax, ref az) || !Nearest(w, ref bx, ref bz)) return false;
        stamp++;
        hc = 0;
        int start = ax * N + az, goal = bx * N + bz;
        g[start] = 0; seen[start] = stamp; came[start] = -1;
        Push(start, H(ax, az, bx, bz));
        int expanded = 0;
        while (hc > 0)
        {
            int cur = Pop();
            if (closed[cur] == stamp) continue;
            closed[cur] = stamp;
            if (cur == goal) { Build(w, cur, path, to); return true; }
            if (++expanded > maxExpand) return false;
            int cx = cur / N, cz = cur % N;
            for (int i = 0; i < 8; i++)
            {
                int nx = cx + dx[i], nz = cz + dz[i];
                if (!w.IsFree(nx, nz)) continue;
                if (i >= 4 && (!w.IsFree(cx + dx[i], cz) || !w.IsFree(cx, cz + dz[i]))) continue;
                int ni = nx * N + nz;
                if (closed[ni] == stamp) continue;
                float ng = g[cur] + (i < 4 ? 1f : 1.4142f);
                if (seen[ni] == stamp && ng >= g[ni]) continue;
                seen[ni] = stamp; g[ni] = ng; came[ni] = cur;
                Push(ni, ng + H(nx, nz, bx, bz));
            }
        }
        return false;
    }

    static Vector3 Center(int idx) => World.CellCenter(idx / N, idx % N);

    static bool LineFree(World w, int a, int b)
    {
        float ax = a / N, az = a % N, bx = b / N, bz = b % N;
        int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(bx - ax), Mathf.Abs(bz - az)) * 2f);
        for (int s = 1; s < steps; s++)
        {
            float t = s / (float)steps;
            int x = Mathf.RoundToInt(Mathf.Lerp(ax, bx, t)), z = Mathf.RoundToInt(Mathf.Lerp(az, bz, t));
            if (!w.IsFree(x, z)) return false;
        }
        return true;
    }

    static void Build(World w, int cur, List<Vector3> path, Vector3 to)
    {
        tmp.Clear();
        while (cur != -1) { tmp.Add(cur); cur = came[cur]; }
        tmp.Reverse();
        int i = 0;
        while (i < tmp.Count - 1)
        {
            int j = i + 1;
            while (j + 1 < tmp.Count && j + 1 - i < 40 && LineFree(w, tmp[i], tmp[j + 1])) j++;
            path.Add(Center(tmp[j]));
            i = j;
        }
        if (path.Count > 0) path[path.Count - 1] = new Vector3(to.x, 0f, to.z);
    }
}
