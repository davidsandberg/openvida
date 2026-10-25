@echo off
set APP_SERVER=JBoss
if "%VIDA_HOME%" == "" set VIDA_HOME=%~dp0
if "%SETENV_SECOND_ENTRY%" == "yes" goto setenv_finish
set SETENV_SECOND_ENTRY=yes

if exist "%VIDA_HOME%jdk\bin\java.exe" set JAVA_HOME=%VIDA_HOME%jdk
set JVM_FLAGS=-ms64m -mx512m

:setenv_finish
