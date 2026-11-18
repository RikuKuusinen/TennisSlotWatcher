# TennisSlotWatcher

Azure Function (.NET 10, isolated worker) that watches the public play.fi booking calendar for
**Laajasalon Tennishallit** and sends a push notification when a **new** tennis court slot appears.
It never logs in and never books anything.

## Behaviour

| What | Default |
|---|---|
| Days checked | today + next 28 days (the furthest play.fi publishes), **Mon, Tue and Thu only** (no Wed/Fri/Sat/Sun) |
| Courts | T1-T6 (`resid` 1-6), tennis (`p_laji=1`) |
| Window | bookable time must lie inside **18:00-21:00** |
| Minimum length | **60 min** inside that window |
| Schedule | every 2 h from 10:00 to 20:00 (`0 0 10-20/2 * * *`, Helsinki time) |
| Alert | one ntfy push per run listing all *new* slots; tapping it opens the first slot's booking page |

A free block is evaluated by its part inside the window: a free block 17:00-19:30 counts as a 18:00-19:30 slot.
(Assumption: you can start a booking inside a longer free block. If the site disallows that, tighten the filter in `SlotFilter`.)

De-duplication: the set of currently matching slots is stored in a blob (`slotwatcher/alerted-slots.json`
in the Function App's storage account). You are alerted once per slot; if it is booked and later freed
(cancellation) you are alerted again. The first run alerts for everything currently free.

If a day's page cannot be read or does not look like a calendar (layout change, error page) the error is logged and
that day's previous state is kept, so there are no spurious alerts.

## Settings

Everything binds from the `Watcher` section; app settings use `__` as separator. Only the ntfy topic is required.

| Setting | Default |
|---|---|
| `Watcher__Ntfy__Topic` | *(required)* long random topic name - treat as a secret |
| `Watcher__Ntfy__Server` / `Watcher__Ntfy__Token` | `https://ntfy.sh` / none |
| `Watcher__DaysAhead` | `28` (also set in local.settings.json; max; later dates return today's page and are flagged as failed) |
| `Watcher__Weekdays` | `Monday,Tuesday,Thursday` (comma-separated English day names) |
| `Watcher__WindowStart` / `Watcher__WindowEnd` | `18:00:00` / `21:00:00` |
| `Watcher__MinDurationMinutes` | `60` |
| `Watcher__CourtIds` | `1,2,3,4,5,6` (comma-separated) |
| `CheckSchedule` | `0 0 10-20/2 * * *` |
| `WEBSITE_TIME_ZONE` | `Europe/Helsinki` (Linux) or `FLE Standard Time` (Windows) - makes the cron run in Helsinki time |

> `WEBSITE_TIME_ZONE` is not supported on the Flex Consumption plan. There, leave it unset and write `CheckSchedule` in UTC
> (e.g. `0 0 8-18/2 * * *` for winter time). The code itself always uses `Watcher__TimeZone` (Europe/Helsinki) to decide "today".

## Phone setup (ntfy)

1. Install the **ntfy** app (Android / iOS).
2. Subscribe to your secret topic name, e.g. `tennis-laajasalo-7f3k9x2m1q8w`.
3. Put the same name into `Watcher__Ntfy__Topic`.

## Run locally

Requirements: .NET 10 SDK, [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
(`winget install Microsoft.Azure.FunctionsCoreTools`), Azurite for local storage (`azurite` in another terminal).

```powershell
# set Watcher__Ntfy__Topic in src/TennisSlotWatcher/local.settings.json (it is committed, so don't commit your real topic - see below)
cd src/TennisSlotWatcher
func start
```

In Debug builds (the default for `func start`) the timer also runs **once immediately on startup**, and the console prints
the matching slots (`[NEW]` marks the ones that trigger a notification). Release builds / Azure do not run on startup.

Dry run (lists matches, sends nothing, touches no state) - locally no key needed:

```
GET http://localhost:7071/api/check              # dryRun=true is the default
GET http://localhost:7071/api/check?dryRun=false # behaves exactly like the timer
```

## Tests

```powershell
dotnet test
```

## Deploy

Any Functions plan works (Consumption / Flex Consumption). Example with Flex Consumption:

```powershell
az functionapp create -g <rg> -n <app-name> --storage-account <storage> --flexconsumption-location westeurope --runtime dotnet-isolated --runtime-version 10.0
az functionapp config appsettings set -g <rg> -n <app-name> --settings Watcher__Ntfy__Topic=<topic> "CheckSchedule=0 0 8-18/2 * * *"
cd src/TennisSlotWatcher; func azure functionapp publish <app-name>
```

(Flex Consumption: UTC schedule, see note above. For a Y1/Windows plan use the default schedule plus `WEBSITE_TIME_ZONE`.)
Optionally attach Application Insights and add an alert on errors, e.g. "calendar layout changed".

> **Note:** local.settings.json is committed with an empty ntfy topic. After the first commit, run
> git update-index --skip-worktree src/TennisSlotWatcher/local.settings.json so your real topic is never committed by accident.

