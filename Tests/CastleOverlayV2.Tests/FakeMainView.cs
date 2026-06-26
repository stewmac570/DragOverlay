using CastleOverlayV2;

namespace CastleOverlayV2.Tests;

/// <summary>
/// In-memory <see cref="IMainView"/> implementation for presenter tests.
/// Records every call so tests can assert what the presenter asked the view to do.
/// </summary>
public sealed class FakeMainView : IMainView
{
    public string? CsvFileToReturn { get; set; }
    public string? TuneFileToReturn { get; set; }
    public string? ProjectOpenPathToReturn { get; set; }
    public string? ProjectSavePathToReturn { get; set; }
    public string? FolderToReturn { get; set; }

    public readonly List<(string title, string message)> Errors = new();
    public readonly List<(string title, string message)> Infos = new();
    public readonly List<(int slot, string fullPath, bool isVisible)> SlotLoaded = new();
    public readonly List<int> SlotReset = new();
    public readonly List<(int slot, bool isVisible)> SlotToggle = new();
    public readonly List<(int slot, bool armed)> SlotArmed = new();
    public readonly List<bool> RunTypeApplies = new();
    public int RunTypeLockUpdates;
    public readonly List<(string runLabel, double offsetMs)> AlignmentShown = new();
    public int AlignmentHidden;
    public readonly List<double> AlignmentOffsets = new();

    public string? PickCsvFile() => CsvFileToReturn;
    public string? PickCastleCsvFile() => CsvFileToReturn;
    public string? PickRaceBoxCsvFile() => CsvFileToReturn;
    public string? PickTuneFile() => TuneFileToReturn;
    public void ShowSettingsDialog() => SettingsShown++;
    public int SettingsShown;
    public string? PickProjectFileToOpen() => ProjectOpenPathToReturn;
    public string? PickProjectFileToSave() => ProjectSavePathToReturn;
    public readonly List<(string title, string? initialDir)> FolderPicks = new();
    public string? PickFolder(string title, string? initialDir)
    {
        FolderPicks.Add((title, initialDir));
        return FolderToReturn;
    }

    public void ShowError(string title, string message) => Errors.Add((title, message));
    public void ShowInfo(string title, string message) => Infos.Add((title, message));

    public void SetSlotLoadedUI(int slot, string fullPath, bool isVisible) =>
        SlotLoaded.Add((slot, fullPath, isVisible));
    public void ResetSlotUI(int slot) => SlotReset.Add(slot);
    public void SetSlotToggleText(int slot, bool isVisible) =>
        SlotToggle.Add((slot, isVisible));
    public void SetSlotArmedUI(int slot, bool armed) =>
        SlotArmed.Add((slot, armed));

    public void ApplyRunTypeUI(bool isSpeedRun) => RunTypeApplies.Add(isSpeedRun);
    public void UpdateRunTypeLockState() => RunTypeLockUpdates++;

    public void ShowAlignmentUI(string runLabel, double offsetMs) =>
        AlignmentShown.Add((runLabel, offsetMs));
    public void HideAlignmentUI() => AlignmentHidden++;
    public void SetAlignmentOffsetUI(double offsetMs) =>
        AlignmentOffsets.Add(offsetMs);
}
