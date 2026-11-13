namespace LocalQuickhacks;

// The puzzle is independent of Unity so the path, timer and authorization rules
// can be exercised without starting EFT. No door operation happens in this type.
internal sealed class BreachPuzzle {
    internal static readonly string[] Symbols = ["1C", "55", "BD", "E9", "7A", "FF"];
    internal readonly int Size, Capacity;
    internal readonly string[,] Matrix;
    internal readonly string[][] Programs;
    internal readonly (int Row, int Col)[] Solution;
    internal readonly List<(int Row, int Col)> Picks = [];
    internal readonly List<string> Buffer = [];
    internal readonly double Duration;
    internal double? Started { get; private set; }
    double? endedAt;
    internal bool Won { get; private set; }
    internal bool Lost { get; private set; }
    internal bool Ended => Won || Lost;
    internal bool SelectRow => Picks.Count % 2 == 0;
    internal int ActiveLine => Picks.Count == 0 ? 0 : SelectRow ? Picks[^1].Row : Picks[^1].Col;

    internal BreachPuzzle(int seed, int difficulty = 1) {
        var random = new Random(seed);
        Size = 7;
        Capacity = 8;
        Duration = 36;
        Matrix = new string[Size, Size];
        for (int r = 0; r < Size; r++) for (int c = 0; c < Size; c++) Matrix[r,c] = Symbols[random.Next(Symbols.Length)];
        int length = Capacity;
        // Backtrack rather than retry random walks: every generated sequence has
        // a legal row/column path, even when many cells have already been used.
        var path = new List<(int Row, int Col)>();
        bool Build() {
            if (path.Count == length) return true;
            bool row = path.Count % 2 == 0;
            int line = path.Count == 0 ? 0 : row ? path[^1].Row : path[^1].Col;
            foreach (int index in Enumerable.Range(0, Size).OrderBy(_ => random.Next())) {
                var cell = row ? (line, index) : (index, line);
                if (path.Contains(cell)) continue;
                path.Add(cell);
                if (Build()) return true;
                path.RemoveAt(path.Count - 1);
            }
            return false;
        }
        if (!Build()) throw new InvalidOperationException("No breach path");
        Solution = path.ToArray();
        // Independent programs share overlapping sections of one valid route.
        // Shuffling the rows makes the best upload order vary between boards.
        // These are never nested prefixes of one authorization sequence.
        int firstLength=difficulty>=3?4:3, secondLength=difficulty<=1?3:4;
        var spans=new[]{(Start:0,Length:firstLength),(Start:2,Length:secondLength),(Start:4,Length:4)};
        string[][] programs=[];
        for(int attempt=0;;attempt++) {
            var codes=Solution.Select(p=>Matrix[p.Row,p.Col]).ToArray();
            programs=spans.Select(s=>codes.Skip(s.Start).Take(s.Length).ToArray()).ToArray();
            bool independent=Enumerable.Range(0,3).All(a=>Enumerable.Range(0,3).All(b=>a==b||!Contains(programs[a],programs[b])));
            if(independent) break;
            for(int i=0;i<Solution.Length;i++) {
                var cell=Solution[i];
                Matrix[cell.Row,cell.Col]=attempt<63?Symbols[random.Next(Symbols.Length)]:Symbols[new[]{0,1,2,3,4,5,0,4}[i]];
            }
        }
        Programs=programs.OrderBy(_=>random.Next()).ToArray();
    }

    internal bool Used(int row, int col) => Picks.Contains((row,col));
    internal bool CanPick(int row, int col) => !Ended && row >= 0 && col >= 0 && row < Size && col < Size && !Used(row,col) && (SelectRow ? row : col) == ActiveLine;
    internal double Remaining(double now) => Started is {} start ? Math.Clamp(Duration - ((endedAt ?? now) - start), 0, Duration) : Duration;
    internal bool Uploaded(int program)=>Contains(Buffer,Programs[program]);
    internal bool AllUploaded=>Enumerable.Range(0,Programs.Length).All(Uploaded);
    void Finish(double now) { Won=Uploaded(2); Lost=!Won; endedAt=now; }
    internal void Tick(double now) { if (double.IsFinite(now)&&!Ended&&Started.HasValue&&Remaining(now)<=0) Finish(now); }
    internal bool Pick(int row, int col, double now) {
        if (!double.IsFinite(now)) return false;
        Tick(now);
        if (!CanPick(row,col)) return false;
        Started ??= now;
        Picks.Add((row,col)); Buffer.Add(Matrix[row,col]);
        // Keep selecting after one match: other independent programs may still
        // fit the remaining slots. Only the authorization program opens a door.
        if (AllUploaded||Buffer.Count>=Capacity||!Enumerable.Range(0,Size).Any(i=>SelectRow?CanPick(ActiveLine,i):CanPick(i,ActiveLine))) Finish(now);
        return true;
    }
    internal static bool Contains(IReadOnlyList<string> buffer, IReadOnlyList<string> sequence) {
        for (int start = 0; start + sequence.Count <= buffer.Count; start++) {
            bool same = true;
            for (int i = 0; i < sequence.Count; i++) same &= buffer[start+i] == sequence[i];
            if (same) return true;
        }
        return false;
    }
}

internal enum HackKind { Cyberpsychosis, BlackwallGateway, RebootOptics }
internal static class HackRules {
    internal static string Name(HackKind kind) => kind switch { HackKind.Cyberpsychosis => "赛博精神病", HackKind.BlackwallGateway => "黑墙网关", _ => "重启义眼" };
    internal static string Icon(HackKind kind) => kind switch { HackKind.Cyberpsychosis => "Cyberpsychosis", HackKind.BlackwallGateway => "BlackwallBreach", _ => "GlitchScreenBlind" };
    internal static int Cost(HackKind kind) => kind switch { HackKind.Cyberpsychosis => 8, HackKind.BlackwallGateway => 12, _ => 2 };
    internal static float Upload(HackKind kind) => kind switch { HackKind.Cyberpsychosis => 3, HackKind.BlackwallGateway => 4, _ => 1.5f };
    internal static string Description(HackKind kind) => kind switch {
        HackKind.Cyberpsychosis => "使目标暂时敌视附近所有 AI，包括原队友。",
        HackKind.BlackwallGateway => "上传黑墙，摧毁目标并向附近 AI 连锁传播。",
        _ => "使目标暂时失去视觉；仍可移动、听声与躲避。"
    };
}

internal sealed class AuthorizationLatch {
    bool consumed;
    internal bool Take(bool won, bool valid) { if (consumed || !won || !valid) return false; consumed = true; return true; }
}
