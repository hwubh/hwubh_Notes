# 获取当前目录
$currentDir = Get-Location

# 查找所有包含 'commandline-tools-' 的 .zip 文件
$zipFiles = Get-ChildItem -Path $currentDir -Filter 'commandline-tools-*.zip'

# 遍历处理所有的 .zip文件
$zipFiles | ForEach-Object {
    # 使用正则表达式提取 'commandline-tools-' 后的部分，例如 linux-x64
    if ($_ -match 'commandline-tools-(.*-?).*.zip') {
        
        # 下载的'commandline-tools-*.zip'压缩包路径
        $sourceZipFilePath = $_.FullName

        # 生成的'openharmony-sdk-*.zip'压缩包路径
        $destinationZipFilePath = Join-Path -Path $currentDir -ChildPath ".zip"

        # 生成的临时文件夹路径
        $outputFolder = Join-Path -Path $currentDir -ChildPath 'temp'

        # 提取的文件夹路径
        $targetFolder = 'command-line-tools/sdk/default/openharmony/native/sysroot/usr/include/window_manager'

        # 需要删除的路径部分
        $removePart = '\command-line-tools\sdk\default\openharmony'
        
        # 加载 System.IO.Compression.FileSystem 程序集
        Add-Type -AssemblyName System.IO.Compression.FileSystem

        # 打开 'commandline-tools-*.zip'压缩包
        $sourceZip = [IO.Compression.ZipFile]::OpenRead($sourceZipFilePath)
        write-host $sourceZip.Entries[0]
        
        # 创建 'openharmony-sdk-*.zip'压缩包
        if (Test-Path $destinationZipFilePath) {
            Remove-Item -Path $destinationZipFilePath -Force
        }
        $destinationZip = [IO.Compression.ZipFile]::Open($destinationZipFilePath, [System.IO.Compression.ZipArchiveMode]::Create)

        # 获取 记录SDK版本的json文件
        $jsonEntry = $sourceZip.Entries | Where-Object { $_.FullName -like "command-line-tools/sdk/default/openharmony/ets/oh-uni-package.json"}

        # 默认API版本
        $apiVersion = 12

        if ($jsonEntry -ne $null) {

            # 创建一个临时文件来保存 JSON 文件
            $tempJsonPath = Join-Path -Path $outputFolder -ChildPath "oh-uni-package.json"

            # 将压缩包中的 JSON 文件提取到临时路径
            [IO.Compression.ZipFileExtensions]::ExtractToFile($jsonEntry, $tempJsonPath, $true)

            # 读取 JSON 文件的内容
            $jsonContent = Get-Content -Path $tempJsonPath -Raw | ConvertFrom-Json

            # 记录 API 版本
            $apiVersion = $jsonContent.apiVersion

            # 删除临时文件
            Remove-Item -Path $tempJsonPath -Force
        }

        # 更新输出文件夹路径
        $outputFolder = Join-Path -Path $outputFolder -ChildPath $apiVersion

        # 筛选目标文件夹中的所有文件
        $entries = $sourceZip.Entries | Where-Object { $_.FullName -like "$targetFolder/*" -and $_.FullName -ne "$targetFolder/" }

        # 提取文件
        $entries | ForEach-Object {
            # 跳过文件夹条目
            if ($_.FullName -match '/$') {
                return
            }

            $destinationPath = Join-Path -Path $outputFolder -ChildPath ($_.FullName -replace '^' + [regex]::Escape($targetFolder) + '/', '')
            $destinationPath = $destinationPath -replace [regex]::Escape($removePart), ''
            
            # 获取文件的父目录
            $destinationDir = Split-Path -Path $destinationPath

            # 如果目录不存在则创建
            if (-not (Test-Path $destinationDir)) {
                New-Item -ItemType Directory -Path $destinationDir -Force
            }

            # 提取文件到目标路径
            [IO.Compression.ZipFileExtensions]::ExtractToFile($_, $destinationPath, $true)

            # 输出文件的相对路径
            $relativePath = $destinationPath -replace [regex]::Escape($outputFolder), $apiVersion

            # 将文件添加到新的 ZIP 文件
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($destinationZip, $destinationPath, $relativePath)
        }
        
        # 释放资源
        $sourceZip.Dispose()
        $destinationZip.Dispose()
    }
}

# $currentDir = Split-Path -Path $MyInvocation.MyCommand.Definition -Parent
# $sourceZipFilePath = Join-Path -Path $currentDir -ChildPath 'commandline-tools-windows-x64-5.0.3.906.zip'
# $destinationZipFilePath = Join-Path -Path $currentDir -ChildPath 'openharmony-sdk-windows.zip'
# $outputFolder = Join-Path -Path $currentDir -ChildPath 'temp'
# $targetFolder = 'command-line-tools/sdk/default/openharmony/native/sysroot/usr/include/window_manager'
# $removePart = '\command-line-tools\sdk\default\openharmony'

# Add-Type -AssemblyName System.IO.Compression.FileSystem

# # 打开 ZIP 文件
# $sourceZip = [IO.Compression.ZipFile]::OpenRead($sourceZipFilePath)

# # 创建 ZIP 文件
# if (Test-Path $destinationZipFilePath) {
#     Remove-Item -Path $destinationZipFilePath -Force
# }
# $destinationZip = [IO.Compression.ZipFile]::Open($destinationZipFilePath, [System.IO.Compression.ZipArchiveMode]::Create)

# # 获取 记录SDK版本的json文件
# $jsonEntry = $sourceZip.Entries | Where-Object { $_.FullName -like "command-line-tools/sdk/default/openharmony/ets/oh-uni-package.json"}

# # 默认API版本
# $apiVersion = 12

# if ($jsonEntry -ne $null) {
#     # 创建一个临时文件来保存 JSON 文件
#     $tempJsonPath = Join-Path -Path $outputFolder -ChildPath "oh-uni-package.json"

#     # 将压缩包中的 JSON 文件提取到临时路径
#     [IO.Compression.ZipFileExtensions]::ExtractToFile($jsonEntry, $tempJsonPath, $true)

#     # 读取 JSON 文件的内容
#     $jsonContent = Get-Content -Path $tempJsonPath -Raw | ConvertFrom-Json

#     # 记录 API 版本
#     $apiVersion = $jsonContent.apiVersion

#     # 删除临时文件
#     Remove-Item -Path $tempJsonPath -Force
# }

# # 更新输出文件夹路径
# $outputFolder = Join-Path -Path $outputFolder -ChildPath $apiVersion

# # 筛选目标文件夹中的所有文件
# $entries = $sourceZip.Entries | Where-Object { $_.FullName -like "$targetFolder/*" -and $_.FullName -ne "$targetFolder/" }

# # 提取文件
# $entries | ForEach-Object {
#     # 跳过文件夹条目
#     if ($_.FullName -match '/$') {
#         return
#     }

#     $destinationPath = Join-Path -Path $outputFolder -ChildPath ($_.FullName -replace '^' + [regex]::Escape($targetFolder) + '/', '')
#     $destinationPath = $destinationPath -replace [regex]::Escape($removePart), ''
    
#     # 获取文件的父目录
#     $destinationDir = Split-Path -Path $destinationPath

#     # 如果目录不存在则创建
#     if (-not (Test-Path $destinationDir)) {
#         New-Item -ItemType Directory -Path $destinationDir -Force
#     }

#     # 提取文件到目标路径
#     [IO.Compression.ZipFileExtensions]::ExtractToFile($_, $destinationPath, $true)

#     # 输出文件的相对路径
#     $relativePath = $destinationPath -replace [regex]::Escape($outputFolder), $apiVersion

#     # 将文件添加到新的 ZIP 文件
#     [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($destinationZip, $destinationPath, $relativePath)
# }

# # 释放资源
# $sourceZip.Dispose()
# $destinationZip.Dispose()