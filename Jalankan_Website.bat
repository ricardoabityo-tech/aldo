@echo off
title Menjalankan Platform Legal Insight
echo ===============================================================================
echo            LEGAL INSIGHT - PLATFORM LAYANAN HUKUM DIGITAL INDONESIA
echo ===============================================================================
echo.
echo [1/2] Memeriksa dan mengompilasi kode program C#...

if exist "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" (
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /out:LegalInsight.exe Program.cs
) else (
    "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe" /nologo /out:LegalInsight.exe Program.cs
)

if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Kompilasi gagal. Silakan buka proyek melalui Visual Studio (LegalInsight.sln).
    pause
    exit /b %ERRORLEVEL%
)

echo [OK] Kompilasi C# berhasil!
echo.
echo [2/2] Memulai server web dan membuka browser...
echo.
start "" "http://localhost:5000"
LegalInsight.exe
