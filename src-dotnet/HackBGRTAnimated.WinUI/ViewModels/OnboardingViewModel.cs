namespace HackBGRTAnimated.WinUI.ViewModels;

public sealed class OnboardingViewModel : ObservableObject
{
    private int _stepIndex = 1;
    public int StepIndex
    {
        get => _stepIndex;
        set => SetProperty(ref _stepIndex, value);
    }
}
