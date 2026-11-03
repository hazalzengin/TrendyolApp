@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-IIS.ps1" %*
