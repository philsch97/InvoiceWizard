using System.Windows;
using System.Windows.Controls;

namespace InvoiceWizard;

public class SectionNavigationItem
{
    public string Title { get; init; } = "";
    public Func<Page> CreatePage { get; init; } = null!;
}

public partial class SectionHostPage : Page
{
    private readonly List<SectionNavigationItem> _items;
    private readonly Dictionary<Button, SectionNavigationItem> _buttonMap = new();

    public SectionHostPage(string title, string description, IEnumerable<SectionNavigationItem> items)
    {
        InitializeComponent();
        SectionTitleText.Text = title;
        SectionDescriptionText.Text = description;
        _items = items.ToList();
        BuildButtons();
    }

    private void BuildButtons()
    {
        SectionButtonsPanel.Children.Clear();
        _buttonMap.Clear();

        foreach (var item in _items)
        {
            var button = new Button
            {
                Content = item.Title,
                Width = 170,
                Height = 40,
                Margin = new Thickness(0, 0, 10, 10),
                Style = (Style)FindResource("SecondaryButtonStyle")
            };
            button.Click += (_, _) => NavigateTo(item, button);
            SectionButtonsPanel.Children.Add(button);
            _buttonMap[button] = item;
        }

        if (_buttonMap.Count > 0)
        {
            var first = _buttonMap.Keys.First();
            NavigateTo(_buttonMap[first], first);
        }
    }

    private void NavigateTo(SectionNavigationItem item, Button activeButton)
    {
        SectionFrame.Navigate(item.CreatePage());
        UpdateButtonState(activeButton);
    }

    private void UpdateButtonState(Button activeButton)
    {
        var primaryStyle = (Style)FindResource(typeof(Button));
        var secondaryStyle = (Style)FindResource("SecondaryButtonStyle");

        foreach (var button in _buttonMap.Keys)
        {
            button.Style = button == activeButton ? primaryStyle : secondaryStyle;
            button.Opacity = button == activeButton ? 1.0 : 0.92;
        }
    }
}
