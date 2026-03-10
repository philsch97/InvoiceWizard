using System.Windows;
using System.Windows.Controls;

namespace InvoiceWizard;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        NavigateTo(new Datenimport(), ImportButton);
    }

    private void Datenimport_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(new Datenimport(), ImportButton);
    }

    private void Datenhandling_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(new DataHandling(), AllocationButton);
    }

    private void CustomerHandling_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(new CustomerHandling(), CustomerButton);
    }

    private void WorkTime_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(new WorkTimePage(), WorkTimeButton);
    }

    private void Notes_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(new NotesPage(), NotesButton);
    }

    private void Analytics_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(new AnalyticsPage(), AnalyticsButton);
    }

    private void NavigateTo(Page page, Button activeButton)
    {
        MainFrame.Navigate(page);
        UpdateNavigationState(activeButton);
    }

    private void UpdateNavigationState(Button activeButton)
    {
        var primaryStyle = (Style)FindResource(typeof(Button));
        var secondaryStyle = (Style)FindResource("SecondaryButtonStyle");

        foreach (var button in new[] { ImportButton, AllocationButton, CustomerButton, WorkTimeButton, NotesButton, AnalyticsButton })
        {
            button.Style = button == activeButton ? primaryStyle : secondaryStyle;
            button.Opacity = button == activeButton ? 1.0 : 0.92;
        }
    }
}
