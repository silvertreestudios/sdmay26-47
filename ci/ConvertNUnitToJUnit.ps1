# NUnit3 to JUnit XML converter for GitLab CI
# Unity outputs NUnit3 format, but GitLab expects JUnit format.
param(
    [Parameter(Mandatory=$true)]
    [string]$InputFile,

    [Parameter(Mandatory=$true)]
    [string]$OutputFile
)

if (-not (Test-Path $InputFile)) {
    Write-Host "Input file not found: $InputFile"
    exit 1
}

[xml]$nunit = Get-Content $InputFile -Raw

$junitXml = '<?xml version="1.0" encoding="UTF-8"?>' + "`n"
$junitXml += '<testsuites>' + "`n"

$testCases = $nunit.SelectNodes("//test-case")

if ($testCases.Count -eq 0) {
    Write-Host "No test cases found in $InputFile"
    $junitXml += '</testsuites>'
    Set-Content -Path $OutputFile -Value $junitXml -Encoding UTF8
    exit 0
}

# Group test cases by classname
$groups = @{}
foreach ($tc in $testCases) {
    $className = $tc.classname
    if (-not $groups.ContainsKey($className)) {
        $groups[$className] = @()
    }
    $groups[$className] += $tc
}

foreach ($className in $groups.Keys) {
    $cases = $groups[$className]
    $total = $cases.Count
    $failures = ($cases | Where-Object { $_.result -eq "Failed" }).Count
    $errors = 0
    $time = ($cases | Measure-Object -Property duration -Sum).Sum

    $junitXml += "  <testsuite name=`"$className`" tests=`"$total`" failures=`"$failures`" errors=`"$errors`" time=`"$time`">`n"

    foreach ($tc in $cases) {
        $name = $tc.name
        $duration = $tc.duration
        $result = $tc.result

        $junitXml += "    <testcase classname=`"$className`" name=`"$name`" time=`"$duration`">`n"

        if ($result -eq "Failed") {
            $failureNode = $tc.SelectSingleNode("failure")
            if ($failureNode) {
                $msg = $failureNode.SelectSingleNode("message")
                $stack = $failureNode.SelectSingleNode("stack-trace")
                $msgText = if ($msg) { [System.Security.SecurityElement]::Escape($msg.InnerText) } else { "Test failed" }
                $stackText = if ($stack) { [System.Security.SecurityElement]::Escape($stack.InnerText) } else { "" }
                $junitXml += "      <failure message=`"$msgText`">$stackText</failure>`n"
            }
        }
        elseif ($result -eq "Skipped" -or $result -eq "Inconclusive") {
            $junitXml += "      <skipped />`n"
        }

        $junitXml += "    </testcase>`n"
    }

    $junitXml += "  </testsuite>`n"
}

$junitXml += '</testsuites>'

Set-Content -Path $OutputFile -Value $junitXml -Encoding UTF8
Write-Host "Converted $($testCases.Count) test cases from NUnit3 to JUnit format -> $OutputFile"
