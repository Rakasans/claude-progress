# Protokół postępu

Kanał to issues w repozytorium `Rakasans/claude-progress`. Jedno zadanie = jedno issue.
Aplikacja czyta tylko tytuł, etykiety i treść (body), więc każda aktualizacja to jedno wywołanie API.

| Pole | Znaczenie |
|---|---|
| Tytuł | `Projekt: nazwa zadania [krok/wszystkie]`, np. `FoodApp: edytor przepisu [3/5]`. Bez `[x/y]` pasek jest nieokreślony. |
| Etykieta `in-progress` | Zadanie trwa. Bez powiadomienia. |
| Etykieta `needs-input` | Potrzebna decyzja użytkownika. **Powiadomienie.** Pierwsza linia body to treść powiadomienia. |
| Etykieta `done` | Zadanie zakończone, issue zamknięte. **Powiadomienie.** Pierwsza linia body to podsumowanie. |
| Body | Aktualny status jednym, dwoma zdaniami. Pierwsza linia jest pokazywana w aplikacji. |

Zmiana treści pytania przy etykiecie `needs-input` też wywołuje nowe powiadomienie.

## Polecenia (`gh api`)

```bash
R=repos/Rakasans/claude-progress

# Start — zapamiętaj numer issue
N=$(gh api $R/issues -f title="FoodApp: edytor przepisu [0/5]" \
  -f body="Zaczynam: edytor przepisu." -f "labels[]=in-progress" --jq .number)

# Po każdym etapie
gh api -X PATCH $R/issues/$N -f title="FoodApp: edytor przepisu [3/5]" \
  -f body="Gotowe: formularz i walidacja. Teraz: zapis do bazy."

# Potrzebna decyzja
gh api -X PATCH $R/issues/$N -f body="Pytanie: trzymać zdjęcia lokalnie czy w chmurze? Szczegóły w czacie." \
  -f "labels[]=needs-input"

# Po odpowiedzi wracamy do pracy
gh api -X PATCH $R/issues/$N -f "labels[]=in-progress" -f body="Kontynuuję: zapis zdjęć lokalnie."

# Koniec
gh api -X PATCH $R/issues/$N -f title="FoodApp: edytor przepisu [5/5]" \
  -f body="Edytor przepisu gotowy, testy przechodzą." -f "labels[]=done" -f state=closed
```

`labels[]` w `PATCH` zastępuje wszystkie etykiety, więc zawsze jest dokładnie jedna.

## Fragment do `CLAUDE.md` innych projektów

```markdown
## Raportowanie postępu
Przy zadaniu na więcej niż ~3 etapy raportuj postęp w repozytorium Rakasans/claude-progress
zgodnie z jego PROTOCOL.md: na starcie załóż issue (etykieta in-progress, tytuł "<Projekt>: <zadanie> [0/N]"),
po każdym etapie zaktualizuj tytuł i body, przy pytaniu do użytkownika ustaw needs-input
(pierwsza linia body = pytanie), na końcu ustaw done i zamknij issue.
```
