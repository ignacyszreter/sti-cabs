using LegacyFighter.Cabs.Claims.Storage;
using LegacyFighter.Cabs.Entity;
using LegacyFighter.Cabs.Repository;
using LegacyFighter.Cabs.Service;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace LegacyFighter.Cabs.Claims.Acl;

public class LegacyCabs
{
  private readonly SqLiteDbContext _context;
  private readonly IClaimRepository _claims;
  private readonly ClaimNumberGenerator _claimNumbers;
  private readonly IAwardsService _awards;
  private readonly IClientNotificationService _clientNotifications;
  private readonly IDriverNotificationService _driverNotifications;

  public LegacyCabs(
    SqLiteDbContext context,
    IClaimRepository claims,
    ClaimNumberGenerator claimNumbers,
    IAwardsService awards,
    IClientNotificationService clientNotifications,
    IDriverNotificationService driverNotifications)
  {
    _context = context;
    _claims = claims;
    _claimNumbers = claimNumbers;
    _awards = awards;
    _clientNotifications = clientNotifications;
    _driverNotifications = driverNotifications;
  }

  public async Task<ClaimRecord> Register(NewClaim newClaim, Instant createdAt)
  {
    _ = await _context.Clients.FindAsync(newClaim.ClientId)
        ?? throw new InvalidOperationException("Client does not exists");
    _ = await _context.Transits.FindAsync(newClaim.TransitId)
        ?? throw new InvalidOperationException("Transit does not exists");
    var claim = new Claim
    {
      CreationDate = createdAt,
      Reason = newClaim.Reason,
      IncidentDescription = newClaim.IncidentDescription,
      Status = newClaim.IsDraft ? Claim.Statuses.Draft : Claim.Statuses.New
    };
    claim.ClaimNo = await _claimNumbers.Generate(claim);
    var entry = _context.Claims.Add(claim);
    entry.Property("OwnerId").CurrentValue = newClaim.ClientId;
    entry.Property("TransitId").CurrentValue = newClaim.TransitId;
    await _context.SaveChangesAsync();
    return new ClaimRecord
    {
      ClaimId = claim.Id!.Value,
      ClaimNo = claim.ClaimNo,
      ClaimantId = newClaim.ClientId,
      TransitId = newClaim.TransitId,
      Status = newClaim.IsDraft ? ClaimStatus.Draft : ClaimStatus.New,
      CreatedAt = createdAt,
      Reason = newClaim.Reason,
      IncidentDescription = newClaim.IncidentDescription
    };
  }

  public async Task Apply(long claimId, string claimNo, long claimantId, Resolution resolution, Instant when)
  {
    var claim = await Find(claimId);
    var refunded = resolution.Decision == Decision.Refunded;
    claim.Status = refunded ? Claim.Statuses.Refunded : Claim.Statuses.Escalated;
    claim.CompletionMode = refunded ? Claim.CompletionModes.Automatic : Claim.CompletionModes.Manual;
    claim.CompletionDate = when;
    claim.ChangeDate = when;
    Send(claimNo, resolution);
    if (resolution.SpecialMiles > 0)
    {
      await _awards.RegisterSpecialMiles(claimantId, resolution.SpecialMiles);
    }
  }

  public async Task MarkInProcess(long claimId)
  {
    var claim = await Find(claimId);
    claim.Status = Claim.Statuses.InProcess;
  }

  public async Task<ClaimRecord> View(long claimId)
  {
    return RecordOf(await Find(claimId));
  }

  public async Task<List<ClaimantRecord>> AllClaimants()
  {
    return await _context.Clients
      .Select(client => new ClaimantRecord
      {
        ClientId = client.Id!.Value,
        IsVip = client.Type == Client.Types.Vip,
        OrderedTransits = _context.Transits.Count(transit => transit.Client == client)
      })
      .ToListAsync();
  }

  public async Task<List<ClaimedTransitRecord>> AllClaimedTransits()
  {
    return await _context.Transits
      .Where(transit => transit.Client != null)
      .Select(transit => new ClaimedTransitRecord
      {
        TransitId = transit.Id!.Value,
        ClaimantId = transit.Client.Id!.Value,
        DriverId = transit.Driver == null ? null : transit.Driver.Id,
        Fare = transit.Price == null ? null : transit.Price.IntValue
      })
      .ToListAsync();
  }

  private static ClaimRecord RecordOf(Claim claim)
  {
    return new ClaimRecord
    {
      ClaimId = claim.Id!.Value,
      ClaimNo = claim.ClaimNo,
      ClaimantId = claim.Owner.Id!.Value,
      TransitId = claim.Transit.Id!.Value,
      Status = StatusOf(claim.Status!.Value),
      CompletionMode = claim.CompletionMode == null ? null : ModeOf(claim.CompletionMode.Value),
      CreatedAt = claim.CreationDate,
      CompletedAt = claim.CompletionDate,
      Reason = claim.Reason,
      IncidentDescription = claim.IncidentDescription
    };
  }

  private static ClaimStatus StatusOf(Claim.Statuses status)
  {
    return status switch
    {
      Claim.Statuses.Draft => ClaimStatus.Draft,
      Claim.Statuses.New => ClaimStatus.New,
      Claim.Statuses.InProcess => ClaimStatus.InProcess,
      Claim.Statuses.Refunded => ClaimStatus.Refunded,
      Claim.Statuses.Escalated => ClaimStatus.Escalated,
      _ => ClaimStatus.Rejected
    };
  }

  private static ClaimCompletionMode ModeOf(Claim.CompletionModes mode)
  {
    return mode == Claim.CompletionModes.Automatic ? ClaimCompletionMode.Automatic : ClaimCompletionMode.Manual;
  }

  private void Send(string claimNo, Resolution resolution)
  {
    switch (resolution.Ask)
    {
      case Ask.ClientAboutRefund:
        _clientNotifications.NotifyClientAboutRefund(claimNo, resolution.Recipient);
        break;
      case Ask.ClientForMoreInformation:
        _clientNotifications.AskForMoreInformation(claimNo, resolution.Recipient);
        break;
      case Ask.DriverForDetails:
        _driverNotifications.AskDriverForDetailsAboutClaim(claimNo, resolution.Recipient);
        break;
    }
  }

  private async Task<Claim> Find(long claimId)
  {
    return await _claims.Find(claimId) ?? throw new InvalidOperationException("Claim does not exists");
  }
}
