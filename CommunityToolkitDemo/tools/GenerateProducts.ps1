<#
.SYNOPSIS
    生成大规模模拟商品数据，用于 10w 级性能与内存基准测试 / 手工验证。

.DESCRIPTION
    按 Data/products.md 的字段约定生成指定条数的商品记录。
    默认写入 Data/large/products.md，**不会覆盖** Data/products.md（47 行演示数据保持原样），
    因此既可以让基准测试读取大文件，也不会影响应用正常启动。

.PARAMETER Count
    生成的商品条数，默认 100000。

.PARAMETER OutputDir
    输出目录，默认 <仓库根>/Data/large。目录不存在会自动创建。

.EXAMPLE
    pwsh -File tools/GenerateProducts.ps1
    pwsh -File tools/GenerateProducts.ps1 -Count 100000 -OutputDir Data/large
#>
[CmdletBinding()]
param(
    [int]$Count = 100000,
    [string]$OutputDir = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Count -le 0) {
    throw "-Count 必须为正整数，当前值：$Count"
}

# 以脚本所在位置推导仓库根目录，保证在任何工作目录下都能正确定位
$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $repoRoot 'Data/large'
}
elseif (-not [System.IO.Path]::IsPathRooted($OutputDir)) {
    $OutputDir = Join-Path $repoRoot $OutputDir
}

if (-not (Test-Path -LiteralPath $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

$outputPath = Join-Path $OutputDir 'products.md'

$categories = @('drink', 'snack', 'dairy', 'fresh', 'staple', 'daily', 'liquor')
$units = @('瓶', '罐', '袋', '盒', '包', '斤', '提', '支')
$specs = @('550ml', '330ml', '1L', '散装', '500g', '250ml', '70g', '104g', '5kg', '750ml')
$names = @(
    '饮用天然水', '纯净水', '可乐', '冰红茶', '乌龙茶', '薯片', '夹心饼干', '每日坚果',
    '纯牛奶', '酸奶', '鲜牛奶', '全脂奶粉', '红富士苹果', '香蕉', '小青菜', '五花肉',
    '稻花香大米', '橄榄油', '红烧牛肉面', '抽纸', '洗衣液', '香皂', '牙膏', '啤酒', '干红葡萄酒'
)
$invariant = [System.Globalization.CultureInfo]::InvariantCulture

# 预分配：每行约 90~100 个字符，按 UTF-16 预留足够容量，避免 StringBuilder 反复扩容
$sb = [System.Text.StringBuilder]::new($Count * 128)

[void]$sb.AppendLine('# 商品主数据（大规模基准数据，由 tools/GenerateProducts.ps1 生成）')
[void]$sb.AppendLine()
[void]$sb.AppendLine('| Code | Name | CategoryId | Price | Barcode | Stock | WarnStock | Unit | Spec |')
[void]$sb.AppendLine('| ---- | ---- | ---------- | ----- | ------- | ----- | --------- | ---- | ---- |')

for ($i = 1; $i -le $Count; $i++) {
    $code = 'P{0:D6}' -f $i
    $name = '{0} {1}' -f $names[($i - 1) % $names.Count], $i
    $category = $categories[($i - 1) % $categories.Count]
    $price = ([math]::Round(1 + (($i * 37) % 19900) / 100.0, 2)).ToString('F2', $invariant)
    $barcode = '69{0:D11}' -f ($i % 100000000000)
    $stock = ($i * 17) % 501
    $warnStock = 5 + ($i % 26)
    $unit = $units[($i - 1) % $units.Count]
    $spec = $specs[($i - 1) % $specs.Count]

    [void]$sb.Append('| ').Append($code).Append(' | ').Append($name).Append(' | ').Append($category).Append(' | ')
    [void]$sb.Append($price).Append(' | ').Append($barcode).Append(' | ').Append($stock).Append(' | ')
    [void]$sb.Append($warnStock).Append(' | ').Append($unit).Append(' | ').Append($spec).AppendLine(' |')
}

$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllText($outputPath, $sb.ToString(), $utf8NoBom)
[void]$sb.Clear()

$file = Get-Item -LiteralPath $outputPath
Write-Host ('已生成 {0:N0} 条商品 -> {1}' -f $Count, $file.FullName)
Write-Host ('文件体积：{0:N2} MB（当前程序内 8 MB 上限：{1}）' -f ($file.Length / 1MB), $(if ($file.Length -gt 8MB) { '会触发跳过' } else { '不会触发跳过' }))
