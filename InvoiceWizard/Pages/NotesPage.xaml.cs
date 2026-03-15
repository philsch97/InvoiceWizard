using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using InvoiceWizard.Data.Entities;
using InvoiceWizard.Data.ViewModels;
using Microsoft.Win32;

namespace InvoiceWizard;

public partial class NotesPage : Page
{
    private List<TodoListEntity> _todoLists = [];
    private TodoItemEntity? _selectedTodoItem;
    private NoteAttachmentPreview? _selectedAttachment;
    private bool _isApplyingListState;

    public NotesPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadCustomersAsync();
    }

    private async void CustomerCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        App.SetSelectedCustomer((CustomerCombo.SelectedItem as CustomerEntity)?.CustomerId);
        await LoadProjectsAsync(CustomerCombo.SelectedItem as CustomerEntity);
        await LoadTodoListsAsync();
    }

    private async void ProjectCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await LoadTodoListsAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadTodoListsAsync();
    }

    private async void CreateTodoList_Click(object sender, RoutedEventArgs e)
    {
        if (CustomerCombo.SelectedItem is not CustomerEntity customer)
        {
            SetStatus("Bitte zuerst einen Kunden auswaehlen.", StatusMessageType.Warning);
            return;
        }

        var title = (TodoListTitleText.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            SetStatus("Bitte einen Titel fuer die Notizliste eingeben.", StatusMessageType.Error);
            return;
        }

        var projectId = (ProjectCombo.SelectedItem as ProjectSelectionItem)?.ProjectId;
        var created = await App.Api.CreateTodoListAsync(customer.CustomerId, projectId, title);
        TodoListTitleText.Clear();
        await LoadTodoListsAsync(created.TodoListId);
        SetStatus($"Liste {created.Title} wurde angelegt.", StatusMessageType.Success);
    }

    private async void DeleteTodoList_Click(object sender, RoutedEventArgs e)
    {
        if (TodoListsBox.SelectedItem is not TodoListEntity list)
        {
            SetStatus("Bitte zuerst eine Notizliste auswaehlen.", StatusMessageType.Warning);
            return;
        }

        if (MessageBox.Show($"Soll die Liste {list.Title} wirklich geloescht werden?", "Liste loeschen", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        await App.Api.DeleteTodoListAsync(list.TodoListId);
        await LoadTodoListsAsync();
        SetStatus($"Liste {list.Title} wurde geloescht.", StatusMessageType.Success);
    }

    private async void AddRootItem_Click(object sender, RoutedEventArgs e)
    {
        await AddTodoItemAsync(parentTodoItemId: null, requireSelectedParent: false);
    }

    private async void AddChildItem_Click(object sender, RoutedEventArgs e)
    {
        await AddTodoItemAsync(_selectedTodoItem?.TodoItemId, requireSelectedParent: true);
    }

    private async Task AddTodoItemAsync(int? parentTodoItemId, bool requireSelectedParent)
    {
        if (TodoListsBox.SelectedItem is not TodoListEntity list)
        {
            SetStatus("Bitte zuerst eine Notizliste auswaehlen.", StatusMessageType.Warning);
            return;
        }

        if (requireSelectedParent && _selectedTodoItem is null)
        {
            SetStatus("Bitte zuerst einen vorhandenen Punkt markieren, damit der Unterpunkt eingeordnet werden kann.", StatusMessageType.Warning);
            return;
        }

        var text = (TodoItemText.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            SetStatus("Bitte einen Text fuer den ToDo-Punkt eingeben.", StatusMessageType.Error);
            return;
        }

        var updated = await App.Api.CreateTodoItemAsync(list.TodoListId, text, parentTodoItemId);
        TodoItemText.Clear();
        ApplyUpdatedList(updated);
        SetStatus(requireSelectedParent ? "Unterpunkt gespeichert." : "Punkt gespeichert.", StatusMessageType.Success);
    }

    private async void DeleteTodoItem_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTodoItem is null)
        {
            SetStatus("Bitte zuerst einen ToDo-Punkt markieren.", StatusMessageType.Warning);
            return;
        }

        if (MessageBox.Show("Soll der markierte Punkt inklusive Unterpunkten geloescht werden?", "Punkt loeschen", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        var updated = await App.Api.DeleteTodoItemAsync(_selectedTodoItem.TodoItemId);
        _selectedTodoItem = null;
        ApplyUpdatedList(updated);
        SetStatus("Punkt wurde geloescht.", StatusMessageType.Success);
    }

    private async void TodoItemCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isApplyingListState || sender is not CheckBox checkBox || checkBox.Tag is not int todoItemId || TodoListsBox.SelectedItem is not TodoListEntity)
        {
            return;
        }

        var updated = await App.Api.UpdateTodoItemStateAsync(todoItemId, checkBox.IsChecked == true);
        ApplyUpdatedList(updated);
        SetStatus(checkBox.IsChecked == true ? "Punkt und Unterpunkte als erledigt markiert." : "Punkt und Unterpunkte wieder geoeffnet.", StatusMessageType.Success);
    }

    private async void UploadImage_Click(object sender, RoutedEventArgs e)
    {
        if (TodoListsBox.SelectedItem is not TodoListEntity list)
        {
            SetStatus("Bitte zuerst eine Notizliste auswaehlen.", StatusMessageType.Warning);
            return;
        }

        var dialog = new OpenFileDialog
        {
            Filter = "Bilder (*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp",
            Multiselect = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var caption = (ImageCaptionText.Text ?? string.Empty).Trim();
        TodoListEntity? updated = null;
        foreach (var fileName in dialog.FileNames)
        {
            updated = await App.Api.UploadTodoAttachmentAsync(list.TodoListId, fileName, caption);
        }

        if (updated is not null)
        {
            ImageCaptionText.Clear();
            ApplyUpdatedList(updated);
            SetStatus($"{dialog.FileNames.Length} Bild(er) mit Beschriftung hinzugefuegt.", StatusMessageType.Success);
        }
    }

    private async void DeleteSelectedImage_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedAttachment is null)
        {
            SetStatus("Bitte zuerst ein Bild auswaehlen.", StatusMessageType.Warning);
            return;
        }

        var updated = await App.Api.DeleteTodoAttachmentAsync(_selectedAttachment.Attachment.TodoAttachmentId);
        _selectedAttachment = null;
        ApplyUpdatedList(updated);
        SetStatus("Bild wurde entfernt.", StatusMessageType.Success);
    }

    private async void TodoListsBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TodoListsBox.SelectedItem is not TodoListEntity list)
        {
            ClearSelectedListUi();
            return;
        }

        await ShowSelectedListAsync(list);
    }

    private void TodoTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        _selectedTodoItem = e.NewValue as TodoItemEntity;
    }

    private void AttachmentsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedAttachment = AttachmentsListBox.SelectedItem as NoteAttachmentPreview;
    }

    private async Task LoadCustomersAsync()
    {
        var customers = await App.Api.GetCustomersAsync();
        CustomerCombo.ItemsSource = customers;
        if (customers.Count == 0)
        {
            CustomerCombo.SelectedItem = null;
            ProjectCombo.ItemsSource = null;
            TodoListsBox.ItemsSource = null;
            ClearSelectedListUi();
            SetStatus("Noch keine Kunden vorhanden. Lege zuerst einen Kunden an.", StatusMessageType.Info);
            return;
        }

        var customer = App.SelectedCustomerId.HasValue
            ? customers.FirstOrDefault(c => c.CustomerId == App.SelectedCustomerId.Value) ?? customers[0]
            : customers[0];
        CustomerCombo.SelectedItem = customer;
        App.SetSelectedCustomer(customer.CustomerId);
        await LoadProjectsAsync(customer);
        await LoadTodoListsAsync();
    }

    private async Task LoadProjectsAsync(CustomerEntity? customer)
    {
        if (customer is null)
        {
            ProjectCombo.ItemsSource = null;
            ProjectCombo.SelectedItem = null;
            return;
        }

        var projects = await App.Api.GetProjectSelectionsAsync(customer.CustomerId, includeAll: true);
        ProjectCombo.ItemsSource = projects;
        ProjectCombo.SelectedItem = projects.FirstOrDefault();
    }

    private async Task LoadTodoListsAsync(int? selectTodoListId = null)
    {
        if (CustomerCombo.SelectedItem is not CustomerEntity customer)
        {
            TodoListsBox.ItemsSource = null;
            ClearSelectedListUi();
            return;
        }

        var projectId = (ProjectCombo.SelectedItem as ProjectSelectionItem)?.ProjectId;
        _todoLists = await App.Api.GetTodoListsAsync(customer.CustomerId, projectId);
        TodoListsBox.ItemsSource = _todoLists;

        if (_todoLists.Count == 0)
        {
            TodoListsBox.SelectedItem = null;
            ClearSelectedListUi();
            SetStatus("Noch keine ToDo-Listen fuer diese Auswahl vorhanden.", StatusMessageType.Info);
            return;
        }

        var toSelect = selectTodoListId.HasValue
            ? _todoLists.FirstOrDefault(x => x.TodoListId == selectTodoListId.Value)
            : _todoLists.First();
        TodoListsBox.SelectedItem = toSelect ?? _todoLists.First();
        SetStatus($"{_todoLists.Count} Notizliste(n) geladen.", StatusMessageType.Info);
    }

    private async Task ShowSelectedListAsync(TodoListEntity list)
    {
        _isApplyingListState = true;
        SelectedListTitleText.Text = list.Title;
        SelectedListMetaText.Text = $"Kunde: {list.Customer.Name} | Bereich: {list.ScopeLabel} | Offen: {list.OpenItemCount} | Erledigt: {list.CompletedItemCount}";
        TodoTree.ItemsSource = list.Items;
        _selectedTodoItem = null;
        _selectedAttachment = null;
        await LoadAttachmentPreviewsAsync(list);
        _isApplyingListState = false;
    }

    private async Task LoadAttachmentPreviewsAsync(TodoListEntity list)
    {
        var previews = new ObservableCollection<NoteAttachmentPreview>();
        foreach (var attachment in list.Attachments)
        {
            try
            {
                var bytes = await App.Api.DownloadTodoAttachmentAsync(attachment.TodoAttachmentId);
                previews.Add(new NoteAttachmentPreview(attachment, CreateImage(bytes)));
            }
            catch
            {
                previews.Add(new NoteAttachmentPreview(attachment, null));
            }
        }

        AttachmentsListBox.ItemsSource = previews;
        AttachmentsListBox.SelectedItem = previews.FirstOrDefault();
    }

    private void ApplyUpdatedList(TodoListEntity updated)
    {
        var index = _todoLists.FindIndex(x => x.TodoListId == updated.TodoListId);
        if (index >= 0)
        {
            _todoLists[index] = updated;
        }
        else
        {
            _todoLists.Insert(0, updated);
        }

        var orderedLists = _todoLists.OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Title).ToList();
        TodoListsBox.ItemsSource = null;
        TodoListsBox.ItemsSource = orderedLists;
        TodoListsBox.SelectedItem = orderedLists.FirstOrDefault(x => x.TodoListId == updated.TodoListId);
    }

    private void ClearSelectedListUi()
    {
        SelectedListTitleText.Text = "Keine Liste ausgewaehlt";
        SelectedListMetaText.Text = "Waehle links eine bestehende Liste aus oder lege oben eine neue an.";
        TodoTree.ItemsSource = null;
        AttachmentsListBox.ItemsSource = null;
        _selectedTodoItem = null;
        _selectedAttachment = null;
    }

    private static ImageSource? CreateImage(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private void SetStatus(string message, StatusMessageType type)
    {
        StatusText.Text = message;
        StatusBorder.Background = GetBrush(type, "Background");
        StatusBorder.BorderBrush = GetBrush(type, "Border");
        StatusText.Foreground = GetBrush(type, "Text");
    }

    private Brush GetBrush(StatusMessageType type, string part)
    {
        var key = $"Status{type}{part}Brush";
        return (Brush)FindResource(key);
    }

    private sealed class NoteAttachmentPreview
    {
        public NoteAttachmentPreview(TodoAttachmentEntity attachment, ImageSource? preview)
        {
            Attachment = attachment;
            Preview = preview;
        }

        public TodoAttachmentEntity Attachment { get; }
        public ImageSource? Preview { get; }
        public string FileName => Attachment.FileName;
        public string Caption => Attachment.Caption;
        public Visibility CaptionVisibility => string.IsNullOrWhiteSpace(Attachment.Caption) ? Visibility.Collapsed : Visibility.Visible;
        public string Subtitle => $"{Attachment.UploadedAt:dd.MM.yyyy HH:mm} | {Attachment.FileSize / 1024d:0.#} KB";
    }
}
