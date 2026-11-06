using CastleOverlayV2;
using CastleOverlayV2.Controls;
using CastleOverlayV2.Plot;
using CastleOverlayV2.Services;
using ScottPlot.WinForms;

namespace CastleOverlayV2.Tests;

/// <summary>
/// Smoke tests for the MainFormPresenter against a <see cref="FakeMainView"/>
/// (issue #69). Establishes that the interface + fake combination work
/// end-to-end; more presenter behaviour can be tested by extending these.
/// </summary>
public sealed class MainFormPresenterTests
{
    [Fact]
    public void FakeMainView_records_every_view_call()
    {
        IMainView view = new FakeMainView();
        view.ShowError("Title", "Body");
        view.SetSlotLoadedUI(2, "C:\\run.csv", true);
        view.ResetSlotUI(4);
        view.ApplyRunTypeUI(true);
        view.HideAlignmentUI();

        var fake = (FakeMainView)view;
        Assert.Single(fake.Errors);
        Assert.Equal(("Title", "Body"), fake.Errors[0]);
        Assert.Single(fake.SlotLoaded);
        Assert.Single(fake.SlotReset);
        Assert.Equal(4, fake.SlotReset[0]);
        Assert.Single(fake.RunTypeApplies);
        Assert.True(fake.RunTypeApplies[0]);
        Assert.Equal(1, fake.AlignmentHidden);
    }

    [Fact]
    public void Presenter_can_be_constructed_against_a_fake_view()
    {
        // Construction exercises the ctor's event-subscriptions on every
        // collaborator. The ChannelDrawer + TunePanel + PlotManager need
        // real WinForms instances; the test project targets net8.0-windows
        // so this is supported.
        var view = new FakeMainView();
        var config = new ConfigService();
        var formsPlot = new FormsPlot();
        try
        {
            var plot = new PlotManager(formsPlot);
            var drawer = new ChannelDrawer(
                new List<string> { "RPM", "Throttle %" },
                new Dictionary<string, bool> { ["RPM"] = true, ["Throttle %"] = true });
            var tune = new TunePanel();

            var presenter = new MainFormPresenter(view, config, plot, drawer, tune);

            Assert.False(presenter.IsSpeedRunMode);
            Assert.False(presenter.IsAnyRunLoaded);
            Assert.False(presenter.IsAlignmentArmed);
        }
        finally
        {
            formsPlot.Dispose();
        }
    }

    [Fact]
    public async Task LoadCastleRunAsync_returns_immediately_when_picker_cancels()
    {
        var view = new FakeMainView { CsvFileToReturn = null };
        var config = new ConfigService();
        var formsPlot = new FormsPlot();
        try
        {
            var plot = new PlotManager(formsPlot);
            var drawer = new ChannelDrawer(
                new List<string> { "RPM" },
                new Dictionary<string, bool> { ["RPM"] = true });
            var tune = new TunePanel();
            var presenter = new MainFormPresenter(view, config, plot, drawer, tune);

            await presenter.LoadCastleRunAsync(1);

            // Nothing was loaded → no UI updates, no errors, no infos.
            Assert.Empty(view.SlotLoaded);
            Assert.Empty(view.Errors);
            Assert.Empty(view.Infos);
            Assert.False(presenter.IsAnyRunLoaded);
        }
        finally
        {
            formsPlot.Dispose();
        }
    }

    [Fact]
    public void Get_Castle_log_saves_a_named_csv_and_loads_it_into_the_first_slot() =>
        RunWithMessageLoop(async () =>
        {
            string folder = Path.Combine(Path.GetTempPath(), $"castle-link-{Guid.NewGuid():N}");
            var link = new FakeCastleLink(CastleLinkConnectStatus.Connected);
            var view = new FakeMainView();
            var formsPlot = new FormsPlot();
            try
            {
                MainFormPresenter presenter = PresenterWith(view, formsPlot, link, folder);

                await presenter.DownloadFromCastleLinkAsync();

                string saved = Assert.Single(Directory.GetFiles(folder));
                Assert.StartsWith("Castle session 33 ", Path.GetFileName(saved));
                Assert.Contains(view.SlotLoaded, loaded => loaded.slot == 1 && loaded.fullPath == saved);
                Assert.Empty(view.Errors);
                Assert.Empty(view.Infos); // an idle log loads with no drag check popup
                Assert.Null(view.CastleLinkBusy[^1]);
            }
            finally
            {
                formsPlot.Dispose();
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
        });

    [Fact]
    public void Get_Castle_log_saves_the_esc_tune_beside_the_log_and_attaches_it() =>
        RunWithMessageLoop(async () =>
        {
            string folder = Path.Combine(Path.GetTempPath(), $"castle-link-{Guid.NewGuid():N}");
            var link = new FakeCastleLink(CastleLinkConnectStatus.Connected)
            {
                TuneBytes = File.ReadAllBytes(Path.Combine(SamplesFolder(), "tune-2026-11-06-round8.bin"))
            };
            var view = new FakeMainView();
            var formsPlot = new FormsPlot();
            try
            {
                MainFormPresenter presenter = PresenterWith(view, formsPlot, link, folder);

                await presenter.DownloadFromCastleLinkAsync();

                string csv = Assert.Single(Directory.GetFiles(folder, "*.csv"));
                string dat = Assert.Single(Directory.GetFiles(folder, "*.dat"));
                Assert.Equal(Path.ChangeExtension(csv, ".dat"), dat);
                Assert.Contains("Max Power: 60%", File.ReadAllText(dat));
                Assert.Equal(Path.GetFileName(dat), presenter.RunTune(1)?.SourceFileName);
            }
            finally
            {
                formsPlot.Dispose();
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
        });

    private static string SamplesFolder()
    {
        DirectoryInfo? folder = new(AppContext.BaseDirectory);
        while (folder is not null)
        {
            string candidate = Path.Combine(folder.FullName, "Tests", "castle-link-samples");
            if (Directory.Exists(candidate))
                return candidate;
            folder = folder.Parent;
        }
        throw new DirectoryNotFoundException("Tests/castle-link-samples was not found.");
    }

    [Theory]
    [InlineData(CastleLinkConnectStatus.NotFound, "No Castle Link")]
    [InlineData(CastleLinkConnectStatus.NoEsc, "ESC did not answer")]
    public async Task Get_Castle_log_explains_what_to_plug_in(CastleLinkConnectStatus status, string expected)
    {
        var view = new FakeMainView();
        var formsPlot = new FormsPlot();
        try
        {
            MainFormPresenter presenter = PresenterWith(view, formsPlot, new FakeCastleLink(status), "unused");

            await presenter.DownloadFromCastleLinkAsync();

            Assert.Contains(expected, Assert.Single(view.Infos).message);
            Assert.Null(view.CastleLinkBusy[^1]);
        }
        finally
        {
            formsPlot.Dispose();
        }
    }

    private static MainFormPresenter PresenterWith(
        FakeMainView view, FormsPlot formsPlot, ICastleLinkClient link, string castleFolder)
    {
        // The presenter saves its config, so this must never be the user's real settings file.
        var config = new ConfigService(Path.Combine(Path.GetTempPath(), $"castle-link-config-{Guid.NewGuid():N}"));
        config.Config.CastleLogDirectory = castleFolder;
        var drawer = new ChannelDrawer(
            new List<string> { "RPM" },
            new Dictionary<string, bool> { ["RPM"] = true });
        return new MainFormPresenter(view, config, new PlotManager(formsPlot), drawer, new TunePanel(),
            castleLinkFactory: () => link);
    }

    /// <summary>
    /// Runs a test the way the app runs: controls are created and awaits resume on one thread
    /// with a live message loop. Without the loop, an await after a control exists never resumes.
    /// </summary>
    private static void RunWithMessageLoop(Func<Task> test)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            async void Start()
            {
                try
                {
                    await test();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
                Application.ExitThread();
            }
            SynchronizationContext.Current!.Post(_ => Start(), null);
            Application.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "The test did not finish within 30 seconds.");
        if (failure is not null)
            throw new Xunit.Sdk.XunitException(failure.ToString());
    }

    private sealed class FakeCastleLink : ICastleLinkClient
    {
        private readonly CastleLinkConnectStatus _status;

        public FakeCastleLink(CastleLinkConnectStatus status) => _status = status;

        public string EscFirmware => "GL00136";

        public byte[]? TuneBytes { get; init; }

        public CastleLinkConnectResult Connect(CancellationToken cancellationToken = default) =>
            new(_status, "COM3");

        public IReadOnlyList<CastleSessionInfo> ListSessions(CancellationToken cancellationToken = default) =>
            new[] { new CastleSessionInfo(33, 1) };

        public void KeepAlive()
        {
        }

        public byte[] DownloadSession(CastleSessionInfo session, CancellationToken cancellationToken = default) =>
            Enumerable.Repeat(new byte[] { 0x02, 0xFE }, 200).SelectMany(record => record).ToArray();

        public void Dispose()
        {
        }
    }
}
