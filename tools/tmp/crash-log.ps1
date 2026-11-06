$since = (Get-Date).AddMinutes(-15)

$events = Get-WinEvent -FilterHashtable @{ LogName = 'Application'; StartTime = $since } -ErrorAction SilentlyContinue |
    Where-Object { $_.ProviderName -match 'NET|Application Error' } |
    Select-Object -First 3

if ($events.Count -eq 0) {
    "nenhum evento encontrado"
}

foreach ($event in $events) {
    "==== $($event.TimeCreated) $($event.ProviderName) ===="
    $event.Message
    ""
}
