Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$process = Get-Process PedalTrainer -ErrorAction Stop
$root = [System.Windows.Automation.AutomationElement]::RootElement
$windowCondition = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $process.Id)
$window = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $windowCondition)

if ($null -eq $window) { Write-Host "janela nao encontrada"; exit 1 }

"janela: {0:N0} x {1:N0}" -f $window.Current.BoundingRectangle.Width, $window.Current.BoundingRectangle.Height

$itemCondition = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
    [System.Windows.Automation.ControlType]::ListItem)
$items = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $itemCondition)

foreach ($item in $items) {
    $rect = $item.Current.BoundingRectangle
    "{0,-18} x={1,6:N0} w={2,4:N0} right={3,6:N0}" -f $item.Current.Name, $rect.X, $rect.Width, ($rect.X + $rect.Width)
}
