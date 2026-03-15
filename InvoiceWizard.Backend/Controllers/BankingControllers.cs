using System.Globalization;
using System.Text;
using InvoiceWizard.Backend.Contracts;
using InvoiceWizard.Backend.Data;
using InvoiceWizard.Backend.Domain;
using InvoiceWizard.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InvoiceWizard.Backend.Controllers;

[Authorize]
[ApiController]
[Route("api/banking")]
public class BankingController(InvoiceWizardDbContext db, ICurrentTenantAccessor tenantAccessor) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<BankAccountSummaryDto>> GetSummary()
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var transactions = await db.BankTransactions
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.BookingDate)
            .ThenByDescending(x => x.BankTransactionId)
            .Select(x => new { x.BookingDate, x.BalanceAfterBooking, x.AccountIban, ImportAccountName = x.BankStatementImport.AccountName })
            .ToListAsync(HttpContext.RequestAborted);

        var latest = transactions.FirstOrDefault(x => x.BalanceAfterBooking.HasValue);
        return Ok(new BankAccountSummaryDto
        {
            TransactionCount = transactions.Count,
            CurrentBalance = latest?.BalanceAfterBooking,
            LastBookingDate = transactions.FirstOrDefault()?.BookingDate,
            AccountIban = latest?.AccountIban ?? "",
            AccountName = latest?.ImportAccountName ?? ""
        });
    }

    [HttpPost("imports/file")]
    [HttpPost("imports/csv")]
    public async Task<ActionResult<ImportBankStatementCsvResponseDto>> ImportFile([FromBody] ImportBankStatementCsvRequest request)
    {
        if (!User.IsInRole(TenantRoles.Admin))
        {
            return Forbid();
        }

        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        ParsedBankStatement parsed;

        try
        {
            var fileBytes = Convert.FromBase64String(request.CsvContentBase64);
            var fileName = request.FileName.Trim();
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            parsed = extension switch
            {
                ".zip" or ".xml" => new BankStatementCamtParser().Parse(fileBytes, fileName),
                _ => new BankStatementCsvParser().Parse(fileBytes, fileName)
            };
        }
        catch (FormatException)
        {
            return ValidationProblem("Die Datei konnte nicht gelesen werden.");
        }
        catch (InvalidOperationException ex)
        {
            return ValidationProblem(ex.Message);
        }

        if (parsed.Transactions.Count == 0)
        {
            return ValidationProblem("In der Datei wurden keine Buchungen erkannt.");
        }

        var import = new BankStatementImport
        {
            TenantId = tenantId,
            FileName = request.FileName.Trim(),
            AccountName = parsed.AccountName,
            AccountIban = parsed.AccountIban,
            Currency = parsed.Currency,
        };

        db.BankStatementImports.Add(import);
        await db.SaveChangesAsync(HttpContext.RequestAborted);

        var contentHashes = parsed.Transactions.Select(x => x.ContentHash).Distinct().ToList();
        var existing = await db.BankTransactions
            .Where(x => x.TenantId == tenantId && contentHashes.Contains(x.ContentHash))
            .Select(x => x.ContentHash)
            .ToListAsync(HttpContext.RequestAborted);
        var existingSet = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var importedCount = 0;
        var skippedCount = 0;
        foreach (var item in parsed.Transactions)
        {
            if (existingSet.Contains(item.ContentHash))
            {
                skippedCount++;
                continue;
            }

            db.BankTransactions.Add(new BankTransaction
            {
                TenantId = tenantId,
                BankStatementImportId = import.BankStatementImportId,
                BookingDate = item.BookingDate,
                ValueDate = item.ValueDate,
                Amount = item.Amount,
                BalanceAfterBooking = item.BalanceAfterBooking,
                Currency = item.Currency,
                CounterpartyName = item.CounterpartyName,
                CounterpartyIban = item.CounterpartyIban,
                Purpose = item.Purpose,
                Reference = item.Reference,
                TransactionType = item.TransactionType,
                AccountIban = item.AccountIban,
                ContentHash = item.ContentHash
            });
            importedCount++;
        }

        await db.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(new ImportBankStatementCsvResponseDto
        {
            ImportId = import.BankStatementImportId,
            FileName = import.FileName,
            AccountName = import.AccountName,
            AccountIban = import.AccountIban,
            Currency = import.Currency,
            ImportedTransactions = importedCount,
            SkippedTransactions = skippedCount,
            CurrentBalance = parsed.CurrentBalance ?? parsed.Transactions
                .Where(x => x.BalanceAfterBooking.HasValue)
                .OrderByDescending(x => x.BookingDate)
                .ThenByDescending(x => x.ValueDate)
                .Select(x => x.BalanceAfterBooking)
                .FirstOrDefault(),
            Warnings = parsed.Warnings
        });
    }

    [HttpGet("transactions")]
    public async Task<ActionResult<IReadOnlyList<BankTransactionListItemDto>>> GetTransactions([FromQuery] bool showAssigned = true, [FromQuery] bool showIgnored = false)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var query = db.BankTransactions
            .Where(x => x.TenantId == tenantId);

        if (!showIgnored)
        {
            query = query.Where(x => !x.IsIgnored);
        }

        var transactions = await query
            .Include(x => x.BankStatementImport)
            .Include(x => x.Assignments).ThenInclude(x => x.SupplierInvoice)
            .Include(x => x.Assignments).ThenInclude(x => x.Customer)
            .OrderByDescending(x => x.BookingDate)
            .ThenByDescending(x => x.BankTransactionId)
            .ToListAsync(HttpContext.RequestAborted);

        var result = transactions.Select(MapTransaction).ToList();
        if (!showAssigned)
        {
            result = result.Where(x => x.RemainingAmount > 0.009m).ToList();
        }

        return Ok(result);
    }

    [HttpPut("transactions/{bankTransactionId:int}/ignore")]
    public async Task<ActionResult<BankTransactionListItemDto>> IgnoreTransaction(int bankTransactionId, [FromBody] IgnoreBankTransactionRequest request)
    {
        if (!User.IsInRole(TenantRoles.Admin))
        {
            return Forbid();
        }

        var comment = (request.Comment ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(comment))
        {
            return ValidationProblem("Bitte einen Kommentar eingeben, warum der Umsatz ignoriert wird.");
        }

        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var transaction = await db.BankTransactions
            .Where(x => x.BankTransactionId == bankTransactionId && x.TenantId == tenantId)
            .Include(x => x.BankStatementImport)
            .Include(x => x.Assignments).ThenInclude(x => x.SupplierInvoice)
            .Include(x => x.Assignments).ThenInclude(x => x.Customer)
            .FirstOrDefaultAsync(HttpContext.RequestAborted);
        if (transaction is null)
        {
            return NotFound();
        }

        if (transaction.Assignments.Count > 0)
        {
            return ValidationProblem("Zugeordnete Buchungen koennen nicht ignoriert werden. Bitte zuerst die Zuordnungen loeschen.");
        }

        transaction.IsIgnored = true;
        transaction.IgnoredComment = comment;
        transaction.IgnoredAt = DateTime.UtcNow;
        await db.SaveChangesAsync(HttpContext.RequestAborted);
        return Ok(MapTransaction(transaction));
    }

    [HttpDelete("transactions/{bankTransactionId:int}/ignore")]
    public async Task<ActionResult<BankTransactionListItemDto>> UnignoreTransaction(int bankTransactionId)
    {
        if (!User.IsInRole(TenantRoles.Admin))
        {
            return Forbid();
        }

        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var transaction = await db.BankTransactions
            .Where(x => x.BankTransactionId == bankTransactionId && x.TenantId == tenantId)
            .Include(x => x.BankStatementImport)
            .Include(x => x.Assignments).ThenInclude(x => x.SupplierInvoice)
            .Include(x => x.Assignments).ThenInclude(x => x.Customer)
            .FirstOrDefaultAsync(HttpContext.RequestAborted);
        if (transaction is null)
        {
            return NotFound();
        }

        transaction.IsIgnored = false;
        transaction.IgnoredComment = string.Empty;
        transaction.IgnoredAt = null;
        await db.SaveChangesAsync(HttpContext.RequestAborted);
        return Ok(MapTransaction(transaction));
    }

    [HttpGet("transactions/{bankTransactionId:int}/candidates")]
    public async Task<ActionResult<IReadOnlyList<BankInvoiceCandidateDto>>> GetCandidates(int bankTransactionId)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var transaction = await db.BankTransactions
            .Include(x => x.Assignments)
            .FirstOrDefaultAsync(x => x.BankTransactionId == bankTransactionId && x.TenantId == tenantId, HttpContext.RequestAborted);
        if (transaction is null)
        {
            return NotFound();
        }

        if (transaction.IsIgnored)
        {
            return Ok(Array.Empty<BankInvoiceCandidateDto>());
        }

        var candidates = transaction.Amount >= 0m
            ? await GetCustomerInvoiceCandidatesAsync(tenantId, transaction)
            : await GetSupplierInvoiceCandidatesAsync(tenantId, transaction);

        return Ok(candidates
            .Where(x => x.RemainingAmount > 0.009m || x.MatchScore >= 80m)
            .OrderByDescending(x => x.MatchScore)
            .ThenBy(x => Math.Abs(x.RemainingAmount - Math.Abs(transaction.Amount)))
            .ThenByDescending(x => x.InvoiceDate)
            .Take(40)
            .ToList());
    }

    [HttpPost("transactions/{bankTransactionId:int}/assignments")]
    public async Task<ActionResult<BankTransactionListItemDto>> CreateAssignment(int bankTransactionId, [FromBody] AssignBankTransactionRequest request)
    {
        if (!User.IsInRole(TenantRoles.Admin))
        {
            return Forbid();
        }

        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var transaction = await db.BankTransactions
            .Include(x => x.BankStatementImport)
            .Include(x => x.Assignments).ThenInclude(x => x.SupplierInvoice)
            .Include(x => x.Assignments).ThenInclude(x => x.Customer)
            .FirstOrDefaultAsync(x => x.BankTransactionId == bankTransactionId && x.TenantId == tenantId, HttpContext.RequestAborted);
        if (transaction is null)
        {
            return NotFound();
        }

        var directionIsIncoming = transaction.Amount >= 0m;
        if (transaction.IsIgnored)
        {
            return ValidationProblem("Ignorierte Buchungen koennen nicht zugeordnet werden.");
        }

        if (directionIsIncoming && string.IsNullOrWhiteSpace(request.CustomerInvoiceNumber))
        {
            return ValidationProblem("Eingehende Buchungen koennen nur Kundenrechnungen zugewiesen werden.");
        }

        if (!directionIsIncoming && !request.SupplierInvoiceId.HasValue)
        {
            return ValidationProblem("Ausgehende Buchungen koennen nur Lieferantenrechnungen zugewiesen werden.");
        }

        var transactionRemaining = CalculateRemainingAmount(transaction.Amount, transaction.Assignments.Sum(x => x.AssignedAmount));
        if (transactionRemaining <= 0.009m)
        {
            return ValidationProblem("Diese Buchung ist bereits vollstaendig zugeordnet.");
        }

        decimal candidateRemaining;
        string partyName;
        int? customerId = null;
        if (request.SupplierInvoiceId.HasValue)
        {
            var supplierCandidate = await GetSupplierInvoiceCandidateAsync(tenantId, request.SupplierInvoiceId.Value, transaction);
            if (supplierCandidate is null)
            {
                return NotFound();
            }

            candidateRemaining = supplierCandidate.RemainingAmount;
            partyName = supplierCandidate.PartyName;
        }
        else
        {
            var invoiceNumber = (request.CustomerInvoiceNumber ?? string.Empty).Trim();
            var customerCandidate = await GetCustomerInvoiceCandidateAsync(tenantId, invoiceNumber, request.CustomerId, transaction);
            if (customerCandidate is null)
            {
                return NotFound();
            }

            candidateRemaining = customerCandidate.RemainingAmount;
            partyName = customerCandidate.PartyName;
            customerId = customerCandidate.CustomerId;
        }

        if (candidateRemaining <= 0.009m)
        {
            return ValidationProblem("Die ausgewaehlte Rechnung ist bereits vollstaendig zugeordnet.");
        }

        var assignedAmount = request.AssignedAmount ?? Math.Min(transactionRemaining, candidateRemaining);
        if (assignedAmount <= 0m)
        {
            return ValidationProblem("Bitte einen gueltigen Zuordnungsbetrag angeben.");
        }

        if (assignedAmount - transactionRemaining > 0.009m)
        {
            return ValidationProblem("Der Zuordnungsbetrag ist groesser als der offene Betrag der Buchung.");
        }

        if (assignedAmount - candidateRemaining > 0.009m)
        {
            return ValidationProblem("Der Zuordnungsbetrag ist groesser als der offene Betrag der Rechnung.");
        }

        db.BankTransactionAssignments.Add(new BankTransactionAssignment
        {
            TenantId = tenantId,
            BankTransactionId = transaction.BankTransactionId,
            SupplierInvoiceId = request.SupplierInvoiceId,
            CustomerInvoiceNumber = string.IsNullOrWhiteSpace(request.CustomerInvoiceNumber) ? null : request.CustomerInvoiceNumber.Trim(),
            CustomerId = customerId ?? request.CustomerId,
            AssignedAmount = decimal.Round(assignedAmount, 2),
            Note = (request.Note ?? string.Empty).Trim()
        });
        await db.SaveChangesAsync(HttpContext.RequestAborted);

        if (request.SupplierInvoiceId.HasValue)
        {
            await RefreshSupplierInvoicePaymentStatusAsync(tenantId, request.SupplierInvoiceId.Value, HttpContext.RequestAborted);
        }
        else if (!string.IsNullOrWhiteSpace(request.CustomerInvoiceNumber))
        {
            await RefreshCustomerInvoicePaymentStatusAsync(tenantId, request.CustomerInvoiceNumber.Trim(), customerId ?? request.CustomerId, HttpContext.RequestAborted);
        }

        transaction = await db.BankTransactions
            .Where(x => x.BankTransactionId == bankTransactionId && x.TenantId == tenantId)
            .Include(x => x.BankStatementImport)
            .Include(x => x.Assignments).ThenInclude(x => x.SupplierInvoice)
            .Include(x => x.Assignments).ThenInclude(x => x.Customer)
            .FirstAsync(HttpContext.RequestAborted);

        return Ok(MapTransaction(transaction));
    }

    [HttpDelete("assignments/{assignmentId:int}")]
    public async Task<IActionResult> DeleteAssignment(int assignmentId)
    {
        if (!User.IsInRole(TenantRoles.Admin))
        {
            return Forbid();
        }

        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var assignment = await db.BankTransactionAssignments
            .FirstOrDefaultAsync(x => x.BankTransactionAssignmentId == assignmentId && x.TenantId == tenantId, HttpContext.RequestAborted);
        if (assignment is null)
        {
            return NotFound();
        }

        var supplierInvoiceId = assignment.SupplierInvoiceId;
        var customerInvoiceNumber = assignment.CustomerInvoiceNumber;
        var customerId = assignment.CustomerId;
        db.BankTransactionAssignments.Remove(assignment);
        await db.SaveChangesAsync(HttpContext.RequestAborted);

        if (supplierInvoiceId.HasValue)
        {
            await RefreshSupplierInvoicePaymentStatusAsync(tenantId, supplierInvoiceId.Value, HttpContext.RequestAborted);
        }

        if (!string.IsNullOrWhiteSpace(customerInvoiceNumber))
        {
            await RefreshCustomerInvoicePaymentStatusAsync(tenantId, customerInvoiceNumber, customerId, HttpContext.RequestAborted);
        }

        return NoContent();
    }

    private async Task<List<BankInvoiceCandidateDto>> GetSupplierInvoiceCandidatesAsync(int tenantId, BankTransaction transaction)
    {
        var assignments = await db.BankTransactionAssignments
            .Where(x => x.TenantId == tenantId && x.SupplierInvoiceId.HasValue)
            .GroupBy(x => x.SupplierInvoiceId!.Value)
            .Select(x => new { InvoiceId = x.Key, AssignedAmount = x.Sum(y => y.AssignedAmount) })
            .ToListAsync(HttpContext.RequestAborted);
        var assignedLookup = assignments.ToDictionary(x => x.InvoiceId, x => x.AssignedAmount);

        var invoices = await db.Invoices
            .Where(x => x.TenantId == tenantId && x.HasSupplierInvoice)
            .Include(x => x.Lines)
            .OrderByDescending(x => x.InvoiceDate)
            .ToListAsync(HttpContext.RequestAborted);

        return invoices.Select(invoice =>
        {
            var total = decimal.Round(invoice.Lines.Sum(x => x.LineTotal), 2);
            var assigned = assignedLookup.GetValueOrDefault(invoice.InvoiceId);
            return BuildCandidateFromInvoice(
                "SupplierInvoice",
                invoice.InvoiceId,
                invoice.InvoiceNumber,
                null,
                null,
                invoice.SupplierName,
                invoice.InvoiceDate,
                total,
                assigned,
                invoice.Lines.All(x => x.IsPaid),
                transaction);
        }).ToList();
    }

    private async Task<List<BankInvoiceCandidateDto>> GetCustomerInvoiceCandidatesAsync(int tenantId, BankTransaction transaction)
    {
        var grouped = await GetCustomerInvoiceGroupsAsync(tenantId);
        return grouped.Select(group =>
            BuildCandidateFromInvoice(
                "CustomerInvoice",
                null,
                null,
                group.InvoiceNumber,
                group.CustomerId,
                group.CustomerName,
                group.InvoiceDate,
                group.TotalAmount,
                group.AssignedAmount,
                group.IsPaid,
                transaction)).ToList();
    }

    private async Task<BankInvoiceCandidateDto?> GetSupplierInvoiceCandidateAsync(int tenantId, int invoiceId, BankTransaction transaction)
    {
        return (await GetSupplierInvoiceCandidatesAsync(tenantId, transaction)).FirstOrDefault(x => x.SupplierInvoiceId == invoiceId);
    }

    private async Task<BankInvoiceCandidateDto?> GetCustomerInvoiceCandidateAsync(int tenantId, string invoiceNumber, int? customerId, BankTransaction transaction)
    {
        return (await GetCustomerInvoiceCandidatesAsync(tenantId, transaction))
            .FirstOrDefault(x => string.Equals(x.CustomerInvoiceNumber, invoiceNumber, StringComparison.OrdinalIgnoreCase) && x.CustomerId == customerId);
    }

    private async Task<List<CustomerInvoiceGroup>> GetCustomerInvoiceGroupsAsync(int tenantId)
    {
        var allocationRows = await db.LineAllocations
            .Where(x => x.TenantId == tenantId && x.CustomerInvoiceNumber != null)
            .Select(x => new
            {
                x.CustomerId,
                CustomerName = x.Customer.Name,
                InvoiceNumber = x.CustomerInvoiceNumber!,
                InvoiceDate = x.CustomerInvoicedAt ?? x.AllocatedAt,
                Amount = x.ExportedLineTotal > 0m ? x.ExportedLineTotal : x.AllocatedQuantity * x.CustomerUnitPrice,
                IsPaid = x.IsPaid
            })
            .ToListAsync(HttpContext.RequestAborted);

        var workRows = await db.WorkTimeEntries
            .Where(x => x.TenantId == tenantId && x.CustomerInvoiceNumber != null)
            .Select(x => new
            {
                x.CustomerId,
                CustomerName = x.Customer.Name,
                InvoiceNumber = x.CustomerInvoiceNumber!,
                InvoiceDate = x.CustomerInvoicedAt ?? x.CreatedAt,
                Amount = x.ExportedLineTotal > 0m ? x.ExportedLineTotal : (x.HoursWorked * x.HourlyRate) + (x.TravelKilometers * x.TravelRatePerKilometer),
                IsPaid = x.IsPaid
            })
            .ToListAsync(HttpContext.RequestAborted);

        var assigned = await db.BankTransactionAssignments
            .Where(x => x.TenantId == tenantId && x.CustomerInvoiceNumber != null)
            .GroupBy(x => new { x.CustomerInvoiceNumber, x.CustomerId })
            .Select(x => new { x.Key.CustomerInvoiceNumber, x.Key.CustomerId, AssignedAmount = x.Sum(y => y.AssignedAmount) })
            .ToListAsync(HttpContext.RequestAborted);
        var assignedLookup = assigned.ToDictionary(x => $"{x.CustomerId}|{x.CustomerInvoiceNumber}", x => x.AssignedAmount, StringComparer.OrdinalIgnoreCase);

        return allocationRows
            .Select(x => new CustomerInvoiceSourceRow(x.CustomerId, x.CustomerName, x.InvoiceNumber, x.InvoiceDate, x.Amount, x.IsPaid))
            .Concat(workRows.Select(x => new CustomerInvoiceSourceRow(x.CustomerId, x.CustomerName, x.InvoiceNumber, x.InvoiceDate, x.Amount, x.IsPaid)))
            .GroupBy(x => $"{x.CustomerId}|{x.InvoiceNumber}", StringComparer.OrdinalIgnoreCase)
            .Select(group => new CustomerInvoiceGroup
            {
                CustomerId = group.First().CustomerId,
                CustomerName = group.First().CustomerName,
                InvoiceNumber = group.First().InvoiceNumber,
                InvoiceDate = group.Min(x => x.InvoiceDate).Date,
                TotalAmount = decimal.Round(group.Sum(x => x.Amount), 2),
                AssignedAmount = assignedLookup.GetValueOrDefault(group.Key),
                IsPaid = group.All(x => x.IsPaid)
            })
            .OrderByDescending(x => x.InvoiceDate)
            .ToList();
    }

    private async Task RefreshSupplierInvoicePaymentStatusAsync(int tenantId, int invoiceId, CancellationToken cancellationToken)
    {
        var invoice = await db.Invoices.Include(x => x.Lines).FirstOrDefaultAsync(x => x.InvoiceId == invoiceId && x.TenantId == tenantId, cancellationToken);
        if (invoice is null)
        {
            return;
        }

        var totalAmount = decimal.Round(invoice.Lines.Sum(x => x.LineTotal), 2);
        var assignedAmount = await db.BankTransactionAssignments
            .Where(x => x.TenantId == tenantId && x.SupplierInvoiceId == invoiceId)
            .SumAsync(x => (decimal?)x.AssignedAmount, cancellationToken) ?? 0m;
        var isPaid = totalAmount <= 0m || assignedAmount + 0.009m >= totalAmount;
        var paidAt = isPaid
            ? await db.BankTransactionAssignments
                .Where(x => x.TenantId == tenantId && x.SupplierInvoiceId == invoiceId)
                .OrderByDescending(x => x.AssignedAt)
                .Select(x => (DateTime?)x.BankTransaction.BookingDate)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        foreach (var line in invoice.Lines)
        {
            line.IsPaid = isPaid;
            line.PaidAt = paidAt;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task RefreshCustomerInvoicePaymentStatusAsync(int tenantId, string customerInvoiceNumber, int? customerId, CancellationToken cancellationToken)
    {
        var assignmentsQuery = db.BankTransactionAssignments.Where(x => x.TenantId == tenantId && x.CustomerInvoiceNumber == customerInvoiceNumber);
        if (customerId.HasValue)
        {
            assignmentsQuery = assignmentsQuery.Where(x => x.CustomerId == customerId.Value);
        }

        var assignedAmount = await assignmentsQuery.SumAsync(x => (decimal?)x.AssignedAmount, cancellationToken) ?? 0m;
        var latestPaidAt = assignedAmount > 0m
            ? await assignmentsQuery.OrderByDescending(x => x.AssignedAt).Select(x => (DateTime?)x.BankTransaction.BookingDate).FirstOrDefaultAsync(cancellationToken)
            : null;

        var allocationQuery = db.LineAllocations.Where(x => x.TenantId == tenantId && x.CustomerInvoiceNumber == customerInvoiceNumber);
        var workQuery = db.WorkTimeEntries.Where(x => x.TenantId == tenantId && x.CustomerInvoiceNumber == customerInvoiceNumber);
        if (customerId.HasValue)
        {
            allocationQuery = allocationQuery.Where(x => x.CustomerId == customerId.Value);
            workQuery = workQuery.Where(x => x.CustomerId == customerId.Value);
        }

        var allocations = await allocationQuery.ToListAsync(cancellationToken);
        var workEntries = await workQuery.ToListAsync(cancellationToken);

        var totalAmount = decimal.Round(
            allocations.Sum(x => x.ExportedLineTotal > 0m ? x.ExportedLineTotal : x.AllocatedQuantity * x.CustomerUnitPrice)
            + workEntries.Sum(x => x.ExportedLineTotal > 0m ? x.ExportedLineTotal : (x.HoursWorked * x.HourlyRate) + (x.TravelKilometers * x.TravelRatePerKilometer)),
            2);
        var isPaid = totalAmount <= 0m || assignedAmount + 0.009m >= totalAmount;

        foreach (var allocation in allocations)
        {
            allocation.IsPaid = isPaid;
            allocation.PaidAt = isPaid ? latestPaidAt : null;
        }

        foreach (var workEntry in workEntries)
        {
            workEntry.IsPaid = isPaid;
            workEntry.PaidAt = isPaid ? latestPaidAt : null;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static BankTransactionListItemDto MapTransaction(BankTransaction transaction)
    {
        var assignedAmount = decimal.Round(transaction.Assignments.Sum(x => x.AssignedAmount), 2);
        return new BankTransactionListItemDto
        {
            BankTransactionId = transaction.BankTransactionId,
            ImportId = transaction.BankStatementImportId,
            BookingDate = transaction.BookingDate,
            ValueDate = transaction.ValueDate,
            Amount = transaction.Amount,
            BalanceAfterBooking = transaction.BalanceAfterBooking,
            Currency = transaction.Currency,
            CounterpartyName = transaction.CounterpartyName,
            CounterpartyIban = transaction.CounterpartyIban,
            Purpose = transaction.Purpose,
            Reference = transaction.Reference,
            TransactionType = transaction.TransactionType,
            AccountIban = transaction.AccountIban,
            ImportFileName = transaction.BankStatementImport.FileName,
            ImportedAt = transaction.ImportedAt,
            IsIgnored = transaction.IsIgnored,
            IgnoredComment = transaction.IgnoredComment,
            IgnoredAt = transaction.IgnoredAt,
            AssignedAmount = assignedAmount,
            RemainingAmount = CalculateRemainingAmount(transaction.Amount, assignedAmount),
            Assignments = transaction.Assignments
                .OrderByDescending(x => x.AssignedAt)
                .Select(x => new BankTransactionAssignmentDto
                {
                    BankTransactionAssignmentId = x.BankTransactionAssignmentId,
                    BankTransactionId = x.BankTransactionId,
                    AssignmentType = x.SupplierInvoiceId.HasValue ? "SupplierInvoice" : "CustomerInvoice",
                    SupplierInvoiceId = x.SupplierInvoiceId,
                    SupplierInvoiceNumber = x.SupplierInvoice?.InvoiceNumber,
                    CustomerInvoiceNumber = x.CustomerInvoiceNumber,
                    CustomerId = x.CustomerId,
                    PartyName = x.SupplierInvoiceId.HasValue ? x.SupplierInvoice!.SupplierName : x.Customer?.Name ?? "",
                    AssignedAmount = x.AssignedAmount,
                    Note = x.Note,
                    AssignedAt = x.AssignedAt
                })
                .ToList()
        };
    }

    private static decimal CalculateRemainingAmount(decimal transactionAmount, decimal assignedAmount)
        => decimal.Round(Math.Max(0m, Math.Abs(transactionAmount) - assignedAmount), 2);

    private static BankInvoiceCandidateDto BuildCandidateFromInvoice(
        string candidateType,
        int? supplierInvoiceId,
        string? supplierInvoiceNumber,
        string? customerInvoiceNumber,
        int? customerId,
        string partyName,
        DateTime invoiceDate,
        decimal totalAmount,
        decimal assignedAmount,
        bool isPaid,
        BankTransaction transaction)
    {
        var remainingAmount = decimal.Round(Math.Max(0m, totalAmount - assignedAmount), 2);
        var score = CalculateMatchScore(transaction, supplierInvoiceNumber ?? customerInvoiceNumber ?? "", partyName, totalAmount, out var reason);

        return new BankInvoiceCandidateDto
        {
            CandidateType = candidateType,
            SupplierInvoiceId = supplierInvoiceId,
            SupplierInvoiceNumber = supplierInvoiceNumber,
            CustomerInvoiceNumber = customerInvoiceNumber,
            CustomerId = customerId,
            PartyName = partyName,
            InvoiceDate = invoiceDate,
            TotalAmount = totalAmount,
            AssignedAmount = assignedAmount,
            RemainingAmount = remainingAmount,
            IsPaid = isPaid,
            MatchScore = score,
            MatchReason = reason
        };
    }

    private static decimal CalculateMatchScore(BankTransaction transaction, string invoiceNumber, string partyName, decimal totalAmount, out string reason)
    {
        decimal score = 0m;
        var reasons = new List<string>();
        var normalizedPurpose = NormalizeForCompare($"{transaction.Purpose} {transaction.Reference}");
        var normalizedCounterparty = NormalizeForCompare(transaction.CounterpartyName);
        var normalizedInvoice = NormalizeForCompare(invoiceNumber);
        var normalizedParty = NormalizeForCompare(partyName);
        var absoluteAmount = Math.Abs(transaction.Amount);
        var amountDifference = Math.Abs(absoluteAmount - totalAmount);

        if (!string.IsNullOrWhiteSpace(normalizedInvoice) && normalizedPurpose.Contains(normalizedInvoice, StringComparison.OrdinalIgnoreCase))
        {
            score += 65m;
            reasons.Add("Rechnungsnummer im Verwendungszweck");
        }

        if (!string.IsNullOrWhiteSpace(normalizedParty) && (normalizedPurpose.Contains(normalizedParty, StringComparison.OrdinalIgnoreCase) || normalizedCounterparty.Contains(normalizedParty, StringComparison.OrdinalIgnoreCase)))
        {
            score += 25m;
            reasons.Add("Name passt");
        }

        if (amountDifference < 0.01m)
        {
            score += 40m;
            reasons.Add("Betrag exakt");
        }
        else if (amountDifference <= 1m)
        {
            score += 22m;
            reasons.Add("Betrag fast passend");
        }
        else if (amountDifference <= 5m)
        {
            score += 10m;
            reasons.Add("Betrag aehnlich");
        }

        reason = reasons.Count == 0 ? "Manuelle Pruefung" : string.Join(", ", reasons);
        return score;
    }

    private static string NormalizeForCompare(string? value)
    {
        var builder = new StringBuilder();
        foreach (var ch in (value ?? string.Empty).ToUpperInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    private sealed record CustomerInvoiceSourceRow(int CustomerId, string CustomerName, string InvoiceNumber, DateTime InvoiceDate, decimal Amount, bool IsPaid);

    private sealed class CustomerInvoiceGroup
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = "";
        public string InvoiceNumber { get; set; } = "";
        public DateTime InvoiceDate { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal AssignedAmount { get; set; }
        public bool IsPaid { get; set; }
    }
}
