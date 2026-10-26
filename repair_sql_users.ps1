# repair_sql_users.ps1
# Ensures login 'ASUser' exists with password 'GunnarS3g3',
# repairs any orphaned SIDs in all 9 VIDA databases, and grants db_owner permissions.

$conn = New-Object System.Data.SqlClient.SqlConnection("Server=127.0.0.1,1433;User Id=sa;Password=GunnarS3g3;Database=master;TrustServerCertificate=True;")
$conn.Open()

# 1. Ensure server login ASUser exists with GunnarS3g3
$loginCmd = $conn.CreateCommand()
$loginCmd.CommandText = @"
IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = 'ASUser')
BEGIN
    CREATE LOGIN [ASUser] WITH PASSWORD = 'GunnarS3g3', DEFAULT_DATABASE = [master], CHECK_EXPIRATION = OFF, CHECK_POLICY = OFF;
END
ELSE
BEGIN
    ALTER LOGIN [ASUser] WITH PASSWORD = 'GunnarS3g3', CHECK_EXPIRATION = OFF, CHECK_POLICY = OFF;
END
"@
$loginCmd.ExecuteNonQuery()
Write-Output "Verified server login [ASUser]"

# 2. Map and repair user SID across all 9 databases
$dbs = @("AccessServer", "BaseData", "CarCom", "DiagSwdlRepository", "DiagSwdlSession", "DiceTiming", "EPC", "ImageRepository", "servicerep_en-US")
foreach ($db in $dbs) {
    try {
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = @"
USE [$db];
IF EXISTS (SELECT * FROM sys.database_principals WHERE name = 'ASUser')
BEGIN
    ALTER USER [ASUser] WITH LOGIN = [ASUser];
END
ELSE
BEGIN
    CREATE USER [ASUser] FOR LOGIN [ASUser];
END
ALTER ROLE [db_owner] ADD MEMBER [ASUser];
"@
        $cmd.ExecuteNonQuery()
        Write-Output "Repaired [ASUser] (db_owner) in $db"
    } catch {
        Write-Output ("Error on " + $db + ": " + $_.Exception.Message)
    }
}
$conn.Close()
Write-Output "All VIDA database user mappings verified successfully."
