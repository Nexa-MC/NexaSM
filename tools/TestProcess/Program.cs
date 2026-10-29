using System.Diagnostics;
// A bounded fixture, never a production supervisor. Parent owns this process handle.
if (args.Length == 0) { Console.Error.WriteLine("mode required: echo|slow|silent|flood|refuse-stop|exit|child"); return 2; }
var value = args.Length > 1 && int.TryParse(args[1], out var parsed) ? parsed : 100;
if (value is < 0 or > 10000) return 2;
switch (args[0])
{
    case "exit": return Math.Min(value, 255);
    case "slow": await Task.Delay(value); Console.WriteLine("READY"); return 0;
    case "silent": await Task.Delay(value); return 0;
    case "flood": for (var i = 0; i < value; i++) Console.WriteLine(new string('x', 8192)); return 0;
    case "refuse-stop": Console.WriteLine("READY"); await Task.Delay(TimeSpan.FromSeconds(30)); return 0;
    case "echo":
        Console.WriteLine("READY");
        while (await Console.In.ReadLineAsync() is { } line) { if (line == "stop") return 0; Console.WriteLine(line); }
        return 0;
    case "child":
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
        if (Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet") start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "TestProcess.dll"));
        start.ArgumentList.Add("silent"); start.ArgumentList.Add(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        using (var child = Process.Start(start)!) { Console.WriteLine($"CHILD {child.Id}"); await child.WaitForExitAsync(); }
        return 0;
    default: return 2;
}
