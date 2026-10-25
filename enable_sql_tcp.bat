@echo off
echo ============================================================
echo Enabling SQL Server TCP/IP, Port 1433, and Mixed Mode
echo ============================================================
echo.

:: 1. Enable TCP/IP
reg add "HKLM\SOFTWARE\Microsoft\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQLServer\SuperSocketNetLib\Tcp" /v Enabled /t REG_DWORD /d 1 /f

:: 2. Set static TCP port 1433
reg add "HKLM\SOFTWARE\Microsoft\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQLServer\SuperSocketNetLib\Tcp\IPAll" /v TcpPort /t REG_SZ /d "1433" /f
reg add "HKLM\SOFTWARE\Microsoft\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQLServer\SuperSocketNetLib\Tcp\IPAll" /v TcpDynamicPorts /t REG_SZ /d "" /f

:: 3. Enable SQL Browser service
sc config SQLBrowser start= auto
net start SQLBrowser

:: 4. Restart SQL Server Express to apply changes
net stop MSSQL$SQLEXPRESS
net start MSSQL$SQLEXPRESS

echo.
echo ============================================================
echo Configuration Complete! You can close this window.
echo ============================================================
pause
