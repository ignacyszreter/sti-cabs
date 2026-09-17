# Demo: zdarzenie synchroniczne wiąże awarie

Czas: 5 minut. Pokaz na żywo, bez commita.

Moduł reklamacji ma już własne dane. Jest autonomiczny w danych.
To demo pokazuje, że w działaniu nadal nie jest.

## Krok 1 — zepsuj handler modułu

W `src/Cabs/Claims/Sync/ClaimsCopy.cs`, na początku metody obsługującej
`ClientTypeChanged`, wstaw jedną linię:

```csharp
throw new InvalidOperationException("Kopia danych modułu jest chwilowo nieczynna");
```

## Krok 2 — uruchom aplikację

```bash
dotnet run --project src/Cabs
```

## Krok 3 — załóż klienta

```bash
curl -s -X POST http://localhost:5000/clients \
  -H 'Content-Type: application/json' \
  -d '{"name":"Jan","lastName":"Kowalski","type":0,"defaultPaymentType":0}'
```

Rejestracja też publikuje `ClientTypeChanged`, więc i ona zwróci 500.
Zdejmij wyjątek na czas rejestracji albo weź identyfikator klienta
z bazy. Zapisz identyfikator jako `ID`.

## Krok 4 — podnieś klienta do VIP-a

```bash
curl -i -X POST http://localhost:5000/clients/ID/upgrade
```

Odpowiedź: **500**.

## Krok 5 — sprawdź, czy klient jest VIP-em

```bash
curl -s http://localhost:5000/clients/ID
```

Odpowiedź zawiera `"type":0`, czyli `Normal`. Klient **nie** został VIP-em.

## Co widać

`TransactionalClientService.UpgradeToVip` otwiera transakcję, woła
`ClientService.UpgradeToVip`, i dopiero potem zatwierdza. Publikacja
zdarzenia dzieje się w środku. Wyjątek handlera przelatuje przez
publikację, przez serwis, aż do kontrolera. Transakcja nigdy nie
dostaje `Commit`, więc `Dispose` ją wycofuje.

Awaria kopii danych modułu reklamacji zatrzymała zmianę typu klienta —
operację, która z reklamacjami nie ma nic wspólnego.

To jest obraz całego bloku: **bąbel autonomiczny w danych, nieautonomiczny
w działaniu.** Kolejny krok, outbox, rozcina to wiązanie.

## Sprzątanie

Usuń wstawioną linię.
