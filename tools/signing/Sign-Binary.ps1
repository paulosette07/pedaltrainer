<#
.SYNOPSIS
    Assina digitalmente um executavel com um certificado de code signing (.pfx) e carimbo de tempo.

.DESCRIPTION
    Usado pelo workflow de release para remover o aviso do SmartScreen do PedalTrainer.exe.
    A chave do certificado nunca fica no repositorio: ela chega em base64 por variavel de
    ambiente/secret (PEDALTRAINER_PFX_BASE64) e e gravada num arquivo temporario descartado
    no final, inclusive quando a assinatura falha.

    O carimbo de tempo (RFC 3161) e obrigatorio: sem ele a assinatura expira junto com o
    certificado e o binario antigo volta a ser "desconhecido" para o Windows.

.PARAMETER Path
    Executavel a assinar. Assine o binario final publicado, nunca o intermediario.

.PARAMETER PfxBase64
    Conteudo do .pfx em base64. Padrao: $env:PEDALTRAINER_PFX_BASE64.

.PARAMETER PfxPassword
    Senha do .pfx. Padrao: $env:PEDALTRAINER_PFX_PASSWORD.

.PARAMETER TimestampUrl
    Servidor de carimbo de tempo RFC 3161.

.EXAMPLE
    $env:PEDALTRAINER_PFX_BASE64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes('cert.pfx'))
    $env:PEDALTRAINER_PFX_PASSWORD = 'senha'
    ./tools/signing/Sign-Binary.ps1 -Path dist/publish/PedalTrainer.exe
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Path,

    [string] $PfxBase64 = $env:PEDALTRAINER_PFX_BASE64,

    [string] $PfxPassword = $env:PEDALTRAINER_PFX_PASSWORD,

    [string] $TimestampUrl = 'http://timestamp.digicert.com'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-SignToolPath {
    $kitsRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    if (Test-Path -LiteralPath $kitsRoot) {
        # @() obrigatorio: com um unico resultado o pipeline devolve string e [0] pegaria o
        # primeiro caractere ('C') em vez do caminho.
        $candidates = @(
            Get-ChildItem -LiteralPath $kitsRoot -Directory |
                Where-Object { $_.Name -match '^\d+\.\d+\.\d+\.\d+$' } |
                Sort-Object { [version] $_.Name } -Descending |
                ForEach-Object { Join-Path $_.FullName 'x64\signtool.exe' } |
                Where-Object { Test-Path -LiteralPath $_ }
        )

        if ($candidates.Count -gt 0) { return $candidates[0] }
    }

    $command = Get-Command 'signtool.exe' -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    throw 'signtool.exe nao encontrado. Instale o Windows SDK (Windows SDK Signing Tools).'
}

function Assert-SignParameters {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Executavel nao encontrado: $Path"
    }

    if ([string]::IsNullOrWhiteSpace($PfxBase64)) {
        throw 'PEDALTRAINER_PFX_BASE64 nao definido: sem certificado nao ha o que assinar.'
    }

    if ([string]::IsNullOrEmpty($PfxPassword)) {
        throw 'PEDALTRAINER_PFX_PASSWORD nao definido.'
    }
}

Assert-SignParameters

$target = (Resolve-Path -LiteralPath $Path).Path
$signTool = Get-SignToolPath
$pfxPath = Join-Path ([IO.Path]::GetTempPath()) ("pedaltrainer-{0}.pfx" -f [guid]::NewGuid())
$rawBase64 = $PfxBase64 -replace '\s', ''

# 100 MB de teto: um .pfx real tem alguns KB, isso e so uma guarda contra lixo no secret.
if ($rawBase64.Length -gt 100MB) {
    throw 'PEDALTRAINER_PFX_BASE64 parece invalido (muito grande).'
}

try {
    [IO.File]::WriteAllBytes($pfxPath, [Convert]::FromBase64String($rawBase64))

    Write-Host "Assinando $target com $signTool"

    & $signTool sign /fd SHA256 /f $pfxPath /p $PfxPassword /tr $TimestampUrl /td SHA256 $target
    if ($LASTEXITCODE -ne 0) {
        throw "signtool sign falhou com codigo $LASTEXITCODE."
    }

    # /pa verifica a cadeia inteira, exatamente o que o SmartScreen faz.
    & $signTool verify /pa /v $target
    if ($LASTEXITCODE -ne 0) {
        throw "signtool verify falhou com codigo $LASTEXITCODE."
    }

    $signature = Get-AuthenticodeSignature -LiteralPath $target
    Write-Host ("Assinatura: {0} | Signatario: {1}" -f `
        $signature.Status, $signature.SignerCertificate.Subject)
}
finally {
    if (Test-Path -LiteralPath $pfxPath) {
        Remove-Item -LiteralPath $pfxPath -Force
    }
}
