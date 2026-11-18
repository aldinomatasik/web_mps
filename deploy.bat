@echo off
title Web MPS - Automated CI/CD Deploy Pipeline
cls
color 0B

echo.
echo   ==================================================================
echo    WEB MPS AUTOMATED PRODUCTION DEPLOYMENT ENGINE
echo    Target Server : 10.83.33.103 (\inetpub\wwwroot\Web MPS)
echo    Authentication: pmi\Administrator
echo   ==================================================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0deploy.ps1"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ==================================================================
    echo   [x] DEPLOYMENT FAILED (Exit Code: %ERRORLEVEL%)
    echo   Please inspect the error details above.
    echo ==================================================================
    echo.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo ==================================================================
echo   [+] ALL OPERATIONS COMPLETED SUCCESSFULLY!
echo ==================================================================
echo.
echo Press any key to close this terminal...
pause >nul
