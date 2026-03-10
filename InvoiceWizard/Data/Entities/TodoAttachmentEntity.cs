namespace InvoiceWizard.Data.Entities;

public class TodoAttachmentEntity
{
    public int TodoAttachmentId { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string Caption { get; set; } = "";
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; }
    public string DownloadUrl { get; set; } = "";
}
