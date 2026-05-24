using Windows.UI.ViewManagement;

namespace HackBGRTAnimated.WinUI.Services;

public static class UiMotionSettings
{
    public static bool AreAnimationsEnabled
    {
        get
        {
            try
            {
                return new UISettings().AnimationsEnabled;
            }
            catch
            {
                return true;
            }
        }
    }
}
