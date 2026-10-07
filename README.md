# Trakt Similar Provider

Plugin Jellyfin **12.1** che registra **"Trakt"** come fornitore nativo di:
- **Elementi simili** (film e serie), selezionabile in Dashboard → Librerie → Fornitori di
  elementi simili, accanto a TheMovieDb e Local Genre/Tag
- **Suggerimenti personalizzati** (tab Suggerimenti), tramite `/recommendations/movies|shows`

Dipende dal plugin ufficiale [jellyfin-plugin-trakt](https://github.com/jellyfin/jellyfin-plugin-trakt)
(da cui legge, via reflection, le credenziali dell'account collegato), ma non lo referenzia a
tempo di compilazione: se non è installato, il fornitore "Simili" continua comunque a
funzionare con dati pubblici; solo i Suggerimenti personalizzati restano disabilitati.

## Endpoint usati

| Funzione | Endpoint | Autenticazione |
|---|---|---|
| Simili a un film | `GET /movies/{imdbId}/related` | Solo client_id (dato pubblico) |
| Simili a una serie | `GET /shows/{imdbId}/related` | Solo client_id (dato pubblico) |
| Suggerimenti film | `GET /recommendations/movies?watchnow=...&ignore_watched=true` | client_id + token utente |
| Suggerimenti serie | `GET /recommendations/shows?watchnow=...&ignore_watched=true` | client_id + token utente |

**Nota sul client_id**: Trakt richiede un'app registrata su ogni chiamata, anche pubblica. Non
avendo una propria app (la creazione di nuove app Trakt richiede ora un abbonamento VIP), questo
plugin riusa il `client_id` pubblico del plugin ufficiale jellyfin-plugin-trakt (visibile nel
loro repository open source). È una scelta consapevole, concordata esplicitamente, non un
dettaglio nascosto: comporta che le chiamate di questo plugin condividano il rate limit
dell'app Trakt del plugin ufficiale.

## Limiti noti

- **Gli endpoint `/movies|shows/{id}/...` di Trakt accettano solo Trakt ID, slug o IMDb ID, non
  TMDb ID.** Un item locale senza IMDb id nei suoi metadati non produrrà risultati "Simili" da
  Trakt (silenziosamente, nessun errore — il fornitore passa semplicemente nessun risultato per
  quell'item).
- **Nessun concetto di "simile a X" nelle raccomandazioni Trakt**: a differenza del fornitore
  "Simili" (dati reali per-titolo), il fornitore Suggerimenti restituisce la stessa lista
  personalizzata per ogni film/serie usato come seme dalla tab Suggerimenti — le diverse
  categorie mostreranno quindi gli stessi titoli. Limite noto e accettato, non un bug.
- **Nessun fallback automatico**: se l'utente Trakt selezionato in configurazione non è valido
  o il token è scaduto, la tab Suggerimenti risulterà vuota (il core usa un solo fornitore batch
  alla volta — non c'è composizione con "Local Genre/Tag").
- **Lettura via reflection del plugin ufficiale**: non essendoci un riferimento a tempo di
  compilazione, un aggiornamento del plugin ufficiale che rinomini `PluginConfiguration`,
  `TraktUsers`, `LinkedMbUserId` o `AccessToken` romperebbe silenziosamente questa lettura
  (gestito con try/catch e log di warning, mai un crash).
- **Non compilato in questa sessione di sviluppo** (nessun accesso a nuget.org nell'ambiente
  usato): ogni API è stata verificata manualmente contro il sorgente reale di Jellyfin 12.1 e
  del plugin Trakt ufficiale, ma serve comunque una build pulita + test reale prima dell'uso
  in produzione.

## Configurazione

Dashboard → Plugin → Trakt Similar Provider:
- **Utente Trakt da usare per i Suggerimenti**: scegli tra gli utenti Jellyfin risultanti
  collegati a Trakt nel plugin ufficiale (elenco popolato automaticamente)
- **Filtro watchnow**: preferenza di disponibilità streaming applicata alle raccomandazioni
- Durate cache per "Simili" (giorni, cache nativa di Jellyfin) e Suggerimenti (ore, cache
  interna di questo plugin)

Il fornitore "Simili" va poi attivato per tipo di contenuto in Dashboard → Librerie → [la tua
libreria] → Fornitori di elementi simili, selezionando "Trakt".

## Build

```bash
dotnet restore
dotnet build -c Release
```

## Pubblicare come repository Jellyfin installabile da UI

1. Crea un repository GitHub tuo (es. `tuoutente/jellyfin-plugin-trakt-similar-provider`) e
   pushaci questo progetto, branch principale `main`
2. I workflow in `.github/workflows/` sono già pronti:
   - `build.yaml`: verifica di build su ogni push/PR
   - `publish.yaml`: alla pubblicazione di una GitHub Release, builda, carica gli asset
     (zip + checksum md5/sha256) e rigenera automaticamente `manifest.json` nel branch
     principale, usando [Kevinjil/jellyfin-plugin-repo-action](https://github.com/Kevinjil/jellyfin-plugin-repo-action)
     (verificato: stesso meccanismo usato da Jellyfin.Xtream, plugin comunitario reale)
3. Crea una Release su GitHub (tag es. `v0.1.0.0`) per innescare `publish.yaml`
4. In Jellyfin: Dashboard → Plugin → Repository → aggiungi
   `https://raw.githubusercontent.com/tuoutente/jellyfin-plugin-trakt-similar-provider/main/manifest.json`
5. Il plugin "Trakt Similar Provider" apparirà nel catalogo plugin, installabile dalla UI
