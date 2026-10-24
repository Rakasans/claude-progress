# Claude Progress

Mała aplikacja w zasobniku systemowym Windows. Pokazuje postęp zadań, nad którymi pracuje Claude,
i wyświetla powiadomienie, kiedy potrzebna jest Twoja decyzja albo zadanie się kończy.

Kanałem są issues w tym repozytorium. Format opisuje [PROTOCOL.md](PROTOCOL.md).

## Instalacja

1. Pobierz `ClaudeProgress.exe` z najnowszego wydania w zakładce **Releases**. Nie trzeba instalować .NET.
2. Utwórz token: GitHub → Settings → Developer settings → **Fine-grained tokens** → Generate new token.
   - Repository access: *Only select repositories* → `claude-progress`
   - Permissions → Repository → **Issues: Read-only** (nic więcej)
3. Uruchom `ClaudeProgress.exe` i wklej token. Trafi do Menedżera poświadczeń Windows.
4. Opcjonalnie: prawy klik na ikonę → **Uruchamiaj z Windows**.

Windows SmartScreen może ostrzec przy pierwszym uruchomieniu (plik nie jest podpisany): *Więcej informacji → Uruchom mimo to*.

## Obsługa

- **Lewy klik** na ikonę: okienko z listą zadań i paskami postępu. Klik w zadanie otwiera issue.
- **Prawy klik**: odśwież, zmień token, autostart, zakończ.
- Kolor ikony: szary — nic się nie dzieje, niebieski — praca w toku, pomarańczowy — czekam na Ciebie, czerwony — błąd.
- Sprawdzanie co 30 s (z ETag, więc niezmienione dane nie zużywają limitu API).

Ustawienia (repozytorium, interwał) są w `%APPDATA%\ClaudeProgress\settings.json`.

## Wentylator RGB (SignalRGB)

Jeden wentylator może pokazywać stan: niebieski — praca, żółty (pulsuje) — potrzebna decyzja,
zielony przez 2 minuty — zakończone, potem kolor bazowy jak reszta komputera.

1. Skopiuj [`signalrgb/ClaudeStatus.html`](signalrgb/ClaudeStatus.html) do `Dokumenty\WhirlwindFX\Effects`
   i uruchom ponownie SignalRGB.
2. Włącz efekt **Claude Status**. Cały komputer dostaje kolor bazowy (domyślnie czerwony, do zmiany w ustawieniach efektu).
3. W **Układy (Layouts)** przesuń solo wentylator (Lian Li, Channel 1) w lewy górny róg płótna,
   a resztę urządzeń poza ten róg. W ustawieniach efektu ustaw **Podgląd → Praca**: róg robi się niebieski,
   więc od razu widać, czy wentylator stoi w strefie. Potem wróć do **Wylaczony**.
   Położenie i rozmiar strefy zmienisz suwakami „Strefa statusu”.

Aplikacja wysyła stan na `http://localhost:16034/canvas/event` przy każdym sprawdzeniu.
Wyłączysz to w menu ikony: **Wentylator RGB (SignalRGB)**. Bez sygnału przez 3 minuty efekt sam wraca do koloru bazowego.

## Budowanie

GitHub Actions buduje `.exe` przy każdej zmianie w `src/` i publikuje wydanie.
Lokalnie: `dotnet publish src/ClaudeProgress -c Release -r win-x64 --self-contained -p:PublishSingleFile=true`.
