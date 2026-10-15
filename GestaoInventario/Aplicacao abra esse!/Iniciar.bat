@echo off
setlocal
cd /d "%~dp0"
if not exist "GestaoInventario.exe" (
  echo Nao foi encontrado GestaoInventario.exe. Extraia novamente o ZIP completo.
  pause
  exit /b 1
)
start "" "GestaoInventario.exe"
