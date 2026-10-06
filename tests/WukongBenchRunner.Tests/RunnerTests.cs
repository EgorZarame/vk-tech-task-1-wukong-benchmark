using WukongBenchRunner.Running;

namespace WukongBenchRunner.Tests;

public class BenchmarkRunnerTests
{
    private readonly FakeTime _time = new();
    private readonly FakeProcess _process = new();
    private readonly FakeNavigator _navigator = new();
    private readonly FakeResultSource _results = new();
    private readonly FakeDelay _delay;

    public BenchmarkRunnerTests() => _delay = new FakeDelay(_time);

    private BenchmarkRunner CreateRunner(RunnerOptions? options = null) =>
        new(_process, _navigator, _results, _delay, _time, options ?? new RunnerOptions(), new NullLog());

    [Fact]
    public async Task Run_NavigatesMenuUntilResultAppears()
    {
        _delay.OnWait = calls =>
        {
            if (calls == 5) _results.Pending = ("result.json", SampleMetrics.Create(fps: 123));
        };

        var outcome = await CreateRunner().RunAsync("b1.exe", CancellationToken.None);

        Assert.Equal(123, outcome.Metrics.AverageFps);
        Assert.Equal([0, 1, 2, 3, 4], _navigator.Steps);
        Assert.Equal(1, _process.Starts);
    }

    [Fact]
    public async Task Run_DoesNotPressAnythingOnceResultIsReady()
    {
        _results.Pending = ("result.json", SampleMetrics.Create());

        await CreateRunner().RunAsync("b1.exe", CancellationToken.None);

        Assert.Empty(_navigator.Steps);
    }

    [Fact]
    public async Task Run_StepCounterAdvancesOnlyWhenActionWasPerformed()
    {
        _navigator.Available = false;
        _delay.OnWait = calls =>
        {
            if (calls == 3) _navigator.Available = true;
            if (calls == 5) _results.Pending = ("result.json", SampleMetrics.Create());
        };

        await CreateRunner().RunAsync("b1.exe", CancellationToken.None);

        // Первые три итерации окна не было — меню начинается с шага 0, а не с 3.
        Assert.Equal([0, 1], _navigator.Steps);
    }

    [Fact]
    public async Task Run_Timeout_ThrowsAndKillsProcess()
    {
        var runner = CreateRunner(new RunnerOptions { Timeout = TimeSpan.FromMinutes(1) });

        await Assert.ThrowsAsync<BenchmarkRunException>(() => runner.RunAsync("b1.exe", CancellationToken.None));

        Assert.Equal(1, _process.Kills);
        Assert.False(_process.Running);
    }

    [Fact]
    public async Task Run_ProcessDisappears_FailsAfterSeveralChecks()
    {
        _delay.OnWait = calls =>
        {
            if (calls == 2) _process.Running = false;
        };
        var runner = CreateRunner(new RunnerOptions { MissingProcessChecks = 3 });

        var error = await Assert.ThrowsAsync<BenchmarkRunException>(() => runner.RunAsync("b1.exe", CancellationToken.None));

        Assert.Contains("закрылся", error.Message);
        // Пропал после 2-й паузы → 3 пустые проверки → ошибка перед 5-й паузой.
        Assert.Equal(4, _delay.Calls);
    }

    [Fact]
    public async Task Run_LauncherRestartsGame_IsNotTreatedAsCrash()
    {
        // b1.exe закрывается и через пару проверок появляется процесс игры.
        _delay.OnWait = calls =>
        {
            if (calls == 1) _process.Running = false;
            if (calls == 3) _process.Running = true;
            if (calls == 6) _results.Pending = ("result.json", SampleMetrics.Create());
        };
        var runner = CreateRunner(new RunnerOptions { MissingProcessChecks = 3 });

        var outcome = await runner.RunAsync("b1.exe", CancellationToken.None);

        Assert.NotNull(outcome);
    }

    [Fact]
    public async Task Run_AlreadyRunning_RefusesToStart()
    {
        _process.Running = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateRunner().RunAsync("b1.exe", CancellationToken.None));

        Assert.Equal(0, _process.Starts);
    }

    [Fact]
    public async Task Run_Cancelled_KillsProcess()
    {
        using var cts = new CancellationTokenSource();
        _delay.OnWait = calls =>
        {
            if (calls == 2) cts.Cancel();
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateRunner().RunAsync("b1.exe", cts.Token));

        Assert.Equal(1, _process.Kills);
    }
}

public class MenuNavigatorTests
{
    private readonly FakeProcess _process = new() { Running = true };
    private readonly FakeInput _input = new();
    private readonly MenuLayout _layout = new();

    [Fact]
    public void TryAdvance_CyclesEnterStartConfirm()
    {
        var navigator = new MenuNavigator(_process, _input, _layout);

        for (var step = 0; step < 4; step++) navigator.TryAdvance(step);

        Assert.Equal(
        [
            "key:13",
            $"click:{_layout.StartButtonX}:{_layout.StartButtonY}",
            $"click:{_layout.ConfirmButtonX}:{_layout.ConfirmButtonY}",
            "key:13",
        ], _input.Actions);
    }

    [Fact]
    public void TryAdvance_NoWindow_DoesNothing()
    {
        _process.Running = false;

        Assert.False(new MenuNavigator(_process, _input, _layout).TryAdvance(0));
        Assert.Empty(_input.Actions);
    }

    [Fact]
    public void TryAdvance_WindowCannotBeFocused_DoesNotClick()
    {
        _input.Foreground = false;
        _input.CanActivate = false;

        Assert.False(new MenuNavigator(_process, _input, _layout).TryAdvance(1));
        Assert.Equal(["activate"], _input.Actions);
    }
}
