# Usage example:
# powershell -WindowStyle Hidden -ExecutionPolicy Bypass -File "input-OFF.ps1"

$epochMillis = [Math]::Floor((([datetime]::UtcNow)-(get-date "1/1/1970")).TotalMilliseconds)

$logFilePath = "C:\Users\Ovidiu\Desktop\EventsToSpadNext\resources\events\demo\inputs"
$logFileName = "stream-deck-inputs-$epochMillis.in"
$logFile = "$logFilePath\$logFileName"

$valueFilePath = "C:\Users\Ovidiu\Desktop\EventsToSpadNext\resources\events\demo\values\off.val"
$value = Get-Content -Path $valueFilePath

New-Item -path $logFile -ItemType "file"
Add-Content $logFile $value

