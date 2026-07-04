namespace GestureClip.App.ViewModels;

public sealed class WorkerLevelUpViewModel
{
    public WorkerLevelUpViewModel(string levelText, string xpText, string title)
    {
        LevelText = levelText;
        XpText = xpText;
        Title = title;
    }

    public string Header => "🎉 升级了！";

    public string LevelText { get; }

    public string XpText { get; }

    public string Title { get; }

    public string Hint => "继续滑动，效率继续升级。";
}
