namespace InvoiceWizard.Web.Services;

public class WebUiSelectionState
{
    public int? SelectedCustomerId { get; private set; }

    public void SetSelectedCustomer(int? customerId)
    {
        SelectedCustomerId = customerId;
    }
}
