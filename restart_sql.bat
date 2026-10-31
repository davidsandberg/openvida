@echo off
echo ============================================================
echo   Restarting Microsoft SQL Server (SQLEXPRESS)
echo ============================================================
echo.
net stop MSSQL$SQLEXPRESS
net start MSSQL$SQLEXPRESS
echo.
echo ============================================================
echo   SQL Server restarted! You can now launch OpenVIDA.
echo ============================================================
pause
