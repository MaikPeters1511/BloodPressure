using Microsoft.AspNetCore.Components;

namespace CleanArchitecture.Presentation.UI.Components;

// Code-Behind zu [Name].razor
public partial class [Name]
{
    [Parameter, EditorRequired]
    public IReadOnlyList<[Name]Dto> Items { get; set; } = [];

    [Parameter]
    public EventCallback<[Name]Dto> OnSelected { get; set; }
}
