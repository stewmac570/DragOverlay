using CastleOverlayV2.Services;

namespace CastleOverlayV2
{
    public interface IMiniLoggerDownloadSession : IDisposable
    {
        CancellationToken CancellationToken { get; }
        void Report(MiniLoggerDownloadProgress progress);
        void Complete();
    }

    /// <summary>
    /// Surface the <see cref="MainFormPresenter"/> calls on the view (MainForm).
    /// Extracted in #69 so the presenter can be unit-tested with a fake.
    /// </summary>
    public interface IMainView
    {
        // File pickers
        string? PickCsvFile();
        string? PickCastleCsvFile();
        string? PickRaceBoxCsvFile();
        string? PickDataLogCsvFile();
        string? PickTuneFile();
        string? PickProjectFileToOpen();
        string? PickProjectFileToSave();
        string? PickFolder(string title, string? initialDir);

        // Dialogs
        void ShowError(string title, string message);
        void ShowInfo(string title, string message);
        bool ConfirmMiniLoggerUsbSafety();
        void SetMiniLoggerSearchState(bool searching);
        IReadOnlyList<MiniLoggerRunInfo>? SelectMiniLoggerRuns(IReadOnlyList<MiniLoggerRunInfo> runs);
        IMiniLoggerDownloadSession BeginMiniLoggerDownload(int runCount);

        // Slot UI
        void SetSlotLoadedUI(int slot, string fullPath, bool isVisible, string? sourceTag = null);
        void ResetSlotUI(int slot);
        void SetSlotToggleText(int slot, bool isVisible);
        void SetSlotArmedUI(int slot, bool armed);

        // RunType
        void ApplyRunTypeUI(bool isSpeedRun);
        void UpdateRunTypeLockState();

        // Alignment
        void ShowAlignmentUI(string runLabel, double offsetMs);
        void HideAlignmentUI();
        void SetAlignmentOffsetUI(double offsetMs);

        // Settings
        void ShowSettingsDialog();

        // Project
        void SetSaveProjectEnabled(bool enabled);

        // Auto trim
        void SetAutoTrimUI(bool on);
    }
}
