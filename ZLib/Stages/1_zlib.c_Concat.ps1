#concat files in the /1_zlib.c_SrcFiles folder. Candidates start with 3 nums and _ (000_)

$outFile = '1_zlib.c'
Clear-Content $outFile

foreach ($f in get-childitem './1_zlib.c_ToConcat' -File | where {$_.Name -match '^[0-9]{3}_' } | sort-object name)
{
    add-content -Path $outFile -Value "//############################################################################`r`n//########  $($f.Name)`r`n"
    get-content $f.FullName | add-content $outFile
}
