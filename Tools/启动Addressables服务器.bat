@echo off
chcp 65001 >nul
cd /d "%~dp0.."

echo ==========================================================
echo   Addressables 本地资源服务器
echo ----------------------------------------------------------
echo   服务目录 : %CD%\ServerData
echo   访问地址 : http://localhost:8090/StandaloneWindows64/catalog_0.1.hash
echo   停止服务 : 按 Ctrl+C
echo ----------------------------------------------------------
echo   本机局域网 IP（Addressables 的 Remote.LoadPath 里要填这个）：
ipconfig | findstr /C:"IPv4"
echo ==========================================================
echo.

python -m http.server 8090 --bind 0.0.0.0 --directory ServerData

echo.
echo [服务器已停止]
pause
