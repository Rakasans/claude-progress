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

## Budowanie

GitHub Actions buduje `.exe` przy każdej zmianie w `src/` i publikuje wydanie.
Lokalnie: `dotnet publish src/ClaudeProgress -c Release -r win-x64 --self-contained -p:PublishSingleFile=true`.
