namespace CastleOverlayV2
{
    /// <summary>
    /// Surface the <see cref="MainFormPresenter"/> calls on the view (MainForm).
    /// Extracted in #69 so the presenter can be unit-tested with a fake.
    /// </summary>
    public interface IMainView
    {
        // File pickers
        string? PickCsvFile();
        string? PickTuneFile();
        string? PickProjectFileToOpen();
        string? PickProjectFileToSave();

        // Dialogs
        void ShowError(string title, string message);
        void ShowInfo(string title, string message);

        // Slot UI
        void SetSlotLoadedUI(int slot, string fullPath, bool isVisible);
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
    }
}
