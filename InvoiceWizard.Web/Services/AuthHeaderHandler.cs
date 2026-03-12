using System.Net.Http.Headers;
using InvoiceWizard.Web.Models;

namespace InvoiceWizard.Web.Services;

public class AuthHeaderHandler(WebAuthSession authSession) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(authSession.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authSession.AccessToken);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
