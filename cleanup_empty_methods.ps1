$paths = Get-ChildItem -Path "e:\FPSLowPoly\Assets\Scripts" -Recurse -Filter *.cs
$regex1 = '(?m)^\s*(?:\[.*?\]\s*)*(?:public\s+|private\s+|protected\s+)?(?:virtual\s+|override\s+)?void\s+(?:Start|Update|FixedUpdate|LateUpdate|Awake|OnEnable|OnDisable)\s*\(\)\s*\{\s*\}\s*?\r?\n'
$regex2 = '(?m)^\s*(?:\[.*?\]\s*)*(?:public\s+|private\s+|protected\s+)?(?:virtual\s+|override\s+)?void\s+(?:Start|Update|FixedUpdate|LateUpdate|Awake|OnEnable|OnDisable)\s*\(\)\s*\r?\n\s*\{\s*\}\s*?\r?\n'
$count = 0
foreach ($file in $paths) {
    $content = Get-Content $file.FullName -Raw
    $newContent = [System.Text.RegularExpressions.Regex]::Replace($content, $regex1, '')
    $newContent = [System.Text.RegularExpressions.Regex]::Replace($newContent, $regex2, '')
    if ($content -ne $newContent) {
        Set-Content -Path $file.FullName -Value $newContent -NoNewline
        Write-Host "Cleaned up $($file.FullName)"
        $count++
    }
}
Write-Host "Total files cleaned: $count"
