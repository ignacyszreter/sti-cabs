using LegacyFighter.Cabs.Claims;
using NUnit.Framework;

namespace LegacyFighter.CabsTests.Claims;

public class ClaimResolverTest
{
  private const long ClientId = 7;
  private const long DriverId = 3;
  private const int Threshold = 10000;

  private static readonly ClaimPolicy Policy = new(Threshold, TransitsForAutomaticRefund: 3);

  [Test]
  public void EscalatesASecondClaimOnTheSameTransitWithoutAskingAnyone()
  {
    var resolution = ClaimResolver.Resolve(AClaim(claimsOnThisTransit: 2), Policy);

    Assert.That(resolution, Is.EqualTo(new Resolution(Decision.Escalated, Ask.Nobody, null, 0)));
  }

  [Test]
  public void RefundsTheThirdClaimWhateverTheFare()
  {
    var resolution = ClaimResolver.Resolve(AClaim(claimsByClaimant: 3, fare: Threshold), Policy);

    Assert.That(resolution, Is.EqualTo(new Resolution(Decision.Refunded, Ask.ClientAboutRefund, ClientId, 0)));
  }

  [Test]
  public void RefundsAFrequentVipOnACheapTransitWithTenMiles()
  {
    var resolution = ClaimResolver.Resolve(AClaim(vip: true, fare: Threshold - 1), Policy);

    Assert.That(resolution, Is.EqualTo(new Resolution(Decision.Refunded, Ask.ClientAboutRefund, ClientId, 10)));
  }

  [Test]
  public void AsksTheDriverAboutAFrequentVipOnAnExpensiveTransit()
  {
    var resolution = ClaimResolver.Resolve(AClaim(vip: true, fare: Threshold), Policy);

    Assert.That(resolution, Is.EqualTo(new Resolution(Decision.Escalated, Ask.DriverForDetails, DriverId, 0)));
  }

  [Test]
  public void RefundsAFrequentClaimantWithThreeOrderedTransitsOnACheapTransit()
  {
    var resolution = ClaimResolver.Resolve(AClaim(orderedTransits: 3, fare: Threshold - 1), Policy);

    Assert.That(resolution, Is.EqualTo(new Resolution(Decision.Refunded, Ask.ClientAboutRefund, ClientId, 0)));
  }

  [Test]
  public void AsksAFrequentClaimantWithThreeOrderedTransitsForMoreInformationOnAnExpensiveTransit()
  {
    var resolution = ClaimResolver.Resolve(AClaim(orderedTransits: 3, fare: Threshold), Policy);

    Assert.That(resolution, Is.EqualTo(new Resolution(Decision.Escalated, Ask.ClientForMoreInformation, ClientId, 0)));
  }

  [Test]
  public void AsksTheDriverAboutAFrequentClaimantWithTwoOrderedTransitsUsingTheClientId()
  {
    var resolution = ClaimResolver.Resolve(AClaim(orderedTransits: 2), Policy);

    Assert.That(resolution, Is.EqualTo(new Resolution(Decision.Escalated, Ask.DriverForDetails, ClientId, 0)));
  }

  private static ClaimToResolve AClaim(
    bool vip = false,
    int orderedTransits = 3,
    int fare = Threshold - 1,
    int claimsByClaimant = 4,
    int claimsOnThisTransit = 1)
  {
    return new ClaimToResolve(
      "0-07/01/2019",
      new Claimant(ClientId, vip, orderedTransits),
      new ClaimedTransit(TransitId: 12, fare, DriverId),
      claimsByClaimant,
      claimsOnThisTransit);
  }
}
