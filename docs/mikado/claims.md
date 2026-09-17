# Mikado: usunięcie encji Claim ze starego modelu

Cel: `src/Cabs/Entity/Claim.cs` i mapowanie tabeli `Claims`
w `SqLiteDbContext` znikają. Moduł reklamacji trzyma reklamacje u siebie.

## Ruch naiwny

Usuń `Entity/Claim.cs`, `DbSet<Claim> Claims`, blok
`modelBuilder.Entity<Claim>(...)` i `HasMany(c => c.Claims)`
z mapowania klienta. Zbuduj.

Wynik: **20 błędów kompilacji w 7 plikach**. Wszystkie to `CS0246:
The type or namespace name 'Claim' could not be found`.

| Plik | Błędy | Czego dotyczy |
| --- | --- | --- |
| `Repository/EfCoreClaimRepository.cs` | 10 | interfejs `IClaimRepository` i jego implementacja |
| `Claims/Acl/LegacyCabs.cs` | 4 | zapis i odczyt starej tabeli przez ACL modułu |
| `Dto/ClaimDto.cs` | 3 | stary kształt odpowiedzi HTTP |
| `Entity/ClaimAttachment.cs` | 1 | załącznik wskazujący na reklamację |
| `Entity/Client.cs` | 1 | kolekcja `Client.Claims` |
| `Service/ClaimNumberGenerator.cs` | 1 | numer reklamacji liczony po starej tabeli |

Lista nie jest pełna. Kompilator zatrzymuje się na pierwszym projekcie,
więc jeden węzeł wychodzi dopiero po naprawie powyższych:

| Plik | Co robi |
| --- | --- |
| `Service/AwardsServiceImpl.cs` | `RemoveMiles` porządkuje mile inaczej, gdy klient ma 3 reklamacje lub więcej |

To najciekawszy węzeł całego grafu. Reguła programu lojalnościowego
czyta liczbę reklamacji przez `Client.Claims` — kolekcję ORM-a, której
nikt nie widzi w sygnaturach.

## Liście grafu

Zacznij od dołu. Każdy punkt to osobny, zielony commit.

1. **Moduł generuje numer reklamacji.** `ClaimNumbers` liczy reklamacje
   we własnej bazie. `ClaimNumberGenerator` przestaje być potrzebny.
2. **Identyfikator reklamacji nadaje własna tabela modułu.**
   Do tej pory nadawała go stara tabela.
3. **Moduł przestaje pisać do starej tabeli.** `LegacyCabs.Register`
   znika. ACL zostaje przy powiadomieniach i milach.
4. **`DriverReportController` bierze reklamację z fasady modułu**
   (`ClaimsFacade.ClaimOf(transitId)`), a nie z `IClaimRepository`.
   `TransitDto` niesie `ClaimView` modułu zamiast `ClaimDto`.
5. **`AwardsServiceImpl` liczy reklamacje przez port modułu**
   (`ClaimsQueries.ClaimsBy(clientId)`). Port czytający jest osobny od
   fasady, bo fasada woła program lojalnościowy po mile — para
   `fasada ↔ program lojalnościowy` zapętliłaby kontener.
6. **`ClaimsMigration` znika razem ze starą tabelą.** Zrobiła swoje.
   Jej kod i test zostają na tagu `rekl-autonomia-migracja`.
7. **Znikają: `IClaimRepository`, `EfCoreClaimRepository`, `ClaimDto`,
   `ClaimNumberGenerator`, `ClaimAttachment`, `IClaimAttachmentRepository`,
   `Client.Claims`.**
8. **Ruch naiwny powtórzony.** Teraz kompiluje się na zielono.

## Czego uczy ta lista

Cztery z siedmiu plików to stary model, który nikt już nie czyta.
Trzymały się tabeli, nie reguł biznesowych. Moduł mógł dostać własne
dane dopiero wtedy, kiedy jego ACL przestał czytać stary model —
to był prawdziwy korzeń, a nie mapowanie EF.

Ósmy plik, `AwardsServiceImpl`, pokazuje drugą lekcję: lista błędów
kompilacji z pierwszego ruchu jest wstępem, nie mapą. Graf rośnie,
kiedy po nim wchodzisz.
