using Letterist.Model;

namespace Letterist.Commands;

public sealed class SetOffPanelIndicatorStyleCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetOffPanelIndicatorStyle";
    public string Description => "Set off-panel indicator style";

    private readonly Guid _pageId;
    private readonly OffPanelIndicatorStyle _newStyle;
    private OffPanelIndicatorStyle? _oldStyle;

    public SetOffPanelIndicatorStyleCommand(Guid pageId, OffPanelIndicatorStyle newStyle)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _newStyle = newStyle;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId) ?? throw new InvalidOperationException("Page not found");
        _oldStyle = page.OffPanelIndicatorStyle;
        page.SetOffPanelIndicatorStyle(_newStyle);
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId) ?? throw new InvalidOperationException("Page not found");
        page.SetOffPanelIndicatorStyle(_oldStyle ?? OffPanelIndicatorStyle.Default);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["pageId"] = _pageId,
                ["colorR"] = _newStyle.Color.R,
                ["colorG"] = _newStyle.Color.G,
                ["colorB"] = _newStyle.Color.B,
                ["colorA"] = _newStyle.Color.A,
                ["size"] = _newStyle.Size
            }
        };
    }
}
